using System.Buffers;
using System.Text.Json;
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
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

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

        var act = () => serializer.Deserialize(new ReadOnlySequence<byte>(invalidJson), context);

        await Assert.That(act).Throws<JsonException>();
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
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

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
        var result = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory), context);

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

    #endregion
}
