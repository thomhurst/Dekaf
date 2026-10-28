using BenchmarkDotNet.Attributes;
using Dekaf.Internal;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Steady-state signal and wait paths of <see cref="AsyncAutoResetSignal"/>, which back
/// producer response, accumulator and linger wakeups and consumer processing signals.
/// </summary>
[MemoryDiagnoser]
public class AsyncAutoResetSignalBenchmarks
{
    private readonly AsyncAutoResetSignal _signal = new();

    [Benchmark(Baseline = true)]
    public bool SignalThenWait()
    {
        _signal.Signal();
        return _signal.WaitAsync(Timeout.Infinite).Result;
    }

    [Benchmark]
    public bool InfiniteWaitThenSignal()
    {
        var wait = _signal.WaitAsync(Timeout.Infinite);
        _signal.Signal();
        return wait.Result;
    }

    [Benchmark]
    public bool FiniteWaitThenSignal()
    {
        var wait = _signal.WaitAsync(60_000);
        _signal.Signal();
        return wait.Result;
    }

    [GlobalCleanup]
    public void Cleanup() => _signal.Dispose();
}
