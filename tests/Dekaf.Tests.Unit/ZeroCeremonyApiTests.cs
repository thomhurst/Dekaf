// Tests for the static Kafka entry point API
// The Kafka class is in the Dekaf namespace, so with 'using Dekaf;' it's available directly

namespace Dekaf.Tests.Unit;

public class ZeroCeremonyApiTests
{
    [Test]
    public async Task CreateProducer_CreatesBuilder()
    {
        // Arrange & Act - Kafka class available via 'using Dekaf;'
        var builder = Kafka.CreateProducer<string, string>();

        // Assert
        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task CreateConsumer_CreatesBuilder()
    {
        // Arrange & Act - Kafka class available via 'using Dekaf;'
        var builder = Kafka.CreateConsumer<string, string>();

        // Assert
        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task ProducerBuilder_Build_ThrowsWithoutConfiguration()
    {
        // Arrange
        var builder = Kafka.CreateProducer<string, string>();

        // Act & Assert - Should throw because BootstrapServers is required
        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ConsumerBuilder_Build_ThrowsWithoutConfiguration()
    {
        // Arrange
        var builder = Kafka.CreateConsumer<string, string>();

        // Act & Assert - Should throw because BootstrapServers is required
        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }
}
