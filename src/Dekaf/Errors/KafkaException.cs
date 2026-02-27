using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Errors;

/// <summary>
/// Base exception for all Kafka-related errors.
/// </summary>
public class KafkaException : Exception
{
    /// <summary>
    /// Creates a new KafkaException.
    /// </summary>
    public KafkaException() : base()
    {
    }

    /// <summary>
    /// Creates a new KafkaException.
    /// </summary>
    public KafkaException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new KafkaException with an error code.
    /// </summary>
    public KafkaException(ErrorCode errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates a new KafkaException with an inner exception.
    /// </summary>
    public KafkaException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new KafkaException with an error code and inner exception.
    /// </summary>
    public KafkaException(ErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// The Kafka error code, if available.
    /// </summary>
    public ErrorCode? ErrorCode { get; }

    /// <summary>
    /// Whether this exception is retriable.
    /// </summary>
    public bool IsRetriable => ErrorCode?.IsRetriable() ?? false;
}

/// <summary>
/// Identifies the kind of timeout that occurred.
/// </summary>
public enum TimeoutKind
{
    /// <summary>
    /// The timeout kind was not specified. Used by basic constructors that don't provide timeout context.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// A metadata fetch operation timed out (e.g., initial metadata, topic metadata refresh).
    /// </summary>
    Metadata,

    /// <summary>
    /// Message delivery timed out after exhausting retries within the delivery timeout window.
    /// </summary>
    Delivery,

    /// <summary>
    /// A connection attempt to a broker timed out.
    /// Reserved for future use by the networking layer.
    /// </summary>
    Connection,

    /// <summary>
    /// Reserved for future use: poll operations that exceed configured timeout.
    /// </summary>
    Poll,

    /// <summary>
    /// A flush operation timed out waiting for in-flight messages to be delivered.
    /// </summary>
    Flush,

    /// <summary>
    /// A transaction operation timed out.
    /// Reserved for future use by the transaction coordinator.
    /// </summary>
    Transaction,

    /// <summary>
    /// A produce call timed out waiting for buffer memory to become available (max.block.ms exceeded).
    /// This occurs when the producer is generating messages faster than they can be sent,
    /// causing <see cref="Producer.ProducerOptions.BufferMemory"/> to be exhausted.
    /// </summary>
    MaxBlock,

    /// <summary>
    /// A consumer group rebalance operation timed out waiting to join or sync with the group.
    /// </summary>
    Rebalance,
}

/// <summary>
/// Exception thrown when a Kafka operation times out.
/// Provides structured context about the kind of timeout, how long elapsed,
/// and what the configured timeout was.
/// </summary>
public sealed class KafkaTimeoutException : KafkaException
{
    /// <summary>
    /// Creates a new KafkaTimeoutException.
    /// </summary>
    public KafkaTimeoutException() : base()
    {
    }

    /// <summary>
    /// Creates a new KafkaTimeoutException with a message.
    /// </summary>
    public KafkaTimeoutException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new KafkaTimeoutException with a message and inner exception.
    /// </summary>
    public KafkaTimeoutException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new KafkaTimeoutException with timeout context.
    /// </summary>
    /// <param name="timeoutKind">The kind of timeout that occurred.</param>
    /// <param name="elapsed">The time that elapsed before the timeout.</param>
    /// <param name="configured">The configured timeout duration.</param>
    /// <param name="message">A descriptive error message.</param>
    public KafkaTimeoutException(TimeoutKind timeoutKind, TimeSpan elapsed, TimeSpan configured, string message)
        : base(message)
    {
        TimeoutKind = timeoutKind;
        Elapsed = elapsed;
        Configured = configured;
    }

    /// <summary>
    /// Creates a new KafkaTimeoutException with timeout context and an inner exception.
    /// </summary>
    /// <param name="timeoutKind">The kind of timeout that occurred.</param>
    /// <param name="elapsed">The time that elapsed before the timeout.</param>
    /// <param name="configured">The configured timeout duration.</param>
    /// <param name="message">A descriptive error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public KafkaTimeoutException(TimeoutKind timeoutKind, TimeSpan elapsed, TimeSpan configured, string message,
        Exception innerException) : base(message, innerException)
    {
        TimeoutKind = timeoutKind;
        Elapsed = elapsed;
        Configured = configured;
    }

    /// <summary>
    /// The kind of timeout that occurred.
    /// </summary>
    public TimeoutKind TimeoutKind { get; init; }

    /// <summary>
    /// The amount of time that elapsed before the timeout.
    /// </summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    /// The configured timeout duration that was exceeded.
    /// </summary>
    public TimeSpan Configured { get; init; }
}

/// <summary>
/// Exception thrown when a produce operation fails.
/// </summary>
public sealed class ProduceException : KafkaException
{
    public ProduceException() : base()
    {
    }

