using System.Security.Cryptography;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Builder;

public sealed class OAuthBearerJwtBearerBuilderTests
{
    [Test]
    public async Task Producer_WithOAuthBearerJwtBearer_ReturnsSameBuilder()
    {
        using var rsa = RSA.Create(2048);
        var builder = Kafka.CreateProducer<string, string>();

        var result = builder.WithOAuthBearerJwtBearer(CreateOptions(rsa));

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task Producer_WithOAuthBearerJwtBearerAction_ReturnsSameBuilder()
    {
        using var rsa = RSA.Create(2048);
        var builder = Kafka.CreateProducer<string, string>();

        var result = builder.WithOAuthBearerJwtBearer(options =>
        {
            options.TokenEndpoint = "https://auth.example.test/token";
            options.ClientId = "client";
            options.PrivateKey = rsa;
            options.Audience = "kafka";
            options.Scopes = ["kafka"];
        });

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task Consumer_WithOAuthBearerJwtBearer_ReturnsSameBuilder()
    {
        using var rsa = RSA.Create(2048);
        var builder = Kafka.CreateConsumer<string, string>();

        var result = builder.WithOAuthBearerJwtBearer(CreateOptions(rsa));

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ShareConsumer_WithOAuthBearerJwtBearer_ReturnsSameBuilder()
    {
        using var rsa = RSA.Create(2048);
        var builder = Kafka.CreateShareConsumer<string, string>();

        var result = builder.WithOAuthBearerJwtBearer(CreateOptions(rsa));

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task AdminClient_WithOAuthBearerJwtBearer_ReturnsSameBuilder()
    {
        using var rsa = RSA.Create(2048);
        var builder = Kafka.CreateAdminClient();

        var result = builder.WithOAuthBearerJwtBearer(CreateOptions(rsa));

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task KafkaClient_WithOAuthBearerJwtBearer_ReturnsSameBuilder()
    {
        using var rsa = RSA.Create(2048);
        var builder = Kafka.Connect();

        var result = builder.WithOAuthBearerJwtBearer(CreateOptions(rsa));

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task Producer_WithOAuthBearerJwtBearer_NullOptions_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        await Assert.That(() => builder.WithOAuthBearerJwtBearer((OAuthBearerJwtBearerOptions)null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Producer_WithOAuthBearerJwtBearer_NullConfigure_ThrowsArgumentNullException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        await Assert.That(() => builder.WithOAuthBearerJwtBearer((Action<OAuthBearerJwtBearerOptions>)null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Producer_WithOAuthBearerJwtBearer_MissingRequiredOptions_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        await Assert.That(() => builder.WithOAuthBearerJwtBearer(new OAuthBearerJwtBearerOptions()))
            .Throws<InvalidOperationException>();
    }

    private static OAuthBearerJwtBearerOptions CreateOptions(AsymmetricAlgorithm privateKey) => new()
    {
        TokenEndpoint = "https://auth.example.test/token",
        ClientId = "client",
        PrivateKey = privateKey,
        Audience = "kafka"
    };
}
