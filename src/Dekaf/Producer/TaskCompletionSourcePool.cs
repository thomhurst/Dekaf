using System.Collections.Concurrent;

namespace Dekaf.Producer;

/// <summary>
/// Thread-safe pool for TaskCompletionSource objects to reduce allocations in hot paths.
/// </summary>
/// <typeparam name="T">The result type of the TaskCompletionSource.</typeparam>
/// <remarks>
/// TaskCompletionSource can only be completed once, so after completion it must be "reset"
/// by creating a new instance. This pool manages the lifecycle of these objects.
/// Uses ConcurrentBag for lock-free thread-safe operations.
/// </remarks>
internal sealed class TaskCompletionSourcePool<T>
{
    private readonly ConcurrentBag<TaskCompletionSource<T>> _pool = new();
    private readonly TaskCreationOptions _creationOptions;

    /// <summary>
    /// Creates a new TaskCompletionSource pool.
    /// </summary>
    /// <param name="creationOptions">Options to use when creating TaskCompletionSource instances.</param>
    public TaskCompletionSourcePool(TaskCreationOptions creationOptions = TaskCreationOptions.RunContinuationsAsynchronously)
    {
        _creationOptions = creationOptions;
    }

    /// <summary>
    /// Rents a TaskCompletionSource from the pool, or creates a new one if the pool is empty.
    /// </summary>
    public TaskCompletionSource<T> Rent()
    {
        if (_pool.TryTake(out var tcs))
        {
            return tcs;
        }

        return new TaskCompletionSource<T>(_creationOptions);
    }

    /// <summary>
    /// Returns a TaskCompletionSource to the pool for reuse.
    /// The TaskCompletionSource must be in a completed state (Result/Exception/Canceled).
    /// </summary>
    public void Return(TaskCompletionSource<T> tcs)
    {
        // Create a new TCS instance to reset the state (TCS can only be completed once)
        // This is cheaper than allocating a new TCS every time in the hot path
        var newTcs = new TaskCompletionSource<T>(_creationOptions);
        _pool.Add(newTcs);
    }
}