    public ProduceException(string message) : base(message)
    {
    }

    public ProduceException(ErrorCode errorCode, string message) : base(errorCode, message)
    {
    }

    public ProduceException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// The topic the produce was for.
    /// </summary>
    public string? Topic { get; init; }

    /// <summary>
    /// The partition the produce was for.
    /// </summary>
    public int? Partition { get; init; }
}

/// <summary>
/// Exception thrown when a consume operation fails.
/// </summary>
public sealed class ConsumeException : KafkaException
{
    public ConsumeException() : base()
    {
    }

    public ConsumeException(string message) : base(message)
    {
    }

    public ConsumeException(ErrorCode errorCode, string message) : base(errorCode, message)
    {
    }

    public ConsumeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a group operation fails.
/// </summary>
public sealed class GroupException : KafkaException
{
    public GroupException() : base()
    {
    }

    public GroupException(string message) : base(message)
    {
    }

    public GroupException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public GroupException(ErrorCode errorCode, string message) : base(errorCode, message)
    {
    }

    /// <summary>
    /// The group ID.
    /// </summary>
    public string? GroupId { get; init; }
}

/// <summary>
/// Exception thrown when a transaction operation fails.
/// </summary>
public sealed class TransactionException : KafkaException
{
    public TransactionException() : base()
    {
    }

    public TransactionException(string message) : base(message)
    {
    }

    public TransactionException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public TransactionException(ErrorCode errorCode, string message) : base(errorCode, message)
    {
    }

    /// <summary>
    /// The transactional ID.
    /// </summary>
    public string? TransactionalId { get; init; }
}

/// <summary>
/// Exception thrown when authentication fails.
/// </summary>
public sealed class AuthenticationException : KafkaException
{
    public AuthenticationException() : base()
    {
    }

    public AuthenticationException(string message) : base(message)
    {
    }

    public AuthenticationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public AuthenticationException(ErrorCode errorCode, string message) : base(errorCode, message)
    {
    }
}

/// <summary>
/// Exception thrown when authorization fails.
/// </summary>
public sealed class AuthorizationException : KafkaException
{
    public AuthorizationException() : base()
    {
    }

    public AuthorizationException(string message) : base(message)
    {
    }

    public AuthorizationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public AuthorizationException(ErrorCode errorCode, string message) : base(errorCode, message)
    {
    }

    /// <summary>
    /// The operation that was denied.
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    /// The resource that was denied.
    /// </summary>
    public string? Resource { get; init; }
}

/// <summary>
/// Exception thrown when serialization or deserialization fails.
/// </summary>
public sealed class SerializationException : KafkaException
{
    public SerializationException() : base()
    {
    }

    public SerializationException(string message) : base(message)
    {
    }

    public SerializationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a serialization exception with context about what was being serialized.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    /// <param name="topic">The topic being produced to or consumed from.</param>
    /// <param name="component">The component (key or value) that failed to serialize.</param>
    public SerializationException(string message, Exception innerException, string? topic, SerializationComponent component)
        : base(message, innerException)
    {
        Topic = topic;
        Component = component;
    }

    /// <summary>
    /// The topic involved in the serialization operation.
    /// </summary>
    public string? Topic { get; init; }

    /// <summary>
    /// The component (key or value) that failed to serialize.
    /// </summary>
    public SerializationComponent Component { get; init; }
}
