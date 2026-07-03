using System.Reflection;
using Dekaf.Producer;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Builder;

public sealed class AwsMskIamBuilderTests
{
    [Test]
    public async Task Producer_WithAwsMskIam_SetsMechanismAndConfig()
    {
        var config = CreateConfig();
        var builder = Kafka.CreateProducer<string, string>();

        var result = builder.WithAwsMskIam(config);

        await Assert.That(result).IsSameReferenceAs(builder);
        await Assert.That(GetSaslMechanism(builder)).IsEqualTo(SaslMechanism.AwsMskIam);
        await Assert.That(GetAwsMskIamConfig(builder)).IsSameReferenceAs(config);
    }

    [Test]
    public async Task Consumer_WithAwsMskIam_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateConsumer<string, string>();

        var result = builder.WithAwsMskIam(CreateConfig());

        await Assert.That(result).IsSameReferenceAs(builder);
        await Assert.That(GetSaslMechanism(builder)).IsEqualTo(SaslMechanism.AwsMskIam);
    }

    [Test]
    public async Task ShareConsumer_WithAwsMskIam_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateShareConsumer<string, string>();

        var result = builder.WithAwsMskIam(CreateConfig());

        await Assert.That(result).IsSameReferenceAs(builder);
        await Assert.That(GetSaslMechanism(builder)).IsEqualTo(SaslMechanism.AwsMskIam);
    }

    [Test]
    public async Task AdminClient_WithAwsMskIam_ReturnsSameBuilder()
    {
        var builder = Kafka.CreateAdminClient();

        var result = builder.WithAwsMskIam(CreateConfig());

        await Assert.That(result).IsSameReferenceAs(builder);
        await Assert.That(GetSaslMechanism(builder)).IsEqualTo(SaslMechanism.AwsMskIam);
    }

    [Test]
    public async Task KafkaClient_WithAwsMskIam_ReturnsSameBuilder()
    {
        var builder = Kafka.Connect();

        var result = builder.WithAwsMskIam(CreateConfig());

        await Assert.That(result).IsSameReferenceAs(builder);
        await Assert.That(GetSaslMechanism(builder)).IsEqualTo(SaslMechanism.AwsMskIam);
    }

    [Test]
    public async Task Producer_Build_WithAwsMskIam_PreservesConfig()
    {
        var config = CreateConfig();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithAwsMskIam(config)
            .Build();

        var options = GetField<ProducerOptions>(producer, "_options");

        await Assert.That(options.SaslMechanism).IsEqualTo(SaslMechanism.AwsMskIam);
        await Assert.That(options.AwsMskIamConfig).IsSameReferenceAs(config);
    }

    [Test]
    public async Task KafkaClient_CreatedBuilders_RejectAwsMskIamOverrides()
    {
        await using var client = Kafka.Connect("localhost:9092");

        await Assert.That(() => client.CreateProducer<string, string>().WithAwsMskIam())
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateConsumer<string, string>().WithAwsMskIam())
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateShareConsumer<string, string>().WithAwsMskIam())
            .Throws<InvalidOperationException>();
        await Assert.That(() => client.CreateAdminClient().WithAwsMskIam())
            .Throws<InvalidOperationException>();
    }

    private static AwsMskIamConfig CreateConfig() => new()
    {
        Region = "us-east-1",
        CredentialsProvider = new StaticAwsCredentialsProvider(new AwsCredentials("access", "secret"))
    };

    private static SaslMechanism GetSaslMechanism(object builder)
        => GetField<SaslMechanism>(builder, "_saslMechanism");

    private static AwsMskIamConfig? GetAwsMskIamConfig(object builder)
        => GetField<AwsMskIamConfig?>(builder, "_awsMskIamConfig");

    private static T GetField<T>(object instance, string name)
    {
        var field = instance.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{name} field not found");

        return (T)field.GetValue(instance)!;
    }
}
