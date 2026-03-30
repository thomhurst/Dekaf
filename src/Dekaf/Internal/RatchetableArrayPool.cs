using System.Buffers;

namespace Dekaf.Internal;

/// <summary>
/// An <see cref="ArrayPool{T}"/> wrapper that supports monotonically-increasing bucket depth.
/// When <see cref="RatchetBucketCapacity"/> is called with a value greater than the current depth,
/// a new <c>ConfigurableArrayPool</c> is created and atomically swapped in. The old pool instance
/// becomes GC-eligible once outstanding rentals are returned.
/// </summary>
internal sealed class RatchetableArrayPool<T>
{
    private readonly int _maxArrayLength;
    private readonly Lock _ratchetLock = new();
    private ArrayPool<T> _pool;
    private int _currentArraysPerBucket;

    internal RatchetableArrayPool(int maxArrayLength, int initialArraysPerBucket)
    {
        _maxArrayLength = maxArrayLength;
        _currentArraysPerBucket = initialArraysPerBucket;
        _pool = ArrayPool<T>.Create(maxArrayLength, initialArraysPerBucket);
    }

    /// <summary>
    /// Gets the current pool instance. Uses <see cref="Volatile.Read{T}(ref T)"/> to ensure
    /// visibility of pool replacements from <see cref="RatchetBucketCapacity"/>.
    /// </summary>
    internal ArrayPool<T> Pool => Volatile.Read(ref _pool);

    /// <summary>
    /// Gets the current per-bucket array capacity. Uses <see cref="Volatile.Read(ref int)"/>
    /// for cross-thread visibility.
    /// </summary>
    internal int CurrentArraysPerBucket => Volatile.Read(ref _currentArraysPerBucket);

    /// <summary>
    /// Increases the per-bucket array capacity if <paramref name="arraysPerBucket"/> exceeds
    /// the current value. Uses double-checked locking to ensure only one replacement occurs
    /// per threshold crossing. The ratchet is monotonically increasing — it never shrinks.
    /// </summary>
    internal void RatchetBucketCapacity(int arraysPerBucket)
    {
        if (arraysPerBucket <= Volatile.Read(ref _currentArraysPerBucket))
            return;

        lock (_ratchetLock)
        {
            if (arraysPerBucket <= _currentArraysPerBucket)
                return;

            Volatile.Write(ref _pool, ArrayPool<T>.Create(_maxArrayLength, arraysPerBucket));
            Volatile.Write(ref _currentArraysPerBucket, arraysPerBucket);
        }
    }
}
