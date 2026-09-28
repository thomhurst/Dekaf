using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class ConsumerCoordinatorKip848Tests
{
    // The broker sends an assignment only when it changes. One naming a topic created after the
    // last metadata refresh used to drop that topic for good: the partition was never fetched.
    [Test]
    public async Task ConsumerProtocol_AssignmentTopicUnknownToMetadata_IsResolvedOnLaterHeartbeat()
    {
        var lateTopicId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        SetupFindCoordinator();
        var heartbeatCount = 0;
        var metadataRefreshCount = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-1",
                MemberEpoch = 1,
                HeartbeatIntervalMs = 60_000,
                // Only the join answer carries the assignment, as the broker does.
                Assignment = Interlocked.Increment(ref heartbeatCount) == 1
                    ? new ConsumerGroupHeartbeatAssignment
                    {
                        AssignedTopicPartitions =
                        [
                            new ConsumerGroupHeartbeatTopicPartitions { TopicId = TestTopicId, Partitions = [0] },
                            new ConsumerGroupHeartbeatTopicPartitions { TopicId = lateTopicId, Partitions = [0] }
                        ],
                        PendingTopicPartitions = []
                    }
                    : null
            }));
        // The first refresh still misses the new topic; the second one has caught up.
        _connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(CreateMetadata(Interlocked.Increment(ref metadataRefreshCount) > 1)));
        _connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(ApiKey.Metadata, 12, 12),
                    new ApiVersion(ApiKey.FindCoordinator, 4, 5),
                    new ApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0)
                ]
            }));
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000), _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic", "late-topic" }, CancellationToken.None);

        // The resolvable part is usable immediately; the rest stays pending.
        await Assert.That(coordinator.Assignment.SetEquals([new TopicPartition("test-topic", 0)])).IsTrue();
        await Assert.That(metadataRefreshCount).IsEqualTo(1);

        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(coordinator.Assignment.SetEquals(
            [new TopicPartition("test-topic", 0), new TopicPartition("late-topic", 0)])).IsTrue();
        await Assert.That(metadataRefreshCount).IsEqualTo(2);

        // Resolved: steady heartbeats no longer refresh metadata.
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        await Assert.That(metadataRefreshCount).IsEqualTo(2);
        await Assert.That(heartbeatCount).IsEqualTo(3);

        MetadataResponse CreateMetadata(bool includeLateTopic)
        {
            List<TopicMetadata> topics = [CreateTopic("test-topic", TestTopicId)];
            if (includeLateTopic)
                topics.Add(CreateTopic("late-topic", lateTopicId));
            return new MetadataResponse
            {
                Brokers = [new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }],
                Topics = topics
            };
        }

        static TopicMetadata CreateTopic(string name, Guid topicId) => new()
        {
            Name = name,
            TopicId = topicId,
            ErrorCode = ErrorCode.None,
            Partitions =
            [
                new PartitionMetadata
                {
                    PartitionIndex = 0, LeaderId = 0, ErrorCode = ErrorCode.None, ReplicaNodes = [0], IsrNodes = [0]
                }
            ]
        };
    }
}
