using System.Buffers;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// One ShareFetch v1 response: optional pool rent/copy, actual connection
/// decoding, metadata consumption and response release. Payload bytes stay opaque;
/// record-batch parsing, network I/O and per-message delivery are outside this boundary.
/// Allocations are per response, not per record. No payload view is read after decoding:
/// older products already return pooled storage before handing the response back.
/// </summary>
[MemoryDiagnoser]
public class ShareFetchResponseDecodingBenchmarks
{
    private static readonly Guid TopicId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private byte[] _encoded = null!;
    private ResponseBufferPool _pool = null!;
    private Func<PooledResponseBuffer, short, bool, IResponseMemoryReservation?, ShareFetchResponse> _parse = null!;
    private Action<ShareFetchResponse> _release = null!;

    [Params(false, true)]
    public bool Pooled { get; set; }

    [Params(0, 65536, 131072)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var payload = new byte[PayloadBytes];
        payload.AsSpan().Fill(0x5a);
        _encoded = Encode(payload).WrittenSpan.ToArray();
        _pool = new ResponseBufferPool(1024 * 1024, managedArraysPerBucket: 1);
        _parse = typeof(KafkaConnection)
            .GetMethod("ParsePipelinedResponse", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(ShareFetchRequest), typeof(ShareFetchResponse))
            .CreateDelegate<Func<PooledResponseBuffer, short, bool, IResponseMemoryReservation?, ShareFetchResponse>>();
        // Older decoders release the frame before returning; newer responses own it.
        // Both revisions measure decode/consume/release, with reflection confined to setup.
        _release = typeof(ShareFetchResponse).GetMethod("Dispose", Type.EmptyTypes)?
            .CreateDelegate<Action<ShareFetchResponse>>() ?? (static _ => { });

        var response = _parse(new PooledResponseBuffer(_encoded, _encoded.Length, false), 1, false, null);
        try
        {
            Validate(response);
            if (!response.Responses[0].Partitions[0].RecordBytes.Span.SequenceEqual(payload))
                throw new InvalidOperationException("ShareFetch payload changed.");
        }
        finally
        {
            _release(response);
        }
        // Validate both the selected storage path and its cleanup before timing.
        DecodeAndRelease();
        if (Pooled && _encoded.Length >= ResponseBufferPool.NativeMemoryThresholdBytes
            && _pool.RetainedNativeBufferCount != 1)
            throw new InvalidOperationException("The decoded native frame was not returned to its pool.");
    }

    [GlobalCleanup]
    public void Cleanup() => _pool.TrimNativeBuffers();

    [Benchmark]
    public int DecodeAndRelease()
    {
        PooledResponseBuffer frame;
        if (!Pooled)
        {
            frame = new PooledResponseBuffer(_encoded, _encoded.Length, false);
        }
        else if (_encoded.Length >= ResponseBufferPool.NativeMemoryThresholdBytes)
        {
            // Match ResponseFrameReader's native-storage selection for large frames.
            var native = _pool.RentNative(_encoded.Length);
            _encoded.AsSpan().CopyTo(native.GetSpan());
            frame = new PooledResponseBuffer(native, _encoded.Length);
        }
        else
        {
            var bytes = _pool.Pool.Rent(_encoded.Length);
            _encoded.CopyTo(bytes, 0);
            frame = new PooledResponseBuffer(bytes, _encoded.Length, true, pool: _pool);
        }
        var response = _parse(frame, 1, false, null);
        try
        {
            return Validate(response);
        }
        finally
        {
            _release(response);
        }
    }

    private int Validate(ShareFetchResponse response)
    {
        if (response.ErrorCode != ErrorCode.None || response.AcquisitionLockTimeoutMs != 30000
            || response.Responses.Count != 1 || response.NodeEndpoints.Count != 0)
            throw new InvalidOperationException("ShareFetch response metadata changed.");
        var topic = response.Responses[0];
        if (topic.TopicId != TopicId || topic.Partitions.Count != 1)
            throw new InvalidOperationException("ShareFetch topic metadata changed.");
        var partition = topic.Partitions[0];
        if (partition.PartitionIndex != 0 || partition.ErrorCode != ErrorCode.None
            || partition.RecordBytes.Length != PayloadBytes || partition.AcquiredRecords.Count != (PayloadBytes == 0 ? 0 : 1)
            || PayloadBytes != 0 && (partition.AcquiredRecords[0].FirstOffset != 0
                                    || partition.AcquiredRecords[0].LastOffset != 0
                                    || partition.AcquiredRecords[0].DeliveryCount != 1))
            throw new InvalidOperationException("ShareFetch partition metadata changed.");
        return partition.RecordBytes.Length;
    }

    private static ArrayBufferWriter<byte> Encode(ReadOnlySpan<byte> records)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0); // throttle
        writer.WriteInt16(0); // top-level error
        writer.WriteCompactString(null);
        writer.WriteInt32(30000); // acquisition lock timeout
        writer.WriteUnsignedVarInt(2); // one topic
        writer.WriteUuid(TopicId);
        writer.WriteUnsignedVarInt(2); // one partition
        writer.WriteInt32(0);
        writer.WriteInt16(0);
        writer.WriteCompactString(null);
        writer.WriteInt16(0); // acknowledgement error
        writer.WriteCompactString(null);
        writer.WriteInt32(1); // leader ID
        writer.WriteInt32(1); // leader epoch
        writer.WriteUnsignedVarInt(0); // leader tags
        writer.WriteUnsignedVarInt(records.Length + 1);
        writer.WriteRawBytes(records);
        writer.WriteUnsignedVarInt(records.IsEmpty ? 1 : 2);
        if (!records.IsEmpty)
        {
            writer.WriteInt64(0);
            writer.WriteInt64(0);
            writer.WriteInt16(1); // delivery count
            writer.WriteUnsignedVarInt(0); // acquired range tags
        }
        writer.WriteUnsignedVarInt(0); // partition tags
        writer.WriteUnsignedVarInt(0); // topic tags
        writer.WriteUnsignedVarInt(1); // no node endpoints
        writer.WriteUnsignedVarInt(0); // response tags
        return buffer;
    }
}
