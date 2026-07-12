using Dekaf.StressTests.Metrics;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class GcStatsTests
{
    [Test]
    public async Task Capture_ReportsAllocationDeltaForPass()
    {
        var readings = new Queue<long>([100, 160, 225]);
        using var stats = new GcStats(readings.Dequeue, sampleInterval: null);

        stats.SampleAllocation();
        stats.Capture();

        await Assert.That(stats.AllocatedBytes).IsEqualTo(125);
    }

    [Test]
    public async Task Capture_CounterRebase_ContinuesFromNewBaseline()
    {
        var readings = new Queue<long>([100, 160, 20, 55]);
        using var stats = new GcStats(readings.Dequeue, sampleInterval: null);

        stats.SampleAllocation();
        stats.SampleAllocation();
        stats.Capture();

        await Assert.That(stats.AllocatedBytes).IsEqualTo(95);
    }

    [Test]
    public async Task Capture_CalledTwice_DoesNotReadOrAccumulateAgain()
    {
        var readings = new Queue<long>([100, 175]);
        using var stats = new GcStats(readings.Dequeue, sampleInterval: null);

        stats.Capture();
        stats.Capture();

        await Assert.That(stats.AllocatedBytes).IsEqualTo(75);
        await Assert.That(readings).IsEmpty();
    }
}
