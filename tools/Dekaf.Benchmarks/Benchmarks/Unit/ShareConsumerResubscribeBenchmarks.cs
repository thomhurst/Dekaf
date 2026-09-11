using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures replacing a classic subscription before another poll window. Retained
/// assignments keep acknowledgements inline; overflow cases release undisclosed ranges.
/// Work and allocations are per subscription change plus window, not per message.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerResubscribeBenchmarks
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
    public ValueTask<long> ResubscribeThenPollWindow()
    {
        _poll.Resubscribe();
        return _poll.PollWindow();
    }

    [GlobalCleanup]
    public Task Cleanup() => _poll.Cleanup();
}
