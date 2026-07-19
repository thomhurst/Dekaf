using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
[Category("Resilience")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ConsumerLogTruncationIntegrationTests(RackAwareKafkaContainer kafka)
{
    private const int CommonRecordCount = 3;
    private const int UnreplicatedRecordCount = 3;
    private const int TruncatedPosition = CommonRecordCount + UnreplicatedRecordCount;

    [Test]
    [Timeout(240_000)]
    public async Task Consumer_UncleanLeaderElection_DetectsTruncation_DoesNotSilentlyContinue(
        CancellationToken cancellationToken)
    {
        string? topic = null;
        using var scenarioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            topic = await CreateDivergentLogAsync(scenarioCancellation.Token).ConfigureAwait(false);
            var partition = new TopicPartition(topic, 0);
            ConsumeResult<string, string> consumedTail;
            await using (var initialConsumer = await CreateAssignedConsumerAsync(
                topic,
                AutoOffsetReset.Earliest,
                scenarioCancellation.Token).ConfigureAwait(false))
            {
                consumedTail = await ConsumeThroughUnreplicatedTailAsync(
                    initialConsumer,
                    scenarioCancellation.Token).ConfigureAwait(false);
            }

            await ElectStaleFollowerAsync(topic, scenarioCancellation.Token).ConfigureAwait(false);
            await ProduceRangeAsync(
                topic,
                "replacement",
                CommonRecordCount,
                count: UnreplicatedRecordCount + 1,
                Acks.Leader,
                scenarioCancellation.Token).ConfigureAwait(false);
            await RestoreFormerLeaderAsync(topic, scenarioCancellation.Token).ConfigureAwait(false);

            await using var validatingConsumer = await CreateAssignedConsumerAsync(
                topic,
                AutoOffsetReset.None,
                scenarioCancellation.Token,
                offset: TruncatedPosition,
                leaderEpoch: consumedTail.LeaderEpoch ?? -1).ConfigureAwait(false);
            var exception = await Assert.That(async () => await validatingConsumer
                    .ConsumeOneAsync(TimeSpan.FromSeconds(60), scenarioCancellation.Token)
                    .ConfigureAwait(false))
                .Throws<LogTruncationException>();

            await Assert.That(consumedTail.Offset).IsEqualTo(TruncatedPosition - 1);
            await Assert.That(consumedTail.LeaderEpoch).IsNotNull();
            var truncation = exception!.TruncationOffsets.Single();
            await Assert.That(truncation.Topic).IsEqualTo(topic);
            await Assert.That(truncation.Partition).IsEqualTo(partition.Partition);
            await Assert.That(truncation.Offset).IsEqualTo(CommonRecordCount);
            await Assert.That(truncation.LeaderEpoch).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            scenarioCancellation.Cancel();
            await RestoreClusterAsync(topic).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(240_000)]
    public async Task Consumer_TruncationWithAutoOffsetReset_RepositionsDeterministically(
        CancellationToken cancellationToken)
    {
        string? topic = null;
        using var scenarioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var logs = new CapturingLoggerProvider();
        using var loggerFactory = CreateLoggerFactory(logs);

        try
        {
            topic = await CreateCommonLogAsync(scenarioCancellation.Token).ConfigureAwait(false);
            var partition = new TopicPartition(topic, 0);

            await using var consumer = await CreateAssignedConsumerAsync(
                topic,
                AutoOffsetReset.Earliest,
                scenarioCancellation.Token,
                loggerFactory).ConfigureAwait(false);
            _ = await ConsumeRecordsAsync(consumer, CommonRecordCount, scenarioCancellation.Token)
                .ConfigureAwait(false);

            await AppendUnreplicatedTailAsync(topic, scenarioCancellation.Token).ConfigureAwait(false);
            _ = await ConsumeRecordsAsync(consumer, UnreplicatedRecordCount, scenarioCancellation.Token)
                .ConfigureAwait(false);
            consumer.Pause(partition);

            await ElectStaleFollowerAsync(topic, scenarioCancellation.Token).ConfigureAwait(false);
            await ProduceRangeAsync(
                topic,
                "replacement",
                CommonRecordCount,
                count: UnreplicatedRecordCount + 1,
                Acks.Leader,
                scenarioCancellation.Token).ConfigureAwait(false);
            await RestoreFormerLeaderAsync(topic, scenarioCancellation.Token).ConfigureAwait(false);

            consumer.Resume(partition);
            var firstAfterTruncation = await consumer
                .ConsumeOneAsync(TimeSpan.FromSeconds(60), scenarioCancellation.Token)
                .ConfigureAwait(false);

            await Assert.That(firstAfterTruncation).IsNotNull();
            await Assert.That(firstAfterTruncation!.Value.Offset).IsEqualTo(CommonRecordCount);
            await Assert.That(firstAfterTruncation.Value.Value).IsEqualTo($"replacement-{CommonRecordCount}");
            await Assert.That(logs.Entries).Contains(entry =>
                entry.Message.Contains($"Log truncation detected for {topic}-0", StringComparison.Ordinal)
                && entry.TryGetProperty<long>("ResumeOffset", out var resumeOffset)
                && resumeOffset == CommonRecordCount
                && entry.TryGetProperty<long>("EndOffset", out var endOffset)
                && endOffset == CommonRecordCount);
        }
        finally
        {
            scenarioCancellation.Cancel();
            await RestoreClusterAsync(topic).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(300_000)]
    public async Task Consumer_CommittedOffsetBeyondTruncatedLog_GroupRestartRecovers(
        CancellationToken cancellationToken)
    {
        string? topic = null;
        var groupId = $"truncation-group-{Guid.NewGuid():N}";
        using var scenarioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            topic = await CreateDivergentLogAsync(scenarioCancellation.Token).ConfigureAwait(false);

            await using (var firstConsumer = await CreateGroupConsumerAsync(
                topic,
                groupId,
                scenarioCancellation.Token).ConfigureAwait(false))
            {
                var consumedTail = await ConsumeThroughUnreplicatedTailAsync(
                    firstConsumer,
                    scenarioCancellation.Token).ConfigureAwait(false);
                await firstConsumer.CommitAsync([
                    new TopicPartitionOffset(
                        topic,
                        partition: 0,
                        TruncatedPosition,
                        consumedTail.LeaderEpoch ?? -1)
                ], scenarioCancellation.Token).ConfigureAwait(false);
            }

            await ElectStaleFollowerAsync(topic, scenarioCancellation.Token).ConfigureAwait(false);
            await ProduceRangeAsync(
                topic,
                "replacement",
                CommonRecordCount,
                count: UnreplicatedRecordCount + 1,
                Acks.Leader,
                scenarioCancellation.Token).ConfigureAwait(false);

            await using var restartedConsumer = await CreateGroupConsumerAsync(
                topic,
                groupId,
                scenarioCancellation.Token).ConfigureAwait(false);
            var firstAfterRestart = await restartedConsumer
                .ConsumeOneAsync(TimeSpan.FromSeconds(60), scenarioCancellation.Token)
                .ConfigureAwait(false);

            await Assert.That(firstAfterRestart).IsNotNull();
            await Assert.That(firstAfterRestart!.Value.Offset).IsEqualTo(CommonRecordCount);
            await Assert.That(firstAfterRestart.Value.Value).IsEqualTo($"replacement-{CommonRecordCount}");
        }
        finally
        {
            scenarioCancellation.Cancel();
            await RestoreClusterAsync(topic).ConfigureAwait(false);
        }
    }

    private async Task<string> CreateDivergentLogAsync(CancellationToken cancellationToken)
    {
        var topic = await CreateCommonLogAsync(cancellationToken).ConfigureAwait(false);
        await AppendUnreplicatedTailAsync(topic, cancellationToken).ConfigureAwait(false);
        return topic;
    }

    private async Task<string> CreateCommonLogAsync(CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateUncleanElectionTopicAsync().ConfigureAwait(false);
        await ProduceRangeAsync(
            topic,
            "common",
            start: 0,
            CommonRecordCount,
            Acks.All,
            cancellationToken).ConfigureAwait(false);
        return topic;
    }

    private async Task AppendUnreplicatedTailAsync(string topic, CancellationToken cancellationToken)
    {
        await kafka.StopBrokerAsync(nodeId: 2, cancellationToken).ConfigureAwait(false);
        await kafka.WaitForInSyncReplicasAsync(topic, expectedCount: 1, cancellationToken)
            .ConfigureAwait(false);

        await ProduceRangeAsync(
            topic,
            "unreplicated",
            CommonRecordCount,
            UnreplicatedRecordCount,
            Acks.Leader,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ElectStaleFollowerAsync(string topic, CancellationToken cancellationToken)
    {
        await kafka.StopBrokerAsync(nodeId: 1, cancellationToken).ConfigureAwait(false);
        await kafka.StartBrokerWithoutClusterWaitAsync(nodeId: 2, cancellationToken).ConfigureAwait(false);

        var newLeader = await kafka.WaitForPartitionLeaderChangeAsync(
            topic,
            previousLeaderId: 1,
            cancellationToken).ConfigureAwait(false);
        if (newLeader != 2)
            throw new InvalidOperationException($"Expected stale broker 2 to become leader, actual broker {newLeader}.");
    }

    private async Task RestoreClusterAsync(string? topic)
    {
        await kafka.StartBrokersAsync([1, 2], CancellationToken.None).ConfigureAwait(false);
        if (topic is not null)
            await kafka.WaitForInSyncReplicasAsync(topic, expectedCount: 2, CancellationToken.None)
                .ConfigureAwait(false);
    }

    private async Task RestoreFormerLeaderAsync(string topic, CancellationToken cancellationToken)
    {
        await kafka.StartBrokerAsync(nodeId: 1, cancellationToken).ConfigureAwait(false);
        await kafka.WaitForInSyncReplicasAsync(topic, expectedCount: 2, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IKafkaConsumer<string, string>> CreateAssignedConsumerAsync(
        string topic,
        AutoOffsetReset autoOffsetReset,
        CancellationToken cancellationToken,
        ILoggerFactory? loggerFactory = null,
        long offset = 0,
        int leaderEpoch = -1)
    {
        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"truncation-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(autoOffsetReset)
            .WithLoggerFactory(loggerFactory ?? GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
        consumer.IncrementalAssign([new TopicPartitionOffset(topic, 0, offset, leaderEpoch)]);
        return consumer;
    }

    private async Task<IKafkaConsumer<string, string>> CreateGroupConsumerAsync(
        string topic,
        string groupId,
        CancellationToken cancellationToken)
    {
        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"truncation-group-consumer-{Guid.NewGuid():N}")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
        consumer.Subscribe(topic);
        return consumer;
    }

    private static Task<ConsumeResult<string, string>> ConsumeThroughUnreplicatedTailAsync(
        IKafkaConsumer<string, string> consumer,
        CancellationToken cancellationToken) =>
        ConsumeRecordsAsync(consumer, TruncatedPosition, cancellationToken);

    private static async Task<ConsumeResult<string, string>> ConsumeRecordsAsync(
        IKafkaConsumer<string, string> consumer,
        int count,
        CancellationToken cancellationToken)
    {
        ConsumeResult<string, string>? last = null;
        for (var index = 0; index < count; index++)
        {
            last = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(60), cancellationToken)
                .ConfigureAwait(false);
            if (last is null)
                throw new InvalidOperationException($"Expected record {index} before truncation.");
        }

        return last ?? throw new InvalidOperationException("No records were consumed before truncation.");
    }

    private async Task ProduceRangeAsync(
        string topic,
        string valuePrefix,
        int start,
        int count,
        Acks acks,
        CancellationToken cancellationToken)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId($"truncation-producer-{Guid.NewGuid():N}")
            .WithAcks(acks)
            .WithIdempotence(acks == Acks.All)
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
                Value = $"{valuePrefix}-{value}"
            }, cancellationToken).ConfigureAwait(false);
        }

        await producer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ILoggerFactory CreateLoggerFactory(CapturingLoggerProvider logs) =>
        LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddProvider(logs);
        });
}
