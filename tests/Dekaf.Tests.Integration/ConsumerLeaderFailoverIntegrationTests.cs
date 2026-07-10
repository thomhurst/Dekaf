using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
[Category("Resilience")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ConsumerLeaderFailoverIntegrationTests(RackAwareKafkaContainer kafka)
{
    private const int MessageCount = 200;

    [Test]
    [Timeout(180_000)]
    public async Task Consumer_PartitionLeaderHandoff_ContinuesWithoutSkippedOrDuplicateRecords(
        CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateReplicatedTopicAsync().ConfigureAwait(false);
        int? stoppedBrokerId = null;
        using var scenarioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await using var consumer = await CreateConsumerAsync(scenarioCancellation.Token).ConfigureAwait(false);
        consumer.Assign(new TopicPartition(topic, 0));

        var firstConsumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumeTask = ConsumeSequenceAsync(consumer, firstConsumed, scenarioCancellation.Token);

        try
        {
            await ProduceRangeAsync(topic, start: 0, count: 25, scenarioCancellation.Token).ConfigureAwait(false);
            await firstConsumed.Task.WaitAsync(scenarioCancellation.Token).ConfigureAwait(false);

            stoppedBrokerId = await kafka.GetPartitionLeaderIdAsync(topic, scenarioCancellation.Token)
                .ConfigureAwait(false);
            await kafka.StopBrokerAsync(stoppedBrokerId.Value, scenarioCancellation.Token).ConfigureAwait(false);
            _ = await kafka.WaitForPartitionLeaderChangeAsync(
                    topic,
                    stoppedBrokerId.Value,
                    scenarioCancellation.Token)
                .ConfigureAwait(false);

            // A fresh producer isolates this consumer-recovery test from the separate
            // stale-producer-metadata path while still producing under the broker fault.
            await ProduceRangeAsync(topic, start: 25, count: 75, scenarioCancellation.Token).ConfigureAwait(false);

            await kafka.StartBrokerAsync(stoppedBrokerId.Value, scenarioCancellation.Token).ConfigureAwait(false);
            await kafka.WaitForInSyncReplicasAsync(topic, 3, scenarioCancellation.Token).ConfigureAwait(false);
            stoppedBrokerId = null;

            await ProduceRangeAsync(topic, start: 100, count: 100, scenarioCancellation.Token).ConfigureAwait(false);
            var consumed = await consumeTask.ConfigureAwait(false);
            AssertCompleteSequence(consumed);
        }
        finally
        {
            scenarioCancellation.Cancel();
            if (stoppedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(240_000)]
    public async Task Consumer_ThreeBrokerRollingRestart_ContinuesWithoutSkippedOrDuplicateRecords(
        CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateReplicatedTopicAsync().ConfigureAwait(false);
        int? stoppedBrokerId = null;
        using var scenarioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await using var consumer = await CreateConsumerAsync(scenarioCancellation.Token).ConfigureAwait(false);
        consumer.Assign(new TopicPartition(topic, 0));

        var firstConsumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumeTask = ConsumeSequenceAsync(consumer, firstConsumed, scenarioCancellation.Token);

        try
        {
            await ProduceRangeAsync(topic, start: 0, count: 25, scenarioCancellation.Token).ConfigureAwait(false);
            await firstConsumed.Task.WaitAsync(scenarioCancellation.Token).ConfigureAwait(false);

            var nextValue = 25;
            for (var nodeId = 1; nodeId <= 3; nodeId++)
            {
                stoppedBrokerId = nodeId;
                var leaderId = await kafka.GetPartitionLeaderIdAsync(topic, scenarioCancellation.Token)
                    .ConfigureAwait(false);
                await kafka.StopBrokerAsync(nodeId, scenarioCancellation.Token).ConfigureAwait(false);
                if (leaderId == nodeId)
                {
                    _ = await kafka.WaitForPartitionLeaderChangeAsync(topic, leaderId, scenarioCancellation.Token)
                        .ConfigureAwait(false);
                }

                // Every phase acknowledges 50 records with one broker down while the
                // same consumer continues polling through metadata/epoch changes.
                await ProduceRangeAsync(topic, nextValue, count: 50, scenarioCancellation.Token)
                    .ConfigureAwait(false);
                nextValue += 50;

                await kafka.StartBrokerAsync(nodeId, scenarioCancellation.Token).ConfigureAwait(false);
                await kafka.WaitForInSyncReplicasAsync(topic, 3, scenarioCancellation.Token).ConfigureAwait(false);
                stoppedBrokerId = null;
            }

            await ProduceRangeAsync(
                    topic,
                    nextValue,
                    MessageCount - nextValue,
                    scenarioCancellation.Token)
                .ConfigureAwait(false);

            var consumed = await consumeTask.ConfigureAwait(false);
            AssertCompleteSequence(consumed);
        }
        finally
        {
            scenarioCancellation.Cancel();
            if (stoppedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<IKafkaConsumer<string, string>> CreateConsumerAsync(CancellationToken cancellationToken)
    {
        return await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"leader-failover-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ProduceRangeAsync(
        string topic,
        int start,
        int count,
        CancellationToken cancellationToken)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"leader-failover-producer-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithIdempotence(true)
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(45))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);

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

    private static async Task<IReadOnlyList<ConsumeResult<string, string>>> ConsumeSequenceAsync(
        IKafkaConsumer<string, string> consumer,
        TaskCompletionSource firstConsumed,
        CancellationToken cancellationToken)
    {
        var results = new List<ConsumeResult<string, string>>(MessageCount);
        while (results.Count < MessageCount)
        {
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cancellationToken)
                .ConfigureAwait(false);
            if (result is null)
                continue;

            results.Add(result.Value);
            firstConsumed.TrySetResult();
        }

        return results;
    }

    private static void AssertCompleteSequence(IReadOnlyList<ConsumeResult<string, string>> consumed)
    {
        if (consumed.Count != MessageCount)
            throw new InvalidOperationException($"Expected {MessageCount} records, actual {consumed.Count}.");

        for (var index = 0; index < MessageCount; index++)
        {
            var result = consumed[index];
            if (result.Offset != index)
            {
                throw new InvalidOperationException(
                    $"Offset mismatch at index {index}: expected {index}, actual {result.Offset}. " +
                    $"Actual offsets: [{string.Join(", ", consumed.Select(static item => item.Offset))}]");
            }

            var expectedValue = index.ToString();
            if (result.Value != expectedValue)
            {
                throw new InvalidOperationException(
                    $"Value mismatch at index {index}: expected {expectedValue}, actual {result.Value}.");
            }
        }
    }
}
