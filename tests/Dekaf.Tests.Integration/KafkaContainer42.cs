using System.Text;
using DotNet.Testcontainers.Configurations;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration;

public class KafkaContainer42 : KafkaTestContainer
{
    public override string ContainerName => "apache/kafka:4.2.0";
    public override int Version => 420;

    // Testcontainers.Kafka generates a startup script that sets
    // KAFKA_ADVERTISED_LISTENERS with a trailing comma when no extra
    // listeners are configured. Kafka 4.2+ (KIP-1161) rejects empty
    // values in LIST-type configs, so the trailing comma causes a
    // ConfigException. This wrapper strips the trailing comma before
    // running the normal Kafka startup sequence.
    private static readonly byte[] RunWrapperScript = Encoding.UTF8.GetBytes(
        "#!/bin/bash\n" +
        "export KAFKA_ADVERTISED_LISTENERS=$(echo \"$KAFKA_ADVERTISED_LISTENERS\" | sed 's/,$//')\n" +
        "/etc/kafka/docker/configure\n" +
        "exec /etc/kafka/docker/launch\n");

    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder)
    {
        return builder
            .WithResourceMapping(RunWrapperScript, "/etc/kafka/docker/run", 0, 0,
                UnixFileModes.UserRead | UnixFileModes.UserWrite | UnixFileModes.UserExecute |
                UnixFileModes.GroupRead | UnixFileModes.GroupExecute |
                UnixFileModes.OtherRead | UnixFileModes.OtherExecute)
            // Enable share groups (KIP-932). Requires two pieces:
            // 1. group.share.enable — gates the share group client protocol
            // 2. group.coordinator.rebalance.protocols must include "share"
            //    for the coordinator to handle share group state
            .WithEnvironment("KAFKA_GROUP_SHARE_ENABLE", "true")
            .WithEnvironment("KAFKA_GROUP_COORDINATOR_REBALANCE_PROTOCOLS", "classic,consumer,share")
            .WithEnvironment("KAFKA_GROUP_SHARE_RECORD_LOCK_DURATION_MS", "15000")
            .WithEnvironment("KAFKA_GROUP_SHARE_MIN_RECORD_LOCK_DURATION_MS", "5000")
            .WithEnvironment("KAFKA_GROUP_SHARE_MAX_RECORD_LOCK_DURATION_MS", "60000")
            // Single-broker: the internal __share_group_state topic requires settings
            // compatible with a single-node cluster.
            .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_MIN_ISR", "1")
            .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_NUM_PARTITIONS", "3");
    }
}
