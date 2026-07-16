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

    [Test]
    [Arguments("6g", 6L * 1024 * 1024 * 1024)]
    [Arguments("512m", 512L * 1024 * 1024)]
    [Arguments("64k", 64L * 1024)]
    [Arguments("1024b", 1024L)]
    [Arguments("1024", 1024L)]
    [Arguments(" 6G ", 6L * 1024 * 1024 * 1024)]
    public async Task ParseByteSize_AcceptsDockerTmpfsSizeGrammar(string value, long expected)
    {
        await Assert.That(KafkaEnvironment.ParseByteSize(value)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("  ")]
    [Arguments("g")]
    [Arguments("-6g")]
    [Arguments("0g")]
    [Arguments("six gigs")]
    public async Task ParseByteSize_RejectsUnparseableValues(string? value)
    {
        await Assert.That(KafkaEnvironment.ParseByteSize(value)).IsNull();
    }
}
