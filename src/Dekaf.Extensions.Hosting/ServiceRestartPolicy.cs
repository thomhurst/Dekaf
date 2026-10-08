using Dekaf.Errors;

namespace Dekaf.Extensions.Hosting;

/// <summary>
/// Restart rules shared by the hosted consumer services for failures outside record processing:
/// initialization, subscription, and polling.
/// </summary>
internal static class ServiceRestartPolicy
{
    /// <summary>
    /// Whether a failure may heal on its own. Disposal, argument, and deserialization errors
    /// repeat identically, so they fault the service instead.
    /// </summary>
    public static bool CanRestartAfter(Exception exception) =>
        exception is not (ObjectDisposedException or ArgumentException or SerializationException);

    /// <summary>Doubles <paramref name="initialDelay"/> per consecutive failure, capped at <paramref name="maxDelay"/>.</summary>
    public static TimeSpan GetDelay(TimeSpan initialDelay, TimeSpan maxDelay, int restartAttempt)
    {
        var multiplier = Math.Pow(2, Math.Min(restartAttempt - 1, 30));
        var delayTicks = Math.Min(initialDelay.Ticks * multiplier, maxDelay.Ticks);
        return TimeSpan.FromTicks((long)delayTicks);
    }
}
