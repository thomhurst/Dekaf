namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    internal Dictionary<TopicPartition, TopicPartitionOffset> GetShareGroupOffsetSnapshot(string groupId)
    {
        lock (_gate)
        {
            if (_shareGroupOffsets.TryGetValue(groupId, out var offsets))
                return new(offsets);
            return new();
        }
    }
}
