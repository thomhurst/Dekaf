using Dekaf.Admin;
using Dekaf.Compression.Brotli;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Producer;
using Dekaf.Protocol.Records;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Security;
using Dekaf.Serialization.Json;
using Dekaf.Tests.Unit.SchemaRegistry;
using NSubstitute;

namespace Dekaf.Tests.Unit.Builder;

public sealed class ObsoleteBuilderMethodsTests
{
    [Test]
    public async Task RootUseTls_DelegatesToWithTls()
    {
        var config = new TlsConfig { TargetHost = "broker.example" };

#pragma warning disable CS0618 // Verifies the compatibility forwarder.
        var obsoleteBuilder = new KafkaClientBuilder().UseTls(config);
#pragma warning restore CS0618
        var modernBuilder = new KafkaClientBuilder().WithTls(config);

        await Assert.That(GetPrivateField<bool>(obsoleteBuilder, "_useTls")).IsTrue();
        await Assert.That(GetPrivateField<bool>(obsoleteBuilder, "_useTls"))
            .IsEqualTo(GetPrivateField<bool>(modernBuilder, "_useTls"));
        await Assert.That(GetPrivateField<TlsConfig>(obsoleteBuilder, "_tlsConfig")).IsSameReferenceAs(config);
        await Assert.That(GetPrivateField<TlsConfig>(obsoleteBuilder, "_tlsConfig"))
            .IsSameReferenceAs(GetPrivateField<TlsConfig>(modernBuilder, "_tlsConfig"));
    }

    [Test]
    public async Task ProducerUseTls_DelegatesToWithTls()
    {
#pragma warning disable CS0618 // Verifies the compatibility forwarder.
        await using var obsoleteProducer = Kafka.CreateProducer<string, string>()
            .UseTls()
#pragma warning restore CS0618
            .WithBootstrapServers("localhost:9092")
            .Build();

        await using var modernProducer = Kafka.CreateProducer<string, string>()
            .WithTls()
            .WithBootstrapServers("localhost:9092")
            .Build();

        var obsoleteOptions = GetPrivateField<ProducerOptions>(obsoleteProducer, "_options");
        var modernOptions = GetPrivateField<ProducerOptions>(modernProducer, "_options");

        await Assert.That(obsoleteOptions.UseTls).IsTrue();
        await Assert.That(obsoleteOptions.UseTls).IsEqualTo(modernOptions.UseTls);
    }

    [Test]
    public async Task ConsumerUseTlsConfig_DelegatesToWithTls()
    {
        var config = new TlsConfig { TargetHost = "consumer.example" };

#pragma warning disable CS0618 // Verifies the compatibility forwarder.
        await using var obsoleteConsumer = Kafka.CreateConsumer<string, string>()
            .UseTls(config)
#pragma warning restore CS0618
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("group-a")
            .Build();

        await using var modernConsumer = Kafka.CreateConsumer<string, string>()
            .WithTls(config)
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("group-a")
            .Build();

        var obsoleteOptions = GetPrivateField<ConsumerOptions>(obsoleteConsumer, "_options");
        var modernOptions = GetPrivateField<ConsumerOptions>(modernConsumer, "_options");

        await Assert.That(obsoleteOptions.UseTls).IsEqualTo(modernOptions.UseTls);
        await Assert.That(obsoleteOptions.TlsConfig).IsSameReferenceAs(modernOptions.TlsConfig);
    }

    [Test]
    public async Task AdminUseTlsConfig_DelegatesToWithTls()
    {
        var config = new TlsConfig { TargetHost = "admin.example" };

#pragma warning disable CS0618 // Verifies the compatibility forwarder.
        await using var obsoleteAdmin = new AdminClientBuilder()
            .UseTls(config)
#pragma warning restore CS0618
            .WithBootstrapServers("localhost:9092")
            .Build();

        await using var modernAdmin = new AdminClientBuilder()
            .WithTls(config)
            .WithBootstrapServers("localhost:9092")
            .Build();

        var obsoleteOptions = GetPrivateField<AdminClientOptions>(obsoleteAdmin, "_options");
        var modernOptions = GetPrivateField<AdminClientOptions>(modernAdmin, "_options");

        await Assert.That(obsoleteOptions.UseTls).IsEqualTo(modernOptions.UseTls);
        await Assert.That(obsoleteOptions.TlsConfig).IsSameReferenceAs(modernOptions.TlsConfig);
    }

    [Test]
    public async Task ProducerCompressionForwarders_DelegateToWithCompression()
    {
#pragma warning disable CS0618 // Verifies the compatibility forwarders.
        await AssertCompressionForwarder(
            builder => builder.UseCompression(CompressionType.Zstd),
            builder => builder.WithCompression(CompressionType.Zstd),
            CompressionType.Zstd);
        await AssertCompressionForwarder(
            builder => builder.UseGzipCompression(),
            builder => builder.WithGzipCompression(),
            CompressionType.Gzip);
        await AssertCompressionForwarder(
            builder => builder.UseLz4Compression(),
            builder => builder.WithLz4Compression(),
            CompressionType.Lz4);
        await AssertCompressionForwarder(
            builder => builder.UseSnappyCompression(),
            builder => builder.WithSnappyCompression(),
            CompressionType.Snappy);
        await AssertCompressionForwarder(
            builder => builder.UseZstdCompression(),
            builder => builder.WithZstdCompression(),
            CompressionType.Zstd);
        await AssertCompressionForwarder(
            builder => builder.UseBrotliCompression(),
            builder => builder.WithBrotliCompression(),
            CompressionType.Brotli);
#pragma warning restore CS0618
    }

