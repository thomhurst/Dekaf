using Dekaf.Compression;
using Dekaf.Protocol.Records;
using Dekaf.Security;
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
    public ulong BufferMemory { get; init; } = 33554432;

    /// <summary>
    /// Compression type.
    /// </summary>
    public CompressionType CompressionType { get; init; } = CompressionType.None;

    /// <summary>
    /// Maximum in-flight requests per connection.
    /// </summary>
    public int MaxInFlightRequestsPerConnection { get; init; } = 5;

    /// <summary>
    /// Number of connections to maintain per broker for parallel request handling.
    /// Higher values can improve throughput by reducing contention on the write lock.
    /// Default is based on processor count (minimum 2).
    /// </summary>
    public int ConnectionsPerBroker { get; init; } = Math.Max(2, Environment.ProcessorCount / 2);

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
    /// Maximum time in milliseconds to wait for pending messages to be delivered during close/dispose.
    /// All pending messages are flushed to Kafka before the producer closes.
    /// Set to 0 for no timeout (wait indefinitely). Default is 30 seconds.
    /// </summary>
    public int CloseTimeoutMs { get; init; } = 30000;

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
    /// TLS configuration for SSL/mTLS connections.
    /// When set, <see cref="UseTls"/> is automatically enabled.
    /// </summary>
    public TlsConfig? TlsConfig { get; init; }

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

    /// <summary>
    /// GSSAPI (Kerberos) configuration. Required when SaslMechanism is Gssapi.
    /// </summary>
    public GssapiConfig? GssapiConfig { get; init; }

    /// <summary>
    /// OAuth bearer token configuration for OAUTHBEARER authentication.
    /// </summary>
    public OAuthBearerConfig? OAuthBearerConfig { get; init; }

    /// <summary>
    /// Custom OAuth bearer token provider function for OAUTHBEARER authentication.
    /// Takes precedence over <see cref="OAuthBearerConfig"/> if both are specified.
    /// </summary>
    public Func<CancellationToken, ValueTask<OAuthBearerToken>>? OAuthBearerTokenProvider { get; init; }

    /// <summary>
    /// Socket send buffer size in bytes. Set to 0 to use system default.
    /// Larger buffers can improve throughput for high-volume producers.
    /// </summary>
    public int SocketSendBufferBytes { get; init; }

    /// <summary>
    /// Socket receive buffer size in bytes. Set to 0 to use system default.
    /// </summary>
    public int SocketReceiveBufferBytes { get; init; }

    /// <summary>
    /// Interval at which statistics events are emitted. Set to null or TimeSpan.Zero to disable.
    /// </summary>
    public TimeSpan? StatisticsInterval { get; init; }

    /// <summary>
    /// Handler for statistics events. Called periodically based on StatisticsInterval.
    /// </summary>
    public Action<Statistics.ProducerStatistics>? StatisticsHandler { get; init; }

    /// <summary>
    /// Maximum size of the internal ValueTaskSource pool.
    /// Higher values reduce allocations in high-throughput scenarios but use more memory.
    /// Default is 4096.
    /// </summary>
    public int ValueTaskSourcePoolSize { get; init; } = ValueTaskSourcePool<RecordMetadata>.DefaultMaxPoolSize;

    /// <summary>
    /// Capacity of the arena buffer for zero-copy serialization in bytes.
    /// Larger arenas reduce fallback allocations when messages are larger than expected.
    /// Default is 64KB which handles most message sizes efficiently.
    /// Set to 0 to use BatchSize as the arena capacity.
    /// </summary>
    public int ArenaCapacity { get; init; } = 65536;

    /// <summary>
    /// Initial capacity for record arrays in partition batches.
    /// Lower values reduce memory for applications with many small batches.
    /// Higher values reduce array resizing for high-throughput scenarios.
    /// Default is 64, balancing memory usage and resize frequency.
    /// </summary>
    public int InitialBatchRecordCapacity { get; init; } = 64;
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
