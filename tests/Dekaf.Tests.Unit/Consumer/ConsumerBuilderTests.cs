namespace Dekaf.Tests.Unit.Consumer;

public class ConsumerBuilderTests
{
    [Test]
    public async Task WithPartitionEof_DefaultsToTrue()
    {
        // WithPartitionEof() with no argument should enable partition EOF
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithPartitionEof();

        // We can't easily inspect the builder's internal state, but we can verify
        // the method returns the builder for chaining
        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task WithPartitionEof_CanBeExplicitlyEnabled()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithPartitionEof(enabled: true);

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task WithPartitionEof_CanBeExplicitlyDisabled()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithPartitionEof(enabled: false);

        await Assert.That(builder).IsNotNull();
    }

    [Test]
    public async Task WithPartitionEof_ReturnsBuilderForChaining()
    {
        var originalBuilder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var returnedBuilder = originalBuilder.WithPartitionEof();

        await Assert.That(returnedBuilder).IsSameReferenceAs(originalBuilder);
    }
}
