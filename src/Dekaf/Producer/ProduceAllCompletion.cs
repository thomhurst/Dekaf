using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace Dekaf.Producer;

/// <summary>
/// Pooled counting completion used by <c>ProduceAllAsync</c> to await many produce operations
/// without allocating a <see cref="Task"/> per message.
/// </summary>
/// <remarks>
/// <para>
/// Each registered produce operation attaches a pooled continuation that stores its result (or
/// records its failure) and decrements a shared counter; the operation that brings the counter to
/// zero completes the single aggregate <see cref="IValueTaskSource{T}"/>. Per-message continuations
/// are bounded (array store + decrement), which is why <c>ProduceAllAsync</c> can produce with
/// <c>runContinuationsAsynchronously: false</c>: running them inline on the broker sender's ack
/// path is safe and avoids one thread-pool hop per acked message. This only holds when the
/// harvest is the source's direct continuation — the producer upgrades to asynchronous
/// continuations whenever an instrumented or retry wrapper (<c>AwaitWithMetrics</c>,
/// <c>AwaitWithActivity</c>, <c>ProduceAsyncWithRetry</c>) interposes its own state machine,
/// so metric listeners, activity export, and retry-policy code never run on the ack thread.
/// </para>
/// <para>
/// The aggregate continuation always runs asynchronously
/// (<see cref="ManualResetValueTaskSourceCore{T}.RunContinuationsAsynchronously"/> is true), so
/// user code awaiting <c>ProduceAllAsync</c> never executes on the sender loop.
/// </para>
/// <para>
/// On failure a non-cancellation fault always beats an <see cref="OperationCanceledException"/>
/// (matching <see cref="Task.WhenAll(Task[])"/>, which faults whenever any child faults, even when
/// other children were cancelled); among exceptions of the same class the smallest message index
/// wins, surfacing the first failed operation in input order. All registered operations are always
/// observed (their <c>GetResult</c> is invoked), so
/// pooled per-message sources are returned to their pool even when the aggregate faults.
/// </para>
/// </remarks>
internal sealed class ProduceAllCompletion : IValueTaskSource<RecordMetadata[]>
{
    private static readonly CompletionPool s_pool = new();
    private static readonly PendingOpPool s_opPool = new();

    private ManualResetValueTaskSourceCore<RecordMetadata[]> _core = new() { RunContinuationsAsynchronously = true };
    private readonly System.Threading.Lock _failureGate = new();
    private RecordMetadata[]? _results;
    private Exception? _firstException;
    private int _firstExceptionIndex;
    private int _remaining;

    /// <summary>
    /// Rents a completion sized for <paramref name="count"/> operations. Must be finished with
    /// <see cref="WaitAsync"/>, whose await resets the instance and returns it to the pool.
    /// </summary>
    public static ProduceAllCompletion Rent(int count)
    {
        var completion = s_pool.Rent();
        completion._results = new RecordMetadata[count];
        // count operations plus a registration guard that WaitAsync drops, so an early inline
        // completion can never observe zero mid-registration. Pre-counting keeps the
        // registration loop free of interlocked operations.
        completion._remaining = count + 1;
        return completion;
    }

    /// <summary>
    /// Consumes the pending operation's <see cref="ValueTask{T}"/>: harvests inline when already
    /// completed, otherwise attaches a pooled continuation. Call exactly once per index; this is
    /// the operation's single await for pooled-source purposes.
    /// </summary>
    public void Register(int index, in ValueTask<RecordMetadata> pending)
    {
        var awaiter = pending.ConfigureAwait(false).GetAwaiter();
        if (awaiter.IsCompleted)
        {
            Harvest(index, in awaiter);
            return;
        }

        s_opPool.Rent().Attach(this, index, in awaiter);
    }

    /// <summary>
    /// Records a failure for <paramref name="index"/> when the produce call itself threw
    /// synchronously and there is no pending operation to register.
    /// </summary>
    public void RecordFailure(int index, Exception exception)
    {
        lock (_failureGate)
        {
            if (_firstException is null || ShouldReplace(_firstException, _firstExceptionIndex, exception, index))
            {
                _firstException = exception;
                _firstExceptionIndex = index;
            }
        }
    }

