using Dekaf.Admin;
using Dekaf.Producer;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Share consumption across partition-level ShareFetch errors: a leadership move and a deleted topic.
/// </summary>
[Category("ShareConsumer")]
[Category("Resilience")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ShareConsumerLeadershipTests(RackAwareKafkaContainer kafka)
{
    // Far below the share consumer's 15-minute background metadata refresh, which was the only
    // way to find a moved leader before the consumer applied partition-level fetch errors.
    private static readonly TimeSpan ResumeTimeout = TimeSpan.FromSeconds(30);

    [Test]
    [Timeout(240_000)]
    public async Task LeaderMove_DuringConsumption_ResumesPromptly(CancellationToken cancellationToken)
    {
        if (!RackAwareKafkaContainer.SupportsShareGroups)
            Skip.Test($"Share groups need Kafka 4.2 or later; this run uses {KafkaContainerDefault.ImageTag}.");

        var topic = await kafka.CreateTopicWithRemoteLeaderAndLocalFollowerAsync().ConfigureAwait(false);
        var partition = new TopicPartition(topic, 0);
        var group = $"share-leader-move-{Guid.NewGuid():N}";
        await using var admin = kafka.CreateAdminClient();
        await ShareConsumerPartitionErrorHelper.ConsumeFromEarliestAsync(admin, group, cancellationToken)
            .ConfigureAwait(false);
        await using var producer = await ShareConsumerPartitionErrorHelper
            .CreateProducerAsync(kafka.BootstrapServers, cancellationToken).ConfigureAwait(false);
        await using var consumer = await ShareConsumerPartitionErrorHelper
            .CreateConsumerAsync(kafka.BootstrapServers, group, cancellationToken).ConfigureAwait(false);
        consumer.Subscribe(topic);

        var received = new HashSet<string>();
        await ShareConsumerPartitionErrorHelper.ProduceAsync(producer, topic, "before", 10, cancellationToken)
            .ConfigureAwait(false);
        await ShareConsumerPartitionErrorHelper.ConsumeAsync(consumer, received, 10, TimeSpan.FromSeconds(60),
            cancellationToken).ConfigureAwait(false);

        // Move leadership from broker 1 to broker 2 while the consumer's metadata still names broker 1.
        await kafka.WaitForInSyncReplicasAsync(topic, expectedCount: 2, cancellationToken).ConfigureAwait(false);
        await admin.AlterPartitionReassignmentsAsync(
                new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>
                {
                    [partition] = NewPartitionReassignment.ToReplicas([2, 1])
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await WaitForLeaderAsync(admin, topic, expectedLeader: 2, elect: true, cancellationToken).ConfigureAwait(false);

        await ShareConsumerPartitionErrorHelper.ProduceAsync(producer, topic, "after", 10, cancellationToken)
            .ConfigureAwait(false);
        await ShareConsumerPartitionErrorHelper.ConsumeAsync(consumer, received, 20, ResumeTimeout, cancellationToken)
            .ConfigureAwait(false);

        await Assert.That(received.Count(static key => key.StartsWith("after-", StringComparison.Ordinal)))
            .IsEqualTo(10);
    }

    private static async Task WaitForLeaderAsync(
        IAdminClient admin, string topic, int expectedLeader, bool elect, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            while (true)
            {
                var active = await admin.ListPartitionReassignmentsAsync(
                        [new TopicPartition(topic, 0)], cancellationToken: timeout.Token)
                    .ConfigureAwait(false);
                if (active.Count == 0)
                {
                    var descriptions = await admin.DescribeTopicsAsync([topic], timeout.Token).ConfigureAwait(false);
                    var description = descriptions[topic].Partitions.Single();
                    if (description.LeaderId == expectedLeader)
                        return;
                    if (elect && description.ReplicaNodes.Count > 0 && description.ReplicaNodes[0] == expectedLeader
                        && description.IsrNodes.Contains(expectedLeader))
                    {
                        await admin.ElectLeadersAsync(ElectionType.Preferred, [new TopicPartition(topic, 0)],
                            cancellationToken: timeout.Token).ConfigureAwait(false);
                    }
                }
                await Task.Delay(TimeSpan.FromMilliseconds(200), timeout.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Topic '{topic}' did not move its leader to broker {expectedLeader}.");
        }
    }
}

[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public sealed class ShareConsumerTopicDeletionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task DeletedTopic_DuringConsumption_OtherTopicKeepsConsuming()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var deleted = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var kept = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var group = $"share-topic-deletion-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await ShareConsumerPartitionErrorHelper.ConsumeFromEarliestAsync(admin, group, timeout.Token);
        await using var producer = await ShareConsumerPartitionErrorHelper
            .CreateProducerAsync(KafkaContainer.BootstrapServers, timeout.Token);
        await using var consumer = await ShareConsumerPartitionErrorHelper
            .CreateConsumerAsync(KafkaContainer.BootstrapServers, group, timeout.Token);
        consumer.Subscribe(deleted, kept);

        var received = new HashSet<string>();
        await ShareConsumerPartitionErrorHelper.ProduceAsync(producer, deleted, "deleted", 2, timeout.Token);
        await ShareConsumerPartitionErrorHelper.ProduceAsync(producer, kept, "before", 4, timeout.Token);
        await ShareConsumerPartitionErrorHelper.ConsumeAsync(consumer, received, 6, TimeSpan.FromSeconds(60),
            timeout.Token);

        await admin.DeleteTopicsAsync([deleted], cancellationToken: timeout.Token);
        await ShareConsumerPartitionErrorHelper.ProduceAsync(producer, kept, "after", 4, timeout.Token);
        await ShareConsumerPartitionErrorHelper.ConsumeAsync(consumer, received, 10, TimeSpan.FromSeconds(30),
            timeout.Token);

        await Assert.That(received.Count(static key => key.StartsWith("after-", StringComparison.Ordinal)))
            .IsEqualTo(4);
    }
}

internal static class ShareConsumerPartitionErrorHelper
{
    internal static async Task ConsumeFromEarliestAsync(IAdminClient admin, string group, CancellationToken cancellationToken)
        => await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        }, cancellationToken: cancellationToken).ConfigureAwait(false);

    internal static async Task<IKafkaProducer<string, string>> CreateProducerAsync(
        string bootstrapServers, CancellationToken cancellationToken)
        => await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);

    internal static async Task<IKafkaShareConsumer<string, string>> CreateConsumerAsync(
        string bootstrapServers, string group, CancellationToken cancellationToken)
        => await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithGroupId(group)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Implicit)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);

    internal static async Task ProduceAsync(
        IKafkaProducer<string, string> producer, string topic, string prefix, int count,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < count; index++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"{prefix}-{index}",
                Value = "value"
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Polls until <paramref name="received"/> holds <paramref name="expectedCount"/> distinct keys.</summary>
    internal static async Task ConsumeAsync(
        IKafkaShareConsumer<string, string> consumer, HashSet<string> received, int expectedCount,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            await foreach (var record in consumer.PollAsync(deadline.Token).ConfigureAwait(false))
            {
                received.Add(record.Key!);
                if (received.Count >= expectedCount)
                    return;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        throw new TimeoutException(
            $"Share consumer received {received.Count} of {expectedCount} records within {timeout.TotalSeconds:0} s.");
    }
}
