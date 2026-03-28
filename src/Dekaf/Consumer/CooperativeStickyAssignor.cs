using Dekaf.Producer;

namespace Dekaf.Consumer;

/// <summary>
/// Cooperative sticky partition assignor (KIP-429): runs the sticky assignment algorithm
/// then withholds partitions transferring ownership. Transferring partitions are removed from
/// the new owner's assignment and left temporarily unassigned — the old owner sees them revoked
/// and triggers a second rebalance round, enabling incremental (non-stop-the-world) rebalancing.
/// </summary>
public sealed class CooperativeStickyAssignor : IPartitionAssignmentStrategy
{
    /// <inheritdoc />
    public string Name => "cooperative-sticky";

    /// <inheritdoc />
    public bool IsCooperative => true;

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

        // Step 4: Build adjusted assignments — remove transferring partitions from their new owner.
        // Per KIP-429, transferring partitions are left temporarily unassigned: they are NOT added
        // back to the old owner. The old owner sees them revoked, calls OnPartitionsRevoked, and
        // triggers a second rebalance round where the partitions (now unowned) are assigned directly.
        var adjusted = new Dictionary<string, IReadOnlyList<TopicPartition>>(targetAssignments.Count);
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

        return adjusted;
    }
}
