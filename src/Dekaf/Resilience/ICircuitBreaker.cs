namespace Dekaf.Resilience;

/// <summary>
/// Defines a circuit breaker for protecting against repeated failures to a Kafka broker.
/// </summary>
/// <remarks>
/// <para>
/// The circuit breaker pattern prevents a client from repeatedly attempting operations
/// that are likely to fail, giving the broker time to recover. It tracks the health of
/// each broker connection and transitions through three states:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="CircuitBreakerState.Closed"/>: Normal operation. Failures are counted.</description></item>
/// <item><description><see cref="CircuitBreakerState.Open"/>: Requests are rejected immediately.</description></item>
/// <item><description><see cref="CircuitBreakerState.HalfOpen"/>: Limited probe requests are allowed to test recovery.</description></item>
/// </list>
/// <para>
/// Implementations must be thread-safe, as circuit breakers are accessed concurrently
/// from producer and consumer threads.
/// </para>
/// </remarks>
public interface ICircuitBreaker
{
    /// <summary>
    /// Gets the current state of the circuit breaker.
    /// </summary>
    CircuitBreakerState State { get; }

    /// <summary>
    /// Gets whether the circuit is currently allowing requests.
    /// Returns <c>true</c> when the circuit is <see cref="CircuitBreakerState.Closed"/>
    /// or when a probe request is permitted in <see cref="CircuitBreakerState.HalfOpen"/> state.
    /// </summary>
    bool IsAllowingRequests { get; }

    /// <summary>
    /// Records a successful operation. If the circuit is half-open and enough
    /// successes have been recorded, the circuit transitions back to closed.
    /// </summary>
    void RecordSuccess();

    /// <summary>
    /// Records a failed operation. If the failure count exceeds the threshold,
    /// the circuit transitions to open.
    /// </summary>
    void RecordFailure();

    /// <summary>
    /// Resets the circuit breaker to the closed state, clearing all failure counts.
    /// </summary>
    void Reset();

    /// <summary>
    /// Gets the number of consecutive failures recorded since the last success or reset.
    /// </summary>
    int ConsecutiveFailures { get; }

    /// <summary>
    /// Raised when the circuit breaker state changes.
    /// </summary>
    event Action<CircuitBreakerState>? StateChanged;
}
