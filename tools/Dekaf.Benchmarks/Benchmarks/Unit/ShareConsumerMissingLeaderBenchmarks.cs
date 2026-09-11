using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures one actual poll with an initially missing leader. Metadata completes
/// synchronously or after an independent timer, which also updates the cache so the
/// old spinning poll completes. The timer starts after the first routing enumeration
/// observes the missing leader. Both revisions pay for that one fixture observer and
/// join the timer before the next operation.
/// Elapsed time therefore includes the fixture delay; this primarily exposes allocation
/// cost while routing is unavailable, not real broker latency or loaded acceptance.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerMissingLeaderBenchmarks
{
    private ShareConsumerPollBenchmarks _poll = null!;

    [Params(false, true)]
    public bool Batch { get; set; }

    [Params(1, 10)]
    public int MetadataDelayMs { get; set; }

    [Params(false, true)]
    public bool AsynchronousMetadata { get; set; }

    [GlobalSetup]
    public async ValueTask Setup()
    {
        _poll = new ShareConsumerPollBenchmarks { RecordCount = 1, BatchCount = 1 };
        await _poll.Setup();
        _poll.PrepareMissingLeaderPolling(Batch, AsynchronousMetadata);
        if (!await _poll.PollWithMissingLeader(MetadataDelayMs))
            throw new InvalidOperationException("Leader recovery lost the pending delivery.");
    }

    [Benchmark]
    public ValueTask<bool> RecoverLeader() => _poll.PollWithMissingLeader(MetadataDelayMs);

    [GlobalCleanup]
    public ValueTask Cleanup() => _poll.Cleanup();
}
