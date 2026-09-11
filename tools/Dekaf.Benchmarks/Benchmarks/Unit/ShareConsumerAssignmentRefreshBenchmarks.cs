using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures a heartbeat assignment refresh followed by a classic poll window.
/// Existing coordinator allocation is per assignment. Prepared inputs and delegate
/// binding are outside measurement; buffered-acquisition retention is exercised on
/// each window, including the unchanged-assignment contents of routine heartbeats.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerAssignmentRefreshBenchmarks
{
    private ShareConsumerPollBufferBenchmarks _poll = null!;

    [Params(1, 64)]
    public int PartitionCount { get; set; }

    [Params(false, true)]
    public bool Overflow { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _poll = new ShareConsumerPollBufferBenchmarks
        {
            PartitionCount = PartitionCount, Overflow = Overflow
        };
        await _poll.Setup();
    }

    [Benchmark]
    public ValueTask<long> AssignmentRefreshThenPollWindow()
    {
        _poll.RefreshAssignment();
        return _poll.PollWindow();
    }

    [GlobalCleanup]
    public Task Cleanup() => _poll.Cleanup();
}
