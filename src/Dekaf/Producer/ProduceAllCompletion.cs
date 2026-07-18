using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using Dekaf.Internal;

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
/// path is safe and avoids one thread-pool hop per acked message.
/// </para>
/// <para>
/// The aggregate continuation always runs asynchronously
/// (<see cref="ManualResetValueTaskSourceCore{T}.RunContinuationsAsynchronously"/> is true), so
/// user code awaiting <c>ProduceAllAsync</c> never executes on the sender loop.
/// </para>
/// <para>
/// On failure the exception with the smallest message index wins, matching the
/// <see cref="Task.WhenAll(Task[])"/> convention of surfacing the first faulted operation in input
/// order. All registered operations are always observed (their <c>GetResult</c> is invoked), so
/// pooled per-message sources are returned to their pool even when the aggregate faults.
/// </para>
/// </remarks>
internal sealed class ProduceAllCompletion : IValueTaskSource<RecordMetadata[]>
{
    private static readonly LockFreeStack<ProduceAllCompletion> s_pool = new(64);
    private static readonly LockFreeStack<PendingOp> s_opPool = new(4096);

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
        if (!s_pool.TryPop(out var completion))
            completion = new ProduceAllCompletion();

        completion._results = new RecordMetadata[count];
        // Registration guard: WaitAsync drops this extra count once all operations are
        // registered, so an early inline completion can never observe zero mid-registration.
        completion._remaining = 1;
        return completion;
    }

    /// <summary>
    /// Consumes the pending operation's <see cref="ValueTask{T}"/>: harvests inline when already
    /// completed, otherwise attaches a pooled continuation. Call at most once per index; this is
    /// the operation's single await for pooled-source purposes.
    /// </summary>
    public void Register(int index, ValueTask<RecordMetadata> pending)
    {
        var awaiter = pending.ConfigureAwait(false).GetAwaiter();
        if (awaiter.IsCompleted)
        {
            Harvest(index, awaiter, decrement: false);
            return;
        }

        Interlocked.Increment(ref _remaining);

        if (!s_opPool.TryPop(out var op))
            op = new PendingOp();
        op.Attach(this, index, awaiter);
    }

    /// <summary>
    /// Records a failure for <paramref name="index"/> when the produce call itself threw
    /// synchronously and there is no pending operation to register.
    /// </summary>
    public void RecordFailure(int index, Exception exception)
    {
        lock (_failureGate)
        {
            if (_firstException is null || index < _firstExceptionIndex)
            {
                _firstException = exception;
                _firstExceptionIndex = index;
            }
        }
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

    private void Harvest(int index, ConfiguredValueTaskAwaitable<RecordMetadata>.ConfiguredValueTaskAwaiter awaiter, bool decrement)
    {
        try
        {
            _results![index] = awaiter.GetResult();
        }
        catch (Exception ex)
        {
            RecordFailure(index, ex);
        }

        if (decrement)
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
            _results = null;
            _firstException = null;
            _firstExceptionIndex = 0;
            _remaining = 0;
            _core.Reset();
            s_pool.TryPush(this);
        }
    }

    ValueTaskSourceStatus IValueTaskSource<RecordMetadata[]>.GetStatus(short token)
        => _core.GetStatus(token);

    void IValueTaskSource<RecordMetadata[]>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);

    /// <summary>
    /// Pooled holder for one in-flight operation's continuation. Returns itself to the pool
    /// before harvesting so the instance is reusable immediately.
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

        public void Attach(ProduceAllCompletion owner, int index, ConfiguredValueTaskAwaitable<RecordMetadata>.ConfiguredValueTaskAwaiter awaiter)
        {
            _owner = owner;
            _index = index;
            _awaiter = awaiter;
            _awaiter.UnsafeOnCompleted(_run);
        }

        private void Run()
        {
            var owner = _owner!;
            var index = _index;
            var awaiter = _awaiter;

            _owner = null;
            _awaiter = default;
            s_opPool.TryPush(this);

            owner.Harvest(index, awaiter, decrement: true);
        }
    }
}
