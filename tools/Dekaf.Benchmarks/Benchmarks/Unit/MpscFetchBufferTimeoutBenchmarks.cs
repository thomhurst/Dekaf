using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 2, iterationCount: 3)]
[IterationTime(5000)]
public class MpscFetchBufferTimeoutBenchmarks
{
    private readonly MpscFetchBuffer _buffer = new(4);
    private CancellationTokenSource? _cancellationSource;
    private CancellationToken _cancellationToken;

    [Params(false, true)]
    public bool Cancellable { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        if (Cancellable)
        {
            _cancellationSource = new CancellationTokenSource();
            _cancellationToken = _cancellationSource.Token;
        }

        await _buffer.WaitToReadAsync(timeoutMs: 1, _cancellationToken).ConfigureAwait(false);
    }

    [Benchmark]
    public ValueTask<bool> TimeoutAsync() =>
        _buffer.WaitToReadAsync(timeoutMs: 1, _cancellationToken);

    [GlobalCleanup]
    public void Cleanup()
    {
        _buffer.Dispose();
        _cancellationSource?.Dispose();
    }
}
