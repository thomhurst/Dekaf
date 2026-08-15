using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Internal;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 3, iterationCount: 10, invocationCount: 100)]
public class AsyncAutoResetSignalTimeoutBenchmarks
{
    private readonly AsyncAutoResetSignal _signal = new(inlineTimeoutContinuations: true);

    [Benchmark]
    public ValueTask<bool> TimeoutWait() => _signal.WaitAsync(1);

    [GlobalCleanup]
    public void Cleanup() => _signal.Dispose();
}
