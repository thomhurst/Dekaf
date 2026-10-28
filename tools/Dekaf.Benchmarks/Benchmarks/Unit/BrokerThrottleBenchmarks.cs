using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Dekaf.Networking;
using Dekaf.Protocol;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class BrokerThrottleBenchmarks
{
    private readonly BrokerThrottleState _unthrottled = new();
    private readonly ConcurrentDictionary<int, BrokerThrottleState> _states =
        new() { [1] = new BrokerThrottleState() };
    private readonly ApiKey _versionGatedApiKey = ApiKey.Fetch;
    private readonly ApiKey _schemaDefaultApiKey = ApiKey.ConsumerGroupHeartbeat;
    private readonly short _version = 1;

    [Benchmark(Baseline = true)]
    public bool ExistingCompletedValueTaskCheck() => ValueTask.CompletedTask.IsCompletedSuccessfully;

    [Benchmark]
    public bool SharedBrokerThrottleFastPath() =>
        _unthrottled.WaitAsync(CancellationToken.None, CancellationToken.None).IsCompletedSuccessfully;

    [Benchmark]
    public bool SharedBrokerLookupFastPath() =>
        _states.TryGetValue(1, out var state) &&
        state.WaitAsync(CancellationToken.None, CancellationToken.None).IsCompletedSuccessfully;

    [Benchmark]
    public bool ShouldClientThrottleVersionGated() =>
        BrokerThrottlePolicy.ShouldClientThrottle(_versionGatedApiKey, _version);

    [Benchmark]
    public bool ShouldClientThrottleSchemaDefault() =>
        BrokerThrottlePolicy.ShouldClientThrottle(_schemaDefaultApiKey, _version);
}
