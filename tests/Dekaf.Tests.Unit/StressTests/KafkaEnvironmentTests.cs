using Dekaf.StressTests.Infrastructure;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class KafkaEnvironmentTests
{
    [Test]
    public async Task SingleBrokerConfiguration_EnablesKip848ConsumerProtocol()
    {
        await Assert.That(KafkaEnvironment.SingleBrokerImage)
            .IsEqualTo("confluentinc/cp-kafka:7.8.0");
        await Assert.That(KafkaEnvironment.SingleBrokerConsensusProtocol)
            .IsEqualTo(ConsensusProtocol.KRaft);
        await Assert.That(KafkaEnvironment.SingleBrokerEnvironment)
            .ContainsKey("KAFKA_GROUP_COORDINATOR_REBALANCE_PROTOCOLS");
        await Assert.That(KafkaEnvironment.SingleBrokerEnvironment["KAFKA_GROUP_COORDINATOR_REBALANCE_PROTOCOLS"])
            .IsEqualTo("classic,consumer");
    }
}
