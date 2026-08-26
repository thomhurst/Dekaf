using Avro;
using Avro.Generic;
using Avro.IO;
using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry.Avro;
using AvroSchema = Avro.Schema;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser(displayGenColumns: false)]
public class AvroInlineValidationBenchmarks
{
    private static readonly int[] TwoItems = [1, 2];

    private AvroInlineRuleValidator _validator = null!;
    private ReadOnlyMemory<byte> _payload;
    private AvroInlineRuleValidator _arrayEqualityValidator = null!;
    private AvroInlineRuleValidator _conditionalArrayEqualityValidator = null!;
    private ReadOnlyMemory<byte> _equalArrayPayload;
    private ReadOnlyMemory<byte> _segmentedArrayPayload;
    private AvroInlineRuleValidator _mapEqualityValidator = null!;
    private ReadOnlyMemory<byte> _reorderedMapPayload;
    private AvroInlineRuleValidator _recordSizeValidator = null!;
    private ReadOnlyMemory<byte> _recordSizePayload;
    private AvroInlineRuleValidator _memberOnlyRecordValidator = null!;
    private ReadOnlyMemory<byte> _memberOnlyRecordPayload;
    private AvroInlineRuleValidator _nullableAggregateValidator = null!;
    private ReadOnlyMemory<byte> _nullableAggregatePayload;
    private AvroInlineRuleValidator _mixedFloatingValidator = null!;
    private ReadOnlyMemory<byte> _mixedFloatingPayload;
    private AvroInlineRuleValidator _nanAggregateValidator = null!;
    private ReadOnlyMemory<byte> _nanAggregatePayload;
    private AvroInlineRuleValidator _rootNanAggregateValidator = null!;
    private ReadOnlyMemory<byte> _rootNanAggregatePayload;
    private AvroInlineRuleValidator _enumValidator = null!;
    private ReadOnlyMemory<byte> _enumPayload;

