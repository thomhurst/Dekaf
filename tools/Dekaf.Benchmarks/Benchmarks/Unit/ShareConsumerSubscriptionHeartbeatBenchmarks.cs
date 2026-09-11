using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Includes real heartbeat handling and deterministic response suspension, excluding broker latency.</summary>
[MemoryDiagnoser]
public class ShareConsumerSubscriptionHeartbeatBenchmarks
{
    private ShareConsumerPollBenchmarks _consumer = null!;

    [Params(false, true)] public bool AsynchronousResponse { get; set; }

    [GlobalSetup]
    public async ValueTask Setup()
    {
        _consumer = new ShareConsumerPollBenchmarks { RecordCount = 1, AsynchronousResponse = AsynchronousResponse };
        await _consumer.Setup();
        _consumer.PrepareSubscriptionHeartbeats();
        await _consumer.SendSubscriptionHeartbeat();
    }

    [Benchmark]
    public ValueTask<bool> UnchangedHeartbeat() => _consumer.SendSubscriptionHeartbeat();

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.Cleanup();
}
