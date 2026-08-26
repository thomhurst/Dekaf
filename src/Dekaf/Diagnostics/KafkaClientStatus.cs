using Dekaf.Consumer;

namespace Dekaf.Diagnostics;

/// <summary>
/// Optional capability exposing the cached identity of a Kafka client.
/// </summary>
/// <remarks>
/// Dekaf's built-in producer, consumer, share consumer, and admin client implement this
/// interface. The property is a non-blocking cache read and never starts network I/O.
/// </remarks>
public interface IKafkaClientIdentity
{
    /// <summary>
    /// Gets the cluster ID from the latest accepted metadata, or <see langword="null"/>
    /// before metadata supplies one.
    /// </summary>
    string? ClusterId { get; }
}

/// <summary>
/// Optional capability exposing the cached KIP-714 identity of a Kafka client.
/// </summary>
/// <remarks>
/// Dekaf's built-in producer, consumer, share consumer, and admin client implement this
/// interface. The property is a non-blocking cache read and never starts network I/O.
/// </remarks>
public interface IKafkaClientInstanceIdentity
{
    /// <summary>
    /// Gets the broker-assigned KIP-714 client instance ID, or <see langword="null"/>
    /// before telemetry negotiation succeeds or when the broker does not support telemetry.
    /// </summary>
    /// <remarks>
    /// The value is the latest accepted immutable identity. It remains available after disposal
    /// when negotiation completed before the client stopped.
    /// </remarks>
    Guid? ClientInstanceId { get; }
}

/// <summary>
/// Optional capability exposing a low-frequency operational status snapshot.
/// </summary>
/// <remarks>
/// Snapshot collection allocates and may enumerate the represented brokers and assigned
/// partitions. It is intended for readiness checks, support bundles, and periodic monitoring,
/// not per-message use. Individual fields are read lock-free and may reflect adjacent instants.
/// </remarks>
public interface IKafkaClientStatusProvider : IKafkaClientIdentity
{
    /// <summary>Captures the client's current operational status.</summary>
    KafkaClientStatus GetStatus();
}

/// <summary>Identifies the built-in client role represented by a status snapshot.</summary>
public enum KafkaClientRole
{
    Producer,
    Consumer,
    ShareConsumer,
    Admin
}

/// <summary>Summarizes the physical connections currently represented for one broker.</summary>
public enum BrokerConnectionState
{
    Disconnected,
    PartiallyConnected,
    Connected
}

/// <summary>A point-in-time operational snapshot for a Kafka client.</summary>
public sealed class KafkaClientStatus
{
    /// <summary>UTC time at which collection began.</summary>
    public required DateTimeOffset CapturedAtUtc { get; init; }

    /// <summary>The built-in client role.</summary>
    public required KafkaClientRole Role { get; init; }

    /// <summary>The latest accepted Kafka cluster ID, when known.</summary>
    public string? ClusterId { get; init; }

    /// <summary>The latest broker-assigned KIP-714 client instance ID, when known.</summary>
    public Guid? ClientInstanceId { get; init; }

    /// <summary>UTC time of the latest accepted metadata update, when available.</summary>
    public DateTimeOffset? MetadataLastRefreshedAtUtc { get; init; }

    /// <summary>Whether the client has been closed or disposed.</summary>
    public bool IsStopped { get; init; }

    /// <summary>Per-broker connection snapshots. Bootstrap-only endpoints are excluded.</summary>
    public required IReadOnlyList<BrokerConnectionStatus> Brokers { get; init; }

    /// <summary>Producer backlog state, populated only for producers.</summary>
    public ProducerBacklogStatus? Producer { get; init; }

    /// <summary>Consumer group state, populated for regular and share consumers.</summary>
    public ConsumerGroupStatus? ConsumerGroup { get; init; }
}

/// <summary>Point-in-time connection state for one broker.</summary>
public sealed class BrokerConnectionStatus
{
    /// <summary>Broker node ID from the latest metadata.</summary>
    public required int BrokerId { get; init; }

