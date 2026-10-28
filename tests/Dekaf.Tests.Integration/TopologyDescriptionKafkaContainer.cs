using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration;

/// <summary>
/// A broker that stores Streams topology descriptions (KIP-1331) with the in-memory plugin that Kafka 4.4+
/// ships in its server jar. Older brokers ignore the unknown setting.
/// </summary>
public sealed class TopologyDescriptionKafkaContainer : KafkaContainerDefault
{
    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder) =>
        base.ConfigureBuilder(builder)
            .WithEnvironment(
                "KAFKA_GROUP_STREAMS_TOPOLOGY_DESCRIPTION_PLUGIN_CLASS",
                "org.apache.kafka.server.streams.InMemoryTopologyDescriptionPlugin");
}
