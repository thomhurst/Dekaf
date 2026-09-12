using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Steady-state buffered consumption with optional DLQ raw-byte tracking; setup costs are per batch.</summary>
[MemoryDiagnoser]
public class ConsumerRawTrackingBenchmarks
{
    private const int RecordsPerBatch = 1024;
    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _consumer = null!;
    private Record[] _records = null!;
    private Queue<PendingFetchData> _pendingFetches = null!;
    private long _nextOffset;

    [Params(false, true)]
    public bool TrackRawBytes { get; set; }

    [Params(0, 1, 100)]
    public int PayloadKind { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var bytes = PayloadKind > 1 ? new byte[PayloadKind] : Array.Empty<byte>();
        var records = new Record[RecordsPerBatch];
        for (var index = 0; index < records.Length; index++)
            records[index] = new Record
            {
                OffsetDelta = index,
                Key = bytes,
                Value = bytes,
                IsKeyNull = PayloadKind == 0,
                IsValueNull = PayloadKind == 0
            };
        _records = records;
        _consumer = new KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new ConsumerOptions { BootstrapServers = ["localhost:9092"], OffsetCommitMode = OffsetCommitMode.Manual },
            Serializers.RawBytes, Serializers.RawBytes);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_consumer, "raw-tracking", 0);
        _pendingFetches = (Queue<PendingFetchData>)BufferedConsumerHarness.GetPrivateField(_consumer, "_pendingFetches")!;
        if (TrackRawBytes)
            ((IRawRecordAccessor)_consumer).EnableRawRecordTracking();
    }

    [Benchmark(OperationsPerInvoke = RecordsPerBatch)]
    public async ValueTask<long> ConsumeBufferedBatch()
    {
        var batch = RecordBatch.RentFromPool();
        batch.BaseOffset = _nextOffset;
        batch.LastOffsetDelta = RecordsPerBatch - 1;
        batch.Records = _records;
        _pendingFetches.Enqueue(PendingFetchData.Create("raw-tracking", 0, [batch]));
        _nextOffset += RecordsPerBatch;
        long lastOffset = -1;
        for (var index = 0; index < RecordsPerBatch; index++)
        {
            var record = await _consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            lastOffset = record!.Value.Offset;
        }
        if (lastOffset != _nextOffset - 1)
            throw new InvalidOperationException("Buffered batch was not fully consumed.");
        return lastOffset;
    }

    [GlobalCleanup]
    public async Task Cleanup() => await _consumer.DisposeAsync();
}
