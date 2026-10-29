using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    internal ErrorCode CreateTopicDetailed(CreateTopicData topic, bool validateOnly)
    {
        lock (_gate)
        {
            if (_topics.ContainsKey(topic.Name)) return ErrorCode.TopicAlreadyExists;
            var partitions = topic.NumPartitions == -1 ? _options.DefaultPartitionCount : topic.NumPartitions;
            if (topic.Assignments is { Count: > 0 } assignments)
            {
                if (topic.NumPartitions != -1 || topic.ReplicationFactor != -1) return ErrorCode.InvalidRequest;
                partitions = assignments.Count;
                foreach (var assignment in assignments)
                    if ((uint)assignment.PartitionIndex >= (uint)partitions ||
                        assignment.BrokerIds.Count != 1 || assignment.BrokerIds[0] != 0)
                        return ErrorCode.InvalidReplicaAssignment;
            }
            else if (topic.ReplicationFactor > 1)
                return ErrorCode.InvalidReplicationFactor;

            if (!validateOnly)
            {
                var configs = topic.Configs?.ToDictionary(static config => config.Name, static config => config.Value!);
                EnsureTopic(topic.Name, partitions, configs);
            }
            return ErrorCode.None;
        }
    }

    internal ErrorCode AlterPartitionReassignmentDetailed(TopicPartition partition, int[]? replicas)
    {
        lock (_gate)
        {
            if (!_topics.TryGetValue(partition.Topic, out var topic) || (uint)partition.Partition >= (uint)topic.Partitions.Count)
                return ErrorCode.UnknownTopicOrPartition;
            // The in-memory cluster has one broker and no asynchronous replica movement.
            if (replicas is null) return ErrorCode.NoReassignmentInProgress;
            return replicas.Length == 1 && replicas[0] == 0 ? ErrorCode.None : ErrorCode.InvalidReplicaAssignment;
        }
    }
}
