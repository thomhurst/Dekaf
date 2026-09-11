using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    internal ConsumerGroupMemberIdentity[] SnapshotConsumerGroupMembers(string groupId)
    {
        lock (_gate)
        {
            ValidateConsumerGroupForMemberRemovalUnderLock(groupId);
            if (!_consumerGroupMembers.TryGetValue(groupId, out var members))
            {
                if (_consumerGroupOffsets.ContainsKey(groupId) || _consumerGroupGenerations.ContainsKey(groupId))
                    return [];
                throw new GroupException(ErrorCode.GroupIdNotFound, $"Consumer group '{groupId}' does not exist.") { GroupId = groupId };
            }
            var snapshot = new ConsumerGroupMemberIdentity[members.Count];
            var index = 0;
            foreach (var entry in members)
                snapshot[index++] = entry.Value.GroupInstanceId is { } instanceId
                    ? new ConsumerGroupMemberIdentity { GroupInstanceId = instanceId }
                    : new ConsumerGroupMemberIdentity { MemberId = entry.Key };
            return snapshot;
        }
    }

    internal RemoveMembersFromConsumerGroupResult RemoveConsumerGroupMembers(
        string groupId, ConsumerGroupMemberIdentity[] requested)
    {
        lock (_gate)
        {
            ValidateConsumerGroupForMemberRemovalUnderLock(groupId);
            var results = new ConsumerGroupMemberRemovalResult[requested.Length];
            _consumerGroupMembers.TryGetValue(groupId, out var members);
            // Consumer-group LeaveGroup resolves the entire request before evicting members.
            // A static and dynamic identity may refer to the same member in this snapshot.
            for (var index = 0; index < requested.Length; index++)
            {
                var identity = requested[index];
                var memberId = identity.MemberId;
                if (identity.GroupInstanceId is { } instanceId)
                    memberId = _staticConsumerGroupMembers?.GetValueOrDefault((groupId, instanceId));
                results[index] = new ConsumerGroupMemberRemovalResult
                {
                    GroupInstanceId = identity.GroupInstanceId ?? string.Empty,
                    MemberId = memberId ?? string.Empty,
                    ErrorCode = memberId is not null && members is not null && members.ContainsKey(memberId)
                        ? ErrorCode.None : ErrorCode.UnknownMemberId
                };
            }
            var changed = false;
            foreach (var result in results)
            {
                if (result.Succeeded && members!.Remove(result.MemberId, out var member))
                {
                    changed = true;
                    if (member.GroupInstanceId is { } removedInstance)
                        _staticConsumerGroupMembers!.Remove((groupId, removedInstance));
                }
            }
            if (changed)
            {
                if (members!.Count == 0)
                {
                    _consumerGroupMembers.Remove(groupId);
                    _consumerGroupGenerations[groupId] = 0;
                }
                else
                    _consumerGroupGenerations[groupId] = ++_nextConsumerGroupGeneration;
            }
            return new RemoveMembersFromConsumerGroupResult { GroupId = groupId, Members = results };
        }
    }

    private void ValidateConsumerGroupForMemberRemovalUnderLock(string groupId)
    {
        if (_streamsGroupIds?.Contains(groupId) == true || _shareGroupMembers.ContainsKey(groupId)
            || _shareGroupsWithMemberHistory.Contains(groupId))
            throw new GroupException(ErrorCode.UnsupportedVersion, "Member removal requires a consumer group.") { GroupId = groupId };
    }
}
