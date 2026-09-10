using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures production polling over sixteen batches with gaps and multiple acquisition ranges.</summary>
[MemoryDiagnoser]
public class ShareConsumerSparsePollBenchmarks
{
    private ShareConsumerPollBenchmarks _poll = null!;

    [Params(64, 1024)]
    public int RecordCount { get; set; }

    [Params(false, true)]
    public bool AsynchronousResponse { get; set; }

    [Params(ShareAcquisitionShape.SparseRanges, ShareAcquisitionShape.InterleavedRanges)]
    public ShareAcquisitionShape AcquisitionShape { get; set; }

    [GlobalSetup]
    public ValueTask Setup()
    {
        _poll = new ShareConsumerPollBenchmarks
        {
            RecordCount = RecordCount, BatchCount = 16,
            AsynchronousResponse = AsynchronousResponse, AcquisitionShape = AcquisitionShape
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
