using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol;
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

    private static List<FetchRequestTopic> InvokeBuildFetchRequestTopics(
        KafkaConsumer<string, string> consumer,
        List<TopicPartition> partitions)
        => (List<FetchRequestTopic>)BuildFetchRequestTopicsMethod.Invoke(
            consumer,
            [partitions, 0, partitions.Count])!;

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
