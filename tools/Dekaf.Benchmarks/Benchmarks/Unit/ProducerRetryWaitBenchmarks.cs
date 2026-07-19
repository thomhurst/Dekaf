using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Internal;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class ProducerRetryWaitBenchmarks
{
    private readonly AsyncAutoResetSignal _signal = new();

    [Benchmark(Baseline = true)]
    public Task TimedPoll() => Task.Delay(1);

    [Benchmark]
    public ValueTask<bool> SignaledWait()
    {
        _signal.Signal();
        return _signal.WaitAsync(100);
    }

    [GlobalCleanup]
    public void Cleanup() => _signal.Dispose();
}
