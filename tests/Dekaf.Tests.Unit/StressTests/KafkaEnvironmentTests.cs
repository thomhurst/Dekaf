using Dekaf.StressTests.Infrastructure;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class KafkaEnvironmentTests
{
    [Test]
    public async Task StressBrokerConfiguration_UsesLatestKafka()
    {
        await Assert.That(KafkaEnvironment.KafkaImage)
            .IsEqualTo("apache/kafka:4.3.1");
        await Assert.That(KafkaEnvironment.SingleBrokerConsensusProtocol)
            .IsEqualTo(ConsensusProtocol.KRaft);
    }
}