    /// <summary>Broker host from the latest metadata.</summary>
    public required string Host { get; init; }

    /// <summary>Broker port from the latest metadata.</summary>
    public required int Port { get; init; }

    /// <summary>Aggregate physical connection state.</summary>
    public required BrokerConnectionState State { get; init; }

    /// <summary>Number of physical connection slots currently represented.</summary>
    public required int ConnectionCount { get; init; }

    /// <summary>Number of represented physical connections currently connected.</summary>
    public required int ConnectedConnectionCount { get; init; }

    /// <summary>Number of requests awaiting responses across represented connections.</summary>
    public required int PendingRequestCount { get; init; }

    /// <summary>Approximate UTC time of the latest successful request.</summary>
    public DateTimeOffset? LastSuccessfulRequestAtUtc { get; init; }

    /// <summary>Approximate UTC time of the latest observed connection-state transition.</summary>
    public DateTimeOffset? LastConnectionStateChangeAtUtc { get; init; }

    /// <summary>Approximate UTC time of the latest connection-attempt failure.</summary>
    public DateTimeOffset? LastErrorAtUtc { get; init; }

    /// <summary>Latest connection-attempt failure summary.</summary>
    public string? LastError { get; init; }
}

/// <summary>Point-in-time producer backlog and capacity state.</summary>
/// <param name="BufferedBytes">Bytes currently reserved by buffered records.</param>
/// <param name="BufferCapacityBytes">Configured producer buffer capacity in bytes.</param>
/// <param name="UnsealedBatchCount">Number of mutable batches accepting records.</param>
/// <param name="QueuedBatchCount">Number of sealed batches queued for dispatch.</param>
/// <param name="InFlightBatchCount">Number of batches dispatched and awaiting completion.</param>
/// <param name="BufferPressureEventCount">Cumulative number of buffer-pressure events.</param>
public readonly record struct ProducerBacklogStatus(
    long BufferedBytes,
    ulong BufferCapacityBytes,
    int UnsealedBatchCount,
    long QueuedBatchCount,
    long InFlightBatchCount,
    long BufferPressureEventCount)
{
    /// <summary>Current buffer utilization. Values above 1 are possible while limits shrink.</summary>
    public double BufferUtilization => BufferCapacityBytes == 0
        ? 0
        : BufferedBytes / (double)BufferCapacityBytes;

    /// <summary>Whether current usage has reached the configured capacity.</summary>
    public bool IsAtCapacity => BufferCapacityBytes != 0 && (ulong)Math.Max(BufferedBytes, 0) >= BufferCapacityBytes;
}

/// <summary>Point-in-time regular or share consumer group state.</summary>
public sealed class ConsumerGroupStatus
{
    /// <summary>Whether the consumer is participating in group coordination.</summary>
    public required bool HasConsumerGroup { get; init; }

    /// <summary>Current coordinator state.</summary>
    public required CoordinatorState State { get; init; }

    /// <summary>Current coordinator broker ID, or <c>-1</c> when unknown.</summary>
    public required int CoordinatorId { get; init; }

    /// <summary>Current group member ID, when assigned.</summary>
    public string? MemberId { get; init; }

    /// <summary>Classic generation ID or consumer/share member epoch.</summary>
    public required int GenerationOrMemberEpoch { get; init; }

    /// <summary>Configured group heartbeat interval.</summary>
    public required TimeSpan HeartbeatInterval { get; init; }

    /// <summary>Elapsed time since the latest successful heartbeat, when available.</summary>
    public TimeSpan? TimeSinceLastHeartbeat { get; init; }

    /// <summary>Latest heartbeat failure summary, when available.</summary>
    public string? LastHeartbeatFailure { get; init; }

    /// <summary>Immutable copy of the current partition assignment.</summary>
    public required IReadOnlyList<TopicPartition> Assignment { get; init; }
}
