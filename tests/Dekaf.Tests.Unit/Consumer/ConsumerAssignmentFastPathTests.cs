using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
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
        MetadataManager metadataManager)
    {
        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "group-a",
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1
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
}
