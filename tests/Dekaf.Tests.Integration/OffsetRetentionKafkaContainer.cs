using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration;

public sealed class OffsetRetentionKafkaContainer : KafkaContainerDefault
{
    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder) =>
        base.ConfigureBuilder(builder)
            .WithEnvironment("KAFKA_OFFSETS_RETENTION_MINUTES", "1")
            .WithEnvironment("KAFKA_OFFSETS_RETENTION_CHECK_INTERVAL_MS", "1000")
            .WithEnvironment("KAFKA_LOG_RETENTION_MS", "600000");
}
