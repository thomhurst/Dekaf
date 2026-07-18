using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

namespace Dekaf.Benchmarks.Infrastructure;

/// <summary>
/// The high-fidelity throughput job used by the published benchmark tables
/// (docs/docs/benchmarks.md): single launch, throughput strategy, with enough
/// iterations that the 99.9% confidence interval stays well below the mean on
/// shared CI runners (3-iteration jobs routinely produced Error &gt; Mean).
/// Classes whose iterations are single-shot and setup-dominated may pass lower
/// counts — one extra iteration there buys one sample at seconds of setup cost.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ThroughputJobAttribute : JobConfigBaseAttribute
{
    public ThroughputJobAttribute(int warmupCount = 5, int iterationCount = 10)
        : base(Job.Default
            .WithStrategy(RunStrategy.Throughput)
            .WithLaunchCount(1)
            .WithWarmupCount(warmupCount)
            .WithIterationCount(iterationCount))
    {
    }
}
