using Dekaf.Producer;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Builder;

public sealed class ProducerBuilderEdgeCaseTests
{
    #region WithMaxBlock Validation

    [Test]
    public async Task WithMaxBlock_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMaxBlock(TimeSpan.Zero);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithMaxBlock_Negative_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMaxBlock(TimeSpan.FromSeconds(-1));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithMaxBlock_Positive_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithMaxBlock(TimeSpan.FromSeconds(30));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion

    #region WithMetadataMaxAge Validation

    [Test]
    public async Task WithMetadataMaxAge_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.Zero);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithMetadataMaxAge_Negative_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromMinutes(-1));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithMetadataMaxAge_Positive_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithMetadataMaxAge(TimeSpan.FromMinutes(5));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion

    #region WithGssapi Validation

    [Test]
    public async Task WithGssapi_Null_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithGssapi(null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    #endregion

    #region WithOAuthBearer Validation

    [Test]
    public async Task WithOAuthBearer_NullConfig_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithOAuthBearer((OAuthBearerConfig)null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task WithOAuthBearer_NullTokenProvider_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithOAuthBearer((Func<CancellationToken, ValueTask<OAuthBearerToken>>)null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    #endregion

    #region AddInterceptor Validation

    [Test]
    public async Task AddInterceptor_Null_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.AddInterceptor(null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    #endregion

    #region Builder Reuse Does Not Mutate Previous Options

    [Test]
    public async Task Build_ThenWithBootstrapServers_DoesNotMutatePreviousOptions()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("broker1:9092,broker2:9092");

        await using var first = builder.Build();

        // Mutate the builder after building
        builder.WithBootstrapServers("broker3:9092");

        // The first producer's options should be unchanged
        var options = GetProducerOptions(first);
        await Assert.That(options.BootstrapServers.Count).IsEqualTo(2);
        await Assert.That(options.BootstrapServers[0]).IsEqualTo("broker1:9092");
        await Assert.That(options.BootstrapServers[1]).IsEqualTo("broker2:9092");
    }

    private static ProducerOptions GetProducerOptions<TKey, TValue>(IKafkaProducer<TKey, TValue> producer)
    {
        var field = producer.GetType().GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find _options field");
        return (ProducerOptions)field.GetValue(producer)!;
    }

    #endregion
}

public sealed class ConsumerBuilderEdgeCaseTests
{
    #region WithMetadataMaxAge Validation

    [Test]
    public async Task WithMetadataMaxAge_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.Zero);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithMetadataMaxAge_Negative_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithMetadataMaxAge(TimeSpan.FromMinutes(-1));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    #endregion

    #region WithGssapi Validation

    [Test]
    public async Task WithGssapi_Null_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithGssapi(null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    #endregion

    #region WithOAuthBearer Validation

    [Test]
    public async Task WithOAuthBearer_NullConfig_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithOAuthBearer((OAuthBearerConfig)null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task WithOAuthBearer_NullTokenProvider_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithOAuthBearer((Func<CancellationToken, ValueTask<OAuthBearerToken>>)null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    #endregion

    #region WithGroupRemoteAssignor Validation

    [Test]
    public async Task WithGroupRemoteAssignor_Null_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithGroupRemoteAssignor(null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Build_WithGroupRemoteAssignor_WithoutConsumerProtocol_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupRemoteAssignor("uniform");

        var act = () => builder.Build();

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    #endregion

    #region AddInterceptor Validation

    [Test]
    public async Task AddInterceptor_Null_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.AddInterceptor(null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    #endregion

    #region Builder Reuse Does Not Mutate Previous Options

    [Test]
    public async Task Build_ThenWithBootstrapServers_DoesNotMutatePreviousOptions()
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("broker1:9092,broker2:9092");

        await using var first = builder.Build();

        // Mutate the builder after building
        builder.WithBootstrapServers("broker3:9092");

        // The first consumer's options should be unchanged
        var options = GetConsumerOptions(first);
        await Assert.That(options.BootstrapServers.Count).IsEqualTo(2);
        await Assert.That(options.BootstrapServers[0]).IsEqualTo("broker1:9092");
        await Assert.That(options.BootstrapServers[1]).IsEqualTo("broker2:9092");
    }

    private static Dekaf.Consumer.ConsumerOptions GetConsumerOptions<TKey, TValue>(Dekaf.Consumer.IKafkaConsumer<TKey, TValue> consumer)
    {
        var field = consumer.GetType().GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find _options field");
        return (Dekaf.Consumer.ConsumerOptions)field.GetValue(consumer)!;
    }

    #endregion
}
