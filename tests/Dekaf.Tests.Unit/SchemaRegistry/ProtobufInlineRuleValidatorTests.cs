using System.Buffers;
using System.Buffers.Binary;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Dekaf.Tests.Unit.SchemaRegistry.ProtobufFixtures;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class ProtobufInlineRuleValidatorTests
{
    [Test]
    public async Task Validate_ValidNestedPayload_Succeeds()
    {
        var message = CreateValidMessage();
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);

        validator.Validate(message.ToByteArray(), schemaId: 17, failFast: false);

        await Assert.That(message.HasNickname).IsFalse();
    }

    [Test]
    public async Task Validate_UnsetOneofAndProto2Optional_SkipsPresenceRules()
    {
        var message = CreateValidMessage();
        message.ClearContact();
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);
        var proto2 = new Proto2ValidationMessage();
        var proto2Validator = new ProtobufInlineRuleValidator(Proto2ValidationMessage.Descriptor);

        validator.Validate(message.ToByteArray(), schemaId: 17, failFast: false);
        proto2Validator.Validate(proto2.ToByteArray(), schemaId: 18, failFast: false);

        proto2.Value = 0;
        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            proto2Validator.Validate(proto2.ToByteArray(), schemaId: 18, failFast: false));
        await Assert.That(exception.Violations[0].Rule.Name).IsEqualTo("positive-when-present");
        await Assert.That(exception.Violations[0].FieldPath).IsEqualTo("value");
    }

    [Test]
    public async Task Validate_InvalidPayload_AggregatesPrecisePaths()
    {
        var message = CreateValidMessage();
        message.Age = 151;
        message.Name = string.Empty;
        message.Tags.Add("third");
        message.Children[0].Value = 0;
        message.ChildrenByName["primary"].Value = -1;
        message.ChildrenByName.Add("secondary", new ValidationChild { Value = 1 });
        message.Token = ByteString.CopyFrom([1, 2]);
        message.CreatedAt = Timestamp.FromDateTime(
            new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        message.Score = -1;
        message.Codes.Add(4);
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);

        var exception = (await Assert.That(() =>
                validator.Validate(message.ToByteArray(), schemaId: 17, failFast: false))
            .Throws<ValidationRulesFailedException>())!;

        var failures = exception.Violations
            .Select(static violation => (violation.Rule.Name, violation.FieldPath))
            .ToHashSet();
        await Assert.That(failures).Contains(("age-upper-bound", ""));
        await Assert.That(failures).Contains(("name-required", "name"));
        await Assert.That(failures).Contains(("tag-limit", "tags"));
        await Assert.That(failures).Contains(("positive-child-value", "children[0]"));
        await Assert.That(failures).Contains(("positive-child-value", "children_by_name[\"primary\"]"));
        await Assert.That(failures).Contains(("child-map-size", "children_by_name"));
        await Assert.That(failures).Contains(("token-size", "token"));
        await Assert.That(failures).Contains(("token-value", "token"));
        await Assert.That(failures).Contains(("modern-date", "created_at"));
        await Assert.That(failures).Contains(("score-not-negative", "score"));
        await Assert.That(failures).Contains(("packed-code-count", "codes"));
    }

    [Test]
    public async Task Validate_FailFast_StopsAtMessageRule()
    {
        var message = CreateValidMessage();
        message.Age = 151;
        message.Name = string.Empty;
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);

        var exception = (await Assert.That(() =>
                validator.Validate(message.ToByteArray(), schemaId: 17, failFast: true))
            .Throws<ValidationRulesFailedException>())!;

        await Assert.That(exception.Violations).HasSingleItem();
        await Assert.That(exception.Violations[0].Rule.Name).IsEqualTo("age-upper-bound");
        await Assert.That(exception.Violations[0].FieldPath).IsEmpty();
    }

    [Test]
    public async Task Validate_MapMessageRule_EscapesStringKeyPath()
    {
        var message = CreateValidMessage();
        message.ChildrenByName.Clear();
        message.ChildrenByName.Add("quoted\"\\key", new ValidationChild { Value = 0 });
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);

        var exception = (await Assert.That(() =>
                validator.Validate(message.ToByteArray(), schemaId: 17, failFast: false))
            .Throws<ValidationRulesFailedException>())!;

        await Assert.That(exception.Violations).HasSingleItem();
        await Assert.That(exception.Violations[0].FieldPath)
            .IsEqualTo("children_by_name[\"quoted\\\"\\\\key\"]");
    }

    [Test]
    public async Task Validate_WarmedValidPayload_AllocatesZeroBytes()
    {
        var payload = CreateValidMessage().ToByteArray();
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);
        validator.Validate(payload, schemaId: 17, failFast: false);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload, schemaId: 17, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_RegisteredDescriptorWithReferences_CompilesOnceAndAllocatesZeroBytes()
    {
        var descriptor = FileDescriptorProto.Parser.ParseFrom(
            ValidationEnvelope.Descriptor.File.SerializedData);
        descriptor.SourceCodeInfo = new SourceCodeInfo();
        var schema = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = descriptor.ToByteString().ToBase64()
        };
        IInlineValidationRuleExecutor validator =
            new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);
        var payload = CreateValidMessage().ToByteArray();
        validator.Validate(payload, schemaId: 19, schema, failFast: false);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload, schemaId: 19, schema, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task Serializer_EnabledValidation_RejectsInvalidMessage()
    {
        var registry = new MockSchemaRegistryClient();
        var config = new ProtobufSerializerConfig
        {
            UseSchemaReferences = false,
            ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
        };
        await using var serializer = new ProtobufSchemaRegistrySerializer<ValidationEnvelope>(registry, config);
        var message = CreateValidMessage();
        message.Age = 151;
        var destination = new ArrayBufferWriter<byte>();

        var exception = (await Assert.That(() => serializer.Serialize(
                message,
                ref destination,
                CreateContext()))
            .Throws<ValidationRulesFailedException>())!;

        await Assert.That(exception.Violations[0].Rule.Name).IsEqualTo("age-upper-bound");
        await Assert.That(destination.WrittenCount).IsEqualTo(0);
    }

    [Test]
    public async Task Deserializer_EnabledValidation_RejectsInvalidPayload()
    {
        var config = new ProtobufDeserializerConfig
        {
            SkipSchemaValidation = true,
            ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
        };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<ValidationEnvelope>(
            new MockSchemaRegistryClient(),
            config);
        var message = CreateValidMessage();
        message.Name = string.Empty;

        var exception = (await Assert.That(() => deserializer.Deserialize(
                CreateWireBytes(schemaId: 17, message),
                CreateContext()))
            .Throws<ValidationRulesFailedException>())!;

        await Assert.That(exception.Violations.Select(static error => error.Rule.Name))
            .Contains("name-required");
    }

    [Test]
    public async Task Deserializer_DisabledValidation_PreservesExistingBehavior()
    {
        var config = new ProtobufDeserializerConfig { SkipSchemaValidation = true };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<ValidationEnvelope>(
            new MockSchemaRegistryClient(),
            config);
        var message = CreateValidMessage();
        message.Name = string.Empty;

        var result = deserializer.Deserialize(CreateWireBytes(schemaId: 17, message), CreateContext());

        await Assert.That(result.Name).IsEmpty();
    }

    [Test]
    public async Task Serializer_InvalidValidationMode_IsRejected()
    {
        var config = new ProtobufSerializerConfig
        {
            ValidationRulesExecution = (ValidationRulesExecution)int.MaxValue
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new ProtobufSchemaRegistrySerializer<ValidationEnvelope>(
                new MockSchemaRegistryClient(),
                config));

        await Assert.That(exception.ParamName).IsEqualTo("execution");
    }

    [Test]
    public async Task Serializer_CustomRuleExecutorWithInlineValidation_IsRejected()
    {
        var config = new ProtobufSerializerConfig
        {
            RuleExecutor = new PassThroughRuleExecutor(),
            ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
        };

        var exception = Assert.Throws<NotSupportedException>(() =>
            _ = new ProtobufSchemaRegistrySerializer<ValidationEnvelope>(
                new MockSchemaRegistryClient(),
                config));

        await Assert.That(exception.Message).Contains("SchemaRegistryRuleExecutor");
    }

    [Test]
    public async Task Serializer_InlineRulesRunBetweenDomainAndEncodingRules()
    {
        using var registry = new MockSchemaRegistryClient();
        var valid = CreateValidMessage();
        var calls = new List<string>();
        var schemaId = await RegisterValidationSchema(registry, SchemaRuleMode.Write);
        var executor = CreateReplacingExecutor(calls, valid.ToByteArray(), "encoded"u8.ToArray());
        var config = new ProtobufSerializerConfig
        {
            AutoRegisterSchemas = false,
            UseSchemaReferences = false,
            RuleExecutor = executor,
            ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
        };
        await using var serializer = new ProtobufSchemaRegistrySerializer<ValidationEnvelope>(registry, config);
        var invalid = CreateValidMessage();
        invalid.Age = 151;
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(invalid, ref destination, CreateContext());

        await Assert.That(schemaId).IsEqualTo(BinaryPrimitives.ReadInt32BigEndian(destination.WrittenSpan[1..5]));
        await Assert.That(calls).IsEquivalentTo(["domain", "encoding"]);
        await Assert.That(destination.WrittenSpan[6..].ToArray()).IsEquivalentTo("encoded"u8.ToArray());

        calls.Clear();
        await using var beforeSerializer = new ProtobufSchemaRegistrySerializer<ValidationEnvelope>(
            registry,
            new ProtobufSerializerConfig
            {
                AutoRegisterSchemas = false,
                UseSchemaReferences = false,
                RuleExecutor = executor,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        var beforeDestination = new ArrayBufferWriter<byte>();
        Assert.Throws<ValidationRulesFailedException>(
            () => beforeSerializer.Serialize(invalid, ref beforeDestination, CreateContext()));
        await Assert.That(calls).IsEmpty();
    }

    [Test]
    public async Task Deserializer_InlineRulesRunAfterEncodingAtConfiguredDomainBoundary()
    {
        using var registry = new MockSchemaRegistryClient();
        var valid = CreateValidMessage();
        var invalid = CreateValidMessage();
        invalid.Age = 151;
        var calls = new List<string>();
        var schemaId = await RegisterValidationSchema(registry, SchemaRuleMode.Read);
        var executor = CreateReplacingExecutor(calls, valid.ToByteArray(), invalid.ToByteArray());
        var config = new ProtobufDeserializerConfig
        {
            RuleExecutor = executor,
            ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
        };
        await using var deserializer = new ProtobufSchemaRegistryDeserializer<ValidationEnvelope>(registry, config);

        var result = deserializer.Deserialize(CreateWireBytes(schemaId, "encoded"u8.ToArray()), CreateContext());

        await Assert.That(result.Age).IsEqualTo(valid.Age);
        await Assert.That(calls).IsEquivalentTo(["encoding", "domain"]);

        calls.Clear();
        await using var beforeDeserializer = new ProtobufSchemaRegistryDeserializer<ValidationEnvelope>(
            registry,
            new ProtobufDeserializerConfig
            {
                RuleExecutor = executor,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        Assert.Throws<ValidationRulesFailedException>(() => beforeDeserializer.Deserialize(
            CreateWireBytes(schemaId, "encoded"u8.ToArray()),
            CreateContext()));
        await Assert.That(calls).IsEquivalentTo(["encoding"]);
    }

    [Test]
    public async Task Deserializer_InlineRulesHonorMigrationBoundary()
    {
        using var registry = new MockSchemaRegistryClient();
        var schemaString = ValidationEnvelope.Descriptor.File.SerializedData.ToBase64();
        var writerSchemaId = await registry.RegisterSchemaAsync(
            "validation-topic-value",
            new Schema { SchemaType = SchemaType.Protobuf, SchemaString = schemaString });
        var valid = CreateValidMessage();
        var calls = new List<string>();
        _ = await registry.RegisterSchemaAsync(
            "validation-topic-value",
            new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = schemaString,
                RuleSet = new SchemaRuleSet
                {
                    MigrationRules =
                    [
                        new SchemaRule
                        {
                            Name = "upgrade",
                            Type = "MIGRATION",
                            Kind = SchemaRuleKind.Transform,
                            Mode = SchemaRuleMode.Upgrade
                        }
                    ]
                }
            });
        var executor = new SchemaRegistryRuleExecutor(
            [new ReplacingRuleHandler("MIGRATION", valid.ToByteArray(), calls)]);
        var invalid = CreateValidMessage();
        invalid.Age = 151;
        var wireBytes = CreateWireBytes(writerSchemaId, invalid);
        await using var afterDeserializer = new ProtobufSchemaRegistryDeserializer<ValidationEnvelope>(
            registry,
            new ProtobufDeserializerConfig
            {
                UseLatestVersion = true,
                RuleExecutor = executor,
                ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
            });

        var result = afterDeserializer.Deserialize(wireBytes, CreateContext());

        await Assert.That(result.Age).IsEqualTo(valid.Age);
        await Assert.That(calls).IsEquivalentTo(["upgrade"]);

        calls.Clear();
        await using var beforeDeserializer = new ProtobufSchemaRegistryDeserializer<ValidationEnvelope>(
            registry,
            new ProtobufDeserializerConfig
            {
                UseLatestVersion = true,
                RuleExecutor = executor,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        Assert.Throws<ValidationRulesFailedException>(() =>
            beforeDeserializer.Deserialize(wireBytes, CreateContext()));
        await Assert.That(calls).IsEmpty();
    }

    private static SerializationContext CreateContext() => new()
    {
        Topic = "validation-topic",
        Component = SerializationComponent.Value
    };

    private static byte[] CreateWireBytes(int schemaId, ValidationEnvelope message)
        => CreateWireBytes(schemaId, message.ToByteArray());

    private static byte[] CreateWireBytes(int schemaId, ReadOnlySpan<byte> payload)
    {
        var wireBytes = new byte[6 + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(wireBytes.AsSpan(1, 4), schemaId);
        wireBytes[5] = 0;
        payload.CopyTo(wireBytes.AsSpan(6));
        return wireBytes;
    }

    private static async Task<int> RegisterValidationSchema(
        MockSchemaRegistryClient registry,
        SchemaRuleMode mode) =>
        await registry.RegisterSchemaAsync(
            "validation-topic-value",
            new Schema
            {
                SchemaType = SchemaType.Protobuf,
                SchemaString = ValidationEnvelope.Descriptor.File.SerializedData.ToBase64(),
                RuleSet = new SchemaRuleSet
                {
                    DomainRules = [CreateRule("domain", "DOMAIN", mode)],
                    EncodingRules = [CreateRule("encoding", "ENCODING", mode)],
                    HasFixedRuleCollections = true
                }
            });

    private static SchemaRegistryRuleExecutor CreateReplacingExecutor(
        List<string> calls,
        ReadOnlyMemory<byte> domainReplacement,
        ReadOnlyMemory<byte> encodingReplacement) =>
        new([
            new ReplacingRuleHandler("DOMAIN", domainReplacement, calls),
            new ReplacingRuleHandler("ENCODING", encodingReplacement, calls)
        ]);

    private static SchemaRule CreateRule(string name, string type, SchemaRuleMode mode) => new()
    {
        Name = name,
        Type = type,
        Kind = SchemaRuleKind.Transform,
        Mode = mode
    };

    private static ValidationEnvelope CreateValidMessage()
    {
        var message = new ValidationEnvelope
        {
            Age = 42,
            Name = "Dekaf",
            Email = "test@example.com",
            Token = ByteString.CopyFromUtf8("abc"),
            Status = ValidationStatus.Active,
            CreatedAt = Timestamp.FromDateTime(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            Score = 7
        };
        message.Tags.Add("fast");
        message.Tags.Add("native");
        message.Children.Add(new ValidationChild { Value = 1 });
        message.ChildrenByName.Add("primary", new ValidationChild { Value = 2 });
        message.Codes.Add(1);
        message.Codes.Add(2);
        message.Codes.Add(3);
        return message;
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

    private sealed class PassThroughRuleExecutor : ISchemaRegistryRuleExecutor
    {
        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;
    }
}
