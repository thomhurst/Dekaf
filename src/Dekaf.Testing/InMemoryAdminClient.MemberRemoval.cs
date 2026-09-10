using Dekaf.Admin;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IConsumerGroupMemberRemovalAdminClient
{
    ValueTask<RemoveMembersFromConsumerGroupResult> IConsumerGroupMemberRemovalAdminClient.RemoveMembersFromConsumerGroupAsync(
        string groupId, ConsumerGroupMemberRemovalOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        var members = AdminClient.ValidateMemberRemoval(options);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        AdminClient.ThrowIfMemberRemovalDeadlineExpired(options);
        return ExecuteWithTimeoutAsync(async token =>
        {
            await ApplyAdminFaultAsync(token, groupId: groupId).ConfigureAwait(false);
            if (options.RemoveAll)
            {
                members = _cluster.SnapshotConsumerGroupMembers(groupId);
                // Discovery and eviction have separate fault boundaries, like the broker API.
                if (members.Length != 0)
                    await ApplyAdminFaultAsync(token, groupId: groupId).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            return _cluster.RemoveConsumerGroupMembers(groupId, members);
        }, options.TimeoutMs, nameof(RemoveMembersFromConsumerGroupAsync), cancellationToken);
    }
}
