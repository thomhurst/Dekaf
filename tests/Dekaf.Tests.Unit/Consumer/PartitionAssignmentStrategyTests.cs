using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PartitionAssignmentStrategyTests
{
    #region RangeAssignor

    [Test]
    public async Task RangeAssignor_Name_ReturnsRange()
    {
        var assignor = new RangeAssignor();

        await Assert.That(assignor.Name).IsEqualTo("range");
    }

    [Test]
    public async Task RangeAssignor_SingleMember_GetsAllPartitions()
    {
        var assignor = new RangeAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-0", new HashSet<string> { "topic-a" }, [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 6 };

        var result = assignor.Assign(members, topicPartitionCounts);

        await Assert.That(result["member-0"].Count).IsEqualTo(6);
        for (var i = 0; i < 6; i++)
        {
            await Assert.That(result["member-0"][i]).IsEqualTo(new TopicPartition("topic-a", i));
        }
    }

    [Test]
    public async Task RangeAssignor_TwoMembers_EvenSplit()
    {
        var assignor = new RangeAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-0", new HashSet<string> { "topic-a" }, []),
            new("member-1", new HashSet<string> { "topic-a" }, [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 6 };

        var result = assignor.Assign(members, topicPartitionCounts);

        // member-0 gets p0,p1,p2; member-1 gets p3,p4,p5
        await Assert.That(result["member-0"].Count).IsEqualTo(3);
        await Assert.That(result["member-1"].Count).IsEqualTo(3);

        await Assert.That(result["member-0"][0]).IsEqualTo(new TopicPartition("topic-a", 0));
        await Assert.That(result["member-0"][2]).IsEqualTo(new TopicPartition("topic-a", 2));
        await Assert.That(result["member-1"][0]).IsEqualTo(new TopicPartition("topic-a", 3));
        await Assert.That(result["member-1"][2]).IsEqualTo(new TopicPartition("topic-a", 5));
    }

    [Test]
    public async Task RangeAssignor_TwoMembers_UnevenSplit()
    {
        var assignor = new RangeAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-0", new HashSet<string> { "topic-a" }, []),
            new("member-1", new HashSet<string> { "topic-a" }, [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 7 };

        var result = assignor.Assign(members, topicPartitionCounts);

        // member-0 gets 4 (3 + 1 extra), member-1 gets 3
        await Assert.That(result["member-0"].Count).IsEqualTo(4);
        await Assert.That(result["member-1"].Count).IsEqualTo(3);
    }

    [Test]
    public async Task RangeAssignor_MemberNotSubscribedToTopic_GetsNoPartitionsForIt()
    {
        var assignor = new RangeAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-0", new HashSet<string> { "topic-a" }, []),
            new("member-1", new HashSet<string> { "topic-b" }, [])
        };
        var topicPartitionCounts = new Dictionary<string, int>
        {
            ["topic-a"] = 3,
            ["topic-b"] = 3
        };

        var result = assignor.Assign(members, topicPartitionCounts);

        // member-0 only gets topic-a partitions
        await Assert.That(result["member-0"].Count).IsEqualTo(3);
        foreach (var tp in result["member-0"])
        {
            await Assert.That(tp.Topic).IsEqualTo("topic-a");
        }

        // member-1 only gets topic-b partitions
        await Assert.That(result["member-1"].Count).IsEqualTo(3);
        foreach (var tp in result["member-1"])
        {
            await Assert.That(tp.Topic).IsEqualTo("topic-b");
        }
    }

    [Test]
    public async Task RangeAssignor_MultipleTopics_AssignedIndependently()
    {
        var assignor = new RangeAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-0", new HashSet<string> { "topic-a", "topic-b" }, []),
            new("member-1", new HashSet<string> { "topic-a", "topic-b" }, [])
        };
        var topicPartitionCounts = new Dictionary<string, int>
        {
            ["topic-a"] = 4,
            ["topic-b"] = 2
        };

        var result = assignor.Assign(members, topicPartitionCounts);

        // topic-a: member-0 gets p0,p1; member-1 gets p2,p3
        // topic-b: member-0 gets p0; member-1 gets p1
        await Assert.That(result["member-0"].Count).IsEqualTo(3); // 2 from topic-a + 1 from topic-b
        await Assert.That(result["member-1"].Count).IsEqualTo(3); // 2 from topic-a + 1 from topic-b
    }

    [Test]
    public async Task RangeAssignor_DeterministicOrdering_SortedByMemberId()
    {
        var assignor = new RangeAssignor();
        // Provide members in reverse order to verify sorting
        var members = new List<ConsumerGroupMember>
        {
            new("member-z", new HashSet<string> { "topic-a" }, []),
            new("member-a", new HashSet<string> { "topic-a" }, [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 4 };

        var result = assignor.Assign(members, topicPartitionCounts);

        // member-a (alphabetically first) gets p0,p1; member-z gets p2,p3
        await Assert.That(result["member-a"][0].Partition).IsEqualTo(0);
        await Assert.That(result["member-a"][1].Partition).IsEqualTo(1);
        await Assert.That(result["member-z"][0].Partition).IsEqualTo(2);
        await Assert.That(result["member-z"][1].Partition).IsEqualTo(3);
    }

    #endregion

    #region RoundRobinAssignor

    [Test]
    public async Task RoundRobinAssignor_Name_ReturnsRoundRobin()
    {
        var assignor = new RoundRobinAssignor();

        await Assert.That(assignor.Name).IsEqualTo("roundrobin");
    }

    [Test]
    public async Task RoundRobinAssignor_SingleMember_GetsAllPartitions()
    {
        var assignor = new RoundRobinAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-0", new HashSet<string> { "topic-a" }, [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 4 };

        var result = assignor.Assign(members, topicPartitionCounts);

        await Assert.That(result["member-0"].Count).IsEqualTo(4);
    }

    [Test]
    public async Task RoundRobinAssignor_TwoMembers_AlternatePartitions()
    {
        var assignor = new RoundRobinAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" }, []),
            new("member-b", new HashSet<string> { "topic-a" }, [])
        };
        var topicPartitionCounts = new Dictionary<string, int> { ["topic-a"] = 4 };

        var result = assignor.Assign(members, topicPartitionCounts);

        // Round-robin: p0→A, p1→B, p2→A, p3→B
        await Assert.That(result["member-a"].Count).IsEqualTo(2);
        await Assert.That(result["member-b"].Count).IsEqualTo(2);

        await Assert.That(result["member-a"][0]).IsEqualTo(new TopicPartition("topic-a", 0));
        await Assert.That(result["member-a"][1]).IsEqualTo(new TopicPartition("topic-a", 2));
        await Assert.That(result["member-b"][0]).IsEqualTo(new TopicPartition("topic-a", 1));
        await Assert.That(result["member-b"][1]).IsEqualTo(new TopicPartition("topic-a", 3));
    }

    [Test]
    public async Task RoundRobinAssignor_SkipsUnsubscribedMembers()
    {
        var assignor = new RoundRobinAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a" }, []),
            new("member-b", new HashSet<string> { "topic-b" }, [])
        };
        var topicPartitionCounts = new Dictionary<string, int>
        {
            ["topic-a"] = 2,
            ["topic-b"] = 2
        };

        var result = assignor.Assign(members, topicPartitionCounts);

        // member-a gets all topic-a partitions, member-b gets all topic-b partitions
        await Assert.That(result["member-a"].Count).IsEqualTo(2);
        await Assert.That(result["member-b"].Count).IsEqualTo(2);

        foreach (var tp in result["member-a"])
        {
            await Assert.That(tp.Topic).IsEqualTo("topic-a");
        }
        foreach (var tp in result["member-b"])
        {
            await Assert.That(tp.Topic).IsEqualTo("topic-b");
        }
    }

    [Test]
    public async Task RoundRobinAssignor_EvenDistribution_MultipleTopics()
    {
        var assignor = new RoundRobinAssignor();
        var members = new List<ConsumerGroupMember>
        {
            new("member-a", new HashSet<string> { "topic-a", "topic-b" }, []),
            new("member-b", new HashSet<string> { "topic-a", "topic-b" }, [])
        };
        var topicPartitionCounts = new Dictionary<string, int>
        {
            ["topic-a"] = 3,
            ["topic-b"] = 3
        };

        var result = assignor.Assign(members, topicPartitionCounts);

        // 6 total partitions, evenly distributed: 3 each
        await Assert.That(result["member-a"].Count).IsEqualTo(3);
        await Assert.That(result["member-b"].Count).IsEqualTo(3);
    }

    #endregion

    #region PartitionAssignors static class

    [Test]
    public async Task PartitionAssignors_Range_IsSingleton()
    {
        var a = PartitionAssignors.Range;
        var b = PartitionAssignors.Range;

        await Assert.That(a).IsSameReferenceAs(b);
    }

    [Test]
    public async Task PartitionAssignors_RoundRobin_IsSingleton()
    {
        var a = PartitionAssignors.RoundRobin;
        var b = PartitionAssignors.RoundRobin;

        await Assert.That(a).IsSameReferenceAs(b);
    }

    #endregion

    #region Builder integration

    [Test]
    public async Task ConsumerBuilder_WithPartitionAssignmentStrategy_Enum_ReturnsBuilderForChaining()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var returned = builder.WithPartitionAssignmentStrategy(PartitionAssignmentStrategy.RoundRobin);

        await Assert.That(returned).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerBuilder_WithPartitionAssignmentStrategy_Custom_ReturnsBuilderForChaining()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var returned = builder.WithPartitionAssignmentStrategy(new TestAssignmentStrategy());

        await Assert.That(returned).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerBuilder_WithPartitionAssignmentStrategy_Enum_ClearsCustom()
    {
        // Setting enum after custom should clear custom
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithPartitionAssignmentStrategy(new TestAssignmentStrategy())
            .WithPartitionAssignmentStrategy(PartitionAssignmentStrategy.Range);

        // Should build without error (custom cleared)
        var consumer = builder.Build();
        await Assert.That(consumer).IsNotNull();
        await consumer.DisposeAsync();
    }

    private sealed class TestAssignmentStrategy : IPartitionAssignmentStrategy
    {
        public string Name => "test";

        public IReadOnlyDictionary<string, IReadOnlyList<TopicPartition>> Assign(
            IReadOnlyList<ConsumerGroupMember> members,
            IReadOnlyDictionary<string, int> topicPartitionCounts)
        {
            var result = new Dictionary<string, IReadOnlyList<TopicPartition>>();
            foreach (var member in members)
            {
                result[member.MemberId] = [];
            }
            return result;
        }
    }

    #endregion
}
