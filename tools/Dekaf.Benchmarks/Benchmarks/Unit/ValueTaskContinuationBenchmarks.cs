using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class ValueTaskContinuationBenchmarks
{
    private readonly ValueTaskSourcePool<int> _pool = new(maxPoolSize: 1);

    [Benchmark(Baseline = true)]
    public ValueTask<int> AsynchronousContinuation() => Complete(runContinuationsAsynchronously: true);

    [Benchmark]
    public ValueTask<int> InlineContinuation() => Complete(runContinuationsAsynchronously: false);

    private ValueTask<int> Complete(bool runContinuationsAsynchronously)
    {
        var source = _pool.Rent();
        source.SetRunContinuationsAsynchronously(runContinuationsAsynchronously);
        var continuation = AwaitCompletion(source.Task);
        source.SetResult(42);
        return continuation;
    }

    private static async ValueTask<int> AwaitCompletion(ValueTask<int> task) =>
        await task.ConfigureAwait(false);
}
