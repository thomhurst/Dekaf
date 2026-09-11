using Dekaf.Protocol;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    internal ErrorCode AlterConsumerGroupOffsetDetailed(string groupId, TopicPartitionOffset offset)
    {
        lock (_gate)
        {
            if (_consumerGroupMembers.TryGetValue(groupId, out var members) && members.Count != 0)
                return ErrorCode.UnknownMemberId;
            var partition = new TopicPartition(offset.Topic, offset.Partition);
            var error = GetTopicPartitionErrorUnderLock(partition);
            if (error != ErrorCode.None) return ConsumerOffsetError(error);
            GetOrCreateConsumerGroupOffsetsUnderLock(groupId)[partition] = offset;
            return ErrorCode.None;
        }
    }

    internal ErrorCode DeleteConsumerGroupOffsetDetailed(string groupId, TopicPartition partition) =>
        ConsumerOffsetError(DeleteStreamsGroupOffsets(groupId, [partition])[partition]);

    // The simulator's consumer-group APIs accept topic names. OffsetDelete has no topic-ID version.
    private static ErrorCode ConsumerOffsetError(ErrorCode error) =>
        error == ErrorCode.UnknownTopicId ? ErrorCode.UnknownTopicOrPartition : error;
}
