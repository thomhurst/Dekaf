using System.Reflection;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Shared reflection helpers for tests that drive RecordAccumulator internals.
/// RecordAccumulatorTests and KafkaProducerFastPathTests carry older private copies of
/// the same batch-sealing dance; new tests should use this instead of adding a fourth.
/// </summary>
internal static class AccumulatorTestHelpers
{
    /// <summary>
    /// Seals the partition's current batch via reflection and returns the ReadyBatch.
    /// </summary>
    public static ReadyBatch CompleteCurrentBatch(RecordAccumulator accumulator, string topic, int partition = 0)
    {
        var topicPartition = new TopicPartition(topic, partition);

        var dequesField = typeof(RecordAccumulator).GetField("_partitionDeques",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var deques = dequesField!.GetValue(accumulator)!;

        var tryGetValueMethod = deques.GetType().GetMethod("TryGetValue");
        var parameters = new object[] { topicPartition, null! };
        tryGetValueMethod!.Invoke(deques, parameters);
        var partitionDeque = parameters[1]
            ?? throw new InvalidOperationException($"No partition deque exists for {topicPartition}; append a record first.");
        var currentBatchField = partitionDeque.GetType().GetField("CurrentBatch");
        var partitionBatch = currentBatchField!.GetValue(partitionDeque)
            ?? throw new InvalidOperationException($"Partition deque for {topicPartition} has no current batch.");

        var completeMethod = partitionBatch.GetType().GetMethod("Complete");
        return (ReadyBatch)completeMethod!.Invoke(partitionBatch, null)!;
    }

    /// <summary>
    /// Creates a MetadataManager with a single broker leading every partition of the topic.
    /// </summary>
    public static MetadataManager CreateMetadataManager(string topic, int partitionCount, int nodeId = 1)
    {
        var manager = new MetadataManager(connectionPool: null!, bootstrapServers: ["localhost:9092"]);

        var partitions = new List<PartitionMetadata>();
        for (var i = 0; i < partitionCount; i++)
        {
            partitions.Add(new PartitionMetadata
            {
                ErrorCode = ErrorCode.None,
                PartitionIndex = i,
                LeaderId = nodeId,
                ReplicaNodes = [nodeId],
                IsrNodes = [nodeId]
            });
        }

        manager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = nodeId, Host = "localhost", Port = 9092 }],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = topic,
                    Partitions = partitions
                }
            ]
        });
        return manager;
    }

    /// <summary>
    /// Extracts a tag value from a metric measurement's tag span by key.
    /// </summary>
    public static string? GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == key)
                return tag.Value?.ToString();
        }

        return null;
    }
}