    /// <summary>
    /// Faults beat cancellations (matching <see cref="Task.WhenAll(Task[])"/>, where any faulted
    /// child makes the aggregate fault regardless of cancelled siblings); within the same class
    /// the smallest message index wins.
    /// </summary>
    private static bool ShouldReplace(Exception current, int currentIndex, Exception candidate, int candidateIndex)
    {
        var currentIsCancellation = current is OperationCanceledException;
        var candidateIsCancellation = candidate is OperationCanceledException;

        if (currentIsCancellation != candidateIsCancellation)
            return currentIsCancellation;

        return candidateIndex < currentIndex;
    }

    /// <summary>
    /// Removes operations that will never be registered because a synchronous produce failure
    /// aborted the registration loop. <paramref name="unregisteredCount"/> is the number of
    /// messages (including the one that threw) that will never decrement the counter.
    /// </summary>
    public void AbortRegistration(int unregisteredCount)
    {
        // Cannot reach zero here: the registration guard from Rent is still held.
        Interlocked.Add(ref _remaining, -unregisteredCount);
    }

    /// <summary>
    /// Completes registration and returns the aggregate task. Await exactly once.
    /// </summary>
    public ValueTask<RecordMetadata[]> WaitAsync()
    {
        var version = _core.Version;
        OnOperationCompleted(); // drop the registration guard
        return new ValueTask<RecordMetadata[]>(this, version);
    }

    private void Harvest(int index, in ConfiguredValueTaskAwaitable<RecordMetadata>.ConfiguredValueTaskAwaiter awaiter)
    {
        try
        {
            _results![index] = awaiter.GetResult();
        }
        catch (Exception ex)
        {
            RecordFailure(index, ex);
        }

        OnOperationCompleted();
    }

    private void OnOperationCompleted()
    {
        if (Interlocked.Decrement(ref _remaining) != 0)
            return;

        // Last completion (or registration guard when everything finished inline).
        // The Interlocked.Decrement fences make all _results writes and RecordFailure
        // updates visible here.
        var exception = _firstException;
        if (exception is not null)
            _core.SetException(exception);
        else
            _core.SetResult(_results!);
    }

    RecordMetadata[] IValueTaskSource<RecordMetadata[]>.GetResult(short token)
    {
        try
        {
            return _core.GetResult(token);
        }
        finally
        {
            s_pool.Return(this);
        }
    }

    ValueTaskSourceStatus IValueTaskSource<RecordMetadata[]>.GetStatus(short token)
        => _core.GetStatus(token);

    void IValueTaskSource<RecordMetadata[]>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);

    private sealed class CompletionPool() : ObjectPool<ProduceAllCompletion>(64)
    {
        protected override ProduceAllCompletion Create() => new();

        protected override void Reset(ProduceAllCompletion item)
        {
            item._results = null;
            item._firstException = null;
            item._firstExceptionIndex = 0;
            item._remaining = 0;
            item._core.Reset();
        }
    }

    /// <summary>
    /// Pooled holder for one in-flight operation's continuation. Harvests from its own fields
    /// and only then returns itself to the pool, so the awaiter is never copied to a local.
    /// </summary>
    private sealed class PendingOp
    {
        private readonly Action _run;
        private ProduceAllCompletion? _owner;
        private int _index;
        private ConfiguredValueTaskAwaitable<RecordMetadata>.ConfiguredValueTaskAwaiter _awaiter;

        public PendingOp()
        {
            _run = Run;
        }

        public void Attach(ProduceAllCompletion owner, int index, in ConfiguredValueTaskAwaitable<RecordMetadata>.ConfiguredValueTaskAwaiter awaiter)
        {
            _owner = owner;
            _index = index;
            _awaiter = awaiter;
            _awaiter.UnsafeOnCompleted(_run);
        }

        private void Run()
        {
            _owner!.Harvest(_index, in _awaiter);
            s_opPool.Return(this);
        }

        internal void ResetForPool()
        {
            _owner = null;
            _awaiter = default;
        }
    }

    private sealed class PendingOpPool() : ObjectPool<PendingOp>(ValueTaskSourcePool.FallbackMaxPoolSize)
    {
        protected override PendingOp Create() => new();

        protected override void Reset(PendingOp item) => item.ResetForPool();
    }
}
