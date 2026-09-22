using DotNet.Testcontainers.Builders;

namespace Dekaf.Tests.Integration;

/// <summary>
/// The <see cref="TransactionFaultKafkaContainer"/> proxy topology on a broker with share groups
/// enabled (KIP-932). The image is pinned to the current default rather than following
/// <c>KAFKA_TEST_IMAGE_TAG</c>, because the release gate also sweeps brokers without share groups.
/// </summary>
public sealed class ShareFaultKafkaContainer : TransactionFaultKafkaContainer
{
    public override string ContainerName => $"apache/kafka:{KafkaContainerDefault.DefaultTag}";

    public override int Version { get; } = KafkaContainerDefault.ParseVersion(KafkaContainerDefault.DefaultTag);

    // The same share settings as KafkaContainerDefault, sized for a single broker.
    protected override ContainerBuilder ConfigureKafka(ContainerBuilder builder) => builder
        .WithEnvironment("KAFKA_GROUP_SHARE_ENABLE", "true")
        .WithEnvironment("KAFKA_GROUP_COORDINATOR_REBALANCE_PROTOCOLS", "classic,consumer,share")
        .WithEnvironment("KAFKA_GROUP_SHARE_RECORD_LOCK_DURATION_MS", "15000")
        .WithEnvironment("KAFKA_GROUP_SHARE_MIN_RECORD_LOCK_DURATION_MS", "5000")
        .WithEnvironment("KAFKA_GROUP_SHARE_MAX_RECORD_LOCK_DURATION_MS", "60000")
        .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_REPLICATION_FACTOR", "1")
        .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_MIN_ISR", "1")
        .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_NUM_PARTITIONS", "3");
}
