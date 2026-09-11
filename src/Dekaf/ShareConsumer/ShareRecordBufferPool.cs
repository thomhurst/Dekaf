using System.Buffers;
using System.Numerics;

namespace Dekaf.ShareConsumer;

/// <summary>
/// Caches copied/decompressed payload arrays across serialized share-consumer operations.
/// Retained array capacity is bounded by one configured fetch; outstanding borrowed arrays are
/// never reclaimed to satisfy the cache limit. Disposal drops late returns into the shared pool.
/// </summary>
internal sealed class ShareRecordBufferPool(int maximumRetainedBytes) : ArrayPool<byte>, IDisposable
{
    private readonly Stack<byte[]>?[] _buckets = new Stack<byte[]>?[31];
    private int _retainedBytes;
    private bool _disposed;

    internal int RetainedBytes => _retainedBytes;

    public override byte[] Rent(int minimumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (minimumLength > 0 && minimumLength <= maximumRetainedBytes && minimumLength <= 1 << 30)
        {
            var bucket = BitOperations.Log2(BitOperations.RoundUpToPowerOf2((uint)Math.Max(16, minimumLength)));
            if (_buckets[bucket] is { Count: > 0 } arrays)
            {
                var array = arrays.Pop();
                _retainedBytes -= array.Length;
                return array;
            }
        }
        return Shared.Rent(minimumLength);
    }

    public override void Return(byte[] array, bool clearArray = false)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (array.Length == 0)
            return;
        if (_disposed || array.Length > maximumRetainedBytes - _retainedBytes || array.Length > 1 << 30)
        {
            Shared.Return(array, clearArray);
            return;
        }
        if (clearArray)
            Array.Clear(array, 0, array.Length);
        var bucket = BitOperations.Log2((uint)array.Length);
        (_buckets[bucket] ??= new Stack<byte[]>()).Push(array);
        _retainedBytes += array.Length;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var arrays in _buckets)
        {
            if (arrays is null)
                continue;
            while (arrays.Count != 0)
                Shared.Return(arrays.Pop());
        }
        Array.Clear(_buckets, 0, _buckets.Length);
        _retainedBytes = 0;
    }
}
