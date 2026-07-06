using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerMaxPollRecordsTests
{
    private const string Topic = "test-topic";
    private const int Partition = 0;

    [Test]
    public async Task ConsumeBatchAsync_LimitsEachBatchToMaxPollRecords()
    {
        await using var consumer = CreateConsumerWithPendingFetches(
            maxPollRecords: 2,
            CreatePendingFetchData(messageCount: 5));

        var batchCounts = new List<int>();
        var offsets = new List<long>();

        await foreach (var batch in consumer.ConsumeBatchAsync(CancellationToken.None))
        {
            var currentOffsets = batch.Select(result => result.Offset).ToArray();
            batchCounts.Add(currentOffsets.Length);
            offsets.AddRange(currentOffsets);

            if (offsets.Count >= 5)
                break;
        }

        await Assert.That(batchCounts).IsEquivalentTo([2, 2, 1]);
        await Assert.That(offsets).IsEquivalentTo([0L, 1L, 2L, 3L, 4L]);
    }

    [Test]
    public async Task ConsumeRawBatchAsync_LimitsEachBatchToMaxPollRecords()
    {
        await using var consumer = CreateConsumerWithPendingFetches(
            maxPollRecords: 2,
            CreatePendingFetchData(messageCount: 5));

        var batchCounts = new List<int>();
        var offsets = new List<long>();

        await foreach (var batch in consumer.ConsumeRawBatchAsync(CancellationToken.None))
        {
            var currentOffsets = batch.Select(result => result.Offset).ToArray();
            batchCounts.Add(currentOffsets.Length);
            offsets.AddRange(currentOffsets);

            if (offsets.Count >= 5)
                break;
        }

        await Assert.That(batchCounts).IsEquivalentTo([2, 2, 1]);
        await Assert.That(offsets).IsEquivalentTo([0L, 1L, 2L, 3L, 4L]);
    }

    [Test]
    public async Task ConsumeBatchAsync_ExactMaxPollRecordsMultiple_RemovesExhaustedFetch()
    {
        await using var consumer = CreateConsumerWithPendingFetches(
            maxPollRecords: 2,
            CreatePendingFetchData(messageCount: 4));

        await using var enumerator = consumer.ConsumeBatchAsync(CancellationToken.None).GetAsyncEnumerator();

        await Assert.That(await enumerator.MoveNextAsync()).IsTrue();
        await Assert.That(enumerator.Current.Select(result => result.Offset).ToArray())
            .IsEquivalentTo([0L, 1L]);

        await Assert.That(await enumerator.MoveNextAsync()).IsTrue();
        await Assert.That(enumerator.Current.Select(result => result.Offset).ToArray())
            .IsEquivalentTo([2L, 3L]);

        await enumerator.DisposeAsync();

        await Assert.That(GetPendingFetches(consumer).Count).IsEqualTo(0);
    }

    private static KafkaConsumer<string, string> CreateConsumerWithPendingFetches(
        int maxPollRecords,
        params PendingFetchData[] fetches)
    {
        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1,
                MaxPollRecords = maxPollRecords
            },
            Serializers.String,
            Serializers.String);

        var tp = new TopicPartition(Topic, Partition);
        consumer.Assign(tp);
        SetInitialized(consumer);
        GetFetchPositions(consumer)[tp] = 0;

        var pendingFetches = GetPendingFetches(consumer);
        foreach (var fetch in fetches)
            pendingFetches.Enqueue(fetch);

        return consumer;
    }

    private static void SetInitialized(KafkaConsumer<string, string> consumer)
    {
        var initializedField = typeof(KafkaConsumer<string, string>)
            .GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_initialized field not found.");

        initializedField.SetValue(consumer, true);
    }

    private static ConcurrentDictionary<TopicPartition, long> GetFetchPositions(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_fetchPositions", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_fetchPositions field not found.");

        return (ConcurrentDictionary<TopicPartition, long>)field.GetValue(consumer)!;
    }

    private static Queue<PendingFetchData> GetPendingFetches(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_pendingFetches", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_pendingFetches field not found.");

        return (Queue<PendingFetchData>)field.GetValue(consumer)!;
    }

    private static PendingFetchData CreatePendingFetchData(int messageCount)
    {
        var records = new Record[messageCount];
        for (var i = 0; i < messageCount; i++)
        {
            records[i] = new Record
            {
                OffsetDelta = i,
                TimestampDelta = 0,
                Key = Encoding.UTF8.GetBytes($"key-{i}"),
                Value = Encoding.UTF8.GetBytes($"value-{i}"),
                IsKeyNull = false,
                IsValueNull = false,
                Headers = null,
                HeaderCount = 0
            };
        }

        var recordBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 1700000000000L,
            Records = records
        };

        return PendingFetchData.Create(Topic, Partition, [recordBatch]);
    }
}
