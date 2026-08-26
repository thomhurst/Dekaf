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
    private AvroInlineRuleValidator _validator = null!;
    private ReadOnlyMemory<byte> _payload;
    private AvroInlineRuleValidator _arrayEqualityValidator = null!;
    private ReadOnlyMemory<byte> _equalArrayPayload;
    private ReadOnlyMemory<byte> _segmentedArrayPayload;
    private AvroInlineRuleValidator _mapEqualityValidator = null!;
    private ReadOnlyMemory<byte> _reorderedMapPayload;

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
    public void ValidateEqualMapsWithDifferentOrder() =>
        _mapEqualityValidator.Validate(_reorderedMapPayload, 3, failFast: false);
}
