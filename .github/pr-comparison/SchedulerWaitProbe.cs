using System.Reflection;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

// Task-scoped probe of the actual suspended wait, excluding new Task allocation.
// Exercise its completion-counter/signal protocol directly: one completion then one
// removal per wait. The placeholder task is never awaited or drained by this probe.
[MemoryDiagnoser]
public class SchedulerWaitProbe
{
    private BrokerPrefetchScheduler _scheduler = null!;
    private Action _complete = null!;

    [GlobalSetup]
    public void Setup()
    {
        _scheduler = new BrokerPrefetchScheduler();
        _scheduler.TryStart((1, 0), static () => new TaskCompletionSource().Task);
        _complete = typeof(BrokerPrefetchScheduler)
            .GetMethod("OnTaskCompleted", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Action>(_scheduler);
    }

    [Benchmark]
    public ValueTask SuspendedWait()
    {
        var wait = _scheduler.WaitForAnyAsync(CancellationToken.None);
        if (wait.IsCompleted)
            throw new InvalidOperationException("Probe must suspend before notification.");
        _complete();
        if (!wait.IsCompletedSuccessfully)
            throw new InvalidOperationException("Inline notification did not complete the wait.");
        RemovedCount(_scheduler)++;
        return wait;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_removedTaskCount")]
    private static extern ref long RemovedCount(BrokerPrefetchScheduler scheduler);

    [GlobalCleanup]
    public void Cleanup() => _scheduler.Dispose();
}
