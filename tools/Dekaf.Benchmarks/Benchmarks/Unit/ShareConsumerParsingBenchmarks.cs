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
/// Measures one complete partition batch, separately from traversal of retained results.
/// Fixture serialization and private-method delegate binding happen only during setup.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerParsingBenchmarks
{
    private const long BaseOffset = 1000;
    private const long BaseTimestamp = 1700000000000;
    private KafkaShareConsumer<int, int> _synchronousConsumer = null!;
    private KafkaShareConsumer<int, int> _preparedConsumer = null!;
    private Func<TopicInfo, ShareFetchResponsePartition, int, List<ShareConsumeResult<int, int>>> _parse = null!;
    private readonly TopicInfo _topic = new() { Name = "share-benchmark", Partitions = [] };
    private ShareFetchResponsePartition _partition = null!;
    private List<ShareConsumeResult<int, int>> _retained = null!;

    [Params(64, 1024)]
    public int RecordCount { get; set; }

    [Params(0, 2)]
    public int HeaderCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var records = new Record[RecordCount];
        for (var index = 0; index < records.Length; index++)
        {
            var key = new ArrayBufferWriter<byte>();
            var value = new ArrayBufferWriter<byte>();
            Serializers.Int32.Serialize(index, ref key, default);
            Serializers.Int32.Serialize(index + 1, ref value, default);
            records[index] = new Record
            {
                OffsetDelta = index,
                TimestampDelta = index,
                Key = key.WrittenMemory,
                Value = value.WrittenMemory,
                Headers = HeaderCount == 0 ? null :
                [
                    new Header("kind", new byte[] { 42 }),
                    new Header("nullable", (byte[]?)null)
                ],
                HeaderCount = HeaderCount
            };
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var batch = new RecordBatch
        {
            BaseOffset = BaseOffset,
            BaseTimestamp = BaseTimestamp,
            Records = records
        })
        {
            batch.Write(buffer);
        }

        _partition = new ShareFetchResponsePartition
        {
            PartitionIndex = 0,
            CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
            RecordBytes = buffer.WrittenMemory,
            AcquiredRecords =
            [
                new ShareFetchAcquiredRecords
                {
                    FirstOffset = BaseOffset,
                    LastOffset = BaseOffset + RecordCount - 1,
                    DeliveryCount = 3
                }
            ]
        };
        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "share-parsing-benchmark"
        };
        _synchronousConsumer = new KafkaShareConsumer<int, int>(options, Serializers.Int32, Serializers.Int32);
        var prepared = new WarmInt32Deserializer();
        _preparedConsumer = new KafkaShareConsumer<int, int>(options, prepared, prepared);
        _parse = typeof(KafkaShareConsumer<int, int>)
            .GetMethod("ParsePartitionRecords", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<TopicInfo, ShareFetchResponsePartition, int, List<ShareConsumeResult<int, int>>>>(
                _synchronousConsumer);

        _retained = _parse(_topic, _partition, RecordCount);
        Validate(_retained);
        Validate(ParsePrepared());
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _synchronousConsumer.DisposeAsync();
        await _preparedConsumer.DisposeAsync();
    }

    [Benchmark]
    public long ParseSynchronousBatch() => Traverse(_parse(_topic, _partition, RecordCount));

    [Benchmark]
    public long ParseWarmPreparedBatch() => Traverse(ParsePrepared());

    [Benchmark]
    public long TraverseRetainedBatch() => Traverse(_retained);

    private List<ShareConsumeResult<int, int>> ParsePrepared()
    {
        // PollAsync starts each partition with an empty list and a fresh parser cursor.
        var records = new List<ShareConsumeResult<int, int>>();
        var state = new KafkaShareConsumer<int, int>.DeserializerPreparationParserState();
        try
        {
            var pending = _preparedConsumer.ParsePartitionRecordsWithPreparation(
                _topic, _partition, RecordCount, records, ref state, false, default);
            if (pending is not null)
                throw new InvalidOperationException("The warm deserializer unexpectedly requested preparation.");
            return records;
        }
        finally
        {
            state.DisposeCurrentBatch();
        }
    }

    private static long Traverse(List<ShareConsumeResult<int, int>> records)
    {
        long checksum = 0;
        foreach (var record in records)
        {
            checksum += record.Offset + record.Key + record.Value + record.DeliveryCount + record.TimestampMs;
            for (var index = 0; index < record.Headers.Count; index++)
            {
                var header = record.Headers[index];
                checksum += header.Key.Length;
                if (!header.IsValueNull)
                    checksum += header.Value.Span[0];
            }
        }
        return checksum;
    }

    private void Validate(List<ShareConsumeResult<int, int>> records)
    {
        if (records.Count != RecordCount)
            throw new InvalidOperationException("The parser did not return the full acquired batch.");
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record.Topic != _topic.Name || record.Partition != 0 || record.Offset != BaseOffset + index
                || record.Key != index || record.Value != index + 1 || record.DeliveryCount != 3
                || record.TimestampMs != BaseTimestamp + index || record.Headers.Count != HeaderCount)
                throw new InvalidOperationException("The parser changed record data or metadata.");
            if (HeaderCount != 0
                && (record.Headers[0].Key != "kind" || record.Headers[0].IsValueNull
                    || (record.Headers[0].Value.Length != 1 || record.Headers[0].Value.Span[0] != 42)
                    || record.Headers[1].Key != "nullable" || !record.Headers[1].IsValueNull))
                throw new InvalidOperationException("The parser changed header data or nullability.");
        }
    }

    private sealed class WarmInt32Deserializer : IDeserializer<int>, IAsyncDeserializerPreparer<int>
    {
        public int Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
            => Serializers.Int32.Deserialize(data, context);

        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out int value)
        {
            value = Deserialize(data, context);
            return true;
        }

        public ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This benchmark models a warm deserializer.");
    }
}
