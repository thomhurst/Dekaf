using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures one actual poll with an initially missing leader. The broker can supply
/// current metadata immediately; an independent timer also publishes it so the old
/// spinning poll completes. Both revisions join that timer before the next operation.
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

    [GlobalSetup]
    public async ValueTask Setup()
    {
        _poll = new ShareConsumerPollBenchmarks { RecordCount = 1, BatchCount = 1 };
        await _poll.Setup();
        _poll.PrepareMissingLeaderPolling(Batch);
        if (!await _poll.PollWithMissingLeader(MetadataDelayMs))
            throw new InvalidOperationException("Leader recovery lost the pending delivery.");
    }

    [Benchmark]
    public ValueTask<bool> RecoverLeader() => _poll.PollWithMissingLeader(MetadataDelayMs);

    [GlobalCleanup]
    public ValueTask Cleanup() => _poll.Cleanup();
}