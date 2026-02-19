using Dekaf.Resilience;

namespace Dekaf.Tests.Unit.Resilience;

public class CircuitBreakerBuilderTests
{
    #region Producer Builder

    [Test]
    public async Task ProducerBuilder_WithCircuitBreaker_DefaultOptions_Builds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithCircuitBreaker()
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task ProducerBuilder_WithCircuitBreaker_CustomOptions_Builds()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithCircuitBreaker(options =>
            {
                options.FailureThreshold = 10;
                options.BreakDuration = TimeSpan.FromMinutes(1);
                options.HalfOpenMaxAttempts = 3;
            })
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task ProducerBuilder_WithCircuitBreaker_NullConfigure_Throws()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092");

        await Assert.That(() => builder.WithCircuitBreaker(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ProducerBuilder_WithCircuitBreaker_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var result = builder.WithCircuitBreaker();

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion

    #region Consumer Builder

    [Test]
    public async Task ConsumerBuilder_WithCircuitBreaker_DefaultOptions_Builds()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group")
            .WithCircuitBreaker()
            .Build();

        await Assert.That(consumer).IsNotNull();
    }

    [Test]
    public async Task ConsumerBuilder_WithCircuitBreaker_CustomOptions_Builds()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group")
            .WithCircuitBreaker(options =>
            {
                options.FailureThreshold = 7;
                options.BreakDuration = TimeSpan.FromSeconds(45);
                options.HalfOpenMaxAttempts = 4;
            })
            .Build();

        await Assert.That(consumer).IsNotNull();
    }

    [Test]
    public async Task ConsumerBuilder_WithCircuitBreaker_NullConfigure_Throws()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group");

        await Assert.That(() => builder.WithCircuitBreaker(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ConsumerBuilder_WithCircuitBreaker_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group");

        var result = builder.WithCircuitBreaker();

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion
}
