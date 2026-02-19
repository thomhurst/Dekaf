namespace Dekaf.Resilience;

/// <summary>
/// Exception thrown when an operation is rejected because the circuit breaker is open.
/// </summary>
public sealed class CircuitBreakerOpenException : KafkaException
{
    /// <summary>
    /// Creates a new <see cref="CircuitBreakerOpenException"/>.
    /// </summary>
    public CircuitBreakerOpenException()
        : base("Circuit breaker is open; the broker is unavailable")
    {
    }

    /// <summary>
    /// Creates a new <see cref="CircuitBreakerOpenException"/> with broker context.
    /// </summary>
    /// <param name="brokerId">The broker ID that is unavailable.</param>
    public CircuitBreakerOpenException(int brokerId)
        : base($"Circuit breaker is open for broker {brokerId}; the broker is unavailable")
    {
        BrokerId = brokerId;
    }

    /// <summary>
    /// Creates a new <see cref="CircuitBreakerOpenException"/> with a custom message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CircuitBreakerOpenException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new <see cref="CircuitBreakerOpenException"/> with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CircuitBreakerOpenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// The broker ID that the circuit breaker is open for, if known.
    /// </summary>
    public int? BrokerId { get; init; }
}
