using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Dekaf.Tests.Unit.SchemaRegistry.ProtobufFixtures;
using Dekaf.Tests.Unit.SchemaRegistry.ProtobufFixtures.Confluent;
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
    public async Task Validate_KnownFieldWithWrongWireTypeRemainsUnknown()
    {
        var payload = new byte[] { 10, 1, 0 };
        var validator = new ProtobufInlineRuleValidator(Proto2ValidationMessage.Descriptor);

        validator.Validate(payload, schemaId: 18, failFast: false);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Validate_AbsentPresenceAwareScalarsUseDefaults()
    {
        new ProtobufInlineRuleValidator(ValidationPresenceEnvelope.Descriptor).Validate(
            ReadOnlyMemory<byte>.Empty,
            schemaId: 17,
            failFast: false);
        new ProtobufInlineRuleValidator(Proto2PresenceValidationMessage.Descriptor).Validate(
            ReadOnlyMemory<byte>.Empty,
            schemaId: 18,
            failFast: false);

        await Task.CompletedTask;
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
    public async Task Validate_FloatingArithmeticPreservesFloatingOperands()
    {
        var result = EvaluateTypedRule(
            "this + 1.0 > 0 && this - 1.0 < 0",
            ValidationCelValue.FromFloating(0.5d));

        await Assert.That(result.Boolean).IsTrue();
    }

    [Test]
    public async Task Validate_NaNComparisonsAreAlwaysFalse()
    {
        var result = EvaluateTypedRule(
            "this != this && !(this < 0) && !(this <= 0) && !(this > 0) && !(this >= 0)",
            ValidationCelValue.FromFloating(double.NaN));

        await Assert.That(result.Boolean).IsTrue();
    }

    [Test]
    public async Task Validate_BytesLiteralDecodesHexEscapes()
    {
        var result = EvaluateTypedRule(
            "this == b'\\xff\\x00'",
            ValidationCelValue.FromBytes(new byte[] { 0xff, 0x00 }));

        await Assert.That(result.Boolean).IsTrue();
    }

    [Test]
    public async Task Validate_MixedFloatingComparisonsPreserveExactIntegerBounds()
    {
        var aboveTwoToThe53 = EvaluateTypedRule(
            "9007199254740993 > 9007199254740992.0",
            ValidationCelValue.FromNumber(0));
        var belowTwoToThe64 = EvaluateTypedRule(
            "18446744073709551615 < 18446744073709551616.0",
            ValidationCelValue.FromNumber(0));
        var exactAndSubnormal = EvaluateTypedRule(
            "9007199254740992 == 9007199254740992.0 && 0 < 5e-324 && -5e-324 < 0",
            ValidationCelValue.FromNumber(0));

        await Assert.That(aboveTwoToThe53.Boolean).IsTrue();
        await Assert.That(belowTwoToThe64.Boolean).IsTrue();
        await Assert.That(exactAndSubnormal.Boolean).IsTrue();
    }

    [Test]
    public async Task Validate_ProtobufMessageEqualityUsesWirePayload()
    {
        var payload = new byte[] { 8, 1 };
        var result = EvaluateTypedRule(
            "this == this",
            new ValidationCelValue(
                ValidationCelValueKind.Object,
                default,
                false,
                0,
                null,
                payload));

        await Assert.That(result.Boolean).IsTrue();
    }

    [Test]
    public async Task Validate_ProtobufMergeAndOneofSemantics_AreAppliedBeforeRules()
    {
        var valid = CreateValidMessage();
        valid.ChildrenByName.Clear();
        var payload = new ArrayBufferWriter<byte>();
        valid.WriteTo(payload);
        WriteLengthDelimited(payload, fieldNumber: 14, [8, 1]);
        WriteLengthDelimited(payload, fieldNumber: 14, []);
        WriteLengthDelimited(payload, fieldNumber: 7, []);
        WriteLengthDelimited(payload, fieldNumber: 8, "active"u8);
        WriteLengthDelimited(payload, fieldNumber: 6, CreateMapEntry("duplicate", [8, 0]));
        WriteLengthDelimited(payload, fieldNumber: 6, CreateMapEntry("duplicate", [8, 1]));
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);

        validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);

        await Assert.That(payload.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Validate_MessageEqualityUsesDecodedValues()
    {
        var left = new ArrayBufferWriter<byte>();
        WriteVarint(left, 8);
        WriteVarint(left, 1);
        WriteLengthDelimited(left, fieldNumber: 2, [8, 2]);
        WriteLengthDelimited(left, fieldNumber: 3, [1, 2]);
        var right = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(right, fieldNumber: 2, [8, 2]);
        WriteVarint(right, 8);
        WriteVarint(right, 0);
        WriteVarint(right, 8);
        WriteVarint(right, 1);
        WriteVarint(right, 3u << 3);
        WriteVarint(right, 1);
        WriteVarint(right, 3u << 3);
        WriteVarint(right, 2);
        var payload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(payload, fieldNumber: 1, left.WrittenSpan);
        WriteLengthDelimited(payload, fieldNumber: 2, right.WrittenSpan);
        var validator = new ProtobufInlineRuleValidator(ValidationMessageEqualityEnvelope.Descriptor);

        validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(payload.WrittenCount).IsGreaterThan(0);
        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_MessageEqualityUsesMapSemantics()
    {
        var left = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(left, fieldNumber: 1, CreateInt32MapEntry("a", 0));
        WriteLengthDelimited(left, fieldNumber: 1, CreateInt32MapEntry("b", 2));
        WriteLengthDelimited(left, fieldNumber: 1, CreateInt32MapEntry("a", 1));
        var right = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(right, fieldNumber: 1, CreateInt32MapEntry("a", 1));
        WriteLengthDelimited(right, fieldNumber: 1, CreateInt32MapEntry("b", 2));
        var payload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(payload, fieldNumber: 1, left.WrittenSpan);
        WriteLengthDelimited(payload, fieldNumber: 2, right.WrittenSpan);
        var validator = new ProtobufInlineRuleValidator(ValidationMapEqualityEnvelope.Descriptor);

        validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        var unequalRight = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(unequalRight, fieldNumber: 1, CreateInt32MapEntry("a", 1));
        WriteLengthDelimited(unequalRight, fieldNumber: 1, CreateInt32MapEntry("b", 3));
        var unequalPayload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(unequalPayload, fieldNumber: 1, left.WrittenSpan);
        WriteLengthDelimited(unequalPayload, fieldNumber: 2, unequalRight.WrittenSpan);
        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            validator.Validate(unequalPayload.WrittenMemory, schemaId: 17, failFast: false));

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(exception.Violations[0].Rule.Name).IsEqualTo("map-message-equality");
    }

    [Test]
    public async Task Validate_MessageEqualityRejectsExcessiveRecursion()
    {
        byte[] left = [8, 1];
        byte[] right = [8, 0, 8, 1];
        for (var depth = 0; depth <= ProtobufInlineRuleValidator.MaximumValidationDepth; depth++)
        {
            var nextLeft = new ArrayBufferWriter<byte>();
            WriteVarint(nextLeft, 8);
            WriteVarint(nextLeft, 1);
            WriteLengthDelimited(nextLeft, fieldNumber: 2, left);
            left = nextLeft.WrittenSpan.ToArray();

            var nextRight = new ArrayBufferWriter<byte>();
            WriteLengthDelimited(nextRight, fieldNumber: 2, right);
            WriteVarint(nextRight, 8);
            WriteVarint(nextRight, 1);
            right = nextRight.WrittenSpan.ToArray();
        }
        var payload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(payload, fieldNumber: 1, left);
        WriteLengthDelimited(payload, fieldNumber: 2, right);
        var validator = new ProtobufInlineRuleValidator(ValidationMessageEqualityEnvelope.Descriptor);

        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false));

        await Assert.That(exception.Violations[0].Cause).IsTypeOf<SchemaRegistryRuleException>();
        await Assert.That(exception.Violations[0].Cause!.Message).Contains("message recursion exceeds");
    }

    [Test]
    public async Task Validate_Proto2GroupPreservesNestedPayload()
    {
        var invalid = new ArrayBufferWriter<byte>();
        WriteVarint(invalid, 2u << 3 | 3u);
        WriteVarint(invalid, 3u << 3);
        WriteVarint(invalid, 0);
        WriteVarint(invalid, 2u << 3 | 4u);
        var validator = new ProtobufInlineRuleValidator(Proto2ValidationMessage.Descriptor);

        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            validator.Validate(invalid.WrittenMemory, schemaId: 17, failFast: false));

        await Assert.That(exception.Violations.Select(static violation => violation.Rule.Name))
            .Contains("group-value-positive");
    }

    [Test]
    public async Task Validate_GroupsRejectExcessiveNesting()
    {
        var payload = new ArrayBufferWriter<byte>();
        for (var depth = 0; depth <= ProtobufInlineRuleValidator.MaximumValidationDepth; depth++)
            WriteVarint(payload, 99u << 3 | 3u);
        for (var depth = 0; depth <= ProtobufInlineRuleValidator.MaximumValidationDepth; depth++)
            WriteVarint(payload, 99u << 3 | 4u);
        var validator = new ProtobufInlineRuleValidator(Proto2ValidationMessage.Descriptor);

        var exception = Assert.Throws<SchemaRegistryRuleException>(() =>
            validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false));

        await Assert.That(exception.Message).Contains("group nesting exceeds");
    }

    [Test]
    public async Task Validate_WrapperUsesLastValueOccurrence()
    {
        var wrapper = new ArrayBufferWriter<byte>();
        WriteVarint(wrapper, 8);
        WriteVarint(wrapper, 5);
        WriteVarint(wrapper, 8);
        WriteVarint(wrapper, ulong.MaxValue);
        var payload = new ArrayBufferWriter<byte>();
        CreateValidMessage().WriteTo(payload);
        WriteLengthDelimited(payload, fieldNumber: 12, wrapper.WrittenSpan);
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);

        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false));

        await Assert.That(exception.Violations.Select(static violation => violation.Rule.Name))
            .Contains("score-not-negative");
    }

    [Test]
    public async Task Validate_OversizedFieldNumberReportsRuleException()
    {
        var payload = new ArrayBufferWriter<byte>();
        WriteVarint(payload, ((ulong)int.MaxValue + 1) << 3);
        WriteVarint(payload, 0);
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);

        var exception = Assert.Throws<SchemaRegistryRuleException>(() =>
            validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false));

        await Assert.That(exception.Message).Contains("field number exceeds Int32.MaxValue");
    }

    [Test]
    public async Task Validate_RecursiveMessagesRejectExcessiveDepth()
    {
        byte[] child = [8, 1];
        for (var depth = 0; depth <= ProtobufInlineRuleValidator.MaximumValidationDepth; depth++)
        {
            var nested = new ArrayBufferWriter<byte>();
            WriteLengthDelimited(nested, fieldNumber: 2, child);
            child = nested.WrittenSpan.ToArray();
        }
        var envelope = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(envelope, fieldNumber: 5, child);
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);

        var exception = Assert.Throws<SchemaRegistryRuleException>(() =>
            validator.Validate(envelope.WrittenMemory, schemaId: 17, failFast: false));

        await Assert.That(exception.Message).Contains("recursion exceeds");
    }

    [Test]
    public async Task Validate_RegisteredReferenceUsesExactSubjectVersion()
    {
        using var registry = new MockSchemaRegistryClient();
        var child = ValidationChild.Descriptor.File.ToProto();
        _ = await registry.RegisterSchemaAsync(
            "validation-child",
            new Schema { SchemaType = SchemaType.Protobuf, SchemaString = child.ToByteString().ToBase64() });
        var stricterChild = child.Clone();
        var meta = stricterChild.MessageType[0].Options.GetExtension(MetaExtensions.MessageMeta).Clone();
        meta.Rules[0].Expr = "this.value > 10";
        stricterChild.MessageType[0].Options.SetExtension(MetaExtensions.MessageMeta, meta);
        _ = await registry.RegisterSchemaAsync(
            "validation-child",
            new Schema { SchemaType = SchemaType.Protobuf, SchemaString = stricterChild.ToByteString().ToBase64() });
        var root = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = ValidationEnvelope.Descriptor.File.SerializedData.ToBase64(),
            References =
            [
                new SchemaReference
                {
                    Name = "protobuf_validation_child.proto",
                    Subject = "validation-child",
                    Version = 2
                }
            ]
        };
        IInlineValidationRuleExecutor validator =
            new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor, registry);
        var message = CreateValidMessage();

        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            validator.Validate(message.ToByteArray(), schemaId: 91, root, failFast: false));

        await Assert.That(exception.Violations.Select(static violation => violation.Rule.Name))
            .Contains("positive-child-value");
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

    private static ValidationResult EvaluateTypedRule(string expression, ValidationCelValue value)
    {
        var rule = CompiledValidationRule.Compile(
            new ValidationRule { Name = "protobuf-edge-case", Expr = expression },
            new Dictionary<string, int>(StringComparer.Ordinal),
            [],
            []);
        return rule.Evaluate(
            value,
            nowUnixMilliseconds: 0,
            default,
            default,
            equalityGeneration: 0);
    }

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

    private static byte[] CreateMapEntry(string key, ReadOnlySpan<byte> value)
    {
        var entry = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(entry, fieldNumber: 1, Encoding.UTF8.GetBytes(key));
        WriteLengthDelimited(entry, fieldNumber: 2, value);
        return entry.WrittenSpan.ToArray();
    }

    private static byte[] CreateInt32MapEntry(string key, ulong value)
    {
        var entry = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(entry, fieldNumber: 1, Encoding.UTF8.GetBytes(key));
        WriteVarint(entry, 2u << 3);
        WriteVarint(entry, value);
        return entry.WrittenSpan.ToArray();
    }

    private static void WriteLengthDelimited(
        IBufferWriter<byte> writer,
        int fieldNumber,
        ReadOnlySpan<byte> value)
    {
        WriteVarint(writer, (uint)(fieldNumber << 3 | 2));
        WriteVarint(writer, (uint)value.Length);
        writer.Write(value);
    }

    private static void WriteVarint(IBufferWriter<byte> writer, ulong value)
    {
        do
        {
            var span = writer.GetSpan(1);
            var current = (byte)(value & 0x7f);
            value >>= 7;
            span[0] = value == 0 ? current : (byte)(current | 0x80);
            writer.Advance(1);
        } while (value != 0);
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
