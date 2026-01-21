using Dekaf.Compression;
using Dekaf.Protocol.Records;
using Dekaf.Security.Sasl;
using Dekaf.Serialization;

namespace Dekaf.Producer;

/// <summary>
/// Configuration options for the Kafka producer.
/// </summary>
public sealed class ProducerOptions
{
    /// <summary>
    /// Bootstrap servers (host:port,host:port).
    /// </summary>
    public required IReadOnlyList<string> BootstrapServers { get; init; }

    /// <summary>
    /// Client ID.
    /// </summary>
    public string? ClientId { get; init; } = "dekaf-producer";

    /// <summary>
    /// Acknowledgment mode.
    /// </summary>
    public Acks Acks { get; init; } = Acks.All;

    /// <summary>
    /// Request timeout in milliseconds.
    /// </summary>
    public int RequestTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Linger time in milliseconds before sending a batch.
    /// </summary>
    public int LingerMs { get; init; } = 0;

    /// <summary>
    /// Maximum batch size in bytes.
    /// </summary>
    public int BatchSize { get; init; } = 16384;

    /// <summary>
    /// Total memory buffer size in bytes.
    /// </summary>
    public long BufferMemory { get; init; } = 33554432;

    /// <summary>
    /// Compression type.
    /// </summary>
    public CompressionType CompressionType { get; init; } = CompressionType.None;

    /// <summary>
    /// Maximum in-flight requests per connection.
    /// </summary>
    public int MaxInFlightRequestsPerConnection { get; init; } = 5;

    /// <summary>
    /// Number of retries.
    /// </summary>
    public int Retries { get; init; } = int.MaxValue;

    /// <summary>
    /// Retry backoff in milliseconds.
    /// </summary>
    public int RetryBackoffMs { get; init; } = 100;

    /// <summary>
    /// Maximum retry backoff in milliseconds.
    /// </summary>
    public int RetryBackoffMaxMs { get; init; } = 1000;

    /// <summary>
    /// Delivery timeout in milliseconds.
    /// </summary>
    public int DeliveryTimeoutMs { get; init; } = 120000;

    /// <summary>
    /// Enable idempotent producer.
    /// </summary>
    public bool EnableIdempotence { get; init; }

    /// <summary>
    /// Transactional ID for exactly-once semantics.
    /// </summary>
    public string? TransactionalId { get; init; }

    /// <summary>
    /// Transaction timeout in milliseconds.
    /// </summary>
    public int TransactionTimeoutMs { get; init; } = 60000;

    /// <summary>
    /// Maximum request size in bytes.
    /// </summary>
    public int MaxRequestSize { get; init; } = 1048576;

    /// <summary>
    /// Partitioner type.
    /// </summary>
    public PartitionerType Partitioner { get; init; } = PartitionerType.Default;

    /// <summary>
    /// Use TLS.
    /// </summary>
    public bool UseTls { get; init; }

    /// <summary>
    /// SASL authentication mechanism.
    /// </summary>
    public SaslMechanism SaslMechanism { get; init; } = SaslMechanism.None;

    /// <summary>
    /// SASL username for PLAIN and SCRAM authentication.
    /// </summary>
    public string? SaslUsername { get; init; }

    /// <summary>
    /// SASL password for PLAIN and SCRAM authentication.
    /// </summary>
    public string? SaslPassword { get; init; }
}

/// <summary>
/// Acknowledgment modes.
/// </summary>
public enum Acks
{
    /// <summary>
    /// No acknowledgment (fire and forget).
    /// </summary>
    None = 0,

    /// <summary>
    /// Leader acknowledgment only.
    /// </summary>
    Leader = 1,

    /// <summary>
    /// All in-sync replicas must acknowledge.
    /// </summary>
    All = -1
}

/// <summary>
/// Partitioner types.
/// </summary>
public enum PartitionerType
{
    /// <summary>
    /// Default partitioner (hash key or round-robin).
    /// </summary>
    Default,

    /// <summary>
    /// Sticky partitioner (sticks to partition for null keys).
    /// </summary>
    Sticky,

    /// <summary>
    /// Round-robin partitioner.
    /// </summary>
    RoundRobin
}