    [GlobalSetup]
    public void Setup()
    {
        const string schemaText = """
            {
              "type": "record",
              "name": "ValidationBenchmarkRecord",
              "fields": [
                {
                  "name": "name",
                  "type": "string",
                  "confluent:rules": [{ "name": "name", "expr": "size(this) > 0" }]
                },
                {
                  "name": "age",
                  "type": "int",
                  "confluent:rules": [{ "name": "age", "expr": "this >= 0" }]
                }
              ]
            }
            """;
        var schema = (RecordSchema)AvroSchema.Parse(schemaText);
        var record = new GenericRecord(schema);
        record.Add("name", "dekaf");
        record.Add("age", 42);
        using var stream = new MemoryStream();
        var encoder = new BinaryEncoder(stream);
        new GenericDatumWriter<GenericRecord>(schema).Write(record, encoder);
        encoder.Flush();
        _payload = stream.ToArray();
        _validator = new AvroInlineRuleValidator(schema);
        _validator.Validate(_payload, 1, failFast: false);

        const string arrayEqualitySchema = """
            {
              "type": "record",
              "name": "ArrayEqualityBenchmarkRecord",
              "confluent:rules": [{ "name": "equal", "expr": "this.left == this.right" }],
              "fields": [
                { "name": "left", "type": { "type": "array", "items": "int" } },
                { "name": "right", "type": { "type": "array", "items": "int" } }
              ]
            }
            """;
        _arrayEqualityValidator = new AvroInlineRuleValidator(
            AvroSchema.Parse(arrayEqualitySchema));
        _equalArrayPayload = new byte[] { 4, 2, 4, 0, 4, 2, 4, 0 };
        _segmentedArrayPayload = new byte[] { 4, 2, 4, 0, 2, 2, 2, 4, 0 };
        _arrayEqualityValidator.Validate(_equalArrayPayload, 2, failFast: false);
        _arrayEqualityValidator.Validate(_segmentedArrayPayload, 2, failFast: false);

        const string conditionalArrayEqualitySchema = """
            {
              "type": "record",
              "name": "ConditionalArrayEqualityBenchmarkRecord",
              "confluent:rules": [{
                "name": "equal",
                "expr": "(true ? this.left : this.right) == this.right"
              }],
              "fields": [
                { "name": "left", "type": { "type": "array", "items": "int" } },
                { "name": "right", "type": { "type": "array", "items": "int" } }
              ]
            }
            """;
        _conditionalArrayEqualityValidator = new AvroInlineRuleValidator(
            AvroSchema.Parse(conditionalArrayEqualitySchema));
        _conditionalArrayEqualityValidator.Validate(_segmentedArrayPayload, 10, failFast: false);

        const string mapEqualitySchema = """
            {
              "type": "record",
              "name": "MapEqualityBenchmarkRecord",
              "confluent:rules": [{ "name": "equal", "expr": "this.left == this.right" }],
              "fields": [
                { "name": "left", "type": { "type": "map", "values": "int" } },
                { "name": "right", "type": { "type": "map", "values": "int" } }
              ]
            }
            """;
        _mapEqualityValidator = new AvroInlineRuleValidator(AvroSchema.Parse(mapEqualitySchema));
        _reorderedMapPayload = new byte[]
        {
            4, 2, (byte)'a', 2, 2, (byte)'b', 4, 0,
            4, 2, (byte)'b', 4, 2, (byte)'a', 2, 0
        };
        _mapEqualityValidator.Validate(_reorderedMapPayload, 3, failFast: false);

        const string recordSizeSchema = """
            {
              "type": "record",
              "name": "RecordSizeBenchmarkRecord",
              "confluent:rules": [{ "name": "size", "expr": "size(this) == 2" }],
              "fields": [
                { "name": "name", "type": "string" },
                { "name": "age", "type": "int" }
              ]
            }
            """;
        var sizedSchema = (RecordSchema)AvroSchema.Parse(recordSizeSchema);
        var sizedRecord = new GenericRecord(sizedSchema);
        sizedRecord.Add("name", "dekaf");
        sizedRecord.Add("age", 42);
        using var sizedStream = new MemoryStream();
        var sizedEncoder = new BinaryEncoder(sizedStream);
        new GenericDatumWriter<GenericRecord>(sizedSchema).Write(sizedRecord, sizedEncoder);
        sizedEncoder.Flush();
        _recordSizePayload = sizedStream.ToArray();
        _recordSizeValidator = new AvroInlineRuleValidator(sizedSchema);
        _recordSizeValidator.Validate(_recordSizePayload, 4, failFast: false);

        const string memberOnlyRecordSchema = """
            {
              "type": "record",
              "name": "MemberOnlyRecordBenchmarkRecord",
              "confluent:rules": [{ "name": "name", "expr": "this.name == 'dekaf'" }],
              "fields": [
                { "name": "name", "type": "string" },
                { "name": "age", "type": "int" }
              ]
            }
            """;
        var memberOnlySchema = (RecordSchema)AvroSchema.Parse(memberOnlyRecordSchema);
        var memberOnlyRecord = new GenericRecord(memberOnlySchema);
        memberOnlyRecord.Add("name", "dekaf");
        memberOnlyRecord.Add("age", 42);
        using var memberOnlyStream = new MemoryStream();
        var memberOnlyEncoder = new BinaryEncoder(memberOnlyStream);
        new GenericDatumWriter<GenericRecord>(memberOnlySchema).Write(memberOnlyRecord, memberOnlyEncoder);
        memberOnlyEncoder.Flush();
        _memberOnlyRecordPayload = memberOnlyStream.ToArray();
        _memberOnlyRecordValidator = new AvroInlineRuleValidator(memberOnlySchema);
        _memberOnlyRecordValidator.Validate(_memberOnlyRecordPayload, 11, failFast: false);

        const string nullableAggregateSchema = """
            {
              "type": "record",
              "name": "NullableAggregateBenchmarkRecord",
              "confluent:rules": [{ "name": "size", "expr": "size(this.items) == 2 && this.child.code == 7" }],
              "fields": [
                { "name": "items", "type": ["null", { "type": "array", "items": "int" }] },
                {
                  "name": "child",
                  "type": ["null", {
                    "type": "record",
                    "name": "NullableAggregateBenchmarkChild",
                    "fields": [{ "name": "code", "type": "int" }]
                  }],
                  "confluent:rules": [{ "name": "code", "expr": "this.code == 7" }]
                }
              ]
            }
            """;
        var nullableSchema = (RecordSchema)AvroSchema.Parse(nullableAggregateSchema);
        var nullableChildSchema = (RecordSchema)((UnionSchema)nullableSchema.Fields[1].Schema)[1];
        var nullableChild = new GenericRecord(nullableChildSchema);
        nullableChild.Add("code", 7);
        var nullableRecord = new GenericRecord(nullableSchema);
        nullableRecord.Add("items", TwoItems);
        nullableRecord.Add("child", nullableChild);
        using var nullableStream = new MemoryStream();
        var nullableEncoder = new BinaryEncoder(nullableStream);
        new GenericDatumWriter<GenericRecord>(nullableSchema).Write(nullableRecord, nullableEncoder);
        nullableEncoder.Flush();
        _nullableAggregatePayload = nullableStream.ToArray();
        _nullableAggregateValidator = new AvroInlineRuleValidator(nullableSchema);
        _nullableAggregateValidator.Validate(_nullableAggregatePayload, 5, failFast: false);

        const string mixedFloatingSchema = """
            {
              "type": "double",
              "confluent:rules": [{
                "name": "precision",
                "expr": "this != 9007199254740993 && this < 9007199254740993"
              }]
            }
            """;
        var floatingSchema = AvroSchema.Parse(mixedFloatingSchema);
        using var floatingStream = new MemoryStream();
        var floatingEncoder = new BinaryEncoder(floatingStream);
        new GenericDatumWriter<double>(floatingSchema).Write(9007199254740992d, floatingEncoder);
        floatingEncoder.Flush();
        _mixedFloatingPayload = floatingStream.ToArray();
        _mixedFloatingValidator = new AvroInlineRuleValidator(floatingSchema);
        _mixedFloatingValidator.Validate(_mixedFloatingPayload, 6, failFast: false);

        const string nanAggregateSchema = """
            {
              "type": "record",
              "name": "NaNAggregateBenchmarkRecord",
              "confluent:rules": [{ "name": "not-equal", "expr": "this.left != this.right" }],
              "fields": [
                { "name": "left", "type": { "type": "array", "items": "double" } },
                { "name": "right", "type": { "type": "array", "items": "double" } }
              ]
            }
            """;
        var nanSchema = (RecordSchema)AvroSchema.Parse(nanAggregateSchema);
        var nanRecord = new GenericRecord(nanSchema);
        nanRecord.Add("left", new[] { double.NaN });
        nanRecord.Add("right", new[] { double.NaN });
        using var nanStream = new MemoryStream();
        var nanEncoder = new BinaryEncoder(nanStream);
        new GenericDatumWriter<GenericRecord>(nanSchema).Write(nanRecord, nanEncoder);
        nanEncoder.Flush();
        _nanAggregatePayload = nanStream.ToArray();
        _nanAggregateValidator = new AvroInlineRuleValidator(nanSchema);
        _nanAggregateValidator.Validate(_nanAggregatePayload, 7, failFast: false);

        const string rootNanAggregateSchema = """
            {
              "type": "array",
              "items": "double",
              "confluent:rules": [{ "name": "not-equal", "expr": "this != this" }]
            }
            """;
        var rootNanSchema = (ArraySchema)AvroSchema.Parse(rootNanAggregateSchema);
        using var rootNanStream = new MemoryStream();
        var rootNanEncoder = new BinaryEncoder(rootNanStream);
        new GenericDatumWriter<object>(rootNanSchema).Write(new[] { double.NaN }, rootNanEncoder);
        rootNanEncoder.Flush();
        _rootNanAggregatePayload = rootNanStream.ToArray();
        _rootNanAggregateValidator = new AvroInlineRuleValidator(rootNanSchema);
        _rootNanAggregateValidator.Validate(_rootNanAggregatePayload, 8, failFast: false);

        const string enumSchemaText = """
            {
              "type": "record",
              "name": "EnumBenchmarkRecord",
              "fields": [{
                "name": "status",
                "type": { "type": "enum", "name": "BenchmarkStatus", "symbols": ["OPEN", "CLOSED"] },
                "confluent:rules": [{ "name": "open", "expr": "this == 'OPEN'" }]
              }]
            }
            """;
        var enumSchema = (RecordSchema)AvroSchema.Parse(enumSchemaText);
        var statusSchema = (EnumSchema)enumSchema.Fields[0].Schema;
        var enumRecord = new GenericRecord(enumSchema);
        enumRecord.Add("status", new GenericEnum(statusSchema, "OPEN"));
        using var enumStream = new MemoryStream();
        var enumEncoder = new BinaryEncoder(enumStream);
        new GenericDatumWriter<GenericRecord>(enumSchema).Write(enumRecord, enumEncoder);
        enumEncoder.Flush();
        _enumPayload = enumStream.ToArray();
        _enumValidator = new AvroInlineRuleValidator(enumSchema);
        _enumValidator.Validate(_enumPayload, 9, failFast: false);
    }

