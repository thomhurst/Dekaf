using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
[SupportsKafka(400)]
public sealed class ConsumerHeartbeatCallbackIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Timeout(120_000)]
    public async Task RevocationLongerThanSessionTimeout_RetainsMembershipAndOwnership(
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"slow-revoke-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var resource = new ConfigResource { Type = ConfigResourceType.Group, Name = groupId };
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [resource] =
            [
                ConfigAlter.Set("consumer.session.timeout.ms", "6000"),
                ConfigAlter.Set("consumer.heartbeat.interval.ms", "1000")
            ]
        }, cancellationToken: cancellationToken);
        await Assert.That(async () =>
        {
            var configs = (await admin.DescribeConfigsAsync([resource], cancellationToken: cancellationToken))[resource];
            return configs.Single(entry => entry.Name == "consumer.session.timeout.ms").Value;
        }).Eventually(value => value.IsEqualTo("6000"), TimeSpan.FromSeconds(15));

        await using var pool = new ConnectionPool("slow-revoke-test", new ConnectionOptions());
        await using var metadata = new MetadataManager(pool, [KafkaContainer.BootstrapServers]);
        await metadata.InitializeAsync(cancellationToken);
        var listener = new BlockingRevocationListener();
        var options = new ConsumerOptions
        {
            BootstrapServers = [KafkaContainer.BootstrapServers],
            GroupId = groupId,
            SessionTimeoutMs = 6000,
            RebalanceTimeoutMs = 60_000,
            RebalanceListener = listener
        };
        await using var first = new ConsumerCoordinator(options, pool, metadata);
        await using var second = new ConsumerCoordinator(new ConsumerOptions
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = groupId,
            SessionTimeoutMs = 6000,
            RebalanceTimeoutMs = 60_000
        }, pool, metadata);
        try
        {
            await first.EnsureActiveGroupAsync(new HashSet<string> { topic }, cancellationToken);
            await Assert.That(() => first.Assignment.Count)
                .Eventually(count => count.IsEqualTo(2), TimeSpan.FromSeconds(30));
            var memberId = first.MemberId;
            await second.EnsureActiveGroupAsync(new HashSet<string> { topic }, cancellationToken);
            await listener.Entered.Task.WaitAsync(cancellationToken);

            // Deliberately span two broker session timeouts while the revocation is blocked.
            await Task.Delay(TimeSpan.FromSeconds(12), cancellationToken);
            var group = (await admin.DescribeConsumerGroupsAsync([groupId], cancellationToken))[groupId];
            var member = group.Members.Single(member => member.MemberId == memberId);
            await Assert.That(member.Assignment).Count().IsEqualTo(2);
            await Assert.That(first.State).IsEqualTo(CoordinatorState.Stable);
            await Assert.That(listener.LostCount).IsEqualTo(0);
            await Assert.That(second.Assignment).IsEmpty();

            listener.Release.TrySetResult();
            await Assert.That(() => second.Assignment.Count)
                .Eventually(count => count.IsEqualTo(1), TimeSpan.FromSeconds(30));
            await Assert.That(first.Assignment).Count().IsEqualTo(1);
            await Assert.That(first.MemberId).IsEqualTo(memberId);
            await Assert.That(listener.LostCount).IsEqualTo(0);
        }
        finally
        {
            listener.Release.TrySetResult();
            await first.LeaveGroupAsync(cancellationToken);
            await second.LeaveGroupAsync(cancellationToken);
        }
    }

    private sealed class BlockingRevocationListener : IRebalanceListener
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _lostCount;
        public int LostCount => Volatile.Read(ref _lostCount);

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public async ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _lostCount);
            return ValueTask.CompletedTask;
        }
    }
}