    [Test]
    public async Task AdminUseMutualTls_DelegatesToWithMutualTls()
    {
#pragma warning disable CS0618 // Verifies the compatibility forwarder.
        await using var obsoleteAdmin = new AdminClientBuilder()
            .UseMutualTls("ca.pem", "client.pem", "client.key", "secret")
#pragma warning restore CS0618
            .WithBootstrapServers("localhost:9092")
            .Build();

        await using var modernAdmin = new AdminClientBuilder()
            .WithMutualTls("ca.pem", "client.pem", "client.key", "secret")
            .WithBootstrapServers("localhost:9092")
            .Build();

        var obsoleteOptions = GetPrivateField<AdminClientOptions>(obsoleteAdmin, "_options");
        var modernOptions = GetPrivateField<AdminClientOptions>(modernAdmin, "_options");

        await Assert.That(obsoleteOptions.UseTls).IsEqualTo(modernOptions.UseTls);
        await Assert.That(obsoleteOptions.TlsConfig!.CaCertificatePath)
            .IsEqualTo(modernOptions.TlsConfig!.CaCertificatePath);
        await Assert.That(obsoleteOptions.TlsConfig.ClientCertificatePath)
            .IsEqualTo(modernOptions.TlsConfig.ClientCertificatePath);
        await Assert.That(obsoleteOptions.TlsConfig.ClientKeyPath)
            .IsEqualTo(modernOptions.TlsConfig.ClientKeyPath);
        await Assert.That(obsoleteOptions.TlsConfig.ClientKeyPassword)
            .IsEqualTo(modernOptions.TlsConfig.ClientKeyPassword);
    }

