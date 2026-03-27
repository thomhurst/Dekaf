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
}
