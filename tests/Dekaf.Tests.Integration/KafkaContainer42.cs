using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration;

public class KafkaContainer42 : KafkaTestContainer
{
    public override string ContainerName => "apache/kafka:4.2.0";
    public override int Version => 420;

    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder)
    {
        return builder
            .WithEnvironment("KAFKA_GROUP_SHARE_ENABLE", "true")
            .WithEnvironment("KAFKA_GROUP_SHARE_RECORD_LOCK_DURATION_MS", "15000")
            .WithEnvironment("KAFKA_GROUP_SHARE_MIN_RECORD_LOCK_DURATION_MS", "5000")
            .WithEnvironment("KAFKA_GROUP_SHARE_MAX_RECORD_LOCK_DURATION_MS", "60000");
    }
}
