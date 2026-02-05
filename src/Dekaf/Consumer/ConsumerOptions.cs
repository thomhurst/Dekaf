using Dekaf.Metadata;
using Dekaf.Protocol.Messages;
using Dekaf.Security;
using Dekaf.Security.Sasl;

namespace Dekaf.Consumer;

/// <summary>
/// Specifies how consumer offsets are committed.
/// </summary>
public enum OffsetCommitMode
{
    /// <summary>
    /// Offsets are automatically committed periodically in the background.
    /// This matches Kafka's enable.auto.commit=true behavior.
    /// </summary>
    Auto,

    /// <summary>
    /// You must call CommitAsync() explicitly to commit offsets.
    /// This matches Kafka's enable.auto.commit=false behavior.
    /// Use for at-least-once processing where you commit after processing.
    /// </summary>
    Manual
}

/// <summary>
/// Configuration options for the Kafka consumer.
/// </summary>
public sealed class ConsumerOptions
{
    /// <summary>
    /// Bootstrap servers (host:port,host:port).
    /// </summary>
    public required IReadOnlyList<string> BootstrapServers { get; init; }

    /// <summary>
    /// Client ID.
    /// </summary>
    public string? ClientId { get; init; } = "dekaf-consumer";

    /// <summary>
    /// Consumer group ID.
    /// </summary>
    public string? GroupId { get; init; }

    /// <summary>
    /// Group instance ID for static membership.
    /// </summary>
    public string? GroupInstanceId { get; init; }

    /// <summary>
    /// The consumer group protocol to use for group coordination.
    /// Default is <see cref="GroupProtocol.Classic"/> for backward compatibility.
    /// Set to <see cref="GroupProtocol.Consumer"/> to use the KIP-848 protocol (Kafka 4.0+).
    /// </summary>
    public GroupProtocol GroupProtocol { get; init; } = GroupProtocol.Classic;

    /// <summary>
    /// The server-side partition assignor to use when <see cref="GroupProtocol"/> is
    /// <see cref="GroupProtocol.Consumer"/>. Common values are "uniform" and "range".
    /// When null, the broker uses its default assignor.
    /// This setting is only applicable with the Consumer group protocol (KIP-848).
    /// </summary>
    public string? GroupRemoteAssignor { get; init; }

    /// <summary>
    /// Offset commit mode controlling how offsets are stored and committed.
    /// Default is <see cref="OffsetCommitMode.Auto"/>.
    /// </summary>
    public OffsetCommitMode OffsetCommitMode { get; init; } = OffsetCommitMode.Auto;

    /// <summary>
    /// Auto-commit interval in milliseconds.
    /// </summary>
    public int AutoCommitIntervalMs { get; init; } = 5000;

    /// <summary>
    /// Auto offset reset behavior.
    /// </summary>
    public AutoOffsetReset AutoOffsetReset { get; init; } = AutoOffsetReset.Latest;

    /// <summary>
    /// Fetch minimum bytes.
    /// </summary>
    public int FetchMinBytes { get; init; } = 1;

    /// <summary>
    /// Fetch maximum bytes.
    /// </summary>
    public int FetchMaxBytes { get; init; } = 52428800;

    /// <summary>
    /// Maximum bytes per partition.
    /// </summary>
    public int MaxPartitionFetchBytes { get; init; } = 1048576;

    /// <summary>
    /// Fetch maximum wait time in milliseconds.
    /// </summary>
    public int FetchMaxWaitMs { get; init; } = 500;

    /// <summary>
    /// Maximum poll records.
    /// </summary>
    public int MaxPollRecords { get; init; } = 500;

    /// <summary>
    /// Maximum poll interval in milliseconds.
    /// </summary>
    public int MaxPollIntervalMs { get; init; } = 300000;

    /// <summary>
    /// Session timeout in milliseconds.
    /// </summary>
    public int SessionTimeoutMs { get; init; } = 45000;

    /// <summary>
    /// Heartbeat interval in milliseconds.
    /// </summary>
    public int HeartbeatIntervalMs { get; init; } = 3000;

    /// <summary>
    /// Rebalance timeout in milliseconds.
    /// </summary>
    public int RebalanceTimeoutMs { get; init; } = 60000;

    /// <summary>
    /// Partition assignment strategy.
    /// </summary>
    public PartitionAssignmentStrategy PartitionAssignmentStrategy { get; init; } =
        PartitionAssignmentStrategy.CooperativeSticky;

    /// <summary>
    /// Isolation level for transactional reads.
    /// </summary>
    public IsolationLevel IsolationLevel { get; init; } = IsolationLevel.ReadUncommitted;

    /// <summary>
    /// Request timeout in milliseconds.
    /// </summary>
    public int RequestTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Check CRCs.
    /// </summary>
    public bool CheckCrcs { get; init; }

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
    public Security.Sasl.GssapiConfig? GssapiConfig { get; init; }

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
    /// Rebalance listener.
    /// </summary>
    public IRebalanceListener? RebalanceListener { get; init; }

