using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class MpscFetchBufferTimeoutBenchmarks
{
    private readonly MpscFetchBuffer _buffer = new(4);

    [Benchmark]
    public ValueTask<bool> TimeoutAsync() =>
        _buffer.WaitToReadAsync(timeoutMs: 1, CancellationToken.None);
}
