using System.Diagnostics;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
[ClassDataSource<OffsetRetentionKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ConsumerOffsetRetentionIntegrationTests(OffsetRetentionKafkaContainer kafka)
{
    private static readonly TimeSpan OffsetExpiryTimeout = TimeSpan.FromSeconds(100);
    private static readonly TimeSpan ActiveRetentionObservation = TimeSpan.FromSeconds(70);

    [Test]
    [Timeout(180_000)]
    public async Task ConsumerGroup_OffsetsExpiredWhileEmpty_EarliestReset_ReprocessesFromStart(
        CancellationToken cancellationToken)
    {
        var scenario = await CreateExpiredOffsetScenarioAsync(cancellationToken).ConfigureAwait(false);
        await ProduceRangeAsync(scenario.Topic, start: 5, count: 2, cancellationToken).ConfigureAwait(false);

        await using var consumer = await CreateConsumerAsync(
            scenario.Topic,
            scenario.GroupId,
            AutoOffsetReset.Earliest,
            cancellationToken).ConfigureAwait(false);

        var records = await ConsumeCountAsync(consumer, count: 7, cancellationToken).ConfigureAwait(false);

        await Assert.That(records.Select(static record => record.Offset)).IsEquivalentTo([0L, 1L, 2L, 3L, 4L, 5L, 6L]);
    }

    [Test]
    [Timeout(180_000)]
    public async Task ConsumerGroup_OffsetsExpiredWhileEmpty_LatestReset_SkipsToEnd(
        CancellationToken cancellationToken)
    {
        var scenario = await CreateExpiredOffsetScenarioAsync(cancellationToken).ConfigureAwait(false);
        await ProduceRangeAsync(scenario.Topic, start: 5, count: 2, cancellationToken).ConfigureAwait(false);

        await using var consumer = await CreateConsumerAsync(
            scenario.Topic,
            scenario.GroupId,
            AutoOffsetReset.Latest,
            cancellationToken).ConfigureAwait(false);
        var partition = new TopicPartition(scenario.Topic, 0);
        var consumeTask = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(60), cancellationToken).AsTask();

        await WaitForPositionAsync(consumer, partition, expected: 7, cancellationToken).ConfigureAwait(false);
        await ProduceRangeAsync(scenario.Topic, start: 7, count: 1, cancellationToken).ConfigureAwait(false);

        var record = await consumeTask.ConfigureAwait(false);
        await Assert.That(record).IsNotNull();
        await Assert.That(record!.Value.Offset).IsEqualTo(7);
    }

    [Test]
    [Timeout(180_000)]
    public async Task ConsumerGroup_OffsetsExpired_AutoOffsetResetNone_ThrowsClearException(
        CancellationToken cancellationToken)
    {
        var scenario = await CreateExpiredOffsetScenarioAsync(cancellationToken).ConfigureAwait(false);
        await using var consumer = await CreateConsumerAsync(
            scenario.Topic,
            scenario.GroupId,
            AutoOffsetReset.None,
            cancellationToken).ConfigureAwait(false);

        var exception = await Assert.That(async () =>
                await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false))
            .Throws<KafkaException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.OffsetOutOfRange);
        await Assert.That(exception.Message).Contains(scenario.GroupId);
        await Assert.That(exception.Message).Contains($"{scenario.Topic}-0");
    }

    [Test]
    [Timeout(180_000)]
    public async Task ConsumerGroup_ActiveGroup_OffsetsNotExpired(CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"active-retention-{Guid.NewGuid():N}";
        var partition = new TopicPartition(topic, 0);
        await ProduceRangeAsync(topic, start: 0, count: 5, cancellationToken).ConfigureAwait(false);

        await using var consumer = await CreateConsumerAsync(
            topic,
            groupId,
            AutoOffsetReset.Earliest,
            cancellationToken).ConfigureAwait(false);
        _ = await ConsumeCountAsync(consumer, count: 3, cancellationToken).ConfigureAwait(false);
        await consumer.CommitAsync([
            new TopicPartitionOffset(partition.Topic, partition.Partition, 3)
        ], cancellationToken).ConfigureAwait(false);

        await using var admin = kafka.CreateAdminClient();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < ActiveRetentionObservation)
        {
            var offsets = await admin.ListConsumerGroupOffsetsAsync(groupId, cancellationToken).ConfigureAwait(false);
            await Assert.That(offsets.GetValueOrDefault(partition, -1)).IsEqualTo(3);
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ExpiredOffsetScenario> CreateExpiredOffsetScenarioAsync(CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"expired-retention-{Guid.NewGuid():N}";
        var partition = new TopicPartition(topic, 0);
        await ProduceRangeAsync(topic, start: 0, count: 5, cancellationToken).ConfigureAwait(false);

        await using (var consumer = await CreateConsumerAsync(
            topic,
            groupId,
            AutoOffsetReset.Earliest,
            cancellationToken).ConfigureAwait(false))
        {
            _ = await ConsumeCountAsync(consumer, count: 3, cancellationToken).ConfigureAwait(false);
            await consumer.CommitAsync([
                new TopicPartitionOffset(partition.Topic, partition.Partition, 3)
            ], cancellationToken).ConfigureAwait(false);
        }

        await using var admin = kafka.CreateAdminClient();
        await WaitForOffsetAsync(admin, groupId, partition, shouldExist: true, TimeSpan.FromSeconds(15), cancellationToken)
            .ConfigureAwait(false);
        await WaitForOffsetAsync(admin, groupId, partition, shouldExist: false, OffsetExpiryTimeout, cancellationToken)
            .ConfigureAwait(false);

        return new ExpiredOffsetScenario(topic, groupId);
    }

    private async Task ProduceRangeAsync(string topic, int start, int count, CancellationToken cancellationToken)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"offset-retention-producer-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken).ConfigureAwait(false);

        for (var value = start; value < start + count; value++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = 0,
                Key = value.ToString(),
                Value = value.ToString()
            }, cancellationToken).ConfigureAwait(false);
        }

        await producer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IKafkaConsumer<string, string>> CreateConsumerAsync(
        string topic,
        string groupId,
        AutoOffsetReset reset,
        CancellationToken cancellationToken)
    {
        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"offset-retention-consumer-{Guid.NewGuid():N}")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(reset)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken).ConfigureAwait(false);
        consumer.Subscribe(topic);
        return consumer;
    }

    private static async Task<List<ConsumeResult<string, string>>> ConsumeCountAsync(
        IKafkaConsumer<string, string> consumer,
        int count,
        CancellationToken cancellationToken)
    {
        var records = new List<ConsumeResult<string, string>>(count);
        while (records.Count < count)
        {
            var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            if (record is null)
                throw new TimeoutException($"Consumed {records.Count} of {count} expected records.");
            records.Add(record.Value);
        }

        return records;
    }

    private static async Task WaitForOffsetAsync(
        IAdminClient admin,
        string groupId,
        TopicPartition partition,
        bool shouldExist,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            var offsets = await admin.ListConsumerGroupOffsetsAsync(groupId, cancellationToken).ConfigureAwait(false);
            if (offsets.ContainsKey(partition) == shouldExist)
                return;
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Offset for group '{groupId}', partition {partition} did not become {(shouldExist ? "present" : "absent")}.");
    }

    private static async Task WaitForPositionAsync(
        IKafkaConsumer<string, string> consumer,
        TopicPartition partition,
        long expected,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (consumer.GetPosition(partition) == expected)
                return;
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Consumer position for {partition} did not reach {expected}.");
    }

    private sealed record ExpiredOffsetScenario(string Topic, string GroupId);
}
