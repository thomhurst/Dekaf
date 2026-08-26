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

[NotInParallel]
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
        var proto3Validator = new ProtobufInlineRuleValidator(ValidationPresenceEnvelope.Descriptor);
        var proto2Validator = new ProtobufInlineRuleValidator(Proto2PresenceValidationMessage.Descriptor);
        proto3Validator.Validate(
            ReadOnlyMemory<byte>.Empty,
            schemaId: 17,
            failFast: false);
        proto2Validator.Validate(
            ReadOnlyMemory<byte>.Empty,
            schemaId: 18,
            failFast: false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            proto2Validator.Validate(ReadOnlyMemory<byte>.Empty, schemaId: 18, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_UnknownClosedEnumValueUsesDeclaredDefault()
    {
        var payload = new ArrayBufferWriter<byte>();
        WriteVarint(payload, 6u << 3);
        WriteVarint(payload, 99);
        var validator = new ProtobufInlineRuleValidator(Proto2PresenceValidationMessage.Descriptor);

        validator.Validate(payload.WrittenMemory, schemaId: 18, failFast: false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload.WrittenMemory, schemaId: 18, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_PackedUnknownClosedEnumValueIsExcludedFromCollection()
    {
        var packed = new ArrayBufferWriter<byte>();
        WriteVarint(packed, 1);
        WriteVarint(packed, 99);
        var payload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(payload, fieldNumber: 1, packed.WrittenSpan);
        var validator = new ProtobufInlineRuleValidator(Proto2PackedEnumValidationMessage.Descriptor);

        validator.Validate(payload.WrittenMemory, schemaId: 18, failFast: false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload.WrittenMemory, schemaId: 18, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_EncodedProto3DefaultUsesImplicitAbsence()
    {
        var payload = new ArrayBufferWriter<byte>();
        WriteVarint(payload, 4u << 3);
        WriteVarint(payload, 0);
        var validator = new ProtobufInlineRuleValidator(ValidationPresenceEnvelope.Descriptor);

        validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        var nonDefault = new ArrayBufferWriter<byte>();
        WriteVarint(nonDefault, 4u << 3);
        WriteVarint(nonDefault, 1);
        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            validator.Validate(nonDefault.WrittenMemory, schemaId: 17, failFast: false));

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(exception.Violations[0].Rule.Name)
            .IsEqualTo("implicit-default-scalars-are-absent");
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
    public async Task Validate_MapMessageValueMergesRepeatedOccurrences()
    {
        var entry = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(entry, fieldNumber: 1, "merged"u8);
        WriteLengthDelimited(entry, fieldNumber: 2, [8, 1]);
        WriteLengthDelimited(entry, fieldNumber: 2, []);
        var message = CreateValidMessage();
        message.ChildrenByName.Clear();
        var payload = new ArrayBufferWriter<byte>();
        message.WriteTo(payload);
        WriteLengthDelimited(payload, fieldNumber: 6, entry.WrittenSpan);
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);

        validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
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
    public async Task Validate_BytesLiteralDecodesEightDigitUnicodeEscapes()
    {
        var result = EvaluateTypedRule(
            "this == b'\\U0001F600'",
            ValidationCelValue.FromBytes("😀"u8.ToArray()));

        await Assert.That(result.Boolean).IsTrue();
    }

    [Test]
    public async Task Validate_TimestampIgnoresComponentsWithWrongWireType()
    {
        var timestamp = new ArrayBufferWriter<byte>();
        WriteVarint(timestamp, 1u << 3);
        WriteVarint(timestamp, 1_700_000_000);
        WriteLengthDelimited(timestamp, fieldNumber: 1, []);
        var message = CreateValidMessage();
        message.CreatedAt = null;
        var payload = new ArrayBufferWriter<byte>();
        message.WriteTo(payload);
        WriteLengthDelimited(payload, fieldNumber: 11, timestamp.WrittenSpan);
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);

        validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);

        await Assert.That(payload.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Validate_TemporalMessagesMergeComponentsAcrossOuterOccurrences()
    {
        var seconds = new ArrayBufferWriter<byte>();
        WriteVarint(seconds, 1u << 3);
        WriteVarint(seconds, 1_700_000_000);
        var nanos = new ArrayBufferWriter<byte>();
        WriteVarint(nanos, 2u << 3);
        WriteVarint(nanos, 5);
        var durationSeconds = new ArrayBufferWriter<byte>();
        WriteVarint(durationSeconds, 1u << 3);
        WriteVarint(durationSeconds, 1);

        var payload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(payload, fieldNumber: 1, seconds.WrittenSpan);
        WriteLengthDelimited(payload, fieldNumber: 1, nanos.WrittenSpan);
        WriteLengthDelimited(payload, fieldNumber: 2, durationSeconds.WrittenSpan);
        WriteLengthDelimited(payload, fieldNumber: 2, nanos.WrittenSpan);
        var validator = new ProtobufInlineRuleValidator(ValidationTemporalEnvelope.Descriptor);
        var parsed = ValidationTemporalEnvelope.Parser.ParseFrom(payload.WrittenSpan.ToArray());

        validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(parsed.CreatedAt.Seconds).IsEqualTo(1_700_000_000);
        await Assert.That(parsed.CreatedAt.Nanos).IsEqualTo(5);
        await Assert.That(parsed.Elapsed.Seconds).IsEqualTo(1);
        await Assert.That(parsed.Elapsed.Nanos).IsEqualTo(5);
        await Assert.That(allocated).IsEqualTo(0);
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
    public async Task Validate_JsonEqualityWithoutGenerationBypassesCache()
    {
        var result = EvaluateTypedRule(
            "this == this",
            ValidationCelValue.FromJson("{\"value\":1}"u8.ToArray()));

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
    public async Task Validate_MessageEqualityUsesBitwiseFloatingSemantics()
    {
        var validator = new ProtobufInlineRuleValidator(ValidationMessageEqualityEnvelope.Descriptor);
        var positiveZero = new ArrayBufferWriter<byte>();
        WriteFixed64(positiveZero, fieldNumber: 4, 0x0000_0000_0000_0000);
        var negativeZero = new ArrayBufferWriter<byte>();
        WriteFixed64(negativeZero, fieldNumber: 4, 0x8000_0000_0000_0000);
        var signedZeroPayload = CreateMessageEqualityPayload(positiveZero.WrittenSpan, negativeZero.WrittenSpan);
        var firstNaN = new ArrayBufferWriter<byte>();
        WriteFixed32(firstNaN, fieldNumber: 5, 0x7fc0_0001);
        var secondNaN = new ArrayBufferWriter<byte>();
        WriteFixed32(secondNaN, fieldNumber: 5, 0x7fc0_0002);
        var nanPayload = CreateMessageEqualityPayload(firstNaN.WrittenSpan, secondNaN.WrittenSpan);

        var signedZeroException = Assert.Throws<ValidationRulesFailedException>(() =>
            validator.Validate(signedZeroPayload, schemaId: 17, failFast: false));
        var nanException = Assert.Throws<ValidationRulesFailedException>(() =>
            validator.Validate(nanPayload, schemaId: 17, failFast: false));

        await Assert.That(signedZeroException.Violations[0].Rule.Name)
            .IsEqualTo("child-message-equality");
        await Assert.That(nanException.Violations[0].Rule.Name)
            .IsEqualTo("child-message-equality");
    }

    [Test]
    public async Task Validate_MessageEqualityUsesUnknownFieldSetSemantics()
    {
        var payload = CreateUnknownFieldEqualityPayload(reverseRepeatedValues: false);
        var validator = new ProtobufInlineRuleValidator(ValidationMessageEqualityEnvelope.Descriptor);

        validator.Validate(payload, schemaId: 17, failFast: false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload, schemaId: 17, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        var unequalPayload = CreateUnknownFieldEqualityPayload(reverseRepeatedValues: true);
        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            validator.Validate(unequalPayload, schemaId: 17, failFast: false));

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(exception.Violations[0].Rule.Name).IsEqualTo("child-message-equality");
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
    public async Task Validate_MapMessageEqualityMergesRepeatedValueFields()
    {
        var firstValue = new ArrayBufferWriter<byte>();
        WriteVarint(firstValue, 1u << 3);
        WriteVarint(firstValue, 1);
        var secondValue = new ArrayBufferWriter<byte>();
        WriteVarint(secondValue, 3u << 3);
        WriteVarint(secondValue, 2);
        var mergedValue = new ArrayBufferWriter<byte>();
        mergedValue.Write(firstValue.WrittenSpan);
        mergedValue.Write(secondValue.WrittenSpan);

        var left = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(left, fieldNumber: 2,
            CreateMessageMapEntry("child", firstValue.WrittenMemory, secondValue.WrittenMemory));
        var right = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(right, fieldNumber: 2,
            CreateMessageMapEntry("child", mergedValue.WrittenMemory));
        var payload = CreateMessageEqualityPayload(left.WrittenSpan, right.WrittenSpan);
        var validator = new ProtobufInlineRuleValidator(ValidationMapEqualityEnvelope.Descriptor);

        validator.Validate(payload, schemaId: 17, failFast: false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload, schemaId: 17, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
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
    public async Task Validate_WrapperUsesLastOuterOccurrence()
    {
        var first = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(first, fieldNumber: 1, "a"u8);
        var second = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(second, fieldNumber: 1, "b"u8);
        var payload = new ArrayBufferWriter<byte>();
        CreateValidMessage().WriteTo(payload);
        WriteLengthDelimited(payload, fieldNumber: 15, first.WrittenSpan);
        WriteLengthDelimited(payload, fieldNumber: 15, second.WrittenSpan);
        var validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);

        validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload.WrittenMemory, schemaId: 17, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(payload.WrittenCount).IsGreaterThan(0);
        await Assert.That(allocated).IsEqualTo(0);
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
    public async Task Validate_AlternatingRegisteredSchemasAllocatesZeroBytes()
    {
        using var registry = new MockSchemaRegistryClient();
        var executor = new ProtobufInlineRuleExecutor(registry, ValidationEnvelope.Descriptor);
        var schema = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = ValidationEnvelope.Descriptor.File.SerializedData.ToBase64()
        };
        var payload = CreateValidMessage().ToByteArray();
        executor.Validate(payload, schemaId: 19, schema, failFast: false);
        executor.Validate(payload, schemaId: 20, schema, failFast: false);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            executor.Validate(payload, schemaId: index % 2 == 0 ? 19 : 20, schema, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_TransientDescriptorLookupFailureIsRetried()
    {
        var descriptor = ValidationEnvelope.Descriptor.File.ToProto();
        var meta = descriptor.MessageType[0].Options.GetExtension(MetaExtensions.MessageMeta).Clone();
        meta.Rules[0].Expr = "this.age <= 0";
        descriptor.MessageType[0].Options.SetExtension(MetaExtensions.MessageMeta, meta);
        var resolved = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = descriptor.ToByteString().ToBase64()
        };
        using var registry = new TransientSchemaRegistryClient(resolved);
        var executor = new ProtobufInlineRuleExecutor(registry, ValidationEnvelope.Descriptor);
        var unresolved = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = "syntax = \"proto3\";"
        };
        var payload = CreateValidMessage().ToByteArray();

        executor.Validate(payload, schemaId: 21, unresolved, failFast: false);
        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            executor.Validate(payload, schemaId: 21, unresolved, failFast: false));

        await Assert.That(registry.GetSchemaCalls).IsEqualTo(2);
        await Assert.That(exception.Violations[0].Rule.Name).IsEqualTo("age-upper-bound");
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

    private static byte[] CreateMessageEqualityPayload(
        ReadOnlySpan<byte> left,
        ReadOnlySpan<byte> right)
    {
        var payload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(payload, fieldNumber: 1, left);
        WriteLengthDelimited(payload, fieldNumber: 2, right);
        return payload.WrittenSpan.ToArray();
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

    private static void WriteUnknownGroup(IBufferWriter<byte> writer, bool reverseFields)
    {
        var first = reverseFields ? 2u : 1u;
        var second = reverseFields ? 1u : 2u;
        WriteVarint(writer, 101u << 3 | 3u);
        WriteVarint(writer, first << 3);
        WriteVarint(writer, first);
        WriteVarint(writer, second << 3);
        WriteVarint(writer, second);
        WriteVarint(writer, 101u << 3 | 4u);
    }

    private static void WriteUnknownVarintFields(
        IBufferWriter<byte> writer,
        int start,
        int end,
        int step)
    {
        for (var fieldNumber = start; fieldNumber != end + step; fieldNumber += step)
        {
            WriteVarint(writer, (uint)fieldNumber << 3);
            WriteVarint(writer, (uint)fieldNumber);
        }
    }

    private static byte[] CreateUnknownFieldEqualityPayload(bool reverseRepeatedValues)
    {
        var left = new ArrayBufferWriter<byte>();
        WriteVarint(left, 1u << 3);
        WriteVarint(left, 1);
        WriteUnknownVarintFields(left, start: 90, end: 98, step: 1);
        WriteVarint(left, 99u << 3);
        WriteVarint(left, 7);
        WriteLengthDelimited(left, fieldNumber: 100, "unknown"u8);
        WriteVarint(left, 99u << 3);
        WriteVarint(left, 8);
        WriteUnknownGroup(left, reverseFields: false);

        var right = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(right, fieldNumber: 100, "unknown"u8);
        WriteVarint(right, 99u << 3);
        WriteVarint(right, reverseRepeatedValues ? 8u : 7u);
        WriteVarint(right, 99u << 3);
        WriteVarint(right, reverseRepeatedValues ? 7u : 8u);
        WriteUnknownVarintFields(right, start: 98, end: 90, step: -1);
        WriteUnknownGroup(right, reverseFields: true);
        WriteVarint(right, 1u << 3);
        WriteVarint(right, 1);

        var payload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(payload, fieldNumber: 1, left.WrittenSpan);
        WriteLengthDelimited(payload, fieldNumber: 2, right.WrittenSpan);
        return payload.WrittenSpan.ToArray();
    }

    private static byte[] CreateMessageMapEntry(
        string key,
        params ReadOnlyMemory<byte>[] values)
    {
        var entry = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(entry, fieldNumber: 1, Encoding.UTF8.GetBytes(key));
        for (var index = 0; index < values.Length; index++)
            WriteLengthDelimited(entry, fieldNumber: 2, values[index].Span);
        return entry.WrittenSpan.ToArray();
    }

    private static void WriteFixed32(IBufferWriter<byte> writer, int fieldNumber, uint value)
    {
        WriteVarint(writer, (uint)(fieldNumber << 3 | 5));
        var span = writer.GetSpan(sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        writer.Advance(sizeof(uint));
    }

    private static void WriteFixed64(IBufferWriter<byte> writer, int fieldNumber, ulong value)
    {
        WriteVarint(writer, (uint)(fieldNumber << 3 | 1));
        var span = writer.GetSpan(sizeof(ulong));
        BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        writer.Advance(sizeof(ulong));
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

    private sealed class TransientSchemaRegistryClient(Schema schema) : ISchemaRegistryClient
    {
        internal int GetSchemaCalls { get; private set; }

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default)
        {
            GetSchemaCalls++;
            return GetSchemaCalls == 1
                ? Task.FromException<Schema>(new HttpRequestException("transient"))
                : Task.FromResult(schema);
        }

        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema value,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema value,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema value,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose() { }
    }
}
