using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Dekaf.Internal;

namespace Dekaf.Producer;

/// <summary>
/// Non-generic companion providing constants and utility methods for <see cref="ValueTaskSourcePool{T}"/>.
/// </summary>
public static class ValueTaskSourcePool
{
    /// <summary>
    /// Minimum pool size used as a floor when auto-calculating.
    /// </summary>
    public const int MinAutoPoolSize = 256;

    /// <summary>
    /// Maximum pool size used as a ceiling when auto-calculating.
    /// </summary>
    public const int MaxAutoPoolSize = 65536;

    /// <summary>
    /// Fallback maximum pool size used when no producer options are available (e.g. parameterless constructor).
    /// </summary>
    internal const int FallbackMaxPoolSize = 4096;

    /// <summary>
    /// Conservative estimate of the number of messages per batch, used as a multiplier
    /// when calculating pool size. Each in-flight message holds a rented pool source,
    /// so the pool must be sized per message, not per batch.
    /// </summary>
    /// <remarks>
    /// With the default 1 MB batch size and typical ~1 KB messages, a batch holds ~1024 messages.
    /// This matches the estimate documented in CLAUDE.md ("Batch = 1MB default = ~1000 messages at 1KB each").
    /// </remarks>
    internal const int EstimatedMessagesPerBatch = 1024;

    /// <summary>
    /// Calculates an appropriate pool size based on the estimated number of concurrent in-flight messages.
    /// The pool is rented per <c>ProduceAsync</c> call (per message), so the formula accounts for
    /// both the number of in-flight batches and the estimated messages per batch:
    /// <c>(BufferMemory / BatchSize) * EstimatedMessagesPerBatch</c>, clamped to
    /// [<see cref="MinAutoPoolSize"/>, <see cref="MaxAutoPoolSize"/>].
    /// </summary>
    /// <param name="bufferMemory">Total producer buffer memory in bytes.</param>
    /// <param name="batchSize">Maximum batch size in bytes.</param>
    /// <returns>A pool size scaled to the expected concurrency level.</returns>
    public static int CalculatePoolSize(ulong bufferMemory, int batchSize)
        => PoolSizing.ForProducer(bufferMemory, batchSize).ValueTaskSources;
}

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
    private readonly ConcurrentStack<PooledValueTaskSource<T>> _pool = new();
    private readonly int _maxPoolSize;
    private int _poolCount; // Approximate count for bounded pool management
    private int _disposed;

    /// <summary>
    /// Creates a new pool with the default maximum size.
    /// </summary>
    public ValueTaskSourcePool() : this(ValueTaskSourcePool.FallbackMaxPoolSize)
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
        if (Volatile.Read(ref _disposed) != 0)
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
        if (Volatile.Read(ref _disposed) != 0)
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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return ValueTask.CompletedTask;

        // Clear the pool - instances will be garbage collected
        _pool.Clear();
        _poolCount = 0;

        return ValueTask.CompletedTask;
    }
}
