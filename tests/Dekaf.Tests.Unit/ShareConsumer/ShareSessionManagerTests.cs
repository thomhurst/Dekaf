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
}
