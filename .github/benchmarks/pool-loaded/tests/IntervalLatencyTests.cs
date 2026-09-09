using System.Diagnostics;
using TUnit.Assertions;
using TUnit.Core;

namespace PoolLoadedValidation;

public sealed class IntervalLatencyTests
{
    [Test]
    public async Task BoundariesUseCompletionTimeAndPreserveEmptyIntervals()
    {
        var tracker = new IntervalLatency(4);
        tracker.RecordTicks(50, 0);
        tracker.RecordTicks(90, Stopwatch.Frequency - 1);
        tracker.RecordTicks(10, Stopwatch.Frequency);
        tracker.RecordTicks(70, 3 * Stopwatch.Frequency);
        var result = tracker.GetSnapshot();
        await Assert.That(result.IntervalSeconds).IsEqualTo(1);
        await Assert.That(result.TicksPerSecond).IsEqualTo(Stopwatch.Frequency);
        var expected = new IntervalExtrema[]
        {
            new(2, 50, 90), new(1, 10, 10), new(0, 0, 0), new(1, 70, 70)
        };
        await Assert.That(result.Intervals.Length).IsEqualTo(expected.Length);
        for (var index = 0; index < expected.Length; index++)
            await Assert.That(result.Intervals[index]).IsEqualTo(expected[index]);
        await Assert.That(result.OutsideCapacity.Count).IsEqualTo(0);
    }

    [Test]
    public async Task OutsideCapacityRetainsEveryCountAndMaximum()
    {
        var tracker = new IntervalLatency(2);
        tracker.RecordTicks(0, 0);
        tracker.RecordTicks(41, 2 * Stopwatch.Frequency);
        tracker.RecordTicks(99, long.MaxValue);
        var result = tracker.GetSnapshot();
        await Assert.That(result.Intervals[0]).IsEqualTo(new IntervalExtrema(1, 0, 0));
        await Assert.That(result.OutsideCapacity).IsEqualTo(new IntervalExtrema(2, 41, 99));
    }

    [Test]
    public async Task ConcurrentWritersRetainAllExtremaAndCounts()
    {
        var tracker = new IntervalLatency(3);
        Parallel.For(0, 8, writer =>
        {
            for (var index = 0; index < 10_000; index++)
                tracker.RecordTicks(writer * 10_000 + index, writer % 4 * Stopwatch.Frequency);
        });
        var result = tracker.GetSnapshot();
        for (var index = 0; index < 3; index++)
            await Assert.That(result.Intervals[index]).IsEqualTo(
                new IntervalExtrema(20_000, index * 10_000, (index + 5) * 10_000 - 1));
        await Assert.That(result.OutsideCapacity).IsEqualTo(new IntervalExtrema(20_000, 30_000, 79_999));
    }

    [Test]
    public async Task InvalidObservationsFailWithoutChangingCounts()
    {
        var tracker = new IntervalLatency(1);
        await Assert.That(() => tracker.RecordTicks(-1, 0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => tracker.RecordTicks(0, -1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new IntervalLatency(0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(tracker.GetSnapshot().Intervals[0].Count).IsEqualTo(0);
    }

    [Test]
    public async Task SnapshotsDoNotResetOrChangePreviouslyReturnedValues()
    {
        var tracker = new IntervalLatency(1);
        tracker.RecordTicks(40, 0);
        var before = tracker.GetSnapshot();
        tracker.RecordTicks(100, 0);
        await Assert.That(before.Intervals[0]).IsEqualTo(new IntervalExtrema(1, 40, 40));
        await Assert.That(tracker.GetSnapshot().Intervals[0]).IsEqualTo(new IntervalExtrema(2, 40, 100));
    }

    [Test]
    public async Task LargestTickValueRemainsAnExactMinimum()
    {
        var tracker = new IntervalLatency(1);
        tracker.RecordTicks(long.MaxValue, 0);
        await Assert.That(tracker.GetSnapshot().Intervals[0]).IsEqualTo(
            new IntervalExtrema(1, long.MaxValue, long.MaxValue));
    }

    [Test]
    public async Task RecordingUsesOnlyPreallocatedStorage()
    {
        var tracker = new IntervalLatency(1);
        for (var index = 0; index < 10_000; index++)
            tracker.RecordTicks(index, index % 2 * Stopwatch.Frequency);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100_000; index++)
            tracker.RecordTicks(index, index % 2 * Stopwatch.Frequency);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(allocated).IsEqualTo(0);
    }
}
