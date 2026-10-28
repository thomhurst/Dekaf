using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Under read_committed, records past the last stable offset belong to open transactions and
/// are not visible, so partition EOF and the lag gauge measure against the LSO, not the high
/// watermark (#3813). An empty fetch response with HWM 120, LSO 100 and the consumer at 100
/// means it has delivered everything it can see.
/// </summary>
public sealed class ConsumerReadCommittedEndOffsetTests
{
    private const string Topic = "eof-topic";
    private const long Position = 100;
    private const long LastStableOffset = 100;
    private const long HighWatermark = 120;
    private static readonly TopicPartition Partition0 = new(Topic, 0);

    [Test]
    [Arguments(IsolationLevel.ReadCommitted, true)]
    [Arguments(IsolationLevel.ReadUncommitted, false)]
    public async Task FetchFromBroker_EmptyResponseAtVisibleEnd_QueuesPartitionEof(
        IsolationLevel isolationLevel,
        bool expectEof)
    {
        await using var metadataManager = CreateMetadataManager(out var pool);
        await using var consumer = CreateConsumer(isolationLevel, pool, metadataManager);

        var pendingItems = await InvokeFetchFromBrokerAsync(consumer);
        if (pendingItems is not null)
            ConsumerFetchPools.ReturnPendingFetchDataList(pendingItems);

        await AssertEof(consumer, expectEof);
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted, true)]
    [Arguments(IsolationLevel.ReadUncommitted, false)]
    public async Task PrefetchFromBroker_EmptyResponseAtVisibleEnd_QueuesPartitionEof(
        IsolationLevel isolationLevel,
        bool expectEof)
    {
        await using var metadataManager = CreateMetadataManager(out var pool);
        await using var consumer = CreateConsumer(isolationLevel, pool, metadataManager);

        await InvokePrefetchFromBrokerAsync(consumer);

        await AssertEof(consumer, expectEof);
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted, LastStableOffset - Position)]
    [Arguments(IsolationLevel.ReadUncommitted, HighWatermark - Position)]
    public async Task LagGauge_MeasuresFromVisibleEndOffset(IsolationLevel isolationLevel, long expectedLag)
    {
        await using var metadataManager = CreateMetadataManager(out var pool);
        await using var consumer = CreateConsumer(isolationLevel, pool, metadataManager);

        var pendingItems = await InvokeFetchFromBrokerAsync(consumer);
        if (pendingItems is not null)
            ConsumerFetchPools.ReturnPendingFetchDataList(pendingItems);
        var positions = (ConcurrentDictionary<TopicPartition, long>)GetField("_positions").GetValue(consumer)!;
        positions[Partition0] = Position;

        var measurement = consumer.ObserveConsumerLag().Single();

        await Assert.That(measurement.Value).IsEqualTo(expectedLag);
    }

    private static async Task AssertEof(KafkaConsumer<string, string> consumer, bool expectEof)
    {
        var eofs = (ConcurrentQueue<(TopicPartition Partition, long Offset)>)GetField("_pendingEofEvents")
            .GetValue(consumer)!;
        if (expectEof)
            await Assert.That(eofs.Single()).IsEqualTo((Partition0, Position));
        else
            await Assert.That(eofs).IsEmpty();
    }

    private static MetadataManager CreateMetadataManager(out IConnectionPool pool)
    {
        pool = Substitute.For<IConnectionPool>();
        var fetchConnection = Substitute.For<IKafkaConnection>();
        fetchConnection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(CreateFetchResponse()));
        pool.GetConnectionByIndexAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(fetchConnection));

        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.SetApiVersion(ApiKey.Fetch, FetchRequest.LowestSupportedVersion, FetchRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(CreateMetadataResponse());
        return metadataManager;
    }

    private static KafkaConsumer<string, string> CreateConsumer(
        IsolationLevel isolationLevel,
        IConnectionPool pool,
        MetadataManager metadataManager)
    {
        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "test-consumer",
                IsolationLevel = isolationLevel,
                EnablePartitionEof = true
            },
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, Position)]);
        return consumer;
    }

    // No records: the broker returns nothing past the LSO while a transaction is open.
    private static FetchResponse CreateFetchResponse() => new()
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
                        HighWatermark = HighWatermark,
                        LastStableOffset = LastStableOffset
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
                    new PartitionMetadata
                    {
                        ErrorCode = ErrorCode.None,
                        PartitionIndex = 0,
                        LeaderId = 1,
                        LeaderEpoch = 5,
                        ReplicaNodes = [1],
                        IsrNodes = [1]
                    }
                ]
            }
        ]
    };

    private static async ValueTask<List<PendingFetchData>?> InvokeFetchFromBrokerAsync(
        KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("FetchFromBrokerAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("FetchFromBrokerAsync method not found");

        var result = method.Invoke(
            consumer,
            [1, new List<TopicPartition> { Partition0 }, GetFetchBufferEpoch(consumer), CancellationToken.None]);
        if (result is not ValueTask<List<PendingFetchData>?> valueTask)
            throw new InvalidOperationException("FetchFromBrokerAsync returned unexpected type");

        return await valueTask.ConfigureAwait(false);
    }

    private static async ValueTask InvokePrefetchFromBrokerAsync(KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("PrefetchFromBrokerAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("PrefetchFromBrokerAsync method not found");

        var result = method.Invoke(
            consumer,
            [1, new List<TopicPartition> { Partition0 }, 0, 1, 0, GetFetchBufferEpoch(consumer), CancellationToken.None]);
        if (result is not ValueTask valueTask)
            throw new InvalidOperationException("PrefetchFromBrokerAsync returned unexpected type");

        await valueTask.ConfigureAwait(false);
    }

    private static int GetFetchBufferEpoch(KafkaConsumer<string, string> consumer) =>
        (int)GetField("_fetchBufferEpoch").GetValue(consumer)!;

    private static FieldInfo GetField(string name) =>
        typeof(KafkaConsumer<string, string>).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"{name} field not found");
}
