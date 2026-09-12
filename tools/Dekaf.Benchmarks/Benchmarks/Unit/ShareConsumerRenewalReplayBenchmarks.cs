using System.Buffers;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Metadata;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures a replay snapshot and the next poll's owner release, with actual borrowed
/// records retained by successful Renew acknowledgements. Serialization, initial parsing
/// and acknowledgement activation happen once. Results are per snapshot, not per record.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerRenewalReplayBenchmarks
{
    private KafkaShareConsumer<int, ReadOnlyMemory<byte>> _consumer = null!;
    private Func<IReadOnlySet<TopicPartition>, int, List<ShareConsumeResult<int, ReadOnlyMemory<byte>>>> _replay = null!;
    private readonly HashSet<TopicPartition> _assignment = [new("renewal-benchmark", 0)];
    private int _recordCount;

    [Params(1, 64)]
    public int RecordsPerBatch { get; set; }

    [Params(1, 16, 64, 128)]
    public int BatchCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _recordCount = RecordsPerBatch * BatchCount;
        var bytes = new ArrayBufferWriter<byte>();
        for (var batchIndex = 0; batchIndex < BatchCount; batchIndex++)
        {
            var records = new Record[RecordsPerBatch];
            for (var index = 0; index < records.Length; index++)
                records[index] = new Record { OffsetDelta = index, IsKeyNull = true, Value = new byte[32] };
            using var batch = new RecordBatch { BaseOffset = batchIndex * RecordsPerBatch, Records = records };
            batch.Write(bytes);
        }
        _consumer = new KafkaShareConsumer<int, ReadOnlyMemory<byte>>(new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], GroupId = "renewal-benchmark",
            AcknowledgementMode = ShareAcknowledgementMode.Explicit
        }, Serializers.Int32, Serializers.RawBytes);
        var type = _consumer.GetType();
        var parse = type.GetMethod("ParsePartitionRecords", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<TopicInfo, ShareFetchResponsePartition, int,
                List<ShareConsumeResult<int, ReadOnlyMemory<byte>>>>>(_consumer);
        var partition = new ShareFetchResponsePartition
        {
            PartitionIndex = 0, CurrentLeader = new(), RecordBytes = bytes.WrittenMemory,
            AcquiredRecords = [new ShareFetchAcquiredRecords
            {
                FirstOffset = 0, LastOffset = _recordCount - 1, DeliveryCount = 1
            }]
        };
        using (_consumer.BeginRecordBatchScope())
        {
            var parsed = parse(new TopicInfo { Name = "renewal-benchmark", Partitions = [] }, partition, _recordCount);
            if (parsed.Count != _recordCount || parsed[0].BatchOwner is null)
                throw new InvalidOperationException("Renewal replay requires actual borrowed batch ownership.");
            // Interleave batches to expose deduplication that only remembers the last owner.
            for (var index = 0; index < RecordsPerBatch; index++)
                for (var batchIndex = 0; batchIndex < BatchCount; batchIndex++)
                    _consumer.Acknowledge(parsed[batchIndex * RecordsPerBatch + index], AcknowledgeType.Renew);
        }
        var tracker = (AcknowledgementTracker)type.GetField("_ackTracker", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(_consumer)!;
        var acknowledgements = tracker.Flush();
        type.GetMethod("ApplySuccessfulAcknowledgements", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_consumer, [acknowledgements, 0L]);
        _replay = type.GetMethod("GetActiveRenewedRecords", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<IReadOnlySet<TopicPartition>, int,
                List<ShareConsumeResult<int, ReadOnlyMemory<byte>>>>>(_consumer);
        if (Replay() != 32L * _recordCount)
            throw new InvalidOperationException("Renewed payloads must remain available across poll rounds.");
    }

    [Benchmark]
    public long Replay()
    {
        using var scope = _consumer.BeginRecordBatchScope();
        var records = _replay(_assignment, _recordCount);
        long bytes = 0;
        foreach (var record in records)
            bytes += record.Value.Length;
        return bytes;
    }

    [GlobalCleanup]
    public async Task Cleanup() => await _consumer.DisposeAsync();
}
