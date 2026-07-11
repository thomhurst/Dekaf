using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class StuckFetchPositionTrackerTests
{
    private static readonly TopicPartition Partition = new("topic", 0);

    [Test]
    public async Task ObserveEmptyParsedFetch_TripsAtThresholdForUnchangedPosition()
    {
        var tracker = new StuckFetchPositionTracker(threshold: 3);

        await Assert.That(tracker.ObserveEmptyParsedFetch(Partition, position: 42)).IsNull();
        await Assert.That(tracker.ObserveEmptyParsedFetch(Partition, position: 42)).IsNull();

        var error = tracker.ObserveEmptyParsedFetch(Partition, position: 42);

        await Assert.That(error).IsNotNull();
        await Assert.That(error!.ErrorCode).IsEqualTo(Dekaf.Protocol.ErrorCode.CorruptMessage);
        await Assert.That(error.IsRetriable).IsFalse();
        await Assert.That(error.Message).Contains("topic[0]");
        await Assert.That(error.Message).Contains("offset 42");
    }

    [Test]
    public async Task ObserveEmptyParsedFetch_PositionChangeRestartsCount()
    {
        var tracker = new StuckFetchPositionTracker(threshold: 3);

        tracker.ObserveEmptyParsedFetch(Partition, position: 42);
        tracker.ObserveEmptyParsedFetch(Partition, position: 42);

        await Assert.That(tracker.ObserveEmptyParsedFetch(Partition, position: 43)).IsNull();
        await Assert.That(tracker.ObserveEmptyParsedFetch(Partition, position: 43)).IsNull();
        await Assert.That(tracker.ObserveEmptyParsedFetch(Partition, position: 43)).IsNotNull();
    }

    [Test]
    public async Task Reset_RemovesPreviousObservations()
    {
        var tracker = new StuckFetchPositionTracker(threshold: 3);
        tracker.ObserveEmptyParsedFetch(Partition, position: 42);
        tracker.ObserveEmptyParsedFetch(Partition, position: 42);

        tracker.Reset(Partition);

        await Assert.That(tracker.ObserveEmptyParsedFetch(Partition, position: 42)).IsNull();
    }

    [Test]
    public async Task Clear_RemovesAllPartitionObservations()
    {
        var tracker = new StuckFetchPositionTracker(threshold: 2);
        var otherPartition = new TopicPartition("topic", 1);
        tracker.ObserveEmptyParsedFetch(Partition, position: 42);
        tracker.ObserveEmptyParsedFetch(otherPartition, position: 84);

        tracker.Clear();

        await Assert.That(tracker.ObserveEmptyParsedFetch(Partition, position: 42)).IsNull();
        await Assert.That(tracker.ObserveEmptyParsedFetch(otherPartition, position: 84)).IsNull();
    }

    [Test]
    public async Task Constructor_RejectsNonPositiveThreshold()
    {
        await Assert.That(() => new StuckFetchPositionTracker(threshold: 0))
            .Throws<ArgumentOutOfRangeException>();
    }
}
