using Dekaf.Internal;
using Dekaf.Producer;

namespace Dekaf.Consumer;

/// <summary>
/// A transactional producer whose transactions carry a partitioned run's offset commits, for
/// <see cref="PartitionCommitPolicy.CommitCompletedInTransaction"/>.
/// </summary>
/// <remarks>
/// Create one with <see cref="Create{TKey, TValue}"/> from a producer configured with a
/// transactional ID, after calling <see cref="IKafkaProducer{TKey, TValue}.InitTransactionsAsync(CancellationToken)"/>.
/// The run begins, commits and aborts the producer's transactions itself, so the application must
/// not call <see cref="IKafkaProducer{TKey, TValue}.BeginTransaction"/> on it while the run is active.
/// Handlers produce with the producer's own <c>ProduceAsync</c>, and those records join the open transaction.
/// </remarks>
public sealed class PartitionedTransactionalProducer
{
    private readonly Func<IPartitionedTransaction> _beginTransaction;

    internal PartitionedTransactionalProducer(Func<IPartitionedTransaction> beginTransaction)
    {
        _beginTransaction = beginTransaction;
    }

    /// <summary>
    /// Wraps a transactional producer for <see cref="PartitionedProcessingOptions.TransactionalProducer"/>.
    /// </summary>
    /// <param name="producer">A producer with a transactional ID whose transactions are initialized.</param>
    public static PartitionedTransactionalProducer Create<TKey, TValue>(IKafkaProducer<TKey, TValue> producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        return new PartitionedTransactionalProducer(() => new Transaction<TKey, TValue>(producer.BeginTransaction()));
    }

    internal IPartitionedTransaction BeginTransaction() => _beginTransaction();

    private sealed class Transaction<TKey, TValue>(ITransaction<TKey, TValue> transaction) : IPartitionedTransaction
    {
        public async ValueTask CommitAsync(
            IReadOnlyList<TopicPartitionOffset> offsets,
            ConsumerGroupMetadata consumerGroupMetadata,
            CancellationToken cancellationToken)
        {
            await transaction.SendOffsetsToTransactionAsync(offsets, consumerGroupMetadata, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            await transaction.DisposeAsync().ConfigureAwait(false);
        }

        public async ValueTask AbortAsync(CancellationToken cancellationToken)
        {
            await transaction.AbortAsync(cancellationToken).ConfigureAwait(false);
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>One open transaction of a <see cref="PartitionedTransactionalProducer"/>.</summary>
internal interface IPartitionedTransaction
{
    /// <summary>Sends <paramref name="offsets"/> to the transaction and commits it.</summary>
    ValueTask CommitAsync(
        IReadOnlyList<TopicPartitionOffset> offsets,
        ConsumerGroupMetadata consumerGroupMetadata,
        CancellationToken cancellationToken);

    ValueTask AbortAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Stops partition processors from reading queued records while a transaction commits, so every
/// record a processor read before the commit completes inside the transaction its output joined.
/// Only the runtime loop closes and opens the gate; processors enter it around each read.
/// </summary>
internal sealed class TransactionReadGate : IDisposable
{
    private const int ClosedFlag = 1 << 30;
    private const int ReaderMask = ClosedFlag - 1;
    private static readonly TaskCompletionSource OpenSource = CreateOpenSource();

    // Low bits count processors inside a read; ClosedFlag stops new reads.
    private int _state;
    private TaskCompletionSource _opened = OpenSource;
    private readonly AsyncAutoResetSignal _progress = new();

    public bool IsOpen => (Volatile.Read(ref _state) & ClosedFlag) == 0;

    /// <summary>Enters a read, or returns false while the gate is closed.</summary>
    public bool TryEnter()
    {
        var state = Volatile.Read(ref _state);
        while ((state & ClosedFlag) == 0)
        {
            var observed = Interlocked.CompareExchange(ref _state, state + 1, state);
            if (observed == state)
            {
                return true;
            }
            state = observed;
        }

        return false;
    }

    public void Exit() => Interlocked.Decrement(ref _state);

    /// <summary>Completes when the gate is open. Allocates only while the gate is closed.</summary>
    public async ValueTask WaitOpenAsync(CancellationToken cancellationToken)
    {
        while (!IsOpen)
        {
            await Volatile.Read(ref _opened).Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Stops new reads and waits for reads already inside the gate, which never block.</summary>
    public void Close()
    {
        // Publish the wait source before the flag: a reader that sees the flag reads this source.
        Volatile.Write(ref _opened, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        var state = Volatile.Read(ref _state);
        while (true)
        {
            var observed = Interlocked.CompareExchange(ref _state, state | ClosedFlag, state);
            if (observed == state)
            {
                break;
            }
            state = observed;
        }

        var spinner = new SpinWait();
        while ((Volatile.Read(ref _state) & ReaderMask) != 0)
        {
            spinner.SpinOnce();
        }
    }

    public void Open()
    {
        var state = Volatile.Read(ref _state);
        if ((state & ClosedFlag) == 0)
        {
            return;
        }

        while (true)
        {
            var observed = Interlocked.CompareExchange(ref _state, state & ~ClosedFlag, state);
            if (observed == state)
            {
                break;
            }
            state = observed;
        }

        Volatile.Read(ref _opened).TrySetResult();
    }

    /// <summary>Wakes the runtime loop waiting for completions while the gate is closed.</summary>
    public void NotifyProgress()
    {
        if (!IsOpen)
        {
            _progress.Signal();
        }
    }

    /// <summary>Waits for <see cref="NotifyProgress"/>; returns false on timeout. Only the runtime loop waits.</summary>
    public ValueTask<bool> WaitForProgressAsync(int timeoutMs) => _progress.WaitAsync(timeoutMs);

    public void Dispose() => _progress.Dispose();

    private static TaskCompletionSource CreateOpenSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }
}
