namespace Dekaf.Resilience;

/// <summary>
/// Configuration options for the circuit breaker.
/// </summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>
    /// The number of consecutive failures required to trip the circuit breaker
    /// from <see cref="CircuitBreakerState.Closed"/> to <see cref="CircuitBreakerState.Open"/>.
    /// Default is 5.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// The duration the circuit remains open before transitioning to
    /// <see cref="CircuitBreakerState.HalfOpen"/> to allow probe requests.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The number of successful probe requests required in the
    /// <see cref="CircuitBreakerState.HalfOpen"/> state before the circuit
    /// transitions back to <see cref="CircuitBreakerState.Closed"/>.
    /// Default is 2.
    /// </summary>
    public int HalfOpenMaxAttempts { get; set; } = 2;

    /// <summary>
    /// Validates the options and throws if any values are invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option is out of range.</exception>
    internal void Validate()
    {
        if (FailureThreshold < 1)
            throw new ArgumentOutOfRangeException(nameof(FailureThreshold), FailureThreshold, "Failure threshold must be at least 1");

        if (BreakDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(BreakDuration), BreakDuration, "Break duration must be positive");

        if (HalfOpenMaxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(HalfOpenMaxAttempts), HalfOpenMaxAttempts, "Half-open max attempts must be at least 1");
    }
}
