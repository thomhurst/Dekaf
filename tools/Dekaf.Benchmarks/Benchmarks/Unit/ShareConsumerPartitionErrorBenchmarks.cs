using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures steady-state polling when every ShareFetch response also carries a partition-level
/// error for a partition the consumer no longer fetches from that broker. The error is handled
/// once per response, so delivery should cost the same as a response without it.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerPartitionErrorBenchmarks
{
    private ShareConsumerPollBenchmarks _poll = null!;

    [Params(1, 1024)]
    public int RecordCount { get; set; }

    [Params(false, true)]
    public bool AsynchronousResponse { get; set; }

    [GlobalSetup]
    public ValueTask Setup()
    {
        _poll = new ShareConsumerPollBenchmarks
        {
            RecordCount = RecordCount, BatchCount = 1,
            AsynchronousResponse = AsynchronousResponse, StalePartitionError = true
        };
        return _poll.Setup();
    }

    [Benchmark]
    public ValueTask<long> PollCompatibilityBatch() => _poll.PollCompatibilityBatch();

    [Benchmark]
    public ValueTask<long> PollBorrowedBatch() => _poll.PollBorrowedBatch();

    [GlobalCleanup]
    public ValueTask Cleanup() => _poll.Cleanup();
}
