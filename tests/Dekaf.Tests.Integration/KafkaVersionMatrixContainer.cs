using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration;

public sealed class KafkaVersionMatrixContainer : KafkaTestContainer
{
    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder)
    {
        if (Version < KafkaTestImages.CurrentVersionNumber)
            return builder;

        // Enable share groups (KIP-932). The coordinator needs both the feature gate
        // and the share rebalance protocol. Single-broker state topics also need
        // replication settings compatible with one node.
        return builder
            .WithEnvironment("KAFKA_GROUP_SHARE_ENABLE", "true")
            .WithEnvironment("KAFKA_GROUP_COORDINATOR_REBALANCE_PROTOCOLS", "classic,consumer,share")
            .WithEnvironment("KAFKA_GROUP_SHARE_RECORD_LOCK_DURATION_MS", "15000")
            .WithEnvironment("KAFKA_GROUP_SHARE_MIN_RECORD_LOCK_DURATION_MS", "5000")
            .WithEnvironment("KAFKA_GROUP_SHARE_MAX_RECORD_LOCK_DURATION_MS", "60000")
            .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_MIN_ISR", "1")
            .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_NUM_PARTITIONS", "3");
    }
}
