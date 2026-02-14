namespace Dekaf.Retry;

/// <summary>
/// Retry policy with a fixed delay between retries.
/// </summary>
public sealed class FixedDelayRetryPolicy : IRetryPolicy
{
    /// <summary>
    /// The constant delay between retries.
    /// </summary>
    public required TimeSpan Delay { get; init; }

    /// <summary>
    /// The maximum number of retry attempts. After this many failures, retrying stops.
    /// </summary>
    public required int MaxAttempts { get; init; }

    /// <inheritdoc />
    public TimeSpan? GetNextDelay(int attemptNumber, Exception exception)
    {
        if (attemptNumber > MaxAttempts)
            return null;

        return Delay;
    }
}
