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
/// Parses one share partition from a transactional topic: every producer batch is followed
/// by its COMMIT marker. The broker acquires the marker offsets, so the parser skips each
/// control batch and queues its offset as a gap. Each operation includes parsing, traversal
/// with implicit delivery tracking, the poll-boundary acknowledgement flush that carries the
/// accepted records and the gaps, and poll cleanup.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerTransactionalParsingBenchmarks
{
    private const int RecordsPerBatch = 4;

    private KafkaShareConsumer<int, ReadOnlyMemory<byte>> _consumer = null!;
    private Func<TopicInfo, ShareFetchResponsePartition, int, List<ShareConsumeResult<int, ReadOnlyMemory<byte>>>> _parse = null!;
    private Action _release = null!;
    private AcknowledgementTracker _tracker = null!;
    private readonly TopicInfo _topic = new() { Name = "share-transactional", Partitions = [] };
    private readonly TopicPartition _topicPartition = new("share-transactional", 0);
    private ShareFetchResponsePartition _partition = null!;
    private int _maxRecords;

    [Params(1, 256)]
    public int BatchCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var buffer = new ArrayBufferWriter<byte>();
        long offset = 0;
        for (var index = 0; index < BatchCount; index++)
        {
            var records = new List<Record>(RecordsPerBatch);
            for (var record = 0; record < RecordsPerBatch; record++)
            {
                var key = new ArrayBufferWriter<byte>();
                var value = new ArrayBufferWriter<byte>();
                Serializers.Int32.Serialize(record, ref key, default);
                Serializers.Int32.Serialize(42, ref value, default);
                records.Add(new Record { OffsetDelta = record, Key = key.WrittenMemory, Value = value.WrittenMemory });
            }
            using (var data = new RecordBatch
            {
                BaseOffset = offset,
                BaseTimestamp = 1700000000000,
                LastOffsetDelta = RecordsPerBatch - 1,
                ProducerId = 7,
                Attributes = RecordBatchAttributes.IsTransactional,
                Records = records
            })
            {
                data.Write(buffer);
            }
            offset += RecordsPerBatch;
            using (var marker = new RecordBatch
            {
                BaseOffset = offset,
                BaseTimestamp = 1700000000000,
                ProducerId = 7,
                Attributes = RecordBatchAttributes.IsTransactional | RecordBatchAttributes.IsControlBatch,
                // EndTransactionMarker: control key version 0, type 1 (COMMIT).
                Records = [new Record { Key = new byte[] { 0, 0, 0, 1 }, Value = new byte[] { 0, 0, 0, 0, 0, 0 } }]
            })
            {
                marker.Write(buffer);
            }
            offset++;
        }

        _maxRecords = BatchCount * (RecordsPerBatch + 1);
        _partition = new ShareFetchResponsePartition
        {
            PartitionIndex = 0,
            CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
            RecordBytes = buffer.WrittenMemory,
            AcquiredRecords = [new ShareFetchAcquiredRecords { FirstOffset = 0, LastOffset = offset - 1, DeliveryCount = 1 }]
        };
        _consumer = new KafkaShareConsumer<int, ReadOnlyMemory<byte>>(
            new ShareConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "transactional-benchmark",
                MaxPollRecords = _maxRecords
            }, Serializers.Int32, Serializers.RawBytes);
        var type = typeof(KafkaShareConsumer<int, ReadOnlyMemory<byte>>);
        _parse = type.GetMethod("ParsePartitionRecords", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<TopicInfo, ShareFetchResponsePartition, int, List<ShareConsumeResult<int, ReadOnlyMemory<byte>>>>>(_consumer);
        _release = type.GetMethod("ReleasePolledBatchOwners", BindingFlags.Instance | BindingFlags.NonPublic)?
            .CreateDelegate<Action>(_consumer) ?? (static () => { });
        _tracker = (AcknowledgementTracker)type.GetField("_ackTracker", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(_consumer)!;

        // Every data record must be delivered. Revisions that deliver control records
        // return more; this fixture compares the parse and flush costs.
        ParseBorrowedPoll();
        if (ParsePreparedBorrowedPoll() == 0)
            throw new InvalidOperationException("No transactional records were parsed.");
    }

    [Benchmark]
    public long ParseBorrowedPoll()
    {
        try
        {
            return Consume(_parse(_topic, _partition, _maxRecords));
        }
        finally
        {
            Complete();
        }
    }

    [Benchmark]
    public long ParsePreparedBorrowedPoll()
    {
        var records = new List<ShareConsumeResult<int, ReadOnlyMemory<byte>>>(_maxRecords);
        var state = new KafkaShareConsumer<int, ReadOnlyMemory<byte>>.DeserializerPreparationParserState();
        try
        {
            if (_consumer.ParsePartitionRecordsWithPreparation(_topic, _partition, _maxRecords,
                records, ref state, false, default) is not null)
                throw new InvalidOperationException("The raw deserializer must not require preparation.");
            return Consume(records);
        }
        finally
        {
            state.DisposeCurrentBatch();
            Complete();
        }
    }

    private long Consume(List<ShareConsumeResult<int, ReadOnlyMemory<byte>>> records)
    {
        if (records.Count < BatchCount * RecordsPerBatch)
            throw new InvalidOperationException("Incomplete transactional poll.");
        long checksum = 0;
        foreach (var record in records)
        {
            // Implicit PollAsync tracks each record as it is delivered.
            _tracker.TrackDeliveredRecords(_topicPartition, record.Offset, record.Offset);
            checksum += record.Offset + record.Key + record.Value.Length;
        }
        return checksum;
    }

    private void Complete()
    {
        // The next ShareFetch carries the queued gaps; flush them at the same boundary.
        _tracker.Flush();
        _release();
    }

    [GlobalCleanup]
    public async ValueTask Cleanup() => await _consumer.DisposeAsync();
}
