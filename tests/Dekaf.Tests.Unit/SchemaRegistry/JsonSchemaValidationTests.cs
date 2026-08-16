using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Json;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class JsonSchemaValidationTests
{
    private const string Draft7Schema = """
        {
          "$schema": "http://json-schema.org/draft-07/schema#",
          "type": "object",
          "required": ["id", "profile", "tags"],
          "properties": {
            "id": { "type": "integer" },
            "profile": {
              "type": "object",
              "required": ["name"],
              "properties": { "name": { "type": "string" } }
            },
            "tags": { "type": "array", "items": { "type": "string" } },
            "nickname": { "type": ["string", "null"] }
          }
        }
        """;

    private static readonly SerializationContext Context = new()
    {
        Topic = "validation",
        Component = SerializationComponent.Value
    };

    [Test]
    [Arguments("""{"id":1,"profile":{"name":"Ada"},"tags":["fast"],"nickname":null}""")]
    [Arguments("""{"id":1,"profile":{"name":"Ada"},"tags":[],"nickname":"ace"}""")]
    public void Validator_AcceptsValidPayloads(string json)
    {
        var validator = CreateFactory().GetOrCreate(CreateSchema(Draft7Schema));

        validator.Validate(Encoding.UTF8.GetBytes(json), 41);
    }

    [Test]
    [Arguments("""{"profile":{"name":"Ada"},"tags":[]}""", "required", "$")]
    [Arguments("""{"id":"secret-value","profile":{"name":"Ada"},"tags":[]}""", "type", "$['id']")]
    [Arguments("""{"id":1,"profile":{"name":9},"tags":[]}""", "type", "$['profile']['name']")]
    [Arguments("""{"id":1,"profile":{"name":"Ada"},"tags":["ok",4]}""", "type", "$['tags'][1]")]
    public async Task Validator_ReportsSchemaIdKeywordAndPathWithoutPayload(
        string json,
        string expectedKeyword,
        string expectedPath)
    {
        var validator = CreateFactory().GetOrCreate(CreateSchema(Draft7Schema));

        var exception = Assert.Throws<JsonSchemaValidationException>(
            () => validator.Validate(Encoding.UTF8.GetBytes(json), 41));

        await Assert.That(exception.SchemaId).IsEqualTo(41);
        await Assert.That(exception.Keyword).IsEqualTo(expectedKeyword);
        await Assert.That(exception.JsonPath).IsEqualTo(expectedPath);
        await Assert.That(exception.Message).DoesNotContain("secret-value");
    }

    [Test]
    public async Task Validator_RejectsMalformedOrTrailingJsonWithoutPayloadDisclosure()
    {
        var validator = CreateFactory().GetOrCreate(CreateSchema(Draft7Schema));

        var exception = Assert.Throws<JsonSchemaValidationException>(
            () => validator.Validate("""{"id":1} trailing-secret"""u8, 52));

        await Assert.That(exception.Keyword).IsEqualTo("$parse");
        await Assert.That(exception.Message).DoesNotContain("trailing-secret");
    }

    [Test]
    public void Validator_SupportsInternalReferencesAndDraft202012()
    {
        const string schemaText = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$defs": { "identifier": { "type": "integer", "minimum": 1 } },
              "type": "object",
              "properties": { "id": { "$ref": "#/$defs/identifier" } },
              "required": ["id"]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.Validate("""{"id":1}"""u8, 9);
        Assert.Throws<JsonSchemaValidationException>(() => validator.Validate("""{"id":0}"""u8, 9));
    }

    [Test]
    public async Task Validator_ResolvesSchemaRegistryReferences()
    {
        using var registry = new MockSchemaRegistryClient();
        const string addressSchema = """
            {
              "$id": "https://example.test/address.json",
              "type": "object",
              "properties": { "postcode": { "type": "string" } },
              "required": ["postcode"]
            }
            """;
        await registry.RegisterSchemaAsync("address-value", CreateSchema(addressSchema));
        var root = CreateSchema(
            """
            {
              "type": "object",
              "properties": { "address": { "$ref": "https://example.test/address.json" } },
              "required": ["address"]
            }
            """,
            [new SchemaReference
            {
                Name = "https://example.test/address.json",
                Subject = "address-value",
                Version = 1
            }]);
        var validator = new StreamingJsonSchemaValidatorFactory(registry).GetOrCreate(root);

        validator.Validate("""{"address":{"postcode":"AB1"}}"""u8, 12);
        Assert.Throws<JsonSchemaValidationException>(
            () => validator.Validate("""{"address":{}}"""u8, 12));
    }

    [Test]
    public async Task Validator_ResolvesRelativeReferencesFromEffectiveSchemaId()
    {
        using var registry = new MockSchemaRegistryClient();
        await registry.RegisterSchemaAsync(
            "address-value",
            CreateSchema("""{"type":"object","required":["postcode"]}"""));
        var root = CreateSchema(
            """
            {
              "$id": "https://example.test/schemas/root.json",
              "type": "object",
              "properties": { "address": { "$ref": "defs/address.json" } },
              "required": ["address"]
            }
            """,
            [new SchemaReference
            {
                Name = "defs/address.json",
                Subject = "address-value",
                Version = 1
            }]);
        var validator = new StreamingJsonSchemaValidatorFactory(registry).GetOrCreate(root);

        validator.Validate("""{"address":{"postcode":"AB1"}}"""u8, 13);
        Assert.Throws<JsonSchemaValidationException>(
            () => validator.Validate("""{"address":{}}"""u8, 13));
    }

    [Test]
    public void Factory_ReferenceResolutionTimeoutIsFailClosed()
    {
        var registry = Substitute.For<ISchemaRegistryClient>();
        registry.GetSchemaBySubjectAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<RegisteredSchema>().Task);
        var factory = new StreamingJsonSchemaValidatorFactory(
            registry,
            new StreamingJsonSchemaValidatorOptions
            {
                ReferenceResolutionTimeout = TimeSpan.FromMilliseconds(10)
            });
        var schema = CreateSchema(
            """{"$ref":"https://example.test/reference.json"}""",
            [new SchemaReference
            {
                Name = "https://example.test/reference.json",
                Subject = "reference-value",
                Version = 1
            }]);

        Assert.Throws<TimeoutException>(() => factory.GetOrCreate(schema));
    }

    [Test]
    public async Task Serializer_ValidationModesAreIndependent()
    {
        const string incompatibleSchema = """
            { "type": "object", "properties": { "id": { "type": "string" } }, "required": ["id"] }
            """;
        using var registry = new MockSchemaRegistryClient();
        var factory = new StreamingJsonSchemaValidatorFactory(registry);
        await using var readOnlySerializer = new JsonSchemaRegistrySerializer<ValidationPayload>(
            registry,
            incompatibleSchema,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = factory,
                Mode = JsonSchemaValidationMode.Deserialize
            });
        await using var writeSerializer = new JsonSchemaRegistrySerializer<ValidationPayload>(
            registry,
            incompatibleSchema,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = factory,
                Mode = JsonSchemaValidationMode.Serialize
            });
        var buffer = new ArrayBufferWriter<byte>();

        readOnlySerializer.Serialize(new ValidationPayload(7), ref buffer, Context);
        await Assert.That(buffer.WrittenCount).IsGreaterThan(5);

        var writeBuffer = new ArrayBufferWriter<byte>();
        Assert.Throws<JsonSchemaValidationException>(
            () => writeSerializer.Serialize(new ValidationPayload(7), ref writeBuffer, Context));
    }

    [Test]
    public async Task Serializer_CompilesCompleteRegisteredSchemaForReferences()
    {
        const string rootSchema = """
            {
              "$id": "https://example.test/schemas/root.json",
              "type": "object",
              "properties": { "address": { "$ref": "address.json" } },
              "required": ["address"]
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        await registry.RegisterSchemaAsync(
            "address-value",
            CreateSchema("""{"type":"object","required":["postcode"]}"""));
        await registry.RegisterSchemaAsync(
            "registered-write-value",
            CreateSchema(rootSchema, [new SchemaReference
            {
                Name = "address.json",
                Subject = "address-value",
                Version = 1
            }]));
        var validation = new JsonSchemaValidationOptions
        {
            ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
            Mode = JsonSchemaValidationMode.Serialize
        };
        await using var serializer = new JsonSchemaRegistrySerializer<ReferencedPayload>(
            registry,
            rootSchema,
            jsonOptions: null,
            validationOptions: validation,
            autoRegisterSchemas: false);
        var context = new SerializationContext
        {
            Topic = "registered-write",
            Component = SerializationComponent.Value
        };
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(new ReferencedPayload(new AddressPayload("AB1")), ref buffer, context);
        await Assert.That(buffer.WrittenCount).IsGreaterThan(5);
    }

    [Test]
    public async Task Deserializer_UsesValidatorForMessageSchemaId()
    {
        using var registry = new MockSchemaRegistryClient();
        var integerId = await registry.RegisterSchemaAsync(
            "value-v1",
            CreateSchema("""{"type":"object","properties":{"id":{"type":"integer"}},"required":["id"]}"""));
        var stringId = await registry.RegisterSchemaAsync(
            "value-v2",
            CreateSchema("""{"type":"object","properties":{"id":{"type":"string"}},"required":["id"]}"""));
        await using var deserializer = new JsonSchemaRegistryDeserializer<ValidationPayload>(
            registry,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.Deserialize
            });

        var result = deserializer.Deserialize(CreateWirePayload(integerId, """{"id":7}"""), Context);
        await Assert.That(result.Id).IsEqualTo(7);

        Assert.Throws<JsonSchemaValidationException>(
            () => deserializer.Deserialize(CreateWirePayload(stringId, """{"id":7}"""), Context));
    }

    [Test]
    public async Task Factory_CachesValidatorBySchemaIdentity()
    {
        var schema = CreateSchema(Draft7Schema);
        var factory = CreateFactory();

        var first = factory.GetOrCreate(schema);
        var second = factory.GetOrCreate(schema);

        await Assert.That(second).IsSameReferenceAs(first);
    }

    [Test]
    public void Factory_RejectsNonJsonSchemas()
    {
        using var registry = new MockSchemaRegistryClient();
        var factory = new StreamingJsonSchemaValidatorFactory(registry);
        var schema = new Schema { SchemaType = SchemaType.Avro, SchemaString = "{}" };

        Assert.Throws<ArgumentException>(() => factory.GetOrCreate(schema));
    }

    private static StreamingJsonSchemaValidatorFactory CreateFactory()
    {
        return new StreamingJsonSchemaValidatorFactory(
            new MockSchemaRegistryClient());
    }

    private static Schema CreateSchema(
        string schema,
        IReadOnlyList<SchemaReference>? references = null)
    {
        return new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = schema,
            References = references
        };
    }

    private static byte[] CreateWirePayload(int schemaId, string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var wire = new byte[payload.Length + 5];
        BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(1, 4), schemaId);
        payload.CopyTo(wire.AsSpan(5));
        return wire;
    }

    private sealed record ValidationPayload(int Id);
    private sealed record ReferencedPayload(AddressPayload Address);
    private sealed record AddressPayload(string Postcode);
}
