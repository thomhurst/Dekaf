using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures a complete acquisition/renewal cycle when a producer batch exceeds the replay
/// poll budget. Each operation drains every renewal through bounded chunks and commits them.
/// The size axis exposes repeated tracking scans; allocations are per batch and replay chunk.
/// </summary>
[MemoryDiagnoser]
public class ShareBatchChunkedRenewalBenchmarks
{
    private ShareConsumerPollBenchmarks _poll = null!;

    [Params(256, 1024, 4096)]
    public int RecordCount { get; set; }

    [Params(1, 64)]
    public int ReplayChunkSize { get; set; }

    [GlobalSetup]
    public ValueTask Setup()
    {
        _poll = new ShareConsumerPollBenchmarks
        {
            RecordCount = RecordCount, BatchCount = 1,
            RenewalMode = true, ReplayChunkSize = ReplayChunkSize
        };
        return _poll.Setup();
    }

    [Benchmark]
    public ValueTask<long> AcquireRenewAndDrainChunks() => _poll.PollBorrowedChunkedRenewalCycle();

    [GlobalCleanup]
    public ValueTask Cleanup() => _poll.Cleanup();
}
