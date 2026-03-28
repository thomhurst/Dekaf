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

    [Test]
    public async Task MultipleTopics_EachDistributedIndependently()
    {
        var assignor = new StickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a", "topic-b" }, [], []),
            new("member-b", new HashSet<string> { "topic-a", "topic-b" }, [], [])
        };
        var topicPartitionCounts = new Dictionary<string, int>
        {
            ["topic-a"] = 4,
            ["topic-b"] = 2
        };

        var result = assignor.Assign(members, topicPartitionCounts);

        // 4 + 2 = 6 total, each member gets 3
        await Assert.That(result["member-a"].Count).IsEqualTo(3);
        await Assert.That(result["member-b"].Count).IsEqualTo(3);

        // Each topic distributed independently: topic-a 2+2, topic-b 1+1
        var aTopicA = result["member-a"].Count(tp => tp.Topic == "topic-a");
        var bTopicA = result["member-b"].Count(tp => tp.Topic == "topic-a");
        await Assert.That(aTopicA).IsEqualTo(2);
        await Assert.That(bTopicA).IsEqualTo(2);

        var aTopicB = result["member-a"].Count(tp => tp.Topic == "topic-b");
        var bTopicB = result["member-b"].Count(tp => tp.Topic == "topic-b");
        await Assert.That(aTopicB).IsEqualTo(1);
        await Assert.That(bTopicB).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleTopics_PreservesExistingAcrossTopics()
    {
        var assignor = new StickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a", "topic-b" },
                [new TopicPartition("topic-a", 0), new TopicPartition("topic-b", 0)], []),
            new("member-b", new HashSet<string> { "topic-a", "topic-b" },
                [new TopicPartition("topic-a", 1), new TopicPartition("topic-b", 1)], [])
        };
        var topicPartitionCounts = new Dictionary<string, int>
        {
            ["topic-a"] = 2,
            ["topic-b"] = 2
        };

        var result = assignor.Assign(members, topicPartitionCounts);

        // Already balanced — zero movement
        var aSet = new HashSet<TopicPartition>(result["member-a"]);
        await Assert.That(aSet.Contains(new TopicPartition("topic-a", 0))).IsTrue();
        await Assert.That(aSet.Contains(new TopicPartition("topic-b", 0))).IsTrue();

        var bSet = new HashSet<TopicPartition>(result["member-b"]);
        await Assert.That(bSet.Contains(new TopicPartition("topic-a", 1))).IsTrue();
        await Assert.That(bSet.Contains(new TopicPartition("topic-b", 1))).IsTrue();
    }

    [Test]
    public async Task ConflictResolution_SortedOrderWins()
    {
        // Both members claim the same partitions — first by sorted member ID wins the claim,
        // then quota enforcement revokes excess. member-b can't claim anything (already taken).
        var assignor = new StickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-b", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 0), new TopicPartition("topic-a", 1)], []),
            new("member-a", new HashSet<string> { "topic-a" },
                [new TopicPartition("topic-a", 0), new TopicPartition("topic-a", 1)], [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 2 };

        var result = assignor.Assign(members, topicPartitionCounts);

        // Each gets exactly 1 partition (balanced)
        await Assert.That(result["member-a"].Count).IsEqualTo(1);
        await Assert.That(result["member-b"].Count).IsEqualTo(1);

        // member-a claimed both first (sorted order), then quota revoked one.
        // member-b gets the revoked partition. No duplicate assignments.
        var allPartitions = result["member-a"].Concat(result["member-b"]).ToHashSet();
        await Assert.That(allPartitions.Count).IsEqualTo(2);
        await Assert.That(allPartitions).Contains(new TopicPartition("topic-a", 0));
        await Assert.That(allPartitions).Contains(new TopicPartition("topic-a", 1));
    }

    [Test]
    public async Task EmptyMembers_ReturnsEmptyAssignment()
    {
        var assignor = new StickyAssignor();
        var members = new List<ConsumerGroupMember>();
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 4 };

        var result = assignor.Assign(members, topicPartitionCounts);

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task EmptyTopicPartitionCounts_ReturnsEmptyAssignments()
    {
        var assignor = new StickyAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" }, [], [])
        };
        var topicPartitionCounts = new Dictionary<string, int>();

        var result = assignor.Assign(members, topicPartitionCounts);

        await Assert.That(result["member-a"].Count).IsEqualTo(0);
    }
}
