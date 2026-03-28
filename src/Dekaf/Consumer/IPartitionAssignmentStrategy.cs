using Dekaf.Producer;

namespace Dekaf.Consumer;

/// <summary>
/// Represents a consumer group member for partition assignment.
/// </summary>
/// <param name="MemberId">The member's unique identifier.</param>
/// <param name="Subscriptions">The set of topics this member is subscribed to.</param>
/// <param name="OwnedPartitions">The partitions currently owned by this member, used by sticky assignors.</param>
/// <param name="Metadata">Raw metadata bytes from the member's JoinGroup request, for custom strategies.</param>
public readonly record struct ConsumerGroupMember(
    string MemberId,
    IReadOnlySet<string> Subscriptions,
    IReadOnlyCollection<TopicPartition> OwnedPartitions,
    byte[] Metadata);

/// <summary>
/// Defines a pluggable strategy for assigning topic partitions to consumer group members.
/// </summary>
public interface IPartitionAssignmentStrategy
{
    /// <summary>
    /// The protocol name for this strategy, sent in JoinGroup/SyncGroup requests.
    /// Must match the Kafka protocol name (e.g., "range", "roundrobin").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this strategy uses cooperative (incremental) rebalancing.
    /// Cooperative strategies only revoke partitions that are transferring ownership,
    /// enabling non-stop-the-world rebalancing via multiple rebalance rounds.
    /// </summary>
    bool IsCooperative => false;

    /// <summary>
    /// Computes partition assignments for the given consumer group members.
    /// </summary>
    /// <param name="members">The consumer group members with their subscriptions.</param>
    /// <param name="topicPartitionCounts">A mapping of topic name to partition count for all subscribed topics.</param>
    /// <returns>A mapping of member ID to the list of topic-partitions assigned to that member.</returns>
    IReadOnlyDictionary<string, IReadOnlyList<TopicPartition>> Assign(
        IReadOnlyList<ConsumerGroupMember> members,
        IReadOnlyDictionary<string, int> topicPartitionCounts);
}

/// <summary>
/// Provides singleton instances of built-in partition assignment strategies.
/// </summary>
public static class PartitionAssignors
{
    /// <summary>
    /// Range assignor: divides partitions into contiguous ranges per topic.
    /// </summary>
    public static IPartitionAssignmentStrategy Range { get; } = new RangeAssignor();

    /// <summary>
    /// Round-robin assignor: distributes all topic-partitions in round-robin order.
    /// </summary>
    public static IPartitionAssignmentStrategy RoundRobin { get; } = new RoundRobinAssignor();

    /// <summary>
    /// Sticky assignor: preserves existing assignments while maintaining balance (KIP-54).
    /// </summary>
    public static IPartitionAssignmentStrategy Sticky { get; } = new StickyAssignor();

    /// <summary>
    /// Cooperative sticky assignor: sticky assignment with incremental rebalancing (KIP-429).
    /// </summary>
    public static IPartitionAssignmentStrategy CooperativeSticky { get; } = new CooperativeStickyAssignor();
}