    /// <summary>
    /// Socket send buffer size in bytes. Set to 0 to use system default.
    /// </summary>
    public int SocketSendBufferBytes { get; init; }

    /// <summary>
    /// Socket receive buffer size in bytes. Set to 0 to use system default.
    /// Larger buffers can improve throughput for high-volume consumers.
    /// </summary>
    public int SocketReceiveBufferBytes { get; init; }

    /// <summary>
    /// Minimum number of messages to prefetch per partition.
    /// The consumer will attempt to keep at least this many messages buffered.
    /// Set to 1 to disable prefetching (fetch on demand). Default is 100000.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When prefetching is enabled (value > 1), the consumer fetches messages in the background
    /// to reduce latency when calling <c>ConsumeAsync</c>. This significantly improves throughput
    /// but increases memory usage.
    /// </para>
    /// <para>
    /// Memory usage is bounded by <see cref="QueuedMaxMessagesKbytes"/> (default: 64 MB).
    /// To reduce memory usage, either lower this value or set <c>QueuedMinMessages</c> to 1.
    /// </para>
    /// <para>
    /// <b>Breaking change:</b> Prior versions defaulted to 1 (prefetching disabled).
    /// The new default of 100000 matches Confluent's <c>queued.min.messages</c> default.
    /// </para>
    /// </remarks>
    public int QueuedMinMessages { get; init; } = 100000;

    /// <summary>
    /// Maximum total size of prefetched messages in kilobytes.
    /// Limits memory usage when prefetching is enabled. Default is 65536 KB (64 MB).
    /// </summary>
    public int QueuedMaxMessagesKbytes { get; init; } = 65536;

    /// <summary>
    /// Enable partition end-of-file (EOF) events.
    /// When enabled, the consumer will emit a special ConsumeResult with IsPartitionEof=true
    /// when it reaches the end of a partition (caught up to the high watermark).
    /// The EOF event fires once per "catch up" - it will fire again after new messages
    /// arrive and are consumed. Default is false.
    /// </summary>
    public bool EnablePartitionEof { get; init; }

    /// <summary>
    /// Interval at which statistics events are emitted. Set to null or TimeSpan.Zero to disable.
    /// </summary>
    public TimeSpan? StatisticsInterval { get; init; }

    /// <summary>
    /// Handler for statistics events. Called periodically based on StatisticsInterval.
    /// </summary>
    public Action<Statistics.ConsumerStatistics>? StatisticsHandler { get; init; }

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
}

/// <summary>
/// Specifies the consumer group protocol to use for group coordination.
/// </summary>
public enum GroupProtocol
{
    /// <summary>
    /// Classic consumer group protocol using JoinGroup/SyncGroup/Heartbeat APIs.
    /// This is the traditional protocol used in Kafka versions prior to 4.0.
    /// Partition assignment is performed client-side by the group leader.
    /// </summary>
    Classic,

    /// <summary>
    /// New consumer group protocol introduced in KIP-848 (Kafka 4.0+).
    /// Uses the ConsumerGroupHeartbeat API for group coordination.
    /// Partition assignment is performed server-side by the group coordinator,
    /// providing up to 20x faster rebalancing.
    /// </summary>
    Consumer
}

/// <summary>
/// Auto offset reset behavior.
/// </summary>
public enum AutoOffsetReset
{
    /// <summary>
    /// Start from earliest available offset.
    /// </summary>
    Earliest,

    /// <summary>
    /// Start from latest offset.
    /// </summary>
    Latest,

    /// <summary>
    /// Throw exception if no offset is found.
    /// </summary>
    None
}

/// <summary>
/// Partition assignment strategies.
/// </summary>
public enum PartitionAssignmentStrategy
{
    /// <summary>
    /// Range assignor.
    /// </summary>
    Range,

    /// <summary>
    /// Round-robin assignor.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Sticky assignor.
    /// </summary>
    Sticky,

    /// <summary>
    /// Cooperative sticky assignor (incremental rebalance).
    /// </summary>
    CooperativeSticky
}

/// <summary>
/// Interface for rebalance callbacks.
/// </summary>
public interface IRebalanceListener
{
    /// <summary>
    /// Called when partitions are assigned.
    /// </summary>
    ValueTask OnPartitionsAssignedAsync(IEnumerable<Producer.TopicPartition> partitions, CancellationToken cancellationToken);

    /// <summary>
    /// Called when partitions are revoked.
    /// </summary>
    ValueTask OnPartitionsRevokedAsync(IEnumerable<Producer.TopicPartition> partitions, CancellationToken cancellationToken);

    /// <summary>
    /// Called when partitions are lost (for cooperative rebalancing).
    /// </summary>
    ValueTask OnPartitionsLostAsync(IEnumerable<Producer.TopicPartition> partitions, CancellationToken cancellationToken);
}
