using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Regression tests for per-partition state cleanup during broker-initiated rebalances.
/// These tests exercise the <c>EnsureAssignmentAsync → RemovePartitionState</c> path
/// that fires when the coordinator returns a changed assignment after a group rebalance.
/// </summary>
public sealed class RebalanceCleanupTests : IAsyncDisposable
{
    private readonly IConnectionPool _connectionPool;
    private readonly IKafkaConnection _connection;
    private readonly MetadataManager _metadataManager;

    public RebalanceCleanupTests()
    {
        _connectionPool = Substitute.For<IConnectionPool>();
        _connection = Substitute.For<IKafkaConnection>();

        _connectionPool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_connection));

        _connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_connection));

        _metadataManager = new MetadataManager(_connectionPool, ["localhost:9092"]);

        _metadataManager.Metadata.Update(new MetadataResponse
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
                    ErrorCode = ErrorCode.None,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            PartitionIndex = 0, LeaderId = 0, ErrorCode = ErrorCode.None,
                            ReplicaNodes = [0], IsrNodes = [0]
                        },
                        new PartitionMetadata
                        {
                            PartitionIndex = 1, LeaderId = 0, ErrorCode = ErrorCode.None,
                            ReplicaNodes = [0], IsrNodes = [0]
                        },
                        new PartitionMetadata
                        {
                            PartitionIndex = 2, LeaderId = 0, ErrorCode = ErrorCode.None,
                            ReplicaNodes = [0], IsrNodes = [0]
                        }
                    ]
                }
            ]
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _metadataManager.DisposeAsync();
    }

    private static ConsumerOptions CreateOptions(int heartbeatIntervalMs = 50) => new()
    {
        BootstrapServers = ["localhost:9092"],
        GroupId = "test-group",
        ClientId = "test-consumer",
        HeartbeatIntervalMs = heartbeatIntervalMs
    };

    /// <summary>
    /// Sets up the mock connection for a successful JoinGroup/SyncGroup flow
    /// with the given assignment data. Heartbeat returns success.
    /// </summary>
    private void SetupJoinFlow(byte[] assignmentData)
    {
        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                ErrorCode = ErrorCode.None,
                NodeId = 0,
                Host = "localhost",
                Port = 9092
            }));

        _connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
                Arg.Any<JoinGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new JoinGroupResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-1",
                GenerationId = 1,
                Leader = "member-1",
                Members = []
            }));

        _connection.SendAsync<SyncGroupRequest, SyncGroupResponse>(
                Arg.Any<SyncGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new SyncGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Assignment = assignmentData
            }));

        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new HeartbeatResponse
            {
                ErrorCode = ErrorCode.None
            }));

        // OffsetFetch returns committed offsets for all partitions so InitializePositionsAsync
        // doesn't fall through to GetResetOffsetAsync (which requires ListOffsets mocks).
        _connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.ArgAt<OffsetFetchRequest>(0);
                var topics = new List<OffsetFetchResponseTopic>();
                if (request.Topics is not null)
                {
                    foreach (var topic in request.Topics)
                    {
                        var partitions = new List<OffsetFetchResponsePartition>();
                        foreach (var idx in topic.PartitionIndexes)
                        {
                            partitions.Add(new OffsetFetchResponsePartition
                            {
                                PartitionIndex = idx,
                                CommittedOffset = 0,
                                ErrorCode = ErrorCode.None
                            });
                        }
                        topics.Add(new OffsetFetchResponseTopic
                        {
                            Name = topic.Name,
                            Partitions = partitions
                        });
                    }
                }
                return ValueTask.FromResult(new OffsetFetchResponse
                {
                    ErrorCode = ErrorCode.None,
                    Topics = topics
                });
            });
    }

    [Test]
    public async Task EnsureAssignment_RebalanceRevokesPartition_ClearsPausedState()
    {
        // Arrange: initial assignment = partitions [0, 1, 2]
        var initialAssignment = BuildAssignmentData("test-topic", [0, 1, 2]);
        SetupJoinFlow(initialAssignment);

        await using var consumer = new KafkaConsumer<string, string>(
            CreateOptions(),
            Serializers.String,
            Serializers.String,
            _connectionPool,
            _metadataManager);

        consumer.Subscribe("test-topic");

        // First EnsureAssignmentAsync — picks up assignment [0, 1, 2]
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(consumer.Assignment).Count().IsEqualTo(3);

        // Pause partition 1
        consumer.Pause(new TopicPartition("test-topic", 1));
        await Assert.That(consumer.Paused.Contains(new TopicPartition("test-topic", 1))).IsTrue();

        // Re-stub SyncGroup with a smaller assignment: partitions [0, 2] only
        var rebalancedAssignment = BuildAssignmentData("test-topic", [0, 2]);

        _connection.SendAsync<SyncGroupRequest, SyncGroupResponse>(
                Arg.Any<SyncGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new SyncGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Assignment = rebalancedAssignment
            }));

        _connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
                Arg.Any<JoinGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new JoinGroupResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-1",
                GenerationId = 2,
                Leader = "member-1",
                Members = []
            }));

        // Make heartbeat trigger a rebalance so the coordinator transitions to Unjoined.
        // Returns RebalanceInProgress once (triggering re-join), then success on subsequent calls.
        var heartbeatRebalanceTriggered = 0;
        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.CompareExchange(ref heartbeatRebalanceTriggered, 1, 0) == 0)
                {
                    return ValueTask.FromResult(new HeartbeatResponse
                    {
                        ErrorCode = ErrorCode.RebalanceInProgress
                    });
                }

                return ValueTask.FromResult(new HeartbeatResponse
                {
                    ErrorCode = ErrorCode.None
                });
            });

        // Poll until the heartbeat-triggered rebalance completes asynchronously
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            await consumer.EnsureAssignmentAsync(CancellationToken.None);
            if (consumer.Assignment.Count != 3)
                break;
            await Task.Delay(50);
        }

        // Assert: partition 1 was revoked — its paused state must be cleaned up
        await Assert.That(consumer.Assignment).Count().IsEqualTo(2);
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("test-topic", 0))).IsTrue();
        await Assert.That(consumer.Assignment.Contains(new TopicPartition("test-topic", 2))).IsTrue();

        // This is the critical assertion: paused state must not survive rebalance
        await Assert.That(consumer.Paused.Contains(new TopicPartition("test-topic", 1))).IsFalse();

        // Position for the removed partition should also be cleared
        var position = consumer.GetPosition(new TopicPartition("test-topic", 1));
        await Assert.That(position).IsNull();
    }

    private static byte[] BuildAssignmentData(string topic, int[] partitions)
        => ConsumerTestHelpers.BuildAssignmentData(topic, partitions);
}
