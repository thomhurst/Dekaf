using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Delivers one record per iterator from sixteen-record poll windows. Measures the
/// retained consumer's undisclosed-record ownership and parser continuation across
/// poll boundaries, with and without renewal state reserving the fresh-record budget.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerPartialPollBenchmarks
{
    private ShareConsumerPollBufferBenchmarks _poll = null!;

    [Params(false, true)]
    public bool Prepared { get; set; }

    [Params(false, true)]
    public bool RenewalBuffering { get; set; }

    [GlobalSetup]
    public Task Setup()
    {
        _poll = new ShareConsumerPollBufferBenchmarks
        {
            PartitionCount = 1, Overflow = true, Prepared = Prepared,
            RenewalBuffering = RenewalBuffering, RecordsPerIteration = 1
        };
        return _poll.Setup();
    }

    [Benchmark]
    public ValueTask<long> PollOneRecord() => _poll.PollWindow();

    [GlobalCleanup]
    public Task Cleanup() => _poll.Cleanup();
}
