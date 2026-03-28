using Dekaf.Producer;

namespace Dekaf.Consumer;

/// <summary>
/// Cooperative sticky partition assignor (KIP-429): runs the sticky assignment algorithm
/// then withholds partitions transferring ownership from the new owner. The old owner keeps
/// them until it revokes in the next rebalance round, enabling incremental (non-stop-the-world) rebalancing.
/// </summary>
public sealed class CooperativeStickyAssignor : IPartitionAssignmentStrategy
{
    /// <inheritdoc />
    public string Name => "cooperative-sticky";

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<TopicPartition>> Assign(
        IReadOnlyList<ConsumerGroupMember> members,
        IReadOnlyDictionary<string, int> topicPartitionCounts)
    {
        // Step 1: Run the base sticky assignment to get target state
        var targetAssignments = StickyAssignor.AssignSticky(members, topicPartitionCounts);

        // Step 2: Build owned-partition-to-owner index from current state
        var currentOwner = new Dictionary<TopicPartition, string>();
        foreach (var member in members)
        {
            foreach (var tp in member.OwnedPartitions)
                currentOwner[tp] = member.MemberId;
        }

        // Step 3: Find partitions transferring ownership
        var transferring = new HashSet<TopicPartition>();
        foreach (var (memberId, partitions) in targetAssignments)
        {
            foreach (var tp in partitions)
            {
                if (currentOwner.TryGetValue(tp, out var prevOwner) && prevOwner != memberId)
                    transferring.Add(tp);
            }
        }

        if (transferring.Count == 0)
            return targetAssignments;

        // Step 4: Build adjusted assignments - remove transferring from new owner, keep with old owner
        var adjusted = new Dictionary<string, List<TopicPartition>>(targetAssignments.Count);
        foreach (var (memberId, partitions) in targetAssignments)
        {
            var filtered = new List<TopicPartition>(partitions.Count);
            foreach (var tp in partitions)
            {
                if (!transferring.Contains(tp))
                    filtered.Add(tp);
            }
            adjusted[memberId] = filtered;
        }

        // Step 5: Ensure old owners keep their transferring partitions
        foreach (var tp in transferring)
        {
            if (currentOwner.TryGetValue(tp, out var oldOwner) && adjusted.TryGetValue(oldOwner, out var ownerList))
            {
                if (!ownerList.Contains(tp))
                    ownerList.Add(tp);
            }
        }

        // Convert to IReadOnlyList
        var result = new Dictionary<string, IReadOnlyList<TopicPartition>>(adjusted.Count);
        foreach (var (memberId, partitions) in adjusted)
            result[memberId] = partitions;

        return result;
    }
}
