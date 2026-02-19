namespace Dekaf.Resilience;

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// The circuit is closed. Requests flow normally.
    /// Failures are counted and may trigger the circuit to open.
    /// </summary>
    Closed,

    /// <summary>
    /// The circuit is open. Requests are immediately rejected
    /// without being sent to the broker.
    /// </summary>
    Open,

    /// <summary>
    /// The circuit is half-open. A limited number of probe requests
    /// are allowed through to determine if the broker has recovered.
    /// </summary>
    HalfOpen
}
