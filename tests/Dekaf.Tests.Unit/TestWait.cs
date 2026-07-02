namespace Dekaf.Tests.Unit;

internal static class TestWait
{
    public static async Task UntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan? pollInterval = null)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        var delay = pollInterval ?? TimeSpan.FromMilliseconds(10);

        while (!condition())
        {
            if (Environment.TickCount64 >= deadline)
                throw new TimeoutException("Condition was not reached before the timeout.");

            await Task.Delay(delay);
        }
    }

    public static async Task UntilAsync(
        Func<bool> condition,
        CancellationToken cancellationToken,
        TimeSpan? pollInterval = null)
    {
        var delay = pollInterval ?? TimeSpan.FromMilliseconds(10);

        while (!condition())
        {
            await Task.Delay(delay, cancellationToken);
        }
    }
}
