using Dekaf.Statistics;

namespace Dekaf.Tests.Unit.Statistics;

public class StatisticsModelTests
{
    [Test]
    public async Task ConsumerPartitionStatistics_Lag_ComputedCorrectly()
    {
        var stats = new ConsumerPartitionStatistics
        {
            Partition = 0,
            HighWatermark = 100,
            CommittedOffset = 80
        };

        await Assert.That(stats.Lag).IsEqualTo(20);
    }

    [Test]
    public async Task ConsumerPartitionStatistics_Lag_ReturnsNull_WhenHighWatermarkIsNull()
    {
        var stats = new ConsumerPartitionStatistics
        {
            Partition = 0,
            HighWatermark = null,
            CommittedOffset = 80
        };

        await Assert.That(stats.Lag).IsNull();
    }

    [Test]
    public async Task ConsumerPartitionStatistics_Lag_ReturnsNull_WhenCommittedOffsetIsNull()
    {
        var stats = new ConsumerPartitionStatistics
        {
            Partition = 0,
            HighWatermark = 100,
            CommittedOffset = null
        };

        await Assert.That(stats.Lag).IsNull();
    }

    [Test]
    public async Task ConsumerPartitionStatistics_Lag_ReturnsNull_WhenBothAreNull()
    {
        var stats = new ConsumerPartitionStatistics
        {
            Partition = 0,
            HighWatermark = null,
            CommittedOffset = null
        };

        await Assert.That(stats.Lag).IsNull();
    }

    [Test]
    public async Task ConsumerPartitionStatistics_Lag_CanBeZero()
    {
        var stats = new ConsumerPartitionStatistics
        {
            Partition = 0,
            HighWatermark = 100,
            CommittedOffset = 100
        };

        await Assert.That(stats.Lag).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumerPartitionStatistics_Lag_CanBeNegative_WhenAheadOfHighWatermark()
    {
        // This can happen during certain race conditions or rebalances
        var stats = new ConsumerPartitionStatistics
        {
            Partition = 0,
            HighWatermark = 50,
            CommittedOffset = 60
        };

        await Assert.That(stats.Lag).IsEqualTo(-10);
    }

    [Test]
    public async Task ProducerStatistics_HasDefaultEmptyDictionaries()
    {
        var stats = new ProducerStatistics
        {
            Timestamp = DateTimeOffset.UtcNow
        };

        await Assert.That(stats.Topics).IsNotNull();
        await Assert.That(stats.Brokers).IsNotNull();
        await Assert.That(stats.Topics).IsEmpty();
        await Assert.That(stats.Brokers).IsEmpty();
    }

    [Test]
    public async Task ConsumerStatistics_HasDefaultEmptyDictionaries()
    {
        var stats = new ConsumerStatistics
        {
            Timestamp = DateTimeOffset.UtcNow
        };

        await Assert.That(stats.Topics).IsNotNull();
        await Assert.That(stats.Brokers).IsNotNull();
        await Assert.That(stats.Topics).IsEmpty();
        await Assert.That(stats.Brokers).IsEmpty();
    }

    [Test]
    public async Task TopicStatistics_HasDefaultEmptyPartitions()
    {
        var stats = new TopicStatistics
        {
            Topic = "test"
        };

        await Assert.That(stats.Partitions).IsNotNull();
        await Assert.That(stats.Partitions).IsEmpty();
    }

    [Test]
    public async Task ConsumerTopicStatistics_HasDefaultEmptyPartitions()
    {
        var stats = new ConsumerTopicStatistics
        {
            Topic = "test"
        };

        await Assert.That(stats.Partitions).IsNotNull();
        await Assert.That(stats.Partitions).IsEmpty();
    }

    [Test]
    public async Task PartitionStatistics_RequiredPropertiesAreSet()
    {
        var stats = new PartitionStatistics
        {
            Partition = 5,
            MessagesProduced = 100,
            MessagesDelivered = 90,
            MessagesFailed = 5,
            BytesProduced = 10000,
            QueuedMessages = 5,
            LeaderNodeId = 1
        };

        await Assert.That(stats.Partition).IsEqualTo(5);
        await Assert.That(stats.MessagesProduced).IsEqualTo(100);
        await Assert.That(stats.MessagesDelivered).IsEqualTo(90);
        await Assert.That(stats.MessagesFailed).IsEqualTo(5);
        await Assert.That(stats.BytesProduced).IsEqualTo(10000);
        await Assert.That(stats.QueuedMessages).IsEqualTo(5);
        await Assert.That(stats.LeaderNodeId).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumerPartitionStatistics_RequiredPropertiesAreSet()
    {
        var stats = new ConsumerPartitionStatistics
        {
            Partition = 3,
            MessagesConsumed = 500,
            BytesConsumed = 50000,
            Position = 505,
            CommittedOffset = 500,
            HighWatermark = 600,
            LeaderNodeId = 2,
            IsPaused = true
        };

        await Assert.That(stats.Partition).IsEqualTo(3);
        await Assert.That(stats.MessagesConsumed).IsEqualTo(500);
        await Assert.That(stats.BytesConsumed).IsEqualTo(50000);
        await Assert.That(stats.Position).IsEqualTo(505);
        await Assert.That(stats.CommittedOffset).IsEqualTo(500);
        await Assert.That(stats.HighWatermark).IsEqualTo(600);
        await Assert.That(stats.LeaderNodeId).IsEqualTo(2);
        await Assert.That(stats.IsPaused).IsTrue();
        await Assert.That(stats.Lag).IsEqualTo(100);
    }
}
