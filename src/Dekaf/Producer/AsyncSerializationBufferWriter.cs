using System.Buffers;

namespace Dekaf.Producer;

/// <summary>
/// Heap-allocated <see cref="IBufferWriter{T}"/> backed by <see cref="ProducerDataPool.BytePool"/>
/// for the asynchronous serialization path. <see cref="PooledBufferWriter"/> is a ref struct and
/// cannot cross an <c>await</c>, so async serializers write into this class instead.
/// </summary>
/// <remarks>
/// Instances are cached per thread via <see cref="Rent"/>/<see cref="Return"/> so the steady state
/// allocates neither the writer nor its backing array. The writer is used by a single logical
/// produce flow at a time; the thread-local slot is only touched synchronously inside
/// <see cref="Rent"/> and <see cref="Return"/>, so continuations resuming on a different thread
/// simply return the instance to that thread's slot.
/// </remarks>
internal sealed class AsyncSerializationBufferWriter : IBufferWriter<byte>
{
    // Matches KafkaProducer.DefaultValueBufferSize — sized for typical message payloads.
    private const int InitialCapacity = 2048;

    [ThreadStatic]
    private static AsyncSerializationBufferWriter? t_cached;

    private byte[]? _buffer;
    private int _written;

    private AsyncSerializationBufferWriter()
    {
    }

    public static AsyncSerializationBufferWriter Rent()
    {
        var writer = t_cached;
        if (writer is not null)
        {
            t_cached = null;
            return writer;
        }

        return new AsyncSerializationBufferWriter();
    }

    /// <summary>
    /// Resets the writer and caches it on the current thread. Safe to call after
    /// <see cref="DetachWrittenMemory"/> (the detached array is not returned to the pool here).
    /// </summary>
    public static void Return(AsyncSerializationBufferWriter writer)
    {
        writer.Reset();
        t_cached = writer;
    }

    // Keep small buffers warm across messages (skips a pool round-trip per message); return
    // oversized ones so a single large message doesn't pin pool memory on this thread forever.
    private const int MaxRetainedBufferBytes = 64 * 1024;

    private void Reset()
    {
        if (_buffer is not null && _buffer.Length > MaxRetainedBufferBytes)
        {
            // clearArray: false — serialized Kafka payload bytes, no credential material;
            // consistent with PooledBufferWriter.Dispose.
            ProducerDataPool.BytePool.Return(_buffer, clearArray: false);
            _buffer = null;
        }

        _written = 0;
    }

    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (_buffer is null || _written + count > _buffer.Length)
            throw new InvalidOperationException("Cannot advance past the end of the buffer");

        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    /// <summary>
    /// Transfers ownership of the written bytes to the caller as a <see cref="PooledMemory"/>
    /// (returned to <see cref="ProducerDataPool.BytePool"/> when the batch completes). The writer
    /// itself stays reusable via <see cref="Return"/>.
    /// </summary>
    public PooledMemory DetachWrittenMemory()
    {
        if (_written == 0)
        {
            // Empty payloads use the shared (null, 0) representation; keep the rented buffer
            // for the next serialization. Mirrors ReusableBufferWriter.ToPooledMemory.
            return new PooledMemory(null, 0, isNull: false);
        }

        var result = new PooledMemory(_buffer, _written);
        _buffer = null;
        _written = 0;
        return result;
    }

    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint < 1)
            sizeHint = 1;

        if (_buffer is null)
        {
            _buffer = ProducerDataPool.BytePool.Rent(Math.Max(InitialCapacity, sizeHint));
            return;
        }

        if (_buffer.Length - _written < sizeHint)
            Grow(sizeHint);
    }

    private void Grow(int sizeHint)
    {
        // Clamp like DetachableBufferWriter.Grow so doubling can never overflow past Array.MaxLength.
        var doubled = Math.Min((long)_buffer!.Length * 2, Array.MaxLength);
        var required = Math.Min((long)_written + sizeHint, Array.MaxLength);
        var newSize = (int)Math.Max(doubled, required);

        if (newSize <= _buffer.Length)
            throw new InvalidOperationException("Cannot grow buffer: maximum size reached.");

        var newBuffer = ProducerDataPool.BytePool.Rent(newSize);
        _buffer.AsSpan(0, _written).CopyTo(newBuffer);
        ProducerDataPool.BytePool.Return(_buffer, clearArray: false);
        _buffer = newBuffer;
    }
}
