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
        var sortedMembers = new List<ConsumerGroupMember>(members);
        sortedMembers.Sort((a, b) => string.Compare(a.MemberId, b.MemberId, StringComparison.Ordinal));

        var memberPartitions = new Dictionary<string, List<TopicPartition>>(sortedMembers.Count);
        foreach (var member in sortedMembers)
        {
            memberPartitions[member.MemberId] = [];
        }

        var sortedTopics = new List<string>(topicPartitionCounts.Keys);
        sortedTopics.Sort(StringComparer.Ordinal);

        var claimed = new HashSet<TopicPartition>();

        // Per-member-per-topic counts to avoid O(M*P) linear scans during quota enforcement
        var topicCounts = new Dictionary<(string MemberId, string Topic), int>();

        // Preserve valid existing assignments (first member by sorted order wins conflicts)
        foreach (var member in sortedMembers)
        {
            foreach (var tp in member.OwnedPartitions)
            {
                // Validate partition exists via topicPartitionCounts (avoids allPartitions HashSet)
                if (topicPartitionCounts.TryGetValue(tp.Topic, out var count)
                    && tp.Partition >= 0 && tp.Partition < count
                    && member.Subscriptions.Contains(tp.Topic)
                    && !claimed.Contains(tp))
                {
                    memberPartitions[member.MemberId].Add(tp);
                    claimed.Add(tp);

                    var key = (member.MemberId, tp.Topic);
                    topicCounts[key] = topicCounts.GetValueOrDefault(key) + 1;
                }
            }
        }

        // Compute per-topic quotas and revoke excess
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

            // Determine which members get maxQuota (minQuota + 1)
            // Members with more existing partitions get priority to avoid unnecessary movement
            var maxQuotaMembers = new HashSet<string>();
            if (extraSlots > 0)
            {
                var membersForQuota = new List<string>(interested);
                membersForQuota.Sort((a, b) =>
                {
                    var cmp = topicCounts.GetValueOrDefault((b, topic))
                        .CompareTo(topicCounts.GetValueOrDefault((a, topic)));
                    return cmp != 0 ? cmp : string.Compare(a, b, StringComparison.Ordinal);
                });

                for (var i = 0; i < extraSlots && i < membersForQuota.Count; i++)
                {
                    maxQuotaMembers.Add(membersForQuota[i]);
                }
            }

            // Revoke excess partitions using RemoveAll to avoid O(n^2) RemoveAt shifts
            foreach (var memberId in interested)
            {
                var quota = maxQuotaMembers.Contains(memberId) ? minQuota + 1 : minQuota;
                var currentCount = topicCounts.GetValueOrDefault((memberId, topic));

                if (currentCount > quota)
                {
                    var toRevoke = currentCount - quota;
                    var revoked = 0;
                    var partList = memberPartitions[memberId];

                    partList.RemoveAll(tp =>
                    {
                        if (revoked >= toRevoke || tp.Topic != topic)
                            return false;

                        claimed.Remove(tp);
                        topicCounts[(memberId, topic)]--;
                        revoked++;
                        return true;
                    });
                }
            }
        }

        // Collect unassigned partitions
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

        // Distribute unassigned partitions to members with fewest assignments per topic
        foreach (var tp in unassigned)
        {
            var interested = topicMembers[tp.Topic];
            if (interested.Count == 0)
                continue;

            // Find the interested member with fewest partitions for this topic (O(1) per member via topicCounts)
            string? bestMember = null;
            var bestCount = int.MaxValue;

            foreach (var memberId in interested)
            {
                var memberCount = topicCounts.GetValueOrDefault((memberId, tp.Topic));

                if (memberCount < bestCount)
                {
                    bestCount = memberCount;
                    bestMember = memberId;
                }
            }

            if (bestMember is not null)
            {
                memberPartitions[bestMember].Add(tp);
                claimed.Add(tp);

                var key = (bestMember, tp.Topic);
                topicCounts[key] = topicCounts.GetValueOrDefault(key) + 1;
            }
        }

        var assignments = new Dictionary<string, IReadOnlyList<TopicPartition>>(memberPartitions.Count);
        foreach (var (memberId, partitions) in memberPartitions)
        {
            assignments[memberId] = partitions;
        }

        return assignments;
    }
}
