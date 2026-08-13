namespace Dekaf.Consumer;

/// <summary>
/// Optional capability that exposes the liveness of a consumer's group membership.
/// </summary>
/// <remarks>
/// Dekaf's built-in consumer implements this interface. Consumer wrappers can implement it so
/// operational integrations can distinguish a live standby member from a consumer that has not
/// joined its group or has stopped heartbeating.
/// </remarks>
public interface IConsumerGroupLiveness
{
    /// <summary>Gets a point-in-time snapshot of the consumer's group liveness.</summary>
    ConsumerGroupLiveness GroupLiveness { get; }
}

/// <summary>
/// A point-in-time snapshot of consumer group liveness.
/// </summary>
/// <param name="HasConsumerGroup">Whether the consumer is configured to participate in a group.</param>
/// <param name="IsJoined">Whether the consumer currently has stable group membership.</param>
/// <param name="IsStopped">Whether the consumer has been closed or disposed.</param>
/// <param name="TimeSinceLastHeartbeat">
/// Time elapsed since the most recent successful group heartbeat, or <see langword="null"/>
/// when no heartbeat has succeeded.
/// </param>
/// <param name="HeartbeatInterval">
/// The broker-directed interval between consumer group heartbeats.
/// </param>
/// <param name="LastHeartbeatFailure">The most recent heartbeat failure, if any.</param>
public readonly record struct ConsumerGroupLiveness(
    bool HasConsumerGroup,
    bool IsJoined,
    bool IsStopped,
    TimeSpan? TimeSinceLastHeartbeat,
    TimeSpan HeartbeatInterval,
    string? LastHeartbeatFailure);
