namespace Dekaf.Internal;

/// <summary>
/// Marks the fault of a task that nothing awaits as observed, so that abandoning the task (for
/// example after a bounded wait times out) does not raise
/// <see cref="TaskScheduler.UnobservedTaskException"/> when it later fails.
/// </summary>
internal static class TaskFaultObserver
{
    public static void ObserveFault(Task task)
    {
        if (task.IsCompleted)
        {
            _ = task.Exception;
            return;
        }

        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
