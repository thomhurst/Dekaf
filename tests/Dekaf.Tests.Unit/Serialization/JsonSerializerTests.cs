using System.Buffers;
using System.Text.Encodings.Web;
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

    #region Scratch Buffer Retention Tests

    private const int MaxRetainedBufferBytes = 1024 * 1024;

    [Test]
    public async Task Serialize_SmallValue_KeepsScratchBufferForReuse()
    {
        var serializer = new JsonSerializer<TestPerson>();
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(new TestPerson { Name = "Alice", Age = 30 }, ref buffer, CreateContext());
        // The scratch buffer is thread-static, so read it before the first await.
        var retained = JsonSerializer<TestPerson>.RetainedBufferCapacity;

        await Assert.That(retained).IsGreaterThan(0);
        await Assert.That(retained).IsLessThanOrEqualTo(MaxRetainedBufferBytes);
    }

    [Test]
    public async Task Serialize_OversizedValue_ReleasesScratchBuffer()
    {
        var serializer = new JsonSerializer<TestPerson>();
        var context = CreateContext();
        var largeName = new string('x', 2 * MaxRetainedBufferBytes);
        var largeBuffer = new ArrayBufferWriter<byte>();
        var smallBuffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(new TestPerson { Name = largeName, Age = 1 }, ref largeBuffer, context);
        var retainedAfterLarge = JsonSerializer<TestPerson>.RetainedBufferCapacity;
        serializer.Serialize(new TestPerson { Name = "Al", Age = 7 }, ref smallBuffer, context);
        var retainedAfterSmall = JsonSerializer<TestPerson>.RetainedBufferCapacity;

        await Assert.That(retainedAfterLarge).IsEqualTo(0);
        await Assert.That(retainedAfterSmall).IsGreaterThan(0);
        await Assert.That(retainedAfterSmall).IsLessThanOrEqualTo(MaxRetainedBufferBytes);
        var large = serializer.Deserialize(largeBuffer.WrittenMemory, context);
        await Assert.That(large.Name).IsEqualTo(largeName);
        await Assert.That(System.Text.Encoding.UTF8.GetString(smallBuffer.WrittenSpan))
            .IsEqualTo("{\"name\":\"Al\",\"age\":7}");
    }

    [Test]
    public async Task Serialize_OversizedValueThatThrows_ReleasesScratchBuffer()
    {
        var serializer = new JsonSerializer<OversizedThrowingPayload>();
        var buffer = new ArrayBufferWriter<byte>();
        var payload = new OversizedThrowingPayload { Data = new string('x', 2 * MaxRetainedBufferBytes) };

        var thrown = false;
        try
        {
            serializer.Serialize(payload, ref buffer, CreateContext());
        }
        catch (InvalidOperationException)
        {
            thrown = true;
        }

        var retained = JsonSerializer<OversizedThrowingPayload>.RetainedBufferCapacity;

        await Assert.That(thrown).IsTrue();
        await Assert.That(retained).IsEqualTo(0);
        await Assert.That(buffer.WrittenCount).IsEqualTo(0);
    }

    [Test]
    public async Task Serialize_RepeatedOversizedValues_DoesNotAllocateAfterWarmup()
    {
        var serializer = new JsonSerializer<string>();
        var context = CreateContext();
        var payload = new string('x', 2 * MaxRetainedBufferBytes);
        var buffer = new ArrayBufferWriter<byte>(payload.Length + 2);

        for (var i = 0; i < 8; i++)
        {
            buffer.ResetWrittenCount();
            serializer.Serialize(payload, ref buffer, context);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 8; i++)
        {
            buffer.ResetWrittenCount();
            serializer.Serialize(payload, ref buffer, context);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var retained = JsonSerializer<string>.RetainedBufferCapacity;

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(retained).IsEqualTo(0);
        await Assert.That(serializer.Deserialize(buffer.WrittenMemory, context)).IsEqualTo(payload);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Serialize_OversizedValueWithThrowingDestination_ReleasesScratchBuffer(bool throwOnAdvance)
    {
        var serializer = new JsonSerializer<string>();
        var context = CreateContext();
        var payload = new string('x', 2 * MaxRetainedBufferBytes);
        var destination = new ThrowingBufferWriter(throwOnAdvance, payload.Length + 2);
        var thrown = false;
        try
        {
            serializer.Serialize(payload, ref destination, context);
        }
        catch (InvalidOperationException)
        {
            thrown = true;
        }

        var retained = JsonSerializer<string>.RetainedBufferCapacity;
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize("ok", ref buffer, context);

        await Assert.That(thrown).IsTrue();
        await Assert.That(retained).IsEqualTo(0);
        await Assert.That(serializer.Deserialize(buffer.WrittenMemory, context)).IsEqualTo("ok");
    }

    #endregion

    #region Custom Options Tests

    [Test]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1869:Cache and reuse 'JsonSerializerOptions' instances", Justification = "The test verifies changes to options between construction and first serialization.")]
    public async Task Serialize_OptionsChangedBeforeFirstUse_UsesCurrentSettings()
    {
        var options = new JsonSerializerOptions();
        var serializer = new JsonSerializer<TestPerson>(options);
        options.WriteIndented = true;
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        var person = new TestPerson { Name = "<Alice>", Age = 30 };
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(person, ref buffer, CreateContext());

        await Assert.That(System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan))
            .IsEqualTo(System.Text.Json.JsonSerializer.Serialize(person, options));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Serialize_AlternatingWriterOptions_MatchesSystemTextJson(bool useTypeInfo)
    {
        var person = new SourceGeneratedPerson { Name = "<Ava> é", Age = 42 };
        var context = CreateContext();
        var options = new[]
        {
            new JsonSerializerOptions(),
            new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping },
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping },
            new JsonSerializerOptions { WriteIndented = true }
        };
#if NET10_0_OR_GREATER
        options[1].IndentCharacter = '\t';
        options[1].IndentSize = 1;
        options[1].NewLine = "\n";
#endif
        var serializers = new JsonSerializer<SourceGeneratedPerson>[options.Length];
        var expected = new string[options.Length];
        for (var i = 0; i < options.Length; i++)
        {
            var typeInfo = new JsonSerializerTestsJsonContext(options[i]).SourceGeneratedPerson;
            serializers[i] = useTypeInfo
                ? new JsonSerializer<SourceGeneratedPerson>(typeInfo)
                : new JsonSerializer<SourceGeneratedPerson>(options[i]);
            expected[i] = useTypeInfo
                ? System.Text.Json.JsonSerializer.Serialize(person, typeInfo)
                : System.Text.Json.JsonSerializer.Serialize(person, options[i]);
        }

        // No awaits: all configurations must exercise the same thread-static cache.
        var actual = new string[12];
        var buffer = new ArrayBufferWriter<byte>();
        for (var i = 0; i < actual.Length; i++)
        {
            buffer.ResetWrittenCount();
            serializers[i % serializers.Length].Serialize(person, ref buffer, context);
            actual[i] = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        for (var i = 0; i < actual.Length; i++)
            await Assert.That(actual[i]).IsEqualTo(expected[i % expected.Length]);
    }

    [Test]
    public async Task Serialize_AlternatingWriterOptions_DoesNotAllocateAfterWarmup()
    {
        var serializers = new[]
        {
            new JsonSerializer<SourceGeneratedPerson>(JsonSerializerTestsJsonContext.Default.SourceGeneratedPerson),
            new JsonSerializer<SourceGeneratedPerson>(new JsonSerializerTestsJsonContext(
                new JsonSerializerOptions { WriteIndented = true }).SourceGeneratedPerson)
        };
        var person = new SourceGeneratedPerson { Name = "Ava", Age = 42 };
        var buffer = new ArrayBufferWriter<byte>(256);
        var context = CreateContext();
        for (var i = 0; i < 16; i++)
        {
            buffer.ResetWrittenCount();
            serializers[i % 2].Serialize(person, ref buffer, context);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 16; i++)
        {
            buffer.ResetWrittenCount();
            serializers[i % 2].Serialize(person, ref buffer, context);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(allocated).IsEqualTo(0);
    }

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

    [Test]
    public async Task Deserialize_Tombstone_ReturnsNull()
    {
        var serializer = new JsonSerializer<TestPerson>();
        var context = CreateContext() with { IsNull = true };

        var result = serializer.Deserialize(ReadOnlyMemory<byte>.Empty, context);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_Tombstone_WithTypeInfo_ReturnsNull()
    {
        var serializer = new JsonSerializer<SourceGeneratedPerson>(
            JsonSerializerTestsJsonContext.Default.SourceGeneratedPerson);
        var context = CreateContext() with { IsNull = true };

        var result = serializer.Deserialize(ReadOnlyMemory<byte>.Empty, context);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_Tombstone_ValueType_ReturnsDefault()
    {
        var serializer = new JsonSerializer<int>();
        var context = CreateContext() with { IsNull = true };

        var result = serializer.Deserialize(ReadOnlyMemory<byte>.Empty, context);

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Deserialize_EmptyNonNullData_Throws()
    {
        var serializer = new JsonSerializer<TestPerson>();
        var context = CreateContext();

        var act = () => serializer.Deserialize(ReadOnlyMemory<byte>.Empty, context);

        await Assert.That(act).Throws<JsonException>();
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

    private sealed class OversizedThrowingPayload
    {
        public string Data { get; init; } = string.Empty;

        // Serialized after Data, so the scratch buffer has already grown when this throws.
        public string Boom => throw new InvalidOperationException($"boom after {Data.Length} chars");
    }

    private sealed class TestOrder
    {
        public string? OrderId { get; set; }
        public List<string> Items { get; set; } = [];
    }

    private sealed record ThrowingPayload(string Value);

    private sealed class ThrowingBufferWriter(bool throwOnAdvance, int capacity) : IBufferWriter<byte>
    {
        private readonly byte[] _buffer = new byte[capacity];

        public void Advance(int count) => throw new InvalidOperationException("destination failed");

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            if (!throwOnAdvance)
                throw new InvalidOperationException("destination failed");

            return _buffer;
        }

        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    }

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
