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
            var exception = await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
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
    public async Task IdentityRemoval_DeadlineEndsSendAfterWrite_ReportsUnknownOutcome()
    {
        // The call's deadline ends a LeaveGroup already being written (the mocked connection
        // reports every send as started). The removal may apply, so the caller must get the
        // non-retriable unknown outcome rather than a retriable timeout.
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        SetupMemberDiscovery(connection);
        var attempts = 0;
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Interlocked.Increment(ref attempts);
                return new ValueTask<LeaveGroupResponse>(WaitForCancellationAsync(call.ArgAt<CancellationToken>(2)));
            });
        await using (admin)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var exception = await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions
                {
                    TimeoutMs = 200,
                    Members = [new ConsumerGroupMemberIdentity { GroupInstanceId = "instance" }]
                })).Throws<KafkaException>();
            stopwatch.Stop();
            // The broker never answers: the call still ends at about the deadline.
            await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
            await Assert.That(exception).IsNotTypeOf<KafkaTimeoutException>();
            await Assert.That(exception!.IsRetriable).IsFalse();
            await Assert.That(exception.InnerException).IsAssignableTo<OperationCanceledException>();
            await Assert.That(attempts).IsEqualTo(1);
        }
    }

    [Test]
    public async Task IdentityRemoval_ResponseRacesDeadline_ReturnsTheResponse()
    {
        // The coordinator's answer completes the send in the same instant the deadline fires,
        // as a real connection's response can race the cancellation it observes. The answer says
        // what happened, so it is returned rather than turned into a timeout, and the call still
        // ends at the deadline.
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        SetupMemberDiscovery(connection);
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<LeaveGroupResponse>(RespondAtCancellationAsync(call.ArgAt<CancellationToken>(2))));
        await using (admin)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions
                {
                    TimeoutMs = 100,
                    Members = [new ConsumerGroupMemberIdentity { GroupInstanceId = "instance" }]
                });
            stopwatch.Stop();
            await Assert.That(result.Members.Count).IsEqualTo(1);
            await Assert.That(result.Members[0].ErrorCode).IsEqualTo(ErrorCode.None);
            await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
        }

        static Task<LeaveGroupResponse> RespondAtCancellationAsync(CancellationToken token)
        {
            var response = new TaskCompletionSource<LeaveGroupResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => response.TrySetResult(new LeaveGroupResponse
            {
                Members = [new LeaveGroupResponseMember { MemberId = "member", GroupInstanceId = "instance", ErrorCode = ErrorCode.None }]
            }));
            return response.Task;
        }
    }

    [Test]
    public async Task IdentityRemoval_CallerCancelsSendAfterWrite_ThrowsCancellation()
    {
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        SetupMemberDiscovery(connection);
        using var cancellation = new CancellationTokenSource();
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                cancellation.Cancel();
                return new ValueTask<LeaveGroupResponse>(WaitForCancellationAsync(call.ArgAt<CancellationToken>(2)));
            });
        await using (admin)
        {
            await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions
                {
                    TimeoutMs = 30_000,
                    Members = [new ConsumerGroupMemberIdentity { GroupInstanceId = "instance" }]
                }, cancellation.Token)).Throws<OperationCanceledException>();
        }
    }

    private static async Task<LeaveGroupResponse> WaitForCancellationAsync(CancellationToken token)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        throw new InvalidOperationException("Cancellation did not end the blocked send.");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LegacyStaticRemoval_AmbiguousOutcome_DoesNotReplay(bool lostResponse)
    {
        // The legacy static-member overload: a lost response, or REQUEST_TIMED_OUT, may have
        // removed the member. A replay could evict a replacement with the same group.instance.id.
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        var attempts = 0;
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref attempts);
                return lostResponse
                    ? ValueTask.FromException<LeaveGroupResponse>(new KafkaException(ErrorCode.NetworkException, "Response lost."))
                    : ValueTask.FromResult(new LeaveGroupResponse { ErrorCode = ErrorCode.RequestTimedOut, Members = [] });
            });
        await using (admin)
        {
            var exception = await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(
                GroupId, [new ConsumerGroupMemberToRemove { GroupInstanceId = "instance" }])).Throws<KafkaException>();
            await Assert.That(exception!.IsRetriable).IsFalse();
            await Assert.That(attempts).IsEqualTo(1);
        }
    }

    [Test]
    public async Task IdentityRemoval_RequestTimedOutAnswer_DoesNotRetryAmbiguousEviction()
    {
        // REQUEST_TIMED_OUT completes the send normally, but the coordinator only stopped waiting
        // for the removal to commit. Replaying it could evict a replacement that joined meanwhile.
        var (admin, connection) = CreateAdmin(3, 5);
        SetupCoordinator(connection);
        SetupMemberDiscovery(connection);
        var attempts = 0;
        connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(Arg.Any<LeaveGroupRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref attempts);
                return ValueTask.FromResult(new LeaveGroupResponse { ErrorCode = ErrorCode.RequestTimedOut, Members = [] });
            });
        await using (admin)
        {
            var exception = await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
                new ConsumerGroupMemberRemovalOptions
                {
                    Members = [new ConsumerGroupMemberIdentity { GroupInstanceId = "instance" }]
                })).Throws<KafkaException>();
            await Assert.That(exception!.IsRetriable).IsFalse();
            await Assert.That(exception.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
            await Assert.That(attempts).IsEqualTo(1);
        }
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
            var exception = await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync(GroupId,
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
