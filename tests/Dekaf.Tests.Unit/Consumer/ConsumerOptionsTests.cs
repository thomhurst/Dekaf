using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public class ConsumerOptionsTests
{
    [Test]
    public async Task EnablePartitionEof_DefaultIsFalse()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };

        await Assert.That(options.EnablePartitionEof).IsFalse();
    }

    [Test]
    public async Task EnablePartitionEof_CanBeSetToTrue()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            EnablePartitionEof = true
        };

        await Assert.That(options.EnablePartitionEof).IsTrue();
    }
}
