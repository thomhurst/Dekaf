using Dekaf.Compression;
using Dekaf.Metadata;
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
    /// Larger batches improve throughput by reducing batch rotation overhead.
    /// Default is 1MB which handles high-throughput scenarios efficiently.
    /// </summary>
    public int BatchSize { get; init; } = 1048576;

    /// <summary>
    /// Total memory buffer size in bytes for pending messages.
    /// When the buffer is full, Send() and ProduceAsync() will block until space is available.
    /// Default is 256MB, which handles bursty workloads without excessive memory usage.
    /// </summary>
    public ulong BufferMemory { get; init; } = 268435456;

    /// <summary>
    /// Compression type.
    /// </summary>
    public CompressionType CompressionType { get; init; } = CompressionType.None;

    /// <summary>
    /// Compression level for the configured compression codec.
    /// When null, the codec's default level is used.
    /// Valid ranges depend on the compression type:
    /// - Gzip: 0-9 (0 = no compression, 9 = best compression)
    /// - LZ4: 0-12 (0 = fast, 12 = max compression)
    /// - Zstd: 1-22 (1 = fast, 22 = best compression)
    /// - Snappy: not supported (fixed algorithm, this value is ignored)
    /// </summary>
    public int? CompressionLevel { get; init; }

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
    /// Maximum time in milliseconds that <see cref="KafkaProducer{TKey,TValue}.ProduceAsync"/>
    /// and <see cref="KafkaProducer{TKey,TValue}.Send"/> will block when the producer's buffer
    /// is full or metadata is unavailable.
    /// <para>
    /// This controls how long the producer waits for buffer space (backpressure) and for
    /// initial metadata when producing to a new topic for the first time.
    /// </para>
    /// <para>
    /// If the timeout expires, a <see cref="TimeoutException"/> is thrown with a descriptive message.
    /// </para>
    /// <para>
    /// Equivalent to Kafka's <c>max.block.ms</c> configuration. Default is 60000ms (60 seconds).
    /// </para>
    /// </summary>
    public int MaxBlockMs { get; init; } = 60000;

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
    /// Should be at least as large as BatchSize for optimal performance.
    /// Default is 0, which uses BatchSize as the arena capacity (1 MB default).
    /// This ensures arena buffer memory stays proportional to BufferMemory limits.
    /// For workloads with very large messages (approaching BatchSize), consider setting
    /// this to BatchSize * 2 to reduce slow-path fallbacks.
    /// </summary>
    public int ArenaCapacity { get; init; } = 0;

    /// <summary>
    /// Initial capacity for record arrays in partition batches.
    /// Lower values reduce memory for applications with many small batches.
    /// Higher values reduce array resizing for high-throughput scenarios.
    /// Default is 64, balancing memory usage and resize frequency.
    /// </summary>
    public int InitialBatchRecordCapacity { get; init; } = 64;

    /// <summary>
    /// Strategy for recovering cluster metadata when all known brokers become unavailable.
    /// <see cref="MetadataRecoveryStrategy.Rebootstrap"/> re-resolves bootstrap server DNS
    /// to discover new broker IPs, which is critical in cloud environments where broker IPs
    /// change during rolling upgrades.
    /// Default is <see cref="MetadataRecoveryStrategy.Rebootstrap"/>.
    /// </summary>
    public MetadataRecoveryStrategy MetadataRecoveryStrategy { get; init; } = MetadataRecoveryStrategy.Rebootstrap;

    /// <summary>
    /// How long in milliseconds to wait before triggering a rebootstrap when all known brokers
    /// are unavailable. Only applies when <see cref="MetadataRecoveryStrategy"/> is
    /// <see cref="MetadataRecoveryStrategy.Rebootstrap"/>.
    /// Default is 300000 (5 minutes).
    /// </summary>
    public int MetadataRecoveryRebootstrapTriggerMs { get; init; } = 300000;

    /// <summary>
    /// Producer interceptors, called in order during the produce pipeline.
    /// Interceptors are called on the hot path (OnSend) and on acknowledgement (OnAcknowledgement).
    /// Interceptor exceptions are caught and logged, not propagated.
    /// </summary>
    internal IReadOnlyList<object>? Interceptors { get; init; }
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
