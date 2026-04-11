using System.Runtime.CompilerServices;
using Dekaf.Internal;

namespace Dekaf.Producer;

/// <summary>
/// Thread-safe bounded pool for <see cref="PendingAppend"/> instances.
/// Uses <see cref="LockFreeStack{T}"/> for zero-allocation Rent/Return in steady state.
/// </summary>
/// <remarks>
/// Follows the same pattern as <see cref="ValueTaskSourcePool{T}"/>.
/// When the pool is empty, new instances are created on demand.
/// When returning to a full pool, instances are discarded (GC reclaims).
/// Pre-warming fills the pool at construction time to avoid cold-start allocations.
/// </remarks>
internal sealed class PendingAppendPool
{
    private readonly LockFreeStack<PendingAppend> _stack;

    /// <summary>
    /// Creates a new pool with the specified maximum size.
    /// </summary>
    /// <param name="maxPoolSize">Maximum number of instances to keep in the pool.</param>
    public PendingAppendPool(int maxPoolSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPoolSize);
        _stack = new LockFreeStack<PendingAppend>(maxPoolSize);
    }

    /// <summary>
    /// Gets a <see cref="PendingAppend"/> from the pool, or creates a new one if empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PendingAppend Rent()
    {
        if (_stack.TryPop(out var item))
            return item;

        return new PendingAppend();
    }

    /// <summary>
    /// Returns a <see cref="PendingAppend"/> to the pool for reuse.
    /// If the pool is full, the instance is discarded.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(PendingAppend item)
    {
        _stack.TryPush(item);
    }

    /// <summary>
    /// Pre-allocates instances to avoid cold-start allocation bursts.
    /// </summary>
    /// <param name="count">Number of instances to pre-allocate.</param>
    public void PreWarm(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _stack.TryPush(new PendingAppend());
        }
    }

    /// <summary>
    /// Gets the approximate number of instances currently in the pool.
    /// </summary>
    public int ApproximateCount => _stack.Count;
}
