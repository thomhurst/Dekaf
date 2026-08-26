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

    [GlobalSetup]
    public void Setup()
    {
        const string schemaText = """
            {
              "type": "record",
              "name": "ValidationBenchmarkRecord",
              "confluent:rules": [{ "name": "root", "expr": "this.name == 'dekaf'" }],
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
    }

    [Benchmark]
    public void ValidateWarmedValidPayload() =>
        _validator.Validate(_payload, 1, failFast: false);
}
