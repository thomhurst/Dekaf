namespace Dekaf.Tests.Unit.StressTests;

/// <summary>
/// Completes when a watchdog callback runs. Waits have no wall-clock cap: under CPU contention
/// an on-time callback can land arbitrarily late, so the test-level timeout is the only backstop.
/// </summary>
internal sealed class WatchdogTestSignal
{
    private readonly TaskCompletionSource<int> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void Complete(int code = 0) => _completion.TrySetResult(code);

    // Continuations run asynchronously, so assertions and Thread.Join never run on the watchdog thread.
    internal Task<int> WaitAsync() => _completion.Task;
}
