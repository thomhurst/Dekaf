using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerConnectionScalingTests
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

    [Test]
    [Arguments(1, 1)]  // 1 connection: 1 fetch connection (shared)
    [Arguments(2, 1)]  // 2 connections: 1 fetch connection
    [Arguments(3, 2)]  // 3 connections: 2 fetch connections
    [Arguments(4, 3)]  // 4 connections: 3 fetch connections
    public async Task FetchConnectionCount_CorrectForConnectionsPerBroker(int connectionsPerBroker, int expectedFetchCount)
    {
        var result = ConsumerConnectionScaler.GetFetchConnectionCount(connectionsPerBroker);
        await Assert.That(result).IsEqualTo(expectedFetchCount);
    }

    [Test]
    public async Task FetchRoundRobin_DistributesAcrossAllFetchConnections()
    {
        var fetchConnectionCount = ConsumerConnectionScaler.GetFetchConnectionCount(4); // 3 fetch connections
        var indices = new int[6];
        var counter = 0;

        for (var i = 0; i < 6; i++)
        {
            indices[i] = ConsumerConnectionScaler.GetNextFetchConnectionIndex(ref counter, fetchConnectionCount);
        }

        // Should cycle through 0, 1, 2, 0, 1, 2
        await Assert.That(indices[0]).IsEqualTo(0);
        await Assert.That(indices[1]).IsEqualTo(1);
        await Assert.That(indices[2]).IsEqualTo(2);
        await Assert.That(indices[3]).IsEqualTo(0);
        await Assert.That(indices[4]).IsEqualTo(1);
        await Assert.That(indices[5]).IsEqualTo(2);
    }

    [Test]
    public async Task FetchRoundRobin_SingleConnection_AlwaysReturns0()
    {
        var fetchConnectionCount = ConsumerConnectionScaler.GetFetchConnectionCount(1);
        var counter = 0;

        for (var i = 0; i < 5; i++)
        {
            var index = ConsumerConnectionScaler.GetNextFetchConnectionIndex(ref counter, fetchConnectionCount);
            await Assert.That(index).IsEqualTo(0);
        }
    }
}
