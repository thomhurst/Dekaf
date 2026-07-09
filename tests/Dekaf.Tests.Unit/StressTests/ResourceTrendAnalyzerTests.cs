#if NET10_0
using Dekaf.StressTests.Metrics;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class ResourceTrendAnalyzerTests
{
    private const long Mib = 1024 * 1024;

    [Test]
    public async Task Analyze_StablePostWarmupSamples_Passes()
    {
        var samples = CreateSamples(90, static _ => 0);

        var analysis = ResourceTrendAnalyzer.Analyze(samples, CreateThresholds());

        await Assert.That(analysis.Failures).IsEmpty();
        await Assert.That(analysis.SampleCount).IsEqualTo(60);
        await Assert.That(analysis.WorkingSetSlopeMibPerHour).IsBetween(-0.001, 0.001);
        await Assert.That(analysis.GcHeapSlopeMibPerHour).IsBetween(-0.001, 0.001);
        await Assert.That(analysis.LohSlopeMibPerHour).IsBetween(-0.001, 0.001);
    }

    [Test]
    public async Task Analyze_GrowthBeforeWarmupOnly_Passes()
    {
        var samples = CreateSamples(90, minute => minute < 30 ? minute * Mib : 30 * Mib);

        var analysis = ResourceTrendAnalyzer.Analyze(samples, CreateThresholds());

        await Assert.That(analysis.Failures).IsEmpty();
        await Assert.That(analysis.WorkingSetSlopeMibPerHour).IsBetween(-0.001, 0.001);
    }

    [Test]
    public async Task Analyze_SustainedWorkingSetGrowth_Fails()
    {
        var samples = CreateSamples(90, minute => minute * Mib / 4);

        var analysis = ResourceTrendAnalyzer.Analyze(samples, CreateThresholds());

        await Assert.That(analysis.WorkingSetSlopeMibPerHour).IsBetween(14.99, 15.01);
        await Assert.That(analysis.Failures).Contains(x => x.Contains("working set", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task Analyze_SustainedGcHeapAndLohGrowth_FailsBoth()
    {
        var samples = CreateSamples(
            90,
            workingSetGrowth: static _ => 0,
            gcHeapGrowth: minute => minute * Mib / 8,
            lohGrowth: minute => minute * Mib / 10);

        var analysis = ResourceTrendAnalyzer.Analyze(samples, CreateThresholds());

        await Assert.That(analysis.Failures).Contains(x => x.Contains("GC heap", StringComparison.OrdinalIgnoreCase));
        await Assert.That(analysis.Failures).Contains(x => x.Contains("LOH", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task Analyze_SustainedThroughputDecay_Fails()
    {
        var samples = CreateSamples(
            90,
            workingSetGrowth: static _ => 0,
            producedRate: minute => 1_000 - (minute * 2),
            consumedRate: minute => 1_000 - (minute * 2));

        var analysis = ResourceTrendAnalyzer.Analyze(samples, CreateThresholds());

        await Assert.That(analysis.ProducedThroughputSlopePercentPerHour).IsLessThan(-10);
        await Assert.That(analysis.ConsumedThroughputSlopePercentPerHour).IsLessThan(-10);
        await Assert.That(analysis.Failures).Contains(x => x.Contains("produced throughput", StringComparison.OrdinalIgnoreCase));
        await Assert.That(analysis.Failures).Contains(x => x.Contains("consumed throughput", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task Analyze_TooFewPostWarmupSamples_FailsClosed()
    {
        var thresholds = CreateThresholds() with { MinimumSampleCount = 60 };
        var samples = CreateSamples(50, static _ => 0);

        var analysis = ResourceTrendAnalyzer.Analyze(samples, thresholds);

        await Assert.That(analysis.Failures).Contains(x => x.Contains("at least 60", StringComparison.OrdinalIgnoreCase));
    }

    private static ResourceTrendThresholds CreateThresholds() => new()
    {
        WarmupMinutes = 30,
        MinimumSampleCount = 30,
        MaxWorkingSetSlopeMibPerHour = 8,
        MaxGcHeapSlopeMibPerHour = 4,
        MaxLohSlopeMibPerHour = 2,
        MaxThroughputDecayPercentPerHour = 5
    };

    private static List<ResourceSample> CreateSamples(
        int count,
        Func<int, long> workingSetGrowth,
        Func<int, long>? gcHeapGrowth = null,
        Func<int, long>? lohGrowth = null,
        Func<int, double>? producedRate = null,
        Func<int, double>? consumedRate = null)
    {
        gcHeapGrowth ??= static _ => 0;
        lohGrowth ??= static _ => 0;
        producedRate ??= static _ => 1_000;
        consumedRate ??= static _ => 1_000;

        return Enumerable.Range(0, count)
            .Select(minute => new ResourceSample
            {
                ElapsedMinutes = minute,
                WorkingSetBytes = (256 * Mib) + workingSetGrowth(minute),
                GcHeapBytes = (128 * Mib) + gcHeapGrowth(minute),
                LohSizeBytes = (32 * Mib) + lohGrowth(minute),
                Gen2Collections = minute / 10,
                ThreadCount = 16,
                ProducedMessagesPerSecond = producedRate(minute),
                ConsumedMessagesPerSecond = consumedRate(minute)
            })
            .ToList();
    }
}
#endif