    [Test]
    public async Task AdminClientServiceUseTls_ReturnsSameBuilder()
    {
        var builder = new AdminClientServiceBuilder();

#pragma warning disable CS0618 // Verifies the compatibility forwarder.
        var result = builder.UseTls();
#pragma warning restore CS0618

        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task JsonSerializationForwarders_DelegateToWithJsonSerialization()
    {
#pragma warning disable CS0618 // Verifies the compatibility forwarders.
        var obsoleteProducer = Kafka.CreateProducer<string, TestMessage>()
            .UseJsonKeySerializer()
            .UseJsonSerializer();
        var obsoleteConsumer = Kafka.CreateConsumer<string, TestMessage>()
            .UseJsonKeyDeserializer()
            .UseJsonDeserializer();
#pragma warning restore CS0618
        var modernProducer = Kafka.CreateProducer<string, TestMessage>()
            .WithJsonKeySerializer()
            .WithJsonSerializer();
        var modernConsumer = Kafka.CreateConsumer<string, TestMessage>()
            .WithJsonKeyDeserializer()
            .WithJsonDeserializer();

        await Assert.That(GetPrivateField<object>(obsoleteProducer, "_keySerializer"))
            .IsTypeOf<Dekaf.Serialization.Json.JsonSerializer<string>>();
        await Assert.That(GetPrivateField<object>(obsoleteProducer, "_valueSerializer"))
            .IsTypeOf<Dekaf.Serialization.Json.JsonSerializer<TestMessage>>();
        await Assert.That(GetPrivateField<object>(obsoleteConsumer, "_keyDeserializer"))
            .IsTypeOf<Dekaf.Serialization.Json.JsonSerializer<string>>();
        await Assert.That(GetPrivateField<object>(obsoleteConsumer, "_valueDeserializer"))
            .IsTypeOf<Dekaf.Serialization.Json.JsonSerializer<TestMessage>>();
        await Assert.That(GetPrivateField<object>(obsoleteProducer, "_keySerializer").GetType())
            .IsEqualTo(GetPrivateField<object>(modernProducer, "_keySerializer").GetType());
        await Assert.That(GetPrivateField<object>(obsoleteProducer, "_valueSerializer").GetType())
            .IsEqualTo(GetPrivateField<object>(modernProducer, "_valueSerializer").GetType());
        await Assert.That(GetPrivateField<object>(obsoleteConsumer, "_keyDeserializer").GetType())
            .IsEqualTo(GetPrivateField<object>(modernConsumer, "_keyDeserializer").GetType());
        await Assert.That(GetPrivateField<object>(obsoleteConsumer, "_valueDeserializer").GetType())
            .IsEqualTo(GetPrivateField<object>(modernConsumer, "_valueDeserializer").GetType());
    }

    [Test]
    public async Task JsonSchemaRegistryForwarders_DelegateToWithJsonSchemaRegistry()
    {
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();

#pragma warning disable CS0618 // Verifies the compatibility forwarders.
        var obsoleteProducer = Kafka.CreateProducer<string, TestMessage>()
            .UseJsonSchemaRegistry(schemaRegistry, "{}");
        var obsoleteConsumer = Kafka.CreateConsumer<string, TestMessage>()
            .UseJsonSchemaRegistry(schemaRegistry);
#pragma warning restore CS0618
        var modernProducer = Kafka.CreateProducer<string, TestMessage>()
            .WithJsonSchemaRegistry(schemaRegistry, "{}");
        var modernConsumer = Kafka.CreateConsumer<string, TestMessage>()
            .WithJsonSchemaRegistry(schemaRegistry);

        await Assert.That(GetPrivateField<object>(obsoleteProducer, "_valueSerializer"))
            .IsTypeOf<JsonSchemaRegistrySerializer<TestMessage>>();
        await Assert.That(GetPrivateField<object>(obsoleteConsumer, "_valueDeserializer"))
            .IsTypeOf<JsonSchemaRegistryDeserializer<TestMessage>>();
        await Assert.That(GetPrivateField<object>(obsoleteProducer, "_valueSerializer").GetType())
            .IsEqualTo(GetPrivateField<object>(modernProducer, "_valueSerializer").GetType());
        await Assert.That(GetPrivateField<object>(obsoleteConsumer, "_valueDeserializer").GetType())
            .IsEqualTo(GetPrivateField<object>(modernConsumer, "_valueDeserializer").GetType());
    }

    [Test]
    public async Task ProtobufSchemaRegistryForwarders_DelegateToWithProtobufSchemaRegistry()
    {
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();

#pragma warning disable CS0618 // Verifies the compatibility forwarders.
        var obsoleteProducer = Kafka.CreateProducer<TestMessage, TestMessage>()
            .UseProtobufSchemaRegistryForKeyAndValue(schemaRegistry);
        var obsoleteConsumer = Kafka.CreateConsumer<TestMessage, TestMessage>()
            .UseProtobufSchemaRegistryForKeyAndValue(schemaRegistry);
#pragma warning restore CS0618
        var modernProducer = Kafka.CreateProducer<TestMessage, TestMessage>()
            .WithProtobufSchemaRegistryForKeyAndValue(schemaRegistry);
        var modernConsumer = Kafka.CreateConsumer<TestMessage, TestMessage>()
            .WithProtobufSchemaRegistryForKeyAndValue(schemaRegistry);

        await Assert.That(GetPrivateField<object>(obsoleteProducer, "_keySerializer"))
            .IsTypeOf<ProtobufSchemaRegistrySerializer<TestMessage>>();
        await Assert.That(GetPrivateField<object>(obsoleteProducer, "_valueSerializer"))
            .IsTypeOf<ProtobufSchemaRegistrySerializer<TestMessage>>();
        await Assert.That(GetPrivateField<object>(obsoleteConsumer, "_keyDeserializer"))
            .IsTypeOf<ProtobufSchemaRegistryDeserializer<TestMessage>>();
        await Assert.That(GetPrivateField<object>(obsoleteConsumer, "_valueDeserializer"))
            .IsTypeOf<ProtobufSchemaRegistryDeserializer<TestMessage>>();
        await Assert.That(GetPrivateField<object>(obsoleteProducer, "_keySerializer").GetType())
            .IsEqualTo(GetPrivateField<object>(modernProducer, "_keySerializer").GetType());
        await Assert.That(GetPrivateField<object>(obsoleteProducer, "_valueSerializer").GetType())
            .IsEqualTo(GetPrivateField<object>(modernProducer, "_valueSerializer").GetType());
        await Assert.That(GetPrivateField<object>(obsoleteConsumer, "_keyDeserializer").GetType())
            .IsEqualTo(GetPrivateField<object>(modernConsumer, "_keyDeserializer").GetType());
        await Assert.That(GetPrivateField<object>(obsoleteConsumer, "_valueDeserializer").GetType())
            .IsEqualTo(GetPrivateField<object>(modernConsumer, "_valueDeserializer").GetType());
    }

    private static async Task AssertCompressionForwarder(
        Action<ProducerBuilder<string, string>> obsoleteConfigure,
        Action<ProducerBuilder<string, string>> modernConfigure,
        CompressionType expected)
    {
        var obsoleteBuilder = Kafka.CreateProducer<string, string>();
        obsoleteConfigure(obsoleteBuilder);
        var modernBuilder = Kafka.CreateProducer<string, string>();
        modernConfigure(modernBuilder);

        await Assert.That(GetPrivateField<CompressionType>(obsoleteBuilder, "_compressionType"))
            .IsEqualTo(expected);
        await Assert.That(GetPrivateField<CompressionType>(obsoleteBuilder, "_compressionType"))
            .IsEqualTo(GetPrivateField<CompressionType>(modernBuilder, "_compressionType"));
    }

    private static TField GetPrivateField<TField>(object target, string fieldName)
    {
        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Could not find {fieldName} field.");

        return (TField)field.GetValue(target)!;
    }
}