    [Benchmark]
    public void ValidateWarmedValidPayload() =>
        _validator.Validate(_payload, 1, failFast: false);

    [Benchmark]
    public void ValidateEqualArraysWithIdenticalEncoding() =>
        _arrayEqualityValidator.Validate(_equalArrayPayload, 2, failFast: false);

    [Benchmark]
    public void ValidateEqualArraysWithDifferentBlocks() =>
        _arrayEqualityValidator.Validate(_segmentedArrayPayload, 2, failFast: false);

    [Benchmark]
    public void ValidateConditionalEqualArraysWithDifferentBlocks() =>
        _conditionalArrayEqualityValidator.Validate(_segmentedArrayPayload, 10, failFast: false);

    [Benchmark]
    public void ValidateEqualMapsWithDifferentOrder() =>
        _mapEqualityValidator.Validate(_reorderedMapPayload, 3, failFast: false);

    [Benchmark]
    public void ValidateRecordSize() =>
        _recordSizeValidator.Validate(_recordSizePayload, 4, failFast: false);

    [Benchmark]
    public void ValidateMemberOnlyRecordRule() =>
        _memberOnlyRecordValidator.Validate(_memberOnlyRecordPayload, 11, failFast: false);

    [Benchmark]
    public void ValidateNullableAggregateMembers() =>
        _nullableAggregateValidator.Validate(_nullableAggregatePayload, 5, failFast: false);

    [Benchmark]
    public void ValidateMixedFloatingComparison() =>
        _mixedFloatingValidator.Validate(_mixedFloatingPayload, 6, failFast: false);

    [Benchmark]
    public void ValidateNaNAggregateInequality() =>
        _nanAggregateValidator.Validate(_nanAggregatePayload, 7, failFast: false);

    [Benchmark]
    public void ValidateRootNaNAggregateInequality() =>
        _rootNanAggregateValidator.Validate(_rootNanAggregatePayload, 8, failFast: false);

    [Benchmark]
    public void ValidateEnumRule() =>
        _enumValidator.Validate(_enumPayload, 9, failFast: false);
}
