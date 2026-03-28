using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class StickyAssignorTests
{
    [Test]
    public async Task Name_ReturnsSticky()
    {
        var assignor = new StickyAssignor();

        await Assert.That(assignor.Name).IsEqualTo("sticky");
    }

    [Test]
    public async Task FreshAssignment_NoOwnedPartitions_BalancedDistribution()
    {
        var assignor = new StickyAssignor();
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
    public async Task Reassignment_PreservesExistingAssignment()
    {
        var assignor = new StickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 0), new TopicPartition("topic-a", 1), new TopicPartition("topic-a", 2)], []),
            new("member-b", new HashSet<string> { "topic-a" }, [], [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 4 };

        var result = assignor.Assign(members, topicPartitionCounts);

        // Each should get 2 partitions (4 / 2 = 2)
        await Assert.That(result["member-a"].Count).IsEqualTo(2);
        await Assert.That(result["member-b"].Count).IsEqualTo(2);

        // member-a should retain at least 2 of its original partitions
        var memberAPartitions = new HashSet<TopicPartition>(result["member-a"]);
        var retained = 0;
        if (memberAPartitions.Contains(new TopicPartition("topic-a", 0))) retained++;
        if (memberAPartitions.Contains(new TopicPartition("topic-a", 1))) retained++;
        if (memberAPartitions.Contains(new TopicPartition("topic-a", 2))) retained++;
        await Assert.That(retained).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task MemberLeaves_PartitionsRedistributed()
    {
        var assignor = new StickyAssignor();
        // member-b left, only member-a remains
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 0), new TopicPartition("topic-a", 1)], [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 4 };

        var result = assignor.Assign(members, topicPartitionCounts);

        await Assert.That(result["member-a"].Count).IsEqualTo(4);

        // Original partitions should be preserved
        var partitions = new HashSet<TopicPartition>(result["member-a"]);
        await Assert.That(partitions.Contains(new TopicPartition("topic-a", 0))).IsTrue();
        await Assert.That(partitions.Contains(new TopicPartition("topic-a", 1))).IsTrue();
    }

    [Test]
    public async Task UnevenPartitions_MaxDifferenceOfOne()
    {
        var assignor = new StickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" }, [], []),
            new("member-b", new HashSet<string> { "topic-a" }, [], []),
            new("member-c", new HashSet<string> { "topic-a" }, [], [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 7 };

        var result = assignor.Assign(members, topicPartitionCounts);

        var counts = new List<int>
        {
            result["member-a"].Count,
            result["member-b"].Count,
            result["member-c"].Count
        };
        counts.Sort();

        // 7 / 3 = 2 remainder 1, so counts should be 2, 2, 3
        await Assert.That(counts[0]).IsEqualTo(2);
        await Assert.That(counts[1]).IsEqualTo(2);
        await Assert.That(counts[2]).IsEqualTo(3);
    }

    [Test]
    public async Task MixedSubscriptions_OnlySubscribedTopicsAssigned()
    {
        var assignor = new StickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" }, [], []),
            new("member-b", new HashSet<string> { "topic-b" }, [], [])
        };
        var topicPartitionCounts = new Dictionary<string, int>
        {
            ["topic-a"] = 3,
            ["topic-b"] = 3
        };

        var result = assignor.Assign(members, topicPartitionCounts);

        await Assert.That(result["member-a"].Count).IsEqualTo(3);
        foreach (var tp in result["member-a"])
        {
            await Assert.That(tp.Topic).IsEqualTo("topic-a");
        }

        await Assert.That(result["member-b"].Count).IsEqualTo(3);
        foreach (var tp in result["member-b"])
        {
            await Assert.That(tp.Topic).IsEqualTo("topic-b");
        }
    }

    [Test]
    public async Task Reassignment_DoesNotMovePartitionsUnnecessarily()
    {
        var assignor = new StickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 0), new TopicPartition("topic-a", 1), new TopicPartition("topic-a", 2)], []),
            new("member-b", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 3), new TopicPartition("topic-a", 4), new TopicPartition("topic-a", 5)], [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 6 };

        var result = assignor.Assign(members, topicPartitionCounts);

        // Already balanced — zero movement expected
        await Assert.That(result["member-a"].Count).IsEqualTo(3);
        await Assert.That(result["member-b"].Count).IsEqualTo(3);

        var memberASet = new HashSet<TopicPartition>(result["member-a"]);
        await Assert.That(memberASet.Contains(new TopicPartition("topic-a", 0))).IsTrue();
        await Assert.That(memberASet.Contains(new TopicPartition("topic-a", 1))).IsTrue();
        await Assert.That(memberASet.Contains(new TopicPartition("topic-a", 2))).IsTrue();

        var memberBSet = new HashSet<TopicPartition>(result["member-b"]);
        await Assert.That(memberBSet.Contains(new TopicPartition("topic-a", 3))).IsTrue();
        await Assert.That(memberBSet.Contains(new TopicPartition("topic-a", 4))).IsTrue();
        await Assert.That(memberBSet.Contains(new TopicPartition("topic-a", 5))).IsTrue();
    }

    [Test]
    public async Task NewPartitionsAdded_DistributedEvenly()
    {
        var assignor = new StickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 0), new TopicPartition("topic-a", 1)], []),
            new("member-b", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 2), new TopicPartition("topic-a", 3)], [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 8 };

        var result = assignor.Assign(members, topicPartitionCounts);

        // Each gets 4 partitions
        await Assert.That(result["member-a"].Count).IsEqualTo(4);
        await Assert.That(result["member-b"].Count).IsEqualTo(4);

        // Original partitions preserved
        var memberASet = new HashSet<TopicPartition>(result["member-a"]);
        await Assert.That(memberASet.Contains(new TopicPartition("topic-a", 0))).IsTrue();
        await Assert.That(memberASet.Contains(new TopicPartition("topic-a", 1))).IsTrue();

        var memberBSet = new HashSet<TopicPartition>(result["member-b"]);
        await Assert.That(memberBSet.Contains(new TopicPartition("topic-a", 2))).IsTrue();
        await Assert.That(memberBSet.Contains(new TopicPartition("topic-a", 3))).IsTrue();
    }

    [Test]
    public async Task SingleMember_GetsAll()
    {
        var assignor = new StickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" }, [], [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 4 };

        var result = assignor.Assign(members, topicPartitionCounts);

        await Assert.That(result["member-a"].Count).IsEqualTo(4);
        for (var i = 0; i < 4; i++)
        {
            await Assert.That(result["member-a"][i]).IsEqualTo(new TopicPartition("topic-a", i));
        }
    }
}
