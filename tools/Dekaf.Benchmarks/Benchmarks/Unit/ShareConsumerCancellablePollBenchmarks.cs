using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Polls with a cancellable token that is never cancelled. A poll that can be cancelled keeps
/// observing its in-flight ShareFetch so the next poll can resume it, which adds a per-poll wait
/// on the fetch tasks. Compare with <see cref="ShareConsumerPollBenchmarks"/> for the uncancellable path.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerCancellablePollBenchmarks
{
    private readonly CancellationTokenSource _cancellation = new();
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
            RecordCount = RecordCount, AsynchronousResponse = AsynchronousResponse,
            PollCancellationToken = _cancellation.Token
        };
        return _poll.Setup();
    }

    [Benchmark]
    public ValueTask<long> PollCompatibilityBatch() => _poll.PollCompatibilityBatch();

    [Benchmark]
    public ValueTask<long> PollBorrowedBatch() => _poll.PollBorrowedBatch();

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _poll.Cleanup();
        _cancellation.Dispose();
    }
}
