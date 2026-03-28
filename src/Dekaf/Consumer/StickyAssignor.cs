using Dekaf.Producer;

namespace Dekaf.Consumer;

/// <summary>
/// Sticky partition assignor (KIP-54): minimizes partition movement while maintaining balance.
/// Preserves valid existing assignments and only moves partitions when necessary for rebalancing.
/// </summary>
public sealed class StickyAssignor : IPartitionAssignmentStrategy
{
    /// <inheritdoc />
    public string Name => "sticky";

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<TopicPartition>> Assign(
        IReadOnlyList<ConsumerGroupMember> members,
        IReadOnlyDictionary<string, int> topicPartitionCounts)
    {
        return AssignSticky(members, topicPartitionCounts);
    }

    /// <summary>
    /// Core sticky assignment algorithm, shared with <see cref="CooperativeStickyAssignor"/>.
    /// </summary>
    internal static IReadOnlyDictionary<string, IReadOnlyList<TopicPartition>> AssignSticky(
        IReadOnlyList<ConsumerGroupMember> members,
        IReadOnlyDictionary<string, int> topicPartitionCounts)
    {
        // Sort members by ID for deterministic results
        var sortedMembers = new List<ConsumerGroupMember>(members);
        sortedMembers.Sort((a, b) => string.Compare(a.MemberId, b.MemberId, StringComparison.Ordinal));

        var memberPartitions = new Dictionary<string, List<TopicPartition>>(sortedMembers.Count);

        // Initialize assignments for all members
        foreach (var member in sortedMembers)
        {
            memberPartitions[member.MemberId] = [];
        }

        // Build set of all valid topic-partitions and track which are claimed
        var allPartitions = new HashSet<TopicPartition>();
        var sortedTopics = new List<string>(topicPartitionCounts.Keys);
        sortedTopics.Sort(StringComparer.Ordinal);

        foreach (var topic in sortedTopics)
        {
            var partitionCount = topicPartitionCounts[topic];
            for (var p = 0; p < partitionCount; p++)
            {
                allPartitions.Add(new TopicPartition(topic, p));
            }
        }

        // Track claimed partitions to prevent duplicates
        var claimed = new HashSet<TopicPartition>();

        // Step 1: Preserve valid existing assignments (first member by sorted order wins conflicts)
        foreach (var member in sortedMembers)
        {
            foreach (var tp in member.OwnedPartitions)
            {
                if (allPartitions.Contains(tp) &&
                    member.Subscriptions.Contains(tp.Topic) &&
                    !claimed.Contains(tp))
                {
                    memberPartitions[member.MemberId].Add(tp);
                    claimed.Add(tp);
                }
            }
        }

        // Step 2: Compute per-topic quotas and revoke excess
        // Build per-topic interested member lists
        var topicMembers = new Dictionary<string, List<string>>();
        foreach (var topic in sortedTopics)
        {
            var interested = new List<string>();
            foreach (var member in sortedMembers)
            {
                if (member.Subscriptions.Contains(topic))
                {
                    interested.Add(member.MemberId);
                }
            }
            topicMembers[topic] = interested;
        }

        // For each topic, compute quotas and revoke excess from members
        foreach (var topic in sortedTopics)
        {
            var interested = topicMembers[topic];
            if (interested.Count == 0)
                continue;

            var partitionCount = topicPartitionCounts[topic];
            var minQuota = partitionCount / interested.Count;
            var extraSlots = partitionCount % interested.Count;

            // Count how many partitions each interested member has for this topic
            // Members sorted by member ID; first 'extraSlots' members get maxQuota, rest get minQuota
            var memberTopicCounts = new Dictionary<string, int>(interested.Count);
            foreach (var memberId in interested)
            {
                var count = 0;
                foreach (var tp in memberPartitions[memberId])
                {
                    if (tp.Topic == topic)
                        count++;
                }
                memberTopicCounts[memberId] = count;
            }

            // Determine which members get maxQuota (minQuota + 1)
            // Sort interested members by their current count descending, then by member ID
            // to determine who gets extra slots. Members with more existing partitions
            // for this topic are more likely to keep them.
            var membersForQuota = new List<string>(interested);

            // Assign max quota slots: members that already have more partitions get priority
            // to avoid unnecessary movement
            var maxQuotaMembers = new HashSet<string>();
            if (extraSlots > 0)
            {
                // Sort by current count descending (prefer members that already have more),
                // then by member ID for determinism
                membersForQuota.Sort((a, b) =>
                {
                    var cmp = memberTopicCounts[b].CompareTo(memberTopicCounts[a]);
                    return cmp != 0 ? cmp : string.Compare(a, b, StringComparison.Ordinal);
                });

                for (var i = 0; i < extraSlots && i < membersForQuota.Count; i++)
                {
                    maxQuotaMembers.Add(membersForQuota[i]);
                }
            }

            // Revoke excess partitions
            foreach (var memberId in interested)
            {
                var quota = maxQuotaMembers.Contains(memberId) ? minQuota + 1 : minQuota;
                var currentCount = memberTopicCounts[memberId];

                if (currentCount > quota)
                {
                    // Remove excess from end of the member's list for this topic
                    var toRevoke = currentCount - quota;
                    var revoked = 0;
                    for (var i = memberPartitions[memberId].Count - 1; i >= 0 && revoked < toRevoke; i--)
                    {
                        if (memberPartitions[memberId][i].Topic == topic)
                        {
                            claimed.Remove(memberPartitions[memberId][i]);
                            memberPartitions[memberId].RemoveAt(i);
                            revoked++;
                        }
                    }
                }
            }
        }

        // Step 3: Collect unassigned partitions
        var unassigned = new List<TopicPartition>();
        foreach (var topic in sortedTopics)
        {
            var partitionCount = topicPartitionCounts[topic];
            for (var p = 0; p < partitionCount; p++)
            {
                var tp = new TopicPartition(topic, p);
                if (!claimed.Contains(tp))
                {
                    unassigned.Add(tp);
                }
            }
        }

        // Step 4: Distribute unassigned partitions to members with fewest assignments per topic
        foreach (var tp in unassigned)
        {
            var interested = topicMembers[tp.Topic];
            if (interested.Count == 0)
                continue;

            var partitionCount = topicPartitionCounts[tp.Topic];
            var minQuota = partitionCount / interested.Count;
            var extraSlots = partitionCount % interested.Count;

            // Find the interested member with fewest partitions for this topic
            string? bestMember = null;
            var bestCount = int.MaxValue;

            foreach (var memberId in interested)
            {
                var count = 0;
                foreach (var existing in memberPartitions[memberId])
                {
                    if (existing.Topic == tp.Topic)
                        count++;
                }

                if (count < bestCount)
                {
                    bestCount = count;
                    bestMember = memberId;
                }
            }

            if (bestMember is not null)
            {
                memberPartitions[bestMember].Add(tp);
                claimed.Add(tp);
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
