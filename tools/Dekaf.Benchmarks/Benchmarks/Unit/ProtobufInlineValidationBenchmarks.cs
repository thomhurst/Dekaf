using System.Buffers;
using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Serialization;
using Dekaf.Tests.Unit.SchemaRegistry.ProtobufFixtures;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Protects warmed valid Protobuf inline CHECK validation from per-message allocations.
/// Descriptor traversal, rule compilation, schema registration, and pools are warmed in setup.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class ProtobufInlineValidationBenchmarks
{
    private readonly ArrayBufferWriter<byte> _destination = new(512);
    private ProtobufInlineRuleValidator _validator = null!;
    private ProtobufInlineRuleValidator _semanticEqualityValidator = null!;
    private ProtobufInlineRuleValidator _mapEqualityValidator = null!;
    private ProtobufInlineRuleValidator _collectionEqualityValidator = null!;
    private ProtobufInlineRuleValidator _presenceValidator = null!;
    private ProtobufInlineRuleValidator _sint32Validator = null!;
    private ProtobufInlineRuleValidator _editionClosedEnumValidator = null!;
    private ProtobufInlineRuleExecutor _alternatingSchemaExecutor = null!;
    private Schema _serializedSchema = null!;
    private ProtobufSchemaRegistrySerializer<ValidationEnvelope> _serializer = null!;
    private ProtobufSchemaRegistrySerializer<ValidationEnvelope> _unvalidatedSerializer = null!;
    private CompiledValidationRule _floatingLiteralRule = null!;
    private CompiledValidationRule _mixedNumericRule = null!;
    private ValidationEnvelope _message = null!;
    private byte[] _payload = null!;
    private byte[] _mergedMapValuePayload = null!;
    private byte[] _semanticEqualityPayload = null!;
    private byte[] _floatingSemanticEqualityPayload = null!;
    private byte[] _unknownSemanticEqualityPayload = null!;
    private byte[] _mapEqualityPayload = null!;
    private byte[] _collectionEqualityPayload = null!;
    private byte[] _sint32Payload = null!;
    private byte[] _simpleLargePayload = null!;
    private byte[] _editionClosedEnumPayload = null!;
    private int _schemaIndex;
    private SerializationContext _context;

    [GlobalSetup]
    public void Setup()
    {
        _message = CreateMessage();
        _payload = _message.ToByteArray();
        _mergedMapValuePayload = CreateMergedMapValuePayload();
        _semanticEqualityPayload = CreateSemanticEqualityPayload();
        _floatingSemanticEqualityPayload = CreateFloatingSemanticEqualityPayload();
        _unknownSemanticEqualityPayload = CreateUnknownSemanticEqualityPayload();
        _mapEqualityPayload = CreateMapEqualityPayload();
        _collectionEqualityPayload = CreateCollectionEqualityPayload();
        _sint32Payload = [8, 1];
        var simpleLargePayload = new ArrayBufferWriter<byte>(4096);
        simpleLargePayload.Write(_sint32Payload);
        WriteLengthDelimited(simpleLargePayload, fieldNumber: 100, new byte[4096]);
        _simpleLargePayload = simpleLargePayload.WrittenSpan.ToArray();
        _editionClosedEnumPayload = [10, 1, 1];
        _validator = new ProtobufInlineRuleValidator(ValidationEnvelope.Descriptor);
        _semanticEqualityValidator = new ProtobufInlineRuleValidator(
            ValidationMessageEqualityEnvelope.Descriptor);
        _mapEqualityValidator = new ProtobufInlineRuleValidator(
            ValidationMapEqualityEnvelope.Descriptor);
        _collectionEqualityValidator = new ProtobufInlineRuleValidator(
            ValidationCollectionEnvelope.Descriptor);
        _presenceValidator = new ProtobufInlineRuleValidator(
            ValidationPresenceEnvelope.Descriptor);
        _sint32Validator = new ProtobufInlineRuleValidator(
            ValidationSint32BenchmarkEnvelope.Descriptor);
        _editionClosedEnumValidator = new ProtobufInlineRuleValidator(
            ValidationEditionClosedEnumBenchmarkEnvelope.Descriptor);
        _alternatingSchemaExecutor = new ProtobufInlineRuleExecutor(
            new BenchmarkSchemaRegistryClient(),
            ValidationEnvelope.Descriptor);
        _serializedSchema = new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = ValidationEnvelope.Descriptor.File.SerializedData.ToBase64()
        };
        _serializer = new ProtobufSchemaRegistrySerializer<ValidationEnvelope>(
            new BenchmarkSchemaRegistryClient(),
            new ProtobufSerializerConfig
            {
                UseSchemaReferences = false,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        _unvalidatedSerializer = new ProtobufSchemaRegistrySerializer<ValidationEnvelope>(
            new BenchmarkSchemaRegistryClient(),
            new ProtobufSerializerConfig { UseSchemaReferences = false });
        _floatingLiteralRule = CompiledValidationRule.Compile(
            new ValidationRule { Name = "floating-literal", Expr = "this == 0.1" },
            new Dictionary<string, int>(StringComparer.Ordinal),
            [],
            []);
        _mixedNumericRule = CompiledValidationRule.Compile(
            new ValidationRule { Name = "mixed-numeric", Expr = "this > 9007199254740992.0" },
            new Dictionary<string, int>(StringComparer.Ordinal),
            [],
            []);
        _context = new SerializationContext
        {
            Topic = "protobuf-inline-validation",
            Component = SerializationComponent.Value
        };

        _validator.Validate(_payload, schemaId: 1, failFast: false);
        _validator.Validate(_mergedMapValuePayload, schemaId: 1, failFast: false);
        _semanticEqualityValidator.Validate(_semanticEqualityPayload, schemaId: 1, failFast: false);
        _semanticEqualityValidator.Validate(_floatingSemanticEqualityPayload, schemaId: 1, failFast: false);
        _semanticEqualityValidator.Validate(_unknownSemanticEqualityPayload, schemaId: 1, failFast: false);
        _mapEqualityValidator.Validate(_mapEqualityPayload, schemaId: 1, failFast: false);
        _collectionEqualityValidator.Validate(
            _collectionEqualityPayload,
            schemaId: 1,
            failFast: false);
        _presenceValidator.Validate(ReadOnlyMemory<byte>.Empty, schemaId: 1, failFast: false);
        _sint32Validator.Validate(_sint32Payload, schemaId: 1, failFast: false);
        _sint32Validator.Validate(_simpleLargePayload, schemaId: 1, failFast: false);
        _editionClosedEnumValidator.Validate(
            _editionClosedEnumPayload,
            schemaId: 1,
            failFast: false);
        _alternatingSchemaExecutor.Validate(_payload, schemaId: 2, _serializedSchema, failFast: false);
        _alternatingSchemaExecutor.Validate(_payload, schemaId: 3, _serializedSchema, failFast: false);
        var destination = _destination;
        _serializer.Serialize(_message, ref destination, _context);
        _destination.Clear();
        destination = _destination;
        _unvalidatedSerializer.Serialize(_message, ref destination, _context);
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await _serializer.DisposeAsync();
        await _unvalidatedSerializer.DisposeAsync();
    }

    [Benchmark]
    public void ValidateRules() => _validator.Validate(_payload, schemaId: 1, failFast: false);

    [Benchmark]
    public void ValidateMergedMapMessageValue() =>
        _validator.Validate(_mergedMapValuePayload, schemaId: 1, failFast: false);

    [Benchmark]
    public void ValidateSemanticMessageEquality() =>
        _semanticEqualityValidator.Validate(_semanticEqualityPayload, schemaId: 1, failFast: false);

    [Benchmark]
    public void ValidateFloatingSemanticMessageEquality() =>
        _semanticEqualityValidator.Validate(_floatingSemanticEqualityPayload, schemaId: 1, failFast: false);

    [Benchmark]
    public void ValidateUnknownFieldSemanticEquality() =>
        _semanticEqualityValidator.Validate(_unknownSemanticEqualityPayload, schemaId: 1, failFast: false);

    [Benchmark]
    public void ValidateMapMessageEquality() =>
        _mapEqualityValidator.Validate(_mapEqualityPayload, schemaId: 1, failFast: false);

    [Benchmark]
    public void ValidateCollectionEquality() =>
        _collectionEqualityValidator.Validate(
            _collectionEqualityPayload,
            schemaId: 1,
            failFast: false);

    [Benchmark]
    public void ValidateAbsentWrapper() =>
        _presenceValidator.Validate(ReadOnlyMemory<byte>.Empty, schemaId: 1, failFast: false);

    [Benchmark]
    public void ValidateSInt32() =>
        _sint32Validator.Validate(_sint32Payload, schemaId: 1, failFast: false);

    [Benchmark]
    public void ValidateSimpleLargePayload() =>
        _sint32Validator.Validate(_simpleLargePayload, schemaId: 1, failFast: false);

    [Benchmark]
    public void ValidateEditionClosedEnum() =>
        _editionClosedEnumValidator.Validate(
            _editionClosedEnumPayload,
            schemaId: 1,
            failFast: false);

    [Benchmark]
    public void ValidateAlternatingRegisteredSchemas()
    {
        _schemaIndex ^= 1;
        _alternatingSchemaExecutor.Validate(
            _payload,
            schemaId: _schemaIndex + 2,
            _serializedSchema,
            failFast: false);
    }

    [Benchmark]
    public bool ValidateFloatingLiteralComparison() => _floatingLiteralRule.Evaluate(
            ValidationCelValue.FromFloating(0.1d),
            nowUnixMilliseconds: 0,
            default,
            default,
            equalityGeneration: 0)
        .Boolean;

    [Benchmark]
    public bool ValidateMixedNumericComparison() => _mixedNumericRule.Evaluate(
            ValidationCelValue.FromNumber(9007199254740993m),
            nowUnixMilliseconds: 0,
            default,
            default,
            equalityGeneration: 0)
        .Boolean;

    [Benchmark]
    public void SerializeValidated()
    {
        _destination.Clear();
        var destination = _destination;
        _serializer.Serialize(_message, ref destination, _context);
    }

    [Benchmark(Baseline = true)]
    public void SerializeWithoutValidation()
    {
        _destination.Clear();
        var destination = _destination;
        _unvalidatedSerializer.Serialize(_message, ref destination, _context);
    }

    private static ValidationEnvelope CreateMessage()
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
        message.Codes.Add(1);
        message.Codes.Add(2);
        message.Codes.Add(3);
        return message;
    }

    private static byte[] CreateSemanticEqualityPayload()
    {
        var left = new ArrayBufferWriter<byte>();
        WriteVarint(left, 8);
        WriteVarint(left, 1);
        WriteLengthDelimited(left, 3, [1, 2]);
        var right = new ArrayBufferWriter<byte>();
        WriteVarint(right, 8);
        WriteVarint(right, 0);
        WriteVarint(right, 8);
        WriteVarint(right, 1);
        WriteVarint(right, 3u << 3);
        WriteVarint(right, 1);
        WriteVarint(right, 3u << 3);
        WriteVarint(right, 2);
        var payload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(payload, 1, left.WrittenSpan);
        WriteLengthDelimited(payload, 2, right.WrittenSpan);
        return payload.WrittenSpan.ToArray();
    }

    private static byte[] CreateUnknownSemanticEqualityPayload()
    {
        var left = new ArrayBufferWriter<byte>();
        WriteVarint(left, 1u << 3);
        WriteVarint(left, 1);
        WriteVarint(left, 99u << 3);
        WriteVarint(left, 7);
        WriteLengthDelimited(left, 100, "unknown"u8);
        WriteVarint(left, 99u << 3);
        WriteVarint(left, 8);
        var right = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(right, 100, "unknown"u8);
        WriteVarint(right, 99u << 3);
        WriteVarint(right, 7);
        WriteVarint(right, 99u << 3);
        WriteVarint(right, 8);
        WriteVarint(right, 1u << 3);
        WriteVarint(right, 1);
        var payload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(payload, 1, left.WrittenSpan);
        WriteLengthDelimited(payload, 2, right.WrittenSpan);
        return payload.WrittenSpan.ToArray();
    }

    private static byte[] CreateFloatingSemanticEqualityPayload()
    {
        var left = new ArrayBufferWriter<byte>();
        WriteFixed64(left, fieldNumber: 4, 0x8000_0000_0000_0000);
        WriteFixed32(left, fieldNumber: 5, 0x7fc0_0001);
        var right = new ArrayBufferWriter<byte>();
        WriteFixed64(right, fieldNumber: 4, 0x8000_0000_0000_0000);
        WriteFixed32(right, fieldNumber: 5, 0x7fc0_0001);
        var payload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(payload, fieldNumber: 1, left.WrittenSpan);
        WriteLengthDelimited(payload, fieldNumber: 2, right.WrittenSpan);
        return payload.WrittenSpan.ToArray();
    }

    private static byte[] CreateMergedMapValuePayload()
    {
        var entry = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(entry, 1, "merged"u8);
        WriteLengthDelimited(entry, 2, [8, 1]);
        WriteLengthDelimited(entry, 2, []);
        var payload = new ArrayBufferWriter<byte>();
        CreateMessage().WriteTo(payload);
        WriteLengthDelimited(payload, 6, entry.WrittenSpan);
        return payload.WrittenSpan.ToArray();
    }

    private static byte[] CreateMapEqualityPayload()
    {
        var left = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(left, 1, CreateMapEntry("a"u8, 0));
        WriteLengthDelimited(left, 1, CreateMapEntry("b"u8, 2));
        WriteLengthDelimited(left, 1, CreateMapEntry("a"u8, 1));
        var right = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(right, 1, CreateMapEntry("a"u8, 1));
        WriteLengthDelimited(right, 1, CreateMapEntry("b"u8, 2));
        var payload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(payload, 1, left.WrittenSpan);
        WriteLengthDelimited(payload, 2, right.WrittenSpan);
        return payload.WrittenSpan.ToArray();
    }

    private static byte[] CreateCollectionEqualityPayload()
    {
        var payload = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(payload, 1, "first"u8);
        WriteLengthDelimited(payload, 1, "second"u8);
        WriteLengthDelimited(payload, 2, "first"u8);
        WriteLengthDelimited(payload, 2, "second"u8);
        WriteLengthDelimited(payload, 3, CreateMapEntry("a"u8, 0));
        WriteLengthDelimited(payload, 3, CreateMapEntry("b"u8, 2));
        WriteLengthDelimited(payload, 3, CreateMapEntry("a"u8, 1));
        WriteLengthDelimited(payload, 4, CreateMapEntry("b"u8, 2));
        WriteLengthDelimited(payload, 4, CreateMapEntry("a"u8, 1));
        return payload.WrittenSpan.ToArray();
    }

    private static byte[] CreateMapEntry(ReadOnlySpan<byte> key, uint value)
    {
        var entry = new ArrayBufferWriter<byte>();
        WriteLengthDelimited(entry, 1, key);
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

    private static void WriteVarint(IBufferWriter<byte> writer, uint value)
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

    private sealed class BenchmarkSchemaRegistryClient : ISchemaRegistryClient
    {
        public Task<int> RegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<int> GetOrRegisterSchemaAsync(
            string subject,
            Schema schema,
            CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RegisteredSchema> GetSchemaBySubjectAsync(
            string subject,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<int>> GetVersionsAsync(
            string subject,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> IsCompatibleAsync(
            string subject,
            Schema schema,
            string version = "latest",
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<int>> DeleteSubjectAsync(
            string subject,
            bool permanent = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose() { }
    }
}
