using Dekaf.SchemaRegistry;
using Dekaf.Serialization;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaIdentityFramingTests
{
    private static readonly Guid SchemaGuid = Guid.Parse("89791762-2336-4186-9674-299b90a802e2");

    private static readonly byte[] GuidVector =
    [
        0x01, 0x89, 0x79, 0x17, 0x62, 0x23, 0x36, 0x41, 0x86,
        0x96, 0x74, 0x29, 0x9b, 0x90, 0xa8, 0x02, 0xe2
    ];

    [Test]
    public async Task SerializerStrategy_DefaultMatchesConfluentPrefix()
    {
        var prefixValue = (int)SchemaIdSerializerStrategy.Prefix;
        var headerValue = (int)SchemaIdSerializerStrategy.Header;

        await Assert.That(prefixValue).IsEqualTo(0);
        await Assert.That(headerValue).IsEqualTo(1);
    }

    [Test]
    public async Task JsonSerializerConfig_SchemaIdentityDefaultsMatchConfluent()
    {
        var config = new JsonSchemaSerializerConfig();

        await Assert.That(config.UseSchemaId).IsNull();
        await Assert.That(config.SchemaIdStrategy).IsEqualTo(SchemaIdSerializerStrategy.Prefix);
        await Assert.That(config.AutoRegisterSchemas).IsTrue();
        await Assert.That(config.UseLatestVersion).IsFalse();
        await Assert.That(new SchemaRegistryDeserializerConfig().SchemaIdStrategy)
            .IsEqualTo(SchemaIdDeserializerStrategy.Dual);
    }

    [Test]
    public async Task JsonReflectionOverloads_PositionalNullSelectJsonOptions()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            registry,
            "{\"type\":\"string\"}",
            null);
        await using var configuredSerializer = new JsonSchemaRegistrySerializer<string>(
            registry,
            "{\"type\":\"string\"}",
            null,
            new JsonSchemaSerializerConfig());
        var producerBuilder = Kafka.CreateProducer<string, string>()
            .UseJsonSchemaRegistry(registry, "{\"type\":\"string\"}", null);
        var configuredProducerBuilder = Kafka.CreateProducer<string, string>()
            .UseJsonSchemaRegistry(
                registry,
                "{\"type\":\"string\"}",
                null,
                new JsonSchemaSerializerConfig());
        var consumerBuilder = Kafka.CreateConsumer<string, string>()
            .UseJsonSchemaRegistry(registry, null);
        var configuredConsumerBuilder = Kafka.CreateConsumer<string, string>()
            .UseJsonSchemaRegistry(registry, null, new SchemaRegistryDeserializerConfig());

        await Assert.That(serializer).IsNotNull();
        await Assert.That(configuredSerializer).IsNotNull();
        await Assert.That(producerBuilder).IsNotNull();
        await Assert.That(configuredProducerBuilder).IsNotNull();
        await Assert.That(consumerBuilder).IsNotNull();
        await Assert.That(configuredConsumerBuilder).IsNotNull();
    }

    [Test]
    public async Task JsonSerializer_UseSchemaId_TakesPrecedenceOverLatestAndRegistration()
    {
        const string schemaText = "{\"type\":\"string\"}";
        using var registry = new MockSchemaRegistryClient();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = schemaText };
        var explicitId = await registry.RegisterSchemaAsync("json-explicit-value", schema);
        _ = await registry.RegisterSchemaAsync("json-explicit-value", schema);
        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            registry,
            schemaText,
            jsonOptions: null,
            new JsonSchemaSerializerConfig
            {
                UseSchemaId = explicitId,
                UseLatestVersion = true,
                AutoRegisterSchemas = true
            });
        await serializer.PrepareAsync("json-explicit", "value");
        var destination = new ArrayBufferWriter<byte>();
        var context = new SerializationContext
        {
            Topic = "json-explicit",
            Component = SerializationComponent.Value
        };

        serializer.Serialize("value", ref destination, context);

        await Assert.That(BinaryPrimitives.ReadInt32BigEndian(destination.WrittenSpan[1..5])).IsEqualTo(explicitId);
    }

    [Test]
    public void JsonSerializer_NegativeUseSchemaId_Throws()
    {
        using var registry = new MockSchemaRegistryClient();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var serializer = new JsonSchemaRegistrySerializer<string>(
                registry,
                "{\"type\":\"string\"}",
                jsonOptions: null,
                new JsonSchemaSerializerConfig { UseSchemaId = -1 });
            GC.KeepAlive(serializer);
        });
    }

    [Test]
    public async Task JsonSerializer_UseSchemaIdWithAvroSchema_Throws()
    {
        const string topic = "json-wrong-format";
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            $"{topic}-value",
            new Schema { SchemaType = SchemaType.Avro, SchemaString = "\"string\"" });
        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            registry,
            "{\"type\":\"string\"}",
            jsonOptions: null,
            new JsonSchemaSerializerConfig { UseSchemaId = schemaId });

        await Assert.That(async () => await serializer.PrepareAsync(topic, "value"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task JsonSerializer_UseSchemaIdWithDifferentJsonSchema_Throws()
    {
        const string topic = "json-wrong-schema";
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            $"{topic}-value",
            new Schema { SchemaType = SchemaType.Json, SchemaString = "{\"type\":\"integer\"}" });
        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            registry,
            "{\"type\":\"string\"}",
            jsonOptions: null,
            new JsonSchemaSerializerConfig { UseSchemaId = schemaId });

        await Assert.That(async () => await serializer.PrepareAsync(topic, "value"))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("does not match");
    }

    [Test]
    public async Task JsonSerializer_LookupWithOnlyAvroSchema_ThrowsNotFound()
    {
        const string topic = "json-lookup-wrong-format";
        using var registry = new MockSchemaRegistryClient();
        _ = await registry.RegisterSchemaAsync(
            $"{topic}-value",
            new Schema { SchemaType = SchemaType.Avro, SchemaString = "\"string\"" });
        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            registry,
            "{\"type\":\"string\"}",
            jsonOptions: null,
            new JsonSchemaSerializerConfig { AutoRegisterSchemas = false });

        await Assert.That(async () => await serializer.PrepareAsync(topic, "value"))
            .Throws<SchemaRegistryException>()
            .WithMessageContaining("Schema not found");
    }

    [Test]
    public async Task JsonDeserializer_GuidSubjectsUseEachSchemaRecordName()
    {
        const string topic = "json-guid-record-name";
        const string firstSchemaText = "{\"type\":\"string\",\"title\":\"FirstRecord\"}";
        const string secondSchemaText = "{\"type\":\"string\",\"title\":\"SecondRecord\"}";
        using var registry = new MockSchemaRegistryClient();
        var firstId = await registry.RegisterSchemaAsync(
            "FirstRecord",
            new Schema { SchemaType = SchemaType.Json, SchemaString = firstSchemaText });
        var secondId = await registry.RegisterSchemaAsync(
            "SecondRecord",
            new Schema { SchemaType = SchemaType.Json, SchemaString = secondSchemaText });
        await using var deserializer = new JsonSchemaRegistryDeserializer<string>(
            registry,
            jsonOptions: null,
            new SchemaRegistryDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Header,
                SubjectNameStrategy = SubjectNameStrategy.RecordName
            });

        var first = deserializer.Deserialize(
            Encoding.UTF8.GetBytes("\"first\""),
            CreateGuidHeaderContext(topic, firstId));
        var second = deserializer.Deserialize(
            Encoding.UTF8.GetBytes("\"second\""),
            CreateGuidHeaderContext(topic, secondId));

        await Assert.That(first).IsEqualTo("first");
        await Assert.That(second).IsEqualTo("second");
        await Assert.That(registry.LastGetSchemaByGuidCancellationToken.CanBeCanceled).IsTrue();
    }

    [Test]
    public async Task BoundedIdentityCache_EvictsOldestExactEntry()
    {
        var cache = new ConcurrentDictionary<int, object>();
        var evictionQueue = new ConcurrentQueue<KeyValuePair<int, object>>();
        var cachedCount = 0;

        for (var key = 1; key <= 3; key++)
        {
            cache[key] = new object();
            BoundedSchemaIdentityCache.RecordSuccessfulResolution(
                cache,
                evictionQueue,
                key,
                ref cachedCount,
                maxCachedEntries: 2);
        }

        await Assert.That(cachedCount).IsEqualTo(2);
        await Assert.That(cache.ContainsKey(1)).IsFalse();
        await Assert.That(cache.ContainsKey(2)).IsTrue();
        await Assert.That(cache.ContainsKey(3)).IsTrue();
    }

    [Test]
    public async Task JsonSerializer_HeaderStrategy_WritesExactGuidHeaderAndUnprefixedPayload()
    {
        const string schemaText = "{\"type\":\"string\"}";
        using var registry = new MockSchemaRegistryClient();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = schemaText };
        var schemaId = await registry.RegisterSchemaAsync("json-header-value", schema);
        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            registry,
            schemaText,
            jsonOptions: null,
            new JsonSchemaSerializerConfig
            {
                AutoRegisterSchemas = false,
                SchemaIdStrategy = SchemaIdSerializerStrategy.Header
            });
        await serializer.PrepareAsync("json-header", "value");
        var destination = new ArrayBufferWriter<byte>();
        var headers = new Headers();
        var context = new SerializationContext
        {
            Topic = "json-header",
            Component = SerializationComponent.Value,
            Headers = headers
        };

        serializer.Serialize("value", ref destination, context);

        var expectedGuid = new Guid(schemaId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var expectedFrame = SchemaIdentityFraming.CreateSchemaGuidFrame(expectedGuid);
        await Assert.That(Encoding.UTF8.GetString(destination.WrittenSpan)).IsEqualTo("\"value\"");
        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers[0].Key).IsEqualTo(SchemaIdentityHeaderNames.Value);
        await Assert.That(headers[0].Value.ToArray()).IsEquivalentTo(expectedFrame);

        await using var deserializer = new JsonSchemaRegistryDeserializer<string>(
            registry,
            jsonOptions: null,
            new SchemaRegistryDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Header
            });
        var result = deserializer.Deserialize(destination.WrittenMemory, context);
        await Assert.That(result).IsEqualTo("value");
    }

    [Test]
    public async Task JsonSerializer_PreparationAdmission_PreservesGuidHeaderFrame()
    {
        const string schemaText = "{\"type\":\"string\"}";
        using var registry = new MockSchemaRegistryClient();
        var schema = new Schema { SchemaType = SchemaType.Json, SchemaString = schemaText };
        var schemaId = await registry.RegisterSchemaAsync("json-admission-value", schema);
        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            registry,
            schemaText,
            jsonOptions: null,
            new JsonSchemaSerializerConfig
            {
                AutoRegisterSchemas = false,
                SchemaIdStrategy = SchemaIdSerializerStrategy.Header
            });
        var headers = new Headers();
        var context = new SerializationContext
        {
            Topic = "json-admission",
            Component = SerializationComponent.Value,
            Headers = headers
        };
        var admissionSerializer = (IAsyncSerializerPreparationAdmission<string>)serializer;
        var admission = await admissionSerializer.PrepareForSerializationAsync("value", context);
        var destination = new ArrayBufferWriter<byte>();

        admissionSerializer.SerializePrepared("value", ref destination, context, in admission);

        var expectedGuid = new Guid(schemaId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var expectedFrame = SchemaIdentityFraming.CreateSchemaGuidFrame(expectedGuid);
        await Assert.That(Encoding.UTF8.GetString(destination.WrittenSpan)).IsEqualTo("\"value\"");
        await Assert.That(headers.Count).IsEqualTo(1);
        await Assert.That(headers[0].Key).IsEqualTo(SchemaIdentityHeaderNames.Value);
        await Assert.That(headers[0].Value.ToArray()).IsEquivalentTo(expectedFrame);
    }

    [Test]
    public async Task IdPrefix_MatchesConfluentWireVector()
    {
        var destination = new byte[SchemaIdentityFraming.SchemaIdFrameSize];

        var written = SchemaIdentityFraming.WriteSchemaId(destination, 1);
        var parsed = SchemaIdentityFraming.ReadPrefix(destination, out var payloadOffset);

        await Assert.That(written).IsEqualTo(5);
        await Assert.That(destination).IsEquivalentTo(new byte[] { 0, 0, 0, 0, 1 });
        await Assert.That(parsed).IsEqualTo(new SchemaIdentity(1));
        await Assert.That(payloadOffset).IsEqualTo(5);
    }

    [Test]
    public async Task GuidHeaderIdentity_MatchesConfluentNetworkOrderVector()
    {
        var destination = new byte[SchemaIdentityFraming.SchemaGuidFrameSize];

        var written = SchemaIdentityFraming.WriteSchemaGuid(destination, SchemaGuid);
        var identityHeader = new Header(SchemaIdentityHeaderNames.Value, destination);
        var parsed = SchemaIdentityFraming.ReadHeader(in identityHeader, out var trailingHeaderData);

        await Assert.That(written).IsEqualTo(17);
        await Assert.That(destination).IsEquivalentTo(GuidVector);
        await Assert.That(parsed).IsEqualTo(new SchemaIdentity(SchemaGuid));
        await Assert.That(trailingHeaderData.IsEmpty).IsTrue();
    }

    [Test]
    [Arguments(SerializationComponent.Key, SchemaIdentityHeaderNames.Key)]
    [Arguments(SerializationComponent.Value, SchemaIdentityHeaderNames.Value)]
    public async Task HeaderStrategy_UsesConfluentHeaderName(
        SerializationComponent component,
        string expectedName)
    {
        var encoded = SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid);

        var identityHeader = SchemaIdentityFraming.CreateSchemaGuidHeader(component, encoded);
        var parsed = SchemaIdentityFraming.ReadHeader(in identityHeader, out _);

        await Assert.That(identityHeader.Key).IsEqualTo(expectedName);
        await Assert.That(identityHeader.Value.ToArray()).IsEquivalentTo(GuidVector);
        await Assert.That(parsed).IsEqualTo(new SchemaIdentity(SchemaGuid));
    }

    [Test]
    public async Task HeaderStrategy_PreservesTrailingProtobufIndexes()
    {
        var headerValue = new byte[GuidVector.Length + 3];
        GuidVector.CopyTo(headerValue, 0);
        headerValue[^3] = 0x04;
        headerValue[^2] = 0x02;
        headerValue[^1] = 0x00;
        var identityHeader = new Header(SchemaIdentityHeaderNames.Value, headerValue);

        _ = SchemaIdentityFraming.ReadHeader(
            in identityHeader,
            out var trailingHeaderData);

        await Assert.That(trailingHeaderData.ToArray()).IsEquivalentTo(new byte[] { 4, 2, 0 });
    }

    [Test]
    public async Task DualStrategy_HeaderWinsAndPayloadRemainsUnchanged()
    {
        var identityHeader = new Header(
            SchemaIdentityHeaderNames.Value,
            SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid));
        byte[] payloadWithDifferentPrefix = [0, 0, 0, 0, 42, 7, 8];

        var parsed = SchemaIdentityFraming.Read(
            payloadWithDifferentPrefix,
            identityHeader,
            SchemaIdDeserializerStrategy.Dual,
            out var payloadOffset,
            out _);

        await Assert.That(parsed).IsEqualTo(new SchemaIdentity(SchemaGuid));
        await Assert.That(payloadOffset).IsEqualTo(0);
    }

    [Test]
    public async Task DualStrategy_MissingHeaderFallsBackToPrefix()
    {
        byte[] payload = [0, 0, 0, 0, 42, 7, 8];

        var parsed = SchemaIdentityFraming.Read(
            payload,
            identityHeader: null,
            SchemaIdDeserializerStrategy.Dual,
            out var payloadOffset,
            out _);

        await Assert.That(parsed).IsEqualTo(new SchemaIdentity(42));
        await Assert.That(payloadOffset).IsEqualTo(5);
    }

    [Test]
    public void DualStrategy_MalformedHeader_DoesNotFallBackToPrefix()
    {
        var identityHeader = new Header(SchemaIdentityHeaderNames.Value, new byte[] { 1, 2, 3 });
        byte[] validPrefix = [0, 0, 0, 0, 42, 7, 8];

        Assert.Throws<InvalidDataException>(() => SchemaIdentityFraming.Read(
            validPrefix,
            identityHeader,
            SchemaIdDeserializerStrategy.Dual,
            out _,
            out _));
    }

    [Test]
    [Arguments(SchemaIdDeserializerStrategy.Header)]
    [Arguments(SchemaIdDeserializerStrategy.Dual)]
    public void HeaderStrategies_IdFrame_ThrowsWithoutReadingPrefix(
        SchemaIdDeserializerStrategy strategy)
    {
        byte[] idFrame = [0, 0, 0, 0, 7];
        var identityHeader = new Header(SchemaIdentityHeaderNames.Value, idFrame);
        byte[] validPrefix = [0, 0, 0, 0, 42, 7, 8];

        Assert.Throws<InvalidDataException>(() => SchemaIdentityFraming.Read(
            validPrefix,
            identityHeader,
            strategy,
            out _,
            out _));
    }

    [Test]
    [Arguments(SchemaIdDeserializerStrategy.Prefix, 5)]
    [Arguments(SchemaIdDeserializerStrategy.Header, 0)]
    public async Task ExplicitStrategy_ReturnsExpectedPayloadOffset(
        SchemaIdDeserializerStrategy strategy,
        int expectedOffset)
    {
        var identityHeader = new Header(
            SchemaIdentityHeaderNames.Value,
            SchemaIdentityFraming.CreateSchemaGuidFrame(SchemaGuid));
        byte[] payload = [0, 0, 0, 0, 42, 7, 8];

        _ = SchemaIdentityFraming.Read(
            payload,
            identityHeader,
            strategy,
            out var payloadOffset,
            out _);

        await Assert.That(payloadOffset).IsEqualTo(expectedOffset);
    }

    [Test]
    public void ReadPrefix_UnknownMagicByte_ThrowsInvalidOperationException() =>
        Assert.Throws<InvalidOperationException>(() => SchemaIdentityFraming.ReadPrefix([2, 0, 0, 0, 1], out _));

    [Test]
    public void ReadPrefix_GuidFrame_ThrowsInvalidOperationException() =>
        Assert.Throws<InvalidOperationException>(() => SchemaIdentityFraming.ReadPrefix(GuidVector, out _));

    [Test]
    public void ReadPrefix_NegativeId_ThrowsInvalidDataException() =>
        Assert.Throws<InvalidDataException>(() =>
            SchemaIdentityFraming.ReadPrefix([0, 0xff, 0xff, 0xff, 0xff], out _));

    [Test]
    public void ReadPrefix_TruncatedIdentity_ThrowsInvalidOperationException() =>
        Assert.Throws<InvalidOperationException>(() => SchemaIdentityFraming.ReadPrefix([0, 0, 0, 1], out _));

    [Test]
    public void ReadHeader_MissingHeader_ThrowsInvalidDataException() =>
        Assert.Throws<InvalidDataException>(() =>
            SchemaIdentityFraming.Read(
                [0, 0, 0, 0, 42],
                identityHeader: null,
                SchemaIdDeserializerStrategy.Header,
                out _,
                out _));

    [Test]
    public void ReadHeader_NullValue_ThrowsInvalidDataException() =>
        Assert.Throws<InvalidDataException>(() =>
        {
            var identityHeader = new Header(SchemaIdentityHeaderNames.Value, (byte[]?)null);
            _ = SchemaIdentityFraming.ReadHeader(in identityHeader, out _);
        });

    [Test]
    public void ReadHeader_MalformedValue_ThrowsInvalidDataException() =>
        Assert.Throws<InvalidDataException>(() =>
        {
            var identityHeader = new Header(SchemaIdentityHeaderNames.Value, new byte[] { 1, 2, 3 });
            _ = SchemaIdentityFraming.ReadHeader(in identityHeader, out _);
        });

    [Test]
    public void WriteSchemaId_NegativeId_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SchemaIdentityFraming.WriteSchemaId(new byte[5], -1));

    [Test]
    public void SchemaIdentity_EmptyGuid_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => _ = new SchemaIdentity(Guid.Empty));

    [Test]
    [Arguments(null, false, true, 0)]
    [Arguments(null, false, false, 1)]
    [Arguments(null, true, false, 2)]
    [Arguments(null, true, true, 2)]
    [Arguments(42, false, true, 3)]
    [Arguments(42, true, false, 3)]
    [Arguments(42, true, true, 3)]
    public async Task ResolveSelection_UsesDocumentedPrecedence(
        int? useSchemaId,
        bool useLatestVersion,
        bool autoRegisterSchemas,
        int expected)
    {
        var actual = SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
            useSchemaId,
            useLatestVersion,
            autoRegisterSchemas);

        await Assert.That((int)actual).IsEqualTo(expected);
    }

    [Test]
    public void ResolveSelection_NegativeExplicitId_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SchemaRegistrySerializerConfigValidator.ValidateAndResolve(
                useSchemaId: -1,
                useLatestVersion: false,
                autoRegisterSchemas: false));

    private static SerializationContext CreateGuidHeaderContext(string topic, int schemaId)
    {
        var guid = new Guid(schemaId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        return new SerializationContext
        {
            Topic = topic,
            Component = SerializationComponent.Value,
            Headers = new Headers(1).Add(
                SchemaIdentityHeaderNames.Value,
                SchemaIdentityFraming.CreateSchemaGuidFrame(guid))
        };
    }
}
