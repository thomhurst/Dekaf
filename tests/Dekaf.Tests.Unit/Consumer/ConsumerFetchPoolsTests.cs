using System.Collections.Concurrent;
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
    private static readonly MethodInfo BuildFetchRequestTopicsForConnectionMethod =
        typeof(KafkaConsumer<string, string>).GetMethod(
            "BuildFetchRequestTopicsForConnection",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find BuildFetchRequestTopicsForConnection");
    private static readonly FieldInfo FetchRequestTemplateCacheField =
        typeof(KafkaConsumer<string, string>).GetField(
            "_fetchRequestTemplateCache",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find _fetchRequestTemplateCache");
    private static readonly FieldInfo FetchPositionsField =
        typeof(KafkaConsumer<string, string>).GetField(
            "_fetchPositions",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find _fetchPositions");

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

    [Test]
    public async Task BuildFetchRequestTopics_RepeatedBuilds_RotatePartitionOrder()
    {
        await using var built = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("group-a")
            .Build();
        var consumer = (KafkaConsumer<string, string>)built;
        var partitions = CreateTemplateCachePartitions();

        var first = InvokeBuildFetchRequestTopics(consumer, partitions);
        var second = InvokeBuildFetchRequestTopics(consumer, partitions);
        var third = InvokeBuildFetchRequestTopics(consumer, partitions);
        var fourth = InvokeBuildFetchRequestTopics(consumer, partitions);
        try
        {
            await Assert.That(GetTopicPartitionOrder(first)).IsEqualTo("topic-a:0,topic-a:1,topic-b:0,topic-b:1");
            await Assert.That(GetTopicPartitionOrder(second)).IsEqualTo("topic-a:1,topic-a:0,topic-b:0,topic-b:1");
            await Assert.That(GetTopicPartitionOrder(third)).IsEqualTo("topic-b:0,topic-b:1,topic-a:0,topic-a:1");
            await Assert.That(GetTopicPartitionOrder(fourth)).IsEqualTo("topic-b:1,topic-b:0,topic-a:0,topic-a:1");
        }
        finally
        {
            ConsumerFetchPools.ReturnFetchRequestTopics(first);
            ConsumerFetchPools.ReturnFetchRequestTopics(second);
            ConsumerFetchPools.ReturnFetchRequestTopics(third);
            ConsumerFetchPools.ReturnFetchRequestTopics(fourth);
        }
    }

    [Test]
    public async Task BuildFetchRequestTopics_PreferredReplicaBroker_RotatesIndependently()
    {
        await using var built = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("group-a")
            .Build();
        var consumer = (KafkaConsumer<string, string>)built;
        var partitions = new List<TopicPartition>
        {
            new("topic-a", 0),
            new("topic-a", 1),
            new("topic-a", 2)
        };

        var brokerOneFirst = InvokeBuildFetchRequestTopics(consumer, partitions, brokerId: 1);
        var brokerOneSecond = InvokeBuildFetchRequestTopics(consumer, partitions, brokerId: 1);
        var brokerTwoFirst = InvokeBuildFetchRequestTopics(consumer, partitions, brokerId: 2);
        try
        {
            await Assert.That(GetPartitionOrder(brokerOneFirst)).IsEqualTo("0,1,2");
            await Assert.That(GetPartitionOrder(brokerOneSecond)).IsEqualTo("1,2,0");
            await Assert.That(GetPartitionOrder(brokerTwoFirst)).IsEqualTo("0,1,2");
        }
        finally
        {
            ConsumerFetchPools.ReturnFetchRequestTopics(brokerOneFirst);
            ConsumerFetchPools.ReturnFetchRequestTopics(brokerOneSecond);
            ConsumerFetchPools.ReturnFetchRequestTopics(brokerTwoFirst);
        }
    }

    [Test]
    public async Task BuildFetchRequestTopics_PrefetchConnections_RotateIndependently()
    {
        await using var built = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("group-a")
            .Build();
        var consumer = (KafkaConsumer<string, string>)built;
        var partitions = new List<TopicPartition>
        {
            new("topic-a", 0),
            new("topic-a", 1),
            new("topic-a", 2)
        };

        var connectionZeroFirst = InvokeBuildFetchRequestTopicsForConnection(consumer, partitions, connectionIndex: 0);
        var connectionZeroSecond = InvokeBuildFetchRequestTopicsForConnection(consumer, partitions, connectionIndex: 0);
        var connectionOneFirst = InvokeBuildFetchRequestTopicsForConnection(consumer, partitions, connectionIndex: 1);
        try
        {
            await Assert.That(GetPartitionOrder(connectionZeroFirst)).IsEqualTo("0,1,2");
            await Assert.That(GetPartitionOrder(connectionZeroSecond)).IsEqualTo("1,2,0");
            await Assert.That(GetPartitionOrder(connectionOneFirst)).IsEqualTo("0,1,2");
        }
        finally
        {
            ConsumerFetchPools.ReturnFetchRequestTopics(connectionZeroFirst);
            ConsumerFetchPools.ReturnFetchRequestTopics(connectionZeroSecond);
            ConsumerFetchPools.ReturnFetchRequestTopics(connectionOneFirst);
        }
    }

    [Test]
    public async Task BuildFetchRequestTopics_AssignmentChange_ContinuesRoundRobin()
    {
        await using var built = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("group-a")
            .Build();
        var consumer = (KafkaConsumer<string, string>)built;

        var original = InvokeBuildFetchRequestTopics(consumer,
        [
            new TopicPartition("topic-a", 0),
            new TopicPartition("topic-a", 1),
            new TopicPartition("topic-a", 2)
        ]);
        var reassigned = InvokeBuildFetchRequestTopics(consumer,
        [
            new TopicPartition("topic-b", 10),
            new TopicPartition("topic-b", 11)
        ]);
        try
        {
            await Assert.That(GetPartitionOrder(original)).IsEqualTo("0,1,2");
            await Assert.That(GetPartitionOrder(reassigned)).IsEqualTo("11,10");
        }
        finally
        {
            ConsumerFetchPools.ReturnFetchRequestTopics(original);
            ConsumerFetchPools.ReturnFetchRequestTopics(reassigned);
        }
    }

    [Test]
    public async Task BuildFetchRequestTopics_FetchSessionReset_UsesNextRotation()
    {
        await using var built = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("group-a")
            .Build();
        var consumer = (KafkaConsumer<string, string>)built;
        var partitions = new List<TopicPartition>
        {
            new("topic-a", 0),
            new("topic-a", 1),
            new("topic-a", 2)
        };
        var handler = new FetchSessionHandler();

        var initialTopics = InvokeBuildFetchRequestTopics(consumer, partitions);
        handler.Build(initialTopics, clusterMetadata: null);
        handler.HandleResponse(new FetchResponse { SessionId = 42, ErrorCode = ErrorCode.None, Responses = [] });
        ConsumerFetchPools.ReturnFetchRequestTopics(initialTopics);

        var incrementalTopics = InvokeBuildFetchRequestTopics(consumer, partitions);
        handler.Build(incrementalTopics, clusterMetadata: null);
        handler.HandleError();
        ConsumerFetchPools.ReturnFetchRequestTopics(incrementalTopics);

        var resetTopics = InvokeBuildFetchRequestTopics(consumer, partitions);
        try
        {
            var resetBuild = handler.Build(resetTopics, clusterMetadata: null);

            await Assert.That(resetBuild.IsFull).IsTrue();
            await Assert.That(GetPartitionOrder(resetBuild.Topics)).IsEqualTo("2,0,1");
        }
        finally
        {
            ConsumerFetchPools.ReturnFetchRequestTopics(resetTopics);
        }
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
    {
        var fetchPositions = (ConcurrentDictionary<TopicPartition, long>)FetchPositionsField.GetValue(consumer)!;
        for (var i = startIndex; i < startIndex + count; i++)
        {
            fetchPositions.TryAdd(partitions[i], 0);
        }

        return (List<FetchRequestTopic>)BuildFetchRequestTopicsMethod.Invoke(
            consumer,
            [partitions, startIndex, count, brokerId])!;
    }

    private static List<FetchRequestTopic> InvokeBuildFetchRequestTopicsForConnection(
        KafkaConsumer<string, string> consumer,
        List<TopicPartition> partitions,
        int connectionIndex,
        int brokerId = 1)
    {
        var fetchPositions = (ConcurrentDictionary<TopicPartition, long>)FetchPositionsField.GetValue(consumer)!;
        for (var i = 0; i < partitions.Count; i++)
            fetchPositions.TryAdd(partitions[i], 0);

        return (List<FetchRequestTopic>)BuildFetchRequestTopicsForConnectionMethod.Invoke(
            consumer,
            [partitions, 0, partitions.Count, brokerId, connectionIndex])!;
    }

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

    private static string GetPartitionOrder(IReadOnlyList<FetchRequestTopic> topics) =>
        string.Join(',', topics.SelectMany(static topic => topic.Partitions).Select(static partition => partition.Partition));

    private static string GetTopicPartitionOrder(IReadOnlyList<FetchRequestTopic> topics) =>
        string.Join(',', topics.SelectMany(static topic => topic.Partitions.Select(
            partition => $"{topic.Topic}:{partition.Partition}")));

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
