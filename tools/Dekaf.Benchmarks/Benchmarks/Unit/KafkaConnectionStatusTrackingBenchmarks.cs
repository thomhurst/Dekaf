using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Isolates the per-request cost of publishing the successful-request timestamp added by
/// client-status snapshots. Both cases share one monotonic clock read; the candidate adds
/// exactly one volatile store and no allocation.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class KafkaConnectionStatusTrackingBenchmarks
{
    private long _lastUsedTimestampMs;
    private long _lastSuccessfulRequestTimestampMs;

    [Benchmark(Baseline = true)]
    public void IdleTrackingOnly()
    {
        var timestampMs = MonotonicClock.GetMilliseconds();
        Volatile.Write(ref _lastUsedTimestampMs, timestampMs);
    }

    [Benchmark]
    public void IdleAndSuccessfulRequestTracking()
    {
        var timestampMs = MonotonicClock.GetMilliseconds();
        Volatile.Write(ref _lastUsedTimestampMs, timestampMs);
        Volatile.Write(ref _lastSuccessfulRequestTimestampMs, timestampMs);
    }
}
