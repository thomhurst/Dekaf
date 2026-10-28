using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Polls with a cancellable token that is never cancelled. A poll that can be cancelled keeps
/// observing its in-flight ShareFetch so the next poll can resume it, which adds a per-poll wait
/// on the fetch tasks. Compare with <see cref="ShareConsumerPollBenchmarks"/> for the uncancellable path.
/// The buffered case fetches four batches and delivers one batch per poll window, so three of every
/// four windows drain implicit acknowledgements with a ShareAcknowledge while records stay buffered.
/// </summary>
[MemoryDiagnoser]
public class ShareConsumerCancellablePollBenchmarks
{
    private readonly CancellationTokenSource _cancellation = new();
    private ShareConsumerPollBenchmarks _poll = null!;
    private ShareConsumerPollBenchmarks _buffered = null!;

    [Params(1, 1024)]
    public int RecordCount { get; set; }

    [Params(false, true)]
    public bool AsynchronousResponse { get; set; }

    [GlobalSetup]
    public async ValueTask Setup()
    {
        _poll = new ShareConsumerPollBenchmarks
        {
            RecordCount = RecordCount, AsynchronousResponse = AsynchronousResponse,
            PollCancellationToken = _cancellation.Token
        };
        await _poll.Setup();
        _buffered = new ShareConsumerPollBenchmarks
        {
            RecordCount = RecordCount, BatchCount = 4, AsynchronousResponse = AsynchronousResponse,
            PollCancellationToken = _cancellation.Token, CompatibilityMaxPollRecords = RecordCount
        };
        await _buffered.Setup();
    }

    [Benchmark]
    public ValueTask<long> PollCompatibilityBatch() => _poll.PollCompatibilityBatch();

    [Benchmark]
    public ValueTask<long> PollBorrowedBatch() => _poll.PollBorrowedBatch();

    [Benchmark]
    public ValueTask<long> PollBufferedCompatibilityBatch() => _buffered.PollCompatibilityBatch();

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _poll.Cleanup();
        await _buffered.Cleanup();
        _cancellation.Dispose();
    }
}
