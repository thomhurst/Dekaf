namespace Dekaf.Retry;

/// <summary>
/// Retry policy with exponential backoff.
/// Delay formula: <c>min(baseDelay * 2^(attempt-1), maxDelay)</c> with optional random jitter.
/// </summary>
public sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    /// <summary>
    /// The base delay before the first retry.
    /// </summary>
    public required TimeSpan BaseDelay { get; init; }

    /// <summary>
    /// The maximum delay between retries.
    /// </summary>
    public required TimeSpan MaxDelay { get; init; }

    /// <summary>
    /// The maximum number of retry attempts. After this many failures, retrying stops.
    /// </summary>
    public required int MaxAttempts { get; init; }

    /// <summary>
    /// Whether to add random jitter (0.5x to 1.5x) to the computed delay. Default is <c>true</c>.
    /// The final delay is always clamped to <see cref="MaxDelay"/>, so jitter cannot exceed it.
    /// </summary>
    public bool Jitter { get; init; } = true;

    /// <inheritdoc />
    public TimeSpan? GetNextDelay(int attemptNumber, Exception exception)
    {
        if (attemptNumber > MaxAttempts)
            return null;

        var delayTicks = BaseDelay.Ticks * (1L << (attemptNumber - 1));

        // Clamp to MaxDelay (also handles overflow: if shift overflows to negative, clamp)
        if (delayTicks <= 0 || delayTicks > MaxDelay.Ticks)
            delayTicks = MaxDelay.Ticks;

        if (Jitter)
        {
            // Jitter range: 0.5x to 1.5x of computed delay
            var jitterMultiplier = 0.5 + Random.Shared.NextDouble();
            delayTicks = (long)(delayTicks * jitterMultiplier);

            // Clamp again so jitter cannot exceed MaxDelay
            if (delayTicks > MaxDelay.Ticks)
                delayTicks = MaxDelay.Ticks;
        }

        return TimeSpan.FromTicks(delayTicks);
    }
}
