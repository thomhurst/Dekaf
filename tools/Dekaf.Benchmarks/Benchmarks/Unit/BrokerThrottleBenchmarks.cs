using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Dekaf.Networking;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class BrokerThrottleBenchmarks
{
    private readonly BrokerThrottleState _unthrottled = new();
    private readonly ConcurrentDictionary<int, BrokerThrottleState> _states =
        new() { [1] = new BrokerThrottleState() };

    [Benchmark(Baseline = true)]
    public bool ExistingCompletedValueTaskCheck() => ValueTask.CompletedTask.IsCompletedSuccessfully;

    [Benchmark]
    public bool SharedBrokerThrottleFastPath() =>
        _unthrottled.WaitAsync(CancellationToken.None, CancellationToken.None).IsCompletedSuccessfully;

    [Benchmark]
    public bool SharedBrokerLookupFastPath() =>
        _states.TryGetValue(1, out var state) &&
        state.WaitAsync(CancellationToken.None, CancellationToken.None).IsCompletedSuccessfully;
}
