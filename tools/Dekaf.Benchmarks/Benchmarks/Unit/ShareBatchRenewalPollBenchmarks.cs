using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures acquisition, renewal, partial replay and the standalone commit before an
/// unread renewal is replayed. Parsing and acknowledgement costs are per batch/cycle.
/// Reuses the production-polling fixture's deterministic synchronous/asynchronous broker.
/// </summary>
[MemoryDiagnoser]
public class ShareBatchRenewalPollBenchmarks
{
    private ShareConsumerPollBenchmarks _poll = null!;

    [Params(64, 1024)]
    public int RecordCount { get; set; }

    [Params(false, true)]
    public bool AsynchronousResponse { get; set; }

    [Params(ShareAcquisitionShape.Contiguous, ShareAcquisitionShape.SparseRanges, ShareAcquisitionShape.InterleavedRanges)]
    public ShareAcquisitionShape AcquisitionShape { get; set; }

    [GlobalSetup]
    public ValueTask Setup()
    {
        _poll = new ShareConsumerPollBenchmarks
        {
            RecordCount = RecordCount, BatchCount = 1,
            AsynchronousResponse = AsynchronousResponse, RenewalMode = true, AcquisitionShape = AcquisitionShape
        };
        return _poll.Setup();
    }

    [Benchmark]
    public ValueTask<long> AcquireRenewAndReplay() => _poll.PollBorrowedRenewalCycle();

    [GlobalCleanup]
    public ValueTask Cleanup() => _poll.Cleanup();
}
