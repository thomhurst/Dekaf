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

    #region WithStatisticsInterval Validation

    [Test]
    public async Task WithStatisticsInterval_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithStatisticsInterval(TimeSpan.Zero);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithStatisticsInterval_Negative_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithStatisticsInterval(TimeSpan.FromSeconds(-1));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithStatisticsInterval_Positive_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();
        var result = builder.WithStatisticsInterval(TimeSpan.FromSeconds(5));
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

    #region WithStatisticsHandler Validation

    [Test]
    public async Task WithStatisticsHandler_Null_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var act = () => builder.WithStatisticsHandler(null!);

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
}

public sealed class ConsumerBuilderEdgeCaseTests
{
    #region WithStatisticsInterval Validation

    [Test]
    public async Task WithStatisticsInterval_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithStatisticsInterval(TimeSpan.Zero);

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WithStatisticsInterval_Negative_ThrowsArgumentOutOfRangeException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithStatisticsInterval(TimeSpan.FromSeconds(-1));

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    #endregion

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

    #region WithStatisticsHandler Validation

    [Test]
    public async Task WithStatisticsHandler_Null_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var act = () => builder.WithStatisticsHandler(null!);

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
}
