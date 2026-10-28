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

    private const int PolicyLookups = 8;

    // ShouldClientThrottle runs once per response. Several keys per case keep the
    // measurement above the sub-nanosecond range, where hosted-runner noise dominates.
    private readonly ApiKey[] _versionGatedApiKeys =
    [
        ApiKey.Produce, ApiKey.Fetch, ApiKey.Metadata, ApiKey.OffsetCommit,
        ApiKey.JoinGroup, ApiKey.Heartbeat, ApiKey.CreateTopics, ApiKey.DeleteGroups
    ];

    private readonly ApiKey[] _schemaDefaultApiKeys =
    [
        ApiKey.ConsumerGroupHeartbeat, ApiKey.ShareFetch, ApiKey.DescribeCluster, ApiKey.DescribeProducers,
        ApiKey.ListTransactions, ApiKey.DescribeQuorum, ApiKey.SaslAuthenticate, ApiKey.OffsetForLeaderEpoch
    ];

    private readonly short _version = 1;

    [Benchmark(Baseline = true)]
    public bool SharedBrokerThrottleFastPath() =>
        _unthrottled.WaitAsync(CancellationToken.None, CancellationToken.None).IsCompletedSuccessfully;

    [Benchmark]
    public bool SharedBrokerLookupFastPath() =>
        _states.TryGetValue(1, out var state) &&
        state.WaitAsync(CancellationToken.None, CancellationToken.None).IsCompletedSuccessfully;

    [Benchmark(OperationsPerInvoke = PolicyLookups)]
    public int ShouldClientThrottleVersionGated() => CountClientThrottled(_versionGatedApiKeys);

    [Benchmark(OperationsPerInvoke = PolicyLookups)]
    public int ShouldClientThrottleSchemaDefault() => CountClientThrottled(_schemaDefaultApiKeys);

    private int CountClientThrottled(ApiKey[] apiKeys)
    {
        var count = 0;
        foreach (var apiKey in apiKeys)
        {
            if (BrokerThrottlePolicy.ShouldClientThrottle(apiKey, _version))
            {
                count++;
            }
        }

        return count;
    }
}
