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

public sealed class ConsumerAssignmentFastPathTests
{
    private static readonly Guid TestTopicId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Test]
    public async Task EnsureAssignmentAsync_UnchangedManualAssignment_SkipsAssignmentLock()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset("topic-a", 0, 10)]);
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var assignmentLock = GetAssignmentLock(consumer);
        await assignmentLock.WaitAsync(CancellationToken.None);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await consumer.EnsureAssignmentAsync(cts.Token);
        }
        finally
        {
            assignmentLock.Release();
        }
    }

    [Test]
    public async Task EnsureAssignmentAsync_ChangedManualAssignment_RequiresAssignmentLock()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset("topic-a", 0, 10)]);
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        consumer.IncrementalAssign([new TopicPartitionOffset("topic-a", 1, 20)]);

        var assignmentLock = GetAssignmentLock(consumer);
        await assignmentLock.WaitAsync(CancellationToken.None);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.That(async () => await consumer.EnsureAssignmentAsync(cts.Token))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            assignmentLock.Release();
        }
    }

    [Test]
    public async Task EnsureAssignmentAsync_UnchangedCoordinatorAssignment_SkipsAssignmentLock()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0));
        SetupOffsetFetch(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var assignmentLock = GetAssignmentLock(consumer);
        await assignmentLock.WaitAsync(CancellationToken.None);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await consumer.EnsureAssignmentAsync(cts.Token);
        }
        finally
        {
            assignmentLock.Release();
        }
    }

    [Test]
    public async Task EnsureAssignmentAsync_ChangedCoordinatorAssignment_RequiresAssignmentLock()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupChangingConsumerGroupHeartbeat(connection);
        SetupOffsetFetch(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        GetCoordinator(consumer).RequestRejoin();

        var assignmentLock = GetAssignmentLock(consumer);
        await assignmentLock.WaitAsync(CancellationToken.None);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.That(async () => await consumer.EnsureAssignmentAsync(cts.Token))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            assignmentLock.Release();
        }

        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(consumer.Assignment).Contains(new TopicPartition("test-topic", 0));
        await Assert.That(consumer.Assignment).Contains(new TopicPartition("test-topic", 1));
    }

    [Test]
    public async Task ConsumeOneAsync_CoordinatorRevocation_ClearsPendingAndPrefetchedRecordsForRemovedPartitions()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupRevokingConsumerGroupHeartbeat(connection);
        SetupOffsetFetch(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager, queuedMinMessages: 2);
        SetInitialized(consumer);
        SetPrefetchStarted(consumer);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var revokedPartition = new TopicPartition("test-topic", 0);
        var retainedPartition = new TopicPartition("test-topic", 1);
        var revokedPending = CreateFetch(partition: 0, baseOffset: 10, value: "revoked-pending");
        var retainedPending = CreateFetch(partition: 1, baseOffset: 20, value: "retained-pending");
        var revokedPrefetched = CreateFetch(partition: 0, baseOffset: 30, value: "revoked-prefetched");
        var retainedPrefetched = CreateFetch(partition: 1, baseOffset: 40, value: "retained-prefetched");

        GetPendingFetches(consumer).Enqueue(revokedPending);
        GetPendingFetches(consumer).Enqueue(retainedPending);
        await Assert.That(GetPrefetchBuffer(consumer).TryWrite(revokedPrefetched)).IsTrue();
        await Assert.That(GetPrefetchBuffer(consumer).TryWrite(retainedPrefetched)).IsTrue();
        SetPrefetchedBytes(
            consumer,
            KafkaConsumer<string, string>.EstimatePendingFetchBytes(revokedPrefetched)
            + KafkaConsumer<string, string>.EstimatePendingFetchBytes(retainedPrefetched));

        GetCoordinator(consumer).RequestRejoin();

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Partition).IsEqualTo(1);
        await Assert.That(result.Value.Offset).IsEqualTo(20L);
        await Assert.That(result.Value.Value).IsEqualTo("retained-pending");
        await Assert.That(consumer.Assignment).DoesNotContain(revokedPartition);
        await Assert.That(consumer.Assignment).Contains(retainedPartition);
        await Assert.That(GetPrefetchedBytes(consumer)).IsEqualTo(0L);
        await Assert.That(GetPendingFetches(consumer).Any(p => p.TopicPartition == revokedPartition)).IsFalse();
    }

    [Test]
    public async Task CoordinatorRevocationFetchClear_ConcurrentQueueAndDrain_KeepsPendingFlagConsistent()
    {
        await using var consumer = CreateConsumer();
        var tasks = new List<Task>();

        for (var i = 0; i < 500; i++)
        {
            var partition = new TopicPartition("test-topic", i);
            tasks.Add(Task.Run(() => QueueCoordinatorRevokedPartitionsForFetchClear(consumer, [partition])));
            tasks.Add(Task.Run(() => ClearFetchBufferForPendingCoordinatorRevocations(consumer)));
        }

        await Task.WhenAll(tasks);

        var pendingRevocations = GetCoordinatorRevokedPartitionsPendingFetchClear(consumer);
        var pendingFlag = GetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer);

        await Assert.That(pendingFlag == 1 || pendingRevocations.IsEmpty).IsTrue();

        while (ClearFetchBufferForPendingCoordinatorRevocations(consumer))
        {
        }

        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClear(consumer)).IsEmpty();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer)).IsEqualTo(0);
    }

    [Test]
    public async Task CancelCoordinatorRevokedPartitionsFetchClear_ReassignedPartitionRemovesPendingClear()
    {
        await using var consumer = CreateConsumer();
        var reassignedPartition = new TopicPartition("test-topic", 0);
        var stillRevokedPartition = new TopicPartition("test-topic", 1);

        QueueCoordinatorRevokedPartitionsForFetchClear(consumer, [reassignedPartition, stillRevokedPartition]);

        CancelCoordinatorRevokedPartitionsFetchClear(consumer, [reassignedPartition]);

        var pendingRevocations = GetCoordinatorRevokedPartitionsPendingFetchClear(consumer);
        await Assert.That(pendingRevocations.ContainsKey(reassignedPartition)).IsFalse();
        await Assert.That(pendingRevocations.ContainsKey(stillRevokedPartition)).IsTrue();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer)).IsEqualTo(1);

        CancelCoordinatorRevokedPartitionsFetchClear(consumer, [stillRevokedPartition]);

        await Assert.That(pendingRevocations).IsEmpty();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer)).IsEqualTo(0);
    }

    [Test]
    public async Task WritePrefetchedItemsAsync_DropsUnassignedPartitionsBeforeAdvancingFetchPosition()
    {
        await using var consumer = CreateConsumer();
        var revokedPartition = new TopicPartition("test-topic", 0);
        var retainedPartition = new TopicPartition("test-topic", 1);
        var revokedFetch = CreateFetch(partition: 0, baseOffset: 10, value: "revoked-prefetch");
        var retainedFetch = CreateFetch(partition: 1, baseOffset: 20, value: "retained-prefetch");

        consumer.IncrementalAssign([new TopicPartitionOffset("test-topic", 1, 20)]);

        await WritePrefetchedItemsAsync(consumer, [revokedFetch, retainedFetch]);

        var fetchPositions = GetFetchPositions(consumer);
        await Assert.That(fetchPositions.ContainsKey(revokedPartition)).IsFalse();
        await Assert.That(fetchPositions[retainedPartition]).IsEqualTo(21L);
        await Assert.That(GetPrefetchBuffer(consumer).TryRead(out var prefetched)).IsTrue();
        await Assert.That(prefetched!.TopicPartition).IsEqualTo(retainedPartition);
        await Assert.That(GetPrefetchBuffer(consumer).TryRead(out _)).IsFalse();

        prefetched.Dispose();
        SetPrefetchedBytes(consumer, 0);
    }

    private static KafkaConsumer<string, string> CreateConsumer()
    {
        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1
            },
            Serializers.String,
            Serializers.String);
    }

    private static KafkaConsumer<string, string> CreateGroupConsumer(
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        int queuedMinMessages = 1)
    {
        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "group-a",
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = queuedMinMessages
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
    }

    private static MetadataManager CreateMetadataManager(IConnectionPool connectionPool)
    {
        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0);
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        metadataManager.SetApiVersion(ApiKey.OffsetFetch, OffsetFetchRequest.LowestSupportedVersion, OffsetFetchRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }
            ],
            Topics =
            [
                new TopicMetadata
                {
                    Name = "test-topic",
                    TopicId = TestTopicId,
                    ErrorCode = ErrorCode.None,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            PartitionIndex = 0,
                            LeaderId = 0,
                            ErrorCode = ErrorCode.None,
                            ReplicaNodes = [0],
                            IsrNodes = [0]
                        },
                        new PartitionMetadata
                        {
                            PartitionIndex = 1,
                            LeaderId = 0,
                            ErrorCode = ErrorCode.None,
                            ReplicaNodes = [0],
                            IsrNodes = [0]
                        }
                    ]
                }
            ]
        });

        return metadataManager;
    }

    private static void SetupConnectionPool(IConnectionPool connectionPool, IKafkaConnection connection)
    {
        connectionPool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));
        connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));
    }

    private static void SetupFindCoordinator(IKafkaConnection connection)
    {
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
    }

    private static void SetupConsumerGroupHeartbeat(
        IKafkaConnection connection,
        ConsumerGroupHeartbeatAssignment assignment)
    {
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-1",
                MemberEpoch = 1,
                HeartbeatIntervalMs = 60000,
                Assignment = assignment
            }));
    }

    private static void SetupChangingConsumerGroupHeartbeat(IKafkaConnection connection)
    {
        var callCount = 0;
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var count = Interlocked.Increment(ref callCount);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = count,
                    HeartbeatIntervalMs = 60000,
                    Assignment = count == 1 ? CreateAssignment(0) : CreateAssignment(0, 1)
                });
            });
    }

    private static void SetupRevokingConsumerGroupHeartbeat(IKafkaConnection connection)
    {
        var callCount = 0;
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var count = Interlocked.Increment(ref callCount);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = count,
                    HeartbeatIntervalMs = 60000,
                    Assignment = count == 1 ? CreateAssignment(0, 1) : CreateAssignment(1)
                });
            });
    }

    private static void SetupOffsetFetch(IKafkaConnection connection)
    {
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new OffsetFetchResponse
            {
                ErrorCode = ErrorCode.None,
                Topics =
                [
                    new OffsetFetchResponseTopic
                    {
                        Name = "test-topic",
                        Partitions =
                        [
                            new OffsetFetchResponsePartition
                            {
                                PartitionIndex = 0,
                                CommittedOffset = 10,
                                ErrorCode = ErrorCode.None
                            },
                            new OffsetFetchResponsePartition
                            {
                                PartitionIndex = 1,
                                CommittedOffset = 20,
                                ErrorCode = ErrorCode.None
                            }
                        ]
                    }
                ]
            }));
    }

    private static ConsumerGroupHeartbeatAssignment CreateAssignment(params int[] partitions)
    {
        return new ConsumerGroupHeartbeatAssignment
        {
            AssignedTopicPartitions =
            [
                new ConsumerGroupHeartbeatTopicPartitions
                {
                    TopicId = TestTopicId,
                    Partitions = partitions
                }
            ],
            PendingTopicPartitions = []
        };
    }

    private static SemaphoreSlim GetAssignmentLock(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_assignmentLock",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_assignmentLock field not found.");

        return (SemaphoreSlim)field.GetValue(consumer)!;
    }

    private static Queue<PendingFetchData> GetPendingFetches(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_pendingFetches",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_pendingFetches field not found.");

        return (Queue<PendingFetchData>)field.GetValue(consumer)!;
    }

    private static MpscFetchBuffer GetPrefetchBuffer(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_prefetchBuffer",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchBuffer field not found.");

        return (MpscFetchBuffer)field.GetValue(consumer)!;
    }

    private static long GetPrefetchedBytes(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_prefetchedBytes",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchedBytes field not found.");

        return (long)field.GetValue(consumer)!;
    }

    private static ConcurrentDictionary<TopicPartition, byte> GetCoordinatorRevokedPartitionsPendingFetchClear(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_coordinatorRevokedPartitionsPendingFetchClear",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_coordinatorRevokedPartitionsPendingFetchClear field not found.");

        return (ConcurrentDictionary<TopicPartition, byte>)field.GetValue(consumer)!;
    }

    private static int GetCoordinatorRevokedPartitionsPendingFetchClearPending(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_coordinatorRevokedPartitionsPendingFetchClearPending",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_coordinatorRevokedPartitionsPendingFetchClearPending field not found.");

        return (int)field.GetValue(consumer)!;
    }

    private static ConcurrentDictionary<TopicPartition, long> GetFetchPositions(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_fetchPositions",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_fetchPositions field not found.");

        return (ConcurrentDictionary<TopicPartition, long>)field.GetValue(consumer)!;
    }

    private static void QueueCoordinatorRevokedPartitionsForFetchClear(
        KafkaConsumer<string, string> consumer,
        TopicPartition[] partitions)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "QueueCoordinatorRevokedPartitionsForFetchClear",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("QueueCoordinatorRevokedPartitionsForFetchClear method not found.");

        method.Invoke(consumer, [partitions]);
    }

    private static void CancelCoordinatorRevokedPartitionsFetchClear(
        KafkaConsumer<string, string> consumer,
        TopicPartition[] partitions)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "CancelCoordinatorRevokedPartitionsFetchClear",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CancelCoordinatorRevokedPartitionsFetchClear method not found.");

        method.Invoke(consumer, [partitions]);
    }

    private static bool ClearFetchBufferForPendingCoordinatorRevocations(KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "ClearFetchBufferForPendingCoordinatorRevocations",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ClearFetchBufferForPendingCoordinatorRevocations method not found.");

        return (bool)method.Invoke(consumer, [])!;
    }

    private static async ValueTask WritePrefetchedItemsAsync(
        KafkaConsumer<string, string> consumer,
        IReadOnlyList<PendingFetchData> pendingItems)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "WritePrefetchedItemsAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("WritePrefetchedItemsAsync method not found.");

        var valueTask = (ValueTask)method.Invoke(consumer, [pendingItems, CancellationToken.None])!;
        await valueTask;
    }

    private static void SetPrefetchedBytes(KafkaConsumer<string, string> consumer, long bytes)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_prefetchedBytes",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchedBytes field not found.");

        field.SetValue(consumer, bytes);
    }

    private static void SetPrefetchStarted(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_prefetchTask",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchTask field not found.");

        field.SetValue(consumer, Task.CompletedTask);
    }

    private static void SetInitialized(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_initialized",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_initialized field not found.");

        field.SetValue(consumer, true);
    }

    private static ConsumerCoordinator GetCoordinator(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_coordinator",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_coordinator field not found.");

        return (ConsumerCoordinator)field.GetValue(consumer)!;
    }

    private static PendingFetchData CreateFetch(int partition, long baseOffset, string value)
    {
        return PendingFetchData.Create("test-topic", partition,
        [
            new RecordBatch
            {
                BaseOffset = baseOffset,
                BaseTimestamp = 1700000000000L,
                Attributes = 0,
                Records =
                [
                    new Record
                    {
                        OffsetDelta = 0,
                        TimestampDelta = 0,
                        Key = Encoding.UTF8.GetBytes($"key-{partition}"),
                        IsKeyNull = false,
                        Value = Encoding.UTF8.GetBytes(value),
                        IsValueNull = false,
                        Headers = null,
                        HeaderCount = 0
                    }
                ]
            }
        ]);
    }
}
