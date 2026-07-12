using Dekaf.StressTests.Metrics;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class ThroughputTrackerTests
{
    [Test]
    public async Task TakeSample_CapturesTimestampedIntervalAlongsideLegacyRate()
    {
        var tracker = new ThroughputTracker();
        tracker.Start();
        tracker.RecordMessages(100, 100_000);

        tracker.TakeSample();
        tracker.Stop();
        var snapshot = tracker.GetSnapshot();

        await Assert.That(snapshot.MessagesPerSecondSamples.Count).IsEqualTo(1);
        await Assert.That(snapshot.IntervalSamples.Count).IsEqualTo(1);
        await Assert.That(snapshot.IntervalSamples[0].MessagesPerSecond)
            .IsEqualTo(snapshot.MessagesPerSecondSamples[0]);
        await Assert.That(snapshot.IntervalSamples[0].ElapsedSeconds).IsGreaterThan(0);
        await Assert.That(snapshot.IntervalSamples[0].CapturedAtUtc).IsGreaterThan(DateTimeOffset.MinValue);
    }
}
