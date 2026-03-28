using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class CooperativeStickyAssignorTests
{
    [Test]
    public async Task Name_ReturnsCooperativeSticky()
    {
        var assignor = new CooperativeStickyAssignor();
        await Assert.That(assignor.Name).IsEqualTo("cooperative-sticky");
    }

    [Test]
    public async Task FreshAssignment_NoOwnedPartitions_SameAsSticky()
    {
        // With no previous assignments, cooperative acts like sticky
        var assignor = new CooperativeStickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" }, [], []),
            new("member-b", new HashSet<string> { "topic-a" }, [], [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 6 };

        var result = assignor.Assign(members, topicPartitionCounts);

        await Assert.That(result["member-a"].Count).IsEqualTo(3);
        await Assert.That(result["member-b"].Count).IsEqualTo(3);
    }

    [Test]
    public async Task OwnershipTransfer_PartitionsWithheldFromNewOwner()
    {
        // member-a owns all 4. member-b joins. Sticky wants to give 2 to member-b.
        // Per KIP-429: transferring partitions are removed from new owner AND from old owner.
        // They are temporarily unassigned, triggering revocation from member-a in round 2.
        var assignor = new CooperativeStickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 0), new TopicPartition("topic-a", 1),
                 new TopicPartition("topic-a", 2), new TopicPartition("topic-a", 3)], []),
            new("member-b", new HashSet<string> { "topic-a" }, [], [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 4 };

        var result = assignor.Assign(members, topicPartitionCounts);

        // member-a keeps only its target share (2), transferring partitions are removed
        await Assert.That(result["member-a"].Count).IsEqualTo(2);
        // member-b gets 0 in round 1 — transferring partitions are withheld
        await Assert.That(result["member-b"].Count).IsEqualTo(0);

        // Total is 2, not 4 — 2 partitions are temporarily unassigned
        var totalAssigned = result["member-a"].Count + result["member-b"].Count;
        await Assert.That(totalAssigned).IsEqualTo(2);
    }

    [Test]
    public async Task NoOwnershipChange_NoPartitionsWithheld()
    {
        // Already balanced, no movement needed — all assigned normally
        var assignor = new CooperativeStickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 0), new TopicPartition("topic-a", 1)], []),
            new("member-b", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 2), new TopicPartition("topic-a", 3)], [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 4 };

        var result = assignor.Assign(members, topicPartitionCounts);

        await Assert.That(result["member-a"].Count).IsEqualTo(2);
        await Assert.That(result["member-b"].Count).IsEqualTo(2);
    }

    [Test]
    public async Task Round2_AfterRevocation_PartitionsAssignedToNewOwner()
    {
        // Simulates round 2: member-a revoked p2,p3, now only owns p0,p1.
        // member-b has no partitions. p2,p3 are unowned — should be assigned directly.
        var assignor = new CooperativeStickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 0), new TopicPartition("topic-a", 1)], []),
            new("member-b", new HashSet<string> { "topic-a" }, [], [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 4 };

        var result = assignor.Assign(members, topicPartitionCounts);

        // No ownership transfers (p2,p3 are unowned), so direct assignment
        await Assert.That(result["member-a"].Count).IsEqualTo(2);
        await Assert.That(result["member-b"].Count).IsEqualTo(2);
    }

    [Test]
    public async Task MemberLeaves_NoWithholding()
    {
        // member-b left. Its partitions are unowned. No transfer withholding needed.
        var assignor = new CooperativeStickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 0), new TopicPartition("topic-a", 1)], [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 4 };

        var result = assignor.Assign(members, topicPartitionCounts);

        // member-a gets all 4 — p2,p3 were unowned so no withholding
        await Assert.That(result["member-a"].Count).IsEqualTo(4);
    }
}
