using System.Reflection;
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
    public async Task WithOAuthBearerClientAssertion_ConfiguresEveryClientBuilder()
    {
        using var rsa = RSA.Create(2048);
        var options = CreateClientAssertionOptions(rsa);
        var producer = Kafka.CreateProducer<string, string>();
        var consumer = Kafka.CreateConsumer<string, string>();
        var shareConsumer = Kafka.CreateShareConsumer<string, string>();
        var admin = Kafka.CreateAdminClient();
        var client = Kafka.Connect();

        await Assert.That(producer.WithOAuthBearerClientAssertion(options)).IsSameReferenceAs(producer);
        await Assert.That(consumer.WithOAuthBearerClientAssertion(options)).IsSameReferenceAs(consumer);
        await Assert.That(shareConsumer.WithOAuthBearerClientAssertion(options)).IsSameReferenceAs(shareConsumer);
        await Assert.That(admin.WithOAuthBearerClientAssertion(options)).IsSameReferenceAs(admin);
        await Assert.That(client.WithOAuthBearerClientAssertion(options)).IsSameReferenceAs(client);

        foreach (var builder in new object[] { producer, consumer, shareConsumer, admin, client })
        {
            await Assert.That(GetSaslMechanism(builder)).IsEqualTo(SaslMechanism.OAuthBearer);
            await Assert.That(GetOAuthConfig(builder)!.ClientAssertion).IsNotNull();
        }
    }

    [Test]
    public async Task Producer_WithOAuthBearerClientAssertionAction_ReturnsSameBuilder()
    {
        using var rsa = RSA.Create(2048);
        var builder = Kafka.CreateProducer<string, string>();

        var result = builder.WithOAuthBearerClientAssertion(options =>
        {
            options.TokenEndpoint = "https://auth.example.test/token";
            options.ClientId = "client";
            options.PrivateKey = rsa;
            options.Audience = "https://auth.example.test/token";
        });

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task WithOAuthBearerClientAssertion_WhenValidationFails_DoesNotChangeExistingSaslMechanism()
    {
        var invalidOptions = new OAuthBearerClientAssertionOptions();

        await AssertInvalidOAuthOptionsDoNotChangeSaslMechanism(
            Kafka.CreateProducer<string, string>().WithSaslPlain("user", "pass"),
            builder => builder.WithOAuthBearerClientAssertion(invalidOptions));
        await AssertInvalidOAuthOptionsDoNotChangeSaslMechanism(
            Kafka.CreateConsumer<string, string>().WithSaslPlain("user", "pass"),
            builder => builder.WithOAuthBearerClientAssertion(invalidOptions));
        await AssertInvalidOAuthOptionsDoNotChangeSaslMechanism(
            Kafka.CreateShareConsumer<string, string>().WithSaslPlain("user", "pass"),
            builder => builder.WithOAuthBearerClientAssertion(invalidOptions));
        await AssertInvalidOAuthOptionsDoNotChangeSaslMechanism(
            Kafka.CreateAdminClient().WithSaslPlain("user", "pass"),
            builder => builder.WithOAuthBearerClientAssertion(invalidOptions));
        await AssertInvalidOAuthOptionsDoNotChangeSaslMechanism(
            Kafka.Connect().WithSaslPlain("user", "pass"),
            builder => builder.WithOAuthBearerClientAssertion(invalidOptions));
    }

    [Test]
    public async Task Producer_WithOAuthBearerClientAssertion_ReservedFormParameterThrows()
    {
        using var rsa = RSA.Create(2048);
        var options = CreateClientAssertionOptions(rsa);
        options.AdditionalParameters = new Dictionary<string, string>
        {
            ["client_assertion"] = "override"
        };

        await Assert.That(() => Kafka.CreateProducer<string, string>()
                .WithOAuthBearerClientAssertion(options))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("reserved");
    }

    [Test]
    public async Task Producer_WithOAuthBearerAzureImds_ReturnsSameBuilderAndConfiguresGrant()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var result = builder.WithOAuthBearerAzureImds(CreateAzureImdsOptions());

        await Assert.That(result).IsSameReferenceAs(builder);
        await Assert.That(GetSaslMechanism(builder)).IsEqualTo(SaslMechanism.OAuthBearer);
        await Assert.That(GetOAuthConfig(builder)!.GrantType).IsEqualTo(OAuthBearerGrantType.AzureImds);
    }

    [Test]
    public async Task Producer_WithOAuthBearerAzureImdsAction_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateProducer<string, string>();

        var result = builder.WithOAuthBearerAzureImds(options =>
        {
            options.Resource = "api://kafka";
            options.ClientId = "managed-identity-client";
        });

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task Consumer_WithOAuthBearerAzureImds_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var result = builder.WithOAuthBearerAzureImds(CreateAzureImdsOptions());

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ShareConsumer_WithOAuthBearerAzureImds_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateShareConsumer<string, string>();

        var result = builder.WithOAuthBearerAzureImds(CreateAzureImdsOptions());

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task AdminClient_WithOAuthBearerAzureImds_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateAdminClient();

        var result = builder.WithOAuthBearerAzureImds(CreateAzureImdsOptions());

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task KafkaClient_WithOAuthBearerAzureImds_ReturnsSameBuilder()
    {
        var builder = Kafka.Connect();

        var result = builder.WithOAuthBearerAzureImds(CreateAzureImdsOptions());

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task Producer_WithOAuthBearerAzureImds_MissingResource_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateProducer<string, string>();

        await Assert.That(() => builder.WithOAuthBearerAzureImds(new OAuthBearerAzureImdsOptions { Resource = string.Empty }))
            .Throws<InvalidOperationException>();
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

    [Test]
    public async Task WithOAuthBearerJwtBearer_WhenValidationFails_DoesNotChangeExistingSaslMechanism()
    {
        var invalidOptions = new OAuthBearerJwtBearerOptions();

        await AssertInvalidOAuthOptionsDoNotChangeSaslMechanism(
            Kafka.CreateProducer<string, string>().WithSaslPlain("user", "pass"),
            builder => builder.WithOAuthBearerJwtBearer(invalidOptions));
        await AssertInvalidOAuthOptionsDoNotChangeSaslMechanism(
            Kafka.CreateConsumer<string, string>().WithSaslPlain("user", "pass"),
            builder => builder.WithOAuthBearerJwtBearer(invalidOptions));
        await AssertInvalidOAuthOptionsDoNotChangeSaslMechanism(
            Kafka.CreateShareConsumer<string, string>().WithSaslPlain("user", "pass"),
            builder => builder.WithOAuthBearerJwtBearer(invalidOptions));
        await AssertInvalidOAuthOptionsDoNotChangeSaslMechanism(
            Kafka.CreateAdminClient().WithSaslPlain("user", "pass"),
            builder => builder.WithOAuthBearerJwtBearer(invalidOptions));
        await AssertInvalidOAuthOptionsDoNotChangeSaslMechanism(
            Kafka.Connect().WithSaslPlain("user", "pass"),
            builder => builder.WithOAuthBearerJwtBearer(invalidOptions));
    }

    private static OAuthBearerJwtBearerOptions CreateOptions(AsymmetricAlgorithm privateKey) => new()
    {
        TokenEndpoint = "https://auth.example.test/token",
        ClientId = "client",
        PrivateKey = privateKey,
        Audience = "kafka"
    };

    private static OAuthBearerAzureImdsOptions CreateAzureImdsOptions() => new()
    {
        Resource = "api://kafka",
        ClientId = "managed-identity-client"
    };

    private static OAuthBearerClientAssertionOptions CreateClientAssertionOptions(
        AsymmetricAlgorithm privateKey) => new()
    {
        TokenEndpoint = "https://auth.example.test/token",
        ClientId = "client",
        PrivateKey = privateKey,
        Audience = "https://auth.example.test/token"
    };

    private static async Task AssertInvalidOAuthOptionsDoNotChangeSaslMechanism<TBuilder>(
        TBuilder builder,
        Action<TBuilder> configure)
    {
        await Assert.That(GetSaslMechanism(builder!)).IsEqualTo(SaslMechanism.Plain);
        await Assert.That(() => configure(builder)).Throws<InvalidOperationException>();
        await Assert.That(GetSaslMechanism(builder!)).IsEqualTo(SaslMechanism.Plain);
    }

    private static SaslMechanism GetSaslMechanism(object builder)
    {
        var field = builder.GetType()
            .GetField("_saslMechanism", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_saslMechanism field not found");

        return (SaslMechanism)field.GetValue(builder)!;
    }

    private static OAuthBearerConfig? GetOAuthConfig(object builder)
    {
        var field = builder.GetType()
            .GetField("_oauthConfig", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_oauthConfig field not found");

        return (OAuthBearerConfig?)field.GetValue(builder);
    }
}
