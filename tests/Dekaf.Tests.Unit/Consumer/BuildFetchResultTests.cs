using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for <see cref="KafkaConsumer{TKey, TValue}.BuildFetchResult"/> to verify
/// that concurrent callers receive independent FetchRequestPartition instances
/// with correct snapshot offsets.
/// </summary>
public class BuildFetchResultTests
{
    private static readonly int[] ExpectedForgottenPartitions = [0, 2];
    private static readonly int[] ExpectedSingleForgottenPartition = [0];

    private static (Dictionary<string, List<(FetchRequestPartition, TopicPartition)>> Template, TopicPartition Tp)
        CreateSinglePartitionTemplate(string topic = "topic-a", int partition = 0)
    {
        var tp = new TopicPartition(topic, partition);
        var dict = new Dictionary<string, List<(FetchRequestPartition, TopicPartition)>>
        {
            [topic] =
            [
                (new FetchRequestPartition { Partition = partition, FetchOffset = 0, PartitionMaxBytes = 1_048_576 }, tp)
            ]
        };
        return (dict, tp);
    }

    private static ClusterMetadata CreateClusterMetadata(string topic, Guid topicId, int leaderEpoch = -1)
    {
        var clusterMetadata = new ClusterMetadata();
        clusterMetadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            Topics =
            [
                new TopicMetadata
                {
                    Name = topic,
                    TopicId = topicId,
                    ErrorCode = ErrorCode.None,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 1,
                            LeaderEpoch = leaderEpoch,
                            ReplicaNodes = [1],
                            IsrNodes = [1]
                        }
                    ]
                }
            ]
        });

        return clusterMetadata;
    }

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
        var (templateDict, _) = CreateSinglePartitionTemplate();
        var fetchPositions = new ConcurrentDictionary<TopicPartition, long>();

        var result = KafkaConsumer<string, string>.BuildFetchResult(templateDict, fetchPositions);

        await Assert.That(result[0].Partitions[0].FetchOffset).IsEqualTo(0);
    }

    [Test]
    public async Task BuildFetchResult_WithClusterMetadata_PopulatesTopicId()
    {
        var topicId = Guid.NewGuid();
        var (templateDict, tp0) = CreateSinglePartitionTemplate();
        var fetchPositions = new ConcurrentDictionary<TopicPartition, long> { [tp0] = 42 };

        var clusterMetadata = CreateClusterMetadata("topic-a", topicId);

        var result = KafkaConsumer<string, string>.BuildFetchResult(templateDict, fetchPositions, clusterMetadata: clusterMetadata);

        await Assert.That(result[0].TopicId).IsEqualTo(topicId);
    }

    [Test]
    public async Task BuildFetchResult_WithLeaderEpochState_PopulatesFetchEpochs()
    {
        var topicId = Guid.NewGuid();
        var (templateDict, tp0) = CreateSinglePartitionTemplate();
        var fetchPositions = new ConcurrentDictionary<TopicPartition, long> { [tp0] = 42 };
        var lastConsumedLeaderEpochs = new ConcurrentDictionary<TopicPartition, int> { [tp0] = 8 };
        var clusterMetadata = CreateClusterMetadata("topic-a", topicId, leaderEpoch: 6);

        var result = KafkaConsumer<string, string>.BuildFetchResult(
            templateDict,
            fetchPositions,
            clusterMetadata: clusterMetadata,
            lastConsumedLeaderEpochs: lastConsumedLeaderEpochs);

        var partition = result[0].Partitions[0];
        await Assert.That(partition.CurrentLeaderEpoch).IsEqualTo(6);
        await Assert.That(partition.LastFetchedEpoch).IsEqualTo(8);
    }

    [Test]
    public async Task BuildFetchResult_WithClusterMetadata_TopicNotInMetadata_ReturnsEmptyGuid()
    {
        var (templateDict, tp0) = CreateSinglePartitionTemplate("unknown-topic");
        var fetchPositions = new ConcurrentDictionary<TopicPartition, long> { [tp0] = 0 };

        var result = KafkaConsumer<string, string>.BuildFetchResult(templateDict, fetchPositions, clusterMetadata: new ClusterMetadata());

        await Assert.That(result[0].TopicId).IsEqualTo(Guid.Empty);
    }

    [Test]
    public async Task BuildFetchResult_NullClusterMetadata_ReturnsEmptyGuid()
    {
        var (templateDict, tp0) = CreateSinglePartitionTemplate();
        var fetchPositions = new ConcurrentDictionary<TopicPartition, long> { [tp0] = 0 };

        var result = KafkaConsumer<string, string>.BuildFetchResult(templateDict, fetchPositions, clusterMetadata: null);

        await Assert.That(result[0].TopicId).IsEqualTo(Guid.Empty);
    }

    [Test]
    public async Task BuildForgottenTopicsData_WithClusterMetadata_PopulatesTopicId()
    {
        var topicId = Guid.NewGuid();
        var forgottenPartitions = new Dictionary<string, List<int>>
        {
            ["topic-a"] = [0, 2]
        };
        var clusterMetadata = CreateClusterMetadata("topic-a", topicId);

        var result = KafkaConsumer<string, string>.BuildForgottenTopicsData(forgottenPartitions, clusterMetadata);

        await Assert.That(result[0].Topic).IsEqualTo("topic-a");
        await Assert.That(result[0].TopicId).IsEqualTo(topicId);
        await Assert.That(result[0].Partitions).IsEquivalentTo(ExpectedForgottenPartitions);
    }

    [Test]
    public async Task BuildForgottenTopicsData_WithMissingMetadata_ReturnsEmptyGuid()
    {
        var forgottenPartitions = new Dictionary<string, List<int>>
        {
            ["unknown-topic"] = [1]
        };

        var result = KafkaConsumer<string, string>.BuildForgottenTopicsData(forgottenPartitions, new ClusterMetadata());

        await Assert.That(result[0].TopicId).IsEqualTo(Guid.Empty);
    }

    [Test]
    public async Task BuildForgottenTopicsData_CopiesPartitions()
    {
        var partitions = new List<int> { 0 };
        var forgottenPartitions = new Dictionary<string, List<int>>
        {
            ["topic-a"] = partitions
        };

        var result = KafkaConsumer<string, string>.BuildForgottenTopicsData(forgottenPartitions);
        partitions.Add(1);

        await Assert.That(result[0].Partitions).IsEquivalentTo(ExpectedSingleForgottenPartition);
    }
}
