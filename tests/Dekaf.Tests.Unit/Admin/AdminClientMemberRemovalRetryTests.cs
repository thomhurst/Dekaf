using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed partial class AdminClientRemoveMembersTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IdentityRemoval_ZeroDeadlineDoesNotStartDiscoveryOrRemoval(bool removeAll)
    {
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        SetupMemberDiscovery(connection);
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LeaveGroupResponse { Members = [] }));
        await using (admin)
        {
            var exception = await Assert.That(async () => await ((IAdminClient)admin).RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions
                {
                    RemoveAll = removeAll, TimeoutMs = 0,
                    Members = removeAll ? [] : [new ConsumerGroupMemberIdentity { MemberId = "member" }]
                })).Throws<KafkaTimeoutException>();
            await Assert.That(exception!.Configured).IsEqualTo(TimeSpan.Zero);
        }
        await connection.DidNotReceive().SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
        await connection.DidNotReceive().SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
            Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task IdentityRemoval_LostResponseDoesNotRetryAmbiguousEviction(bool removeAll, bool staticMember)
    {
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        SetupMemberDiscovery(connection, new DescribeGroupsResponseMember
        {
            MemberId = "original", GroupInstanceId = staticMember ? "instance" : null
        });
        var transportError = new KafkaException(ErrorCode.NetworkException, "Removal applied; response lost.");
        var attempts = 0;
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++attempts == 1
                ? ValueTask.FromException<LeaveGroupResponse>(transportError)
                : ValueTask.FromResult(new LeaveGroupResponse
                {
                    Members = [new LeaveGroupResponseMember
                    {
                        MemberId = "original", GroupInstanceId = staticMember ? "instance" : null,
                        ErrorCode = ErrorCode.UnknownMemberId
                    }]
                }));
        await using (admin)
        {
            var exception = await Assert.That(async () => await ((IAdminClient)admin).RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions
                {
                    RemoveAll = removeAll,
                    Members = removeAll ? [] : [staticMember
                        ? new ConsumerGroupMemberIdentity { GroupInstanceId = "instance" }
                        : new ConsumerGroupMemberIdentity { MemberId = "original" }]
                })).Throws<KafkaException>();
            await Assert.That(exception!.InnerException).IsSameReferenceAs(transportError);
            await Assert.That(exception.IsRetriable).IsFalse();
            await Assert.That(exception.ErrorCode).IsEqualTo(ErrorCode.NetworkException);
            await Assert.That(attempts).IsEqualTo(1);
        }
    }
}
