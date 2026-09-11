using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures repeated subscribe/fetch/unsubscribe cycles, including awaited release work.</summary>
[MemoryDiagnoser]
public class ShareConsumerUnsubscribeBenchmarks
{
    private ShareConsumerPollBenchmarks _poll = null!;

    [Params(1, 16)]
    public int BatchCount { get; set; }

    [Params(1, 1024)]
    public int RecordCount { get; set; }

    [GlobalSetup]
    public async ValueTask Setup()
    {
        _poll = new ShareConsumerPollBenchmarks { BatchCount = BatchCount, RecordCount = RecordCount };
        await _poll.Setup();
        await _poll.PrepareUnsubscribeCycles();
        if (await _poll.PollThenUnsubscribe() != (long)BatchCount * RecordCount)
            throw new InvalidOperationException("Unsubscribe failed to release every response acquisition.");
    }

    // One operation is a lifecycle, including subscription, one yielded batch, release
    // request construction, sending and iterator cleanup. Allocation is per lifecycle.
    [Benchmark]
    public ValueTask<long> UnsubscribeAfterFirstBatch() => _poll.PollThenUnsubscribe();

    [GlobalCleanup]
    public ValueTask Cleanup() => _poll.Cleanup();
}
