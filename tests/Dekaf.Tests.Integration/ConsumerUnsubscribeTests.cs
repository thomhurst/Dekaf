using System.Collections.Concurrent;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Unsubscribe and a switch to manual assignment end the consumer's group membership: the
/// consumer revokes what it owns (OnPartitionsRevoked), leaves the group (KIP-848 leave
/// heartbeat) and stops heartbeating, so the remaining members take over its partitions well
/// before a session timeout. A later Subscribe joins again with a fresh membership.
/// </summary>
[Category("ConsumerGroup")]
public sealed class ConsumerUnsubscribeTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    // Far below the 45 s default session timeout. A member that never leaves keeps
    // heartbeating, so its partitions would never move at all.
    private static readonly TimeSpan HandOffBound = TimeSpan.FromSeconds(15);

    [Test]
    public async Task Unsubscribe_WhileConsuming_HandsAllPartitionsToRemainingMember()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"unsubscribe-{Guid.NewGuid():N}";
        var listenerA = new RecordingListener();
        await using var memberA = await CreateGroupConsumerAsync(groupId, listenerA);
        await using var memberB = await CreateGroupConsumerAsync(groupId, new RecordingListener());
        await using var loopA = ConsumeLoop.Start(memberA);
        await using var loopB = ConsumeLoop.Start(memberB);

        memberA.Subscribe(topic);
        memberB.Subscribe(topic);
        var ownedByA = await WaitForSplitAsync(memberA, memberB, topic);

        var assignedCallsBeforeUnsubscribe = listenerA.AssignedCallCount;
        memberA.Unsubscribe();

        await Assert.That(memberA.Subscription).Count().IsEqualTo(0);
        await Assert.That(() => memberB.Assignment.Count(tp => tp.Topic == topic))
            .Eventually(count => count.IsEqualTo(4), HandOffBound);
        await Assert.That(() => listenerA.RevokedSnapshot().IsSupersetOf(ownedByA))
            .Eventually(revoked => revoked.IsTrue(), HandOffBound);

        // B consumes what is produced to A's former partitions.
        await ProduceToPartitionsAsync(topic, ownedByA, "after-unsubscribe");
        await Assert.That(() => loopB.ValuesFrom(topic, "after-unsubscribe").Count)
            .Eventually(count => count.IsEqualTo(ownedByA.Count), HandOffBound);

        await Assert.That(listenerA.AssignedCallCount).IsEqualTo(assignedCallsBeforeUnsubscribe);
        await Assert.That(memberA.Assignment.Count).IsEqualTo(0);
        await Assert.That(loopA.ValuesFrom(topic, "after-unsubscribe")).IsEmpty();
    }

    [Test]
    public async Task Assign_AfterSubscribe_LeavesGroupAndConsumesManualPartition()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var manualTopic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var groupId = $"unsubscribe-assign-{Guid.NewGuid():N}";
        var listenerA = new RecordingListener();
        await using var memberA = await CreateGroupConsumerAsync(groupId, listenerA);
        await using var memberB = await CreateGroupConsumerAsync(groupId, new RecordingListener());
        await using var loopA = ConsumeLoop.Start(memberA);
        await using var loopB = ConsumeLoop.Start(memberB);

        memberA.Subscribe(topic);
        memberB.Subscribe(topic);
        var ownedByA = await WaitForSplitAsync(memberA, memberB, topic);

        var assignedCallsBeforeAssign = listenerA.AssignedCallCount;
        memberA.Assign(new TopicPartition(manualTopic, 0));

        await Assert.That(() => memberB.Assignment.Count(tp => tp.Topic == topic))
            .Eventually(count => count.IsEqualTo(4), HandOffBound);
        await Assert.That(() => listenerA.RevokedSnapshot().IsSupersetOf(ownedByA))
            .Eventually(revoked => revoked.IsTrue(), HandOffBound);

        await ProduceToPartitionsAsync(topic, ownedByA, "after-assign");
        await ProduceToPartitionsAsync(manualTopic, [0], "manual");
        await Assert.That(() => loopB.ValuesFrom(topic, "after-assign").Count)
            .Eventually(count => count.IsEqualTo(ownedByA.Count), HandOffBound);
        await Assert.That(() => loopA.ValuesFrom(manualTopic, "manual").Count)
            .Eventually(count => count.IsEqualTo(1), HandOffBound);

        await Assert.That(listenerA.AssignedCallCount).IsEqualTo(assignedCallsBeforeAssign);
        await Assert.That(loopA.ValuesFrom(topic, "after-assign")).IsEmpty();
        await Assert.That(memberA.Assignment.ToArray())
            .IsEquivalentTo(new[] { new TopicPartition(manualTopic, 0) });
    }

    [Test]
    public async Task Unsubscribe_ThenSubscribe_RejoinsGroup()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"unsubscribe-rejoin-{Guid.NewGuid():N}";
        var listenerA = new RecordingListener();
        await using var memberA = await CreateGroupConsumerAsync(groupId, listenerA);
        await using var memberB = await CreateGroupConsumerAsync(groupId, new RecordingListener());
        await using var loopA = ConsumeLoop.Start(memberA);
        await using var loopB = ConsumeLoop.Start(memberB);

        memberA.Subscribe(topic);
        memberB.Subscribe(topic);
        await WaitForSplitAsync(memberA, memberB, topic);

        memberA.Unsubscribe();
        await Assert.That(() => memberB.Assignment.Count(tp => tp.Topic == topic))
            .Eventually(count => count.IsEqualTo(4), HandOffBound);

        var assignedCallsBeforeRejoin = listenerA.AssignedCallCount;
        memberA.Subscribe(topic);

        var ownedByA = await WaitForSplitAsync(memberA, memberB, topic);
        await Assert.That(listenerA.AssignedCallCount).IsGreaterThan(assignedCallsBeforeRejoin);

        await ProduceToPartitionsAsync(topic, ownedByA, "after-rejoin");
        await Assert.That(() => loopA.ValuesFrom(topic, "after-rejoin").Count)
            .Eventually(count => count.IsEqualTo(ownedByA.Count), HandOffBound);
    }

    [Test]
    public async Task Unsubscribe_AutoCommitFromConsumeLoop_CommitsProcessedRecordsOnRevoke()
    {
        // As for a cooperative revoke, an auto-commit consumer commits the offsets of the records
        // it processed for the partitions it gives up, although Unsubscribe clears the consumer's
        // state for them (and discards the fetch still being iterated) at once. The record being
        // processed when Unsubscribe is called is in doubt and is not committed (at-least-once).
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var groupId = $"unsubscribe-autocommit-{Guid.NewGuid():N}";
        await ProduceToPartitionsAsync(topic, [0, 0, 0, 0, 0], "record");

        await using var memberA = await CreateGroupConsumerAsync(
            groupId,
            new RecordingListener(),
            autoCommitInterval: TimeSpan.FromMinutes(5));
        memberA.Subscribe(topic);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await foreach (var record in memberA.ConsumeAsync(timeout.Token))
        {
            // Records 0-2 were processed; record 3 is being processed when the consumer leaves.
            if (record.Offset == 3)
            {
                memberA.Unsubscribe();
                break;
            }
        }

        await using var memberB = await CreateGroupConsumerAsync(groupId, new RecordingListener());
        memberB.Subscribe(topic);
        var first = await memberB.ConsumeOneAsync(HandOffBound, timeout.Token);

        await Assert.That(first).IsNotNull();
        await Assert.That(first!.Value.Offset).IsEqualTo(3);
    }

    [Test]
    public async Task Unsubscribe_CommitAsyncInRevokeCallback_NextMemberResumesAfterConsumedRecords()
    {
        // The documented pattern: commit from OnPartitionsRevokedAsync. Unsubscribe clears the
        // departing partitions at once, so the callback's CommitAsync() must commit what they held.
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var groupId = $"unsubscribe-revoke-commit-{Guid.NewGuid():N}";
        await ProduceToPartitionsAsync(topic, [0, 0, 0], "record");

        var listener = new CommittingListener();
        await using var memberA = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(listener)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        memberA.Subscribe(topic);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        for (var i = 0; i < 3; i++)
        {
            var record = await memberA.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token);
            await Assert.That(record).IsNotNull();
            await Assert.That(record!.Value.Offset).IsEqualTo(i);
        }

        memberA.Unsubscribe();
        await listener.RevokedCommitted.Task.WaitAsync(HandOffBound, timeout.Token);

        await ProduceToPartitionsAsync(topic, [0], "after-unsubscribe");
        await using var memberB = await CreateGroupConsumerAsync(groupId, new RecordingListener());
        memberB.Subscribe(topic);
        var first = await memberB.ConsumeOneAsync(HandOffBound, timeout.Token);

        await Assert.That(first).IsNotNull();
        await Assert.That(first!.Value.Offset).IsEqualTo(3);
        await Assert.That(first.Value.Value).IsEqualTo("after-unsubscribe");
    }

    private sealed class CommittingListener : IConsumerAwareRebalanceListener
    {
        public TaskCompletionSource RevokedCommitted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask OnPartitionsAssignedAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async ValueTask OnPartitionsRevokedAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            try
            {
                await consumer.CommitAsync(cancellationToken);
                RevokedCommitted.TrySetResult();
            }
            catch (Exception ex)
            {
                RevokedCommitted.TrySetException(ex);
            }
        }

        public ValueTask OnPartitionsLostAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    [Test]
    public async Task Unsubscribe_SoleMember_LeavesGroupEmpty()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"unsubscribe-empty-{Guid.NewGuid():N}";
        await ProduceToPartitionsAsync(topic, [0], "record");

        await using var consumer = await CreateGroupConsumerAsync(groupId, new RecordingListener());
        consumer.Subscribe(topic);
        var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30));
        await Assert.That(record).IsNotNull();

        await using var admin = new AdminClientBuilder()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
        await Assert.That(await CountMembersAsync(admin, groupId)).IsEqualTo(1);

        consumer.Unsubscribe();

        await Assert.That(async () => await CountMembersAsync(admin, groupId))
            .Eventually(count => count.IsEqualTo(0), HandOffBound);
    }

    internal static async Task<int> CountMembersAsync(IAdminClient admin, string groupId)
    {
        var description = (await admin.DescribeConsumerGroupsAsync([groupId]))[groupId];
        return description.Members.Count;
    }

    private async Task<IKafkaConsumer<string, string>> CreateGroupConsumerAsync(
        string groupId,
        IRebalanceListener listener,
        TimeSpan? autoCommitInterval = null)
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());
        if (autoCommitInterval is { } interval)
            builder = builder.WithAutoCommitInterval(interval);

        return await builder.BuildAsync();
    }

    private async Task ProduceToPartitionsAsync(string topic, IEnumerable<int> partitions, string value)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        foreach (var partition in partitions)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = partition,
                Key = $"key-{partition}",
                Value = value
            });
        }
    }

    /// <summary>Waits until both members own part of the topic and returns A's partitions.</summary>
    private static async Task<HashSet<int>> WaitForSplitAsync(
        IKafkaConsumer<string, string> memberA,
        IKafkaConsumer<string, string> memberB,
        string topic)
    {
        await Assert.That(() =>
                memberA.Assignment.Any(tp => tp.Topic == topic)
                && memberB.Assignment.Any(tp => tp.Topic == topic)
                && memberA.Assignment.Count(tp => tp.Topic == topic)
                    + memberB.Assignment.Count(tp => tp.Topic == topic) == 4)
            .Eventually(split => split.IsTrue(), TimeSpan.FromSeconds(60));

        return memberA.Assignment.Where(tp => tp.Topic == topic).Select(tp => tp.Partition).ToHashSet();
    }

    private sealed class RecordingListener : IRebalanceListener
    {
        private readonly ConcurrentQueue<TopicPartition> _revoked = new();
        private int _assignedCalls;

        public int AssignedCallCount => Volatile.Read(ref _assignedCalls);

        public HashSet<int> RevokedSnapshot() => _revoked.Select(tp => tp.Partition).ToHashSet();

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _assignedCalls);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            foreach (var partition in partitions)
                _revoked.Enqueue(partition);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    private sealed class ConsumeLoop : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentQueue<ConsumeResult<string, string>> _records = new();
        private Task _task = Task.CompletedTask;

        public static ConsumeLoop Start(IKafkaConsumer<string, string> consumer)
        {
            var loop = new ConsumeLoop();
            loop._task = Task.Run(async () =>
            {
                try
                {
                    await foreach (var record in consumer.ConsumeAsync(loop._cts.Token))
                        loop._records.Enqueue(record);
                }
                catch (OperationCanceledException) when (loop._cts.IsCancellationRequested)
                {
                }
            });
            return loop;
        }

        public List<ConsumeResult<string, string>> ValuesFrom(string topic, string value) =>
            _records.Where(record => record.Topic == topic && record.Value == value).ToList();

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            await _task.WaitAsync(TimeSpan.FromSeconds(30));
            _cts.Dispose();
        }
    }
}
