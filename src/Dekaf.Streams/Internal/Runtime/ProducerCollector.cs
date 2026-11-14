using System.Buffers;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Streams.Internal.Runtime;

/// <summary>Serde for byte payloads where null and empty are different values.</summary>
internal sealed class NullableBytesSerde : ISerializer<ReadOnlyMemory<byte>?>, IDeserializer<ReadOnlyMemory<byte>?>
{
    public static NullableBytesSerde Instance { get; } = new();

    public void Serialize<TWriter>(ReadOnlyMemory<byte>? value, ref TWriter destination, SerializationContext context)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var data = value.GetValueOrDefault().Span;
        if (!data.IsEmpty)
        {
            data.CopyTo(destination.GetSpan(data.Length));
            destination.Advance(data.Length);
        }
    }

    public ReadOnlyMemory<byte>? Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
        context.IsNull ? (ReadOnlyMemory<byte>?)null : data;
}

/// <summary>
/// Holds the bytes of records handed to the producer until the commit that covers them completes. Nodes
/// serialize into buffers they reuse for the next record, and the producer may read a record after the
/// send call returns, so each record is copied here once. Chunks are reused across commits.
/// </summary>
internal sealed class OutputArena : IDisposable
{
    private const int ChunkSize = 64 * 1024;
    private const int RetainedChunks = 16;
    private readonly List<byte[]> _chunks = [];
    private readonly List<byte[]> _oversized = [];
    private int _chunk;
    private int _offset;

    public ReadOnlyMemory<byte> Copy(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        if (data.Length > ChunkSize)
        {
            var large = ArrayPool<byte>.Shared.Rent(data.Length);
            _oversized.Add(large);
            data.CopyTo(large);
            return large.AsMemory(0, data.Length);
        }

        if (_chunks.Count == 0)
        {
            _chunks.Add(ArrayPool<byte>.Shared.Rent(ChunkSize));
        }
        else if (_offset + data.Length > ChunkSize)
        {
            if (++_chunk == _chunks.Count)
            {
                _chunks.Add(ArrayPool<byte>.Shared.Rent(ChunkSize));
            }

            _offset = 0;
        }

        var target = _chunks[_chunk].AsMemory(_offset, data.Length);
        data.CopyTo(target.Span);
        _offset += data.Length;
        return target;
    }

    /// <summary>Makes the space reusable. Call only when the producer no longer holds any copied record.</summary>
    public void Reset()
    {
        Return(_oversized, keep: 0);
        Return(_chunks, RetainedChunks);
        _chunk = 0;
        _offset = 0;
    }

    public void Dispose()
    {
        Return(_oversized, keep: 0);
        Return(_chunks, keep: 0);
    }

    private static void Return(List<byte[]> buffers, int keep)
    {
        for (var i = buffers.Count - 1; i >= keep; i--)
        {
            ArrayPool<byte>.Shared.Return(buffers[i]);
            buffers.RemoveAt(i);
        }
    }
}

/// <summary>
/// Sends task output through the thread's producer. A send never throws into a task: failures are kept
/// and reported at the next commit, so they are never mistaken for a failure of the record being processed.
/// </summary>
internal sealed class ProducerCollector : IRecordCollector, IDisposable
{
    private readonly IKafkaProducer<ReadOnlyMemory<byte>?, ReadOnlyMemory<byte>?> _producer;
    private readonly OutputArena _arena = new();
    private readonly List<Task> _pending = [];
    private readonly Action<RecordMetadata, Exception?> _onDelivery;
    private Exception? _failure;

    public ProducerCollector(IKafkaProducer<ReadOnlyMemory<byte>?, ReadOnlyMemory<byte>?> producer)
    {
        _producer = producer;
        _onDelivery = OnDelivery;
    }

    /// <summary>Whether a send is waiting for producer buffer space.</summary>
    public bool HasPending => _pending.Count > 0;

    public void Send(
        string topic,
        int partition,
        ReadOnlySpan<byte> key,
        bool keyIsNull,
        ReadOnlySpan<byte> value,
        bool valueIsNull,
        long timestamp,
        Headers? headers)
    {
        if (Volatile.Read(ref _failure) is not null)
        {
            return;
        }

        try
        {
            var message = new ProducerMessage<ReadOnlyMemory<byte>?, ReadOnlyMemory<byte>?>
            {
                Topic = topic,
                // The casts matter: a bare null would convert to an empty ReadOnlyMemory through its
                // implicit conversion from an array, and a tombstone would be sent as an empty value.
                Key = keyIsNull ? (ReadOnlyMemory<byte>?)null : _arena.Copy(key),
                Value = valueIsNull ? (ReadOnlyMemory<byte>?)null : _arena.Copy(value),
                Partition = partition < 0 ? null : partition,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp),
                Headers = headers
            };
            var send = _producer.FireAsync(message, _onDelivery);
            if (!send.IsCompletedSuccessfully)
            {
                _pending.Add(send.AsTask());
            }
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref _failure, exception, null);
        }
    }

    /// <summary>Waits for sends that are blocked on producer buffer space.</summary>
    public async ValueTask DrainAsync()
    {
        foreach (var send in _pending)
        {
            try
            {
                await send.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Interlocked.CompareExchange(ref _failure, exception, null);
            }
        }

        _pending.Clear();
    }

    /// <summary>Returns the first send or delivery failure since the last <see cref="Reset"/>.</summary>
    public Exception? Failure => Volatile.Read(ref _failure);

    /// <summary>Call after a commit or abort completed: no record of the finished cycle is in flight.</summary>
    public void Reset()
    {
        _pending.Clear();
        _arena.Reset();
        Volatile.Write(ref _failure, null);
    }

    public void Dispose() => _arena.Dispose();

    // Runs on a producer thread.
    private void OnDelivery(RecordMetadata metadata, Exception? exception)
    {
        if (exception is not null)
        {
            Interlocked.CompareExchange(ref _failure, exception, null);
        }
    }
}
