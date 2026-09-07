using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IConsumerGroupMemberRemovalAdminClient
{
    ValueTask<RemoveMembersFromConsumerGroupResult> IConsumerGroupMemberRemovalAdminClient.RemoveMembersFromConsumerGroupAsync(
        string groupId, ConsumerGroupMemberRemovalOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        var members = ValidateMemberRemoval(options);
        cancellationToken.ThrowIfCancellationRequested();
        return ExecuteWithTimeoutAsync(
            token => RemoveGroupMembersCoreAsync(groupId, members, options, token),
            options.TimeoutMs, nameof(RemoveMembersFromConsumerGroupAsync), cancellationToken);
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
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (options.RemoveAll)
        {
            // Snapshot once outside mutation retries. Concurrent joins are not added to the request.
            var groups = await DescribeConsumerGroupsAsync([groupId], cancellationToken).ConfigureAwait(false);
            if (!groups.TryGetValue(groupId, out var group))
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

        return await WithRetryAsync(async () =>
        {
            var coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
            using var lease = await _connectionPool.LeaseConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);
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
            var response = await connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
                new LeaveGroupRequest { GroupId = groupId, Members = requestMembers }, version, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
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
        }, cancellationToken).ConfigureAwait(false);
    }

    private static GroupException MemberRemovalError(string groupId, ErrorCode errorCode) =>
        new(errorCode, $"RemoveMembersFromConsumerGroup failed for group '{groupId}': {errorCode}") { GroupId = groupId };
}
