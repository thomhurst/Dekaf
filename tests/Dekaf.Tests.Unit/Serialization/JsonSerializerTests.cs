using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dekaf.Serialization;
using Dekaf.Serialization.Json;

namespace Dekaf.Tests.Unit.Serialization;

public class JsonSerializerTests
{
    private static SerializationContext CreateContext(string topic = "test") =>
        new() { Topic = topic, Component = SerializationComponent.Value };

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
