using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

[MemoryDiagnoser]
public class WireBatchBoundBench
{
    private byte[][] _wire = null!;
    private RecordBatch[] _batches = null!;

    [Params(1, 128)]
    public int Batches { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _wire = new byte[Batches][];
        _batches = new RecordBatch[Batches];
        for (var index = 0; index < Batches; index++)
        {
            var records = new Record[8];
            for (var record = 0; record < records.Length; record++)
                records[record] = new Record { OffsetDelta = record, Value = new byte[16], IsKeyNull = true };
            using var batch = new RecordBatch { BaseOffset = index * 8, LastOffsetDelta = 7, Records = records };
            var buffer = new ArrayBufferWriter<byte>();
            batch.Write(buffer);
            _wire[index] = buffer.WrittenSpan.ToArray();
        }
        if (ParseAndReadWireFetch() != Batches * 8 * 16)
            throw new InvalidOperationException("Wire fixture did not parse every record.");
    }

    [Benchmark]
    public int ParseAndReadWireFetch()
    {
        for (var index = 0; index < Batches; index++)
        {
            var reader = new KafkaProtocolReader(_wire[index]);
            _batches[index] = RecordBatch.Read(ref reader);
        }
        using var pending = PendingFetchData.Create("wire-bounds", 0, _batches);
        pending.EagerParseAll();
        var bytes = 0;
        while (pending.MoveNext())
            bytes += pending.CurrentRecord.Value.Length;
        return bytes;
    }
}
