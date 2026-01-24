using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Unit tests for incremental assignment functionality.
/// </summary>
public class IncrementalAssignmentTests
{
    private static ConsumerOptions CreateTestOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "test-consumer"
    };

    [Test]
    public async Task IncrementalAssign_AddsPartitionsToEmptyAssignment()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        var partitions = new[]
        {
            new TopicPartitionOffset("topic1", 0, 100),
            new TopicPartitionOffset("topic1", 1, 200)
        };

        // Act
        consumer.IncrementalAssign(partitions);

        // Assert
        await Assert.That(consumer.Assignment).Count().IsEqualTo(2);
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("topic1", 0))).IsTrue();
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("topic1", 1))).IsTrue();
    }

    [Test]
    public async Task IncrementalAssign_AddsPartitionsToExistingAssignment()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        // First assignment
        consumer.Assign(new TopicPartition("topic1", 0));

        // Act - incremental assignment should add, not replace
        consumer.IncrementalAssign(new[]
        {
            new TopicPartitionOffset("topic1", 1, 0),
            new TopicPartitionOffset("topic2", 0, 0)
        });

        // Assert
        await Assert.That(consumer.Assignment).Count().IsEqualTo(3);
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("topic1", 0))).IsTrue();
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("topic1", 1))).IsTrue();
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("topic2", 0))).IsTrue();
    }

    [Test]
    public async Task IncrementalAssign_ClearsSubscription()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        consumer.Subscribe("topic1", "topic2");
        await Assert.That(consumer.Subscription).Count().IsEqualTo(2);

        // Act
        consumer.IncrementalAssign(new[] { new TopicPartitionOffset("topic1", 0, 0) });

        // Assert - subscription should be cleared when doing manual assignment
        await Assert.That(consumer.Subscription).Count().IsEqualTo(0);
        await Assert.That(consumer.Assignment).Count().IsEqualTo(1);
    }

    [Test]
    public async Task IncrementalAssign_SetsPositionWhenOffsetSpecified()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        var partitions = new[]
        {
            new TopicPartitionOffset("topic1", 0, 12345)
        };

        // Act
        consumer.IncrementalAssign(partitions);

        // Assert
        var position = consumer.GetPosition(new TopicPartition("topic1", 0));
        await Assert.That(position).IsEqualTo(12345);
    }

    [Test]
    public async Task IncrementalAssign_DoesNotSetPositionWhenOffsetNegative()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        // Using -1 to indicate "no specific offset" - should be initialized lazily
        var partitions = new[]
        {
            new TopicPartitionOffset("topic1", 0, -1)
        };

        // Act
        consumer.IncrementalAssign(partitions);

        // Assert - position should be null (not yet set, will be initialized lazily)
        var position = consumer.GetPosition(new TopicPartition("topic1", 0));
        await Assert.That(position).IsNull();
    }

    [Test]
    public async Task IncrementalAssign_DoesNotDuplicateExistingPartitions()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        consumer.Assign(new TopicPartition("topic1", 0));

        // Act - assign same partition again
        consumer.IncrementalAssign(new[] { new TopicPartitionOffset("topic1", 0, 500) });

        // Assert - should still have only 1 partition (set semantics)
        await Assert.That(consumer.Assignment).Count().IsEqualTo(1);

        // Position should be updated to the new offset
        var position = consumer.GetPosition(new TopicPartition("topic1", 0));
        await Assert.That(position).IsEqualTo(500);
    }

    [Test]
    public async Task IncrementalUnassign_RemovesSpecifiedPartitions()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        consumer.Assign(
            new TopicPartition("topic1", 0),
            new TopicPartition("topic1", 1),
            new TopicPartition("topic2", 0));

        // Act
        consumer.IncrementalUnassign(new[] { new TopicPartition("topic1", 1) });

        // Assert
        await Assert.That(consumer.Assignment).Count().IsEqualTo(2);
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("topic1", 0))).IsTrue();
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("topic1", 1))).IsFalse();
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("topic2", 0))).IsTrue();
    }

    [Test]
    public async Task IncrementalUnassign_RemovesMultiplePartitions()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        consumer.Assign(
            new TopicPartition("topic1", 0),
            new TopicPartition("topic1", 1),
            new TopicPartition("topic2", 0),
            new TopicPartition("topic2", 1));

        // Act
        consumer.IncrementalUnassign(new[]
        {
            new TopicPartition("topic1", 1),
            new TopicPartition("topic2", 0)
        });

        // Assert
        await Assert.That(consumer.Assignment).Count().IsEqualTo(2);
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("topic1", 0))).IsTrue();
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("topic2", 1))).IsTrue();
    }

    [Test]
    public async Task IncrementalUnassign_RemovesPausedState()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        consumer.Assign(
            new TopicPartition("topic1", 0),
            new TopicPartition("topic1", 1));

        consumer.Pause(new TopicPartition("topic1", 1));
        await Assert.That(consumer.Paused.Contains(new TopicPartition("topic1", 1))).IsTrue();

        // Act
        consumer.IncrementalUnassign(new[] { new TopicPartition("topic1", 1) });

        // Assert - paused state should be cleared for removed partition
        await Assert.That(consumer.Paused.Contains(new TopicPartition("topic1", 1))).IsFalse();
    }

    [Test]
    public async Task IncrementalUnassign_ClearsPositionForRemovedPartitions()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        consumer.IncrementalAssign(new[]
        {
            new TopicPartitionOffset("topic1", 0, 100),
            new TopicPartitionOffset("topic1", 1, 200)
        });

        // Verify positions are set
        await Assert.That(consumer.GetPosition(new TopicPartition("topic1", 0))).IsEqualTo(100);
        await Assert.That(consumer.GetPosition(new TopicPartition("topic1", 1))).IsEqualTo(200);

        // Act
        consumer.IncrementalUnassign(new[] { new TopicPartition("topic1", 1) });

        // Assert
        await Assert.That(consumer.GetPosition(new TopicPartition("topic1", 0))).IsEqualTo(100);
        await Assert.That(consumer.GetPosition(new TopicPartition("topic1", 1))).IsNull();
    }

    [Test]
    public async Task IncrementalUnassign_HandlesNonExistentPartitions()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        consumer.Assign(new TopicPartition("topic1", 0));

        // Act - try to unassign non-existent partition
        consumer.IncrementalUnassign(new[]
        {
            new TopicPartition("topic1", 99), // doesn't exist
            new TopicPartition("topic99", 0)  // doesn't exist
        });

        // Assert - should not throw, existing assignment unchanged
        await Assert.That(consumer.Assignment).Count().IsEqualTo(1);
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("topic1", 0))).IsTrue();
    }

    [Test]
    public async Task IncrementalUnassign_EmptyEnumerable_DoesNothing()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        consumer.Assign(
            new TopicPartition("topic1", 0),
            new TopicPartition("topic1", 1));

        // Act
        consumer.IncrementalUnassign(Array.Empty<TopicPartition>());

        // Assert
        await Assert.That(consumer.Assignment).Count().IsEqualTo(2);
    }

    [Test]
    public async Task IncrementalAssign_MethodChaining()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        // Act - verify method chaining returns the consumer
        var result = consumer
            .IncrementalAssign(new[] { new TopicPartitionOffset("topic1", 0, 0) })
            .IncrementalAssign(new[] { new TopicPartitionOffset("topic1", 1, 0) })
            .IncrementalAssign(new[] { new TopicPartitionOffset("topic2", 0, 0) });

        // Assert
        await Assert.That(result).IsEqualTo(consumer);
        await Assert.That(consumer.Assignment).Count().IsEqualTo(3);
    }

    [Test]
    public async Task IncrementalUnassign_MethodChaining()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        consumer.Assign(
            new TopicPartition("topic1", 0),
            new TopicPartition("topic1", 1),
            new TopicPartition("topic2", 0));

        // Act - verify method chaining returns the consumer
        var result = consumer
            .IncrementalUnassign(new[] { new TopicPartition("topic1", 0) })
            .IncrementalUnassign(new[] { new TopicPartition("topic1", 1) });

        // Assert
        await Assert.That(result).IsEqualTo(consumer);
        await Assert.That(consumer.Assignment).Count().IsEqualTo(1);
    }

    [Test]
    public async Task IncrementalAssign_AfterUnassign_WorksCorrectly()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        // Full assign
        consumer.Assign(
            new TopicPartition("topic1", 0),
            new TopicPartition("topic1", 1));

        // Full unassign
        consumer.Unassign();
        await Assert.That(consumer.Assignment).Count().IsEqualTo(0);

        // Act - incremental assign after full unassign
        consumer.IncrementalAssign(new[] { new TopicPartitionOffset("topic2", 0, 0) });

        // Assert
        await Assert.That(consumer.Assignment).Count().IsEqualTo(1);
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("topic2", 0))).IsTrue();
    }

    [Test]
    public async Task IncrementalUnassign_AllPartitions_LeavesEmptyAssignment()
    {
        // Arrange
        await using var consumer = new KafkaConsumer<string, string>(
            CreateTestOptions(),
            Serializers.String,
            Serializers.String);

        consumer.Assign(
            new TopicPartition("topic1", 0),
            new TopicPartition("topic1", 1));

        // Act
        consumer.IncrementalUnassign(new[]
        {
            new TopicPartition("topic1", 0),
            new TopicPartition("topic1", 1)
        });

        // Assert
        await Assert.That(consumer.Assignment).Count().IsEqualTo(0);
    }
}
