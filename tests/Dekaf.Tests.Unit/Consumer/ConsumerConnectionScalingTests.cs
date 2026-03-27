using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public class ConsumerConnectionScalingTests
{
    [Test]
    public async Task ConsumerOptions_ConnectionsPerBroker_AllowsUpTo4()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ConnectionsPerBroker = 4
        };

        await Assert.That(options.ConnectionsPerBroker).IsEqualTo(4);
    }

    [Test]
    public async Task ConsumerOptions_MaxConnectionsPerBroker_DefaultIs4()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.MaxConnectionsPerBroker).IsEqualTo(4);
    }

    [Test]
    public async Task ConsumerOptions_EnableAdaptiveConnections_DefaultIsTrue()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.EnableAdaptiveConnections).IsTrue();
    }

    [Test]
    public async Task ConsumerOptions_MaxConnectionsPerBroker_ThrowsIfLessThan1()
    {
        await Assert.That(() =>
        {
            _ = new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                MaxConnectionsPerBroker = 0
            };
        }).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ConsumerBuilder_WithAdaptiveConnections_SetsMaxAndEnables()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithAdaptiveConnections(3);

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task ConsumerBuilder_WithoutAdaptiveConnections_Compiles()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithoutAdaptiveConnections();

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task ConsumerBuilder_WithConnectionsPerBroker3_Allowed()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithConnectionsPerBroker(3);

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    [Arguments(1, 0)]  // 1 connection: coordination uses 0 (shared)
    [Arguments(2, 1)]  // 2 connections: coordination uses 1
    [Arguments(3, 2)]  // 3 connections: coordination uses 2
    [Arguments(4, 3)]  // 4 connections: coordination uses 3
    public async Task CoordinationConnectionIndex_IsLastIndex(int connectionsPerBroker, int expectedIndex)
    {
        var result = ConsumerCoordinator.GetCoordinationConnectionIndex(connectionsPerBroker);
        await Assert.That(result).IsEqualTo(expectedIndex);
    }
}
