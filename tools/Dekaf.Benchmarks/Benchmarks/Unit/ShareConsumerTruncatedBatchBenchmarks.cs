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
/// Parses one ShareFetch partition whose last batch the broker cut at the response byte limit.
/// The complete batches are delivered and the partial tail is skipped once per partition.
/// Each operation includes parsing, traversal and poll cleanup; serialization happens in setup.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerTruncatedBatchBenchmarks
{
    private KafkaShareConsumer<int, ReadOnlyMemory<byte>> _consumer = null!;
    private Func<TopicInfo, ShareFetchResponsePartition, int, List<ShareConsumeResult<int, ReadOnlyMemory<byte>>>> _parse = null!;
    private Action _release = null!;
    private readonly TopicInfo _topic = new() { Name = "share-truncated-batch", Partitions = [] };
    private ShareFetchResponsePartition _partition = null!;

    [Params(1, 500)]
    public int BatchCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var completeLength = 0;
        // One extra batch becomes the truncated tail. Its header is complete and its records are not.
        for (var index = 0; index <= BatchCount; index++)
        {
            var key = new ArrayBufferWriter<byte>();
            var value = new ArrayBufferWriter<byte>();
            Serializers.Int32.Serialize(index, ref key, default);
            Serializers.Int32.Serialize(42, ref value, default);
            using var batch = new RecordBatch
            {
                BaseOffset = index,
                BaseTimestamp = 1700000000000,
                Records = [new Record { Key = key.WrittenMemory, Value = value.WrittenMemory }]
            };
            completeLength = buffer.WrittenCount;
            batch.Write(buffer);
        }

        _partition = new ShareFetchResponsePartition
        {
            PartitionIndex = 0,
            CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
            RecordBytes = buffer.WrittenMemory[..(completeLength + RecordBatch.TotalBatchHeaderSize + 1)],
            AcquiredRecords = [new ShareFetchAcquiredRecords
            {
                FirstOffset = 0,
                LastOffset = BatchCount - 1,
                DeliveryCount = 1
            }]
        };
        _consumer = new KafkaShareConsumer<int, ReadOnlyMemory<byte>>(
            new ShareConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "truncated-batch-benchmark",
                MaxPollRecords = BatchCount + 1
            }, Serializers.Int32, Serializers.RawBytes);
        _parse = typeof(KafkaShareConsumer<int, ReadOnlyMemory<byte>>)
            .GetMethod("ParsePartitionRecords", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<TopicInfo, ShareFetchResponsePartition, int, List<ShareConsumeResult<int, ReadOnlyMemory<byte>>>>>(_consumer);
        _release = typeof(KafkaShareConsumer<int, ReadOnlyMemory<byte>>)
            .GetMethod("ReleasePolledBatchOwners", BindingFlags.Instance | BindingFlags.NonPublic)?
            .CreateDelegate<Action>(_consumer) ?? (static () => { });

        try
        {
            var records = _parse(_topic, _partition, BatchCount + 1);
            if (records.Count != BatchCount || records[^1].Offset != BatchCount - 1)
                throw new InvalidOperationException("The truncated tail hid complete batches.");
        }
        finally
        {
            _release();
        }
    }

    [Benchmark]
    public long ParseTruncatedPoll()
    {
        try
        {
            var records = _parse(_topic, _partition, BatchCount + 1);
            long checksum = 0;
            foreach (var record in records)
                checksum += record.Offset + record.Key + record.Value.Span[3];
            return checksum;
        }
        finally
        {
            _release();
        }
    }

    [GlobalCleanup]
    public async ValueTask Cleanup() => await _consumer.DisposeAsync();
}
