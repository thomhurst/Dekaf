using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Dekaf.Serialization;
using Dekaf.Serialization.Json;

namespace Dekaf.Tests.Unit.Serialization;

public class JsonSerializerTests
{
    private static SerializationContext CreateContext(
        string topic = "test",
        SerializationComponent component = SerializationComponent.Value) =>
        new() { Topic = topic, Component = component };

    #region Basic Roundtrip Tests

    [Test]
    public async Task Serialize_SimpleObject_RoundTrip()
    {
        var serializer = new JsonSerializer<TestPerson>();
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var person = new TestPerson { Name = "Alice", Age = 30 };

        serializer.Serialize(person, ref buffer, context);
        var result = serializer.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result.Name).IsEqualTo("Alice");
        await Assert.That(result.Age).IsEqualTo(30);
    }

    [Test]
    public async Task Serialize_DefaultOptions_UsesCamelCase()
    {
        var serializer = new JsonSerializer<TestPerson>();
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var person = new TestPerson { Name = "Bob", Age = 25 };

        serializer.Serialize(person, ref buffer, context);

        var json = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        await Assert.That(json).Contains("\"name\"");
        await Assert.That(json).Contains("\"age\"");
        // Should NOT contain PascalCase
        await Assert.That(json).DoesNotContain("\"Name\"");
        await Assert.That(json).DoesNotContain("\"Age\"");
    }

    [Test]
    public async Task Serialize_RepeatedCalls_ResetsWriterAndBuffer()
    {
        var serializer = new JsonSerializer<TestPerson>();
        var context = CreateContext();
        var firstBuffer = new ArrayBufferWriter<byte>();
        var secondBuffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(new TestPerson { Name = "Longer name", Age = 41 }, ref firstBuffer, context);
        serializer.Serialize(new TestPerson { Name = "Al", Age = 7 }, ref secondBuffer, context);

        var json = System.Text.Encoding.UTF8.GetString(secondBuffer.WrittenSpan);
        await Assert.That(json).IsEqualTo("{\"name\":\"Al\",\"age\":7}");
    }

    #endregion

    #region Custom Options Tests

    [Test]
    public async Task Serialize_CustomOptions_Respected()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // PascalCase
            WriteIndented = false
        };
        var serializer = new JsonSerializer<TestPerson>(options);
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var person = new TestPerson { Name = "Charlie", Age = 35 };

        serializer.Serialize(person, ref buffer, context);

        var json = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        await Assert.That(json).Contains("\"Name\"");
        await Assert.That(json).Contains("\"Age\"");
    }

    [Test]
    public async Task Serialize_SourceGeneratedTypeInfo_RoundTrips()
    {
        var serializer = new JsonSerializer<SourceGeneratedPerson>(
            JsonSerializerTestsJsonContext.Default.SourceGeneratedPerson);
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var person = new SourceGeneratedPerson { Name = "Ava", Age = 42 };

        serializer.Serialize(person, ref buffer, context);
        var result = serializer.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result.Name).IsEqualTo("Ava");
        await Assert.That(result.Age).IsEqualTo(42);
    }

    [Test]
    public async Task Serialize_SourceGeneratedKeyTypeInfo_RoundTrips()
    {
        var serializer = new JsonSerializer<SourceGeneratedOrderKey>(
            JsonSerializerTestsJsonContext.Default.SourceGeneratedOrderKey);
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext(component: SerializationComponent.Key);
        var key = new SourceGeneratedOrderKey { Id = "order-123" };

        serializer.Serialize(key, ref buffer, context);
        var result = serializer.Deserialize(buffer.WrittenMemory, context);

        var json = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        await Assert.That(json).IsEqualTo("{\"id\":\"order-123\"}");
        await Assert.That(result.Id).IsEqualTo("order-123");
    }

    [Test]
    public async Task Serialize_SourceGeneratedTypeInfo_UsesGeneratedNamingPolicy()
    {
        var serializer = new JsonSerializer<SourceGeneratedPerson>(
            JsonSerializerTestsJsonContext.Default.SourceGeneratedPerson);
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(new SourceGeneratedPerson { Name = "Ivy", Age = 11 }, ref buffer, context);

        var json = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        await Assert.That(json).IsEqualTo("{\"name\":\"Ivy\",\"age\":11}");
    }

    [Test]
    public async Task FluentHelpers_SourceGeneratedTypeInfo_ReturnBuilders()
    {
        var producerBuilder = new ProducerBuilder<SourceGeneratedOrderKey, SourceGeneratedPerson>();
        var consumerBuilder = new ConsumerBuilder<SourceGeneratedOrderKey, SourceGeneratedPerson>();

        var configuredProducerBuilder = producerBuilder
            .UseJsonKeySerializer(JsonSerializerTestsJsonContext.Default.SourceGeneratedOrderKey)
            .UseJsonSerializer(JsonSerializerTestsJsonContext.Default.SourceGeneratedPerson);
        var configuredConsumerBuilder = consumerBuilder
            .UseJsonKeyDeserializer(JsonSerializerTestsJsonContext.Default.SourceGeneratedOrderKey)
            .UseJsonDeserializer(JsonSerializerTestsJsonContext.Default.SourceGeneratedPerson);

        await Assert.That(configuredProducerBuilder).IsSameReferenceAs(producerBuilder);
        await Assert.That(configuredConsumerBuilder).IsSameReferenceAs(consumerBuilder);
    }

    [Test]
    public async Task Constructor_NullTypeInfo_ThrowsArgumentNullException()
    {
        var act = () => new JsonSerializer<SourceGeneratedPerson>((JsonTypeInfo<SourceGeneratedPerson>)null!);

        await Assert.That(act).Throws<ArgumentNullException>();
    }

    #endregion

    #region Deserialization Error Tests

    [Test]
    public async Task Deserialize_InvalidJson_Throws()
    {
        var serializer = new JsonSerializer<TestPerson>();
        var context = CreateContext();
        var invalidJson = "not valid json"u8.ToArray();

        var act = () => serializer.Deserialize((ReadOnlyMemory<byte>)invalidJson, context);

        await Assert.That(act).Throws<JsonException>();
    }

    [Test]
    public async Task Serialize_AfterSerializationException_CanSerializeAgain()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ThrowingPayloadConverter());
        var throwingSerializer = new JsonSerializer<ThrowingPayload>(options);
        var serializer = new JsonSerializer<ThrowingPayload>();
        var context = CreateContext();
        var failedBuffer = new ArrayBufferWriter<byte>();
        var buffer = new ArrayBufferWriter<byte>();

        var act = () => throwingSerializer.Serialize(new ThrowingPayload("boom"), ref failedBuffer, context);
        await Assert.That(act).Throws<InvalidOperationException>();

        serializer.Serialize(new ThrowingPayload("ok"), ref buffer, context);
        var json = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        await Assert.That(json).IsEqualTo("{\"value\":\"ok\"}");
    }

    #endregion

    #region Null Handling Tests

    [Test]
    public async Task Serialize_NullProperty_RoundTrips()
    {
        var serializer = new JsonSerializer<TestPerson>();
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var person = new TestPerson { Name = null!, Age = 0 };

        serializer.Serialize(person, ref buffer, context);
        var result = serializer.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result.Name).IsNull();
    }

    #endregion

    #region Collection Tests

    [Test]
    public async Task Serialize_WithCollection_RoundTrips()
    {
        var serializer = new JsonSerializer<TestOrder>();
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();
        var order = new TestOrder
        {
            OrderId = "ORD-001",
            Items = ["Item1", "Item2", "Item3"]
        };

        serializer.Serialize(order, ref buffer, context);
        var result = serializer.Deserialize(buffer.WrittenMemory, context);

        await Assert.That(result.OrderId).IsEqualTo("ORD-001");
        await Assert.That(result.Items.Count).IsEqualTo(3);
        await Assert.That(result.Items[0]).IsEqualTo("Item1");
        await Assert.That(result.Items[1]).IsEqualTo("Item2");
        await Assert.That(result.Items[2]).IsEqualTo("Item3");
    }

    #endregion

    #region ISerde Tests

    [Test]
    public async Task JsonSerializer_ImplementsISerde()
    {
        var serializer = new JsonSerializer<TestPerson>();
        await Assert.That(serializer).IsAssignableTo<ISerde<TestPerson>>();
    }

    [Test]
    public async Task JsonSerializer_ImplementsISerializer()
    {
        var serializer = new JsonSerializer<TestPerson>();
        await Assert.That(serializer).IsAssignableTo<ISerializer<TestPerson>>();
    }

    [Test]
    public async Task JsonSerializer_ImplementsIDeserializer()
    {
        var serializer = new JsonSerializer<TestPerson>();
        await Assert.That(serializer).IsAssignableTo<IDeserializer<TestPerson>>();
    }

    #endregion

    #region Test Models

    private sealed class TestPerson
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    private sealed class TestOrder
    {
        public string? OrderId { get; set; }
        public List<string> Items { get; set; } = [];
    }

    private sealed record ThrowingPayload(string Value);

    private sealed class ThrowingPayloadConverter : JsonConverter<ThrowingPayload>
    {
        public override ThrowingPayload Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, ThrowingPayload value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("value");
            throw new InvalidOperationException("stop");
        }
    }

    #endregion
}

internal sealed class SourceGeneratedPerson
{
    public string? Name { get; set; }
    public int Age { get; set; }
}

internal sealed class SourceGeneratedOrderKey
{
    public string? Id { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SourceGeneratedPerson))]
[JsonSerializable(typeof(SourceGeneratedOrderKey))]
internal sealed partial class JsonSerializerTestsJsonContext : JsonSerializerContext;
