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
            if (_streamsGroupIds?.Contains(groupId) == true || _shareGroupMembers.ContainsKey(groupId)
                || _shareGroupsWithMemberHistory.Contains(groupId))
                throw new GroupException(ErrorCode.UnsupportedVersion, "Member removal requires a consumer group.") { GroupId = groupId };
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
            var results = new ConsumerGroupMemberRemovalResult[requested.Length];
            _consumerGroupMembers.TryGetValue(groupId, out var members);
            var changed = false;
            for (var index = 0; index < requested.Length; index++)
            {
                var identity = requested[index];
                var memberId = identity.MemberId;
                if (identity.GroupInstanceId is { } instanceId && members is not null)
                {
                    foreach (var entry in members)
                    {
                        if (entry.Value.GroupInstanceId == instanceId)
                        {
                            memberId = entry.Key;
                            break;
                        }
                    }
                }
                var removed = memberId is not null && members?.Remove(memberId) == true;
                changed |= removed;
                results[index] = new ConsumerGroupMemberRemovalResult
                {
                    GroupInstanceId = identity.GroupInstanceId ?? string.Empty,
                    MemberId = memberId ?? string.Empty,
                    ErrorCode = removed ? ErrorCode.None : ErrorCode.UnknownMemberId
                };
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
}
