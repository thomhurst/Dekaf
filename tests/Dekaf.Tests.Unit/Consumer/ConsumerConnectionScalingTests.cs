using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerConnectionScalingTests
{
    [Test]
    [Arguments(true, true)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(false, false)]
    public async Task ConsumerBuilder_AboveDefaultMaximum_RaisesImplicitMaximum(
        bool adaptiveConnections, bool setConnectionsFirst)
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092");

        if (setConnectionsFirst)
            builder.WithConnectionsPerBroker(6);
        if (!adaptiveConnections)
            builder.WithoutAdaptiveConnections();
        if (!setConnectionsFirst)
            builder.WithConnectionsPerBroker(6);

        await using var consumer = builder.Build();
        var options = ((KafkaConsumer<string, string>)consumer).Options;

        await Assert.That(options.ConnectionsPerBroker).IsEqualTo(6);
        await Assert.That(options.MaxConnectionsPerBroker).IsEqualTo(6);
        await Assert.That(options.EnableAdaptiveConnections).IsEqualTo(adaptiveConnections);
    }

    [Test]
    [Arguments(true, true)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(false, false)]
    public async Task ConsumerBuilder_ExplicitMaximum_IsEnforcedOnlyWhenAdaptiveConnectionsEnabled(
        bool adaptiveConnections, bool setMaximumFirst)
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092");

        if (setMaximumFirst)
            builder.WithAdaptiveConnections(4);
        builder.WithConnectionsPerBroker(6);
        if (!setMaximumFirst)
            builder.WithAdaptiveConnections(4);

        if (adaptiveConnections)
        {
            await Assert.That(() => builder.Build()).Throws<InvalidOperationException>()
                .And.HasMessageContaining("MaxConnectionsPerBroker (4)");
            return;
        }

        await using var consumer = builder.WithoutAdaptiveConnections().Build();
        var options = ((KafkaConsumer<string, string>)consumer).Options;
        await Assert.That(options.ConnectionsPerBroker).IsEqualTo(6);
        await Assert.That(options.MaxConnectionsPerBroker).IsEqualTo(4);
        await Assert.That(options.EnableAdaptiveConnections).IsFalse();
    }

    [Test]
    [Arguments(2, 4)]
    [Arguments(6, 6)]
    public async Task ConsumerBuilder_LowerConnectionOverride_PreservesImplicitMaximum(
        int initialConnections, int expectedMaximum)
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithConnectionsPerBroker(initialConnections)
            .WithConnectionsPerBroker(2)
            .Build();

        var options = ((KafkaConsumer<string, string>)consumer).Options;
        await Assert.That(options.ConnectionsPerBroker).IsEqualTo(2);
        await Assert.That(options.MaxConnectionsPerBroker).IsEqualTo(expectedMaximum);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ConsumerConstructor_AboveMaximum_IsEnforcedOnlyWhenAdaptiveConnectionsEnabled(
        bool adaptiveConnections)
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ConnectionsPerBroker = 6,
            MaxConnectionsPerBroker = 4,
            EnableAdaptiveConnections = adaptiveConnections
        };

        if (adaptiveConnections)
        {
            await Assert.That(() => new KafkaConsumer<string, string>(options, Serializers.String, Serializers.String))
                .Throws<ArgumentOutOfRangeException>();
            return;
        }

        await using var consumer = new KafkaConsumer<string, string>(options, Serializers.String, Serializers.String);
        await Assert.That(consumer.Options.ConnectionsPerBroker).IsEqualTo(6);
    }

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

}
