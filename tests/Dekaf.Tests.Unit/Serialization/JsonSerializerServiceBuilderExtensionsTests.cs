using System.Text.Json;
using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Producer;
using Dekaf.Serialization.Json;

namespace Dekaf.Tests.Unit.Serialization;

public class JsonSerializerServiceBuilderExtensionsTests
{
    #region Producer UseJsonValueSerializer Tests

    [Test]
    public async Task UseJsonValueSerializer_ReturnsSelf()
    {
        var builder = new ProducerServiceBuilder<string, TestEvent>();
        var result = builder.UseJsonValueSerializer();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task UseJsonValueSerializer_WithOptions_ReturnsSelf()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
        var builder = new ProducerServiceBuilder<string, TestEvent>();
        var result = builder.UseJsonValueSerializer(options);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion

    #region Producer UseJsonKeySerializer Tests

    [Test]
    public async Task UseJsonKeySerializer_ReturnsSelf()
    {
        var builder = new ProducerServiceBuilder<TestEvent, string>();
        var result = builder.UseJsonKeySerializer();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task UseJsonKeySerializer_WithOptions_ReturnsSelf()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
        var builder = new ProducerServiceBuilder<TestEvent, string>();
        var result = builder.UseJsonKeySerializer(options);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion

    #region Consumer UseJsonValueDeserializer Tests

    [Test]
    public async Task UseJsonValueDeserializer_ReturnsSelf()
    {
        var builder = new ConsumerServiceBuilder<string, TestEvent>();
        var result = builder.UseJsonValueDeserializer();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task UseJsonValueDeserializer_WithOptions_ReturnsSelf()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
        var builder = new ConsumerServiceBuilder<string, TestEvent>();
        var result = builder.UseJsonValueDeserializer(options);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion

    #region Consumer UseJsonKeyDeserializer Tests

    [Test]
    public async Task UseJsonKeyDeserializer_ReturnsSelf()
    {
        var builder = new ConsumerServiceBuilder<TestEvent, string>();
        var result = builder.UseJsonKeyDeserializer();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task UseJsonKeyDeserializer_WithOptions_ReturnsSelf()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
        var builder = new ConsumerServiceBuilder<TestEvent, string>();
        var result = builder.UseJsonKeyDeserializer(options);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    #endregion

    #region DI Integration Tests

    [Test]
    public async Task UseJsonValueSerializer_WorksInAddDekafBuilder()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddProducer<string, TestEvent>(p => p
                .WithBootstrapServers("localhost:9092")
                .UseJsonValueSerializer());
        });

        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IKafkaProducer<string, TestEvent>));
        await Assert.That(descriptor).IsNotNull();
    }

    [Test]
    public async Task UseJsonValueDeserializer_WorksInAddDekafBuilder()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<string, TestEvent>(c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("test-group")
                .UseJsonValueDeserializer());
        });

        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IKafkaConsumer<string, TestEvent>));
        await Assert.That(descriptor).IsNotNull();
    }

    [Test]
    public async Task UseJsonKeyAndValueSerializer_CanBeChained()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddProducer<TestEvent, TestEvent>(p => p
                .WithBootstrapServers("localhost:9092")
                .UseJsonKeySerializer()
                .UseJsonValueSerializer());
        });

        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IKafkaProducer<TestEvent, TestEvent>));
        await Assert.That(descriptor).IsNotNull();
    }

    [Test]
    public async Task UseJsonKeyAndValueDeserializer_CanBeChained()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        services.AddDekaf(builder =>
        {
            builder.AddConsumer<TestEvent, TestEvent>(c => c
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("test-group")
                .UseJsonKeyDeserializer()
                .UseJsonValueDeserializer());
        });

        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IKafkaConsumer<TestEvent, TestEvent>));
        await Assert.That(descriptor).IsNotNull();
    }

    #endregion

    #region Test Models

    public sealed class TestEvent
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    #endregion
}
