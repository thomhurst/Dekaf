using System.Buffers;
using System.Runtime.CompilerServices;

namespace Dekaf.Networking;

/// <summary>
/// A lightweight <see cref="IBufferWriter{T}"/> backed by a rented <see cref="ArrayPool{T}"/> buffer.
/// Serialization writes directly into the rented array, avoiding an intermediate copy.
/// The caller detaches the underlying array via <see cref="DetachBuffer"/> and is responsible
/// for returning it to <see cref="DekafPools.SerializationBuffers"/>.
/// <para/>
/// <b>Why a dedicated pool?</b> Request serialization buffers are rented on BrokerSender
/// <c>LongRunning</c> threads but those threads hop to thread pool threads after each
/// <c>await FlushAsync</c>. Using <c>ArrayPool&lt;byte&gt;.Shared</c> would cause each
/// visited thread to accumulate arrays in its TLS cache (8 arrays per size bucket per thread).
/// With multiple brokers exercising many size classes through the growth pattern (4KB → 1MB),
/// the retained working set grows linearly with the number of unique threads visited —
/// proportional to broker count and test duration. A dedicated <c>ConfigurableArrayPool</c>
/// uses a single lock-based pool with bounded bucket depth, eliminating TLS accumulation.
/// The writer object is pooled separately so request serialization does not allocate a wrapper.
/// </summary>
internal sealed class RentedBufferWriter : IBufferWriter<byte>, IDisposable
{
    private const int MaxPoolSize = 256;
    // Rent and Dispose bracket synchronous request serialization before any await, so this is the
    // one producer pool that reliably benefits from Reservoir's same-thread fast path.
    private static readonly Reservoir.ObjectPool<RentedBufferWriter, PoolPolicy> Pool =
        new(default, MaxPoolSize, threadLocalFastPath: true);

    private byte[] _buffer = null!;
    private int _written;
    private bool _detached;
    private bool _active;

    private RentedBufferWriter()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RentedBufferWriter Rent(int initialCapacity, int offset)
    {
        var writer = Pool.Rent();
        writer._written = offset;
        writer._buffer = DekafPools.SerializationBuffers.Rent(initialCapacity + offset);
        writer._detached = false;
        writer._active = true;
        return writer;
    }

    internal int WrittenCount => _written;

    /// <summary>
    /// Detaches the underlying rented array. The caller owns the array and must return it
    /// to <see cref="DekafPools.SerializationBuffers"/>. After this call, the writer must not be used.
    /// </summary>
    public (byte[] Array, int Length) DetachBuffer()
    {
        _detached = true;
        return (_buffer, _written);
    }

    /// <summary>
    /// Returns the rented buffer to the pool if ownership was not transferred via <see cref="DetachBuffer"/>.
    /// </summary>
    public void Dispose()
    {
        if (!_active)
            return;

        _active = false;
        var buffer = _buffer;
        _buffer = null!;
        if (!_detached)
            DekafPools.SerializationBuffers.Return(buffer, clearArray: false);

        Pool.Return(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        if ((uint)count > (uint)(_buffer.Length - _written))
            throw new ArgumentOutOfRangeException(nameof(count));

        _written += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint < 1)
            sizeHint = 1;

        if (_buffer.Length - _written < sizeHint)
            Grow(sizeHint);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int sizeHint)
    {
        var doubled = Math.Min((long)_buffer.Length * 2, Array.MaxLength);
        var required = Math.Min((long)_written + sizeHint, Array.MaxLength);
        var newSize = (int)Math.Max(doubled, required);

        if (newSize <= _buffer.Length)
            throw new InvalidOperationException("Cannot grow buffer: maximum size reached.");

        var newBuffer = DekafPools.SerializationBuffers.Rent(newSize);
        _buffer.AsSpan(0, _written).CopyTo(newBuffer);
        DekafPools.SerializationBuffers.Return(_buffer, clearArray: false);
        _buffer = newBuffer;
    }

    private readonly struct PoolPolicy : Reservoir.IPooledObjectPolicy<RentedBufferWriter>
    {
        public RentedBufferWriter Create() => new();

        public bool TryReset(RentedBufferWriter writer)
        {
            writer._written = 0;
            writer._detached = false;
            return true;
        }
    }
}
