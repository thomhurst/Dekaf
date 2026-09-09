using System.Diagnostics;
using System.Text.Json;
using Dekaf.Benchmarks;
using TUnit.Assertions;
using TUnit.Core;

namespace Dekaf.Benchmarks;

// Exercise the actual probe loop without a product revision or network dependency.
public sealed class AdminFixture
{
    public object? Value { get; init; }
    public ValueTask<object?> Call() => new(Value);
}

public sealed class RecorderTests
{
    [Test]
    public async Task ExactTicksAndLongTailsSurviveIntervalReuse()
    {
        var histogram = new Probe.ExactHistogram(16);
        long[] input = [0, 17, 17, 51, Stopwatch.Frequency, long.MaxValue, long.MaxValue];
        foreach (var tick in input)
            histogram.Record(tick);
        var first = histogram.Drain();
        histogram.Record(17);
        histogram.Record(long.MaxValue);
        var second = histogram.Drain();
        foreach (var group in input.GroupBy(static tick => tick))
            await Assert.That(first.Single(row => row.Ticks == group.Key).Count).IsEqualTo(group.LongCount());
        await Assert.That(second.Sum(static row => row.Count)).IsEqualTo(2);
        await Assert.That(first.Sum(static row => row.Count)).IsEqualTo(input.LongLength);
        // Preserve the existing JSON array contract for replay validators.
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(first));
        await Assert.That(json.RootElement.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(json.RootElement.GetArrayLength()).IsEqualTo(5);
    }

    [Test]
    public async Task RandomizedIntervalsMatchIndependentReference()
    {
        var histogram = new Probe.ExactHistogram(100_000);
        var random = new Random(731);
        var retained = new List<(ArraySegment<Probe.TickCount> Actual, Dictionary<long, long> Expected)>();
        for (var interval = 0; interval < 30; interval++)
        {
            var expected = new Dictionary<long, long>();
            for (var index = 0; index < 2000; index++)
            {
                var tick = index % 20 == 0 ? long.MaxValue - random.Next(20) : random.Next(300);
                histogram.Record(tick);
                expected[tick] = expected.GetValueOrDefault(tick) + 1;
            }
            retained.Add((histogram.Drain(), expected));
        }
        foreach (var (actual, expected) in retained)
        {
            await Assert.That(actual.Count).IsEqualTo(expected.Count);
            foreach (var row in actual)
                await Assert.That(row.Count).IsEqualTo(expected[row.Ticks]);
        }
    }

    [Test]
    public async Task ExhaustionRejectsCaptureWithoutOverwritingPriorSamples()
    {
        var histogram = new Probe.ExactHistogram(1);
        histogram.Record(13);
        var retained = histogram.Drain();
        histogram.Record(29);
        await Assert.That(() => histogram.Drain()).Throws<InvalidOperationException>();
        await Assert.That(retained[0]).IsEqualTo(new Probe.TickCount(13, 1));
    }

    [Test]
    public async Task IntervalOverflowRejectsNewBucketsButAllowsRepeatedSamples()
    {
        var histogram = new Probe.ExactHistogram(65536);
        for (var tick = 0; tick < 65536; tick++)
            histogram.Record(tick);
        histogram.Record(0);
        await Assert.That(() => histogram.Record(long.MaxValue)).Throws<InvalidOperationException>();
        var retained = histogram.Drain();
        await Assert.That(retained.Count).IsEqualTo(65536);
        await Assert.That(retained.Sum(static row => row.Count)).IsEqualTo(65537);
    }

    [Test]
    public async Task EmptyIntervalAndNegativeLatencyAreHandledExplicitly()
    {
        var histogram = new Probe.ExactHistogram(1);
        await Assert.That(histogram.Drain().Count).IsEqualTo(0);
        await Assert.That(() => histogram.Record(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task RecordAndDrainAllocateNothingAfterConstruction()
    {
        var histogram = new Probe.ExactHistogram(10000);
        for (var index = 0; index < 100; index++)
        {
            histogram.Record(1);
            histogram.Record(long.MaxValue);
            _ = histogram.Drain();
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1000; index++)
        {
            histogram.Record(1);
            histogram.Record(long.MaxValue);
            _ = histogram.Drain();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task CompleteProbePreservesCountsPercentilesAndIntervalBoundaries()
    {
        var captures = await Probe.CapturePhasesAsync(new AdminFixture(), [0.1, 0.1]);
        foreach (var capture in captures)
        {
            var result = Probe.Complete(capture);
            await Assert.That(result.Completed).IsGreaterThan(0);
            await Assert.That(result.Latencies.Sum(static row => row.Count)).IsEqualTo(result.Completed);
            await Assert.That(result.Intervals.Sum(static row => row.Latencies.Sum(static bucket => bucket.Count)))
                .IsEqualTo(result.Completed);
            await Assert.That(result.MaxNs).IsEqualTo(result.Latencies[^1].Ticks * 1e9 / Stopwatch.Frequency);
            await Assert.That(result.P50Ns).IsLessThanOrEqualTo(result.P99Ns);
            await Assert.That(result.P99Ns).IsLessThanOrEqualTo(result.MaxNs);
        }
    }
}
