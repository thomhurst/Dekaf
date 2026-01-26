using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Dekaf.Producer;

/// <summary>
/// Thread-safe bounded pool for <see cref="PooledValueTaskSource{T}"/> instances.
/// Uses lock-free operations via <see cref="ConcurrentStack{T}"/> for high throughput.
/// </summary>
/// <remarks>
/// <para>
/// Unlike TaskCompletionSource which cannot be reset or reused, this pool actually
/// reuses instances because <see cref="PooledValueTaskSource{T}"/> wraps a resettable
/// <see cref="System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore{T}"/>.
/// </para>
/// <para>
/// The pool has a configurable maximum size. When the pool is empty, new instances are created.
/// When returning an instance to a full pool, the instance is discarded (let GC handle it).
/// This bounded approach prevents unbounded memory growth while still reducing allocations
/// in typical workloads.
/// </para>
/// <para>
/// Note: The pool count is approximate due to lock-free operations. Under high contention,
/// the pool may temporarily contain slightly more or fewer items than <see cref="MaxPoolSize"/>.
/// This is intentional to avoid locks in the hot path and has no correctness impact.
/// </para>
/// </remarks>
/// <typeparam name="T">The result type of the value task sources.</typeparam>
public sealed class ValueTaskSourcePool<T> : IAsyncDisposable
{
    /// <summary>
    /// Default maximum number of pooled instances.
    /// Increased from 1024 to 4096 to reduce allocations in high-throughput scenarios.
    /// </summary>
    public const int DefaultMaxPoolSize = 4096;

    private readonly ConcurrentStack<PooledValueTaskSource<T>> _pool = new();
    private readonly int _maxPoolSize;
    private int _poolCount; // Approximate count for bounded pool management
    private volatile bool _disposed;

    /// <summary>
    /// Creates a new pool with the default maximum size.
    /// </summary>
    public ValueTaskSourcePool() : this(DefaultMaxPoolSize)
    {
    }

    /// <summary>
    /// Creates a new pool with a specified maximum size.
    /// </summary>
    /// <param name="maxPoolSize">Maximum number of instances to keep in the pool.</param>
    public ValueTaskSourcePool(int maxPoolSize)
    {
        if (maxPoolSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPoolSize), "Max pool size must be positive.");

        _maxPoolSize = maxPoolSize;
    }

    /// <summary>
    /// Gets a <see cref="PooledValueTaskSource{T}"/> from the pool, or creates a new one if empty.
    /// The returned instance is associated with this pool and will auto-return on completion.
    /// </summary>
    /// <returns>A <see cref="PooledValueTaskSource{T}"/> ready for use.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledValueTaskSource<T> Rent()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ValueTaskSourcePool<T>));

        if (_pool.TryPop(out var source))
        {
            // Decrement approximate count (may be slightly off due to races, but that's fine)
            Interlocked.Decrement(ref _poolCount);
            return source;
        }

        // Pool empty - create new instance
        var newSource = new PooledValueTaskSource<T>();
        newSource.SetPool(this);
        return newSource;
    }

    /// <summary>
    /// Returns a <see cref="PooledValueTaskSource{T}"/> to the pool for reuse.
    /// If the pool is full, the instance is discarded.
    /// </summary>
    /// <remarks>
    /// This method is typically called automatically by <see cref="PooledValueTaskSource{T}"/>
    /// after GetResult() is invoked (when the await completes).
    /// </remarks>
    /// <param name="source">The source to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(PooledValueTaskSource<T> source)
    {
        if (_disposed)
            return; // Silently discard after disposal

        // Check approximate count to avoid unbounded growth
        // Use Interlocked.Increment first to "reserve" a slot, then check if over limit
        var count = Interlocked.Increment(ref _poolCount);

        if (count <= _maxPoolSize)
        {
            _pool.Push(source);
        }
        else
        {
            // Pool is full - decrement count and let GC handle the instance
            Interlocked.Decrement(ref _poolCount);
            // Instance is not pushed, will be garbage collected
        }
    }

    /// <summary>
    /// Gets the approximate number of instances currently in the pool.
    /// This is an approximation due to lock-free operations.
    /// </summary>
    public int ApproximateCount => Volatile.Read(ref _poolCount);

    /// <summary>
    /// Gets the maximum pool size.
    /// </summary>
    public int MaxPoolSize => _maxPoolSize;

    /// <summary>
    /// Disposes the pool. Outstanding instances can still complete but won't be returned to the pool.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _disposed = true;

        // Clear the pool - instances will be garbage collected
        _pool.Clear();
        _poolCount = 0;

        return ValueTask.CompletedTask;
    }
}
