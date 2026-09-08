using System.Diagnostics;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class LatencyTrackerTests
{
    [Test]
    public async Task Reset_ClearsWarmupHistogramAndOutliersWithoutAllocatingAnotherHistogram()
    {
        var tracker = new LatencyTracker();
        for (var index = 0; index < 257; index++) tracker.Record(6000);
        tracker.Record(10);
        await tracker.WaitForDeliverySamplesAsync();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        tracker.Reset();
        var resetAllocation = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        await Assert.That(resetAllocation).IsLessThan(4096);

        var empty = tracker.GetSnapshot();
        await Assert.That(empty.Count).IsEqualTo(0);
        await Assert.That(empty.OverflowCount).IsEqualTo(0);
        await Assert.That(empty.MinUs).IsEqualTo(0);
        await Assert.That(empty.MaxUs).IsEqualTo(0);
        await Assert.That(empty.P50Us).IsEqualTo(0);
        await Assert.That(empty.P95Us).IsEqualTo(0);
        await Assert.That(empty.P99Us).IsEqualTo(0);
        await Assert.That(empty.OutlierSamples).IsEmpty();
        await Assert.That(empty.DroppedOutlierSamples).IsEqualTo(0);

        tracker.Record(20);
        var measured = tracker.GetSnapshot();
        await Assert.That(measured.Count).IsEqualTo(1);
        await Assert.That(measured.P50Us).IsBetween(20_000, 20_010);
        await Assert.That(measured.MinUs).IsEqualTo(20_000);
        await Assert.That(measured.MaxUs).IsEqualTo(20_000);
    }

    [Test]
    public async Task Reset_RearmsDeliveryDrainAndRejectsOutstandingCallbacks()
    {
        var tracker = new LatencyTracker();
        for (var phase = 0; phase < 7; phase++)
        {
            tracker.Reset();
            tracker.BeginDeliverySample();
            var drained = tracker.WaitForDeliverySamplesAsync();
            await Assert.That(drained.IsCompleted).IsFalse();
            await Assert.That(() => tracker.Reset()).Throws<InvalidOperationException>();
            tracker.CompleteDeliverySample();
            await drained;
        }
    }

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
    public async Task DeliveryLatencyTracker_CapturesSubSecondOutlier()
    {
        var tracker = StressTestHelpers.CreateDeliveryLatencyTracker();

        tracker.RecordTicks(Stopwatch.Frequency * 99 / 1_000, messageIndex: 41);
        tracker.RecordTicks(Stopwatch.Frequency * 333 / 1_000, messageIndex: 42);

        var snapshot = tracker.GetSnapshot();
        await Assert.That(snapshot.OutlierSamples).Count().IsEqualTo(1);
        await Assert.That(snapshot.OutlierSamples[0].MessageIndex).IsEqualTo(42);
        await Assert.That(snapshot.OutlierSamples[0].LatencyUs).IsBetween(332_000, 334_000);
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
