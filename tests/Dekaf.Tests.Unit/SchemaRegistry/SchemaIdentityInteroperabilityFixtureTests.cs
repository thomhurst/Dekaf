using System.Buffers;
using System.Text.Json;
using Avro.Generic;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaIdentityInteroperabilityFixtureTests
{
    private const string TopicSuffix = "-fixture";
    private const string FixtureResource =
        "Dekaf.Tests.Unit.SchemaRegistry.IdentityFixtures.confluent-schema-identity-v2.15.0.json";

    private static readonly JsonSerializerOptions FixtureJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IdentityFixture[] Fixtures = LoadFixtures();

    [Test]
    public async Task Avro_ConfluentPrefixHeaderAndDualFixtures_Deserialize()
    {
        var fixture = GetFixture("avro");
        using var registry = CreateRegistry(fixture);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(fixture.Schema);

        await AssertAvroFixtureAsync(fixture, registry, SchemaIdDeserializerStrategy.Prefix, useHeader: false);
        await AssertAvroFixtureAsync(fixture, registry, SchemaIdDeserializerStrategy.Header, useHeader: true);
        await AssertAvroFixtureAsync(fixture, registry, SchemaIdDeserializerStrategy.Dual, useHeader: false);
        await AssertAvroFixtureAsync(fixture, registry, SchemaIdDeserializerStrategy.Dual, useHeader: true);

        var record = new GenericRecord(schema);
        record.Add("id", 42);
        record.Add("name", "confluent-avro");
        await AssertAvroEmissionAsync(fixture, registry, record, SchemaIdSerializerStrategy.Prefix);
        await AssertAvroEmissionAsync(fixture, registry, record, SchemaIdSerializerStrategy.Header);
    }

    [Test]
    public async Task Json_ConfluentPrefixHeaderAndDualFixtures_Deserialize()
    {
        var fixture = GetFixture("json");
        using var registry = CreateRegistry(fixture);

        await AssertJsonFixtureAsync(fixture, registry, SchemaIdDeserializerStrategy.Prefix, useHeader: false);
        await AssertJsonFixtureAsync(fixture, registry, SchemaIdDeserializerStrategy.Header, useHeader: true);
        await AssertJsonFixtureAsync(fixture, registry, SchemaIdDeserializerStrategy.Dual, useHeader: false);
        await AssertJsonFixtureAsync(fixture, registry, SchemaIdDeserializerStrategy.Dual, useHeader: true);

        var value = new IdentityFixtureJsonRecord { Id = 43, Name = "confluent-json" };
        await AssertJsonEmissionAsync(fixture, registry, value, SchemaIdSerializerStrategy.Prefix);
        await AssertJsonEmissionAsync(fixture, registry, value, SchemaIdSerializerStrategy.Header);
    }

    [Test]
    public async Task Protobuf_ConfluentPrefixHeaderAndDualFixtures_Deserialize()
    {
        var fixture = GetFixture("protobuf");
        using var registry = CreateRegistry(fixture);

        await AssertProtobufFixtureAsync(fixture, registry, SchemaIdDeserializerStrategy.Prefix, useHeader: false);
        await AssertProtobufFixtureAsync(fixture, registry, SchemaIdDeserializerStrategy.Header, useHeader: true);
        await AssertProtobufFixtureAsync(fixture, registry, SchemaIdDeserializerStrategy.Dual, useHeader: false);
        await AssertProtobufFixtureAsync(fixture, registry, SchemaIdDeserializerStrategy.Dual, useHeader: true);

        var value = new TestMessage
        {
            Id = 44,
            Name = "confluent-protobuf",
            Value = 12.5
        };
        await AssertProtobufEmissionAsync(fixture, registry, value, SchemaIdSerializerStrategy.Prefix);
        await AssertProtobufEmissionAsync(fixture, registry, value, SchemaIdSerializerStrategy.Header);
    }

    [Test]
    public async Task MockRegistry_DeleteSharedSubject_PreservesIdentityUntilLastAlias()
    {
        const int schemaId = 41;
        var schemaGuid = Guid.Parse("bc703644-6486-4f78-98d3-c531c5c5a147");
        var schema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = "{}"
        };
        using var registry = new MockSchemaRegistryClient();
        registry.AddRegisteredSchema(schemaId, schemaGuid, "first-value", schema);
        registry.AddSchemaSubject(schemaId, "second-value");

        _ = await registry.DeleteSubjectAsync("first-value");

        await Assert.That(await registry.GetSchemaAsync(schemaId)).IsSameReferenceAs(schema);
        await Assert.That(await registry.GetSchemaByGuidAsync(schemaGuid.ToString())).IsSameReferenceAs(schema);
        await Assert.That((await registry.GetSchemaBySubjectAsync("second-value")).Guid)
            .IsEqualTo(schemaGuid.ToString());
        await Assert.That((await registry.LookupSchemaAsync("second-value", schema)).Guid)
            .IsEqualTo(schemaGuid.ToString());

        var deletedVersions = await registry.DeleteSubjectAsync("second-value");

        await Assert.That(deletedVersions).IsEquivalentTo([1]);
    }

    [Test]
    public async Task DualFixture_MalformedHeader_DoesNotFallBackToValidPrefix()
    {
        var fixture = GetFixture("json");
        using var registry = CreateRegistry(fixture);
        await using var deserializer = new JsonSchemaRegistryDeserializer<IdentityFixtureJsonRecord>(
            registry,
            PayloadJsonOptions,
            new SchemaRegistryDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Dual
            });
        var context = CreateContext(fixture, useHeader: false);
        context.Headers!.Add(new Header(fixture.HeaderName, new byte[] { 1, 2, 3 }));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(
            deserializer.Deserialize(Convert.FromHexString(fixture.PrefixPayloadHex), context)));

        await Assert.That(exception!.Message).Contains("GUID frame is truncated");
    }

    [Test]
    public async Task DualFixture_ConflictingHeaderIdentity_RejectsWrongSchemaFormat()
    {
        var avro = GetFixture("avro");
        var json = GetFixture("json");
        using var registry = CreateRegistry(avro, json);
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(
            registry,
            new AvroDeserializerConfig
            {
                SchemaIdStrategy = SchemaIdDeserializerStrategy.Dual
            });
        var context = CreateContext(json, useHeader: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromResult(
            deserializer.Deserialize(Convert.FromHexString(avro.PrefixPayloadHex), context)));

        await Assert.That(exception!.Message).Contains("not an Avro schema");
    }

    private static async Task AssertAvroFixtureAsync(
        IdentityFixture fixture,
        MockSchemaRegistryClient registry,
        SchemaIdDeserializerStrategy strategy,
        bool useHeader)
    {
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(
            registry,
            new AvroDeserializerConfig { SchemaIdStrategy = strategy });

        var record = deserializer.Deserialize(GetPayload(fixture, useHeader), CreateContext(fixture, useHeader));

        await Assert.That(record["id"]).IsEqualTo(42);
        await Assert.That(record["name"].ToString()).IsEqualTo("confluent-avro");
    }

    private static async Task AssertJsonFixtureAsync(
        IdentityFixture fixture,
        MockSchemaRegistryClient registry,
        SchemaIdDeserializerStrategy strategy,
        bool useHeader)
    {
        await using var deserializer = new JsonSchemaRegistryDeserializer<IdentityFixtureJsonRecord>(
            registry,
            PayloadJsonOptions,
            new SchemaRegistryDeserializerConfig { SchemaIdStrategy = strategy });

        var value = deserializer.Deserialize(GetPayload(fixture, useHeader), CreateContext(fixture, useHeader));

        await Assert.That(value.Id).IsEqualTo(43);
        await Assert.That(value.Name).IsEqualTo("confluent-json");
    }

    private static async Task AssertProtobufFixtureAsync(
        IdentityFixture fixture,
        MockSchemaRegistryClient registry,
        SchemaIdDeserializerStrategy strategy,
        bool useHeader)
    {
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<TestMessage>(
            registry,
            new ProtobufDeserializerConfig { SchemaIdStrategy = strategy });

        var value = deserializer.Deserialize(GetPayload(fixture, useHeader), CreateContext(fixture, useHeader));

        await Assert.That(value.Id).IsEqualTo(44);
        await Assert.That(value.Name).IsEqualTo("confluent-protobuf");
        await Assert.That(value.Value).IsEqualTo(12.5);
    }

    private static async Task AssertAvroEmissionAsync(
        IdentityFixture fixture,
        MockSchemaRegistryClient registry,
        GenericRecord value,
        SchemaIdSerializerStrategy strategy)
    {
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            registry,
            new AvroSerializerConfig
            {
                AutoRegisterSchemas = false,
                SchemaIdStrategy = strategy
            });
        await AssertEmissionAsync(fixture, strategy, (ref ArrayBufferWriter<byte> destination, SerializationContext context) =>
            serializer.Serialize(value, ref destination, context));
    }

    private static async Task AssertJsonEmissionAsync(
        IdentityFixture fixture,
        MockSchemaRegistryClient registry,
        IdentityFixtureJsonRecord value,
        SchemaIdSerializerStrategy strategy)
    {
        await using var serializer = new JsonSchemaRegistrySerializer<IdentityFixtureJsonRecord>(
            registry,
            fixture.Schema,
            PayloadJsonOptions,
            new JsonSchemaSerializerConfig
            {
                AutoRegisterSchemas = false,
                SchemaIdStrategy = strategy
            });
        await AssertEmissionAsync(fixture, strategy, (ref ArrayBufferWriter<byte> destination, SerializationContext context) =>
            serializer.Serialize(value, ref destination, context));
    }

    private static async Task AssertProtobufEmissionAsync(
        IdentityFixture fixture,
        MockSchemaRegistryClient registry,
        TestMessage value,
        SchemaIdSerializerStrategy strategy)
    {
        await using var serializer = new ProtobufSchemaRegistrySerializer<TestMessage>(
            registry,
            new ProtobufSerializerConfig
            {
                AutoRegisterSchemas = false,
                SchemaIdStrategy = strategy
            });
        await AssertEmissionAsync(fixture, strategy, (ref ArrayBufferWriter<byte> destination, SerializationContext context) =>
            serializer.Serialize(value, ref destination, context));
    }

    private static async Task AssertEmissionAsync(
        IdentityFixture fixture,
        SchemaIdSerializerStrategy strategy,
        SerializeFixture serialize)
    {
        var destination = new ArrayBufferWriter<byte>();
        var context = CreateContext(fixture, useHeader: false);

        serialize(ref destination, context);

        var expectedPayload = Convert.FromHexString(
            strategy == SchemaIdSerializerStrategy.Prefix
                ? fixture.PrefixPayloadHex
                : fixture.HeaderPayloadHex);
        await Assert.That(destination.WrittenSpan.SequenceEqual(expectedPayload)).IsTrue();
        if (strategy == SchemaIdSerializerStrategy.Prefix)
        {
            await Assert.That(context.Headers!.Count).IsEqualTo(0);
            return;
        }

        await Assert.That(context.Headers!.Count).IsEqualTo(1);
        await Assert.That(context.Headers[0].Key).IsEqualTo(fixture.HeaderName);
        await Assert.That(context.Headers[0].Value.Span.SequenceEqual(
            Convert.FromHexString(fixture.HeaderValueHex))).IsTrue();
    }

    private static MockSchemaRegistryClient CreateRegistry(params IdentityFixture[] fixtures)
    {
        var registry = new MockSchemaRegistryClient();
        for (var index = 0; index < fixtures.Length; index++)
        {
            var fixture = fixtures[index];
            registry.AddRegisteredSchema(
                fixture.SchemaId,
                Guid.Parse(fixture.SchemaGuid),
                GetSubject(fixture),
                new Schema
                {
                    SchemaType = GetSchemaType(fixture.Format),
                    SchemaString = fixture.Schema
                });
        }

        return registry;
    }

    private static SerializationContext CreateContext(IdentityFixture fixture, bool useHeader)
    {
        var headers = new Headers();
        if (useHeader)
        {
            headers.Add(new Header(
                fixture.HeaderName,
                Convert.FromHexString(fixture.HeaderValueHex)));
        }

        return new SerializationContext
        {
            Topic = GetTopic(fixture),
            Component = SerializationComponent.Value,
            Headers = headers
        };
    }

    private static ReadOnlyMemory<byte> GetPayload(IdentityFixture fixture, bool useHeader) =>
        Convert.FromHexString(useHeader ? fixture.HeaderPayloadHex : fixture.PrefixPayloadHex);

    private static string GetTopic(IdentityFixture fixture) => fixture.Format + TopicSuffix;

    private static string GetSubject(IdentityFixture fixture) => GetTopic(fixture) + "-value";

    private static SchemaType GetSchemaType(string format) => format switch
    {
        "avro" => SchemaType.Avro,
        "json" => SchemaType.Json,
        "protobuf" => SchemaType.Protobuf,
        _ => throw new InvalidDataException($"Unknown fixture schema format '{format}'.")
    };

    private static IdentityFixture GetFixture(string format) =>
        Fixtures.Single(fixture => fixture.Format == format);

    private static IdentityFixture[] LoadFixtures()
    {
        using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(FixtureResource)
                           ?? throw new InvalidOperationException($"Missing fixture resource '{FixtureResource}'.");
        return JsonSerializer.Deserialize<IdentityFixture[]>(stream, FixtureJsonOptions)
               ?? throw new InvalidDataException("The Confluent schema identity fixture is empty.");
    }

    private delegate void SerializeFixture(
        ref ArrayBufferWriter<byte> destination,
        SerializationContext context);

    private sealed class IdentityFixture
    {
        public required string Format { get; init; }
        public required string ProducerPackage { get; init; }
        public required string RegistryImage { get; init; }
        public required int SchemaId { get; init; }
        public required string SchemaGuid { get; init; }
        public required string Schema { get; init; }
        public required string PrefixPayloadHex { get; init; }
        public required string HeaderPayloadHex { get; init; }
        public required string HeaderName { get; init; }
        public required string HeaderValueHex { get; init; }
    }

    private sealed class IdentityFixtureJsonRecord
    {
        public int Id { get; init; }
        public required string Name { get; init; }
    }
}
