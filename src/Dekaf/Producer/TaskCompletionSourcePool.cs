namespace Dekaf.Producer;

/// <summary>
/// Factory for TaskCompletionSource objects.
/// </summary>
/// <typeparam name="T">The result type of the TaskCompletionSource.</typeparam>
/// <remarks>
/// Previously this was a pool, but TaskCompletionSource can only be completed once
/// and cannot be reset. The previous "pool" created a new TCS on every Return(),
/// which didn't actually reduce allocations - it just moved them from Rent to Return.
/// This simplified implementation allocates on Rent() which has the same allocation
/// count but with less complexity and overhead from ConcurrentBag operations.
/// </remarks>
internal sealed class TaskCompletionSourcePool<T> : IAsyncDisposable
{
    private readonly TaskCreationOptions _creationOptions;
    private readonly Action<TaskCompletionSource<T>> _returnCallback;
    private volatile bool _disposed;

    /// <summary>
    /// Creates a new TaskCompletionSource factory.
    /// </summary>
    /// <param name="creationOptions">Options to use when creating TaskCompletionSource instances.</param>
    public TaskCompletionSourcePool(TaskCreationOptions creationOptions = TaskCreationOptions.RunContinuationsAsynchronously)
    {
        _creationOptions = creationOptions;
        _returnCallback = Return; // Cache delegate to avoid allocation on every access
    }

    /// <summary>
    /// Gets the return callback that can be passed to components that need to signal completion.
    /// This delegate is cached to avoid allocation on every access.
    /// </summary>
    public Action<TaskCompletionSource<T>> ReturnCallback => _returnCallback;

    /// <summary>
    /// Creates a new TaskCompletionSource.
    /// </summary>
    public TaskCompletionSource<T> Rent()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TaskCompletionSourcePool<T>));

        return new TaskCompletionSource<T>(_creationOptions);
    }

    /// <summary>
    /// No-op for API compatibility. The TCS will be garbage collected.
    /// </summary>
    public void Return(TaskCompletionSource<T> tcs)
    {
        // No-op - TCS cannot be reused, let GC handle it
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
