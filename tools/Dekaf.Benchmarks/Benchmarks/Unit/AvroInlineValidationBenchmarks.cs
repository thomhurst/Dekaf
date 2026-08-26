using System.Text;
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
    private AvroInlineRuleValidator _conditionalArraySizeValidator = null!;
    private ReadOnlyMemory<byte> _conditionalArraySizePayload;
    private ReadOnlyMemory<byte> _equalArrayPayload;
    private ReadOnlyMemory<byte> _segmentedArrayPayload;
    private AvroInlineRuleValidator _unionArrayEqualityValidator = null!;
    private ReadOnlyMemory<byte> _equalUnionArrayControlPayload;
    private ReadOnlyMemory<byte> _equalUnionArrayReorderedPayload;
    private AvroInlineRuleValidator _unionConcreteArrayEqualityValidator = null!;
    private ReadOnlyMemory<byte> _equalUnionConcreteArrayPayload;
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
    private AvroInlineRuleValidator _mixedFloatingArithmeticValidator = null!;
    private ReadOnlyMemory<byte> _mixedFloatingArithmeticPayload;
    private AvroInlineRuleValidator _nanAggregateValidator = null!;
    private ReadOnlyMemory<byte> _nanAggregatePayload;
    private AvroInlineRuleValidator _rootNanAggregateValidator = null!;
    private ReadOnlyMemory<byte> _rootNanAggregatePayload;
    private AvroInlineRuleValidator _enumValidator = null!;
    private ReadOnlyMemory<byte> _enumPayload;
    private AvroInlineRuleValidator _rootArrayNestedValidator = null!;
    private ReadOnlyMemory<byte> _rootArrayNestedPayload;
    private AvroInlineRuleValidator _rootArrayHasValidator = null!;
    private AvroInlineRuleValidator _memberArrayHasValidator = null!;
    private AvroInlineRuleValidator _mapMemberNestedValidator = null!;
    private AvroInlineRuleValidator _mapMemberAggregateValidator = null!;
    private ReadOnlyMemory<byte> _mapMemberNestedPayload;
    private AvroInlineRuleValidator _unionMemberNestedValidator = null!;
    private ReadOnlyMemory<byte> _unionMemberNestedPayload;
    private AvroInlineRuleValidator _mixedRootMemberNestedValidator = null!;
    private ReadOnlyMemory<byte> _mixedRootMemberNestedPayload;
    private AvroInlineRuleValidator _nestedMemberFrameValidator = null!;
    private ReadOnlyMemory<byte> _nestedMemberFramePayload;

    [GlobalSetup]
    public void Setup()
    {
        const string schemaText = """
            {
              "type": "record",
              "name": "ValidationBenchmarkRecord",
              "confluent:rules": [{ "name": "root-size", "expr": "size(this) == 2" }],
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

        const string conditionalArraySizeSchema = """
            {
              "type": "record",
              "name": "ConditionalArraySizeBenchmarkRecord",
              "confluent:rules": [{
                "name": "size",
                "expr": "size(this.flag ? this.left : this.right) == 128"
              }],
              "fields": [
                { "name": "flag", "type": "boolean" },
                { "name": "left", "type": { "type": "array", "items": "int" } },
                { "name": "right", "type": { "type": "array", "items": "int" } }
              ]
            }
            """;
        var conditionalSizeSchema = (RecordSchema)AvroSchema.Parse(conditionalArraySizeSchema);
        var conditionalSizeRecord = new GenericRecord(conditionalSizeSchema);
        var conditionalSizeItems = Enumerable.Range(0, 128).ToArray();
        conditionalSizeRecord.Add("flag", true);
        conditionalSizeRecord.Add("left", conditionalSizeItems);
        conditionalSizeRecord.Add("right", conditionalSizeItems);
        using var conditionalSizeStream = new MemoryStream();
        var conditionalSizeEncoder = new BinaryEncoder(conditionalSizeStream);
        new GenericDatumWriter<GenericRecord>(conditionalSizeSchema).Write(
            conditionalSizeRecord,
            conditionalSizeEncoder);
        conditionalSizeEncoder.Flush();
        _conditionalArraySizePayload = conditionalSizeStream.ToArray();
        _conditionalArraySizeValidator = new AvroInlineRuleValidator(conditionalSizeSchema);
        _conditionalArraySizeValidator.Validate(
            _conditionalArraySizePayload,
            21,
            failFast: false);

        const string unionArrayEqualitySchema = """
            {
              "type": "record",
              "name": "UnionArrayEqualityBenchmarkRecord",
              "confluent:rules": [{ "name": "equal", "expr": "this.left == this.right" }],
              "fields": [
                { "name": "left", "type": { "type": "array", "items": ["int", "long"] } },
                { "name": "right", "type": { "type": "array", "items": ["long", "int"] } }
              ]
            }
            """;
        _unionArrayEqualityValidator = new AvroInlineRuleValidator(
            AvroSchema.Parse(unionArrayEqualitySchema));
        _equalUnionArrayControlPayload = new byte[] { 2, 0, 2, 0, 2, 0, 2, 0 };
        _equalUnionArrayReorderedPayload = new byte[] { 2, 0, 2, 0, 2, 2, 2, 0 };
        _unionArrayEqualityValidator.Validate(
            _equalUnionArrayControlPayload,
            17,
            failFast: false);

        const string unionConcreteArrayEqualitySchema = """
            {
              "type": "record",
              "name": "UnionConcreteArrayEqualityBenchmarkRecord",
              "confluent:rules": [{ "name": "equal", "expr": "this.left == this.right" }],
              "fields": [
                { "name": "left", "type": { "type": "array", "items": ["null", "int"] } },
                { "name": "right", "type": { "type": "array", "items": "int" } }
              ]
            }
            """;
        _unionConcreteArrayEqualityValidator = new AvroInlineRuleValidator(
            AvroSchema.Parse(unionConcreteArrayEqualitySchema));
        _equalUnionConcreteArrayPayload = new byte[] { 2, 2, 2, 0, 2, 2, 0 };
        _unionConcreteArrayEqualityValidator.Validate(
            _equalUnionConcreteArrayPayload,
            20,
            failFast: false);

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

        const string mixedFloatingArithmeticSchema = """
            {
              "type": "record",
              "name": "MixedFloatingArithmeticBenchmarkRecord",
              "confluent:rules": [{
                "name": "arithmetic",
                "expr": "this.exact + this.floating == 2 && this.exact - this.floating == 0"
              }],
              "fields": [
                { "name": "exact", "type": "long" },
                { "name": "floating", "type": "double" }
              ]
            }
            """;
        var arithmeticSchema = (RecordSchema)AvroSchema.Parse(mixedFloatingArithmeticSchema);
        var arithmeticRecord = new GenericRecord(arithmeticSchema);
        arithmeticRecord.Add("exact", 1L);
        arithmeticRecord.Add("floating", 1d);
        using var arithmeticStream = new MemoryStream();
        var arithmeticEncoder = new BinaryEncoder(arithmeticStream);
        new GenericDatumWriter<GenericRecord>(arithmeticSchema).Write(arithmeticRecord, arithmeticEncoder);
        arithmeticEncoder.Flush();
        _mixedFloatingArithmeticPayload = arithmeticStream.ToArray();
        _mixedFloatingArithmeticValidator = new AvroInlineRuleValidator(arithmeticSchema);
        _mixedFloatingArithmeticValidator.Validate(
            _mixedFloatingArithmeticPayload,
            14,
            failFast: false);

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

        const string rootArrayNestedSchemaText = """
            {
              "type": "array",
              "confluent:rules": [{ "name": "size", "expr": "size(this) == 128" }],
              "items": {
                "type": "int",
                "confluent:rules": [{ "name": "positive", "expr": "this > 0" }]
              }
            }
            """;
        var rootArrayNestedSchema = (ArraySchema)AvroSchema.Parse(rootArrayNestedSchemaText);
        var rootArrayNestedItems = new int[128];
        Array.Fill(rootArrayNestedItems, 1);
        using var rootArrayNestedStream = new MemoryStream();
        var rootArrayNestedEncoder = new BinaryEncoder(rootArrayNestedStream);
        new GenericDatumWriter<object>(rootArrayNestedSchema).Write(
            rootArrayNestedItems,
            rootArrayNestedEncoder);
        rootArrayNestedEncoder.Flush();
        _rootArrayNestedPayload = rootArrayNestedStream.ToArray();
        _rootArrayNestedValidator = new AvroInlineRuleValidator(rootArrayNestedSchema);
        _rootArrayNestedValidator.Validate(_rootArrayNestedPayload, 12, failFast: false);

        const string rootArrayHasSchemaText = """
            {
              "type": "array",
              "confluent:rules": [{ "name": "present", "expr": "has(this)" }],
              "items": "int"
            }
            """;
        _rootArrayHasValidator = new AvroInlineRuleValidator(
            AvroSchema.Parse(rootArrayHasSchemaText));
        _rootArrayHasValidator.Validate(_rootArrayNestedPayload, 18, failFast: false);

        const string memberArrayHasSchemaText = """
            {
              "type": "record",
              "name": "MemberArrayHasBenchmarkRecord",
              "confluent:rules": [{ "name": "present", "expr": "has(this.items)" }],
              "fields": [{ "name": "items", "type": { "type": "array", "items": "int" } }]
            }
            """;
        _memberArrayHasValidator = new AvroInlineRuleValidator(
            AvroSchema.Parse(memberArrayHasSchemaText));
        _memberArrayHasValidator.Validate(_rootArrayNestedPayload, 19, failFast: false);

        const string mapMemberNestedSchemaText = """
            {
              "type": "map",
              "confluent:rules": [{ "name": "selected", "expr": "this.selected.code > 0" }],
              "values": {
                "type": "record",
                "name": "MapMemberNestedBenchmarkValue",
                "fields": [{
                  "name": "code",
                  "type": "int",
                  "confluent:rules": [{ "name": "positive", "expr": "this > 0" }]
                }]
              }
            }
            """;
        var mapMemberNestedSchema = (MapSchema)AvroSchema.Parse(mapMemberNestedSchemaText);
        var mapMemberNestedValueSchema = (RecordSchema)mapMemberNestedSchema.ValueSchema;
        var mapMemberNestedValues = new Dictionary<string, object>(128);
        for (var index = 0; index < 127; index++)
        {
            var mapValue = new GenericRecord(mapMemberNestedValueSchema);
            mapValue.Add("code", 1);
            mapMemberNestedValues.Add($"entry-{index}", mapValue);
        }
        var selectedMapValue = new GenericRecord(mapMemberNestedValueSchema);
        selectedMapValue.Add("code", 1);
        mapMemberNestedValues.Add("selected", selectedMapValue);
        using var mapMemberNestedStream = new MemoryStream();
        var mapMemberNestedEncoder = new BinaryEncoder(mapMemberNestedStream);
        new GenericDatumWriter<object>(mapMemberNestedSchema).Write(
            mapMemberNestedValues,
            mapMemberNestedEncoder);
        mapMemberNestedEncoder.Flush();
        _mapMemberNestedPayload = mapMemberNestedStream.ToArray();
        _mapMemberNestedValidator = new AvroInlineRuleValidator(mapMemberNestedSchema);
        _mapMemberNestedValidator.Validate(_mapMemberNestedPayload, 13, failFast: false);

        const string mapMemberAggregateSchemaText = """
            {
              "type": "map",
              "confluent:rules": [{ "name": "selected", "expr": "this.selected.code > 0" }],
              "values": {
                "type": "record",
                "name": "MapMemberAggregateBenchmarkValue",
                "confluent:rules": [{ "name": "present", "expr": "has(this.code)" }],
                "fields": [{ "name": "code", "type": "int" }]
              }
            }
            """;
        _mapMemberAggregateValidator = new AvroInlineRuleValidator(
            AvroSchema.Parse(mapMemberAggregateSchemaText));
        _mapMemberAggregateValidator.Validate(_mapMemberNestedPayload, 20, failFast: false);

        var unionSchemaText = new StringBuilder(
            "{\"type\":[\"null\",{\"type\":\"record\",\"name\":\"UnionMemberNestedBenchmarkValue\",\"fields\":[");
        for (var index = 0; index < 128; index++)
        {
            if (index != 0)
                unionSchemaText.Append(',');
            unionSchemaText.Append("{\"name\":\"field").Append(index).Append("\",\"type\":\"int\"");
            if (index == 0)
            {
                unionSchemaText.Append(
                    ",\"confluent:rules\":[{\"name\":\"positive\",\"expr\":\"this > 0\"}]");
            }
            unionSchemaText.Append('}');
        }
        unionSchemaText.Append(
            "]}],\"confluent:rules\":[{\"name\":\"selected\",\"expr\":\"this.field127 > 0\"}]}");
        var unionMemberNestedSchema = (UnionSchema)AvroSchema.Parse(unionSchemaText.ToString());
        var unionMemberNestedValueSchema = (RecordSchema)unionMemberNestedSchema[1];
        var unionMemberNestedValue = new GenericRecord(unionMemberNestedValueSchema);
        for (var index = 0; index < 128; index++)
            unionMemberNestedValue.Add($"field{index}", 1);
        using var unionMemberNestedStream = new MemoryStream();
        var unionMemberNestedEncoder = new BinaryEncoder(unionMemberNestedStream);
        new GenericDatumWriter<object>(unionMemberNestedSchema).Write(
            unionMemberNestedValue,
            unionMemberNestedEncoder);
        unionMemberNestedEncoder.Flush();
        _unionMemberNestedPayload = unionMemberNestedStream.ToArray();
        _unionMemberNestedValidator = new AvroInlineRuleValidator(unionMemberNestedSchema);
        _unionMemberNestedValidator.Validate(_unionMemberNestedPayload, 14, failFast: false);

        const string mixedRootMemberNestedSchemaText = """
            {
              "type": "record",
              "name": "MixedRootMemberNestedBenchmarkRecord",
              "confluent:rules": [{
                "name": "root-and-member",
                "expr": "size(this) == 1 && size(this.items) == 128"
              }],
              "fields": [{
                "name": "items",
                "confluent:rules": [{ "name": "field-size", "expr": "size(this) == 128" }],
                "type": {
                  "type": "array",
                  "items": {
                    "type": "int",
                    "confluent:rules": [{ "name": "positive", "expr": "this > 0" }]
                  }
                }
              }]
            }
            """;
        var mixedRootMemberNestedSchema = (RecordSchema)AvroSchema.Parse(
            mixedRootMemberNestedSchemaText);
        var mixedRootMemberNestedRecord = new GenericRecord(mixedRootMemberNestedSchema);
        mixedRootMemberNestedRecord.Add("items", Enumerable.Range(1, 128).ToArray());
        using var mixedRootMemberNestedStream = new MemoryStream();
        var mixedRootMemberNestedEncoder = new BinaryEncoder(mixedRootMemberNestedStream);
        new GenericDatumWriter<GenericRecord>(mixedRootMemberNestedSchema).Write(
            mixedRootMemberNestedRecord,
            mixedRootMemberNestedEncoder);
        mixedRootMemberNestedEncoder.Flush();
        _mixedRootMemberNestedPayload = mixedRootMemberNestedStream.ToArray();
        _mixedRootMemberNestedValidator = new AvroInlineRuleValidator(mixedRootMemberNestedSchema);
        _mixedRootMemberNestedValidator.Validate(
            _mixedRootMemberNestedPayload,
            15,
            failFast: false);

        const string nestedMemberFrameSchemaText = """
            {
              "type": "record",
              "name": "NestedMemberFrameBenchmarkRecord",
              "confluent:rules": [{
                "name": "parent-members",
                "expr": "size(this) == 2 && this.name == 'dekaf' && this.child.value == 1"
              }],
              "fields": [
                { "name": "name", "type": "string" },
                {
                  "name": "child",
                  "type": {
                    "type": "record",
                    "name": "NestedMemberFrameBenchmarkChild",
                    "confluent:rules": [{
                      "name": "child-member",
                      "expr": "size(this) == 1 && this.value == 1"
                    }],
                    "fields": [{ "name": "value", "type": "int" }]
                  }
                }
              ]
            }
            """;
        var nestedMemberFrameSchema = (RecordSchema)AvroSchema.Parse(
            nestedMemberFrameSchemaText);
        var nestedMemberFrameChildSchema = (RecordSchema)nestedMemberFrameSchema.Fields[1].Schema;
        var nestedMemberFrameChild = new GenericRecord(nestedMemberFrameChildSchema);
        nestedMemberFrameChild.Add("value", 1);
        var nestedMemberFrameRecord = new GenericRecord(nestedMemberFrameSchema);
        nestedMemberFrameRecord.Add("name", "dekaf");
        nestedMemberFrameRecord.Add("child", nestedMemberFrameChild);
        using var nestedMemberFrameStream = new MemoryStream();
        var nestedMemberFrameEncoder = new BinaryEncoder(nestedMemberFrameStream);
        new GenericDatumWriter<GenericRecord>(nestedMemberFrameSchema).Write(
            nestedMemberFrameRecord,
            nestedMemberFrameEncoder);
        nestedMemberFrameEncoder.Flush();
        _nestedMemberFramePayload = nestedMemberFrameStream.ToArray();
        _nestedMemberFrameValidator = new AvroInlineRuleValidator(nestedMemberFrameSchema);
        _nestedMemberFrameValidator.Validate(_nestedMemberFramePayload, 16, failFast: false);
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
    public void ValidateConditionalArraySize() =>
        _conditionalArraySizeValidator.Validate(
            _conditionalArraySizePayload,
            21,
            failFast: false);

    [Benchmark]
    public void ValidateEqualUnionArraysControl() =>
        _unionArrayEqualityValidator.Validate(
            _equalUnionArrayControlPayload,
            17,
            failFast: false);

    [Benchmark]
    public void ValidateEqualUnionArraysWithDifferentOrdering() =>
        _unionArrayEqualityValidator.Validate(
            _equalUnionArrayReorderedPayload,
            17,
            failFast: false);

    [Benchmark]
    public void ValidateEqualUnionAndConcreteArrays() =>
        _unionConcreteArrayEqualityValidator.Validate(
            _equalUnionConcreteArrayPayload,
            20,
            failFast: false);

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
    public void ValidateMixedFloatingArithmetic() =>
        _mixedFloatingArithmeticValidator.Validate(
            _mixedFloatingArithmeticPayload,
            14,
            failFast: false);

    [Benchmark]
    public void ValidateNaNAggregateInequality() =>
        _nanAggregateValidator.Validate(_nanAggregatePayload, 7, failFast: false);

    [Benchmark]
    public void ValidateRootNaNAggregateInequality() =>
        _rootNanAggregateValidator.Validate(_rootNanAggregatePayload, 8, failFast: false);

    [Benchmark]
    public void ValidateEnumRule() =>
        _enumValidator.Validate(_enumPayload, 9, failFast: false);

    [Benchmark]
    public void ValidateRootArrayWithNestedRules() =>
        _rootArrayNestedValidator.Validate(_rootArrayNestedPayload, 12, failFast: false);

    [Benchmark]
    public void ValidateRootArrayWithoutSizeDemand() =>
        _rootArrayHasValidator.Validate(_rootArrayNestedPayload, 18, failFast: false);

    [Benchmark]
    public void ValidateMemberArrayWithoutSizeDemand() =>
        _memberArrayHasValidator.Validate(_rootArrayNestedPayload, 19, failFast: false);

    [Benchmark]
    public void ValidateMapMemberWithNestedRules() =>
        _mapMemberNestedValidator.Validate(_mapMemberNestedPayload, 13, failFast: false);

    [Benchmark]
    public void ValidateMapMemberWithoutSizeDemand() =>
        _mapMemberAggregateValidator.Validate(_mapMemberNestedPayload, 20, failFast: false);

    [Benchmark]
    public void ValidateUnionMemberWithNestedRules() =>
        _unionMemberNestedValidator.Validate(_unionMemberNestedPayload, 14, failFast: false);

    [Benchmark]
    public void ValidateMixedRootMemberWithNestedRules() =>
        _mixedRootMemberNestedValidator.Validate(
            _mixedRootMemberNestedPayload,
            15,
            failFast: false);

    [Benchmark]
    public void ValidateNestedMemberFrames() =>
        _nestedMemberFrameValidator.Validate(_nestedMemberFramePayload, 16, failFast: false);
}
