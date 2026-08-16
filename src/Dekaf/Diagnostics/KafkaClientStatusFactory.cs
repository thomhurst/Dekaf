using System.Collections.ObjectModel;
using Dekaf.Metadata;
using Dekaf.Networking;

namespace Dekaf.Diagnostics;

internal static class KafkaClientStatusFactory
{
    public static KafkaClientStatus Capture(
        KafkaClientRole role,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        bool isStopped,
        ProducerBacklogStatus? producer = null,
        ConsumerGroupStatus? consumerGroup = null) =>
        Capture(
            role,
            connectionPool,
            metadataManager.ClusterId,
            metadataManager.Metadata.LastRefreshed,
            isStopped,
            producer,
            consumerGroup);

    public static KafkaClientStatus Capture(
        KafkaClientRole role,
        IConnectionPool connectionPool,
        string? clusterId,
        DateTimeOffset metadataLastRefreshed,
        bool isStopped,
        ProducerBacklogStatus? producer = null,
        ConsumerGroupStatus? consumerGroup = null,
        IReadOnlyList<BrokerConnectionStatus>? brokers = null)
    {
        return new KafkaClientStatus
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Role = role,
            ClusterId = clusterId,
            MetadataLastRefreshedAtUtc = metadataLastRefreshed == default
                ? null
                : metadataLastRefreshed,
            IsStopped = isStopped,
            Brokers = brokers
                ?? (connectionPool is IConnectionPoolStatusSource statusSource
                    ? statusSource.GetBrokerConnectionStatus()
                    : Array.Empty<BrokerConnectionStatus>()),
            Producer = producer,
            ConsumerGroup = consumerGroup
        };
    }

    public static IReadOnlyList<TopicPartition> CopyAssignment(
        IEnumerable<TopicPartition> assignment,
        int count)
    {
        if (count == 0)
            return Array.Empty<TopicPartition>();

        var copy = new TopicPartition[count];
        var index = 0;
        foreach (var topicPartition in assignment)
        {
            if ((uint)index >= (uint)copy.Length)
                break;

            copy[index++] = topicPartition;
        }

        if (index != copy.Length)
            Array.Resize(ref copy, index);

        return new ReadOnlyCollection<TopicPartition>(copy);
    }
}
