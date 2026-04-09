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
    /// Calculates an appropriate pool size based on the estimated number of concurrent in-flight messages.
    /// Delegates to <see cref="PoolSizing.ForProducer"/> which centralizes all pool size derivation.
    /// </summary>
    /// <param name="bufferMemory">Total producer buffer memory in bytes.</param>
    /// <param name="batchSize">Maximum batch size in bytes.</param>
    /// <returns>A pool size scaled to the expected concurrency level.</returns>
    public static int CalculatePoolSize(ulong bufferMemory, int batchSize)
        => PoolSizing.ForProducer(bufferMemory, batchSize).ValueTaskSources;
}

/// <summary>
/// Thread-safe bounded pool for <see cref="PooledValueTaskSource{T}"/> instances.
/// Uses a pre-allocated array with CAS-guarded index for zero-allocation Rent/Return.
/// </summary>
/// <remarks>
/// <para>
/// Unlike TaskCompletionSource which cannot be reset or reused, this pool actually
/// reuses instances because <see cref="PooledValueTaskSource{T}"/> wraps a resettable
/// <see cref="System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore{T}"/>.
/// </para>
/// <para>
/// The previous ConcurrentStack implementation allocated a ~32-byte Node per Push call.
/// Every ProduceAsync flows through this pool, so at high throughput (millions/sec) the
/// Node allocations promoted to Gen2 and caused a GC feedback loop. This array-based
/// CAS stack eliminates all per-operation allocations — only the fixed-size array is allocated
/// at construction time.
/// </para>
/// <para>
/// The pool has a configurable maximum size. When the pool is empty, new instances are created.
/// When returning an instance to a full pool, the instance is discarded (let GC handle it).
/// This bounded approach prevents unbounded memory growth while still reducing allocations
/// in typical workloads.
/// </para>
/// </remarks>
/// <typeparam name="T">The result type of the value task sources.</typeparam>
public sealed class ValueTaskSourcePool<T> : IAsyncDisposable
{
    private readonly LockFreeStack<PooledValueTaskSource<T>> _stack;
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

        _stack = new LockFreeStack<PooledValueTaskSource<T>>(maxPoolSize);
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

        if (_stack.TryPop(out var source))
            return source;

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

        _stack.TryPush(source);
    }

    /// <summary>
    /// Gets the approximate number of instances currently in the pool.
    /// </summary>
    public int ApproximateCount => _stack.Count;

    /// <summary>
    /// Gets the maximum pool size.
    /// </summary>
    public int MaxPoolSize => _stack.Capacity;

    /// <summary>
    /// Disposes the pool. Outstanding instances can still complete but won't be returned to the pool.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return ValueTask.CompletedTask;

        _stack.Clear();

        return ValueTask.CompletedTask;
    }
}
