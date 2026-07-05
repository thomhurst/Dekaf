namespace Dekaf.Internal;

internal static class ReconnectBackoffValidation
{
    public static int ToMilliseconds(TimeSpan value, string parameterName, string errorMessage)
    {
        if (value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(parameterName, errorMessage);
        CompatibilityThrowHelpers.ThrowIfGreaterThan(value.TotalMilliseconds, int.MaxValue, parameterName);

        return (int)value.TotalMilliseconds;
    }

    public static void ValidateMilliseconds(int reconnectBackoffMs, int reconnectBackoffMaxMs)
    {
        CompatibilityThrowHelpers.ThrowIfNegative(reconnectBackoffMs);
        CompatibilityThrowHelpers.ThrowIfNegative(reconnectBackoffMaxMs);
        if (reconnectBackoffMaxMs < reconnectBackoffMs)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reconnectBackoffMaxMs),
                "Maximum reconnect backoff must be greater than or equal to reconnect backoff");
        }
    }
}
