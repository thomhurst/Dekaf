using Dekaf.Producer;

namespace Dekaf.Consumer;

/// <summary>
/// Range partition assignor: divides partitions into contiguous ranges per topic.
/// For each topic, partitions are divided evenly among interested members (sorted by member ID).
/// If the division is uneven, the first members get one extra partition.
/// </summary>
public sealed class RangeAssignor : IPartitionAssignmentStrategy
{
    /// <inheritdoc />
    public string Name => "range";

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<TopicPartition>> Assign(
        IReadOnlyList<ConsumerGroupMember> members,
        IReadOnlyDictionary<string, int> topicPartitionCounts)
    {
        var assignments = new Dictionary<string, IReadOnlyList<TopicPartition>>(members.Count);
        var memberPartitions = new Dictionary<string, List<TopicPartition>>(members.Count);

        // Initialize assignments for all members
        foreach (var member in members)
        {
            memberPartitions[member.MemberId] = [];
        }

        // Assign partitions per topic
        foreach (var (topic, partitionCount) in topicPartitionCounts)
        {
            // Get members interested in this topic, sorted for deterministic assignment
            var interestedMembers = new List<string>();
            foreach (var member in members)
            {
                if (member.Subscriptions.Contains(topic))
                {
                    interestedMembers.Add(member.MemberId);
                }
            }

            if (interestedMembers.Count > 1)
            {
                interestedMembers.Sort(StringComparer.Ordinal);
            }

            if (interestedMembers.Count == 0)
                continue;

            // Range assignment: divide partitions evenly among interested members
            var partitionsPerMember = partitionCount / interestedMembers.Count;
            var extraPartitions = partitionCount % interestedMembers.Count;

            var partitionIndex = 0;
            for (var memberIdx = 0; memberIdx < interestedMembers.Count; memberIdx++)
            {
                var memberId = interestedMembers[memberIdx];
                var assignedCount = partitionsPerMember + (memberIdx < extraPartitions ? 1 : 0);

                for (var i = 0; i < assignedCount; i++)
                {
                    memberPartitions[memberId].Add(new TopicPartition(topic, partitionIndex++));
                }
            }
        }

        // Convert to IReadOnlyList
        foreach (var (memberId, partitions) in memberPartitions)
        {
            assignments[memberId] = partitions;
        }

        return assignments;
    }
}
