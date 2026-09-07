using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed partial class AdminClientRemoveMembersTests
{
    [Test]
    [Arguments(null, null)]
    [Arguments("static", "dynamic")]
    [Arguments("", null)]
    [Arguments(null, " ")]
    public async Task IdentityRemoval_RejectsInvalidIdentity(string? instanceId, string? memberId)
    {
        var (admin, _) = CreateAdmin(3, 5);
        await using (admin)
            await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions
                {
                    Members = [new ConsumerGroupMemberIdentity { GroupInstanceId = instanceId, MemberId = memberId }]
                })).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IdentityRemoval_RejectsEmptyOrAmbiguousMode(bool removeAll)
    {
        var (admin, _) = CreateAdmin(3, 5);
        await using (admin)
            await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions
                {
                    RemoveAll = removeAll,
                    Members = removeAll ? [new ConsumerGroupMemberIdentity { MemberId = "member" }] : []
                })).Throws<ArgumentException>();
    }

    [Test]
    public async Task IdentityRemoval_CustomAdminDoesNotGainAnImplicitCapability()
    {
        var admin = Substitute.For<IAdminClient>();
        await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
            new ConsumerGroupMemberRemovalOptions { RemoveAll = true })).Throws<NotSupportedException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IdentityRemoval_MapsReorderedPartialResultsToRequestedIdentities(bool removeAll)
    {
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        SetupMemberDiscovery(connection,
            new DescribeGroupsResponseMember { MemberId = "static-member", GroupInstanceId = "instance" },
            new DescribeGroupsResponseMember { MemberId = "dynamic" });
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LeaveGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Members =
                [
                    new LeaveGroupResponseMember { MemberId = "dynamic", ErrorCode = ErrorCode.UnknownMemberId },
                    new LeaveGroupResponseMember { MemberId = "", GroupInstanceId = "instance", ErrorCode = ErrorCode.None }
                ]
            }));
        await using (admin)
        {
            var result = await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions
                {
                    RemoveAll = removeAll, Reason = "operator request",
                    Members = removeAll ? [] :
                    [new ConsumerGroupMemberIdentity { GroupInstanceId = "instance" }, new ConsumerGroupMemberIdentity { MemberId = "dynamic" }]
                });
            await Assert.That(result.Members).Count().IsEqualTo(2);
            await Assert.That(result.Members[0].GroupInstanceId).IsEqualTo("instance");
            await Assert.That(result.Members[0].Succeeded).IsTrue();
            await Assert.That(result.Members[1].GroupInstanceId).IsEqualTo(string.Empty);
            await Assert.That(result.Members[1].MemberId).IsEqualTo("dynamic");
            await Assert.That(result.Members[1].ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
        }
        await connection.Received(1).SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
            Arg.Is<LeaveGroupRequest>(request => request.Members.Count == 2 &&
                request.Members[0].GroupInstanceId == "instance" && request.Members[0].MemberId == "" &&
                request.Members[1].GroupInstanceId == null && request.Members[1].MemberId == "dynamic" &&
                request.Members[1].Reason == "operator request"), 5, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveAll_RetriesCoordinatorWithoutRediscoveringMembership()
    {
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        SetupMemberDiscovery(connection, new DescribeGroupsResponseMember { MemberId = "original" });
        var attempts = 0;
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var error = ++attempts == 1 ? ErrorCode.NotCoordinator : ErrorCode.None;
                SetupMemberDiscovery(connection, new DescribeGroupsResponseMember { MemberId = "later-join" });
                return ValueTask.FromResult(new LeaveGroupResponse
                {
                    ErrorCode = error,
                    Members = [new LeaveGroupResponseMember { MemberId = "original", ErrorCode = ErrorCode.None }]
                });
            });
        await using (admin)
        {
            var result = await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions { RemoveAll = true });
            await Assert.That(result.Succeeded).IsTrue();
            await Assert.That(result.Members[0].MemberId).IsEqualTo("original");
        }
        await connection.Received(1).SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(
            Arg.Any<DescribeGroupsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
        await connection.Received(2).SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
            Arg.Is<LeaveGroupRequest>(request => request.Members[0].MemberId == "original"), 5, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveAll_EmptySnapshotDoesNotSendLeaveGroup()
    {
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        SetupMemberDiscovery(connection);
        await using (admin)
        {
            var result = await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions { RemoveAll = true });
            await Assert.That(result.Members).IsEmpty();
        }
        await connection.DidNotReceive().SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
            Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(ErrorCode.GroupAuthorizationFailed)]
    [Arguments(ErrorCode.UnsupportedVersion)]
    public async Task IdentityRemoval_PreservesGroupErrors(ErrorCode error)
    {
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LeaveGroupResponse { ErrorCode = error, Members = [] }));
        await using (admin)
        {
            var exception = await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions { Members = [new ConsumerGroupMemberIdentity { MemberId = "member" }] }))
                .Throws<GroupException>();
            await Assert.That(exception!.GroupId).IsEqualTo(GroupId);
            await Assert.That(exception.ErrorCode).IsEqualTo(error);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IdentityRemoval_TimeoutAndCancellationDoNotEvict(bool callerCancellation)
    {
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        using var cancellation = new CancellationTokenSource();
        connection.SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(Arg.Any<DescribeGroupsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<DescribeGroupsResponse>(WaitForCancellation(call.Arg<CancellationToken>())));
        async Task<DescribeGroupsResponse> WaitForCancellation(CancellationToken token)
        {
            if (callerCancellation) await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Unreachable");
        }
        await using (admin)
        {
            var action = async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions { RemoveAll = true, TimeoutMs = callerCancellation ? 30000 : 20 }, cancellation.Token);
            if (callerCancellation)
                await Assert.That(action).Throws<OperationCanceledException>();
            else
                await Assert.That(action).Throws<KafkaTimeoutException>();
        }
        await connection.DidNotReceive().SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
            Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task IdentityRemoval_RejectsPreV3Broker()
    {
        var (admin, connection) = CreateAdmin(0, 2);
        SetupCoordinator(connection);
        await using (admin)
            await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions { Members = [new ConsumerGroupMemberIdentity { MemberId = "member" }] }))
                .Throws<BrokerVersionException>();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task IdentityRemoval_RejectsNullDuplicateAndInvalidTimeoutInputs(int invalid)
    {
        var (admin, _) = CreateAdmin(3, 5);
        var identity = new ConsumerGroupMemberIdentity { MemberId = "member" };
        var options = invalid switch
        {
            0 => new ConsumerGroupMemberRemovalOptions { Members = null! },
            1 => new ConsumerGroupMemberRemovalOptions { Members = [null!] },
            2 => new ConsumerGroupMemberRemovalOptions { Members = [identity, identity] },
            _ => new ConsumerGroupMemberRemovalOptions { Members = [identity], TimeoutMs = -1 }
        };
        await using (admin)
            await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId, options))
                .Throws<ArgumentException>();
    }

    [Test]
    public async Task IdentityRemoval_MissingOutcomeCannotBecomeSuccess()
    {
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LeaveGroupResponse { ErrorCode = ErrorCode.None, Members = [] }));
        await using (admin)
        {
            var result = await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions { Members = [new ConsumerGroupMemberIdentity { MemberId = "member" }] });
            await Assert.That(result.Members).Count().IsEqualTo(1);
            await Assert.That(result.Members[0].MemberId).IsEqualTo("member");
            await Assert.That(result.Members[0].ErrorCode).IsEqualTo(ErrorCode.UnknownServerError);
        }
    }

    [Test]
    [Arguments("connect", ErrorCode.None, ErrorCode.UnsupportedVersion)]
    [Arguments("consumer", ErrorCode.GroupAuthorizationFailed, ErrorCode.GroupAuthorizationFailed)]
    public async Task RemoveAll_RejectsUnsupportedProtocolAndDiscoveryAuthorization(
        string protocol, ErrorCode discoveryError, ErrorCode expected)
    {
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        connection.SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(Arg.Any<DescribeGroupsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DescribeGroupsResponse
            {
                Groups = [new DescribeGroupsResponseGroup
                {
                    GroupId = GroupId, ErrorCode = discoveryError, GroupState = "Stable", ProtocolType = protocol,
                    Members = [new DescribeGroupsResponseMember { MemberId = "member", MemberAssignment = [1, 2, 3] }]
                }]
            }));
        await using (admin)
        {
            var exception = await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions { RemoveAll = true })).Throws<GroupException>();
            await Assert.That(exception!.ErrorCode).IsEqualTo(expected);
        }
        await connection.DidNotReceive().SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
            Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveAll_UsesKip848MembershipWhenSupported()
    {
        var (admin, connection) = CreateAdmin(3, 5, modern: true);
        SetupCoordinator(connection);
        connection.SendAsync<ConsumerGroupDescribeRequest, ConsumerGroupDescribeResponse>(
                Arg.Any<ConsumerGroupDescribeRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ConsumerGroupDescribeResponse
            {
                Groups = [new ConsumerGroupDescribeGroup
                {
                    GroupId = GroupId, GroupState = "Stable", AssignorName = "uniform",
                    Members = [new ConsumerGroupDescribeMember
                    {
                        MemberId = "modern-member", ClientId = "client", ClientHost = "host", SubscribedTopicNames = [],
                        Assignment = new ConsumerGroupDescribeAssignment { TopicPartitions = [] },
                        TargetAssignment = new ConsumerGroupDescribeAssignment { TopicPartitions = [] }
                    }]
                }]
            }));
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LeaveGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Members = [new LeaveGroupResponseMember { MemberId = "modern-member", ErrorCode = ErrorCode.None }]
            }));
        await using (admin)
        {
            var result = await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions { RemoveAll = true });
            await Assert.That(result.Succeeded).IsTrue();
            await Assert.That(result.Members[0].MemberId).IsEqualTo("modern-member");
        }
        await connection.DidNotReceive().SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(
            Arg.Any<DescribeGroupsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    private static void SetupMemberDiscovery(IKafkaConnection connection, params DescribeGroupsResponseMember[] members) =>
        connection.SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(Arg.Any<DescribeGroupsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DescribeGroupsResponse
            {
                Groups = [new DescribeGroupsResponseGroup
                {
                    GroupId = GroupId, ErrorCode = ErrorCode.None, GroupState = members.Length == 0 ? "Empty" : "Stable",
                    ProtocolType = "consumer", ProtocolData = "range", Members = members
                }]
            }));
}
