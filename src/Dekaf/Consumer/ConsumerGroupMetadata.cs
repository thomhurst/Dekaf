namespace Dekaf.Consumer;

/// <summary>
/// Contains metadata about the consumer's membership in a consumer group.
/// Used for transactional producer integration to enable exactly-once semantics.
/// </summary>
public sealed class ConsumerGroupMetadata
{
    /// <summary>
    /// The consumer group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The generation ID of the consumer group.
    /// This changes each time the group rebalances.
    /// </summary>
    public required int GenerationId { get; init; }

    /// <summary>
    /// The unique member ID assigned to this consumer by the group coordinator.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// The group instance ID for static membership.
    /// Null if the consumer is not using static membership.
    /// </summary>
    public string? GroupInstanceId { get; init; }
}
