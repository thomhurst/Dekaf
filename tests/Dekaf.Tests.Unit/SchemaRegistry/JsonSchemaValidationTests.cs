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
    public async Task Validator_AcceptsIntMinValueAsARealSchemaId()
    {
        var validator = CreateFactory().GetOrCreate(CreateSchema("""{"type":"string"}"""));

        var exception = Assert.Throws<JsonSchemaValidationException>(
            () => validator.Validate("1"u8, int.MinValue));

        await Assert.That(exception.SchemaId).IsEqualTo(int.MinValue);
        await Assert.That(exception.Keyword).IsEqualTo("type");
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
    public void InlineRules_ApplyRefSiblingsAccordingToDialect()
    {
        const string siblingRule =
            "\"confluent:rules\":[{\"name\":\"sibling\",\"expr\":\"false\"}]";
        var explicitDraft7 = CreateFactory().GetOrCreate(CreateSchema(
            """{"$schema":"http://json-schema.org/draft-07/schema#","definitions":{"s":{}},"$ref":"#/definitions/s",""" +
            siblingRule + "}"));
        var implicitDraft7 = CreateFactory().GetOrCreate(CreateSchema(
            """{"definitions":{"s":{}},"$ref":"#/definitions/s",""" + siblingRule + "}"));
        var draft202012 = CreateFactory().GetOrCreate(CreateSchema(
            """{"$schema":"https://json-schema.org/draft/2020-12/schema","$defs":{"s":{}},"$ref":"#/$defs/s",""" +
            siblingRule + "}"));

        explicitDraft7.ValidateRules("1"u8.ToArray(), 35, failFast: false);
        implicitDraft7.ValidateRules("1"u8.ToArray(), 35, failFast: false);
        Assert.Throws<ValidationRulesFailedException>(() => draft202012.ValidateRules(
            "1"u8.ToArray(),
            36,
            failFast: false));
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
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Serializer_AsyncStrategyOverloadsApplyInlineRules(
        bool useTypeInfo,
        bool usePreparedSerialization)
    {
        const string schemaText = """
            {
              "confluent:rules": [{ "name": "validName", "expr": "this.name == 'ok'" }]
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        var strategy = new FixedAsyncSubjectNameStrategy("validation-value");
        var validationOptions = new JsonSchemaValidationOptions
        {
            ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
            Mode = JsonSchemaValidationMode.None,
            ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
        };
        var typeInfo = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<NamePayload>)
            System.Text.Json.JsonSerializerOptions.Default.GetTypeInfo(typeof(NamePayload));
        await using var serializer = useTypeInfo
            ? new JsonSchemaRegistrySerializer<NamePayload>(
                registry,
                strategy,
                schemaText,
                typeInfo,
                validationOptions)
            : new JsonSchemaRegistrySerializer<NamePayload>(
                registry,
                strategy,
                schemaText,
                jsonOptions: null,
                validationOptions);
        var invalidValue = new NamePayload("bad");
        var buffer = new ArrayBufferWriter<byte>();

        if (usePreparedSerialization)
        {
            var admissionSerializer = (IAsyncSerializerPreparationAdmission<NamePayload>)serializer;
            var admission = await admissionSerializer.PrepareForSerializationAsync(
                invalidValue,
                Context);

            Assert.Throws<ValidationRulesFailedException>(() => admissionSerializer.SerializePrepared(
                invalidValue,
                ref buffer,
                Context,
                in admission));
        }
        else
        {
            _ = await serializer.PrepareAsync(Context.Topic, invalidValue);
            Assert.Throws<ValidationRulesFailedException>(
                () => serializer.Serialize(invalidValue, ref buffer, Context));
        }
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
    public async Task Serializer_AutoRegistrationWithRulesOnlyValidation_UsesIdOnlyLookup()
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
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
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
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
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

    [Test]
    public async Task InlineRules_AggregateNestedArrayAndMapViolations()
    {
        const string schemaText = """
            {
              "type": "object",
              "confluent:rules": [{ "name": "root", "expr": "this.name != 'forbidden'" }],
              "properties": {
                "name": {
                  "type": "string",
                  "confluent:rules": [{ "name": "name", "doc": "name required", "expr": "size(this) > 0" }]
                },
                "items": {
                  "type": "array",
                  "items": {
                    "type": "integer",
                    "confluent:rules": [{ "name": "positive", "expr": "this >= 0" }]
                  }
                },
                "labels": {
                  "type": "object",
                  "additionalProperties": {
                    "type": "string",
                    "confluent:rules": [{ "name": "label", "expr": "size(this) > 0" }]
                  }
                }
              }
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"name":"","items":[1,-1],"labels":{"region":""}}"""u8.ToArray(),
            17,
            failFast: false));

        await Assert.That(exception.Violations.Count).IsEqualTo(3);
        await Assert.That(exception.Message).Contains("$.name: name: name required");
        await Assert.That(exception.Message).Contains("$.items[1]: positive");
        await Assert.That(exception.Message).Contains("$.labels[\"region\"]: label");
    }

    [Test]
    public async Task InlineRules_QuoteNonIdentifierPropertyNamesInPaths()
    {
        const string schemaText = """
            {
              "properties": {
                "a.b": {
                  "confluent:rules": [{ "name": "required", "expr": "size(this) > 0" }]
                }
              }
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"a.b":""}"""u8.ToArray(),
            17,
            failFast: false));

        await Assert.That(exception.Violations[0].FieldPath).IsEqualTo("$[\"a.b\"]");
    }

    [Test]
    public async Task InlineRules_JsonEscapeQuotedPropertyNamesInPaths()
    {
        const string schemaText = """
            {
              "properties": {
                "a\"b\\c\n": {
                  "confluent:rules": [{ "name": "required", "expr": "size(this) > 0" }]
                }
              }
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"a\"b\\c\n":""}"""u8.ToArray(),
            17,
            failFast: false));

        await Assert.That(exception.Violations[0].FieldPath)
            .IsEqualTo("$[\"a\\\"b\\\\c\\n\"]");
    }

    [Test]
    public async Task InlineRules_JsonEscapeAdditionalPropertyNamesInPaths()
    {
        const string schemaText = """
            {
              "additionalProperties": {
                "confluent:rules": [{ "name": "required", "expr": "size(this) > 0" }]
              }
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"a\b\f\n\r\t\u0001\"\\":""}"""u8.ToArray(),
            17,
            failFast: false));

        await Assert.That(exception.Violations[0].FieldPath)
            .IsEqualTo("""$["a\b\f\n\r\t\u0001\"\\"]""");
    }

    [Test]
    public async Task InlineRules_StringResultAndFailFastMatchConfluentSemantics()
    {
        const string schemaText = """
            {
              "type": "object",
              "properties": {
                "id": {
                  "type": "string",
                  "confluent:rules": [
                    { "name": "prefix", "expr": "this.startsWith('ord-') ? '' : 'id must start with ord-'" },
                    { "name": "length", "expr": "size(this) > 8" }
                  ]
                }
              }
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"id":"bad"}"""u8.ToArray(),
            18,
            failFast: true));

        await Assert.That(exception.Violations.Count).IsEqualTo(1);
        await Assert.That(exception.Violations[0].Message).IsEqualTo("id must start with ord-");
        await Assert.That(exception.Message).Contains("1 violation)");
    }

    [Test]
    public void InlineRules_SkipNullAndSupportNowInternalReferencesAndOneOf()
    {
        const string schemaText = """
            {
              "$defs": {
                "code": {
                  "type": ["string", "null"],
                  "confluent:rules": [{ "name": "code", "expr": "size(this) > 0 && now > timestamp('2000-01-01T00:00:00Z')" }]
                }
              },
              "type": "object",
              "properties": {
                "code": { "$ref": "#/$defs/code" },
                "choice": {
                  "oneOf": [
                    {
                      "type": "object",
                      "required": ["a"],
                      "confluent:rules": [{ "name": "a", "expr": "this.a == 'ok'" }],
                      "properties": { "a": { "type": "string" } }
                    },
                    {
                      "type": "object",
                      "required": ["b"],
                      "confluent:rules": [{ "name": "b", "expr": "this.b == 'ok'" }],
                      "properties": { "b": { "type": "string" } }
                    }
                  ]
                }
              }
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules("""{"code":null,"choice":{"b":"ok"}}"""u8.ToArray(), 19, failFast: false);
    }

    [Test]
    public async Task InlineRules_RequireExactlyOneMatchingOneOfBranch()
    {
        const string schemaText = """
            {
              "oneOf": [
                { "type": "object", "properties": { "value": { "type": "integer" } } },
                { "type": "object", "properties": { "value": { "minimum": 0 } } }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        var exception = Assert.Throws<JsonSchemaValidationException>(() => validator.ValidateRules(
            """{"value":1}"""u8.ToArray(),
            19,
            failFast: false));

        await Assert.That(exception.SchemaId).IsEqualTo(19);
        await Assert.That(exception.Keyword).IsEqualTo("oneOf");
    }

    [Test]
    public void InlineRules_RejectTrailingJsonContent()
    {
        const string schemaText = """
            {
              "confluent:rules": [{ "name": "valid", "expr": "true" }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        Assert.Throws<JsonSchemaValidationException>(() => validator.ValidateRules(
            """{"value":1}{"value":2}"""u8.ToArray(),
            19,
            failFast: false));
        Assert.Throws<JsonSchemaValidationException>(() => validator.ValidateRules(
            """{"value":1}garbage"""u8.ToArray(),
            19,
            failFast: false));
    }

    [Test]
    [Arguments("")]
    [Arguments(" \r\n\t")]
    public async Task InlineRules_RejectMissingJsonRoot(string payload)
    {
        const string schemaText = """
            {
              "confluent:rules": [{ "name": "valid", "expr": "true" }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        var exception = Assert.Throws<JsonSchemaValidationException>(() => validator.ValidateRules(
            Encoding.UTF8.GetBytes(payload),
            19,
            failFast: false));

        await Assert.That(exception.SchemaId).IsEqualTo(19);
        await Assert.That(exception.Keyword).IsEqualTo("$parse");
        await Assert.That(exception.JsonPath).IsEqualTo("$");
    }

    [Test]
    public void InlineRules_CompositionCountsFinalDuplicateProperties()
    {
        const string schemaText = """
            {
              "oneOf": [
                { "type": "object", "minProperties": 2 },
                {
                  "type": "object",
                  "maxProperties": 1,
                  "confluent:rules": [{ "name": "final-value", "expr": "this.x == 2" }]
                }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules("""{"x":1,"x":2}"""u8.ToArray(), 19, failFast: false);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InlineRules_NestedAllOfPreservesLeafFailure(bool failFast)
    {
        const string schemaText = """
            {
              "allOf": [{
                "type": "object",
                "properties": {
                  "child": {
                    "allOf": [{
                      "type": "object",
                      "properties": {
                        "child": {
                          "allOf": [{
                            "type": "integer",
                            "confluent:rules": [{ "name": "positive", "expr": "this > 0" }]
                          }]
                        }
                      }
                    }]
                  }
                }
              }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"child":{"child":-1}}"""u8.ToArray(),
            19,
            failFast));

        await Assert.That(exception.Violations).Count().IsEqualTo(1);
        await Assert.That(exception.Violations[0].FieldPath).IsEqualTo("$.child.child");
    }

    [Test]
    [Arguments("anyOf")]
    [Arguments("oneOf")]
    public async Task InlineRules_CompositionsRejectNullWhenNoBranchMatches(string keyword)
    {
        var validator = CreateFactory().GetOrCreate(CreateSchema(
            $$"""{ "{{keyword}}": [{ "type": "string" }] }"""));

        var exception = Assert.Throws<JsonSchemaValidationException>(() => validator.ValidateRules(
            "null"u8.ToArray(),
            19,
            failFast: false));

        await Assert.That(exception.SchemaId).IsEqualTo(19);
        await Assert.That(exception.Keyword).IsEqualTo(keyword);
    }

    [Test]
    [Arguments("anyOf")]
    [Arguments("oneOf")]
    public async Task InlineRules_EmptyCompositionBesideReferenceRejectsEveryInstance(string keyword)
    {
        var validator = CreateFactory().GetOrCreate(CreateSchema($$"""
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$defs": { "base": { "type": "object" } },
              "$ref": "#/$defs/base",
              "{{keyword}}": []
            }
            """));

        var exception = Assert.Throws<JsonSchemaValidationException>(() => validator.ValidateRules(
            "{}"u8.ToArray(),
            19,
            failFast: false));

        await Assert.That(exception.Keyword).IsEqualTo(keyword);
    }

    [Test]
    public void InlineRules_ParseAdjacentArithmeticOperators()
    {
        const string schemaText = """
            {
              "type": "integer",
              "confluent:rules": [{ "name": "sum", "expr": "this+2 == 5" }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules("3"u8.ToArray(), 21, failFast: false);
    }

    [Test]
    public void InlineRules_AcceptCelNumberGrammarBeyondJson()
    {
        const string schemaText = """
            {
              "type": "number",
              "confluent:rules": [{ "name": "cel-numbers", "expr": "this == 5. && this < 5.e5 && this == 05" }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules("5"u8.ToArray(), 21, failFast: false);
    }

    [Test]
    public void InlineRules_PreserveLargeNumbersAndCountUnicodeScalars()
    {
        const string schemaText = """
            {
              "type": "object",
              "confluent:rules": [
                {
                  "name": "exact",
                  "expr": "this.large == 9007199254740993 && size(this.symbol) == 1"
                }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            """{"large":9007199254740993,"symbol":"😀"}"""u8.ToArray(),
            23,
            failFast: false);
        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"large":9007199254740992,"symbol":"😀"}"""u8.ToArray(),
            23,
            failFast: false));
    }

    [Test]
    public void InlineRules_CompareArbitraryPrecisionNumbersExactly()
    {
        const string schemaText = """
            {
              "type": "object",
              "confluent:rules": [{
                "name": "exact",
                "expr": "this.precise == 0.1234567890123456789012345678901 && this.precise < this.next && this.huge == 1e1000 && this.huge > 9e999 && this.huge > 1 && this.negative < -1 && this.overflowA < this.overflowB && this.underflowB < this.underflowA"
              }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            """{"precise":0.1234567890123456789012345678901,"next":0.1234567890123456789012345678902,"huge":1e1000,"negative":-1e1000,"overflowA":1e9223372036854775808,"overflowB":1e9223372036854775809,"underflowA":1e-9223372036854775808,"underflowB":1e-9223372036854775809}"""u8.ToArray(),
            23,
            failFast: false);
        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"precise":0.1234567890123456789012345678902,"next":0.1234567890123456789012345678901,"huge":1e1000,"negative":-1e1000}"""u8.ToArray(),
            23,
            failFast: false));
    }

    [Test]
    public void InlineRules_NegateArbitraryPrecisionNumbersExactly()
    {
        const string schemaText = """
            {
              "confluent:rules": [{
                "name": "exact-negation",
                "expr": "-this.huge == -1e1000 && -this.precise == -0.1234567890123456789012345678901 && -(-this.huge) == this.huge"
              }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            """{"huge":1e1000,"precise":0.1234567890123456789012345678901}"""u8.ToArray(),
            23,
            failFast: false);
        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"huge":1e1001,"precise":0.1234567890123456789012345678901}"""u8.ToArray(),
            23,
            failFast: false));
    }

    [Test]
    public void InlineRules_NegateDecimalMinValueExactly()
    {
        const string schemaText = """
            {
              "confluent:rules": [{
                "name": "decimal-boundary",
                "expr": "-this == 79228162514264337593543950335 && -(-this) == this"
              }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            "-79228162514264337593543950335"u8.ToArray(),
            23,
            failFast: false);
    }

    [Test]
    public async Task InlineRules_EvaluateSiblingMemberRulesAgainstSharedValues()
    {
        const string schemaText = """
            {
              "confluent:rules": [
                { "name": "a", "expr": "this.a == 1" },
                { "name": "b", "expr": "this.b == 2" },
                { "name": "c", "expr": "this.c == 3" }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules("""{"a":1,"b":2,"c":3}"""u8.ToArray(), 24, failFast: false);
        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"a":0,"b":2,"c":0}"""u8.ToArray(),
            24,
            failFast: false));

        await Assert.That(exception.Violations.Select(static violation => violation.Rule.Name!))
            .IsEquivalentTo(["a", "c"]);
    }

    [Test]
    public async Task InlineRules_EvaluateNestedSiblingMemberRulesAgainstSharedValues()
    {
        const string schemaText = """
            {
              "confluent:rules": [
                { "name": "a", "expr": "this.details.a == 1" },
                { "name": "b", "expr": "this.details.b == 2" },
                { "name": "c", "expr": "this.details.c == 3" }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            """{"details":{"a":1,"b":2,"c":3}}"""u8.ToArray(),
            25,
            failFast: false);
        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"details":{"a":0,"b":2,"c":0}}"""u8.ToArray(),
            25,
            failFast: false));

        await Assert.That(exception.Violations.Select(static violation => violation.Rule.Name!))
            .IsEquivalentTo(["a", "c"]);
    }

    [Test]
    public async Task InlineRules_EvaluateMemberRulesAcrossSchemaLayers()
    {
        const string schemaText = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$defs": {
                "base": {
                  "confluent:rules": [{ "name": "a", "expr": "this.a == 1" }]
                }
              },
              "$ref": "#/$defs/base",
              "confluent:rules": [{ "name": "b", "expr": "this.b == 2" }],
              "allOf": [{
                "confluent:rules": [{ "name": "c", "expr": "this.c == 3" }]
              }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules("""{"a":1,"b":2,"c":3}"""u8.ToArray(), 25, failFast: false);
        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"a":0,"b":2,"c":0}"""u8.ToArray(),
            25,
            failFast: false));

        await Assert.That(exception.Violations.Select(static violation => violation.Rule.Name!))
            .IsEquivalentTo(["a", "c"]);
    }

    [Test]
    public async Task InlineRules_ReResolveSharedMembersAfterNestedTraversal()
    {
        const string schemaText = """
            {
              "allOf": [
                {
                  "confluent:rules": [{ "name": "a", "expr": "this.a == 1" }],
                  "properties": {
                    "child": {
                      "confluent:rules": [{ "name": "child", "expr": "this.x == 2" }]
                    }
                  }
                },
                {
                  "confluent:rules": [{ "name": "b", "expr": "this.b == 3" }]
                }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            """{"a":1,"b":3,"child":{"x":2}}"""u8.ToArray(),
            25,
            failFast: false);
        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"a":1,"b":0,"child":{"x":2}}"""u8.ToArray(),
            25,
            failFast: false));

        await Assert.That(exception.Violations.Select(static violation => violation.Rule.Name!))
            .IsEquivalentTo(["b"]);
    }

    [Test]
    public void InlineRules_MissingMembersDoNotReuseValuesFromEarlierPayloads()
    {
        const string schemaText = """
            {
              "confluent:rules": [
                { "name": "members", "expr": "this.a == 1 && this.b == 2" }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules("""{"a":1,"b":2}"""u8.ToArray(), 25, failFast: false);

        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"a":1}"""u8.ToArray(),
            25,
            failFast: false));
    }

    [Test]
    public void InlineRules_DuplicateParentsUseFinalDescendantValues()
    {
        const string schemaText = """
            {
              "confluent:rules": [
                { "name": "final-value", "expr": "this.a.b == 1" }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            """{"a":{"b":0},"a":{"b":1}}"""u8.ToArray(),
            25,
            failFast: false);
        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"a":{"b":1},"a":{}}"""u8.ToArray(),
            25,
            failFast: false));
        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"a":{"b":1},"a":null}"""u8.ToArray(),
            25,
            failFast: false));
    }

    [Test]
    public void InlineRules_DuplicateDeclaredPropertiesUseFinalValue()
    {
        const string schemaText = """
            {
              "properties": {
                "name": {
                  "confluent:rules": [{ "name": "final-value", "expr": "this == 'ok'" }]
                }
              }
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            """{"name":"bad","name":"ok"}"""u8.ToArray(),
            25,
            failFast: false);
        validator.ValidateRules(
            """{"name":"bad","name":"ok"}"""u8.ToArray(),
            25,
            failFast: true);
        validator.ValidateRules(
            """{"name":"bad","\u006eame":"ok"}"""u8.ToArray(),
            25,
            failFast: false);
        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"name":"ok","name":"bad"}"""u8.ToArray(),
            25,
            failFast: false));
        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"name":"ok","name":"bad"}"""u8.ToArray(),
            25,
            failFast: true));
    }

    [Test]
    public void InlineRules_DuplicatePropertiesUseFinalCompositionBranch()
    {
        const string schemaText = """
            {
              "properties": {
                "value": {
                  "oneOf": [
                    {
                      "type": "integer",
                      "confluent:rules": [{ "name": "positive", "expr": "this > 0" }]
                    },
                    {
                      "type": "string",
                      "confluent:rules": [{ "name": "ok", "expr": "this == 'ok'" }]
                    }
                  ]
                }
              }
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules("""{"value":0,"value":"ok"}"""u8.ToArray(), 25, failFast: false);
        validator.ValidateRules("""{"value":0,"value":"ok"}"""u8.ToArray(), 25, failFast: true);
        validator.ValidateRules("""{"value":null,"value":"ok"}"""u8.ToArray(), 25, failFast: false);
        validator.ValidateRules("""{"value":null,"value":"ok"}"""u8.ToArray(), 25, failFast: true);
        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"value":"ok","value":0}"""u8.ToArray(),
            25,
            failFast: false));
    }

    [Test]
    [Arguments("anyOf")]
    [Arguments("oneOf")]
    public void InlineRules_AggregateCompositionUsesFinalDuplicatePropertyValue(string keyword)
    {
        var schemaText = $$"""
            {
              "{{keyword}}": [
                {
                  "properties": {
                    "value": {
                      "type": "integer",
                      "confluent:rules": [{ "name": "positive", "expr": "this > 0" }]
                    }
                  }
                },
                {
                  "properties": {
                    "value": {
                      "type": "string",
                      "confluent:rules": [{ "name": "ok", "expr": "this == 'ok'" }]
                    }
                  }
                }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            """{"value":1,"value":"ok"}"""u8.ToArray(),
            25,
            failFast: false);
    }

    [Test]
    [Arguments(true, true)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(false, false)]
    public void InlineRules_AggregateManyDuplicatePropertiesUseFinalValues(
        bool declaredProperties,
        bool failFast)
    {
        var (schemaText, payload) = CreateManyDuplicateRuleCase(
            propertyCount: 16,
            declaredProperties);
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(payload, 25, failFast);
    }

    [Test]
    public void InlineRules_AggregateProbeGrowthPreservesCompositionCacheOwnership()
    {
        var (schemaText, payload) = CreateAggregateCompositionGrowthCase(branchCount: 40);
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(payload, 25, failFast: false);
    }

    [Test]
    public void InlineRules_SizeCountsUniqueMapKeys()
    {
        const string schemaText = """
            {
              "confluent:rules": [{
                "name": "map-size",
                "expr": "size(this) == 1 && this.a == 2"
              }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules("""{"a":1,"a":2}"""u8.ToArray(), 25, failFast: false);
        validator.ValidateRules("""{"a":1,"\u0061":2}"""u8.ToArray(), 25, failFast: false);
        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"a":1,"b":2}"""u8.ToArray(),
            25,
            failFast: false));
    }

    [Test]
    public void InlineRules_ResolveMemberPathsAtSupportedDepth()
    {
        var (schema, payload) = CreateDeepMemberRule(depth: 80);
        var validator = CreateFactory().GetOrCreate(CreateSchema(schema));

        validator.ValidateRules(payload, 25, failFast: false);
    }

    [Test]
    public void InlineRules_RejectMalformedDeclarations()
    {
        var invalidCollection = CreateFactory().GetOrCreate(CreateSchema(
            """{ "confluent:rules": {} }"""));
        var invalidEntry = CreateFactory().GetOrCreate(CreateSchema(
            """{ "confluent:rules": [true] }"""));

        invalidCollection.Validate("{}"u8, 25);
        invalidEntry.Validate("{}"u8, 25);
        Assert.Throws<InvalidOperationException>(() => invalidCollection.ValidateRules(
            "{}"u8.ToArray(),
            25,
            failFast: false));
        Assert.Throws<InvalidOperationException>(() => invalidEntry.ValidateRules(
            "{}"u8.ToArray(),
            25,
            failFast: false));
    }

    [Test]
    public async Task Serializer_DisabledInlineRulesIgnoreMalformedDeclarations()
    {
        const string schemaText = """
            {
              "type": "object",
              "confluent:rules": {},
              "properties": { "name": { "type": "string" } }
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = new JsonSchemaRegistrySerializer<NamePayload>(
            registry,
            schemaText,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.Serialize
            });
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(new NamePayload("valid"), ref buffer, Context);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(5);
    }

    [Test]
    public void InlineRules_CompareCollectionsStructurally()
    {
        const string schemaText = """
            {
              "type": "object",
              "confluent:rules": [{
                "name": "collections",
                "expr": "this.left == this.right && this.values == this.expected"
              }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            """{"left":{"id":1e1000,"name":"a"},"right":{"name":"a","id":10e999},"values":[0.1234567890123456789012345678901,"\u0061"],"expected":[0.12345678901234567890123456789010,"a"]}"""u8.ToArray(),
            24,
            failFast: false);
        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"left":{},"right":{},"values":[1,2],"expected":[2,1]}"""u8.ToArray(),
            24,
            failFast: false));
    }

    [Test]
    public void InlineRules_ObjectEqualityUsesFinalDuplicatePropertyValues()
    {
        const string schemaText = """
            {
              "confluent:rules": [{ "name": "equal", "expr": "this.left == this.right" }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            """{"left":{"id":1,"id":2},"right":{"id":2}}"""u8.ToArray(),
            24,
            failFast: false);
        validator.ValidateRules(
            """{"left":{"id":2},"right":{"id":1,"id":2}}"""u8.ToArray(),
            24,
            failFast: false);
        validator.ValidateRules(
            """{"left":{"id":0,"id":2},"right":{"id":1,"id":2}}"""u8.ToArray(),
            24,
            failFast: false);
        validator.ValidateRules(
            """{"left":{"nested":{"id":2}},"right":{"nested":{"id":1},"nested":{"id":2}}}"""u8.ToArray(),
            24,
            failFast: false);
        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"left":{"id":2,"id":1},"right":{"id":2}}"""u8.ToArray(),
            24,
            failFast: false));
    }

    [Test]
    public void InlineRules_CompareDeepCollectionsInOneTraversal()
    {
        const string schemaText = """
            {
              "confluent:rules": [{ "name": "equal", "expr": "this.left == this.right" }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(CreateDeepEqualityPayload(depth: 32, rightLeaf: 1), 24, failFast: false);
        Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            CreateDeepEqualityPayload(depth: 32, rightLeaf: 2),
            24,
            failFast: false));
    }

    [Test]
    public void InlineRules_LongStringCacheResetsPerPayload()
    {
        const string schemaText = """
            {
              "confluent:rules": [
                { "name": "prefix", "expr": "this.text.startsWith('prefix')" },
                { "name": "suffix", "expr": "this.text.endsWith('suffix')" },
                { "name": "contains", "expr": "this.text.contains('aaaaaaaa')" },
                { "name": "size", "expr": "size(this.text) > 500" }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));
        var valid = Encoding.UTF8.GetBytes(
            "{\"text\":\"prefix" + new string('a', 512) + "suffix\"}");
        var invalid = Encoding.UTF8.GetBytes(
            "{\"text\":\"prefix" + new string('a', 512) + "wrong\"}");

        validator.ValidateRules(valid, 24, failFast: false);
        Assert.Throws<ValidationRulesFailedException>(() =>
            validator.ValidateRules(invalid, 24, failFast: false));
        validator.ValidateRules(valid, 24, failFast: false);
    }

    [Test]
    public async Task InlineRules_RepeatedStructuralEqualityCacheResetsPerPayload()
    {
        const string schemaText = """
            {
              "confluent:rules": [
                { "name": "a", "expr": "this.left == this.right" },
                { "name": "b", "expr": "this.left == this.right" },
                { "name": "c", "expr": "this.right == this.left" },
                { "name": "d", "expr": "this.left != this.other" }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));
        var equal = """{"left":{"id":1},"right":{"id":1},"other":{"id":2}}"""u8.ToArray();
        var unequal = """{"left":{"id":1},"right":{"id":2},"other":{"id":2}}"""u8.ToArray();

        validator.ValidateRules(equal, 24, failFast: false);
        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            validator.ValidateRules(unequal, 24, failFast: false));
        validator.ValidateRules(equal, 24, failFast: false);

        await Assert.That(exception.Violations.Select(static violation => violation.Rule.Name!))
            .IsEquivalentTo(["a", "b", "c"]);
    }

    [Test]
    public void InlineRules_SameValueEqualityCacheIsolatedFromChildTraversal()
    {
        const string schemaText = """
            {
              "allOf": [
                {
                  "confluent:rules": [{ "name": "root-first", "expr": "this.left == this.right" }],
                  "properties": {
                    "child": {
                      "confluent:rules": [{ "name": "child", "expr": "this.left != this.right" }]
                    }
                  }
                },
                {
                  "confluent:rules": [{ "name": "root-second", "expr": "this.left == this.right" }]
                }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            """{"left":{"id":1},"right":{"id":1},"child":{"left":{"id":1},"right":{"id":2}}}"""u8
                .ToArray(),
            24,
            failFast: false);
    }

    [Test]
    public async Task InlineRules_MissingMembersRequireHasGuard()
    {
        const string schemaText = """
            {
              "confluent:rules": [
                { "name": "equal", "expr": "this.start == this.end" },
                { "name": "not-equal", "expr": "this.name != 'forbidden'" },
                { "name": "guarded", "expr": "!has(this.optional) || this.optional == 'present'" }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            "{}"u8.ToArray(),
            24,
            failFast: false));

        await Assert.That(exception.Violations.Select(static violation => violation.Rule.Name!))
            .IsEquivalentTo(["equal", "not-equal"]);
    }

    [Test]
    public async Task InlineRules_RejectEmptyMemberPathSegments()
    {
        foreach (var expression in new[] { "this.", "this..value", "this.value." })
        {
            var schemaText = $$"""
                { "confluent:rules": [{ "name": "invalid", "expr": "{{expression}} == 1" }] }
                """;
            var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

            var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
                "{}"u8.ToArray(),
                24,
                failFast: false));

            await Assert.That(exception.Violations[0].Cause!.Message).Contains(
                $"Unsupported CEL identifier '{expression}'");
        }
    }

    [Test]
    public void InlineRules_RejectOpaqueCustomRuleExecutor()
    {
        using var registry = new MockSchemaRegistryClient();
        var options = new JsonSchemaValidationOptions
        {
            ValidatorFactory = CreateFactory(),
            Mode = JsonSchemaValidationMode.None,
            ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
        };

        Assert.Throws<NotSupportedException>(() =>
        {
            _ = new JsonSchemaRegistrySerializer<NamePayload>(
                registry,
                """{"type":"object"}""",
                jsonOptions: null,
                validationOptions: options,
                ruleExecutor: new PassThroughRuleExecutor());
        });
    }

    [Test]
    public void InlineRules_SizeCountsNestedCollectionElementsOnce()
    {
        const string schemaText = """
            {
              "type": "array",
              "confluent:rules": [{ "name": "two", "expr": "size(this) == 2" }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules("[{},[]]"u8.ToArray(), 25, failFast: false);
    }

    [Test]
    public void InlineRules_SameValueLayersGrowSharedSizeCache()
    {
        const string schemaText = """
            {
              "confluent:rules": [{ "name": "root-size", "expr": "size(this) == 9" }],
              "allOf": [{
                "confluent:rules": [{
                  "name": "member-sizes",
                  "expr": "size(this.a) == 1 && size(this.b) == 1 && size(this.c) == 1 && size(this.d) == 1 && size(this.e) == 1 && size(this.f) == 1 && size(this.g) == 1 && size(this.h) == 1 && size(this.i) == 1"
                }]
              }]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.ValidateRules(
            """{"a":"1","b":"2","c":"3","d":"4","e":"5","f":"6","g":"7","h":"8","i":"9"}"""u8.ToArray(),
            25,
            failFast: false);
    }

    [Test]
    public async Task Validator_EnforcesCompositionKeywordsWithoutAllocatingProbeFailures()
    {
        const string schemaText = """
            {
              "allOf": [{ "type": "object" }],
              "anyOf": [
                { "required": ["legacy"] },
                { "required": ["current"] }
              ],
              "oneOf": [
                { "required": ["a"] },
                { "required": ["b"] }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        validator.Validate("""{"current":true,"a":true}"""u8, 22);
        var anyOf = Assert.Throws<JsonSchemaValidationException>(
            () => validator.Validate("""{"a":true}"""u8, 22));
        var oneOf = Assert.Throws<JsonSchemaValidationException>(
            () => validator.Validate("""{"current":true,"a":true,"b":true}"""u8, 22));

        await Assert.That(anyOf.Keyword).IsEqualTo("anyOf");
        await Assert.That(anyOf.JsonPath).IsEqualTo("$");
        await Assert.That(oneOf.Keyword).IsEqualTo("oneOf");
        await Assert.That(oneOf.JsonPath).IsEqualTo("$");
    }

    [Test]
    [Arguments("anyOf")]
    [Arguments("oneOf")]
    public async Task Validator_EmptyCompositionRejectsEveryInstance(string keyword)
    {
        var validator = CreateFactory().GetOrCreate(CreateSchema($$"""{"{{keyword}}":[]}"""));

        var exception = Assert.Throws<JsonSchemaValidationException>(
            () => validator.Validate("{}"u8, 22));

        await Assert.That(exception.Keyword).IsEqualTo(keyword);
        await Assert.That(exception.JsonPath).IsEqualTo("$");
    }

    [Test]
    public async Task Validator_CompositionProbeFailureRestoresParentPath()
    {
        const string schemaText = """
            {
              "anyOf": [
                { "properties": { "nested": { "required": ["legacy"] } } },
                { "properties": { "nested": { "required": ["current"] } } }
              ]
            }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        var exception = Assert.Throws<JsonSchemaValidationException>(
            () => validator.Validate("""{"nested":{}}"""u8, 22));

        await Assert.That(exception.Keyword).IsEqualTo("anyOf");
        await Assert.That(exception.JsonPath).IsEqualTo("$");
    }

    [Test]
    public async Task Serializer_InlineRulesRunBetweenDomainAndEncodingRules()
    {
        const string schemaText = """
            {
              "type": "object",
              "confluent:rules": [{ "name": "validName", "expr": "this.name == 'ok'" }],
              "properties": { "name": { "type": "string" } }
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        await registry.RegisterSchemaAsync(
            "inline-value",
            new Schema
            {
                SchemaType = SchemaType.Json,
                SchemaString = schemaText,
                RuleSet = new SchemaRuleSet
                {
                    DomainRules = [CreateRule("domain", "DOMAIN")],
                    EncodingRules = [CreateRule("encoding", "ENCODING")]
                }
            });
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor([
            new ReplacingRuleHandler("DOMAIN", """{"name":"ok"}"""u8.ToArray(), calls),
            new ReplacingRuleHandler("ENCODING", """{"name":"encoded"}"""u8.ToArray(), calls)
        ]);
        var options = new JsonSchemaValidationOptions
        {
            ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
            Mode = JsonSchemaValidationMode.None,
            ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
        };
        await using var serializer = new JsonSchemaRegistrySerializer<NamePayload>(
            registry,
            schemaText,
            jsonOptions: null,
            validationOptions: options,
            autoRegisterSchemas: false,
            ruleExecutor: executor);
        var buffer = new ArrayBufferWriter<byte>();
        var context = new SerializationContext
        {
            Topic = "inline",
            Component = SerializationComponent.Value
        };

        serializer.Serialize(new NamePayload("bad"), ref buffer, context);

        await Assert.That(calls).IsEquivalentTo(["domain", "encoding"]);
        await Assert.That(Encoding.UTF8.GetString(buffer.WrittenSpan[5..])).IsEqualTo("""{"name":"encoded"}""");

        await using var beforeSerializer = new JsonSchemaRegistrySerializer<NamePayload>(
            registry,
            schemaText,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = options.ValidatorFactory,
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            },
            autoRegisterSchemas: false,
            ruleExecutor: executor);
        var beforeBuffer = new ArrayBufferWriter<byte>();
        Assert.Throws<ValidationRulesFailedException>(
            () => beforeSerializer.Serialize(new NamePayload("bad"), ref beforeBuffer, context));
    }

    [Test]
    public async Task Serializer_ValidationRulesFailFastOptionStopsAfterFirstViolation()
    {
        const string schemaText = """
            {
              "confluent:rules": [
                { "name": "first", "expr": "false" },
                { "name": "second", "expr": "false" }
              ]
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = new JsonSchemaRegistrySerializer<NamePayload>(
            registry,
            schemaText,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules,
                ValidationRulesFailFast = true
            });
        var buffer = new ArrayBufferWriter<byte>();

        var exception = Assert.Throws<ValidationRulesFailedException>(
            () => serializer.Serialize(new NamePayload("bad"), ref buffer, Context));

        await Assert.That(exception.Violations.Count).IsEqualTo(1);
        await Assert.That(exception.Violations[0].Rule.Name).IsEqualTo("first");
    }

    [Test]
    public async Task Serializer_PreparedSerializationRunsInlineRules()
    {
        const string schemaText = """
            {
              "confluent:rules": [{ "name": "validName", "expr": "this.name == 'ok'" }]
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = new JsonSchemaRegistrySerializer<NamePayload>(
            registry,
            schemaText,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        var admissionSerializer = (IAsyncSerializerPreparationAdmission<NamePayload>)serializer;
        var admission = await admissionSerializer.PrepareForSerializationAsync(
            new NamePayload("bad"),
            Context);
        var buffer = new ArrayBufferWriter<byte>();

        Assert.Throws<ValidationRulesFailedException>(() => admissionSerializer.SerializePrepared(
            new NamePayload("bad"),
            ref buffer,
            Context,
            in admission));
    }

    [Test]
    public async Task Deserializer_InlineRulesRunBetweenEncodingAndDomainRules()
    {
        const string schemaText = """
            {
              "type": "object",
              "confluent:rules": [{ "name": "validName", "expr": "this.name == 'ok'" }],
              "properties": { "name": { "type": "string" } }
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "validation-value",
            new Schema
            {
                SchemaType = SchemaType.Json,
                SchemaString = schemaText,
                RuleSet = new SchemaRuleSet
                {
                    DomainRules = [CreateRule("domain", "DOMAIN", SchemaRuleMode.Read)],
                    EncodingRules = [CreateRule("encoding", "ENCODING", SchemaRuleMode.Read)]
                }
            });
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor([
            new ReplacingRuleHandler("DOMAIN", """{"name":"ok"}"""u8.ToArray(), calls),
            new ReplacingRuleHandler("ENCODING", """{"name":"bad"}"""u8.ToArray(), calls)
        ]);
        var factory = new StreamingJsonSchemaValidatorFactory(registry);
        await using var deserializer = new JsonSchemaRegistryDeserializer<NamePayload>(
            registry,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = factory,
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
            },
            ruleExecutor: executor);

        var result = deserializer.Deserialize(
            CreateWirePayload(schemaId, """{"name":"encoded"}"""),
            Context);

        await Assert.That(result.Name).IsEqualTo("ok");
        await Assert.That(calls).IsEquivalentTo(["encoding", "domain"]);

        calls.Clear();
        await using var beforeDeserializer = new JsonSchemaRegistryDeserializer<NamePayload>(
            registry,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = factory,
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            },
            ruleExecutor: executor);
        Assert.Throws<ValidationRulesFailedException>(() => beforeDeserializer.Deserialize(
            CreateWirePayload(schemaId, """{"name":"encoded"}"""),
            Context));
        await Assert.That(calls).IsEquivalentTo(["encoding"]);
    }

    [Test]
    public async Task Deserializer_LatestVersionRunsInlineRulesWithoutExplicitExecutor()
    {
        const string schemaText = """
            {
              "type": "object",
              "confluent:rules": [{ "name": "validName", "expr": "this.name == 'ok'" }],
              "properties": { "name": { "type": "string" } }
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "validation-value",
            CreateSchema(schemaText));
        await using var deserializer = new JsonSchemaRegistryDeserializer<NamePayload>(
            registry,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            },
            config: new SchemaRegistryDeserializerConfig { UseLatestVersion = true });

        var result = deserializer.Deserialize(
            CreateWirePayload(schemaId, """{"name":"ok"}"""),
            Context);

        await Assert.That(result.Name).IsEqualTo("ok");
        Assert.Throws<ValidationRulesFailedException>(() => deserializer.Deserialize(
            CreateWirePayload(schemaId, """{"name":"bad"}"""),
            Context));
    }

    [Test]
    public async Task Deserializer_LatestVersionMarkerValidatesWriterRulesBeforeDomainBoundary()
    {
        const string writerSchemaText = """
            {
              "confluent:rules": [{ "name": "writer", "expr": "this.id == 7" }]
            }
            """;
        const string readerSchemaText = """
            {
              "confluent:rules": [{ "name": "reader", "expr": "this.latest == 'ok'" }]
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        var writerSchemaId = await registry.RegisterSchemaAsync(
            "validation-value",
            CreateSchema(writerSchemaText));
        _ = await registry.RegisterSchemaAsync(
            "validation-value",
            CreateSchema(readerSchemaText));
        await using var deserializer = new JsonSchemaRegistryDeserializer<ValidationPayload>(
            registry,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            },
            config: new SchemaRegistryDeserializerConfig { UseLatestVersion = true });

        var result = deserializer.Deserialize(
            CreateWirePayload(writerSchemaId, """{"id":7}"""),
            Context);

        await Assert.That(result.Id).IsEqualTo(7);
        Assert.Throws<ValidationRulesFailedException>(() => deserializer.Deserialize(
            CreateWirePayload(writerSchemaId, """{"id":6}"""),
            Context));
    }

    [Test]
    public async Task Deserializer_LatestVersionMarkerValidatesWriterRulesAfterDomainBoundary()
    {
        const string writerSchemaText = """
            {
              "confluent:rules": [{ "name": "writer", "expr": "this.id == 7" }]
            }
            """;
        const string readerSchemaText = """
            {
              "confluent:rules": [{ "name": "reader", "expr": "this.latest == 'ok'" }]
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        var writerSchemaId = await registry.RegisterSchemaAsync(
            "validation-value",
            CreateSchema(writerSchemaText));
        _ = await registry.RegisterSchemaAsync(
            "validation-value",
            CreateSchema(readerSchemaText));
        await using var deserializer = new JsonSchemaRegistryDeserializer<ValidationPayload>(
            registry,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
            },
            config: new SchemaRegistryDeserializerConfig { UseLatestVersion = true });

        var result = deserializer.Deserialize(
            CreateWirePayload(writerSchemaId, """{"id":7}"""),
            Context);

        await Assert.That(result.Id).IsEqualTo(7);
        Assert.Throws<ValidationRulesFailedException>(() => deserializer.Deserialize(
            CreateWirePayload(writerSchemaId, """{"id":6}"""),
            Context));
    }

    [Test]
    public async Task Deserializer_LatestVersionValidatesTransformedReaderDomainPayload()
    {
        const string writerSchemaText = """
            {
              "confluent:rules": [{ "name": "writer", "expr": "this.id == 7" }]
            }
            """;
        const string readerSchemaText = """
            {
              "confluent:rules": [{ "name": "reader", "expr": "this.name == 'ok'" }]
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        var writerSchemaId = await registry.RegisterSchemaAsync(
            "validation-value",
            CreateSchema(writerSchemaText));
        _ = await registry.RegisterSchemaAsync(
            "validation-value",
            new Schema
            {
                SchemaType = SchemaType.Json,
                SchemaString = readerSchemaText,
                RuleSet = new SchemaRuleSet
                {
                    DomainRules = [CreateRule("domain", "DOMAIN", SchemaRuleMode.Read)]
                }
            });
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor([
            new ReplacingRuleHandler("DOMAIN", """{"name":"ok"}"""u8.ToArray(), calls)
        ]);
        await using var deserializer = new JsonSchemaRegistryDeserializer<NamePayload>(
            registry,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
            },
            config: new SchemaRegistryDeserializerConfig { UseLatestVersion = true },
            ruleExecutor: executor);

        var result = deserializer.Deserialize(
            CreateWirePayload(writerSchemaId, """{"id":7}"""),
            Context);

        await Assert.That(result.Name).IsEqualTo("ok");
        await Assert.That(calls).IsEquivalentTo(["domain"]);
    }

    [Test]
    public async Task Deserializer_LatestVersionRunsBeforeRulesAfterEncoding()
    {
        const string schemaText = """
            {
              "type": "object",
              "confluent:rules": [{ "name": "validName", "expr": "this.name == 'ok'" }],
              "properties": { "name": { "type": "string" } }
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "validation-value",
            new Schema
            {
                SchemaType = SchemaType.Json,
                SchemaString = schemaText,
                RuleSet = new SchemaRuleSet
                {
                    DomainRules = [CreateRule("domain", "DOMAIN", SchemaRuleMode.Read)],
                    EncodingRules = [CreateRule("encoding", "ENCODING", SchemaRuleMode.Read)]
                }
            });
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor([
            new ReplacingRuleHandler("ENCODING", """{"name":"ok"}"""u8.ToArray(), calls),
            new ReplacingRuleHandler("DOMAIN", """{"name":"ok"}"""u8.ToArray(), calls)
        ]);
        await using var deserializer = new JsonSchemaRegistryDeserializer<NamePayload>(
            registry,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            },
            config: new SchemaRegistryDeserializerConfig { UseLatestVersion = true },
            ruleExecutor: executor);

        var result = deserializer.Deserialize(
            CreateWirePayload(schemaId, "encoded"),
            Context);

        await Assert.That(result.Name).IsEqualTo("ok");
        await Assert.That(calls).IsEquivalentTo(["encoding", "domain"]);

        calls.Clear();
        var invalidExecutor = new SchemaRegistryRuleExecutor([
            new ReplacingRuleHandler("ENCODING", """{"name":"bad"}"""u8.ToArray(), calls),
            new ReplacingRuleHandler("DOMAIN", """{"name":"ok"}"""u8.ToArray(), calls)
        ]);
        await using var invalidDeserializer = new JsonSchemaRegistryDeserializer<NamePayload>(
            registry,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.None,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            },
            config: new SchemaRegistryDeserializerConfig { UseLatestVersion = true },
            ruleExecutor: invalidExecutor);

        Assert.Throws<ValidationRulesFailedException>(() => invalidDeserializer.Deserialize(
            CreateWirePayload(schemaId, "encoded"),
            Context));
        await Assert.That(calls).IsEquivalentTo(["encoding"]);
    }

    [Test]
    public async Task InlineRules_ResolveSchemaRegistryReferences()
    {
        using var registry = new MockSchemaRegistryClient();
        await registry.RegisterSchemaAsync(
            "inline-address-value",
            CreateSchema("""
                {
                  "$id": "https://example.test/address.json",
                  "type": "object",
                  "properties": {
                    "postcode": {
                      "type": "string",
                      "confluent:rules": [{ "name": "postcode", "expr": "size(this) > 0" }]
                    }
                  }
                }
                """));
        var root = CreateSchema(
            """
            {
              "$id": "https://example.test/root.json",
              "type": "object",
              "properties": { "address": { "$ref": "address.json" } }
            }
            """,
            [new SchemaReference
            {
                Name = "address.json",
                Subject = "inline-address-value",
                Version = 1
            }]);
        var validator = new StreamingJsonSchemaValidatorFactory(registry).GetOrCreate(root);

        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            """{"address":{"postcode":""}}"""u8.ToArray(),
            20,
            failFast: false));

        await Assert.That(exception.Message).Contains("$.address.postcode: postcode");
    }

    [Test]
    public async Task Serializer_InlineRulesAreDisabledByDefault()
    {
        const string schemaText = """
            { "confluent:rules": [{ "name": "unsupported", "expr": "this.all(x, x > 0)" }] }
            """;
        using var registry = new MockSchemaRegistryClient();
        var options = new JsonSchemaValidationOptions
        {
            ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
            Mode = JsonSchemaValidationMode.None
        };
        await using var serializer = new JsonSchemaRegistrySerializer<NamePayload>(
            registry,
            schemaText,
            jsonOptions: null,
            validationOptions: options);
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(new NamePayload("anything"), ref buffer, Context);
    }

    [Test]
    public async Task InlineRules_ReportCachedCompilationErrorsAsViolations()
    {
        const string schemaText = """
            { "confluent:rules": [{ "name": "unsupported", "expr": "this.all(x, x > 0)" }] }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            "[]"u8.ToArray(),
            24,
            failFast: false));

        await Assert.That(exception.Violations.Count).IsEqualTo(1);
        await Assert.That(exception.Violations[0].Cause).IsTypeOf<SchemaRegistryRuleException>();
        await Assert.That(exception.Violations[0].Cause!.Message).Contains(
            "Could not compile validation rule 'unsupported'");
    }

    [Test]
    public async Task InlineRules_RejectMethodCallsOnIdentifiersThatOnlyEndWithThisMember()
    {
        const string schemaText = """
            { "confluent:rules": [{ "name": "invalid", "expr": "thisx.startsWith('x')" }] }
            """;
        var validator = CreateFactory().GetOrCreate(CreateSchema(schemaText));

        var exception = Assert.Throws<ValidationRulesFailedException>(() => validator.ValidateRules(
            "\"x\""u8.ToArray(),
            24,
            failFast: false));

        await Assert.That(exception.Violations[0].Cause!.Message).Contains(
            "Unsupported CEL function 'thisx.startsWith'");
    }

    [Test]
    public async Task Deserializer_IncompleteMigrationValidatesAgainstPayloadSchema()
    {
        const string writerSchemaText = """
            { "type": "object", "properties": { "id": { "type": "integer" } }, "required": ["id"] }
            """;
        const string readerSchemaText = """
            { "type": "object", "properties": { "latest": { "type": "string" } }, "required": ["latest"] }
            """;
        using var registry = new MockSchemaRegistryClient();
        var writerSchemaId = await registry.RegisterSchemaAsync(
            "validation-value",
            CreateSchema(writerSchemaText));
        await registry.RegisterSchemaAsync(
            "validation-value",
            new Schema
            {
                SchemaType = SchemaType.Json,
                SchemaString = readerSchemaText,
                RuleSet = new SchemaRuleSet
                {
                    MigrationRules =
                    [
                        new SchemaRule
                        {
                            Name = "unavailable",
                            Type = "MISSING",
                            Kind = SchemaRuleKind.Transform,
                            Mode = SchemaRuleMode.Upgrade,
                            OnFailure = "NONE"
                        }
                    ]
                }
            });
        await using var deserializer = new JsonSchemaRegistryDeserializer<ValidationPayload>(
            registry,
            jsonOptions: null,
            validationOptions: new JsonSchemaValidationOptions
            {
                ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
                Mode = JsonSchemaValidationMode.Deserialize
            },
            config: new SchemaRegistryDeserializerConfig { UseLatestVersion = true },
            ruleExecutor: new SchemaRegistryRuleExecutor([]));

        var result = deserializer.Deserialize(
            CreateWirePayload(writerSchemaId, """{"id":7}"""),
            Context);

        await Assert.That(result.Id).IsEqualTo(7);
    }

    [Test]
    public async Task MigrationRunner_LegacyTransformationValidatesAndReturnsReaderSchema()
    {
        const string writerSchemaText = """
            {
              "confluent:rules": [{ "name": "writer", "expr": "this.id == 7" }]
            }
            """;
        const string readerSchemaText = """
            {
              "confluent:rules": [{ "name": "reader", "expr": "this.latest == 'ok'" }]
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        var writerSchema = CreateSchema(writerSchemaText);
        var writerSchemaId = await registry.RegisterSchemaAsync("validation-value", writerSchema);
        var readerSchema = CreateSchema(readerSchemaText);
        var readerSchemaId = await registry.RegisterSchemaAsync("validation-value", readerSchema);
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new ReplacingLegacyRuleExecutor("""{"latest":"ok"}"""u8.ToArray()),
            TimeSpan.FromSeconds(1));

        var result = runner.TransformWithBeforeDomainValidation(
            """{"id":7}"""u8.ToArray(),
            writerSchemaId,
            "validation-value",
            writerSchema,
            Context,
            SchemaRegistryPayloadFormat.Json,
            new StreamingJsonSchemaValidatorFactory(registry),
            validationRulesFailFast: false);

        await Assert.That(result.PayloadSchemaId).IsEqualTo(readerSchemaId);
        await Assert.That(result.PayloadSchema).IsSameReferenceAs(readerSchema);
        await Assert.That(Encoding.UTF8.GetString(result.Payload.Span)).IsEqualTo("""{"latest":"ok"}""");
    }

    [Test]
    [Arguments("ok", false)]
    [Arguments("bad", true)]
    public async Task MigrationRunner_ValidatesTransformedPayloadAgainstReaderRules(
        string latest,
        bool shouldFail)
    {
        const string writerSchemaText = """
            {
              "confluent:rules": [{ "name": "writer", "expr": "this.id == 7" }]
            }
            """;
        const string readerSchemaText = """
            {
              "confluent:rules": [{ "name": "reader", "expr": "this.latest == 'ok'" }]
            }
            """;
        using var registry = new MockSchemaRegistryClient();
        var writerSchema = CreateSchema(writerSchemaText);
        var writerSchemaId = await registry.RegisterSchemaAsync("validation-value", writerSchema);
        var readerSchema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = readerSchemaText,
            RuleSet = new SchemaRuleSet
            {
                MigrationRules =
                [
                    new SchemaRule
                    {
                        Name = "upgrade",
                        Type = "MIGRATE",
                        Kind = SchemaRuleKind.Transform,
                        Mode = SchemaRuleMode.Upgrade
                    }
                ]
            }
        };
        _ = await registry.RegisterSchemaAsync("validation-value", readerSchema);
        var calls = new List<string>();
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new SchemaRegistryRuleExecutor([
                new ReplacingRuleHandler(
                    "MIGRATE",
                    Encoding.UTF8.GetBytes($$"""{"latest":"{{latest}}"}"""),
                    calls)
            ]),
            TimeSpan.FromSeconds(1));

        void Transform() => runner.TransformWithBeforeDomainValidation(
                """{"id":7}"""u8.ToArray(),
                writerSchemaId,
                "validation-value",
                writerSchema,
                Context,
                SchemaRegistryPayloadFormat.Json,
                new StreamingJsonSchemaValidatorFactory(registry),
                validationRulesFailFast: false);

        if (shouldFail)
            Assert.Throws<ValidationRulesFailedException>(Transform);
        else
            Transform();
        await Assert.That(calls).IsEquivalentTo(["upgrade"]);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task MigrationRunner_NoOpReaderDomainTransformRetainsWriterPayloadSchema(
        bool validateBeforeDomain,
        bool copyPayload)
    {
        const string writerSchemaText = """{ "required": ["id"] }""";
        const string readerSchemaText = """{ "required": ["latest"] }""";
        using var registry = new MockSchemaRegistryClient();
        var writerSchema = CreateSchema(writerSchemaText);
        var writerSchemaId = await registry.RegisterSchemaAsync("validation-value", writerSchema);
        var readerSchema = new Schema
        {
            SchemaType = SchemaType.Json,
            SchemaString = readerSchemaText,
            RuleSet = new SchemaRuleSet
            {
                DomainRules = [CreateRule("domain", "PASSTHROUGH", SchemaRuleMode.Read)]
            }
        };
        _ = await registry.RegisterSchemaAsync("validation-value", readerSchema);
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new SchemaRegistryRuleExecutor([new PassThroughRuleHandler("PASSTHROUGH", copyPayload)]),
            TimeSpan.FromSeconds(1));
        var payload = """{"id":7}"""u8.ToArray();

        var result = validateBeforeDomain
            ? runner.TransformWithBeforeDomainValidation(
                payload,
                writerSchemaId,
                "validation-value",
                writerSchema,
                Context,
                SchemaRegistryPayloadFormat.Json,
                new StreamingJsonSchemaValidatorFactory(registry),
                validationRulesFailFast: false)
            : runner.Transform(
                payload,
                writerSchemaId,
                "validation-value",
                writerSchema,
                Context,
                SchemaRegistryPayloadFormat.Json);

        await Assert.That(result.PayloadSchemaId).IsEqualTo(writerSchemaId);
        await Assert.That(result.PayloadSchema).IsSameReferenceAs(writerSchema);
        await Assert.That(result.Payload).IsEquivalentTo(payload);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task MigrationRunner_CustomNoOpTransformRetainsWriterPayloadSchema(
        bool validateBeforeDomain,
        bool copyPayload)
    {
        const string writerSchemaText = """{ "required": ["id"] }""";
        const string readerSchemaText = """{ "required": ["latest"] }""";
        using var registry = new MockSchemaRegistryClient();
        var writerSchema = CreateSchema(writerSchemaText);
        var writerSchemaId = await registry.RegisterSchemaAsync("validation-value", writerSchema);
        _ = await registry.RegisterSchemaAsync("validation-value", CreateSchema(readerSchemaText));
        var runner = new SchemaRegistryMigrationRunner(
            registry,
            new PassThroughLegacyRuleExecutor(copyPayload),
            TimeSpan.FromSeconds(1));
        var payload = """{"id":7}"""u8.ToArray();

        var result = validateBeforeDomain
            ? runner.TransformWithBeforeDomainValidation(
                payload,
                writerSchemaId,
                "validation-value",
                writerSchema,
                Context,
                SchemaRegistryPayloadFormat.Json,
                new StreamingJsonSchemaValidatorFactory(registry),
                validationRulesFailFast: false)
            : runner.Transform(
                payload,
                writerSchemaId,
                "validation-value",
                writerSchema,
                Context,
                SchemaRegistryPayloadFormat.Json);

        await Assert.That(result.PayloadSchemaId).IsEqualTo(writerSchemaId);
        await Assert.That(result.PayloadSchema).IsSameReferenceAs(writerSchema);
        await Assert.That(result.Payload).IsEquivalentTo(payload);
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

    private static SchemaRule CreateRule(
        string name,
        string type,
        SchemaRuleMode mode = SchemaRuleMode.Write) =>
        new()
        {
            Name = name,
            Type = type,
            Kind = SchemaRuleKind.Transform,
            Mode = mode
        };

    private static byte[] CreateDeepEqualityPayload(int depth, int rightLeaf)
    {
        var json = new StringBuilder(depth * 48);
        json.Append("{\"left\":");
        for (var index = 0; index < depth; index++)
            json.Append("{\"value\":");
        json.Append('1');
        for (var index = 0; index < depth; index++)
            json.Append(",\"tag\":1}");

        json.Append(",\"right\":");
        for (var index = 0; index < depth; index++)
            json.Append("{\"tag\":1,\"value\":");
        json.Append(rightLeaf);
        json.Append('}', depth);
        json.Append('}');
        return Encoding.UTF8.GetBytes(json.ToString());
    }

    private static (string Schema, byte[] Payload) CreateManyDuplicateRuleCase(
        int propertyCount,
        bool declaredProperties)
    {
        var schema = new StringBuilder(propertyCount * 96);
        var payload = new StringBuilder(propertyCount * 20);
        schema.Append(declaredProperties
            ? "{\"properties\":{"
            : "{\"additionalProperties\":{\"confluent:rules\":[{\"name\":\"value\",\"expr\":\"this == 1\"}]}}");
        payload.Append('{');
        for (var index = 0; index < propertyCount; index++)
        {
            if (index != 0)
            {
                if (declaredProperties)
                    schema.Append(',');
                payload.Append(',');
            }
            if (declaredProperties)
            {
                schema.Append("\"p").Append(index)
                    .Append("\":{\"confluent:rules\":[{\"name\":\"p").Append(index)
                    .Append("\",\"expr\":\"this == 1\"}]}");
            }
            payload.Append("\"p").Append(index).Append("\":0");
        }
        if (declaredProperties)
            schema.Append("}}");
        for (var index = 0; index < propertyCount; index++)
            payload.Append(",\"p").Append(index).Append("\":1");
        payload.Append('}');
        return (schema.ToString(), Encoding.UTF8.GetBytes(payload.ToString()));
    }

    private static (string Schema, byte[] Payload) CreateDeepMemberRule(int depth)
    {
        var path = new StringBuilder("this");
        var payload = new StringBuilder(depth * 12);
        for (var index = 0; index < depth; index++)
        {
            path.Append(".child");
            payload.Append("{\"child\":");
        }
        path.Append(".value");
        payload.Append("{\"value\":1}");
        payload.Append('}', depth);
        var schema = $$"""
            {
              "confluent:rules": [{ "name": "deep", "expr": "{{path}} == 1" }]
            }
            """;
        return (schema, Encoding.UTF8.GetBytes(payload.ToString()));
    }

    private static (string Schema, byte[] Payload) CreateAggregateCompositionGrowthCase(int branchCount)
    {
        var schema = new StringBuilder(branchCount * 112);
        schema.Append("{\"properties\":{\"value\":{\"anyOf\":[");
        for (var index = 0; index < branchCount; index++)
        {
            if (index != 0)
                schema.Append(',');
            schema.Append(
                "{\"type\":\"integer\",\"confluent:rules\":[{\"name\":\"positive\",\"expr\":\"this > 0\"}]}");
        }
        schema.Append("]}}}");
        return (schema.ToString(), """{"value":-1,"value":1}"""u8.ToArray());
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
    private sealed record NamePayload(string Name);
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

    private sealed class PassThroughRuleHandler(string type, bool copyPayload = false) :
        ISchemaRegistryRuleTransformResultHandler
    {
        public string Type => type;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => copyPayload ? payload.ToArray() : payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context) => copyPayload ? payload.ToArray() : payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context,
            out bool payloadChanged)
        {
            payloadChanged = false;
            return copyPayload ? payload.ToArray() : payload;
        }
    }

    private sealed class PassThroughLegacyRuleExecutor(bool copyPayload) : ISchemaRegistryRuleExecutor
    {
        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => copyPayload ? payload.ToArray() : payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => copyPayload ? payload.ToArray() : payload;
    }

    private sealed class ReplacingLegacyRuleExecutor(ReadOnlyMemory<byte> replacement)
        : ISchemaRegistryRuleExecutor
    {
        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => replacement;
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
            SchemaRegistryRuleHandlerContext context)
        {
            calls.Add(context.Rule.Name);
            return replacement;
        }
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
