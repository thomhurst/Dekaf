namespace Dekaf.Internal;

internal static class ReconnectBackoffValidation
{
    public static int ToMilliseconds(TimeSpan value, string parameterName, string errorMessage)
    {
        if (value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(parameterName, errorMessage);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.TotalMilliseconds, int.MaxValue, parameterName);

        return (int)value.TotalMilliseconds;
    }

    public static void ValidateMilliseconds(int reconnectBackoffMs, int reconnectBackoffMaxMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reconnectBackoffMs);
        ArgumentOutOfRangeException.ThrowIfNegative(reconnectBackoffMaxMs);
        if (reconnectBackoffMaxMs < reconnectBackoffMs)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reconnectBackoffMaxMs),
                "Maximum reconnect backoff must be greater than or equal to reconnect backoff");
        }
    }
}
