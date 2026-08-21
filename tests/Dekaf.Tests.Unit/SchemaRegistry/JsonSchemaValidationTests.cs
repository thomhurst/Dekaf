using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Text;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Json;
using Dekaf.Serialization;
using Dekaf.Errors;
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
    public async Task ValidationException_IsAKafkaException()
    {
        var validator = CreateFactory().GetOrCreate(CreateSchema("""{"type":"string"}"""));

        var exception = Assert.Throws<JsonSchemaValidationException>(() => validator.Validate("1"u8, 9));

        await Assert.That(exception).IsAssignableTo<KafkaException>();
    }

    [Test]
    public void Validator_PreservesExactNumericAssertions()
    {
        var minimum = CreateFactory().GetOrCreate(CreateSchema("""{"minimum":9007199254740993}"""));
        var multiple = CreateFactory().GetOrCreate(CreateSchema("""{"multipleOf":0.01}"""));

        minimum.Validate("9007199254740993"u8, 31);
        Assert.Throws<JsonSchemaValidationException>(() => minimum.Validate("9007199254740992"u8, 31));
        multiple.Validate("9007199254740993.01"u8, 32);
        Assert.Throws<JsonSchemaValidationException>(() => multiple.Validate("9007199254740993.011"u8, 32));
    }

    [Test]
    public void Validator_ClassifiesIntegersOutsideDecimalRangeExactly()
    {
        var validator = CreateFactory().GetOrCreate(CreateSchema("""{"type":"integer"}"""));

        validator.Validate("1e100"u8, 38);
        Assert.Throws<JsonSchemaValidationException>(() => validator.Validate("1e-100"u8, 38));
    }

    [Test]
    public void Validator_CombinesNumericBoundsUsingTheTighterAssertion()
    {
        var validator = CreateFactory().GetOrCreate(CreateSchema(
            """{"minimum":10,"exclusiveMinimum":5,"maximum":20,"exclusiveMaximum":25}"""));

        validator.Validate("10"u8, 33);
        validator.Validate("20"u8, 33);
        Assert.Throws<JsonSchemaValidationException>(() => validator.Validate("9"u8, 33));
        Assert.Throws<JsonSchemaValidationException>(() => validator.Validate("21"u8, 33));
    }

    [Test]
    public async Task Validator_RejectsRequiredOnlyPropertyWhenAdditionalPropertiesAreDisabled()
    {
        var validator = CreateFactory().GetOrCreate(CreateSchema(
            """{"type":"object","required":["ssn"],"additionalProperties":false}"""));

        var exception = Assert.Throws<JsonSchemaValidationException>(
            () => validator.Validate("""{"ssn":"123-45-6789"}"""u8, 34));

        await Assert.That(exception.Keyword).IsEqualTo("additionalProperties");
    }

    [Test]
    public void Validator_AppliesRefSiblingsAccordingToDialect()
    {
        var draft7 = CreateFactory().GetOrCreate(CreateSchema(
            """{"$schema":"http://json-schema.org/draft-07/schema#","definitions":{"s":{"type":"string"}},"$ref":"#/definitions/s","minLength":5}"""));
        var draft202012 = CreateFactory().GetOrCreate(CreateSchema(
            """{"$schema":"https://json-schema.org/draft/2020-12/schema","$defs":{"s":{"type":"string"}},"$ref":"#/$defs/s","minLength":5}"""));

        draft7.Validate("\"a\""u8, 35);
        Assert.Throws<JsonSchemaValidationException>(() => draft202012.Validate("\"a\""u8, 36));
    }

    [Test]
    public void Validator_ResolvesEmbeddedResourcesById()
    {
        var validator = CreateFactory().GetOrCreate(CreateSchema(
            """
            {
              "$schema":"https://json-schema.org/draft/2020-12/schema",
              "$id":"https://example.test/root.json",
              "$defs":{"inner":{"$id":"inner.json","type":"string","minLength":2}},
              "properties":{"value":{"$ref":"inner.json"}}
            }
            """));

        validator.Validate("""{"value":"ok"}"""u8, 37);
        Assert.Throws<JsonSchemaValidationException>(() => validator.Validate("""{"value":"x"}"""u8, 37));
    }

    [Test]
    public void Factory_RejectsMixedTupleKeywordDialects()
    {
        const string schema =
            """{"prefixItems":[{"type":"string"}],"items":[{"type":"integer"}]}""";

        Assert.Throws<NotSupportedException>(() => CreateFactory().GetOrCreate(CreateSchema(schema)));
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
    public async Task Serializer_AsyncStrategyOptionsOverload_AppliesValidation()
    {
        const string incompatibleSchema =
            """{ "type": "object", "properties": { "id": { "type": "string" } }, "required": ["id"] }""";
        using var registry = new MockSchemaRegistryClient();
        var strategy = new FixedAsyncSubjectNameStrategy("validation-value");
        await using var serializer = new JsonSchemaRegistrySerializer<ValidationPayload>(
            registry,
            strategy,
            incompatibleSchema,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.Serialize
            });
        _ = await serializer.PrepareAsync(Context.Topic, new ValidationPayload(7));
        var buffer = new ArrayBufferWriter<byte>();

        Assert.Throws<JsonSchemaValidationException>(
            () => serializer.Serialize(new ValidationPayload(7), ref buffer, Context));
    }

    [Test]
    public async Task Serializer_AsyncStrategyTypeInfoOverload_AppliesValidation()
    {
        const string incompatibleSchema =
            """{ "type": "object", "properties": { "id": { "type": "string" } }, "required": ["id"] }""";
        using var registry = new MockSchemaRegistryClient();
        var strategy = new FixedAsyncSubjectNameStrategy("validation-value");
        var typeInfo = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<ValidationPayload>)
            System.Text.Json.JsonSerializerOptions.Default.GetTypeInfo(typeof(ValidationPayload));
        await using var serializer = new JsonSchemaRegistrySerializer<ValidationPayload>(
            registry,
            strategy,
            incompatibleSchema,
            typeInfo,
            new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.Serialize
            });
        _ = await serializer.PrepareAsync(Context.Topic, new ValidationPayload(7));
        var buffer = new ArrayBufferWriter<byte>();

        Assert.Throws<JsonSchemaValidationException>(
            () => serializer.Serialize(new ValidationPayload(7), ref buffer, Context));
    }

    [Test]
    public async Task Serializer_AutoRegistrationWithValidation_UsesIdOnlyLookup()
    {
        const string schemaText = """{ "type": "object", "required": ["id"] }""";
        var schema = CreateSchema(schemaText);
        using var registry = Substitute.For<ISchemaRegistryClient>();
        registry.GetOrRegisterSchemaAsync(
                "validation-value",
                Arg.Any<Schema>(),
                Arg.Any<CancellationToken>())
            .Returns(42);
        registry.GetSchemaAsync(42, Arg.Any<CancellationToken>())
            .Returns(schema);
        await using var serializer = new JsonSchemaRegistrySerializer<ValidationPayload>(
            registry,
            schemaText,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.Serialize
            });
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(new ValidationPayload(7), ref buffer, Context);

        await registry.Received(1).GetSchemaAsync(42, Arg.Any<CancellationToken>());
        await registry.DidNotReceive().GetSchemaAsync(
            42,
            "validation-value",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Serializer_AutoRegistrationWithCustomRules_UsesLocalSchema()
    {
        const string schemaText = """{ "type": "object", "required": ["id"] }""";
        using var registry = Substitute.For<ISchemaRegistryClient>();
        registry.GetOrRegisterSchemaAsync(
                "validation-value",
                Arg.Any<Schema>(),
                Arg.Any<CancellationToken>())
            .Returns(42);
        await using var serializer = new JsonSchemaRegistrySerializer<ValidationPayload>(
            registry,
            schemaText,
            jsonOptions: null,
            ruleExecutor: new PassThroughRuleExecutor());
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(new ValidationPayload(7), ref buffer, Context);

        await registry.DidNotReceive().GetSchemaAsync(
            42,
            "validation-value",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Serializer_ValidatesAfterDomainRulesBeforeEncodingRules()
    {
        const string schemaText = """{ "type": "string" }""";
        using var registry = new MockSchemaRegistryClient();
        await registry.RegisterSchemaAsync(
            "validation-value",
            new Schema
            {
                SchemaType = SchemaType.Json,
                SchemaString = schemaText,
                RuleSet = new SchemaRuleSet
                {
                    DomainRules = [CreateRule("domain", "DOMAIN")],
                    EncodingRules = [CreateRule("encoding", "ENCODING")],
                    HasFixedRuleCollections = true
                }
            });
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor(
        [
            new ReplacingRuleHandler("DOMAIN", "1"u8.ToArray(), calls),
            new ReplacingRuleHandler("ENCODING", "\"encoded\""u8.ToArray(), calls)
        ]);
        await using var serializer = new JsonSchemaRegistrySerializer<string>(
            registry,
            schemaText,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.Serialize
            },
            autoRegisterSchemas: false,
            ruleExecutor: executor);
        var buffer = new ArrayBufferWriter<byte>();

        Assert.Throws<JsonSchemaValidationException>(
            () => serializer.Serialize("valid", ref buffer, Context));

        await Assert.That(calls).IsEquivalentTo(["domain"]);
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
            validation,
            new JsonSchemaSerializerConfig
            {
                AutoRegisterSchemas = false,
                UseLatestVersion = true
            });
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
    public async Task Serializer_PrepareAsync_CachesCompleteRegisteredSchemaForReferences()
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
        await using var serializer = new JsonSchemaRegistrySerializer<ReferencedPayload>(
            registry,
            rootSchema,
            jsonOptions: null,
            new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.Serialize
            },
            new JsonSchemaSerializerConfig
            {
                AutoRegisterSchemas = false,
                UseLatestVersion = true
            });
        var context = new SerializationContext
        {
            Topic = "registered-write",
            Component = SerializationComponent.Value
        };

        var resolved = await serializer.PrepareAsync(
            context.Topic,
            new ReferencedPayload(new AddressPayload("AB1")));
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(new ReferencedPayload(new AddressPayload("AB1")), ref buffer, context);

        await Assert.That(resolved.Schema.References).IsNotNull();
        await Assert.That(resolved.Schema.References!.Count).IsEqualTo(1);
        await Assert.That(buffer.WrittenCount).IsGreaterThan(5);
    }

    [Test]
    public async Task Serializer_DefaultAutoRegistrationUsesServerSchemaReferences()
    {
        const string rootSchema =
            """{"$id":"https://example.test/root.json","properties":{"address":{"$ref":"address.json"}},"required":["address"]}""";
        using var handler = new QueueingHandler(
            """
            {
              "subject":"registered-write-value","version":1,"id":42,
              "schema":"{\"$id\":\"https://example.test/root.json\",\"properties\":{\"address\":{\"$ref\":\"address.json\"}},\"required\":[\"address\"]}",
              "schemaType":"JSON",
              "references":[{"name":"address.json","subject":"address-value","version":1}]
            }
            """,
            """
            {
              "subject":"address-value","version":1,"id":43,
              "schema":"{\"type\":\"object\",\"required\":[\"postcode\"]}",
              "schemaType":"JSON"
            }
            """);
        using var registry = new SchemaRegistryClient(
            new SchemaRegistryConfig { Url = "http://registry:8081" },
            handler);
        await using var serializer = new JsonSchemaRegistrySerializer<ReferencedPayload>(
            registry,
            rootSchema,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.Serialize
            });
        var context = new SerializationContext
        {
            Topic = "registered-write",
            Component = SerializationComponent.Value
        };
        var buffer = new ArrayBufferWriter<byte>();

        var resolved = await serializer.PrepareAsync(
            context.Topic,
            new ReferencedPayload(new AddressPayload("AB1")));
        serializer.Serialize(new ReferencedPayload(new AddressPayload("AB1")), ref buffer, context);

        await Assert.That(resolved.Schema.References).IsNotNull();
        await Assert.That(resolved.Schema.References!.Count).IsEqualTo(1);
        await Assert.That(buffer.WrittenCount).IsGreaterThan(5);
        await Assert.That(handler.RequestCount).IsEqualTo(2);
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

    private static SchemaRule CreateRule(string name, string type) =>
        new()
        {
            Name = name,
            Type = type,
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.Write
        };

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

    private sealed class FixedAsyncSubjectNameStrategy(string subject) : IAsyncSubjectNameStrategy
    {
        public ValueTask<string> GetSubjectNameAsync(
            string topic,
            string? recordType,
            bool isKey,
            CancellationToken cancellationToken = default) =>
            new(subject);
    }

    private sealed class PassThroughRuleExecutor : ISchemaRegistryRuleExecutor
    {
        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;
    }

    private sealed class ReplacingRuleHandler(
        string type,
        ReadOnlyMemory<byte> replacement,
        List<string> calls) : ISchemaRegistryRuleHandler
    {
        public string Type => type;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context)
        {
            calls.Add(context.Rule.Name);
            return replacement;
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => payload;
    }

    private sealed class QueueingHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);

        internal int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responses.Dequeue(), Encoding.UTF8, "application/json")
            });
        }
    }
}
