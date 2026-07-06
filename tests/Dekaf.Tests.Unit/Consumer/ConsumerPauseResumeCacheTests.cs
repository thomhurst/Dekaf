using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerPauseResumeCacheTests
{
    private static readonly MethodInfo BuildFetchRequestTopicsMethod =
        typeof(KafkaConsumer<string, string>).GetMethod(
            "BuildFetchRequestTopics",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("BuildFetchRequestTopics method not found");

    private static readonly MethodInfo GroupPartitionsByBrokerMethod =
        typeof(KafkaConsumer<string, string>).GetMethod(
            "GroupPartitionsByBrokerAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("GroupPartitionsByBrokerAsync method not found");

    private static readonly FieldInfo CachedPartitionsByBrokerField =
        typeof(KafkaConsumer<string, string>).GetField(
            "_cachedPartitionsByBroker",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("_cachedPartitionsByBroker field not found");

    private static readonly FieldInfo FetchRequestTemplateCacheField =
        typeof(KafkaConsumer<string, string>).GetField(
            "_fetchRequestTemplateCache",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("_fetchRequestTemplateCache field not found");

    [Test]
    public async Task PauseResume_UpdatesPartitionBrokerCacheIncrementally()
    {
        var partition0 = new TopicPartition("topic-a", 0);
        var partition1 = new TopicPartition("topic-a", 1);
        var pool = Substitute.For<IConnectionPool>();

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        consumer.Assign(partition0, partition1);

        var groups = await InvokeGroupPartitionsByBrokerAsync(consumer).ConfigureAwait(false);
        await Assert.That(groups[1].Count).IsEqualTo(2);

        consumer.Pause(partition0);

        var pausedCache = GetCachedPartitionsByBroker(consumer);
        await Assert.That(pausedCache).IsNotNull();
        await Assert.That(pausedCache![1].Count).IsEqualTo(1);
        await Assert.That(pausedCache[1].Contains(partition1)).IsTrue();
        await Assert.That(pausedCache[1].Contains(partition0)).IsFalse();

        consumer.Resume(partition0);

        var resumedCache = GetCachedPartitionsByBroker(consumer);
        await Assert.That(resumedCache).IsNotNull();
        await Assert.That(resumedCache![1].Count).IsEqualTo(2);
        await Assert.That(resumedCache[1].Contains(partition0)).IsTrue();
        await Assert.That(resumedCache[1].Contains(partition1)).IsTrue();
    }

    [Test]
    public async Task PauseResume_InvalidatesFetchRequestTemplateCache()
    {
        var partition0 = new TopicPartition("topic-a", 0);
        var partition1 = new TopicPartition("topic-a", 1);
        var partitions = new List<TopicPartition> { partition0, partition1 };
        var pool = Substitute.For<IConnectionPool>();

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        consumer.Assign(partition0, partition1);

        var fetchTopics = InvokeBuildFetchRequestTopics(consumer, partitions);
        ConsumerFetchPools.ReturnFetchRequestTopics(fetchTopics);
        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(1);

        consumer.Pause(partition0);

        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(0);
        var pausedFetchTopics = InvokeBuildFetchRequestTopics(consumer, [partition1]);
        ConsumerFetchPools.ReturnFetchRequestTopics(pausedFetchTopics);
        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(1);

        consumer.Resume(partition0);

        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(0);
    }

    private static KafkaConsumer<string, string> CreateConsumer(
        IConnectionPool pool,
        MetadataManager metadataManager)
        => new(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "test-consumer",
                EnableFetchSessions = false
            },
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager);

    private static MetadataManager CreateMetadataManager(IConnectionPool pool)
    {
        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.SetApiVersion(ApiKey.Fetch, FetchRequest.LowestSupportedVersion, FetchRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            ClusterId = "test-cluster",
            ControllerId = 1,
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9092 }
            ],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = "topic-a",
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 1,
                            LeaderEpoch = 5,
                            ReplicaNodes = [1],
                            IsrNodes = [1]
                        },
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 1,
                            LeaderId = 1,
                            LeaderEpoch = 5,
                            ReplicaNodes = [1],
                            IsrNodes = [1]
                        }
                    ]
                }
            ]
        });

        return metadataManager;
    }

    private static async ValueTask<Dictionary<int, List<TopicPartition>>> InvokeGroupPartitionsByBrokerAsync(
        KafkaConsumer<string, string> consumer)
    {
        var result = GroupPartitionsByBrokerMethod.Invoke(consumer, [CancellationToken.None]);
        if (result is not ValueTask<Dictionary<int, List<TopicPartition>>> valueTask)
            throw new InvalidOperationException("GroupPartitionsByBrokerAsync returned unexpected type");

        return await valueTask.ConfigureAwait(false);
    }

    private static List<FetchRequestTopic> InvokeBuildFetchRequestTopics(
        KafkaConsumer<string, string> consumer,
        List<TopicPartition> partitions)
        => (List<FetchRequestTopic>)BuildFetchRequestTopicsMethod.Invoke(
            consumer,
            [partitions, 0, partitions.Count, 1])!;

    private static Dictionary<int, List<TopicPartition>>? GetCachedPartitionsByBroker(
        KafkaConsumer<string, string> consumer)
        => (Dictionary<int, List<TopicPartition>>?)CachedPartitionsByBrokerField.GetValue(consumer);

    private static int GetFetchRequestTemplateCacheCount(KafkaConsumer<string, string> consumer)
    {
        var cached = (System.Collections.ICollection)FetchRequestTemplateCacheField.GetValue(consumer)!;
        return cached.Count;
    }
}
