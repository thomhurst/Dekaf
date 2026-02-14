using Dekaf.Producer;

namespace Dekaf.Consumer;

/// <summary>
/// Represents a consumer group member for partition assignment.
/// </summary>
/// <param name="MemberId">The member's unique identifier.</param>
/// <param name="Subscriptions">The set of topics this member is subscribed to.</param>
/// <param name="Metadata">Raw metadata bytes from the member's JoinGroup request, for custom strategies.</param>
public readonly record struct ConsumerGroupMember(
    string MemberId,
    IReadOnlySet<string> Subscriptions,
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
}
