using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerFetchPoolsTests
{
    private static readonly MethodInfo BuildFetchRequestTopicsMethod =
        typeof(KafkaConsumer<string, string>).GetMethod(
            "BuildFetchRequestTopics",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find BuildFetchRequestTopics");
    private static readonly FieldInfo FetchRequestTemplateCacheField =
        typeof(KafkaConsumer<string, string>).GetField(
            "_fetchRequestTemplateCache",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find _fetchRequestTemplateCache");

    [Test]
    public async Task ReturnedPendingFetchDataLists_AreClearedBeforeRent()
    {
        var pending = PendingFetchData.Create("topic-a", 0, Array.Empty<RecordBatch>());
        var list = ConsumerFetchPools.RentPendingFetchDataList();
        try
        {
            list.Add(pending);

            ConsumerFetchPools.ReturnPendingFetchDataList(list);

            var returned = ConsumerFetchPools.RentPendingFetchDataList();
            try
            {
                await Assert.That(returned.Count).IsEqualTo(0);
            }
            finally
            {
                ConsumerFetchPools.ReturnPendingFetchDataList(returned);
            }
        }
        finally
        {
            pending.Dispose();
        }
    }

    [Test]
    public async Task ReturnedFetchRequestTopicLists_ClearNestedPartitionLists()
    {
        var partitions = ConsumerFetchPools.RentFetchRequestPartitionList(1);
        partitions.Add(new FetchRequestPartition
        {
            Partition = 0,
            FetchOffset = 10,
            PartitionMaxBytes = 1024
        });

        var topics = ConsumerFetchPools.RentFetchRequestTopicList(1);
        topics.Add(new FetchRequestTopic
        {
            Topic = "topic-a",
            Partitions = partitions
        });

        ConsumerFetchPools.ReturnFetchRequestTopics(topics);

        await AssertFetchPoolsRentClearedLists();
    }

    [Test]
    public async Task BuildFetchRequestTopics_CacheMissAndCacheHit_ReturnPooledLists()
    {
        await using var built = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("group-a")
            .Build();
        var consumer = (KafkaConsumer<string, string>)built;
        var partitions = new List<TopicPartition>
        {
            new("topic-a", 0),
            new("topic-a", 1)
        };

        var cacheMissResult = InvokeBuildFetchRequestTopics(consumer, partitions);
        await Assert.That(cacheMissResult[0].Partitions).IsTypeOf<List<FetchRequestPartition>>();
        ConsumerFetchPools.ReturnFetchRequestTopics(cacheMissResult);
        await AssertFetchPoolsRentClearedLists();

        var cacheHitResult = InvokeBuildFetchRequestTopics(consumer, partitions);
        await Assert.That(cacheHitResult[0].Partitions).IsTypeOf<List<FetchRequestPartition>>();
        ConsumerFetchPools.ReturnFetchRequestTopics(cacheHitResult);
        await AssertFetchPoolsRentClearedLists();
    }

    [Test]
    public async Task BuildFetchRequestTopics_SubrangeCacheHit_ReturnPooledLists()
    {
        await using var built = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("group-a")
            .Build();
        var consumer = (KafkaConsumer<string, string>)built;
        var partitions = CreateTemplateCachePartitions();

        var cacheMissResult = InvokeBuildFetchRequestTopics(consumer, partitions, startIndex: 1, count: 2);
        await Assert.That(cacheMissResult.Sum(static topic => topic.Partitions.Count)).IsEqualTo(2);
        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(1);
        ConsumerFetchPools.ReturnFetchRequestTopics(cacheMissResult);
        await AssertFetchPoolsRentClearedLists();

        var cacheHitResult = InvokeBuildFetchRequestTopics(consumer, partitions, startIndex: 1, count: 2);
        await Assert.That(cacheHitResult.Sum(static topic => topic.Partitions.Count)).IsEqualTo(2);
        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(1);
        await Assert.That(cacheHitResult[0].Partitions).IsTypeOf<List<FetchRequestPartition>>();
        ConsumerFetchPools.ReturnFetchRequestTopics(cacheHitResult);
        await AssertFetchPoolsRentClearedLists();
    }

    [Test]
    public async Task BuildFetchRequestTopics_DifferentSubranges_CacheSeparateTemplates()
    {
        await using var built = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("group-a")
            .Build();
        var consumer = (KafkaConsumer<string, string>)built;
        var partitions = CreateTemplateCachePartitions();

        var firstRange = InvokeBuildFetchRequestTopics(consumer, partitions, startIndex: 0, count: 2);
        ConsumerFetchPools.ReturnFetchRequestTopics(firstRange);

        var secondRange = InvokeBuildFetchRequestTopics(consumer, partitions, startIndex: 2, count: 2);
        ConsumerFetchPools.ReturnFetchRequestTopics(secondRange);

        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(2);
    }

    [Test]
    public async Task BuildFetchRequestTopics_DifferentBrokersWithSameShape_CacheSeparateTemplates()
    {
        await using var built = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("group-a")
            .Build();
        var consumer = (KafkaConsumer<string, string>)built;
        var brokerOnePartitions = new List<TopicPartition>
        {
            new("topic-a", 0),
            new("topic-a", 1)
        };
        var brokerTwoPartitions = new List<TopicPartition>
        {
            new("topic-b", 0),
            new("topic-b", 1)
        };

        var brokerOneResult = InvokeBuildFetchRequestTopics(consumer, brokerOnePartitions, brokerId: 1);
        ConsumerFetchPools.ReturnFetchRequestTopics(brokerOneResult);

        var brokerTwoResult = InvokeBuildFetchRequestTopics(consumer, brokerTwoPartitions, brokerId: 2);
        ConsumerFetchPools.ReturnFetchRequestTopics(brokerTwoResult);

        var brokerOneHit = InvokeBuildFetchRequestTopics(consumer, brokerOnePartitions, brokerId: 1);
        ConsumerFetchPools.ReturnFetchRequestTopics(brokerOneHit);

        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(2);
    }

    private static List<FetchRequestTopic> InvokeBuildFetchRequestTopics(
        KafkaConsumer<string, string> consumer,
        List<TopicPartition> partitions)
        => InvokeBuildFetchRequestTopics(consumer, partitions, 0, partitions.Count, brokerId: 1);

    private static List<FetchRequestTopic> InvokeBuildFetchRequestTopics(
        KafkaConsumer<string, string> consumer,
        List<TopicPartition> partitions,
        int brokerId)
        => InvokeBuildFetchRequestTopics(consumer, partitions, 0, partitions.Count, brokerId);

    private static List<FetchRequestTopic> InvokeBuildFetchRequestTopics(
        KafkaConsumer<string, string> consumer,
        List<TopicPartition> partitions,
        int startIndex,
        int count,
        int brokerId = 1)
        => (List<FetchRequestTopic>)BuildFetchRequestTopicsMethod.Invoke(
            consumer,
            [partitions, startIndex, count, brokerId])!;

    private static int GetFetchRequestTemplateCacheCount(KafkaConsumer<string, string> consumer)
    {
        var cache = FetchRequestTemplateCacheField.GetValue(consumer)
            ?? throw new InvalidOperationException("Fetch request template cache is null");
        return ((System.Collections.ICollection)cache).Count;
    }

    private static List<TopicPartition> CreateTemplateCachePartitions() =>
    [
        new("topic-a", 0),
        new("topic-a", 1),
        new("topic-b", 0),
        new("topic-b", 1)
    ];

    private static async Task AssertFetchPoolsRentClearedLists()
    {
        var topics = ConsumerFetchPools.RentFetchRequestTopicList(0);
        var partitions = ConsumerFetchPools.RentFetchRequestPartitionList(0);

        try
        {
            await Assert.That(topics.Count).IsEqualTo(0);
            await Assert.That(partitions.Count).IsEqualTo(0);
        }
        finally
        {
            topics.Add(new FetchRequestTopic
            {
                Topic = "topic-a",
                Partitions = partitions
            });
            ConsumerFetchPools.ReturnFetchRequestTopics(topics);
        }
    }
}
