using System.Diagnostics;
using Dekaf.Metadata;
using Dekaf.Protocol.Records;
using Dekaf.Retry;
using Dekaf.Security;
using Dekaf.Security.Sasl;

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
    /// Default is 0 (immediate send), matching the Java Kafka producer default.
    /// Use <see cref="ProducerBuilder{TKey,TValue}.ForHighThroughput"/> to set 100ms
    /// for throughput-oriented workloads.
    /// </summary>
    public int LingerMs { get; init; }

    /// <summary>
    /// Maximum batch size in bytes.
    /// Larger batches improve throughput by reducing batch rotation overhead.
    /// Default is 1MB which handles high-throughput scenarios efficiently.
    /// </summary>
    public int BatchSize { get; init; } = 1048576;

    /// <summary>
    /// Total memory buffer size in bytes for pending messages.
    /// When the buffer is full, Send() and ProduceAsync() will block until space is available.
    /// Default is 1GB, matching librdkafka's default (queue.buffering.max.kbytes).
    /// </summary>
    public ulong BufferMemory { get; init; } = 1073741824;

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
    /// For idempotent producers (the default), values greater than 1 allow multiple batches
    /// per partition to be in-flight simultaneously, improving throughput. Ordering is
    /// guaranteed by Kafka's idempotent sequence numbers. Default is 5.
    /// </summary>
    public int MaxInFlightRequestsPerConnection { get; init; } = 5;

    /// <summary>
    /// Number of connections to maintain per broker for parallel request handling.
    /// When set greater than 1 for non-idempotent producers, connections are selected via round-robin
    /// to distribute load across TCP streams, improving throughput by enabling parallel sends.
    /// <para>
    /// For idempotent producers (<see cref="EnableIdempotence"/> = true), this must be 1
    /// because sequence number ordering requires all produce requests to be pipelined on
    /// the same TCP connection. Setting this greater than 1 with idempotence enabled will
    /// throw an <see cref="InvalidOperationException"/> at build time.
    /// </para>
    /// Default is 1.
    /// </summary>
    public int ConnectionsPerBroker { get; init; } = 1;

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
    /// <see cref="DeliveryTimeoutMs"/> converted to <see cref="Stopwatch"/> ticks.
    /// Cached on first access to avoid repeated floating-point multiply.
    /// </summary>
    private long _deliveryTimeoutTicks;
    internal long DeliveryTimeoutTicks =>
        _deliveryTimeoutTicks != 0 ? _deliveryTimeoutTicks
        : (_deliveryTimeoutTicks = (long)(DeliveryTimeoutMs * (Stopwatch.Frequency / 1000.0)));

    /// <summary>
    /// <see cref="RequestTimeoutMs"/> converted to <see cref="Stopwatch"/> ticks.
    /// Cached on first access to avoid repeated floating-point multiply.
    /// </summary>
    private long _requestTimeoutTicks;
    internal long RequestTimeoutTicks =>
        _requestTimeoutTicks != 0 ? _requestTimeoutTicks
        : (_requestTimeoutTicks = (long)(RequestTimeoutMs * (Stopwatch.Frequency / 1000.0)));

    /// <summary>
    /// <see cref="RetryBackoffMs"/> converted to <see cref="Stopwatch"/> ticks.
    /// Cached on first access to avoid repeated floating-point multiply.
    /// </summary>
    private long _retryBackoffTicks;
    internal long RetryBackoffTicks =>
        _retryBackoffTicks != 0 ? _retryBackoffTicks
        : (_retryBackoffTicks = (long)(RetryBackoffMs * (Stopwatch.Frequency / 1000.0)));

    /// <summary>
    /// Enables idempotent producer mode, which ensures that messages are delivered exactly once
    /// to a particular topic partition during the lifetime of a single producer session.
    /// <para>
    /// Idempotence is enabled by default for safety. When enabled, the producer obtains a
    /// producer ID from the broker and assigns sequence numbers to each batch, allowing the
    /// broker to deduplicate retried messages.
    /// </para>
    /// <para>
    /// Disabling idempotence reduces overhead slightly (no <c>InitProducerId</c> call during
    /// initialization, no sequence number tracking) but allows duplicate messages on retry.
    /// </para>
    /// <para>
    /// Cannot be disabled when <see cref="TransactionalId"/> is set, because transactions
    /// require idempotence for correctness.
    /// </para>
    /// </summary>
    public bool EnableIdempotence { get; init; } = true;

    /// <summary>
    /// Transactional ID for exactly-once semantics.
    /// </summary>
    public string? TransactionalId { get; init; }

    /// <summary>
    /// Transaction timeout in milliseconds.
    /// </summary>
    public int TransactionTimeoutMs { get; init; } = 60000;

    /// <summary>
    /// Maximum total time in milliseconds for producer disposal (close).
    /// The budget is split into two phases:
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Graceful phase</b> (first half): flushes pending messages to Kafka and waits
    ///     for the sender to drain remaining batches.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Forceful phase</b> (remaining time): cancels in-flight work and disposes
    ///     broker senders, with sub-phase timeouts derived from the remaining budget so
    ///     the total dispose time does not exceed this value.
    ///   </description></item>
    /// </list>
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
    /// If the timeout expires, a <see cref="Errors.KafkaTimeoutException"/> is thrown with a descriptive message.
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
    /// Custom partitioner instance. When set, this takes precedence over <see cref="Partitioner"/>.
    /// </summary>
    public IPartitioner? CustomPartitioner { get; init; }

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
    /// Maximum size of the internal ValueTaskSource pool.
    /// Higher values reduce allocations in high-throughput scenarios but use more memory.
    /// Default is 0, which auto-calculates from <see cref="BufferMemory"/> / <see cref="BatchSize"/>
    /// to scale with the expected concurrency level. Set an explicit positive value to override.
    /// For the default BufferMemory and BatchSize settings, the auto-calculated value is 65,536 (MaxAutoPoolSize).
    /// </summary>
    public int ValueTaskSourcePoolSize { get; init; }

    /// <summary>
    /// Capacity of the arena buffer for zero-copy serialization in bytes.
    /// Larger arenas reduce fallback allocations when messages are larger than expected.
    /// Default is 0, which uses BatchSize + 12.5% overflow margin as the arena capacity.
    /// The margin provides headroom so that messages appended near the end of a batch
    /// can still be serialized in the arena instead of falling back to ArrayPool.
    /// For workloads with very large messages (approaching BatchSize), consider setting
    /// this to BatchSize * 2 to reduce slow-path fallbacks.
    /// </summary>
    public int ArenaCapacity { get; init; } = 0;

    /// <summary>
    /// Initial capacity for record arrays in partition batches.
    /// When 0 (default), automatically computed from <see cref="BatchSize"/> to minimize
    /// array resizing. Set explicitly to override: lower values reduce memory for applications
    /// with many small batches, higher values reduce resizing for high-throughput scenarios.
    /// </summary>
    public int InitialBatchRecordCapacity { get; init; } = 0;

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
    /// Application-level retry policy for <see cref="KafkaProducer{TKey,TValue}.ProduceAsync"/>.
    /// When set, retriable exceptions that escape the internal protocol-level retries will be
    /// retried according to this policy. When <c>null</c>, no application-level retries occur.
    /// </summary>
    public IRetryPolicy? RetryPolicy { get; init; }

    /// <summary>
    /// Enable adaptive connection scaling based on buffer pressure.
    /// When enabled, the producer will automatically increase connections per broker
    /// when sustained buffer backpressure is detected, improving drain throughput.
    /// <para>
    /// Only applies to non-idempotent producers (<see cref="EnableIdempotence"/> = false).
    /// Idempotent producers require partition affinity on a fixed connection count for
    /// sequence number ordering, so adaptive scaling is automatically disabled.
    /// </para>
    /// Default: false.
    /// </summary>
    public bool EnableAdaptiveConnections { get; init; }

    /// <summary>
    /// Maximum connections per broker when adaptive scaling is enabled.
    /// The producer will not scale beyond this limit regardless of buffer pressure.
    /// Must be at least 1. Default: 10.
    /// </summary>
    public int MaxConnectionsPerBroker
    {
        get => _maxConnectionsPerBroker;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxConnectionsPerBroker = value;
        }
    }

    private readonly int _maxConnectionsPerBroker = 10;

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
