using System.Diagnostics;
using TUnit.Assertions;
using TUnit.Core;

namespace PoolLoadedValidation;

public sealed class BlockHistogramTests
{
    [Test]
    public async Task CompletionAndLatencyBoundariesPreserveExactBuckets()
    {
        var histogram = new BlockHistogram(21);
        histogram.RecordTicks(0, 0);
        histogram.RecordTicks(Ticks(9), 10 * Stopwatch.Frequency - 1);
        histogram.RecordTicks(Ticks(10), 10 * Stopwatch.Frequency);
        histogram.RecordTicks(Ticks(4_999_999), 20 * Stopwatch.Frequency);
        histogram.RecordTicks(Ticks(5_000_000), 20 * Stopwatch.Frequency);
        var result = histogram.GetSnapshot();
        await Assert.That(result.BlockSeconds).IsEqualTo(10);
        await Assert.That(result.BucketWidthUs).IsEqualTo(10);
        await Assert.That(result.BucketCount).IsEqualTo(500_000);
        await Assert.That(result.Blocks.Length).IsEqualTo(3);
        await Assert.That(result.Blocks[0].Buckets.Single()).IsEqualTo(new LatencyBucket(0, 2));
        await Assert.That(result.Blocks[1].Buckets.Single()).IsEqualTo(new LatencyBucket(1, 1));
        await Assert.That(result.Blocks[2].Buckets.Single()).IsEqualTo(new LatencyBucket(499_999, 1));
        await Assert.That(result.Blocks[2].Count).IsEqualTo(2);
        await Assert.That(result.Blocks[2].LatencyOverflowCount).IsEqualTo(1);
    }

    [Test]
    public async Task PartialFinalBlockHonorsExactCapacity()
    {
        var histogram = new BlockHistogram(11);
        histogram.RecordTicks(100, 11 * Stopwatch.Frequency - 1);
        histogram.RecordTicks(200, 11 * Stopwatch.Frequency);
        histogram.RecordTicks(long.MaxValue, long.MaxValue);
        var result = histogram.GetSnapshot();
        await Assert.That(result.CapacitySeconds).IsEqualTo(11);
        await Assert.That(result.Blocks.Length).IsEqualTo(2);
        await Assert.That(result.Blocks[0].Count).IsEqualTo(0);
        await Assert.That(result.Blocks[0].Buckets.Length).IsEqualTo(0);
        await Assert.That(result.Blocks[1].Count).IsEqualTo(1);
        await Assert.That(result.OutsideCapacityCount).IsEqualTo(2);
    }

    [Test]
    public async Task ConcurrentWritersPreserveEveryBucketAndOverflow()
    {
        var histogram = new BlockHistogram(21);
        Parallel.For(0, 8, writer =>
        {
            for (var index = 0; index < 10_000; index++)
                histogram.RecordTicks(Ticks(index % 2 == 0 ? writer * 10 : 5_000_000),
                    writer % 3 * 10 * Stopwatch.Frequency);
        });
        var result = histogram.GetSnapshot();
        for (var block = 0; block < 3; block++)
        {
            var writers = Enumerable.Range(0, 8).Where(writer => writer % 3 == block).ToArray();
            await Assert.That(result.Blocks[block].Count).IsEqualTo(writers.Length * 10_000L);
            await Assert.That(result.Blocks[block].LatencyOverflowCount).IsEqualTo(writers.Length * 5_000L);
            await Assert.That(result.Blocks[block].Buckets.Length).IsEqualTo(writers.Length);
            foreach (var writer in writers)
                await Assert.That(result.Blocks[block].Buckets.Single(bucket => bucket.Index == writer).Count).IsEqualTo(5_000);
        }
    }

    [Test]
    public async Task CountValidationRejectsMovedOrOutsideCapacityObservations()
    {
        var histogram = new BlockHistogram(11);
        histogram.RecordTicks(1, 0);
        var intervals = new IntervalLatency(11);
        intervals.RecordTicks(1, 10 * Stopwatch.Frequency);
        await Assert.That(histogram.GetSnapshot().MatchesIntervalCounts(intervals.GetSnapshot())).IsFalse();
        histogram.RecordTicks(1, 10 * Stopwatch.Frequency);
        intervals.RecordTicks(1, 0);
        await Assert.That(histogram.GetSnapshot().MatchesIntervalCounts(intervals.GetSnapshot())).IsTrue();
        histogram.RecordTicks(1, 11 * Stopwatch.Frequency);
        intervals.RecordTicks(1, 11 * Stopwatch.Frequency);
        await Assert.That(histogram.GetSnapshot().MatchesIntervalCounts(intervals.GetSnapshot())).IsFalse();
    }

    [Test]
    public async Task InvalidInputsLeaveEmptySnapshots()
    {
        var histogram = new BlockHistogram(1);
        await Assert.That(() => new BlockHistogram(0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => histogram.RecordTicks(-1, 0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => histogram.RecordTicks(0, -1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(histogram.GetSnapshot().Blocks[0].Count).IsEqualTo(0);
    }

    [Test]
    public async Task SnapshotsPreservePreviouslyReturnedBuckets()
    {
        var histogram = new BlockHistogram(1);
        histogram.RecordTicks(Ticks(20), 0);
        var before = histogram.GetSnapshot();
        histogram.RecordTicks(Ticks(30), 0);
        await Assert.That(before.Blocks[0].Buckets.Single()).IsEqualTo(new LatencyBucket(2, 1));
        await Assert.That(histogram.GetSnapshot().Blocks[0].Count).IsEqualTo(2);
    }

    [Test]
    public async Task RecordingUsesPreallocatedStorageIncludingOverflowPaths()
    {
        var histogram = new BlockHistogram(1);
        for (var index = 0; index < 10_000; index++)
            histogram.RecordTicks(index % 2 == 0 ? index : long.MaxValue, index % 3 * Stopwatch.Frequency);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100_000; index++)
            histogram.RecordTicks(index % 2 == 0 ? index : long.MaxValue, index % 3 * Stopwatch.Frequency);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(allocated).IsEqualTo(0);
    }

    private static long Ticks(long microseconds) => microseconds * Stopwatch.Frequency / 1_000_000;
}
