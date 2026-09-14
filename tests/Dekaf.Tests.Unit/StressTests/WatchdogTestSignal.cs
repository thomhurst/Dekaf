namespace Dekaf.Tests.Unit.StressTests;

/// <summary>Separates watchdog callback deadlines from scheduling test assertions.</summary>
internal sealed class WatchdogTestSignal(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    // WaitAsync must see completion on the callback thread. With asynchronous
    // continuations, a saturated pool can process the timer before the queued
    // completion action even though the callback ran before the deadline.
    private readonly TaskCompletionSource<(int Code, long Timestamp)> _completion = new(TaskCreationOptions.None);

    internal void Complete(int code = 0) => _completion.TrySetResult((code, _time.GetTimestamp()));

    internal async Task<int> WaitAsync(TimeSpan timeout)
    {
        var started = _time.GetTimestamp();
        var completion = await _completion.Task.WaitAsync(timeout);

        // A pending await may resume inline in Complete. Yield after the bounded wait
        // so assertions and Thread.Join never run on the watchdog thread itself.
        await Task.Yield();

        // Timer callbacks also depend on the pool. Reject a late callback even
        // when it beats delayed timer processing; later assertion scheduling
        // must not turn an on-time callback into a timeout.
        if (timeout != Timeout.InfiniteTimeSpan && completion.Timestamp > started
            && _time.GetElapsedTime(started, completion.Timestamp) >= timeout)
            throw new TimeoutException();

        return completion.Code;
    }
}
