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
/// Parses and consumes one partition containing many single-record producer batches.
/// Borrowed values keep every batch owner alive until the poll boundary. Each operation
/// includes parsing, traversal and poll cleanup; serialization and binding happen in setup.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerMultiBatchBenchmarks
{
    private KafkaShareConsumer<int, ReadOnlyMemory<byte>> _consumer = null!;
    private Func<TopicInfo, ShareFetchResponsePartition, int, List<ShareConsumeResult<int, ReadOnlyMemory<byte>>>> _parse = null!;
    private Action _release = null!;
    private readonly TopicInfo _topic = new() { Name = "share-multi-batch", Partitions = [] };
    private ShareFetchResponsePartition _partition = null!;

    [Params(1, 500, 1024)]
    public int BatchCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var buffer = new ArrayBufferWriter<byte>();
        for (var index = 0; index < BatchCount; index++)
        {
            var key = new ArrayBufferWriter<byte>();
            var value = new ArrayBufferWriter<byte>();
            Serializers.Int32.Serialize(index, ref key, default);
            // Identical borrowed values let the historical parser run this fixture even
            // though it returns batch storage early. Ownership tests use distinct values
            // to detect that corruption; this fixture compares allocation and parse costs.
            Serializers.Int32.Serialize(42, ref value, default);
            using var batch = new RecordBatch
            {
                BaseOffset = index,
                BaseTimestamp = 1700000000000,
                Records = [new Record { Key = key.WrittenMemory, Value = value.WrittenMemory }]
            };
            batch.Write(buffer);
        }

        _partition = new ShareFetchResponsePartition
        {
            PartitionIndex = 0,
            CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
            RecordBytes = buffer.WrittenMemory,
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
                GroupId = "multi-batch-benchmark",
                MaxPollRecords = BatchCount
            }, Serializers.Int32, Serializers.RawBytes);
        _parse = typeof(KafkaShareConsumer<int, ReadOnlyMemory<byte>>)
            .GetMethod("ParsePartitionRecords", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<TopicInfo, ShareFetchResponsePartition, int, List<ShareConsumeResult<int, ReadOnlyMemory<byte>>>>>(_consumer);
        _release = typeof(KafkaShareConsumer<int, ReadOnlyMemory<byte>>)
            .GetMethod("ReleasePolledBatchOwners", BindingFlags.Instance | BindingFlags.NonPublic)?
            .CreateDelegate<Action>(_consumer) ?? (static () => { });

        Validate(_parse(_topic, _partition, BatchCount));
        Validate(ParsePrepared());
    }

    private void Validate(List<ShareConsumeResult<int, ReadOnlyMemory<byte>>> records)
    {
        try
        {
            if (records.Count != BatchCount)
                throw new InvalidOperationException("Incomplete multi-batch poll.");
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record.Partition != _partition.PartitionIndex || record.Offset != index || record.Key != index ||
                    Serializers.Int32.Deserialize(record.Value, default) != 42)
                    throw new InvalidOperationException("Borrowed multi-batch payload was corrupted.");
            }
        }
        finally
        {
            _release();
        }
    }

    [Benchmark]
    public long ParseBorrowedPoll()
    {
        try
        {
            var records = _parse(_topic, _partition, BatchCount);
            long checksum = 0;
            foreach (var record in records)
                checksum += record.Partition + record.Offset + record.Key + record.Value.Span[3];
            return checksum;
        }
        finally
        {
            _release();
        }
    }

    [Benchmark]
    public long ParsePreparedBorrowedPoll()
    {
        try
        {
            var records = ParsePrepared();
            long checksum = 0;
            foreach (var record in records)
                checksum += record.Partition + record.Offset + record.Key + record.Value.Span[3];
            return checksum;
        }
        finally
        {
            _release();
        }
    }

    private List<ShareConsumeResult<int, ReadOnlyMemory<byte>>> ParsePrepared()
    {
        var records = new List<ShareConsumeResult<int, ReadOnlyMemory<byte>>>();
        var state = new KafkaShareConsumer<int, ReadOnlyMemory<byte>>.DeserializerPreparationParserState();
        try
        {
            if (_consumer.ParsePartitionRecordsWithPreparation(_topic, _partition, BatchCount,
                records, ref state, false, default) is not null)
                throw new InvalidOperationException("The raw deserializer must not require preparation.");
            return records;
        }
        finally
        {
            state.DisposeCurrentBatch();
        }
    }

    [GlobalCleanup]
    public async ValueTask Cleanup() => await _consumer.DisposeAsync();
}
