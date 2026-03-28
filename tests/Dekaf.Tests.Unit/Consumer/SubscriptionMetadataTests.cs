using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public class SubscriptionMetadataTests
{
    [Test]
    public async Task RoundTrip_EmptyOwnedPartitions()
    {
        var topics = new HashSet<string> { "topic-a" };
        var ownedPartitions = new HashSet<TopicPartition>();

        var data = ConsumerCoordinator.BuildSubscriptionMetadata(topics, ownedPartitions);
        var (parsedTopics, parsedOwned) = ConsumerCoordinator.ParseSubscriptionMetadata(data);

        await Assert.That(parsedTopics).Contains("topic-a");
        await Assert.That(parsedTopics.Count).IsEqualTo(1);
        await Assert.That(parsedOwned).IsEmpty();
    }

    [Test]
    public async Task RoundTrip_WithOwnedPartitions()
    {
        var topics = new HashSet<string> { "topic-a" };
        var ownedPartitions = new HashSet<TopicPartition>
        {
            new("topic-a", 0),
            new("topic-a", 1),
            new("topic-a", 2)
        };

        var data = ConsumerCoordinator.BuildSubscriptionMetadata(topics, ownedPartitions);
        var (parsedTopics, parsedOwned) = ConsumerCoordinator.ParseSubscriptionMetadata(data);

        await Assert.That(parsedTopics).Contains("topic-a");
        await Assert.That(parsedTopics.Count).IsEqualTo(1);
        await Assert.That(parsedOwned.Count).IsEqualTo(3);
        await Assert.That(parsedOwned).Contains(new TopicPartition("topic-a", 0));
        await Assert.That(parsedOwned).Contains(new TopicPartition("topic-a", 1));
        await Assert.That(parsedOwned).Contains(new TopicPartition("topic-a", 2));
    }

    [Test]
    public async Task RoundTrip_MultipleTopics_WithOwnedPartitions()
    {
        var topics = new HashSet<string> { "topic-a", "topic-b", "topic-c" };
        var ownedPartitions = new HashSet<TopicPartition>
        {
            new("topic-a", 0),
            new("topic-a", 1),
            new("topic-b", 3),
            new("topic-c", 0)
        };

        var data = ConsumerCoordinator.BuildSubscriptionMetadata(topics, ownedPartitions);
        var (parsedTopics, parsedOwned) = ConsumerCoordinator.ParseSubscriptionMetadata(data);

        await Assert.That(parsedTopics.Count).IsEqualTo(3);
        await Assert.That(parsedTopics).Contains("topic-a");
        await Assert.That(parsedTopics).Contains("topic-b");
        await Assert.That(parsedTopics).Contains("topic-c");

        await Assert.That(parsedOwned.Count).IsEqualTo(4);
        await Assert.That(parsedOwned).Contains(new TopicPartition("topic-a", 0));
        await Assert.That(parsedOwned).Contains(new TopicPartition("topic-a", 1));
        await Assert.That(parsedOwned).Contains(new TopicPartition("topic-b", 3));
        await Assert.That(parsedOwned).Contains(new TopicPartition("topic-c", 0));
    }

    [Test]
    public async Task BackwardCompat_V0Metadata_ParsesWithEmptyOwned()
    {
        var topics = new HashSet<string> { "topic-a", "topic-b" };

        var data = ConsumerCoordinator.BuildSubscriptionMetadataV0(topics);
        var (parsedTopics, parsedOwned) = ConsumerCoordinator.ParseSubscriptionMetadata(data);

        await Assert.That(parsedTopics.Count).IsEqualTo(2);
        await Assert.That(parsedTopics).Contains("topic-a");
        await Assert.That(parsedTopics).Contains("topic-b");
        await Assert.That(parsedOwned).IsEmpty();
    }
}
