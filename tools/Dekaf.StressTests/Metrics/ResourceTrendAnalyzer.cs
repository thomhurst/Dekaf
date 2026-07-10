namespace Dekaf.StressTests.Metrics;

internal sealed record ResourceSample
{
    public required double ElapsedMinutes { get; init; }
    public required long WorkingSetBytes { get; init; }
    public required long GcHeapBytes { get; init; }
    public required long LohSizeBytes { get; init; }
    public required int Gen2Collections { get; init; }
    public required int ThreadCount { get; init; }
    public required double ProducedMessagesPerSecond { get; init; }
    public required double ConsumedMessagesPerSecond { get; init; }
}

internal sealed record ResourceTrendThresholds
{
    public required double WarmupMinutes { get; init; }
    public required int MinimumSampleCount { get; init; }
    public required double MaxWorkingSetSlopeMibPerHour { get; init; }
    public required double MaxGcHeapSlopeMibPerHour { get; init; }
    public required double MaxLohSlopeMibPerHour { get; init; }
    public required double MaxThroughputDecayPercentPerHour { get; init; }
}

internal sealed class ResourceTrendAnalysis
{
    public required int SampleCount { get; init; }
    public required double WorkingSetSlopeMibPerHour { get; init; }
    public required double GcHeapSlopeMibPerHour { get; init; }
    public required double LohSlopeMibPerHour { get; init; }
    public required double ProducedThroughputSlopePercentPerHour { get; init; }
    public required double ConsumedThroughputSlopePercentPerHour { get; init; }
    public required List<string> Failures { get; init; }

    public bool Passed => Failures.Count == 0;
}

internal static class ResourceTrendAnalyzer
{
    private const double BytesPerMib = 1024 * 1024;

    internal static ResourceTrendAnalysis Analyze(
        IReadOnlyCollection<ResourceSample> samples,
        ResourceTrendThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(thresholds);
        ValidateThresholds(thresholds);

        var postWarmup = samples
            .Where(sample => sample.ElapsedMinutes >= thresholds.WarmupMinutes)
            .OrderBy(sample => sample.ElapsedMinutes)
            .ToArray();

        var failures = new List<string>();
        if (postWarmup.Length < thresholds.MinimumSampleCount)
        {
            failures.Add(
                $"Resource trend requires at least {thresholds.MinimumSampleCount} post-warmup samples; " +
                $"captured {postWarmup.Length}.");
        }

        var workingSetSlope = CalculateSlopePerHour(postWarmup, static sample => sample.WorkingSetBytes) / BytesPerMib;
        var gcHeapSlope = CalculateSlopePerHour(postWarmup, static sample => sample.GcHeapBytes) / BytesPerMib;
        var lohSlope = CalculateSlopePerHour(postWarmup, static sample => sample.LohSizeBytes) / BytesPerMib;
        var producedThroughputSlope = CalculatePercentSlopePerHour(
            postWarmup,
            static sample => sample.ProducedMessagesPerSecond,
            "produced throughput",
            failures);
        var consumedThroughputSlope = CalculatePercentSlopePerHour(
            postWarmup,
            static sample => sample.ConsumedMessagesPerSecond,
            "consumed throughput",
            failures);

        AddGrowthFailure(
            failures,
            "working set",
            workingSetSlope,
            thresholds.MaxWorkingSetSlopeMibPerHour);
        AddGrowthFailure(
            failures,
            "GC heap",
            gcHeapSlope,
            thresholds.MaxGcHeapSlopeMibPerHour);
        AddGrowthFailure(
            failures,
            "LOH",
            lohSlope,
            thresholds.MaxLohSlopeMibPerHour);
        AddDecayFailure(
            failures,
            "produced throughput",
            producedThroughputSlope,
            thresholds.MaxThroughputDecayPercentPerHour);
        AddDecayFailure(
            failures,
            "consumed throughput",
            consumedThroughputSlope,
            thresholds.MaxThroughputDecayPercentPerHour);

        return new ResourceTrendAnalysis
        {
            SampleCount = postWarmup.Length,
            WorkingSetSlopeMibPerHour = workingSetSlope,
            GcHeapSlopeMibPerHour = gcHeapSlope,
            LohSlopeMibPerHour = lohSlope,
            ProducedThroughputSlopePercentPerHour = producedThroughputSlope,
            ConsumedThroughputSlopePercentPerHour = consumedThroughputSlope,
            Failures = failures
        };
    }

    private static void ValidateThresholds(ResourceTrendThresholds thresholds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(thresholds.WarmupMinutes);
        ArgumentOutOfRangeException.ThrowIfLessThan(thresholds.MinimumSampleCount, 2);
        ArgumentOutOfRangeException.ThrowIfNegative(thresholds.MaxWorkingSetSlopeMibPerHour);
        ArgumentOutOfRangeException.ThrowIfNegative(thresholds.MaxGcHeapSlopeMibPerHour);
        ArgumentOutOfRangeException.ThrowIfNegative(thresholds.MaxLohSlopeMibPerHour);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thresholds.MaxThroughputDecayPercentPerHour);
    }

    private static double CalculateSlopePerHour(
        IReadOnlyList<ResourceSample> samples,
        Func<ResourceSample, double> selector)
    {
        if (samples.Count < 2)
        {
            return 0;
        }

        var meanHours = samples.Average(static sample => sample.ElapsedMinutes / 60);
        var meanValue = samples.Average(selector);
        var numerator = 0.0;
        var denominator = 0.0;

        foreach (var sample in samples)
        {
            var hoursDelta = (sample.ElapsedMinutes / 60) - meanHours;
            numerator += hoursDelta * (selector(sample) - meanValue);
            denominator += hoursDelta * hoursDelta;
        }

        return denominator == 0 ? 0 : numerator / denominator;
    }

    private static double CalculatePercentSlopePerHour(
        IReadOnlyList<ResourceSample> samples,
        Func<ResourceSample, double> selector,
        string metric,
        List<string> failures)
    {
        if (samples.Count < 2)
        {
            return 0;
        }

        var mean = samples.Average(selector);
        if (mean <= 0 || !double.IsFinite(mean))
        {
            failures.Add($"Post-warmup {metric} mean is not positive and finite ({mean:N2} msg/s).");
            return 0;
        }

        return CalculateSlopePerHour(samples, selector) / mean * 100;
    }

    private static void AddGrowthFailure(
        List<string> failures,
        string metric,
        double slopeMibPerHour,
        double maximumMibPerHour)
    {
        if (slopeMibPerHour > maximumMibPerHour)
        {
            failures.Add(
                $"Post-warmup {metric} slope {slopeMibPerHour:N2} MiB/hour exceeds " +
                $"{maximumMibPerHour:N2} MiB/hour.");
        }
    }

    private static void AddDecayFailure(
        List<string> failures,
        string metric,
        double slopePercentPerHour,
        double maximumDecayPercentPerHour)
    {
        if (slopePercentPerHour < -maximumDecayPercentPerHour)
        {
            failures.Add(
                $"Post-warmup {metric} slope {slopePercentPerHour:N2}%/hour exceeds " +
                $"the allowed {maximumDecayPercentPerHour:N2}%/hour decay.");
        }
    }
}
