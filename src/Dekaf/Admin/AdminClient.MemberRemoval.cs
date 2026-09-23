using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IConsumerGroupMemberRemovalAdminClient
{
    ValueTask<RemoveMembersFromConsumerGroupResult> IConsumerGroupMemberRemovalAdminClient.RemoveMembersFromConsumerGroupAsync(
        string groupId, ConsumerGroupMemberRemovalOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        var members = ValidateMemberRemoval(options);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfMemberRemovalDeadlineExpired(options);
        return ExecuteWithTimeoutAsync(
            token => RemoveGroupMembersCoreAsync(groupId, members, options, token, cancellationToken),
            options.TimeoutMs, nameof(RemoveMembersFromConsumerGroupAsync), cancellationToken);
    }

    internal static void ThrowIfMemberRemovalDeadlineExpired(ConsumerGroupMemberRemovalOptions options)
    {
        if (options.TimeoutMs == 0)
            throw new KafkaTimeoutException(TimeoutKind.Api, TimeSpan.Zero, TimeSpan.Zero,
                "RemoveMembersFromConsumerGroupAsync timed out after 0 ms.");
    }

    internal static ConsumerGroupMemberIdentity[] ValidateMemberRemoval(ConsumerGroupMemberRemovalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Members);
        ArgumentOutOfRangeException.ThrowIfNegative(options.TimeoutMs);
        if (options.RemoveAll ? options.Members.Count != 0 : options.Members.Count == 0)
            throw new ArgumentException("Specify either RemoveAll or a nonempty explicit member collection, never both.", nameof(options));

        var members = new ConsumerGroupMemberIdentity[options.Members.Count];
        var identities = new HashSet<(bool Static, string Id)>();
        for (var index = 0; index < members.Length; index++)
        {
            var member = options.Members[index];
            ArgumentNullException.ThrowIfNull(member);
            if ((member.GroupInstanceId is null) == (member.MemberId is null))
                throw new ArgumentException("Each member requires exactly one GroupInstanceId or MemberId.", nameof(options));
            var id = member.GroupInstanceId ?? member.MemberId!;
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            if (!identities.Add((member.GroupInstanceId is not null, id)))
                throw new ArgumentException("Member identities must be unique.", nameof(options));
            members[index] = member;
        }
        return members;
    }

    private async ValueTask<RemoveMembersFromConsumerGroupResult> RemoveGroupMembersCoreAsync(
        string groupId, ConsumerGroupMemberIdentity[] members, ConsumerGroupMemberRemovalOptions options,
        CancellationToken cancellationToken, CancellationToken callerToken)
    {
        await InitializeUntilDeadlineAsync(nameof(RemoveMembersFromConsumerGroupAsync), cancellationToken).ConfigureAwait(false);
        if (options.RemoveAll)
        {
            // Snapshot once outside mutation retries. Concurrent joins are not added to the request.
            var groups = await DescribeConsumerGroupsCoreAsync([groupId], Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            // Classic DescribeGroups before v6 represents a missing group as Dead with no error.
            if (!groups.TryGetValue(groupId, out var group) || string.Equals(group.State, "Dead", StringComparison.OrdinalIgnoreCase))
                throw MemberRemovalError(groupId, ErrorCode.GroupIdNotFound);
            if (group.ProtocolType != "consumer" && !(string.IsNullOrEmpty(group.ProtocolType) && group.Members.Count == 0))
                throw MemberRemovalError(groupId, ErrorCode.UnsupportedVersion);
            members = new ConsumerGroupMemberIdentity[group.Members.Count];
            for (var index = 0; index < members.Length; index++)
            {
                var member = group.Members[index];
                members[index] = member.GroupInstanceId is { } instanceId
                    ? new ConsumerGroupMemberIdentity { GroupInstanceId = instanceId }
                    : new ConsumerGroupMemberIdentity { MemberId = member.MemberId };
            }
            // Reject malformed discovery before mutation, using the explicit-identity rules.
            if (members.Length != 0)
                members = ValidateMemberRemoval(new ConsumerGroupMemberRemovalOptions { Members = members });
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (members.Length == 0)
            return new RemoveMembersFromConsumerGroupResult { GroupId = groupId, Members = [] };

        var writeContext = new KafkaRequestWriteContext(CancellationToken.None);
        return await WithRetryAsync(async attemptToken =>
        {
            var coordinatorId = await FindGroupCoordinatorAsync(groupId, attemptToken).ConfigureAwait(false);
            using var lease = await _connectionPool.LeaseConnectionAsync(coordinatorId, attemptToken).ConfigureAwait(false);
            var connection = lease.Connection;
            var version = _metadataManager.GetNegotiatedApiVersion(connection, ApiKey.LeaveGroup,
                LeaveGroupRequest.LowestSupportedVersion, LeaveGroupRequest.HighestSupportedVersion);
            var requestMembers = new LeaveGroupRequestMember[members.Length];
            for (var index = 0; index < members.Length; index++)
            {
                requestMembers[index] = new LeaveGroupRequestMember
                {
                    MemberId = members[index].MemberId ?? string.Empty,
                    GroupInstanceId = members[index].GroupInstanceId,
                    Reason = version >= 5 ? options.Reason : null
                };
            }
            LeaveGroupResponse response;
            try
            {
                response = await SendObservingWriteAsync<LeaveGroupRequest, LeaveGroupResponse>(
                    writeContext, connection,
                    new LeaveGroupRequest { GroupId = groupId, Members = requestMembers }, version, attemptToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (writeContext.WriteStarted
                && !callerToken.IsCancellationRequested
                && (RetryHelper.IsRetriableRequestFailure(exception) || exception is OperationCanceledException))
            {
                // A lost response cannot prove which members were removed. Replaying a
                // static selector could also evict a replacement that joined meanwhile.
                // The call's deadline ending a send already being written is the same unknown
                // outcome; only the caller's own cancellation surfaces as cancellation. A failure
                // before the frame write starts sent nothing and is retried.
                throw new KafkaException((exception as KafkaException)?.ErrorCode ?? ErrorCode.NetworkException,
                    "LeaveGroup outcome is unknown after a request failure. Inspect group membership before retrying removal.",
                    isRetriable: false, exception);
            }
            // A response is authoritative: it is processed even when the deadline expired
            // meanwhile, because the removal it reports has already happened.
            if (MayHaveAppliedDespiteError(response.ErrorCode))
            {
                // The coordinator stopped waiting for the removal to commit, not that it dropped
                // it: the same unknown outcome as a response lost after the write.
                throw new KafkaException(response.ErrorCode,
                    "LeaveGroup outcome is unknown after an ambiguous answer. Inspect group membership before retrying removal.",
                    isRetriable: false, MemberRemovalError(groupId, response.ErrorCode));
            }
            if (response.ErrorCode != ErrorCode.None)
                throw MemberRemovalError(groupId, response.ErrorCode);

            var outcomes = new Dictionary<(bool Static, string Id), LeaveGroupResponseMember>();
            foreach (var member in response.Members)
            {
                var identity = (member.GroupInstanceId is not null, member.GroupInstanceId ?? member.MemberId);
                if (!outcomes.TryAdd(identity, member))
                    throw new KafkaException(ErrorCode.UnknownServerError, "LeaveGroup returned duplicate member outcomes.");
            }
            var results = new ConsumerGroupMemberRemovalResult[members.Length];
            for (var index = 0; index < members.Length; index++)
            {
                var member = members[index];
                var identity = (member.GroupInstanceId is not null, member.GroupInstanceId ?? member.MemberId!);
                outcomes.TryGetValue(identity, out var outcome);
                results[index] = new ConsumerGroupMemberRemovalResult
                {
                    GroupInstanceId = member.GroupInstanceId ?? string.Empty,
                    MemberId = member.MemberId ?? outcome?.MemberId ?? string.Empty,
                    ErrorCode = outcome?.ErrorCode ?? ErrorCode.UnknownServerError
                };
            }
            return new RemoveMembersFromConsumerGroupResult { GroupId = groupId, Members = results };
        }, cancellationToken, Timeout.Infinite, nameof(RemoveMembersFromConsumerGroupAsync)).ConfigureAwait(false);
    }

    private static GroupException MemberRemovalError(string groupId, ErrorCode errorCode) =>
        new(errorCode, $"RemoveMembersFromConsumerGroup failed for group '{groupId}': {errorCode}") { GroupId = groupId };
}
