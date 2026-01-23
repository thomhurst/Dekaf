using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Consumer;

public class TopicPartitionTimestampTests
{
    [Test]
    public async Task TopicPartitionTimestamp_Constructor_SetsProperties()
    {
        var tpt = new TopicPartitionTimestamp("my-topic", 3, 1234567890L);

        await Assert.That(tpt.Topic).IsEqualTo("my-topic");
        await Assert.That(tpt.Partition).IsEqualTo(3);
        await Assert.That(tpt.Timestamp).IsEqualTo(1234567890L);
    }

    [Test]
    public async Task TopicPartitionTimestamp_FromTopicPartition_SetsProperties()
    {
        var tp = new TopicPartition("my-topic", 5);
        var tpt = new TopicPartitionTimestamp(tp, 9876543210L);

        await Assert.That(tpt.Topic).IsEqualTo("my-topic");
        await Assert.That(tpt.Partition).IsEqualTo(5);
        await Assert.That(tpt.Timestamp).IsEqualTo(9876543210L);
    }

    [Test]
    public async Task TopicPartitionTimestamp_FromDateTimeOffset_ConvertsToUnixMilliseconds()
    {
        var tp = new TopicPartition("my-topic", 2);
        var timestamp = new DateTimeOffset(2024, 6, 15, 12, 30, 45, TimeSpan.Zero);
        var expectedMillis = timestamp.ToUnixTimeMilliseconds();

        var tpt = new TopicPartitionTimestamp(tp, timestamp);

        await Assert.That(tpt.Topic).IsEqualTo("my-topic");
        await Assert.That(tpt.Partition).IsEqualTo(2);
        await Assert.That(tpt.Timestamp).IsEqualTo(expectedMillis);
    }

    [Test]
    public async Task TopicPartitionTimestamp_TopicPartitionProperty_ReturnsCorrectValue()
    {
        var tpt = new TopicPartitionTimestamp("my-topic", 7, 123L);

        var tp = tpt.TopicPartition;

        await Assert.That(tp.Topic).IsEqualTo("my-topic");
        await Assert.That(tp.Partition).IsEqualTo(7);
    }

    [Test]
    public async Task TopicPartitionTimestamp_LatestConstant_HasCorrectValue()
    {
        var latest = TopicPartitionTimestamp.Latest;
        await Assert.That(latest).IsEqualTo(-1L);
    }

    [Test]
    public async Task TopicPartitionTimestamp_EarliestConstant_HasCorrectValue()
    {
        var earliest = TopicPartitionTimestamp.Earliest;
        await Assert.That(earliest).IsEqualTo(-2L);
    }

    [Test]
    public async Task TopicPartitionTimestamp_WithLatestTimestamp_Works()
    {
        var tpt = new TopicPartitionTimestamp("my-topic", 0, TopicPartitionTimestamp.Latest);

        await Assert.That(tpt.Timestamp).IsEqualTo(-1L);
    }

    [Test]
    public async Task TopicPartitionTimestamp_WithEarliestTimestamp_Works()
    {
        var tpt = new TopicPartitionTimestamp("my-topic", 0, TopicPartitionTimestamp.Earliest);

        await Assert.That(tpt.Timestamp).IsEqualTo(-2L);
    }

    [Test]
    public async Task TopicPartitionTimestamp_Equality_WorksCorrectly()
    {
        var tpt1 = new TopicPartitionTimestamp("topic", 1, 100L);
        var tpt2 = new TopicPartitionTimestamp("topic", 1, 100L);
        var tpt3 = new TopicPartitionTimestamp("topic", 1, 200L);
        var tpt4 = new TopicPartitionTimestamp("other-topic", 1, 100L);

        await Assert.That(tpt1).IsEqualTo(tpt2);
        await Assert.That(tpt1).IsNotEqualTo(tpt3);
        await Assert.That(tpt1).IsNotEqualTo(tpt4);
    }

    [Test]
    public async Task TopicPartitionTimestamp_HashCode_ConsistentWithEquality()
    {
        var tpt1 = new TopicPartitionTimestamp("topic", 1, 100L);
        var tpt2 = new TopicPartitionTimestamp("topic", 1, 100L);

        await Assert.That(tpt1.GetHashCode()).IsEqualTo(tpt2.GetHashCode());
    }
}
