using System.Diagnostics;
using Dekaf.StressTests.Metrics;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class LatencyTrackerTests
{
    [Test]
    public async Task RecordTicks_AboveOneSecond_CapturesOutlierContext()
    {
        var tracker = new LatencyTracker();

        tracker.RecordTicks(Stopwatch.Frequency * 2, messageIndex: 42);

        var snapshot = tracker.GetSnapshot();
        await Assert.That(snapshot.OutlierSamples).Count().IsEqualTo(1);
        var sample = snapshot.OutlierSamples[0];
        await Assert.That(sample.MessageIndex).IsEqualTo(42);
        await Assert.That(sample.LatencyUs).IsBetween(1_999_000, 2_001_000);
        await Assert.That(sample.CompletedAtUtc - sample.StartedAtUtc)
            .IsBetween(TimeSpan.FromMilliseconds(1_999), TimeSpan.FromMilliseconds(2_001));
        await Assert.That(snapshot.DroppedOutlierSamples).IsEqualTo(0);
    }

    [Test]
    public async Task RecordTicks_BelowOneSecond_DoesNotCaptureOutlier()
    {
        var tracker = new LatencyTracker();

        tracker.RecordTicks(Stopwatch.Frequency - 1, messageIndex: 42);

        var snapshot = tracker.GetSnapshot();
        await Assert.That(snapshot.OutlierSamples).IsEmpty();
        await Assert.That(snapshot.DroppedOutlierSamples).IsEqualTo(0);
    }

    [Test]
    public async Task RecordTicks_AboveDiagnosticCapacity_TracksDroppedCount()
    {
        var tracker = new LatencyTracker();
        for (var index = 0; index < 257; index++)
        {
            tracker.RecordTicks(Stopwatch.Frequency, messageIndex: index);
        }

        var snapshot = tracker.GetSnapshot();
        await Assert.That(snapshot.OutlierSamples).Count().IsEqualTo(256);
        await Assert.That(snapshot.DroppedOutlierSamples).IsEqualTo(1);
    }

    [Test]
    public async Task GetSnapshot_ConcurrentWithOutliers_NeverPublishesDefaultSample()
    {
        var tracker = new LatencyTracker();
        var recording = Task.Run(() => Parallel.For(0, 10_000, index =>
            tracker.RecordTicks(Stopwatch.Frequency, messageIndex: index)));

        while (!recording.IsCompleted)
        {
            var snapshot = tracker.GetSnapshot();
            await Assert.That(snapshot.OutlierSamples.All(
                sample => sample.StartedAtUtc != default && sample.LatencyUs > 0)).IsTrue();
        }

        await recording;
        var completed = tracker.GetSnapshot();
        await Assert.That(completed.OutlierSamples.All(
            sample => sample.StartedAtUtc != default && sample.LatencyUs > 0)).IsTrue();
    }
}
