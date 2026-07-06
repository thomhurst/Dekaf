namespace Dekaf.Internal;

internal static class ConnectionOptionValidation
{
    public static TimeSpan ValidatePositiveTimeout(TimeSpan timeout, string paramName, string message)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(paramName, message);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeout.TotalMilliseconds, int.MaxValue, paramName);
        return timeout;
    }

    public static void ValidateTcpKeepAlive(TimeSpan time, TimeSpan interval, int retryCount)
    {
        ValidatePositiveTimeout(time, nameof(time), "TCP keepalive time must be positive");
        ValidatePositiveTimeout(interval, nameof(interval), "TCP keepalive interval must be positive");
        ArgumentOutOfRangeException.ThrowIfLessThan(retryCount, 1);
    }
}
