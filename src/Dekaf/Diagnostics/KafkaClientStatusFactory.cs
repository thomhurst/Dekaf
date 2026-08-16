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
        ConsumerGroupStatus? consumerGroup = null)
    {
        var metadata = metadataManager.Metadata;
        var lastRefreshed = metadata.LastRefreshed;
        return new KafkaClientStatus
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Role = role,
            ClusterId = metadataManager.ClusterId,
            MetadataLastRefreshedAtUtc = lastRefreshed == default ? null : lastRefreshed,
            IsStopped = isStopped,
            Brokers = connectionPool is IConnectionPoolStatusSource statusSource
                ? statusSource.GetBrokerConnectionStatus()
                : Array.Empty<BrokerConnectionStatus>(),
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
