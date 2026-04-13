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
        await Assert.That(ShareSessionManager.CloseEpoch).IsEqualTo(-1);
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
}
