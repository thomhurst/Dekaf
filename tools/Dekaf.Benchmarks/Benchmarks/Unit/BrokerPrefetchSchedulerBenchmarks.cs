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

[MemoryDiagnoser]
[ShortRunJob]
public class BrokerPrefetchSchedulerDrainBenchmarks
{
    private const int OperationsPerInvoke = 32_768;
    private const int PendingTasksPerScheduler = 3;
    private static readonly Task PendingTask = new TaskCompletionSource().Task;
    private BrokerPrefetchScheduler[] _schedulers = null!;
    private Dictionary<(int BrokerId, int ConnectionIndex), Task>[] _baselineInFlight = null!;
    private List<KeyValuePair<(int BrokerId, int ConnectionIndex), Task>>[] _baselineCompleted = null!;

    [IterationSetup]
    public void Setup()
    {
        _schedulers = new BrokerPrefetchScheduler[OperationsPerInvoke];
        _baselineInFlight = new Dictionary<(int BrokerId, int ConnectionIndex), Task>[OperationsPerInvoke];
        _baselineCompleted = new List<KeyValuePair<(int BrokerId, int ConnectionIndex), Task>>[OperationsPerInvoke];

        for (var operation = 0; operation < OperationsPerInvoke; operation++)
        {
            var scheduler = new BrokerPrefetchScheduler();
            scheduler.TryStart((BrokerId: 0, ConnectionIndex: 0), static () => Task.CompletedTask);

            var baselineInFlight = new Dictionary<(int BrokerId, int ConnectionIndex), Task>
            {
                [(BrokerId: 0, ConnectionIndex: 0)] = Task.CompletedTask
            };

            for (var pending = 1; pending <= PendingTasksPerScheduler; pending++)
            {
                var key = (BrokerId: pending, ConnectionIndex: 0);
                scheduler.TryStart(key, static () => PendingTask);
                baselineInFlight.Add(key, PendingTask);
            }

            _schedulers[operation] = scheduler;
            _baselineInFlight[operation] = baselineInFlight;
            _baselineCompleted[operation] = [];
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = OperationsPerInvoke)]
    public async ValueTask<int> Original_MultiBrokerDrain()
    {
        var drained = 0;
        for (var operation = 0; operation < OperationsPerInvoke; operation++)
        {
            drained += await DrainCompletedOriginalAsync(
                _baselineInFlight[operation],
                _baselineCompleted[operation]).ConfigureAwait(false);
        }

        return drained;
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public async ValueTask<int> Current_MultiBrokerDrain()
    {
        var drained = 0;
        for (var operation = 0; operation < OperationsPerInvoke; operation++)
            drained += await _schedulers[operation].DrainCompletedAsync().ConfigureAwait(false);

        return drained;
    }

    private static async ValueTask<int> DrainCompletedOriginalAsync(
        Dictionary<(int BrokerId, int ConnectionIndex), Task> inFlight,
        List<KeyValuePair<(int BrokerId, int ConnectionIndex), Task>> completed)
    {
        foreach (var entry in inFlight)
        {
            if (entry.Value.IsCompleted)
                completed.Add(entry);
        }

        try
        {
            for (var i = 0; i < completed.Count; i++)
            {
                var (key, task) = completed[i];
                if (inFlight.Remove(key))
                    await task.ConfigureAwait(false);
            }

            return completed.Count;
        }
        finally
        {
            completed.Clear();
        }
    }
}
