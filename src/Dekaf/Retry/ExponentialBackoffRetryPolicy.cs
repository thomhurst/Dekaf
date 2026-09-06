using System.Numerics;

namespace Dekaf.Retry;

/// <summary>
/// Retry policy with exponential backoff.
/// Delay formula: <c>min(baseDelay * 2^(attempt-1), maxDelay)</c> with optional random jitter.
/// </summary>
public sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly int _maxAttempts;
    private int _saturationExponent;

    /// <summary>
    /// The nonnegative base delay before the first retry. Zero disables the delay.
    /// </summary>
    public required TimeSpan BaseDelay
    {
        get => _baseDelay;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value.Ticks, nameof(BaseDelay));
            _baseDelay = value;
            UpdateSaturation();
        }
    }

    /// <summary>
    /// The nonnegative maximum delay between retries. Zero disables the delay.
    /// </summary>
    public required TimeSpan MaxDelay
    {
        get => _maxDelay;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value.Ticks, nameof(MaxDelay));
            _maxDelay = value;
            UpdateSaturation();
        }
    }

    /// <summary>
    /// The nonnegative maximum number of retry attempts. Zero disables retries.
    /// </summary>
    public required int MaxAttempts
    {
        get => _maxAttempts;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(MaxAttempts));
            _maxAttempts = value;
        }
    }

    /// <summary>
    /// Whether to add random jitter (0.5x to 1.5x) to the computed delay. Default is <c>true</c>.
    /// The final delay is always clamped to <see cref="MaxDelay"/>, so jitter cannot exceed it.
    /// </summary>
    public bool Jitter { get; init; } = true;

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attemptNumber"/> is less than one.</exception>
    public TimeSpan? GetNextDelay(int attemptNumber, Exception exception)
    {
        // One unsigned range check handles both exhausted and nonpositive attempts.
        var exponent = unchecked(attemptNumber - 1);
        if ((uint)exponent >= (uint)MaxAttempts)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(attemptNumber, 1);
            return null;
        }

        // Below the threshold, positive ticks cannot overflow; shifting zero is always safe.
        var maxTicks = MaxDelay.Ticks;
        var delayTicks = exponent >= _saturationExponent ? maxTicks : BaseDelay.Ticks << exponent;

        if (Jitter)
        {
            // Jitter range: 0.5x to 1.5x of computed delay
            var jitterMultiplier = 0.5 + Random.Shared.NextDouble();
            // The product is below 1.5 * long.MaxValue, which fits in ulong even
            // when it cannot fit in long. Clamp before converting back to signed ticks.
            var jitteredTicks = (ulong)(delayTicks * jitterMultiplier);

            delayTicks = jitteredTicks > (ulong)maxTicks ? maxTicks : (long)jitteredTicks;
        }

        return TimeSpan.FromTicks(delayTicks);
    }

    private void UpdateSaturation()
    {
        var baseTicks = BaseDelay.Ticks;
        var maxTicks = MaxDelay.Ticks;
        // Recompute from either init setter so property assignment order is irrelevant.
        // For positive delays below the cap, this is the first exponent whose product
        // reaches MaxDelay. The division and logarithm happen only during initialization.
        if (baseTicks == 0)
            _saturationExponent = int.MaxValue;
        else if (baseTicks >= maxTicks)
            _saturationExponent = 0;
        else
            _saturationExponent = BitOperations.Log2((ulong)((maxTicks - 1) / baseTicks)) + 1;
    }
}
