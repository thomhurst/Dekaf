using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Docker-free gate for the consumer's parsed-record slab lifecycle. One partition fetch of
/// <see cref="RecordCount"/> lazily decoded 1 KiB records is read zero-copy from a wire buffer,
/// attached to a pooled <see cref="Record"/> slab and parsed by
/// <see cref="PendingFetchData.EagerParseAll"/>, iterated through
/// <see cref="PendingFetchData.MoveNext"/> and <see cref="PendingFetchData.CurrentRecord"/>,
/// then disposed so the slab returns to its pool. The measured body is that whole
/// parse-attach-iterate-release lifecycle (record parsing dominates it); CRC verification and
/// network I/O are excluded.
/// </summary>
[MemoryDiagnoser]
[ThroughputJob]
public class ParsedRecordSlabLifecycleBenchmarks
{
    private const int RecordCount = 1_000;
    private const int ValueSize = 1024;

    private readonly RecordBatch[] _batches = new RecordBatch[1];
    private WireBuffer _wire = null!;

    [GlobalSetup]
    public void Setup()
    {
        var records = new Record[RecordCount];
        for (var i = 0; i < records.Length; i++)
        {
            var value = new byte[ValueSize];
            value.AsSpan().Fill((byte)i);
            records[i] = new Record
            {
                OffsetDelta = i,
                TimestampDelta = i,
                Key = BitConverter.GetBytes((long)i),
                Value = value
            };
        }

        using var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1_700_000_000_000L,
            MaxTimestamp = 1_700_000_000_000L + RecordCount - 1,
            LastOffsetDelta = RecordCount - 1,
            Records = records
        };
        var writer = new ArrayBufferWriter<byte>();
        batch.Write(writer);
        _wire = new WireBuffer(writer.WrittenSpan.ToArray());
    }

    [Benchmark]
    public long ConsumeFetch()
    {
        // Same zero-copy parsing context as KafkaConnection.ParseFetchResponse: records slice the
        // pooled frame instead of copying into a rented byte[].
        using var scope = ResponseParsingContext.SetPooledMemory(_wire);
        var reader = new KafkaProtocolReader(_wire.Memory);
        _batches[0] = RecordBatch.Read(ref reader);

        var pending = PendingFetchData.Create("bench-topic", 0, _batches);
        pending.EagerParseAll();

        long consumedBytes = 0;
        while (pending.MoveNext())
            consumedBytes += pending.CurrentRecord.Value.Length;

        pending.Dispose();
        return consumedBytes;
    }

    /// <summary>Frame stand-in owned by the benchmark for its whole lifetime.</summary>
    private sealed class WireBuffer(byte[] bytes) : IPooledMemory
    {
        public ReadOnlyMemory<byte> Memory => bytes;

        public void Dispose()
        {
        }
    }
}
