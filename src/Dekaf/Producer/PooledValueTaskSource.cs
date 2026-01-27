using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace Dekaf.Producer;

/// <summary>
/// A poolable <see cref="IValueTaskSource{T}"/> implementation that wraps
/// <see cref="ManualResetValueTaskSourceCore{T}"/> for zero-allocation async completion.
/// </summary>
/// <remarks>
/// Unlike <see cref="TaskCompletionSource{T}"/>, this type can be reset and reused,
/// eliminating per-message allocations in the producer hot path.
///
/// Thread-safety: The completion methods (SetResult, SetException) are NOT thread-safe
/// and should only be called once per await cycle. The internal pool return is atomic.
/// </remarks>
/// <typeparam name="T">The result type.</typeparam>
public sealed class PooledValueTaskSource<T> : IValueTaskSource<T>
{
    private ManualResetValueTaskSourceCore<T> _core;
    private ValueTaskSourcePool<T>? _pool;
    private Action<T, Exception?>? _deliveryHandler;
    private int _hasCompleted; // 0 = not completed, 1 = completed

    /// <summary>
    /// Gets a <see cref="ValueTask{T}"/> bound to this source.
    /// </summary>
    public ValueTask<T> Task => new(this, _core.Version);

    /// <summary>
    /// Gets the current version token. Used to detect misuse (awaiting same source twice).
    /// </summary>
    public short Version => _core.Version;

    /// <summary>
    /// Associates this source with a pool for automatic return on completion.
    /// </summary>
    internal void SetPool(ValueTaskSourcePool<T> pool)
    {
        _pool = pool;
    }

    /// <summary>
    /// Sets a delivery handler to be invoked when the operation completes.
    /// The handler receives the result (or default) and any exception that occurred.
    /// The handler is cleared after invocation.
    /// </summary>
    /// <param name="handler">The handler to invoke on completion.</param>
    public void SetDeliveryHandler(Action<T, Exception?>? handler)
    {
        _deliveryHandler = handler;
    }

    /// <summary>
    /// Completes the operation with a successful result.
    /// </summary>
    /// <param name="result">The result value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetResult(T result)
    {
        Volatile.Write(ref _hasCompleted, 1);
        _core.SetResult(result);
    }

    /// <summary>
    /// Attempts to complete the operation with a successful result.
    /// Returns false if already completed.
    /// </summary>
    /// <param name="result">The result value.</param>
    /// <returns>True if the result was set; false if already completed.</returns>
    public bool TrySetResult(T result)
    {
        // Check our own flag first to avoid the race in ManualResetValueTaskSourceCore
        // where _result is set before _completed is checked
        if (Interlocked.CompareExchange(ref _hasCompleted, 1, 0) != 0)
        {
            return false;
        }

        _core.SetResult(result);
        return true;
    }

    /// <summary>
    /// Completes the operation with an exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetException(Exception exception)
    {
        Volatile.Write(ref _hasCompleted, 1);
        _core.SetException(exception);
    }

    /// <summary>
    /// Attempts to complete the operation with an exception.
    /// Returns false if already completed.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>True if the exception was set; false if already completed.</returns>
    public bool TrySetException(Exception exception)
    {
        // Check our own flag first to avoid the race in ManualResetValueTaskSourceCore
        // where _result is set before _completed is checked
        if (Interlocked.CompareExchange(ref _hasCompleted, 1, 0) != 0)
        {
            return false;
        }

        _core.SetException(exception);
        return true;
    }

    /// <summary>
    /// Attempts to complete the operation as canceled.
    /// Returns false if already completed.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if cancellation was set; false if already completed.</returns>
    public bool TrySetCanceled(CancellationToken cancellationToken)
    {
        // Check our own flag first to avoid the race in ManualResetValueTaskSourceCore
        // where _result is set before _completed is checked
        if (Interlocked.CompareExchange(ref _hasCompleted, 1, 0) != 0)
        {
            return false;
        }

        _core.SetException(new OperationCanceledException(cancellationToken));
        return true;
    }

    /// <summary>
    /// Gets the result of the operation. Called by the awaiter.
    /// After this call, the source is reset and returned to the pool.
    /// </summary>
    T IValueTaskSource<T>.GetResult(short token)
    {
        // Capture values before reset
        var handler = _deliveryHandler;
        var pool = _pool;

        try
        {
            var result = _core.GetResult(token);

            // Invoke delivery handler if set (for fire-and-forget with callback)
            handler?.Invoke(result, null);

            return result;
        }
        catch (Exception ex)
        {
            // Invoke delivery handler with exception (default value for error case)
            handler?.Invoke(default!, ex);
            throw;
        }
        finally
        {
            // Clear handler before returning to pool
            _deliveryHandler = null;

            // Reset completion flag and core for reuse
            Volatile.Write(ref _hasCompleted, 0);
            _core.Reset();
            pool?.Return(this);
        }
    }

    /// <summary>
    /// Gets the status of the operation. Called by the awaiter.
    /// </summary>
    ValueTaskSourceStatus IValueTaskSource<T>.GetStatus(short token)
    {
        return _core.GetStatus(token);
    }

    /// <summary>
    /// Schedules continuation. Called by the awaiter.
    /// </summary>
    void IValueTaskSource<T>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _core.OnCompleted(continuation, state, token, flags);
    }
}
