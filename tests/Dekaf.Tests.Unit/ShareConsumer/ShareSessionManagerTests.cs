using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public class ShareSessionManagerTests
{
    [Test]
    public async Task NewSession_ReturnsEpochZero()
    {
        var manager = new ShareSessionManager();

        var epoch = manager.GetSessionEpoch(brokerId: 1);

        await Assert.That(epoch).IsEqualTo(0);
    }

    [Test]
    public async Task AfterIncrement_EpochIsOne()
    {
        var manager = new ShareSessionManager();

        manager.IncrementEpoch(brokerId: 1);

        var epoch = manager.GetSessionEpoch(brokerId: 1);

        await Assert.That(epoch).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleIncrements_EpochIncrementsEachTime()
    {
        var manager = new ShareSessionManager();

        manager.IncrementEpoch(brokerId: 1);
        manager.IncrementEpoch(brokerId: 1);
        manager.IncrementEpoch(brokerId: 1);

        var epoch = manager.GetSessionEpoch(brokerId: 1);

        await Assert.That(epoch).IsEqualTo(3);
    }

    [Test]
    public async Task ResetSession_ReturnsToEpochZero()
    {
        var manager = new ShareSessionManager();

        manager.IncrementEpoch(brokerId: 1);
        manager.IncrementEpoch(brokerId: 1);

        manager.ResetSession(brokerId: 1);

        var epoch = manager.GetSessionEpoch(brokerId: 1);

        await Assert.That(epoch).IsEqualTo(0);
    }

    [Test]
    public async Task CloseEpoch_ReturnsMinusOne()
    {
        var closeEpoch = ShareSessionManager.CloseEpoch;
        await Assert.That(closeEpoch).IsEqualTo(-1);
    }

    [Test]
    public async Task MultipleBrokers_TrackedIndependently()
    {
        var manager = new ShareSessionManager();

        manager.IncrementEpoch(brokerId: 1);
        manager.IncrementEpoch(brokerId: 1);
        manager.IncrementEpoch(brokerId: 2);

        await Assert.That(manager.GetSessionEpoch(brokerId: 1)).IsEqualTo(2);
        await Assert.That(manager.GetSessionEpoch(brokerId: 2)).IsEqualTo(1);
        await Assert.That(manager.GetSessionEpoch(brokerId: 3)).IsEqualTo(0);
    }

    [Test]
    public async Task ResetAll_ClearsAllSessions()
    {
        var manager = new ShareSessionManager();

        manager.IncrementEpoch(brokerId: 1);
        manager.IncrementEpoch(brokerId: 2);
        manager.IncrementEpoch(brokerId: 3);

        manager.ResetAll();

        await Assert.That(manager.GetSessionEpoch(brokerId: 1)).IsEqualTo(0);
        await Assert.That(manager.GetSessionEpoch(brokerId: 2)).IsEqualTo(0);
        await Assert.That(manager.GetSessionEpoch(brokerId: 3)).IsEqualTo(0);
    }

    [Test]
    public async Task ResetSession_OnlyAffectsTargetBroker()
    {
        var manager = new ShareSessionManager();

        manager.IncrementEpoch(brokerId: 1);
        manager.IncrementEpoch(brokerId: 2);

        manager.ResetSession(brokerId: 1);

        await Assert.That(manager.GetSessionEpoch(brokerId: 1)).IsEqualTo(0);
        await Assert.That(manager.GetSessionEpoch(brokerId: 2)).IsEqualTo(1);
    }

    [Test]
    public async Task ResetSession_NonExistentBroker_DoesNotThrow()
    {
        var manager = new ShareSessionManager();

        // Should not throw
        manager.ResetSession(brokerId: 999);

        await Assert.That(manager.GetSessionEpoch(brokerId: 999)).IsEqualTo(0);
    }

    [Test]
    public async Task IncrementAfterReset_StartsFromOne()
    {
        var manager = new ShareSessionManager();

        manager.IncrementEpoch(brokerId: 1);
        manager.IncrementEpoch(brokerId: 1);
        manager.ResetSession(brokerId: 1);
        manager.IncrementEpoch(brokerId: 1);

        await Assert.That(manager.GetSessionEpoch(brokerId: 1)).IsEqualTo(1);
    }

    [Test]
    public async Task IncrementAtMaxValue_WrapsToOne()
    {
        var manager = new ShareSessionManager();
        manager.IncrementEpoch(brokerId: 1);
        var epochs = (System.Collections.IDictionary)typeof(ShareSessionManager)
            .GetField("_sessionEpochs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(manager)!;
        var slot = epochs[1]!;
        slot.GetType().GetField("Epoch")!.SetValue(slot, int.MaxValue);
        await Assert.That(manager.GetSessionEpoch(brokerId: 1)).IsEqualTo(int.MaxValue);

        manager.IncrementEpoch(brokerId: 1);

        await Assert.That(manager.GetSessionEpoch(brokerId: 1)).IsEqualTo(1);
    }

    [Test]
    public async Task ConcurrentBrokers_KeepEveryBrokersEpoch()
    {
        // A commit and a poll send to every broker at once, and each per-broker task
        // advances or resets its own broker's epoch when its response arrives.
        const int brokers = 8;
        const int updates = 20_000;
        const int lastReset = updates - 1 - ((updates - 1) % 97);
        var manager = new ShareSessionManager();
        using var start = new Barrier(brokers);

        var tasks = new Task[brokers];
        for (var broker = 0; broker < brokers; broker++)
        {
            var brokerId = broker;
            tasks[broker] = Task.Factory.StartNew(() =>
            {
                start.SignalAndWait();
                for (var i = 0; i < updates; i++)
                {
                    manager.IncrementEpoch(brokerId);
                    if (i % 97 == 0)
                    {
                        manager.ResetSession(brokerId);
                        manager.IncrementEpoch(brokerId);
                    }
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        await Task.WhenAll(tasks);

        // The last reset is followed by one increment, then one per remaining iteration.
        const int expected = 1 + (updates - 1 - lastReset);
        for (var broker = 0; broker < brokers; broker++)
            await Assert.That(manager.GetSessionEpoch(broker)).IsEqualTo(expected);
    }

    [Test]
    public async Task UpdateSessionPartitions_NewSessionHoldsOnlySentPartitions()
    {
        var manager = new ShareSessionManager();
        var oldTopicId = Guid.NewGuid();
        var newTopicId = Guid.NewGuid();
        var session = manager.GetSessionPartitions(1);
        ShareSessionManager.UpdateSessionPartitions(session, newSession: true, SentTopics(("old", oldTopicId, [0, 1])), null);

        ShareSessionManager.UpdateSessionPartitions(session, newSession: true, SentTopics(("new", newTopicId, [2])), null);

        await Assert.That(manager.GetSessionPartitions(1))
            .IsEquivalentTo(new Dictionary<TopicPartition, Guid> { [new("new", 2)] = newTopicId });
    }

    [Test]
    public async Task UpdateSessionPartitions_IncrementalRemovesForgottenThenAddsSent()
    {
        var manager = new ShareSessionManager();
        var oldTopicId = Guid.NewGuid();
        var recreatedTopicId = Guid.NewGuid();
        var session = manager.GetSessionPartitions(1);
        ShareSessionManager.UpdateSessionPartitions(session, newSession: true, SentTopics(("topic", oldTopicId, [0, 1])), null);

        // The topic was recreated: partition 0 is forgotten under its old ID and sent under the new one.
        ShareSessionManager.UpdateSessionPartitions(
            session,
            newSession: false,
            SentTopics(("topic", recreatedTopicId, [0])),
            [new TopicPartition("topic", 0), new TopicPartition("topic", 1)]);

        await Assert.That(manager.GetSessionPartitions(1))
            .IsEquivalentTo(new Dictionary<TopicPartition, Guid> { [new("topic", 0)] = recreatedTopicId });
    }

    [Test]
    public async Task UpdateSessionPartitions_ReplyFromBeforeResetAll_LeavesNewSessionUnchanged()
    {
        var manager = new ShareSessionManager();
        var topicId = Guid.NewGuid();
        // A request captures the session, then Unsubscribe resets every session before its reply.
        var abandoned = manager.GetSessionPartitions(1);
        manager.ResetAll();
        var current = manager.GetSessionPartitions(1);
        ShareSessionManager.UpdateSessionPartitions(current, newSession: true, SentTopics(("new", topicId, [0])), null);

        ShareSessionManager.UpdateSessionPartitions(abandoned, newSession: true, SentTopics(("old", topicId, [1])), null);

        await Assert.That(manager.GetSessionPartitions(1))
            .IsEquivalentTo(new Dictionary<TopicPartition, Guid> { [new("new", 0)] = topicId });
    }

    [Test]
    public async Task GetSessionPartitions_DoesNotMarkSessionOpened()
    {
        var manager = new ShareSessionManager();

        ShareSessionManager.UpdateSessionPartitions(
            manager.GetSessionPartitions(1), newSession: true, SentTopics(("topic", Guid.NewGuid(), [0])), null);

        await Assert.That(manager.HadSession(1)).IsFalse();
        manager.IncrementEpoch(1);
        manager.ResetSession(1);
        await Assert.That(manager.HadSession(1)).IsTrue();
    }

    [Test]
    public async Task GetSessionBrokers_IncludesResetSessionsButNotUnopenedSlots()
    {
        var manager = new ShareSessionManager();
        manager.IncrementEpoch(1);
        manager.IncrementEpoch(2);
        manager.ResetSession(1);
        manager.GetSessionPartitions(3);

        var brokers = manager.GetSessionBrokers();

        await Assert.That(brokers).IsEquivalentTo([1, 2]);
        manager.ResetAll();
        await Assert.That(manager.GetSessionBrokers()).IsEmpty();
        await Assert.That(brokers).IsEquivalentTo([1, 2]);
    }

    private static Dictionary<string, (Guid TopicId, List<ShareFetchRequestPartition> Partitions)> SentTopics(
        params (string Topic, Guid TopicId, int[] Partitions)[] topics)
        => topics.ToDictionary(
            static topic => topic.Topic,
            static topic => (topic.TopicId, topic.Partitions
                .Select(static partition => new ShareFetchRequestPartition { PartitionIndex = partition })
                .ToList()));
}
