using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures stable polling and repeated equivalent subscriptions through the public API.</summary>
[MemoryDiagnoser]
public class ShareConsumerSubscriptionBenchmarks
{
    private ShareConsumerPollBenchmarks _consumer = null!;

    [Params(1, 1024)] public int RecordCount { get; set; }
    [Params(false, true)] public bool Batch { get; set; }

    [GlobalSetup]
    public async ValueTask Setup()
    {
        _consumer = new ShareConsumerPollBenchmarks { RecordCount = RecordCount };
        await _consumer.Setup();
    }

    [Benchmark]
    public ValueTask<long> StablePoll()
        => Batch ? _consumer.PollBorrowedBatch() : _consumer.PollCompatibilityBatch();

    [Benchmark]
    public ValueTask<long> EquivalentSubscriptionAndPoll()
    {
        _consumer.RepeatSubscription(Batch);
        return StablePoll();
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.Cleanup();
}
