using System.Diagnostics;

namespace Dekaf.StressTests.Metrics;

internal sealed class ResourceTrendMonitor(
    Func<long> producedCount,
    Func<long> consumedCount,
    TimeSpan sampleInterval)
{
    private readonly Stopwatch _elapsed = new();
    private readonly List<ResourceSample> _samples = [];
    private long _lastProducedCount;
    private long _lastConsumedCount;
    private double _lastSampleElapsedSeconds;

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleInterval, TimeSpan.Zero);

        _elapsed.Start();
        CaptureSample();

        using var timer = new PeriodicTimer(sampleInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                CaptureSample();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the soak measurement window ends.
        }
        finally
        {
            var elapsedSinceLastSample = TimeSpan.FromSeconds(
                _elapsed.Elapsed.TotalSeconds - _lastSampleElapsedSeconds);
            if (ShouldCaptureFinalSample(elapsedSinceLastSample, sampleInterval))
            {
                CaptureSample();
            }
            _elapsed.Stop();
        }
    }

    // Cancellation can race an exact timer tick. Treat a shorter tail as the same interval so it
    // cannot add a near-zero-rate point, while retaining a substantial partial interval.
    internal static bool ShouldCaptureFinalSample(TimeSpan elapsedSinceLastSample, TimeSpan sampleInterval)
        => elapsedSinceLastSample >= TimeSpan.FromTicks(sampleInterval.Ticks / 2);

    internal ResourceTrendSnapshot GetSnapshot(ResourceTrendThresholds thresholds)
    {
        var samples = _samples.ToList();
        return new ResourceTrendSnapshot
        {
            SampleIntervalSeconds = sampleInterval.TotalSeconds,
            WarmupMinutes = thresholds.WarmupMinutes,
            Samples = samples,
            Analysis = ResourceTrendAnalyzer.Analyze(samples, thresholds)
        };
    }

    private void CaptureSample()
    {
        var elapsedSeconds = _elapsed.Elapsed.TotalSeconds;
        if (_samples.Count > 0 && elapsedSeconds <= _lastSampleElapsedSeconds)
        {
            return;
        }

        var currentProducedCount = producedCount();
        var currentConsumedCount = consumedCount();
        var intervalSeconds = elapsedSeconds - _lastSampleElapsedSeconds;
        var producedRate = intervalSeconds > 0
            ? (currentProducedCount - _lastProducedCount) / intervalSeconds
            : 0;
        var consumedRate = intervalSeconds > 0
            ? (currentConsumedCount - _lastConsumedCount) / intervalSeconds
            : 0;

        using var process = Process.GetCurrentProcess();
        process.Refresh();

        var gcMemoryInfo = GC.GetGCMemoryInfo();
        var generationInfo = gcMemoryInfo.GenerationInfo;
        var lohSize = generationInfo.Length > 3 ? generationInfo[3].SizeAfterBytes : 0;
        var sample = new ResourceSample
        {
            ElapsedMinutes = elapsedSeconds / 60,
            WorkingSetBytes = process.WorkingSet64,
            GcHeapBytes = GC.GetTotalMemory(forceFullCollection: false),
            LohSizeBytes = lohSize,
            Gen2Collections = GC.CollectionCount(2),
            ThreadCount = process.Threads.Count,
            ProducedMessagesPerSecond = producedRate,
            ConsumedMessagesPerSecond = consumedRate
        };

        _samples.Add(sample);
        _lastProducedCount = currentProducedCount;
        _lastConsumedCount = currentConsumedCount;
        _lastSampleElapsedSeconds = elapsedSeconds;

        Console.WriteLine(
            $"  [{DateTime.UtcNow:HH:mm:ss}] Soak resources: " +
            $"WorkingSet={ToMib(sample.WorkingSetBytes):N1}MiB, " +
            $"GCHeap={ToMib(sample.GcHeapBytes):N1}MiB, " +
            $"LOH={ToMib(sample.LohSizeBytes):N1}MiB, " +
            $"Gen2={sample.Gen2Collections:N0}, Threads={sample.ThreadCount:N0}, " +
            $"Produced={sample.ProducedMessagesPerSecond:N0}msg/s, " +
            $"Consumed={sample.ConsumedMessagesPerSecond:N0}msg/s");
    }

    private static double ToMib(long bytes) => bytes / (1024.0 * 1024);
}

internal sealed class ResourceTrendSnapshot
{
    public required double SampleIntervalSeconds { get; init; }
    public required double WarmupMinutes { get; init; }
    public required List<ResourceSample> Samples { get; init; }
    public required ResourceTrendAnalysis Analysis { get; init; }
}
