using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

public sealed class CancelledCorrelationIdTrackerTests
{
    [Test]
    public async Task TryAdd_WhenCapacityExceeded_EvictsOldestCorrelationId()
    {
        var tracker = new CancelledCorrelationIdTracker(capacity: 2);

        tracker.TryAdd(1);
        tracker.TryAdd(2);
        tracker.TryAdd(3);

        await Assert.That(tracker.TryRemove(1)).IsFalse();
        await Assert.That(tracker.TryRemove(2)).IsTrue();
        await Assert.That(tracker.TryRemove(3)).IsTrue();
    }

    [Test]
    public async Task TryRemove_WhenCorrelationIdTracked_RemovesOnce()
    {
        var tracker = new CancelledCorrelationIdTracker(capacity: 2);

        tracker.TryAdd(42);

        await Assert.That(tracker.TryRemove(42)).IsTrue();
        await Assert.That(tracker.TryRemove(42)).IsFalse();
    }

    [Test]
    public async Task Clear_RemovesTrackedIdsAndQueuedEvictions()
    {
        var tracker = new CancelledCorrelationIdTracker(capacity: 2);
        tracker.TryAdd(1);
        tracker.TryAdd(2);

        tracker.Clear();
        tracker.TryAdd(3);

        await Assert.That(tracker.TryRemove(1)).IsFalse();
        await Assert.That(tracker.TryRemove(2)).IsFalse();
        await Assert.That(tracker.TryRemove(3)).IsTrue();
    }
}
