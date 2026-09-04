using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class BrokerPrefetchDispatchBenchmarks
{
    private BrokerPrefetchScheduler _pending = null!;
    private BrokerPrefetchScheduler _completed = null!;
    private Task _task = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pending = new BrokerPrefetchScheduler();
        _completed = new BrokerPrefetchScheduler();
        _task = Task.CompletedTask;
        _pending.TryStart((1, 0), static () => new TaskCompletionSource().Task);
        // Populate the completed list before measuring steady dispatch.
        _completed.TryStart((1, 0), static () => Task.CompletedTask);
        _completed.DrainCompletedAsync().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public bool CapturingFactory_Pending() => _pending.TryStart((1, 0), Capture(_task));

    [Benchmark]
    public bool StateFactory_Pending() => _pending.TryStart((1, 0), _task, static task => task);

    [Benchmark]
    public ValueTask<int> CapturingFactory_StartAndDrain()
    {
        _completed.TryStart((1, 0), Capture(_task));
        return _completed.DrainCompletedAsync();
    }

    [Benchmark]
    public ValueTask<int> StateFactory_StartAndDrain()
    {
        _completed.TryStart((1, 0), _task, static task => task);
        return _completed.DrainCompletedAsync();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<Task> Capture(Task task) => () => task;

    [GlobalCleanup]
    public void Cleanup()
    {
        _pending.Dispose();
        _completed.Dispose();
    }
}

/// <summary>
/// Measures one prefetch-loop wake cycle, amortized over <see cref="WakeCycles"/> cycles per
/// invocation so per-wake time and allocation are resolvable: start a task for the completing
/// key, wait for any in-flight task, complete it, resume, drain it. <c>TaskWhenAny_*</c> is the
/// hand-rolled reference (dictionary snapshot + <c>Task.WhenAny</c> + <c>WaitAsync</c>) the
/// scheduler used to inline and serves as the within-run control; <c>Scheduler_*</c> exercise
/// <see cref="BrokerPrefetchScheduler"/> itself. Both variants pay the same per-cycle
/// <see cref="TaskCompletionSource"/> and closure, so the difference is the wake-up mechanism.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class BrokerPrefetchSchedulerBenchmarks
{
    private const int WakeCycles = 1_000;
    private const int PendingPeers = 3;
    private static readonly Task PendingTask = new TaskCompletionSource().Task;
    private static readonly (int BrokerId, int ConnectionIndex) CompletingKey = (BrokerId: 0, ConnectionIndex: 0);

    private BrokerPrefetchScheduler _singleScheduler = null!;
    private BrokerPrefetchScheduler _multiScheduler = null!;
    private Dictionary<(int BrokerId, int ConnectionIndex), Task> _whenAnySingle = null!;
    private Dictionary<(int BrokerId, int ConnectionIndex), Task> _whenAnyMulti = null!;
    private CancellationTokenSource _cancellation = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cancellation = new CancellationTokenSource();
        _singleScheduler = new BrokerPrefetchScheduler();
        _multiScheduler = new BrokerPrefetchScheduler();
        _whenAnySingle = [];
        _whenAnyMulti = [];
        for (var peer = 1; peer <= PendingPeers; peer++)
        {
            var key = (BrokerId: peer, ConnectionIndex: 0);
            _multiScheduler.TryStart(key, static () => PendingTask);
            _whenAnyMulti[key] = PendingTask;
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => _cancellation.Dispose();

    [Benchmark(Baseline = true, OperationsPerInvoke = WakeCycles)]
    public Task TaskWhenAny_SinglePending() => RunTaskWhenAnyCyclesAsync(_whenAnySingle);

    [Benchmark(OperationsPerInvoke = WakeCycles)]
    public Task Scheduler_SinglePending() => RunSchedulerCyclesAsync(_singleScheduler);

    [Benchmark(OperationsPerInvoke = WakeCycles)]
    public Task TaskWhenAny_MultiPending() => RunTaskWhenAnyCyclesAsync(_whenAnyMulti);

    [Benchmark(OperationsPerInvoke = WakeCycles)]
    public Task Scheduler_MultiPending() => RunSchedulerCyclesAsync(_multiScheduler);

    private async Task RunSchedulerCyclesAsync(BrokerPrefetchScheduler scheduler)
    {
        for (var cycle = 0; cycle < WakeCycles; cycle++)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!scheduler.TryStart(CompletingKey, CreateTaskFactory(completion)))
                throw new InvalidOperationException("Completing key was still in flight.");

            var wait = scheduler.WaitForAnyAsync(_cancellation.Token);
            completion.SetResult();
            await wait.ConfigureAwait(false);

            if (await scheduler.DrainCompletedAsync().ConfigureAwait(false) != 1)
                throw new InvalidOperationException("Expected exactly one completed task per cycle.");
        }
    }

    private async Task RunTaskWhenAnyCyclesAsync(Dictionary<(int BrokerId, int ConnectionIndex), Task> inFlight)
    {
        for (var cycle = 0; cycle < WakeCycles; cycle++)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            inFlight[CompletingKey] = CreateTaskFactory(completion)();

            var wait = WaitWithTaskWhenAnyAsync(inFlight, _cancellation.Token);
            completion.SetResult();
            await wait.ConfigureAwait(false);

            if (!inFlight.Remove(CompletingKey))
                throw new InvalidOperationException("Completing key was not tracked.");
        }
    }

    // Keep both paths' factory allocation observable across the same call boundary.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<Task> CreateTaskFactory(TaskCompletionSource completion) => () => completion.Task;

    private static async ValueTask WaitWithTaskWhenAnyAsync(
        Dictionary<(int BrokerId, int ConnectionIndex), Task> inFlight,
        CancellationToken cancellationToken)
    {
        var tasks = inFlight.Values.ToArray();
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
