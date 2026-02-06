using Dekaf.Admin;

namespace Dekaf.Tests.Unit.Builder;

public sealed class MetadataMaxAgeBuilderTests
{
    #region ProducerBuilder

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithMetadataMaxAge(TimeSpan.FromMinutes(5));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_Milliseconds_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(300000));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.Zero);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_Negative_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromSeconds(-1));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_ZeroMilliseconds_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(0));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_NegativeMilliseconds_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(-1000));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataMaxAge(TimeSpan.FromMinutes(5))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_Milliseconds_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataMaxAge(TimeSpan.FromMilliseconds(60000))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task ProducerBuilder_WithMetadataMaxAge_ChainsWithOtherMethods()
    {
        var act = () => Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("test")
            .WithMetadataMaxAge(TimeSpan.FromMinutes(5))
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region ConsumerBuilder

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithMetadataMaxAge(TimeSpan.FromMinutes(5));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_Milliseconds_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();
        var result = builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(300000));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.Zero);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_Negative_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromSeconds(-1));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_ZeroMilliseconds_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(0));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_NegativeMilliseconds_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(-1000));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataMaxAge(TimeSpan.FromMinutes(5))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_Milliseconds_ThenBuild_Succeeds()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataMaxAge(TimeSpan.FromMilliseconds(60000))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task ConsumerBuilder_WithMetadataMaxAge_ChainsWithOtherMethods()
    {
        var act = () => Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group")
            .WithMetadataMaxAge(TimeSpan.FromMinutes(5))
            .WithMaxPollRecords(100)
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region AdminClientBuilder

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_ReturnsSameBuilder()
    {
        var builder = new AdminClientBuilder();
        var result = builder.WithMetadataMaxAge(TimeSpan.FromMinutes(5));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_Milliseconds_ReturnsSameBuilder()
    {
        var builder = new AdminClientBuilder();
        var result = builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(300000));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = new AdminClientBuilder();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.Zero);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_Negative_ThrowsArgumentOutOfRangeException()
    {
        var builder = new AdminClientBuilder();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromSeconds(-1));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_ZeroMilliseconds_ThrowsArgumentOutOfRangeException()
    {
        var builder = new AdminClientBuilder();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(0));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_NegativeMilliseconds_ThrowsArgumentOutOfRangeException()
    {
        var builder = new AdminClientBuilder();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromMilliseconds(-1000));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_ThenBuild_Succeeds()
    {
        var act = () => new AdminClientBuilder()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataMaxAge(TimeSpan.FromMinutes(5))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_Milliseconds_ThenBuild_Succeeds()
    {
        var act = () => new AdminClientBuilder()
            .WithBootstrapServers("localhost:9092")
            .WithMetadataMaxAge(TimeSpan.FromMilliseconds(60000))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task AdminClientBuilder_WithMetadataMaxAge_ChainsWithOtherMethods()
    {
        var act = () => new AdminClientBuilder()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("test")
            .WithMetadataMaxAge(TimeSpan.FromMinutes(5))
            .Build();

        await Assert.That(act).ThrowsNothing();
    }

    #endregion
}
