using System.Runtime.CompilerServices;

namespace Dekaf.Tests.Unit;

/// <summary>
/// Detects a specific exception reaching <see cref="TaskScheduler.UnobservedTaskException"/>.
/// Matching by exception instance keeps unrelated leaks from parallel tests out of the result.
/// </summary>
internal sealed class UnobservedTaskExceptionProbe : IDisposable
{
    private readonly Exception _target;
    private int _observed;

    public UnobservedTaskExceptionProbe(Exception target)
    {
        _target = target;
        TaskScheduler.UnobservedTaskException += OnUnobserved;
    }

    /// <summary>
    /// Runs <paramref name="scenario"/> in its own frame so its tasks become unreachable, then
    /// forces finalization and reports whether the target exception went unobserved.
    /// </summary>
    public async Task<bool> RunAndCollectAsync(Func<Task> scenario)
    {
        await RunScenarioAsync(scenario).ConfigureAwait(false);
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return Volatile.Read(ref _observed) != 0;
    }

    public void Dispose() => TaskScheduler.UnobservedTaskException -= OnUnobserved;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task RunScenarioAsync(Func<Task> scenario) => scenario();

    private void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (Contains(e.Exception, _target))
            Volatile.Write(ref _observed, 1);
    }

    private static bool Contains(Exception? exception, Exception target)
    {
        if (exception is null)
            return false;
        if (ReferenceEquals(exception, target))
            return true;
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                if (Contains(inner, target))
                    return true;
            }

            return false;
        }

        return Contains(exception.InnerException, target);
    }
}
