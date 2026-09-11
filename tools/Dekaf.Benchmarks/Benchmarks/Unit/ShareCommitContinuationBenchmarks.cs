using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures healthy commits that suspend for a broker response. The controlled reply
/// prevents synchronous completion from hiding growth in the commit state machine.
/// Reply-gate and scheduling costs are fixture overhead on both measured revisions.
/// </summary>
[MemoryDiagnoser]
public class ShareCommitContinuationBenchmarks
{
    private HostedShareRequestBenchmarks _requests = null!;

    [Params(false, true)]
    public bool Hosted { get; set; }

    [Params(1, 16)]
    public int PartitionCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _requests = new HostedShareRequestBenchmarks { Hosted = Hosted, PartitionCount = PartitionCount };
        await _requests.Setup();
        await Commit();
    }

    [Benchmark]
    public ValueTask Commit() => _requests.CommitWithDeferredReply();

    [GlobalCleanup]
    public Task Cleanup() => _requests.Cleanup();
}
