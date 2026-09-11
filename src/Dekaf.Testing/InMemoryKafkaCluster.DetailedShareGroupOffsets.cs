using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    internal ErrorCode GetShareGroupMutationError(string groupId, bool allowMissing)
    {
        lock (_gate) return GetShareGroupMutationErrorUnderLock(groupId, allowMissing);
    }

    private ErrorCode GetShareGroupMutationErrorUnderLock(string groupId, bool allowMissing = false)
    {
        if (_shareGroupMembers.TryGetValue(groupId, out var members) && members.Count != 0)
            return ErrorCode.NonEmptyGroup;
        return allowMissing || _shareGroupsWithMemberHistory.Contains(groupId) || _shareGroupOffsets.ContainsKey(groupId)
            ? ErrorCode.None : ErrorCode.GroupIdNotFound;
    }

    internal ErrorCode AlterShareGroupOffsetDetailed(string groupId, ShareGroupOffsetAlteration item)
    {
        lock (_gate)
        {
            var groupError = GetShareGroupMutationErrorUnderLock(groupId, allowMissing: true);
            if (groupError != ErrorCode.None) return groupError;
            // Kafka creates an empty share group before checking individual partition outcomes.
            if (!_shareGroupOffsets.ContainsKey(groupId)) _shareGroupOffsets.Add(groupId, new());
            var error = GetTopicPartitionErrorUnderLock(item.TopicPartition);
            if (error != ErrorCode.None) return error == ErrorCode.UnknownTopicId ? ErrorCode.UnknownTopicOrPartition : error;
            if (item.StartOffset < 0) return ErrorCode.InvalidRequest;
            CommitShareOffsetsUnderLock(groupId, [new(item.TopicPartition.Topic, item.TopicPartition.Partition, item.StartOffset)]);
            return ErrorCode.None;
        }
    }

    internal ErrorCode DeleteShareGroupOffsetsDetailed(string groupId, string topicName)
    {
        lock (_gate)
        {
            var groupError = GetShareGroupMutationErrorUnderLock(groupId);
            if (groupError != ErrorCode.None) return groupError;
            if (!_topics.ContainsKey(topicName)) return ErrorCode.UnknownTopicOrPartition;
            if (_shareGroupOffsets.TryGetValue(groupId, out var offsets))
            {
                // Keep the group identity after deleting its final offset, as the broker does.
                var removed = offsets.Keys.Where(partition => StringComparer.Ordinal.Equals(partition.Topic, topicName)).ToArray();
                foreach (var partition in removed) offsets.Remove(partition);
            }
            return ErrorCode.None;
        }
    }
}
