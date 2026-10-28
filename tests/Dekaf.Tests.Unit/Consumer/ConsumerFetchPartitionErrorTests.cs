using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Fetch partition errors without a dedicated recovery path (#3718): non-retriable codes end the
/// consume stream, and retriable codes back the partition off and refresh its metadata instead
/// of refetching it in a tight loop.
/// </summary>
public sealed class ConsumerFetchPartitionErrorTests
{
    private const string Topic = "test-topic";
    private static readonly TopicPartition Partition0 = new(Topic, 0);
    private static readonly TopicPartition Partition1 = new(Topic, 1);

    [Test]
    [Arguments(ErrorCode.TopicAuthorizationFailed, true)]
    [Arguments(ErrorCode.UnsupportedCompressionType, true)]
    [Arguments(ErrorCode.UnknownServerError, false)]
    [Arguments(ErrorCode.KafkaStorageError, false)]
    [Arguments(ErrorCode.ReplicaNotAvailable, false)]
    [Arguments(ErrorCode.OffsetNotAvailable, false)]
    [Arguments(ErrorCode.CorruptMessage, false)]
    public async Task IsFatalFetchPartitionError_ClassifiesByRetriability(ErrorCode errorCode, bool expected)
    {
        await Assert.That(KafkaConsumer<string, string>.IsFatalFetchPartitionError(errorCode)).IsEqualTo(expected);
    }

    [Test]
    public async Task FetchFromBroker_TopicAuthorizationFailed_ThrowsFatalAuthorizationException()
    {
        using var harness = new Harness(ErrorCode.TopicAuthorizationFailed);
        await using var metadataManager = harness.CreateMetadataManager();
        await using var consumer = harness.CreateConsumer(metadataManager);

        var exception = await Assert.That(async () => await InvokeFetchFromBrokerAsync(consumer))
            .Throws<AuthorizationException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(exception)).IsTrue();
    }

    [Test]
    public async Task PrefetchFromBroker_TopicAuthorizationFailed_ThrowsFatalAuthorizationException()
    {
        using var harness = new Harness(ErrorCode.TopicAuthorizationFailed);
        await using var metadataManager = harness.CreateMetadataManager();
        await using var consumer = harness.CreateConsumer(metadataManager);

        var exception = await Assert.That(async () => await InvokePrefetchFromBrokerAsync(consumer))
            .Throws<AuthorizationException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await Assert.That(KafkaConsumer<string, string>.IsFatalPrefetchError(exception)).IsTrue();
    }

    [Test]
    [Arguments(ErrorCode.KafkaStorageError)]
    [Arguments(ErrorCode.ReplicaNotAvailable)]
    [Arguments(ErrorCode.OffsetNotAvailable)]
    [Arguments(ErrorCode.UnknownServerError)]
    public async Task FetchFromBroker_RetriableError_BacksOffPartitionAndRefreshesMetadata(ErrorCode errorCode)
    {
        using var harness = new Harness(errorCode);
        await using var metadataManager = harness.CreateMetadataManager();
        await using var consumer = harness.CreateConsumer(metadataManager);

        await AssertBothPartitionsGrouped(consumer);

        var pendingItems = await InvokeFetchFromBrokerAsync(consumer);
        DisposeAndReturn(pendingItems);

        // The errored partition leaves the fetch requests; the healthy one keeps fetching.
        var grouped = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(grouped[1]).IsEquivalentTo([Partition1]);

        // The topic's metadata refresh starts in the background.
        await harness.MetadataRefreshRequested.Task;
        harness.FailMetadataRefresh();
        await WaitForLeaderRefreshToDrainAsync(consumer);
    }

    [Test]
    public async Task PrefetchFromBroker_RetriableError_BacksOffPartitionUntilBackoffExpires()
    {
        using var harness = new Harness(ErrorCode.KafkaStorageError);
        await using var metadataManager = harness.CreateMetadataManager();
        await using var consumer = harness.CreateConsumer(metadataManager);

        await AssertBothPartitionsGrouped(consumer);

        await InvokePrefetchFromBrokerAsync(consumer);

        var grouped = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(grouped[1]).IsEquivalentTo([Partition1]);

        // Once the backoff expires, the cached grouping is rebuilt and the partition returns.
        ExpireFetchBackoffs(consumer);
        await AssertBothPartitionsGrouped(consumer);

        await harness.MetadataRefreshRequested.Task;
        harness.FailMetadataRefresh();
        await WaitForLeaderRefreshToDrainAsync(consumer);
    }

    [Test]
    public async Task Resume_DuringFetchBackoff_DoesNotBypassBackoff()
    {
        using var harness = new Harness(ErrorCode.KafkaStorageError);
        await using var metadataManager = harness.CreateMetadataManager();
        await using var consumer = harness.CreateConsumer(metadataManager);

        await AssertBothPartitionsGrouped(consumer);
        consumer.Pause([Partition0]);
        await InvokeGroupPartitionsByBrokerAsync(consumer);

        // Backing off a paused partition and then resuming it must not patch it into the cache.
        var pendingItems = await InvokeFetchFromBrokerAsync(consumer);
        DisposeAndReturn(pendingItems);
        await InvokeGroupPartitionsByBrokerAsync(consumer);
        consumer.Resume([Partition0]);

        var grouped = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(grouped[1]).IsEquivalentTo([Partition1]);

        await harness.MetadataRefreshRequested.Task;
        harness.FailMetadataRefresh();
        await WaitForLeaderRefreshToDrainAsync(consumer);
    }

    private static async Task AssertBothPartitionsGrouped(KafkaConsumer<string, string> consumer)
    {
        var grouped = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(grouped[1]).IsEquivalentTo([Partition0, Partition1]);
    }

    private sealed class Harness : IDisposable
    {
        private readonly TaskCompletionSource<IKafkaConnection> _metadataConnection =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Harness(ErrorCode partition0Error)
        {
            Pool = Substitute.For<IConnectionPool>();
            var fetchConnection = Substitute.For<IKafkaConnection>();
            fetchConnection.SendAsync<FetchRequest, FetchResponse>(
                    Arg.Any<FetchRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ => ValueTask.FromResult(CreateFetchResponse(partition0Error)));
            Pool.GetConnectionByIndexAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(fetchConnection));

            // Metadata refreshes park until the test releases them, so the test can observe
            // that one started without racing its completion.
            Pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(_ => OnMetadataConnectionRequested());
            Pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(_ => OnMetadataConnectionRequested());
        }

        public IConnectionPool Pool { get; }

        public TaskCompletionSource MetadataRefreshRequested { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public MetadataManager CreateMetadataManager()
        {
            var metadataManager = new MetadataManager(Pool, ["localhost:9092"]);
            metadataManager.SetApiVersion(ApiKey.Fetch, FetchRequest.LowestSupportedVersion, FetchRequest.HighestSupportedVersion);
            metadataManager.Metadata.Update(CreateMetadataResponse());
            return metadataManager;
        }

        public KafkaConsumer<string, string> CreateConsumer(MetadataManager metadataManager)
        {
            var consumer = new KafkaConsumer<string, string>(
                new ConsumerOptions
                {
                    BootstrapServers = ["localhost:9092"],
                    ClientId = "test-consumer",
                    // Long enough that the backoff never expires on its own during a test.
                    RetryBackoffMs = 60_000,
                    RetryBackoffMaxMs = 60_000
                },
                Serializers.String,
                Serializers.String,
                Pool,
                metadataManager);
            consumer.IncrementalAssign(
            [
                new TopicPartitionOffset(Topic, 0, 0),
                new TopicPartitionOffset(Topic, 1, 0)
            ]);
            return consumer;
        }

        public void FailMetadataRefresh() =>
            _metadataConnection.TrySetException(new InvalidOperationException("stop refresh"));

        public void Dispose() => FailMetadataRefresh();

        private ValueTask<IKafkaConnection> OnMetadataConnectionRequested()
        {
            MetadataRefreshRequested.TrySetResult();
            return new ValueTask<IKafkaConnection>(_metadataConnection.Task);
        }
    }

    private static FetchResponse CreateFetchResponse(ErrorCode partition0Error) => new()
    {
        Responses =
        [
            new FetchResponseTopic
            {
                Topic = Topic,
                Partitions =
                [
                    new FetchResponsePartition
                    {
                        PartitionIndex = 0,
                        ErrorCode = partition0Error,
                        HighWatermark = -1
                    },
                    new FetchResponsePartition
                    {
                        PartitionIndex = 1,
                        HighWatermark = 1,
                        Records =
                        [
                            new RecordBatch
                            {
                                BaseOffset = 0,
                                Records = [new Record { OffsetDelta = 0, Value = "healthy"u8.ToArray() }]
                            }
                        ]
                    }
                ]
            }
        ]
    };

    private static MetadataResponse CreateMetadataResponse() => new()
    {
        ClusterId = "test-cluster",
        ControllerId = 1,
        Brokers = [new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9093 }],
        Topics =
        [
            new TopicMetadata
            {
                ErrorCode = ErrorCode.None,
                Name = Topic,
                Partitions =
                [
                    CreatePartitionMetadata(0),
                    CreatePartitionMetadata(1)
                ]
            }
        ]
    };

    private static PartitionMetadata CreatePartitionMetadata(int partition) => new()
    {
        ErrorCode = ErrorCode.None,
        PartitionIndex = partition,
        LeaderId = 1,
        LeaderEpoch = 5,
        ReplicaNodes = [1],
        IsrNodes = [1]
    };

    private static async ValueTask<List<PendingFetchData>?> InvokeFetchFromBrokerAsync(
        KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("FetchFromBrokerAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("FetchFromBrokerAsync method not found");

        var result = method.Invoke(
            consumer,
            [1, new List<TopicPartition> { Partition0, Partition1 }, GetFetchBufferEpoch(consumer), CancellationToken.None]);
        if (result is not ValueTask<List<PendingFetchData>?> valueTask)
            throw new InvalidOperationException("FetchFromBrokerAsync returned unexpected type");

        return await valueTask.ConfigureAwait(false);
    }

    private static async ValueTask InvokePrefetchFromBrokerAsync(KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("PrefetchFromBrokerAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("PrefetchFromBrokerAsync method not found");

        // Only partition 0 is fetched so the response yields no records to write to the
        // prefetch channel, which this harness does not start.
        var result = method.Invoke(
            consumer,
            [1, new List<TopicPartition> { Partition0 }, 0, 1, 0, GetFetchBufferEpoch(consumer), CancellationToken.None]);
        if (result is not ValueTask valueTask)
            throw new InvalidOperationException("PrefetchFromBrokerAsync returned unexpected type");

        await valueTask.ConfigureAwait(false);
    }

    private static async ValueTask<Dictionary<int, List<TopicPartition>>> InvokeGroupPartitionsByBrokerAsync(
        KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("GroupPartitionsByBrokerAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GroupPartitionsByBrokerAsync method not found");

        var result = method.Invoke(consumer, [CancellationToken.None]);
        if (result is not ValueTask<Dictionary<int, List<TopicPartition>>> valueTask)
            throw new InvalidOperationException("GroupPartitionsByBrokerAsync returned unexpected type");

        return await valueTask.ConfigureAwait(false);
    }

    private static void ExpireFetchBackoffs(KafkaConsumer<string, string> consumer)
    {
        var type = typeof(KafkaConsumer<string, string>);
        var backoffs = (Dictionary<TopicPartition, long>)(type
            .GetField("_partitionFetchBackoffs", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_partitionFetchBackoffs field not found"))
            .GetValue(consumer)!;
        var cacheLock = (type
            .GetField("_partitionCacheLock", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_partitionCacheLock field not found"))
            .GetValue(consumer)!;

        // Rewind the deadline instead of waiting for it, and leave the cached grouping in place:
        // the cache must expire by itself when the earliest backoff ends.
        lock (cacheLock)
        {
            foreach (var partition in backoffs.Keys.ToList())
                backoffs[partition] = 0;
        }

        var cacheField = type.GetField("_cachedPartitionsByBroker", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_cachedPartitionsByBroker field not found");
        var cached = cacheField.GetValue(consumer)
            ?? throw new InvalidOperationException("Expected a cached partition grouping");
        var entryType = cached.GetType();
        var expiry = entryType.GetProperty("ExpiresAtTimestamp")
            ?? throw new InvalidOperationException("ExpiresAtTimestamp not found");
        if ((long)expiry.GetValue(cached)! == long.MaxValue)
            throw new InvalidOperationException("Cached grouping does not record the backoff expiry");

        lock (cacheLock)
        {
            var rewound = Activator.CreateInstance(
                entryType,
                entryType.GetProperty("PartitionsByBroker")!.GetValue(cached),
                0L,
                entryType.GetProperty("MetadataLastRefreshed")!.GetValue(cached));
            cacheField.SetValue(consumer, rewound);
        }
    }

    private static int GetFetchBufferEpoch(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_fetchBufferEpoch",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_fetchBufferEpoch field not found.");

        return (int)field.GetValue(consumer)!;
    }

    private static async Task WaitForLeaderRefreshToDrainAsync(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_pendingLeaderRefreshTasks", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_pendingLeaderRefreshTasks field not found");

        var pending = (ConcurrentDictionary<string, Task>)field.GetValue(consumer)!;
        if (pending.TryGetValue(Topic, out var task))
            await task.ConfigureAwait(false);
    }

    private static void DisposeAndReturn(List<PendingFetchData>? pendingItems)
    {
        if (pendingItems is null)
            return;

        try
        {
            foreach (var pending in pendingItems)
                pending.Dispose();
        }
        finally
        {
            ConsumerFetchPools.ReturnPendingFetchDataList(pendingItems);
        }
    }
}
