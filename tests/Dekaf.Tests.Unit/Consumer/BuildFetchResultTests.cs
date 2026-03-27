using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for <see cref="KafkaConsumer{TKey, TValue}.BuildFetchResult"/> to verify
/// that concurrent callers receive independent FetchRequestPartition instances
/// with correct snapshot offsets.
/// </summary>
public class BuildFetchResultTests
{
    [Test]
    public async Task BuildFetchResult_ReturnsFreshPartitionObjects_NotSharedReferences()
    {
        var tp0 = new TopicPartition("topic-a", 0);
        var tp1 = new TopicPartition("topic-a", 1);

        var fetchPositions = new ConcurrentDictionary<TopicPartition, long>();
        fetchPositions[tp0] = 100;
        fetchPositions[tp1] = 200;

        var templateDict = new Dictionary<string, List<(FetchRequestPartition, TopicPartition)>>
        {
            ["topic-a"] =
            [
                (new FetchRequestPartition { Partition = 0, FetchOffset = 0, PartitionMaxBytes = 1_048_576 }, tp0),
                (new FetchRequestPartition { Partition = 1, FetchOffset = 0, PartitionMaxBytes = 1_048_576 }, tp1)
            ]
        };

        var result1 = KafkaConsumer<string, string>.BuildFetchResult(templateDict, fetchPositions);
        var result2 = KafkaConsumer<string, string>.BuildFetchResult(templateDict, fetchPositions);

        // Different list instances
        await Assert.That(result1).IsNotSameReferenceAs(result2);

        // Different FetchRequestPartition instances
        var partitions1 = result1[0].Partitions;
        var partitions2 = result2[0].Partitions;
        await Assert.That(partitions1[0]).IsNotSameReferenceAs(partitions2[0]);
        await Assert.That(partitions1[1]).IsNotSameReferenceAs(partitions2[1]);

        // Both have correct offsets from _fetchPositions
        await Assert.That(partitions1[0].FetchOffset).IsEqualTo(100);
        await Assert.That(partitions1[1].FetchOffset).IsEqualTo(200);
        await Assert.That(partitions2[0].FetchOffset).IsEqualTo(100);
        await Assert.That(partitions2[1].FetchOffset).IsEqualTo(200);
    }

    [Test]
    public async Task BuildFetchResult_ConcurrentCallsWithChangingPositions_EachGetsOwnSnapshot()
    {
        var tp0 = new TopicPartition("topic-a", 0);
        var tp1 = new TopicPartition("topic-a", 1);

        var fetchPositions = new ConcurrentDictionary<TopicPartition, long>();
        fetchPositions[tp0] = 100;
        fetchPositions[tp1] = 200;

        var templateDict = new Dictionary<string, List<(FetchRequestPartition, TopicPartition)>>
        {
            ["topic-a"] =
            [
                (new FetchRequestPartition { Partition = 0, FetchOffset = 0, PartitionMaxBytes = 1_048_576 }, tp0),
                (new FetchRequestPartition { Partition = 1, FetchOffset = 0, PartitionMaxBytes = 1_048_576 }, tp1)
            ]
        };

        // Call 1 captures offsets [100, 200]
        var result1 = KafkaConsumer<string, string>.BuildFetchResult(templateDict, fetchPositions);

        // Positions advance (simulating UpdateFetchPositionsFromPrefetch)
        fetchPositions[tp0] = 1100;
        fetchPositions[tp1] = 1200;

        // Call 2 captures offsets [1100, 1200]
        var result2 = KafkaConsumer<string, string>.BuildFetchResult(templateDict, fetchPositions);

        // Call 1 still has original offsets (init-only, cannot be mutated)
        await Assert.That(result1[0].Partitions[0].FetchOffset).IsEqualTo(100);
        await Assert.That(result1[0].Partitions[1].FetchOffset).IsEqualTo(200);

        // Call 2 has updated offsets
        await Assert.That(result2[0].Partitions[0].FetchOffset).IsEqualTo(1100);
        await Assert.That(result2[0].Partitions[1].FetchOffset).IsEqualTo(1200);
    }

    [Test]
    public async Task BuildFetchResult_MissingPosition_DefaultsToZero()
    {
        var tp0 = new TopicPartition("topic-a", 0);
        var fetchPositions = new ConcurrentDictionary<TopicPartition, long>();
        // tp0 not added to fetchPositions

        var templateDict = new Dictionary<string, List<(FetchRequestPartition, TopicPartition)>>
        {
            ["topic-a"] =
            [
                (new FetchRequestPartition { Partition = 0, FetchOffset = 0, PartitionMaxBytes = 1_048_576 }, tp0)
            ]
        };

        var result = KafkaConsumer<string, string>.BuildFetchResult(templateDict, fetchPositions);

        await Assert.That(result[0].Partitions[0].FetchOffset).IsEqualTo(0);
    }
}
