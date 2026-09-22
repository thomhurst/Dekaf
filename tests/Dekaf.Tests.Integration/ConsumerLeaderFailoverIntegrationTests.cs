using Dekaf.Consumer;
using Dekaf.Producer;
using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// The prefetch position runs ahead of the consumed one. A leader change that falls between
    /// the two must not be reported as log truncation: the next fetch has to be validated
    /// against the epoch of the last FETCHED batch, not of the last consumed record.
    /// </summary>
    [Test]
    [Timeout(180_000)]
    [Arguments(AutoOffsetReset.Earliest)]
    [Arguments(AutoOffsetReset.None)]
    public async Task Consumer_LaggingItsPrefetchAcrossLeaderChange_DoesNotReportLogTruncation(
        AutoOffsetReset autoOffsetReset,
        CancellationToken cancellationToken)
    {
        const int beforeLeaderChange = 25;
        const int afterLeaderChange = 75;
        const int total = beforeLeaderChange + afterLeaderChange + 1;

        var topic = await kafka.CreateReplicatedTopicAsync().ConfigureAwait(false);
        var partition = new TopicPartition(topic, 0);
        int? stoppedBrokerId = null;
        using var scenarioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddProvider(logs);
        });

        await ProduceRangeAsync(topic, start: 0, count: beforeLeaderChange, scenarioCancellation.Token).ConfigureAwait(false);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"leader-failover-lagging-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(autoOffsetReset)
            .WithLoggerFactory(loggerFactory)
            .BuildAsync(scenarioCancellation.Token)
            .ConfigureAwait(false);
        // An explicit start offset keeps the None policy from needing a reset to begin with.
        consumer.IncrementalAssign([new TopicPartitionOffset(topic, 0, 0)]);

        var consumed = new List<ConsumeResult<string, string>>(total);
        try
        {
            // A slow application: everything written under the first leader epoch is processed,
            // then nothing is polled while the background prefetch keeps running. Reading the
            // position (as lag monitoring does) publishes the consumed offset and its epoch.
            await ConsumeUntilAsync(consumer, consumed, beforeLeaderChange, scenarioCancellation.Token)
                .ConfigureAwait(false);
            if (consumer.GetPosition(partition) != beforeLeaderChange)
                throw new InvalidOperationException($"Expected position {beforeLeaderChange} before the leader change.");

            stoppedBrokerId = await kafka.GetPartitionLeaderIdAsync(topic, scenarioCancellation.Token)
                .ConfigureAwait(false);
            await kafka.StopBrokerAsync(stoppedBrokerId.Value, scenarioCancellation.Token).ConfigureAwait(false);
            _ = await kafka.WaitForPartitionLeaderChangeAsync(
                    topic,
                    stoppedBrokerId.Value,
                    scenarioCancellation.Token)
                .ConfigureAwait(false);

            await ProduceRangeAsync(topic, beforeLeaderChange, afterLeaderChange, scenarioCancellation.Token)
                .ConfigureAwait(false);

            // The prefetch has fetched past the epoch boundary once it has seen these records,
            // and the fetch that returns the marker below was sent from that position, while the
            // consumed position is still the last offset of the first epoch. The waits are
            // bounded: a consumer that reports a divergence parks its prefetch until the
            // application polls, and the assertions below describe that better than a timeout.
            await WaitForCachedHighWatermarkAsync(
                    consumer, partition, beforeLeaderChange + afterLeaderChange, scenarioCancellation.Token)
                .ConfigureAwait(false);
            await ProduceRangeAsync(topic, total - 1, count: 1, scenarioCancellation.Token).ConfigureAwait(false);
            await WaitForCachedHighWatermarkAsync(consumer, partition, total, scenarioCancellation.Token)
                .ConfigureAwait(false);

            await kafka.StartBrokerAsync(stoppedBrokerId.Value, scenarioCancellation.Token).ConfigureAwait(false);
            await kafka.WaitForInSyncReplicasAsync(topic, 3, scenarioCancellation.Token).ConfigureAwait(false);
            stoppedBrokerId = null;

            // With AutoOffsetReset.None a reported divergence surfaces here as LogTruncationException.
            await ConsumeUntilAsync(consumer, consumed, total, scenarioCancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            scenarioCancellation.Cancel();
            if (stoppedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }

        for (var index = 0; index < total; index++)
        {
            if (consumed[index].Offset != index || consumed[index].Value != index.ToString())
            {
                throw new InvalidOperationException(
                    $"Expected offset and value {index} at index {index}. " +
                    $"Actual offsets: [{string.Join(", ", consumed.Select(static item => item.Offset))}]");
            }
        }

        var truncationReports = logs.Entries
            .Where(static entry => entry.Message.Contains("Log truncation detected", StringComparison.Ordinal))
            .Select(static entry => entry.Message)
            .ToArray();
        if (truncationReports.Length != 0)
        {
            throw new InvalidOperationException(
                "Nothing was truncated, but the consumer reported: " + string.Join(" | ", truncationReports));
        }
    }

    private static async Task ConsumeUntilAsync(
        IKafkaConsumer<string, string> consumer,
        List<ConsumeResult<string, string>> consumed,
        int count,
        CancellationToken cancellationToken)
    {
        while (consumed.Count < count)
        {
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cancellationToken)
                .ConfigureAwait(false);
            if (result is not null)
                consumed.Add(result.Value);
        }
    }

    private static async Task WaitForCachedHighWatermarkAsync(
        IKafkaConsumer<string, string> consumer,
        TopicPartition partition,
        long highWatermark,
        CancellationToken cancellationToken)
    {
        // The cache is written by fetch responses only, so it reports how far the background
        // prefetch has seen without polling the consumer.
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            while (consumer.GetWatermarkOffsets(partition) is not { } watermarks || watermarks.High < highWatermark)
                await Task.Delay(50, bound.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
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
