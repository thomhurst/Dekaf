using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerDirtyCommitTests
{
    [Test]
    public async Task CommitAsync_AfterSuccessfulCommit_CommitsOnlyOffsetsChangedSinceLastCommit()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);

        consumer.Seek(new TopicPartitionOffset("topic-a", 0, 10));
        consumer.Seek(new TopicPartitionOffset("topic-a", 1, 20));

        await consumer.CommitAsync(CancellationToken.None);

        consumer.Seek(new TopicPartitionOffset("topic-a", 1, 21));

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests.Count).IsEqualTo(2);
        await Assert.That(GetCommittedOffsets(requests[0])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10),
            new TopicPartitionOffset("topic-a", 1, 20)
        ]);
        await Assert.That(GetCommittedOffsets(requests[1])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 1, 21)
        ]);
    }

    [Test]
    public async Task CommitAsync_WhenNoOffsetsChangedAfterSuccessfulCommit_DoesNotSendCommitRequest()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);

        consumer.Seek(new TopicPartitionOffset("topic-a", 0, 10));

        await consumer.CommitAsync(CancellationToken.None);
        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CommitAsync_WhenCommitFails_PreservesDirtyOffsetsForRetry()
    {
        var requests = new List<OffsetCommitRequest>();
        var responseErrors = new Queue<ErrorCode>([ErrorCode.InvalidCommitOffsetSize, ErrorCode.None]);
        await using var consumer = CreateConsumer(requests, responseErrors);

        consumer.Seek(new TopicPartitionOffset("topic-a", 0, 10));

        await Assert.That(async () => await consumer.CommitAsync(CancellationToken.None))
            .Throws<GroupException>();

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests.Count).IsEqualTo(2);
        await Assert.That(GetCommittedOffsets(requests[0])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10)
        ]);
        await Assert.That(GetCommittedOffsets(requests[1])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10)
        ]);
    }

    [Test]
    public async Task CommitAsync_AfterNonDirtyPositionReset_DoesNotCommitStaleDirtyOffset()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);
        var partition = new TopicPartition("topic-a", 0);

        consumer.Seek(new TopicPartitionOffset("topic-a", 0, 10));
        SetPosition(consumer, partition, 2, dirty: false);

        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(2);

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests).IsEmpty();
    }

    [Test]
    public async Task CommitAsync_ExplicitLeaderEpoch_SendsCommittedLeaderEpoch()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(requests, ErrorCode.None);

        await consumer.CommitAsync(
            [new TopicPartitionOffset("topic-a", 0, 42, leaderEpoch: 7)],
            CancellationToken.None);

        var partition = requests.Single().Topics.Single().Partitions.Single();
        await Assert.That(partition.CommittedLeaderEpoch).IsEqualTo(7);
    }

    [Test]
    public async Task StoreOffset_AutoCommit_CommitsNextOffset()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false);

        consumer.StoreOffset(CreateConsumeResult(offset: 41, leaderEpoch: 7));

        await CommitStoredOffsetsAsync(consumer);

        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 42, leaderEpoch: 7)
        ]);
    }

    [Test]
    public async Task CloseAsync_UnknownCoordinator_RediscoversAndCommitsPendingOffset()
    {
        var requests = new List<OffsetCommitRequest>();
        var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false);

        try
        {
            consumer.StoreOffset(CreateConsumeResult(offset: 41, leaderEpoch: 7));

            await consumer.CloseAsync(CancellationToken.None);

            await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
            [
                new TopicPartitionOffset("topic-a", 0, 42, leaderEpoch: 7)
            ]);
        }
        finally
        {
            await consumer.DisposeAsync();
        }
    }

    [Test]
    public async Task AutoCommitCycle_WhenCommitFails_RetriesOnce()
    {
        var requests = new List<OffsetCommitRequest>();
        var responseErrors = new Queue<ErrorCode>([ErrorCode.InvalidCommitOffsetSize, ErrorCode.None]);
        await using var consumer = CreateConsumer(
            requests,
            responseErrors,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false);
        consumer.StoreOffset(CreateConsumeResult(offset: 41, leaderEpoch: 7));
        SetCoordinatorState(consumer, CoordinatorState.Stable);

        await RunAutoCommitCycleAsync(consumer);

        await Assert.That(requests).Count().IsEqualTo(2);
        await Assert.That(requests.SelectMany(GetCommittedOffsets).ToArray()).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 42, leaderEpoch: 7),
            new TopicPartitionOffset("topic-a", 0, 42, leaderEpoch: 7)
        ]);
    }

    [Test]
    public async Task AutoCommitCycle_WhenBothAttemptsFail_NextCycleRetries()
    {
        var requests = new List<OffsetCommitRequest>();
        var responseErrors = new Queue<ErrorCode>(
            [ErrorCode.InvalidCommitOffsetSize, ErrorCode.InvalidCommitOffsetSize, ErrorCode.None]);
        await using var consumer = CreateConsumer(
            requests,
            responseErrors,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false);
        consumer.StoreOffset(CreateConsumeResult(offset: 41, leaderEpoch: 7));
        SetCoordinatorState(consumer, CoordinatorState.Stable);

        await RunAutoCommitCycleAsync(consumer);
        await RunAutoCommitCycleAsync(consumer);

        await Assert.That(requests).Count().IsEqualTo(3);
        await Assert.That(GetCommittedOffsets(requests[^1])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 42, leaderEpoch: 7)
        ]);
    }

    [Test]
    public async Task AutoCommitCycle_WhenCoordinatorIsNotStable_SkipsCommit()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false);
        consumer.StoreOffset(CreateConsumeResult(offset: 41, leaderEpoch: 7));
        SetCoordinatorState(consumer, CoordinatorState.Joining);

        await RunAutoCommitCycleAsync(consumer);

        await Assert.That(requests).IsEmpty();
    }

    [Test]
    public async Task AutoCommitCycle_WhenCommitIsCancelled_PropagatesWithoutRetry()
    {
        var requests = new List<OffsetCommitRequest>();
        using var cancellation = new CancellationTokenSource();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false,
            onOffsetCommit: token =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
            });
        consumer.StoreOffset(CreateConsumeResult(offset: 41, leaderEpoch: 7));
        SetCoordinatorState(consumer, CoordinatorState.Stable);

        await Assert.That(async () => await RunAutoCommitCycleAsync(consumer, cancellation.Token))
            .ThrowsExactly<OperationCanceledException>();

        await Assert.That(requests).Count().IsEqualTo(1);
    }

    [Test]
    public async Task CloseAsync_WhenCommitFailsTwice_RetriesThirdTime()
    {
        var requests = new List<OffsetCommitRequest>();
        var responseErrors = new Queue<ErrorCode>(
            [ErrorCode.InvalidCommitOffsetSize, ErrorCode.InvalidCommitOffsetSize, ErrorCode.None]);
        var consumer = CreateConsumer(
            requests,
            responseErrors,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false);

        try
        {
            consumer.StoreOffset(CreateConsumeResult(offset: 41, leaderEpoch: 7));

            await consumer.CloseAsync(CancellationToken.None);

            await Assert.That(requests).Count().IsEqualTo(3);
            await Assert.That(GetCommittedOffsets(requests[^1])).IsEquivalentTo(
            [
                new TopicPartitionOffset("topic-a", 0, 42, leaderEpoch: 7)
            ]);
        }
        finally
        {
            await consumer.DisposeAsync();
        }
    }

    [Test]
    public async Task AutoCommit_WhenAutoOffsetStoreDisabled_DoesNotCommitConsumedPosition()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false);

        RecordConsumedPosition(consumer, new TopicPartition("topic-a", 0), 10, leaderEpoch: 3);

        await CommitStoredOffsetsAsync(consumer);

        await Assert.That(requests).IsEmpty();
    }

    [Test]
    public async Task AutoCommit_WhenAutoOffsetStoreEnabled_CommitsConsumedPosition()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: true);

        RecordConsumedPosition(consumer, new TopicPartition("topic-a", 0), 10, leaderEpoch: 3);

        await CommitStoredOffsetsAsync(consumer);

        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10, leaderEpoch: 3)
        ]);
    }

    [Test]
    public async Task Revoke_AutoCommit_CommitsOnlyRevokedDirtyOffsets()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false);
        var revoked = new TopicPartition("topic-a", 0);
        var retained = new TopicPartition("topic-a", 1);
        consumer.StoreOffset(new TopicPartitionOffset(revoked.Topic, revoked.Partition, 10));
        consumer.StoreOffset(new TopicPartitionOffset(retained.Topic, retained.Partition, 20));

        await CommitRevokedOffsetsAsync(consumer, [revoked]);
        await CommitStoredOffsetsAsync(consumer);

        await Assert.That(requests).Count().IsEqualTo(2);
        await Assert.That(GetCommittedOffsets(requests[0])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10)
        ]);
        await Assert.That(GetCommittedOffsets(requests[1])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 1, 20)
        ]);
    }

    [Test]
    public async Task Revoke_ManualCommit_DoesNotCommitStoredOffsets()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Manual,
            enableAutoOffsetStore: false);
        var revoked = new TopicPartition("topic-a", 0);
        consumer.StoreOffset(new TopicPartitionOffset(revoked.Topic, revoked.Partition, 10));

        await CommitRevokedOffsetsAsync(consumer, [revoked]);

        await Assert.That(requests).IsEmpty();
    }

    [Test]
    public async Task Revoke_WhenCommitFails_PreservesOffsetAndContinues()
    {
        var requests = new List<OffsetCommitRequest>();
        var responseErrors = new Queue<ErrorCode>([ErrorCode.InvalidCommitOffsetSize, ErrorCode.None]);
        await using var consumer = CreateConsumer(
            requests,
            responseErrors,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false);
        var revoked = new TopicPartition("topic-a", 0);
        consumer.StoreOffset(new TopicPartitionOffset(revoked.Topic, revoked.Partition, 10));

        await CommitRevokedOffsetsAsync(consumer, [revoked]);
        await CommitStoredOffsetsAsync(consumer);

        await Assert.That(requests).Count().IsEqualTo(2);
        await Assert.That(GetCommittedOffsets(requests[1])).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10)
        ]);
    }

    [Test]
    public async Task Revoke_WhenCommitExceedsRebalanceTimeout_Continues()
    {
        var requests = new List<OffsetCommitRequest>();
        var commitAttempt = 0;
        var commitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Auto,
            enableAutoOffsetStore: false,
            onOffsetCommitAsync: async token =>
            {
                if (Interlocked.Increment(ref commitAttempt) != 1)
                    return;

                commitStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });
        var revoked = new TopicPartition("topic-a", 0);
        consumer.StoreOffset(new TopicPartitionOffset(revoked.Topic, revoked.Partition, 10));
        using var rebalanceTimeout = new CancellationTokenSource();

        var commit = CommitRevokedOffsetsWithTimeoutAsync(consumer, [revoked], rebalanceTimeout.Token);
        await commitStarted.Task;
        await rebalanceTimeout.CancelAsync();
        await commit;

        await Assert.That(requests).Count().IsEqualTo(1);
    }

    [Test]
    public async Task CommitAsync_WhenAutoOffsetStoreDisabled_CommitsOnlyStoredOffset()
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Manual,
            enableAutoOffsetStore: false);
        var partition = new TopicPartition("topic-a", 0);

        RecordConsumedPosition(consumer, partition, 10, leaderEpoch: 3);

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests).IsEmpty();

        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10, leaderEpoch: 3));

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10, leaderEpoch: 3)
        ]);
    }

    [Test]
    [Arguments("Seek")]
    [Arguments("SeekToBeginning")]
    [Arguments("SeekToEnd")]
    public async Task CommitAsync_WhenAutoOffsetStoreDisabled_DoesNotCommitSeekedPositionUntilStored(string seekOperation)
    {
        var requests = new List<OffsetCommitRequest>();
        await using var consumer = CreateConsumer(
            requests,
            ErrorCode.None,
            OffsetCommitMode.Manual,
            enableAutoOffsetStore: false);
        var partition = new TopicPartition("topic-a", 0);

        ApplySeek(consumer, seekOperation, partition);

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(requests).IsEmpty();

        consumer.StoreOffset(new TopicPartitionOffset("topic-a", 0, 10, leaderEpoch: 3));

        await consumer.CommitAsync(CancellationToken.None);

        await Assert.That(GetCommittedOffsets(requests.Single())).IsEquivalentTo(
        [
            new TopicPartitionOffset("topic-a", 0, 10, leaderEpoch: 3)
        ]);
    }

    private static KafkaConsumer<string, string> CreateConsumer(
        List<OffsetCommitRequest> requests,
        ErrorCode responseError,
        OffsetCommitMode offsetCommitMode = OffsetCommitMode.Manual,
        bool enableAutoOffsetStore = true,
        Action<CancellationToken>? onOffsetCommit = null,
        int rebalanceTimeoutMs = 60_000,
        Func<CancellationToken, ValueTask>? onOffsetCommitAsync = null)
        => CreateConsumer(
            requests,
            new Queue<ErrorCode>([responseError]),
            offsetCommitMode,
            enableAutoOffsetStore,
            onOffsetCommit,
            rebalanceTimeoutMs,
            onOffsetCommitAsync);

    private static KafkaConsumer<string, string> CreateConsumer(
        List<OffsetCommitRequest> requests,
        Queue<ErrorCode> responseErrors,
        OffsetCommitMode offsetCommitMode = OffsetCommitMode.Manual,
        bool enableAutoOffsetStore = true,
        Action<CancellationToken>? onOffsetCommit = null,
        int rebalanceTimeoutMs = 60_000,
        Func<CancellationToken, ValueTask>? onOffsetCommitAsync = null)
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();

        connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                Coordinators =
                [
                    new Coordinator
                    {
                        Key = "group-a",
                        NodeId = 0,
                        Host = "localhost",
                        Port = 9092,
                        ErrorCode = ErrorCode.None
                    }
                ]
            }));

        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<OffsetCommitRequest>()!;
                requests.Add(CloneRequest(request));
                var token = call.Arg<CancellationToken>();
                onOffsetCommit?.Invoke(token);

                var error = responseErrors.Count == 0 ? ErrorCode.None : responseErrors.Dequeue();
                return onOffsetCommitAsync is null
                    ? ValueTask.FromResult(CreateResponse(request, error))
                    : CompleteOffsetCommitAsync(request, error, onOffsetCommitAsync(token));
            });

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }],
            Topics = []
        });
        metadataManager.SetApiVersion(
            ApiKey.FindCoordinator,
            FindCoordinatorRequest.LowestSupportedVersion,
            FindCoordinatorRequest.HighestSupportedVersion);
        metadataManager.SetApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            9);

        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "group-a",
                OffsetCommitMode = offsetCommitMode,
                EnableAutoOffsetStore = enableAutoOffsetStore,
                RebalanceTimeoutMs = rebalanceTimeoutMs
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
    }

    private static void SetPosition(
        KafkaConsumer<string, string> consumer,
        TopicPartition partition,
        long position,
        bool dirty)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "SetPosition",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        method.Invoke(consumer, [partition, position, dirty]);
    }

    private static void SetCoordinatorState(
        KafkaConsumer<string, string> consumer,
        CoordinatorState state)
    {
        var coordinatorField = typeof(KafkaConsumer<string, string>).GetField(
            "_coordinator",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var coordinator = (ConsumerCoordinator)coordinatorField.GetValue(consumer)!;
        var stateField = typeof(ConsumerCoordinator).GetField(
            "_state",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        stateField.SetValue(coordinator, state);
    }

    private static async Task RunAutoCommitCycleAsync(
        KafkaConsumer<string, string> consumer,
        CancellationToken cancellationToken = default)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "TryAutoCommitAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)method.Invoke(consumer, [cancellationToken])!;
    }

    private static void ApplySeek(
        KafkaConsumer<string, string> consumer,
        string seekOperation,
        TopicPartition partition)
    {
        switch (seekOperation)
        {
            case "Seek":
                consumer.Seek(new TopicPartitionOffset(partition.Topic, partition.Partition, 10, leaderEpoch: 3));
                break;
            case "SeekToBeginning":
                consumer.SeekToBeginning(partition);
                break;
            case "SeekToEnd":
                consumer.SeekToEnd(partition);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(seekOperation), seekOperation, null);
        }
    }

    private static void RecordConsumedPosition(
        KafkaConsumer<string, string> consumer,
        TopicPartition partition,
        long position,
        int leaderEpoch)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "ApplyConsumedPosition",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        method.Invoke(consumer, [partition, position, leaderEpoch]);
    }

    private static async Task CommitStoredOffsetsAsync(KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "CommitStoredOffsetsAsync",
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: [typeof(CancellationToken)],
            modifiers: null)!;

        var commit = method.Invoke(consumer, [CancellationToken.None])!;
        if (commit is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
            return;
        }

        if (commit is ValueTask<bool> valueTaskWithResult)
        {
            _ = await valueTaskWithResult.ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException($"Unexpected CommitStoredOffsetsAsync return type: {commit.GetType()}.");
    }

    private static async Task CommitRevokedOffsetsAsync(
        KafkaConsumer<string, string> consumer,
        IReadOnlyList<TopicPartition> partitions)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "CommitRevokedOffsetsAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (ValueTask)method.Invoke(consumer, [partitions, CancellationToken.None])!;
    }

    private static async Task CommitRevokedOffsetsWithTimeoutAsync(
        KafkaConsumer<string, string> consumer,
        IReadOnlyList<TopicPartition> partitions,
        CancellationToken commitCancellationToken)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "CommitRevokedOffsetsWithTimeoutAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (ValueTask)method.Invoke(
            consumer,
            [partitions, CancellationToken.None, commitCancellationToken])!;
    }

    private static ConsumeResult<string, string> CreateConsumeResult(long offset, int leaderEpoch)
    {
        return new ConsumeResult<string, string>(
            topic: "topic-a",
            partition: 0,
            offset: offset,
            keyData: ReadOnlyMemory<byte>.Empty,
            isKeyNull: true,
            valueData: ReadOnlyMemory<byte>.Empty,
            isValueNull: true,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: leaderEpoch,
            keyDeserializer: null,
            valueDeserializer: null);
    }

    private static OffsetCommitRequest CloneRequest(OffsetCommitRequest request)
    {
        return new OffsetCommitRequest
        {
            GroupId = request.GroupId,
            GenerationIdOrMemberEpoch = request.GenerationIdOrMemberEpoch,
            MemberId = request.MemberId,
            GroupInstanceId = request.GroupInstanceId,
            Topics = request.Topics
                .Select(static topic => new OffsetCommitRequestTopic
                {
                    Name = topic.Name,
                    Partitions = topic.Partitions
                        .Select(static partition => new OffsetCommitRequestPartition
                        {
                            PartitionIndex = partition.PartitionIndex,
                            CommittedOffset = partition.CommittedOffset,
                            CommittedLeaderEpoch = partition.CommittedLeaderEpoch
                        })
                        .ToArray()
                })
                .ToArray()
        };
    }

    private static OffsetCommitResponse CreateResponse(OffsetCommitRequest request, ErrorCode error)
    {
        return new OffsetCommitResponse
        {
            Topics = request.Topics
                .Select(topic => new OffsetCommitResponseTopic
                {
                    Name = topic.Name,
                    Partitions = topic.Partitions
                        .Select(partition => new OffsetCommitResponsePartition
                        {
                            PartitionIndex = partition.PartitionIndex,
                            ErrorCode = error
                        })
                        .ToArray()
                })
                .ToArray()
        };
    }

    private static async ValueTask<OffsetCommitResponse> CompleteOffsetCommitAsync(
        OffsetCommitRequest request,
        ErrorCode error,
        ValueTask beforeCommit)
    {
        await beforeCommit;
        return CreateResponse(request, error);
    }

    private static TopicPartitionOffset[] GetCommittedOffsets(OffsetCommitRequest request)
    {
        return request.Topics
            .SelectMany(static topic => topic.Partitions.Select(partition =>
                new TopicPartitionOffset(
                    topic.Name,
                    partition.PartitionIndex,
                    partition.CommittedOffset,
                    partition.CommittedLeaderEpoch)))
            .OrderBy(static offset => offset.Topic, StringComparer.Ordinal)
            .ThenBy(static offset => offset.Partition)
            .ThenBy(static offset => offset.Offset)
            .ToArray();
    }
}
