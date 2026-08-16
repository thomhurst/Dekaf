using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerPauseResumeCacheTests
{
    private const string PrefetchedTopic = "prefetched-topic";

    private sealed class CallbackOnceDeserializer(Action callback) : IDeserializer<string>
    {
        private Action? _callback = callback;

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Interlocked.Exchange(ref _callback, null)?.Invoke();
            return Serializers.String.Deserialize(data, context);
        }
    }

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

    private static readonly FieldInfo PreferredReadReplicasField =
        typeof(KafkaConsumer<string, string>).GetField(
            "_preferredReadReplicas",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("_preferredReadReplicas field not found");

    [Test]
    public async Task PauseResume_WhenCachesAreEmpty_LeavesCachesEmpty()
    {
        var partition0 = new TopicPartition("topic-a", 0);
        var pool = Substitute.For<IConnectionPool>();

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        consumer.Assign(partition0);

        consumer.Pause(partition0);

        await Assert.That(GetCachedPartitionsByBroker(consumer)).IsNull();
        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(0);

        consumer.Resume(partition0);

        await Assert.That(GetCachedPartitionsByBroker(consumer)).IsNull();
        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(0);
    }

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
    public async Task Resume_WithPreferredReadReplica_InvalidatesPartitionBrokerCache()
    {
        var partition0 = new TopicPartition("topic-a", 0);
        var partition1 = new TopicPartition("topic-a", 1);
        var pool = Substitute.For<IConnectionPool>();

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        consumer.Assign(partition0, partition1);

        _ = await InvokeGroupPartitionsByBrokerAsync(consumer).ConfigureAwait(false);
        consumer.Pause(partition0);
        await Assert.That(GetCachedPartitionsByBroker(consumer)).IsNotNull();

        AddPreferredReadReplica(consumer, partition1, replicaId: 2);
        consumer.Resume(partition0);

        await Assert.That(GetCachedPartitionsByBroker(consumer)).IsNull();
    }

    [Test]
    public async Task Resume_WithUnknownLeader_InvalidatesPartitionBrokerCache()
    {
        var knownPartition = new TopicPartition("topic-a", 0);
        var missingLeaderPartition = new TopicPartition("topic-a", 99);
        var pool = Substitute.For<IConnectionPool>();

        await using var metadataManager = CreateMetadataManager(
            pool,
            new PartitionMetadataSeed("topic-a", 0, 1));
        await using var consumer = CreateConsumer(pool, metadataManager);
        consumer.Assign(knownPartition, missingLeaderPartition);

        consumer.Pause(missingLeaderPartition);
        _ = await InvokeGroupPartitionsByBrokerAsync(consumer).ConfigureAwait(false);
        await Assert.That(GetCachedPartitionsByBroker(consumer)).IsNotNull();

        consumer.Resume(missingLeaderPartition);

        await Assert.That(GetCachedPartitionsByBroker(consumer)).IsNull();
    }

    [Test]
    public async Task Pause_AllPartitionsInBrokerBucket_DropsBucketFromPartitionBrokerCache()
    {
        var partition0 = new TopicPartition("topic-a", 0);
        var partition1 = new TopicPartition("topic-a", 1);
        var pool = Substitute.For<IConnectionPool>();

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        consumer.Assign(partition0, partition1);

        _ = await InvokeGroupPartitionsByBrokerAsync(consumer).ConfigureAwait(false);

        consumer.Pause(partition0, partition1);

        var pausedCache = GetCachedPartitionsByBroker(consumer);
        await Assert.That(pausedCache).IsNotNull();
        await Assert.That(pausedCache!.Count).IsEqualTo(0);
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

    [Test]
    public async Task Pause_AllPartitionsInTopicBucket_ClearsFetchRequestTemplateCache()
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

        consumer.Pause(partition0, partition1);

        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(0);
    }

    [Test]
    public async Task PauseResume_WithMultipleBrokersAndTopics_UpdatesBrokerCacheAndInvalidatesFetchTemplateCache()
    {
        var topicA0 = new TopicPartition("topic-a", 0);
        var topicB0 = new TopicPartition("topic-b", 0);
        var topicB1 = new TopicPartition("topic-b", 1);
        var partitions = new List<TopicPartition> { topicA0, topicB0, topicB1 };
        var pool = Substitute.For<IConnectionPool>();

        await using var metadataManager = CreateMetadataManager(
            pool,
            new PartitionMetadataSeed("topic-a", 0, 1),
            new PartitionMetadataSeed("topic-b", 0, 2),
            new PartitionMetadataSeed("topic-b", 1, 2));
        await using var consumer = CreateConsumer(pool, metadataManager);
        consumer.Assign(topicA0, topicB0, topicB1);

        _ = await InvokeGroupPartitionsByBrokerAsync(consumer).ConfigureAwait(false);
        var fetchTopics = InvokeBuildFetchRequestTopics(consumer, partitions);
        ConsumerFetchPools.ReturnFetchRequestTopics(fetchTopics);
        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(1);

        consumer.Pause(topicB0);

        var pausedBrokerCache = GetCachedPartitionsByBroker(consumer);
        await Assert.That(pausedBrokerCache).IsNotNull();
        await Assert.That(pausedBrokerCache![1]).IsEquivalentTo(new[] { topicA0 });
        await Assert.That(pausedBrokerCache[2]).IsEquivalentTo(new[] { topicB1 });
        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(0);

        consumer.Resume(topicB0);

        var resumedBrokerCache = GetCachedPartitionsByBroker(consumer);
        await Assert.That(resumedBrokerCache).IsNotNull();
        await Assert.That(resumedBrokerCache![1]).IsEquivalentTo(new[] { topicA0 });
        await Assert.That(resumedBrokerCache[2]).IsEquivalentTo(new[] { topicB1, topicB0 });
        await Assert.That(GetFetchRequestTemplateCacheCount(consumer)).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumeAsync_PausedPrefetchedPartition_PreservesRecordsAndAllowsOtherPartition()
    {
        var pausedPartition = new TopicPartition(PrefetchedTopic, 0);
        var activePartition = new TopicPartition(PrefetchedTopic, 1);
        await using var consumer = CreatePrefetchedConsumer(
            CreatePendingFetch(pausedPartition, 10, 2),
            CreatePendingFetch(activePartition, 20, 1),
            CreatePendingFetch(pausedPartition, 12, 1));

        consumer.Pause(pausedPartition);
        await using var records = consumer.ConsumeAsync().GetAsyncEnumerator();

        await Assert.That(await records.MoveNextAsync()).IsTrue();
        await Assert.That(records.Current.Topic).IsEqualTo(activePartition.Topic);
        await Assert.That(records.Current.Partition).IsEqualTo(activePartition.Partition);

        consumer.Resume(pausedPartition);

        await Assert.That(await records.MoveNextAsync()).IsTrue();
        await Assert.That(records.Current.Topic).IsEqualTo(pausedPartition.Topic);
        await Assert.That(records.Current.Partition).IsEqualTo(pausedPartition.Partition);
        await Assert.That(records.Current.Offset).IsEqualTo(10);
        await Assert.That(await records.MoveNextAsync()).IsTrue();
        await Assert.That(records.Current.Topic).IsEqualTo(pausedPartition.Topic);
        await Assert.That(records.Current.Partition).IsEqualTo(pausedPartition.Partition);
        await Assert.That(records.Current.Offset).IsEqualTo(11);
        await Assert.That(await records.MoveNextAsync()).IsTrue();
        await Assert.That(records.Current.Offset).IsEqualTo(12);
    }

    [Test]
    public async Task ConsumeAsync_PauseDuringDeserialization_ReplaysSuppressedRecordAfterResume()
    {
        var pausedPartition = new TopicPartition(PrefetchedTopic, 0);
        var activePartition = new TopicPartition(PrefetchedTopic, 1);
        KafkaConsumer<string, string>? consumer = null;
        var deserializer = new CallbackOnceDeserializer(() => consumer!.Pause(pausedPartition));
        var createdConsumer = CreatePrefetchedConsumer(
            deserializer,
            CreatePendingFetch(pausedPartition, 10, 1),
            CreatePendingFetch(activePartition, 20, 1));
        consumer = createdConsumer;
        await using var ownedConsumer = createdConsumer;
        await using var records = createdConsumer.ConsumeAsync().GetAsyncEnumerator();

        await Assert.That(await records.MoveNextAsync()).IsTrue();
        await Assert.That(records.Current.Partition).IsEqualTo(activePartition.Partition);

        createdConsumer.Resume(pausedPartition);

        await Assert.That(await records.MoveNextAsync()).IsTrue();
        await Assert.That(records.Current.Partition).IsEqualTo(pausedPartition.Partition);
        await Assert.That(records.Current.Offset).IsEqualTo(10);
    }

    [Test]
    public async Task ConsumeAsync_RevocationDuringPrefetchWait_SuppressesRevokedPartition()
    {
        var revokedPartition = new TopicPartition(PrefetchedTopic, 0);
        var activePartition = new TopicPartition(PrefetchedTopic, 1);
        using var waitEntered = new ManualResetEventSlim();
        using var releaseWait = new ManualResetEventSlim();
        var prefetchBuffer = new MpscFetchBuffer(
            4,
            afterProducerWaiterCountIncrementedForTesting: null,
            beforeConsumerWaitSpinForTesting: () =>
            {
                waitEntered.Set();
                releaseWait.Wait();
            });
        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                QueuedMinMessages = 2
            },
            Serializers.String,
            Serializers.String);
        consumer.Assign([revokedPartition, activePartition]);
        GetField("_initialized").SetValue(consumer, true);
        GetField("_prefetchTask").SetValue(consumer, Task.CompletedTask);
        var fetchPositions = (ConcurrentDictionary<TopicPartition, long>)
            GetField("_fetchPositions").GetValue(consumer)!;
        fetchPositions[revokedPartition] = 0;
        fetchPositions[activePartition] = 0;
        var prefetchBufferField = GetField("_prefetchBuffer");
        var originalPrefetchBuffer = (MpscFetchBuffer)prefetchBufferField.GetValue(consumer)!;
        prefetchBufferField.SetValue(consumer, prefetchBuffer);
        originalPrefetchBuffer.Dispose();

        await using var ownedConsumer = consumer;
        await using var records = consumer.ConsumeAsync().GetAsyncEnumerator();
        var moveNext = Task.Run(() => records.MoveNextAsync().AsTask());
        var stagePendingClear = typeof(KafkaConsumer<string, string>).GetMethod(
            "StagePendingFetchClear",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StagePendingFetchClear method not found");

        try
        {
            await Assert.That(waitEntered.Wait(TimeSpan.FromSeconds(5))).IsTrue();
            stagePendingClear.Invoke(consumer, [revokedPartition]);
            await Assert.That(prefetchBuffer.TryWrite(CreatePendingFetch(revokedPartition, 10, 1))).IsTrue();
            await Assert.That(prefetchBuffer.TryWrite(CreatePendingFetch(activePartition, 20, 1))).IsTrue();
        }
        finally
        {
            releaseWait.Set();
        }

        await Assert.That(await moveNext.WaitAsync(TimeSpan.FromSeconds(5))).IsTrue();
        await Assert.That(records.Current.Topic).IsEqualTo(activePartition.Topic);
        await Assert.That(records.Current.Partition).IsEqualTo(activePartition.Partition);
        await Assert.That(records.Current.Offset).IsEqualTo(20);
    }

    [Test]
    public async Task ConsumeBatchAsync_PausedPrefetchedPartition_PreservesRecordsAndAllowsOtherPartition()
    {
        var pausedPartition = new TopicPartition(PrefetchedTopic, 0);
        var activePartition = new TopicPartition(PrefetchedTopic, 1);
        await using var consumer = CreatePrefetchedConsumer(
            CreatePendingFetch(pausedPartition, 10, 2),
            CreatePendingFetch(activePartition, 20, 1),
            CreatePendingFetch(pausedPartition, 12, 1));

        consumer.Pause(pausedPartition);
        await using var batches = consumer.ConsumeBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.TopicPartition).IsEqualTo(activePartition);
        _ = batches.Current.ToArray();

        consumer.Resume(pausedPartition);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.TopicPartition).IsEqualTo(pausedPartition);
        var resumedOffsets = batches.Current.Select(static record => record.Offset).ToArray();
        await Assert.That(resumedOffsets.Length).IsEqualTo(2);
        await Assert.That(resumedOffsets[0]).IsEqualTo(10);
        await Assert.That(resumedOffsets[1]).IsEqualTo(11);
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Single().Offset).IsEqualTo(12);
    }

    [Test]
    public async Task ConsumeBatchAsync_PauseDuringDeserialization_ReplaysSuppressedRecordAfterResume()
    {
        var pausedPartition = new TopicPartition(PrefetchedTopic, 0);
        var activePartition = new TopicPartition(PrefetchedTopic, 1);
        KafkaConsumer<string, string>? consumer = null;
        var deserializer = new CallbackOnceDeserializer(() => consumer!.Pause(pausedPartition));
        var createdConsumer = CreatePrefetchedConsumer(
            deserializer,
            CreatePendingFetch(pausedPartition, 10, 1),
            CreatePendingFetch(activePartition, 20, 1));
        consumer = createdConsumer;
        await using var ownedConsumer = createdConsumer;
        await using var batches = createdConsumer.ConsumeBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.ToArray()).IsEmpty();
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(activePartition.Partition);
        _ = batches.Current.ToArray();

        createdConsumer.Resume(pausedPartition);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Partition).IsEqualTo(pausedPartition.Partition);
        await Assert.That(batches.Current.Single().Offset).IsEqualTo(10);
    }

    [Test]
    public async Task ConsumeRawBatchAsync_PausedPrefetchedPartition_PreservesRecordsAndAllowsOtherPartition()
    {
        var pausedPartition = new TopicPartition(PrefetchedTopic, 0);
        var activePartition = new TopicPartition(PrefetchedTopic, 1);
        await using var consumer = CreatePrefetchedConsumer(
            CreatePendingFetch(pausedPartition, 10, 2),
            CreatePendingFetch(activePartition, 20, 1),
            CreatePendingFetch(pausedPartition, 12, 1));

        consumer.Pause(pausedPartition);
        await using var batches = consumer.ConsumeRawBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.TopicPartition).IsEqualTo(activePartition);
        _ = batches.Current.ToArray();

        consumer.Resume(pausedPartition);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.TopicPartition).IsEqualTo(pausedPartition);
        var resumedOffsets = batches.Current.Select(static record => record.Offset).ToArray();
        await Assert.That(resumedOffsets.Length).IsEqualTo(2);
        await Assert.That(resumedOffsets[0]).IsEqualTo(10);
        await Assert.That(resumedOffsets[1]).IsEqualTo(11);
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.Single().Offset).IsEqualTo(12);
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

    private static KafkaConsumer<string, string> CreatePrefetchedConsumer(params PendingFetchData[] fetches) =>
        CreatePrefetchedConsumer(Serializers.String, fetches);

    private static KafkaConsumer<string, string> CreatePrefetchedConsumer(
        IDeserializer<string> valueDeserializer,
        params PendingFetchData[] fetches)
    {
        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                QueuedMinMessages = 1
            },
            Serializers.String,
            valueDeserializer);
        var partitions = fetches
            .Select(static fetch => fetch.TopicPartition)
            .Distinct()
            .ToArray();
        consumer.Assign(partitions);

        GetField("_initialized").SetValue(consumer, true);
        var fetchPositions = (ConcurrentDictionary<TopicPartition, long>)
            GetField("_fetchPositions").GetValue(consumer)!;
        var pendingFetches = (Queue<PendingFetchData>)
            GetField("_pendingFetches").GetValue(consumer)!;

        foreach (var partition in partitions)
            fetchPositions[partition] = 0;
        foreach (var fetch in fetches)
            pendingFetches.Enqueue(fetch);
        GetField("_pendingFetchDepth").SetValue(consumer, fetches.Length);

        return consumer;
    }

    private static PendingFetchData CreatePendingFetch(
        TopicPartition partition,
        long baseOffset,
        int recordCount)
    {
        var records = new Record[recordCount];
        for (var i = 0; i < records.Length; i++)
        {
            records[i] = new Record
            {
                OffsetDelta = i,
                Key = Encoding.UTF8.GetBytes($"key-{i}"),
                Value = Encoding.UTF8.GetBytes($"value-{i}"),
                IsKeyNull = false,
                IsValueNull = false
            };
        }

        return PendingFetchData.Create(partition.Topic, partition.Partition,
        [
            new RecordBatch
            {
                BaseOffset = baseOffset,
                BaseTimestamp = 1_700_000_000_000,
                Records = records
            }
        ]);
    }

    private static FieldInfo GetField(string name) =>
        typeof(KafkaConsumer<string, string>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"{name} field not found");

    private static MetadataManager CreateMetadataManager(IConnectionPool pool)
        => CreateMetadataManager(
            pool,
            new PartitionMetadataSeed("topic-a", 0, 1),
            new PartitionMetadataSeed("topic-a", 1, 1));

    private static MetadataManager CreateMetadataManager(
        IConnectionPool pool,
        params PartitionMetadataSeed[] partitions)
    {
        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.SetApiVersion(ApiKey.Fetch, FetchRequest.LowestSupportedVersion, FetchRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            ClusterId = "test-cluster",
            ControllerId = 1,
            Brokers = partitions
                .Select(static partition => partition.LeaderId)
                .Distinct()
                .Order()
                .Select(static brokerId => new BrokerMetadata
                {
                    NodeId = brokerId,
                    Host = $"broker-{brokerId}",
                    Port = 9090 + brokerId
                })
                .ToArray(),
            Topics = partitions
                .GroupBy(static partition => partition.Topic)
                .Select(static topic => new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = topic.Key,
                    Partitions = topic
                        .Select(static partition => new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = partition.Partition,
                            LeaderId = partition.LeaderId,
                            LeaderEpoch = 5,
                            ReplicaNodes = [partition.LeaderId],
                            IsrNodes = [partition.LeaderId]
                        })
                        .ToArray()
                })
                .ToArray()
        });

        return metadataManager;
    }

    private static void AddPreferredReadReplica(
        KafkaConsumer<string, string> consumer,
        TopicPartition partition,
        int replicaId)
    {
        var replicas = PreferredReadReplicasField.GetValue(consumer)
            ?? throw new InvalidOperationException("_preferredReadReplicas was null");
        var stateType = replicas.GetType().GetGenericArguments()[1];
        var state = Activator.CreateInstance(
            stateType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [replicaId, DateTimeOffset.UtcNow, long.MaxValue],
            culture: null);

        var tryAdd = replicas.GetType().GetMethod("TryAdd")
            ?? throw new InvalidOperationException("TryAdd method not found");
        tryAdd.Invoke(replicas, [partition, state]);
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
    {
        var entry = CachedPartitionsByBrokerField.GetValue(consumer);
        if (entry is null)
            return null;

        var property = entry.GetType().GetProperty("PartitionsByBroker")
            ?? throw new InvalidOperationException("PartitionsByBroker property not found");
        return (Dictionary<int, List<TopicPartition>>)property.GetValue(entry)!;
    }

    private static int GetFetchRequestTemplateCacheCount(KafkaConsumer<string, string> consumer)
    {
        var cached = (System.Collections.ICollection)FetchRequestTemplateCacheField.GetValue(consumer)!;
        return cached.Count;
    }

    private readonly record struct PartitionMetadataSeed(string Topic, int Partition, int LeaderId);
}
