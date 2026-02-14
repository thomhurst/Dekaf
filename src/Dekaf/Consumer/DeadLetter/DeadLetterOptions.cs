using Dekaf;

namespace Dekaf.Consumer.DeadLetter;

/// <summary>
/// Configuration options for dead letter queue routing.
/// </summary>
public sealed class DeadLetterOptions
{
    /// <summary>
    /// Topic name suffix appended to the source topic to form the DLQ topic name.
    /// Default: ".DLQ"
    /// </summary>
    public string TopicSuffix { get; init; } = ".DLQ";

    /// <summary>
    /// Number of processing failures required before routing a message to the DLQ.
    /// Default: 1 (dead-letter on first failure).
    /// </summary>
    public int MaxFailures { get; init; } = 1;

    /// <summary>
    /// Whether to include exception details (message, type) in DLQ message headers.
    /// Default: true.
    /// </summary>
    public bool IncludeExceptionInHeaders { get; init; } = true;

    /// <summary>
    /// Whether to await DLQ produce acknowledgment (guaranteed delivery) or use fire-and-forget.
    /// Default: false (fire-and-forget).
    /// </summary>
    public bool AwaitDelivery { get; init; } = false;

    /// <summary>
    /// Bootstrap servers for the DLQ producer. If null, must be configured via <see cref="ConfigureProducer"/>.
    /// </summary>
    public string? BootstrapServers { get; init; }

    /// <summary>
    /// Optional configuration overrides for the internal DLQ producer.
    /// The producer uses byte[] serializers by default.
    /// </summary>
    public Action<ProducerBuilder<byte[]?, byte[]?>>? ConfigureProducer { get; init; }
}
