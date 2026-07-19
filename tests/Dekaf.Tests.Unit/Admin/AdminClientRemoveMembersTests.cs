using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientRemoveMembersTests
{
    private const string GroupId = "orders";

    [Test]
    public async Task RemoveMembers_SendsMultipleStaticMembersAndReturnsPartialFailure()
    {
        var (admin, connection) = CreateAdmin(leaveGroupMinVersion: 3, leaveGroupMaxVersion: 5);
        SetupCoordinator(connection);
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
                Arg.Any<LeaveGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LeaveGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Members =
                [
                    new LeaveGroupResponseMember
                    {
                        MemberId = "",
                        GroupInstanceId = "worker-a",
                        ErrorCode = ErrorCode.None
                    },
                    new LeaveGroupResponseMember
                    {
                        MemberId = "",
                        GroupInstanceId = null,
                        ErrorCode = ErrorCode.UnknownMemberId
                    }
                ]
            }));

        await using (admin)
        {
            var result = await admin.RemoveMembersFromConsumerGroupAsync(
                GroupId,
                [
                    new ConsumerGroupMemberToRemove { GroupInstanceId = "worker-a" },
                    new ConsumerGroupMemberToRemove { GroupInstanceId = "worker-b" }
                ],
                new RemoveMembersFromConsumerGroupOptions { Reason = "stale deployment" });

            await Assert.That(result.Succeeded).IsFalse();
            await Assert.That(result.Members[0].Succeeded).IsTrue();
            await Assert.That(result.Members[1].GroupInstanceId).IsEqualTo("worker-b");
            await Assert.That(result.Members[1].ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
        }

        await connection.Received(1).SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
            Arg.Is<LeaveGroupRequest>(request =>
                request != null &&
                request.GroupId == GroupId &&
                request.Members.Count == 2 &&
                request.Members.All(member => member.Reason == "stale deployment")),
            5,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveMembers_NotCoordinator_RediscoversAndRetries()
    {
        var (admin, connection) = CreateAdmin(leaveGroupMinVersion: 3, leaveGroupMaxVersion: 5);
        var coordinatorAttempt = 0;
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(CoordinatorResponse(++coordinatorAttempt)));
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
                Arg.Any<LeaveGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromResult(new LeaveGroupResponse
                {
                    ErrorCode = ErrorCode.NotCoordinator,
                    Members = []
                }),
                ValueTask.FromResult(new LeaveGroupResponse
                {
                    ErrorCode = ErrorCode.None,
                    Members =
                    [
                        new LeaveGroupResponseMember
                        {
                            MemberId = "",
                            GroupInstanceId = "worker-a",
                            ErrorCode = ErrorCode.None
                        }
                    ]
                }));

        await using (admin)
        {
            var result = await admin.RemoveMembersFromConsumerGroupAsync(
                GroupId,
                [new ConsumerGroupMemberToRemove { GroupInstanceId = "worker-a" }]);
            await Assert.That(result.Succeeded).IsTrue();
        }

        await connection.Received(2).SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            Arg.Any<FindCoordinatorRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
        await connection.Received(2).SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
            Arg.Any<LeaveGroupRequest>(),
            5,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveMembers_GroupAuthorizationFailureIncludesGroupId()
    {
        var (admin, connection) = CreateAdmin(leaveGroupMinVersion: 3, leaveGroupMaxVersion: 5);
        SetupCoordinator(connection);
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
                Arg.Any<LeaveGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LeaveGroupResponse
            {
                ErrorCode = ErrorCode.GroupAuthorizationFailed,
                Members = []
            }));

        await using (admin)
        {
            var exception = await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(
                    GroupId,
                    [new ConsumerGroupMemberToRemove { GroupInstanceId = "worker-a" }]))
                .Throws<GroupException>();

            await Assert.That(exception!.GroupId).IsEqualTo(GroupId);
            await Assert.That(exception.ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        }
    }

    [Test]
    public async Task RemoveMembers_PreV3BrokerThrowsUsefulVersionError()
    {
        var (admin, connection) = CreateAdmin(leaveGroupMinVersion: 0, leaveGroupMaxVersion: 2);
        SetupCoordinator(connection);

        await using (admin)
        {
            await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(
                    GroupId,
                    [new ConsumerGroupMemberToRemove { GroupInstanceId = "worker-a" }]))
                .Throws<BrokerVersionException>();
        }

        await connection.DidNotReceive().SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
            Arg.Any<LeaveGroupRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveMembers_HonorsCancellation()
    {
        var (admin, _) = CreateAdmin(leaveGroupMinVersion: 3, leaveGroupMaxVersion: 5);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await using (admin)
        {
            await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(
                    GroupId,
                    [new ConsumerGroupMemberToRemove { GroupInstanceId = "worker-a" }],
                    cancellationToken: cancellation.Token))
                .Throws<OperationCanceledException>();
        }
    }

    private static void SetupCoordinator(IKafkaConnection connection) =>
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CoordinatorResponse(1)));

    private static FindCoordinatorResponse CoordinatorResponse(int nodeId) => new()
    {
        Coordinators =
        [
            new Coordinator
            {
                Key = GroupId,
                NodeId = nodeId,
                Host = "localhost",
                Port = 9091 + nodeId,
                ErrorCode = ErrorCode.None
            }
        ]
    };

    private static (AdminClient Admin, IKafkaConnection Connection) CreateAdmin(
        short leaveGroupMinVersion,
        short leaveGroupMaxVersion)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.BrokerId.Returns(1);
        connection.Host.Returns("localhost");
        connection.Port.Returns(9092);
        connection.IsConnected.Returns(true);

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        var metadata = CreateMetadata();
        metadataManager.Metadata.Update(metadata);
        metadataManager.SetApiVersion(ApiKey.Metadata, 9, 13);
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        metadataManager.SetApiVersion(ApiKey.LeaveGroup, leaveGroupMinVersion, leaveGroupMaxVersion);

        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(metadata));
        connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(ApiKey.Metadata, 9, 13),
                    new ApiVersion(ApiKey.FindCoordinator, 4, 5),
                    new ApiVersion(ApiKey.LeaveGroup, leaveGroupMinVersion, leaveGroupMaxVersion)
                ]
            }));

        return (
            new AdminClient(new AdminClientOptions { BootstrapServers = ["localhost:9092"] }, pool, metadataManager),
            connection);
    }

    private static MetadataResponse CreateMetadata() => new()
    {
        Brokers =
        [
            new BrokerMetadata
            {
                NodeId = 1,
                Host = "localhost",
                Port = 9092
            }
        ],
        ClusterId = "test-cluster",
        ControllerId = 1,
        Topics = []
    };
}
