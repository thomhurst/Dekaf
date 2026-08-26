using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ShortRunJob]
public class ShareConsumerPreparationReplayBenchmarks
{
    private const int RecordCount = 64;
    private byte[] _recordBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        var records = new Record[RecordCount];
        for (var index = 0; index < records.Length; index++)
        {
            records[index] = new Record
            {
                OffsetDelta = index,
                IsKeyNull = true,
                Value = new byte[] { (byte)index }
            };
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var batch = new RecordBatch { Records = records })
            batch.Write(buffer);

        _recordBytes = buffer.WrittenSpan.ToArray();
        _ = LegacyReplayEveryColdRecord();
        _ = CursorRetainsDecodedBatch();
    }

    [Benchmark(Baseline = true)]
    public int LegacyReplayEveryColdRecord()
    {
        var checksum = 0;
        for (var targetIndex = 0; targetIndex < RecordCount; targetIndex++)
        {
            var reader = new KafkaProtocolReader(_recordBytes);
            var batch = RecordBatch.Read(ref reader);
            try
            {
                checksum += batch.Records[targetIndex].Value.Span[0];
            }
            finally
            {
                batch.DisposeAndReturnUnownedConsumerBatch();
            }
        }

        return checksum;
    }

    [Benchmark]
    public int CursorRetainsDecodedBatch()
    {
        var reader = new KafkaProtocolReader(_recordBytes);
        var batch = RecordBatch.Read(ref reader);
        try
        {
            var checksum = 0;
            var records = batch.Records;
            for (var index = 0; index < records.Count; index++)
                checksum += records[index].Value.Span[0];
            return checksum;
        }
        finally
        {
            batch.DisposeAndReturnUnownedConsumerBatch();
        }
    }
}
