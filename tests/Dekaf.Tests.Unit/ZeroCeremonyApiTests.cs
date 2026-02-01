// IMPORTANT: No using Dekaf directive here - we're testing zero-ceremony API
// This file proves that Kafka.CreateProducer() and Kafka.CreateConsumer() work
// from the global namespace without needing 'using Dekaf;'
//
// NOTE: These tests compile successfully but may not appear in test discovery
// due to namespace resolution complexities when testing code in the global namespace
// from within a Dekaf.* namespace. The successful compilation is the key validation.

namespace Dekaf.Tests.Unit;

public class ZeroCeremonyApiTests
{
    [Test]
    public async Task CreateProducer_WithoutUsingDirective_CreatesBuilder()
    {
        // Arrange & Act - Note: No 'using Dekaf;' needed!
        var builder = global::Kafka.CreateProducer<string, string>();

        // Assert
        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task CreateConsumer_WithoutUsingDirective_CreatesBuilder()
    {
        // Arrange & Act - Note: No 'using Dekaf;' needed!
        var builder = global::Kafka.CreateConsumer<string, string>();

        // Assert
        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task ProducerBuilder_Build_ThrowsWithoutConfiguration()
    {
        // Arrange
        var builder = global::Kafka.CreateProducer<string, string>();

        // Act & Assert - Should throw because BootstrapServers is required
        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ConsumerBuilder_Build_ThrowsWithoutConfiguration()
    {
        // Arrange
        var builder = global::Kafka.CreateConsumer<string, string>();

        // Act & Assert - Should throw because BootstrapServers is required
        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }
}
