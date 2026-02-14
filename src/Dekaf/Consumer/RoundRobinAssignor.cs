using Dekaf.Producer;

namespace Dekaf.Consumer;

/// <summary>
/// Round-robin partition assignor: collects all topic-partitions sorted by (topic, partition),
/// then distributes them round-robin to sorted members, skipping members not subscribed to a topic.
/// </summary>
public sealed class RoundRobinAssignor : IPartitionAssignmentStrategy
{
    /// <inheritdoc />
    public string Name => "roundrobin";

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<TopicPartition>> Assign(
        IReadOnlyList<ConsumerGroupMember> members,
        IReadOnlyDictionary<string, int> topicPartitionCounts)
    {
        var memberPartitions = new Dictionary<string, List<TopicPartition>>(members.Count);

        // Initialize assignments for all members
        foreach (var member in members)
        {
            memberPartitions[member.MemberId] = [];
        }

        // Collect all topic-partitions, sorted by (topic, partition)
        var allTopicPartitions = new List<TopicPartition>();
        var sortedTopics = new List<string>(topicPartitionCounts.Keys);
        sortedTopics.Sort(StringComparer.Ordinal);

        foreach (var topic in sortedTopics)
        {
            var partitionCount = topicPartitionCounts[topic];
            for (var p = 0; p < partitionCount; p++)
            {
                allTopicPartitions.Add(new TopicPartition(topic, p));
            }
        }

        // Sort members by ID for deterministic assignment
        var sortedMembers = new List<ConsumerGroupMember>(members);
        sortedMembers.Sort((a, b) => string.Compare(a.MemberId, b.MemberId, StringComparison.Ordinal));

        // Distribute round-robin, skipping unsubscribed members
        var memberIdx = 0;
        foreach (var tp in allTopicPartitions)
        {
            // Find the next subscribed member (wrapping around)
            var startIdx = memberIdx;
            var assigned = false;

            do
            {
                var candidate = sortedMembers[memberIdx % sortedMembers.Count];
                memberIdx = (memberIdx + 1) % sortedMembers.Count;

                if (candidate.Subscriptions.Contains(tp.Topic))
                {
                    memberPartitions[candidate.MemberId].Add(tp);
                    assigned = true;
                    break;
                }
            } while (memberIdx != startIdx);

            // If no member is subscribed (shouldn't happen with valid input), skip
            if (!assigned)
            {
                memberIdx = (startIdx + 1) % sortedMembers.Count;
            }
        }

        // Convert to IReadOnlyList
        var assignments = new Dictionary<string, IReadOnlyList<TopicPartition>>(memberPartitions.Count);
        foreach (var (memberId, partitions) in memberPartitions)
        {
            assignments[memberId] = partitions;
        }

        return assignments;
    }
}
