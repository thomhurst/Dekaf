using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ShortRunJob]
public class BrokerPrefetchSchedulerBenchmarks
{
    private BrokerPrefetchScheduler _scheduler = null!;
    private TaskCompletionSource _completion = null!;
    private CancellationTokenSource _cancellation = null!;

    [IterationSetup]
    public void Setup()
    {
        _scheduler = new BrokerPrefetchScheduler();
        _completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _cancellation = new CancellationTokenSource();

        if (!_scheduler.TryStart((BrokerId: 1, ConnectionIndex: 0), () => _completion.Task))
            throw new InvalidOperationException("Could not register benchmark task.");
    }

    [IterationCleanup]
    public void Cleanup() => _cancellation.Dispose();

    [Benchmark(Baseline = true)]
    public ValueTask TaskWhenAny_PendingCancellable()
    {
        var wait = WaitWithTaskWhenAnyAsync(_completion.Task, _cancellation.Token);
        _completion.SetResult();
        return wait;
    }

    [Benchmark]
    public ValueTask SingleTask_PendingCancellable()
    {
        var wait = _scheduler.WaitForAnyAsync(_cancellation.Token);
        _completion.SetResult();
        return wait;
    }

    private static async ValueTask WaitWithTaskWhenAnyAsync(Task task, CancellationToken cancellationToken)
    {
        Task[] tasks = [task];
        await Task.WhenAny(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
