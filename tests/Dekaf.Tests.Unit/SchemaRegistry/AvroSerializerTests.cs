using System.Buffers;
using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Text;
using Avro.Generic;
using Avro.IO;
using Avro.Specific;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;
using NSubstitute;
using AvroSchema = Avro.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.Tests.Unit.SchemaRegistry;

// Apache.Avro's Schema.Parse and PreresolvingDatumReader have thread-safety issues
// when multiple tests parse the same schema concurrently. Serialize Avro tests.
[NotInParallel("AvroSerialization")]
public sealed class AvroSerializerTests
{
    private static readonly int[] CustomLogicalValueTypeValues = [1, 2];

    private const string SimpleRecordSchema = """
        {
            "type": "record",
            "name": "SimpleRecord",
            "namespace": "test",
            "fields": [
                { "name": "id", "type": "int" },
                { "name": "name", "type": "string" }
            ]
        }
        """;

    private const string SpecificScalarRecordSchema = """
        {
            "type": "record",
            "name": "SpecificScalarRecord",
            "namespace": "test",
            "fields": [
                { "name": "nothing", "type": "null" },
                { "name": "enabled", "type": "boolean" },
                { "name": "count", "type": "int" },
                { "name": "sequence", "type": "long" },
                { "name": "ratio", "type": "float" },
                { "name": "total", "type": "double" },
                { "name": "name", "type": "string" },
                { "name": "payload", "type": "bytes" }
            ]
        }
        """;

    private const string SpecificArrayRecordSchema = """
        {
            "type": "record",
            "name": "SpecificArrayRecord",
            "namespace": "test",
            "fields": [
                { "name": "values", "type": { "type": "array", "items": "int" } }
            ]
        }
        """;

    private const string SpecificMissingPropertySchema = """
        {
            "type": "record",
            "name": "SpecificMissingPropertyRecord",
            "namespace": "test",
            "fields": [{ "name": "missing", "type": "int" }]
        }
        """;

    private const string SpecificMismatchedPropertySchema = """
        {
            "type": "record",
            "name": "SpecificMismatchedPropertyRecord",
            "namespace": "test",
            "fields": [{ "name": "count", "type": "int" }]
        }
        """;

    private const string SpecificCaseInsensitivePropertySchema = """
        {
            "type": "record",
            "name": "SpecificCaseInsensitivePropertyRecord",
            "namespace": "test",
            "fields": [{ "name": "userId", "type": "int" }]
        }
        """;

    private const string SpecificVirtualPropertySchema = """
        {
            "type": "record",
            "name": "SpecificVirtualPropertyRecord",
            "namespace": "test",
            "fields": [{ "name": "count", "type": "int" }]
        }
        """;

    private const string SpecificAmbiguousPropertySchema = """
        {
            "type": "record",
            "name": "SpecificAmbiguousPropertyRecord",
            "namespace": "test",
            "fields": [{ "name": "count", "type": "int" }]
        }
        """;

    private const string SpecificAliasedPropertySchema = """
        {
            "type": "record",
            "name": "SpecificAliasedPropertyRecord",
            "namespace": "test",
            "fields": [
                { "name": "Name", "type": "string" },
                { "name": "name", "type": "string" }
            ]
        }
        """;

    private const string AllFieldTypesSchema = """
        {
            "type": "record",
            "name": "AllFieldTypes",
            "namespace": "test",
            "fields": [
                { "name": "nullValue", "type": "null" },
                { "name": "booleanValue", "type": "boolean" },
                { "name": "intValue", "type": "int" },
                { "name": "longValue", "type": "long" },
                { "name": "floatValue", "type": "float" },
                { "name": "doubleValue", "type": "double" },
                { "name": "bytesValue", "type": "bytes" },
                { "name": "stringValue", "type": "string" },
                { "name": "enumValue", "type": { "type": "enum", "name": "State", "symbols": ["ON", "OFF"] } },
                { "name": "fixedValue", "type": { "type": "fixed", "name": "Hash", "size": 4 } },
                { "name": "arrayValue", "type": { "type": "array", "items": "int" } },
                { "name": "mapValue", "type": { "type": "map", "values": "string" } },
                { "name": "unionValue", "type": ["null", "string"] },
                {
                    "name": "nestedValue",
                    "type": {
                        "type": "record",
                        "name": "Nested",
                        "fields": [{ "name": "value", "type": "long" }]
                    }
                }
            ]
        }
        """;

    private const string LogicalFieldTypesSchema = """
        {
            "type": "record",
            "name": "LogicalFieldTypes",
            "namespace": "test",
            "fields": [
                { "name": "dateValue", "type": { "type": "int", "logicalType": "date" } },
                { "name": "timeMillisValue", "type": { "type": "int", "logicalType": "time-millis" } },
                { "name": "timeMicrosValue", "type": { "type": "long", "logicalType": "time-micros" } },
                { "name": "timestampMillisValue", "type": { "type": "long", "logicalType": "timestamp-millis" } },
                { "name": "timestampMicrosValue", "type": { "type": "long", "logicalType": "timestamp-micros" } },
                { "name": "localTimestampMillisValue", "type": { "type": "long", "logicalType": "local-timestamp-millis" } },
                { "name": "localTimestampMicrosValue", "type": { "type": "long", "logicalType": "local-timestamp-micros" } },
                { "name": "uuidValue", "type": { "type": "string", "logicalType": "uuid" } },
                {
                    "name": "dateArrayValue",
                    "type": { "type": "array", "items": { "type": "int", "logicalType": "date" } }
                },
                {
                    "name": "decimalBytesValue",
                    "type": { "type": "bytes", "logicalType": "decimal", "precision": 8, "scale": 2 }
                },
                {
                    "name": "fixedSeedValue",
                    "type": { "type": "fixed", "name": "DecimalFixed", "size": 8 }
                },
                {
                    "name": "decimalFixedValue",
                    "type": {
                        "type": "DecimalFixed",
                        "logicalType": "decimal",
                        "precision": 16,
                        "scale": 2
                    }
                }
            ]
        }
        """;

    private const string IntListSchema = """
        {
            "type": "record",
            "name": "IntListRecord",
            "fields": [{ "name": "values", "type": { "type": "array", "items": "int" } }]
        }
        """;

    private const string NullableIntArraySchema = """
        {
            "type": "record",
            "name": "NullableIntArrayRecord",
            "fields": [{ "name": "values", "type": { "type": "array", "items": ["null", "int"] } }]
        }
        """;

    private const string NestedRecordListSchema = """
        {
            "type": "record",
            "name": "NestedRecordList",
            "fields": [{
                "name": "values",
                "type": {
                    "type": "array",
                    "items": {
                        "type": "record",
                        "name": "NestedListValue",
                        "fields": [{ "name": "id", "type": "int" }]
                    }
                }
            }]
        }
        """;

    private const string NullableNonIntArraySchema = """
        {
          "type": "record",
          "name": "NullableNonIntArrayRecord",
          "fields": [
            { "name": "values", "type": { "type": "array", "items": ["null", "string"] } }
          ]
        }
        """;

    private const string LocalTimestampSchema = """
        {
            "type": "record",
            "name": "LocalTimestampRecord",
            "fields": [{
                "name": "value",
                "type": { "type": "long", "logicalType": "local-timestamp-micros" }
            }]
        }
        """;

    private const string TinyDecimalSchema = """
        {
            "type": "record",
            "name": "TinyDecimalRecord",
            "fields": [
                { "name": "seed", "type": { "type": "fixed", "name": "TinyDecimal", "size": 1 } },
                {
                    "name": "value",
                    "type": {
                        "type": "TinyDecimal",
                        "logicalType": "decimal",
                        "precision": 2,
                        "scale": 0
                    }
                }
            ]
        }
        """;

    private static SerializationContext CreateContext(string topic = "test-topic", bool isKey = false) =>
        new()
        {
            Topic = topic,
            Component = isKey ? SerializationComponent.Key : SerializationComponent.Value
        };

    private static byte[] SerializeAvroRecord(GenericRecord record, Avro.RecordSchema schema)
    {
        using var ms = new MemoryStream();
        var encoder = new BinaryEncoder(ms);
        var writer = new GenericDatumWriter<GenericRecord>(schema);
        writer.Write(record, encoder);
        encoder.Flush();
        return ms.ToArray();
    }

    private sealed class CapturingRuleExecutor(
        byte[]? serializedPayload = null,
        byte[]? deserializedPayload = null) : ISchemaRegistryRuleExecutor
    {
        public SchemaRegistryRuleContext? SerializeContext { get; private set; }
        public SchemaRegistryRuleContext? DeserializeContext { get; private set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            SerializeContext = context;
            return serializedPayload ?? payload;
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            DeserializeContext = context;
            return deserializedPayload ?? payload;
        }
    }

    [Test]
    public async Task Serializer_SerializesGenericRecord_WithWireFormat()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 42);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        // Act
        serializer.Serialize(record, ref buffer, context);

        // Assert
        var data = buffer.WrittenMemory;

        // Verify wire format: [magic byte] [4-byte schema ID] [Avro payload]
        await Assert.That(data.Length).IsGreaterThan(5);
        await Assert.That(data.Span[0]).IsEqualTo((byte)0x00); // Magic byte

        var schemaId = BinaryPrimitives.ReadInt32BigEndian(data.Span.Slice(1, 4));
        await Assert.That(schemaId).IsGreaterThan(0);
    }

    [Test]
    public async Task Serializer_GenericRecord_AllFieldTypes_MatchesApacheAvroBytes()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(AllFieldTypesSchema);
        var record = new GenericRecord(schema);
        var nestedSchema = (Avro.RecordSchema)schema["nestedValue"].Schema;
        var nested = new GenericRecord(nestedSchema);
        nested.Add("value", 9_876_543_210L);

        record.Add("nullValue", null!);
        record.Add("booleanValue", true);
        record.Add("intValue", -123_456);
        record.Add("longValue", 9_876_543_210L);
        record.Add("floatValue", -123.25f);
        record.Add("doubleValue", Math.PI);
        record.Add("bytesValue", new byte[] { 0, 1, 127, 128, 255 });
        record.Add("stringValue", string.Concat(Enumerable.Repeat("Grüße 🌍 ", 40)));
        record.Add("enumValue", new GenericEnum((Avro.EnumSchema)schema["enumValue"].Schema, "OFF"));
        record.Add("fixedValue", new GenericFixed((Avro.FixedSchema)schema["fixedValue"].Schema, [1, 2, 3, 4]));
        record.Add("arrayValue", new[] { int.MinValue, -1, 0, 1, int.MaxValue });
        record.Add("mapValue", new Dictionary<string, object>
        {
            ["first"] = "alpha",
            ["second"] = "βeta"
        });
        record.Add("unionValue", "selected");
        record.Add("nestedValue", nested);

        await AssertSerializedPayloadMatchesApache(serializer, schema, record);
    }

    [Test]
    public async Task Serializer_GenericRecord_NonDictionaryMap_IsRejected()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            """{"type":"record","name":"MapRecord","fields":[{"name":"mapValue","type":{"type":"map","values":"string"}}]}""");
        var record = new GenericRecord(schema);
        record.Add("mapValue", new SortedDictionary<string, object>());
        var buffer = new ArrayBufferWriter<byte>();

        Assert.Throws<Avro.AvroTypeException>(
            () => serializer.Serialize(record, ref buffer, CreateContext()));
    }

    [Test]
    public async Task Serializer_GenericRecord_LogicalFieldTypes_MatchesApacheAvroBytes()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(LogicalFieldTypesSchema);
        var record = new GenericRecord(schema);
        var timestamp = new DateTime(2026, 8, 16, 14, 23, 45, 678, DateTimeKind.Utc).AddTicks(9_010);

        record.Add("dateValue", new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc));
        record.Add("timeMillisValue", new TimeSpan(0, 14, 23, 45, 678));
        record.Add("timeMicrosValue", new TimeSpan(0, 14, 23, 45, 678).Add(TimeSpan.FromTicks(9_010)));
        record.Add("timestampMillisValue", timestamp);
        record.Add("timestampMicrosValue", timestamp);
        record.Add("localTimestampMillisValue", timestamp);
        record.Add("localTimestampMicrosValue", timestamp);
        record.Add("uuidValue", Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
        record.Add("dateArrayValue", new[]
        {
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc)
        });
        record.Add("decimalBytesValue", new Avro.AvroDecimal(new BigInteger(-12_345), 2));
        record.Add(
            "fixedSeedValue",
            new GenericFixed((Avro.FixedSchema)schema["fixedSeedValue"].Schema, new byte[8]));
        record.Add("decimalFixedValue", new Avro.AvroDecimal(new BigInteger(98_765), 2));

        await AssertSerializedPayloadMatchesApache(serializer, schema, record);
    }

    [Test]
    public async Task Serializer_GenericRecord_LogicalUnion_UsesFirstCompatibleBranch()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            """
            {
                "type": "record",
                "name": "LogicalUnionRecord",
                "fields": [{
                    "name": "value",
                    "type": [
                        { "type": "int", "logicalType": "date" },
                        { "type": "long", "logicalType": "timestamp-millis" }
                    ]
                }]
            }
            """);
        var record = new GenericRecord(schema);
        record.Add("value", new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc));

        await AssertSerializedPayloadMatchesApache(serializer, schema, record);
    }

    [Test]
    public async Task Serializer_GenericRecord_SelectedCustomLogicalBranch_IsRejected()
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new StringBytesLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "CustomLogicalUnionRecord",
                "fields": [{
                    "name": "value",
                    "type": [
                        { "type": "bytes", "logicalType": "{{StringBytesLogicalType.LogicalName}}" },
                        "string"
                    ]
                }]
            }
            """);
        var record = new GenericRecord(schema);
        record.Add("value", "logical-value");

        await Assert.That(SerializeLogical).Throws<Avro.AvroTypeException>();

        var primitiveRecord = new GenericRecord(schema);
        primitiveRecord.Add("value", "primitive-value");
        await AssertSerializedPayloadMatchesApache(serializer, schema, primitiveRecord);

        void SerializeLogical()
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(record, ref buffer, CreateContext());
        }
    }

    [Test]
    public async Task Serializer_GenericRecord_SelectedMultipleCustomLogicalBranches_AreRejected()
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new StringBytesLogicalType());
        Avro.Util.LogicalTypeFactory.Instance.Register(new StringTextLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "MultipleCustomLogicalUnionRecord",
                "fields": [{
                    "name": "value",
                    "type": [
                        { "type": "bytes", "logicalType": "{{StringBytesLogicalType.LogicalName}}" },
                        { "type": "string", "logicalType": "{{StringTextLogicalType.LogicalName}}" }
                    ]
                }]
            }
            """);
        var bytesRecord = new GenericRecord(schema);
        bytesRecord.Add("value", "logical-bytes");
        var textRecord = new GenericRecord(schema);
        textRecord.Add("value", "text-value");

        await Assert.That(() => Serialize(bytesRecord)).Throws<Avro.AvroTypeException>();
        await Assert.That(() => Serialize(textRecord)).Throws<Avro.AvroTypeException>();

        void Serialize(GenericRecord record)
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(record, ref buffer, CreateContext());
        }
    }

    [Test]
    public async Task Serializer_GenericRecord_CustomLogicalValueTypeUnionArray_IsRejected()
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new IntBytesLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "CustomLogicalValueTypeArrayRecord",
                "fields": [{
                    "name": "values",
                    "type": {
                        "type": "array",
                        "items": [
                            { "type": "bytes", "logicalType": "{{IntBytesLogicalType.LogicalName}}" },
                            "int"
                        ]
                    }
                }]
            }
            """);
        var record = new GenericRecord(schema);
        record.Add("values", CustomLogicalValueTypeValues);

        await Assert.That(Serialize).Throws<Avro.AvroTypeException>();

        void Serialize()
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(record, ref buffer, CreateContext());
        }
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Serializer_GenericRecord_CustomLogicalValueTypeList_IsRejected(int collectionKind)
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new IntBytesLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "CustomLogicalValueTypeListRecord",
                "fields": [{
                    "name": "values",
                    "type": {
                        "type": "array",
                        "items": { "type": "bytes", "logicalType": "{{IntBytesLogicalType.LogicalName}}" }
                    }
                }]
            }
            """);
        object values = collectionKind switch
        {
            0 => new List<int> { 1, 2 },
            1 => new Collection<int>([1, 2]),
            2 => new NonGenericIntValues(),
            _ => throw new ArgumentOutOfRangeException(nameof(collectionKind))
        };
        var record = new GenericRecord(schema);
        record.Add("values", values);

        await Assert.That(Serialize).Throws<Avro.AvroTypeException>();

        void Serialize()
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(record, ref buffer, CreateContext());
        }
    }

    [Test]
    public async Task Serializer_GenericRecord_CustomLogicalUnionNonGenericValueTypeList_IsRejected()
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new IntBytesLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "CustomLogicalUnionNonGenericValueTypeListRecord",
                "fields": [{
                    "name": "values",
                    "type": {
                        "type": "array",
                        "items": [
                            { "type": "bytes", "logicalType": "{{IntBytesLogicalType.LogicalName}}" },
                            "string"
                        ]
                    }
                }]
            }
            """);
        var record = new GenericRecord(schema);
        record.Add("values", new NonGenericIntValues());

        await Assert.That(Serialize).Throws<Avro.AvroTypeException>();

        void Serialize()
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(record, ref buffer, CreateContext());
        }
    }

    [Test]
    public async Task Serializer_GenericRecord_CustomStructUnionList_IsRejectedBeforeIndexing()
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new CustomStructBytesLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "CustomStructUnionListRecord",
                "fields": [{
                    "name": "values",
                    "type": {
                        "type": "array",
                        "items": [
                            { "type": "bytes", "logicalType": "{{CustomStructBytesLogicalType.LogicalName}}" },
                            "string"
                        ]
                    }
                }]
            }
            """);
        var record = new GenericRecord(schema);
        record.Add("values", new Collection<CustomLogicalValue>([new(1)]));

        await Assert.That(Serialize).Throws<Avro.AvroTypeException>();

        void Serialize()
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(record, ref buffer, CreateContext());
        }
    }

    [Test]
    [Arguments(0, false)]
    [Arguments(0, true)]
    [Arguments(1, false)]
    [Arguments(1, true)]
    public async Task Serializer_GenericRecord_CustomStructUnionReferenceList_Serializes(
        int collectionKind,
        bool includeValue)
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new CustomStructBytesLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "CustomStructUnionReferenceListRecord",
                "fields": [{
                    "name": "values",
                    "type": {
                        "type": "array",
                        "items": [
                            { "type": "bytes", "logicalType": "{{CustomStructBytesLogicalType.LogicalName}}" },
                            "string"
                        ]
                    }
                }]
            }
            """);
        System.Collections.IList values = collectionKind switch
        {
            0 => includeValue ? new List<string> { "value" } : new List<string>(),
            1 => includeValue ? new System.Collections.ArrayList { "value" } : [],
            _ => throw new ArgumentOutOfRangeException(nameof(collectionKind))
        };
        var record = new GenericRecord(schema);
        record.Add("values", values);
        var expectedRecord = new GenericRecord(schema);
        expectedRecord.Add("values", includeValue ? new object[] { "value" } : []);

        await AssertSerializedPayloadMatches(serializer, schema, record, expectedRecord);
    }

    [Test]
    public async Task Serializer_GenericRecord_CustomStructUnionGenericRecordList_Serializes()
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new CustomStructBytesLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "CustomStructUnionGenericRecordListRecord",
                "fields": [{
                    "name": "values",
                    "type": {
                        "type": "array",
                        "items": [
                            { "type": "bytes", "logicalType": "{{CustomStructBytesLogicalType.LogicalName}}" },
                            { "type": "record", "name": "Nested", "fields": [{ "name": "id", "type": "int" }] }
                        ]
                    }
                }]
            }
            """);
        var nestedSchema = (Avro.RecordSchema)((Avro.UnionSchema)((Avro.ArraySchema)schema.Fields[0].Schema).ItemSchema)[1];
        var nested = new GenericRecord(nestedSchema);
        nested.Add("id", 42);
        var record = new GenericRecord(schema);
        record.Add("values", new List<GenericRecord> { nested });
        var expectedRecord = new GenericRecord(schema);
        expectedRecord.Add("values", new object[] { nested });

        await AssertSerializedPayloadMatches(serializer, schema, record, expectedRecord);
    }

    [Test]
    [Arguments(0, false)]
    [Arguments(0, true)]
    [Arguments(1, false)]
    [Arguments(1, true)]
    [Arguments(2, false)]
    [Arguments(2, true)]
    public async Task Serializer_GenericRecord_ConditionalNullableUnionCollection_AcceptsEmptyOrNulls(
        int collectionKind,
        bool includeNull)
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new IntBytesLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "ConditionalNullableUnionCollectionRecord",
                "fields": [{
                    "name": "values",
                    "type": {
                        "type": "array",
                        "items": [
                            "null",
                            { "type": "bytes", "logicalType": "{{IntBytesLogicalType.LogicalName}}" }
                        ]
                    }
                }]
            }
            """);
        var nullableValues = includeNull ? new int?[] { null } : [];
        object values = collectionKind switch
        {
            0 => nullableValues,
            1 => new List<int?>(nullableValues),
            2 => new Collection<int?>(nullableValues),
            _ => throw new ArgumentOutOfRangeException(nameof(collectionKind))
        };
        var record = new GenericRecord(schema);
        record.Add("values", values);
        var expectedRecord = new GenericRecord(schema);
        expectedRecord.Add("values", includeNull ? new object?[] { null } : []);

        await AssertSerializedPayloadMatches(serializer, schema, record, expectedRecord);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Serializer_GenericRecord_CustomLogicalAndStructuralBranches_UseSchemaOrder(
        bool arrayFirst)
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new IntListBytesLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var branches = arrayFirst
            ? $$"""{ "type": "array", "items": "int" }, { "type": "bytes", "logicalType": "{{IntListBytesLogicalType.LogicalName}}" }"""
            : $$"""{ "type": "bytes", "logicalType": "{{IntListBytesLogicalType.LogicalName}}" }, { "type": "array", "items": "int" }""";
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "CustomLogicalStructuralUnionRecord",
                "fields": [{ "name": "value", "type": [{{branches}}] }]
            }
            """);
        var record = new GenericRecord(schema);
        var values = arrayFirst ? new[] { -1, 2 } : new[] { 1, 2 };
        record.Add("value", new List<int>(values));
        var expectedRecord = new GenericRecord(schema);
        expectedRecord.Add("value", values);

        await AssertSerializedPayloadMatches(serializer, schema, record, expectedRecord);
    }

    [Test]
    public async Task Serializer_GenericRecord_SelectedAssignableCustomLogicalBranch_IsRejected()
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new IntListBytesLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "AssignableCustomLogicalUnionRecord",
                "fields": [{
                    "name": "value",
                    "type": [
                        { "type": "bytes", "logicalType": "{{IntListBytesLogicalType.LogicalName}}" },
                        { "type": "array", "items": "int" }
                    ]
                }]
            }
            """);
        var record = new GenericRecord(schema);
        record.Add("value", new List<int> { -1, 2 });

        await Assert.That(Serialize).Throws<Avro.AvroTypeException>();

        void Serialize()
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(record, ref buffer, CreateContext());
        }
    }

    [Test]
    [Arguments(false, "plain-value", false)]
    [Arguments(false, "logical-value", true)]
    [Arguments(true, "logical-value", false)]
    public async Task Serializer_GenericRecord_AssignableLogicalAndPrimitiveBranches_UseSchemaOrder(
        bool stringFirst,
        string value,
        bool rejectsCustomLogicalBranch)
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new ComparableBytesLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var logicalBranch = $$"""{ "type": "bytes", "logicalType": "{{ComparableBytesLogicalType.LogicalName}}" }""";
        var branches = stringFirst ? $"\"string\", {logicalBranch}" : $"{logicalBranch}, \"string\"";
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "AssignableLogicalPrimitiveUnionRecord",
                "fields": [{ "name": "value", "type": [{{branches}}] }]
            }
            """);
        var record = new GenericRecord(schema);
        record.Add("value", value);

        if (rejectsCustomLogicalBranch)
        {
            await Assert.That(Serialize).Throws<Avro.AvroTypeException>();
            return;
        }

        await AssertSerializedPayloadMatchesApache(serializer, schema, record);

        void Serialize()
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(record, ref buffer, CreateContext());
        }
    }

    [Test]
    public async Task Serializer_GenericRecord_SelectedExactAndAssignableLogicalBranch_IsRejected()
    {
        Avro.Util.LogicalTypeFactory.Instance.Register(new IntListBytesLogicalType());
        Avro.Util.LogicalTypeFactory.Instance.Register(new IntListStringLogicalType());
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "ExactAndAssignableLogicalUnionRecord",
                "fields": [{
                    "name": "value",
                    "type": [
                        { "type": "bytes", "logicalType": "{{IntListBytesLogicalType.LogicalName}}" },
                        { "type": "string", "logicalType": "{{IntListStringLogicalType.LogicalName}}" }
                    ]
                }]
            }
            """);
        var record = new GenericRecord(schema);
        record.Add("value", new List<int> { -1, 2 });

        await Assert.That(Serialize).Throws<Avro.AvroTypeException>();

        void Serialize()
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(record, ref buffer, CreateContext());
        }
    }

    [Test]
    public async Task Serializer_GenericRecord_ListArray_MatchesApacheAvroBytes()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(IntListSchema);
        var record = new GenericRecord(schema);
        record.Add("values", new List<int> { int.MinValue, -1, 0, 1, int.MaxValue });
        var expectedRecord = new GenericRecord(schema);
        expectedRecord.Add("values", new[] { int.MinValue, -1, 0, 1, int.MaxValue });

        await AssertSerializedPayloadMatches(serializer, schema, record, expectedRecord);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Serializer_GenericRecord_NullableUnionCollection_MatchesApacheAvroBytes(bool useList)
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(NullableIntArraySchema);
        var record = new GenericRecord(schema);
        record.Add("values", useList
            ? new List<int?> { int.MinValue, null, 0, int.MaxValue }
            : new int?[] { int.MinValue, null, 0, int.MaxValue });
        var expectedRecord = new GenericRecord(schema);
        expectedRecord.Add("values", new int?[] { int.MinValue, null, 0, int.MaxValue });

        await AssertSerializedPayloadMatches(serializer, schema, record, expectedRecord);
    }

    [Test]
    public async Task Serializer_GenericRecord_NullableUnionCollectionIList_MatchesApacheAvroBytes()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(NullableIntArraySchema);
        var record = new GenericRecord(schema);
        record.Add(
            "values",
            new Collection<int?>([int.MinValue, null, 0, int.MaxValue]));
        var expectedRecord = new GenericRecord(schema);
        expectedRecord.Add("values", new int?[] { int.MinValue, null, 0, int.MaxValue });

        await AssertSerializedPayloadMatches(serializer, schema, record, expectedRecord);
    }

    [Test]
    [Arguments("string", 0)]
    [Arguments("string", 1)]
    [Arguments("string", 2)]
    [Arguments("bytes", 0)]
    [Arguments("bytes", 1)]
    [Arguments("bytes", 2)]
    public async Task Serializer_GenericRecord_NullInNonNullableReferenceCollection_IsRejected(
        string itemType,
        int collectionKind)
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(
            $$"""
            {
                "type": "record",
                "name": "NonNullableReferenceArray",
                "fields": [{ "name": "values", "type": { "type": "array", "items": "{{itemType}}" } }]
            }
            """);
        object values = (itemType, collectionKind) switch
        {
            ("string", 0) => new string?[] { "first", null, "second" },
            ("string", 1) => new List<string?> { "first", null, "second" },
            ("string", 2) => new Collection<string?>(["first", null, "second"]),
            ("bytes", 0) => new byte[]?[] { new byte[] { 1 }, null, new byte[] { 2 } },
            ("bytes", 1) => new List<byte[]?> { new byte[] { 1 }, null, new byte[] { 2 } },
            ("bytes", 2) => new Collection<byte[]?>(
                new byte[]?[] { new byte[] { 1 }, null, new byte[] { 2 } }),
            _ => throw new ArgumentOutOfRangeException(nameof(collectionKind))
        };
        var record = new GenericRecord(schema);
        record.Add("values", values);

        await Assert.That(Serialize).Throws<Avro.AvroTypeException>();

        void Serialize()
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(record, ref buffer, CreateContext());
        }
    }

    [Test]
    public async Task Serializer_GenericRecord_NestedRecordList_MatchesApacheAvroBytes()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(NestedRecordListSchema);
        var itemSchema = (Avro.RecordSchema)((Avro.ArraySchema)schema.Fields[0].Schema).ItemSchema;
        var first = new GenericRecord(itemSchema);
        first.Add("id", 1);
        var second = new GenericRecord(itemSchema);
        second.Add("id", 2);
        var record = new GenericRecord(schema);
        record.Add("values", new List<GenericRecord> { first, second });
        var expectedRecord = new GenericRecord(schema);
        expectedRecord.Add("values", new[] { first, second });

        await AssertSerializedPayloadMatches(serializer, schema, record, expectedRecord);
    }

    [Test]
    [Arguments(0, false)]
    [Arguments(0, true)]
    [Arguments(1, false)]
    [Arguments(1, true)]
    [Arguments(2, false)]
    [Arguments(2, true)]
    public async Task Serializer_GenericRecord_NullableUnionWithoutValueBranch_AcceptsEmptyOrNulls(
        int collectionKind,
        bool includeNull)
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(NullableNonIntArraySchema);
        var nullableValues = includeNull ? new int?[] { null } : [];
        object values = collectionKind switch
        {
            0 => nullableValues,
            1 => new List<int?>(nullableValues),
            2 => new Collection<int?>(nullableValues),
            _ => throw new ArgumentOutOfRangeException(nameof(collectionKind))
        };
        var record = new GenericRecord(schema);
        record.Add("values", values);
        var expectedRecord = new GenericRecord(schema);
        expectedRecord.Add("values", includeNull ? new object?[] { null } : []);

        await AssertSerializedPayloadMatches(serializer, schema, record, expectedRecord);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Serializer_GenericRecord_NonNullableUnionWithoutValueBranch_AcceptsEmpty(
        int collectionKind)
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(NullableNonIntArraySchema);
        object values = collectionKind switch
        {
            0 => Array.Empty<int>(),
            1 => new List<int>(),
            2 => new Collection<int>(),
            _ => throw new ArgumentOutOfRangeException(nameof(collectionKind))
        };
        var record = new GenericRecord(schema);
        record.Add("values", values);
        var expectedRecord = new GenericRecord(schema);
        expectedRecord.Add("values", Array.Empty<object>());

        await AssertSerializedPayloadMatches(serializer, schema, record, expectedRecord);
    }

    [Test]
    public async Task Serializer_GenericRecord_LocalTimestamp_PreservesWallClock()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(LocalTimestampSchema);
        var value = new DateTime(2026, 8, 16, 14, 23, 45, 678, DateTimeKind.Local).AddTicks(9_010);
        var record = new GenericRecord(schema);
        record.Add("value", value);
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(record, ref buffer, CreateContext());

        using var payload = new MemoryStream(buffer.WrittenSpan.Slice(5).ToArray());
        var decoder = new BinaryDecoder(payload);
        var expected = (DateTime.SpecifyKind(value, DateTimeKind.Unspecified) -
                        new DateTime(1970, 1, 1)).Ticks / 10;
        await Assert.That(decoder.ReadLong()).IsEqualTo(expected);
    }

    [Test]
    [Arguments(128)]
    [Arguments(-129)]
    public async Task Serializer_GenericRecord_FixedDecimalOverflow_IsRejected(int unscaledValue)
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        var schema = (Avro.RecordSchema)AvroSchema.Parse(TinyDecimalSchema);
        var record = new GenericRecord(schema);
        record.Add("seed", new GenericFixed((Avro.FixedSchema)schema["seed"].Schema, [0]));
        record.Add("value", new Avro.AvroDecimal(new BigInteger(unscaledValue), 0));

        await Assert.That(Serialize).Throws<ArgumentOutOfRangeException>();

        void Serialize()
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(record, ref buffer, CreateContext());
        }
    }

    [Test]
    public async Task Serializer_RuleExecutor_TransformsAvroPayload()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 42);
        record.Add("name", "plain");

        var replacement = new GenericRecord(schema!);
        replacement.Add("id", 99);
        replacement.Add("name", "encrypted");
        var replacementPayload = SerializeAvroRecord(replacement, schema!);
        var executor = new CapturingRuleExecutor(serializedPayload: replacementPayload);
        var config = new AvroSerializerConfig { RuleExecutor = executor };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry, config);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(record, ref buffer, CreateContext());

        await Assert.That(buffer.WrittenSpan.Slice(5).ToArray()).IsEquivalentTo(replacementPayload);
        await Assert.That(executor.SerializeContext).IsNotNull();
        await Assert.That(executor.SerializeContext!.PayloadFormat).IsEqualTo(SchemaRegistryPayloadFormat.Avro);
        await Assert.That(executor.SerializeContext.Subject).IsEqualTo("test-topic-value");
        await Assert.That(executor.SerializeContext.SchemaId).IsGreaterThan(0);
        await Assert.That(executor.SerializeContext.Schema).IsNotNull();
        await Assert.That(executor.SerializeContext.Schema!.SchemaType).IsEqualTo(SchemaType.Avro);
        await Assert.That(executor.SerializeContext.Schema.SchemaString).IsEqualTo(schema!.ToString());
    }

    [Test]
    public async Task Deserializer_DeserializesGenericRecord_FromWireFormat()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();

        // Pre-register schema
        var schemaObj = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("test-topic-value", schemaObj);

        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        // Warm up the deserializer cache with the schema
        await deserializer.WarmupAsync(schemaId);

        // Serialize a record using the Avro library for correct binary encoding
        var avroSchema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(avroSchema!);
        record.Add("id", 42);
        record.Add("name", "test");
        var avroPayload = SerializeAvroRecord(record, avroSchema!);

        // Create wire format
        var wireFormat = new byte[1 + 4 + avroPayload.Length];
        wireFormat[0] = 0x00; // Magic byte
        BinaryPrimitives.WriteInt32BigEndian(wireFormat.AsSpan(1, 4), schemaId);
        avroPayload.CopyTo(wireFormat.AsSpan(5));

        var context = CreateContext();

        // Act
        var result = deserializer.Deserialize(wireFormat, context);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That((int)result["id"]!).IsEqualTo(42);
        await Assert.That((string)result["name"]!).IsEqualTo("test");
    }

    [Test]
    public async Task Deserializer_DeserializesGenericRecord_FromSlicedWireMemory()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        var schemaObj = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("test-topic-value", schemaObj);

        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);
        await deserializer.WarmupAsync(schemaId);

        var avroSchema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(avroSchema!);
        record.Add("id", 64);
        record.Add("name", "offset");

        var wireFormat = CreateWireFormat(schemaId, SerializeAvroRecord(record, avroSchema!));
        var offset = 7;
        var backing = new byte[offset + wireFormat.Length + 3];
        wireFormat.CopyTo(backing.AsSpan(offset));

        var result = deserializer.Deserialize(backing.AsMemory(offset, wireFormat.Length), CreateContext());

        await Assert.That((int)result["id"]!).IsEqualTo(64);
        await Assert.That((string)result["name"]!).IsEqualTo("offset");
    }

    [Test]
    public async Task Deserializer_RuleExecutor_TransformsAvroPayload()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        var schemaObj = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("test-topic-value", schemaObj);

        var avroSchema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var replacement = new GenericRecord(avroSchema!);
        replacement.Add("id", 7);
        replacement.Add("name", "plain");
        var replacementPayload = SerializeAvroRecord(replacement, avroSchema!);
        var wireFormat = CreateWireFormat(schemaId, "encrypted"u8.ToArray());
        var executor = new CapturingRuleExecutor(deserializedPayload: replacementPayload);
        var config = new AvroDeserializerConfig { RuleExecutor = executor };
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry, config);

        var result = deserializer.Deserialize(wireFormat, CreateContext());

        await Assert.That((int)result["id"]!).IsEqualTo(7);
        await Assert.That((string)result["name"]!).IsEqualTo("plain");
        await Assert.That(executor.DeserializeContext).IsNotNull();
        await Assert.That(executor.DeserializeContext!.PayloadFormat).IsEqualTo(SchemaRegistryPayloadFormat.Avro);
        await Assert.That(executor.DeserializeContext.SchemaId).IsEqualTo(schemaId);
        await Assert.That(executor.DeserializeContext.Schema).IsSameReferenceAs(schemaObj);
    }

    [Test]
    public async Task Serializer_RoundTrips_GenericRecord()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 123);
        record.Add("name", "round trip test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        // Act
        serializer.Serialize(record, ref buffer, context);
        var result = deserializer.Deserialize(buffer.WrittenMemory, context);

        // Assert
        await Assert.That((int)result["id"]!).IsEqualTo(123);
        await Assert.That((string)result["name"]!).IsEqualTo("round trip test");
    }

    [Test]
    public async Task Serializer_RoundTrips_GenericRecord_LargerThanInitialBuffer()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        var name = new string('x', 5000);
        record.Add("id", 321);
        record.Add("name", name);

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(record, ref buffer, CreateContext());

        var result = deserializer.Deserialize(buffer.WrittenMemory, CreateContext());

        await Assert.That((int)result["id"]!).IsEqualTo(321);
        await Assert.That((string)result["name"]!).IsEqualTo(name);
    }

    [Test]
    public async Task Serializer_CachesSchemaId_ForSameSubject()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record1 = new GenericRecord(schema!);
        record1.Add("id", 1);
        record1.Add("name", "first");

        var record2 = new GenericRecord(schema!);
        record2.Add("id", 2);
        record2.Add("name", "second");

        var buffer1 = new ArrayBufferWriter<byte>();
        var buffer2 = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        // Act
        serializer.Serialize(record1, ref buffer1, context);
        serializer.Serialize(record2, ref buffer2, context);

        // Assert - both should have same schema ID
        var schemaId1 = BinaryPrimitives.ReadInt32BigEndian(buffer1.WrittenSpan.Slice(1, 4));
        var schemaId2 = BinaryPrimitives.ReadInt32BigEndian(buffer2.WrittenSpan.Slice(1, 4));
        await Assert.That(schemaId1).IsEqualTo(schemaId2);
    }

    [Test]
    public async Task Serializer_NormalizeSchemas_PassesNormalizeToRegistry()
    {
        var schemaRegistry = Substitute.For<ISchemaRegistryClient>();
        schemaRegistry.GetOrRegisterSchemaAsync(
                Arg.Any<string>(),
                Arg.Any<RegistrySchema>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(123));
        var config = new AvroSerializerConfig { NormalizeSchemas = true };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry, config);
        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "normalized");

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(record, ref buffer, CreateContext());

        await schemaRegistry.Received(1).GetOrRegisterSchemaAsync(
            Arg.Any<string>(),
            Arg.Any<RegistrySchema>(),
            true,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Serializer_CachesGenericDatumWriter_ForSameSchema()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record1 = new GenericRecord(schema!);
        record1.Add("id", 1);
        record1.Add("name", "first");

        var record2 = new GenericRecord(schema!);
        record2.Add("id", 2);
        record2.Add("name", "second");

        var buffer1 = new ArrayBufferWriter<byte>();
        var buffer2 = new ArrayBufferWriter<byte>();
        var context = CreateContext();

        serializer.Serialize(record1, ref buffer1, context);
        serializer.Serialize(record2, ref buffer2, context);

        await Assert.That(serializer.CachedGenericWriterCount).IsEqualTo(1);
        await Assert.That(serializer.CachedSpecificWriterCount).IsEqualTo(0);
    }

    [Test]
    public async Task Serializer_SpecificRecord_ScalarFieldsMatchApacheAvroBytes()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<SpecificScalarRecord>(schemaRegistry);
        var record = new SpecificScalarRecord
        {
            Enabled = true,
            Count = -42,
            Sequence = long.MaxValue,
            Ratio = 1.25f,
            Total = -123.5,
            Name = "specific",
            Payload = [0, 1, 127, 255]
        };
        var expected = SerializeSpecificAvroRecord(record);
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(record, ref buffer, CreateContext());

        await Assert.That(buffer.WrittenSpan.Slice(5).SequenceEqual(expected)).IsTrue();
        await Assert.That(serializer.CachedSpecificWriterCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Serializer_SpecificRecord_NonNullableReferenceFieldRejectsNull(bool nullBytes)
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<SpecificScalarRecord>(schemaRegistry);
        var record = new SpecificScalarRecord
        {
            Name = nullBytes ? "specific" : null!,
            Payload = nullBytes ? null! : []
        };

        await Assert.That(Serialize).Throws<Avro.AvroTypeException>();

        void Serialize()
        {
            var buffer = new ArrayBufferWriter<byte>();
            serializer.Serialize(record, ref buffer, CreateContext());
        }
    }

    [Test]
    public async Task Serializer_InterfaceTypedSpecificRecord_FailsDuringPreparation()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();

        var exception = Assert.Throws<NotSupportedException>(
            () => GC.KeepAlive(new AvroSchemaRegistrySerializer<ISpecificRecord>(schemaRegistry)));

        await Assert.That(exception!.Message).Contains("trimming-unsafe runtime type discovery");
    }

    [Test]
    public async Task Serializer_SpecificRecord_UnsupportedFieldFailsDuringPreparation()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();

        var exception = Assert.Throws<NotSupportedException>(
            () => GC.KeepAlive(new AvroSchemaRegistrySerializer<SpecificArrayRecord>(schemaRegistry)));

        await Assert.That(exception!.Message).Contains("schema type Array is not supported");
    }

    [Test]
    public async Task Serializer_SpecificRecord_MissingPropertyFailsDuringPreparation()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();

        var exception = Assert.Throws<NotSupportedException>(
            () => GC.KeepAlive(new AvroSchemaRegistrySerializer<SpecificMissingPropertyRecord>(schemaRegistry)));

        await Assert.That(exception!.Message).Contains("a readable public property named 'missing' was not found");
    }

    [Test]
    public async Task Serializer_SpecificRecord_MismatchedPropertyTypeFailsDuringPreparation()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();

        var exception = Assert.Throws<NotSupportedException>(
            () => GC.KeepAlive(new AvroSchemaRegistrySerializer<SpecificMismatchedPropertyRecord>(schemaRegistry)));

        await Assert.That(exception!.Message).Contains("has type System.Int64, expected System.Int32");
    }

    [Test]
    public async Task Serializer_SpecificRecord_CaseInsensitivePropertyMatchesSchemaField()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<SpecificCaseInsensitivePropertyRecord>(schemaRegistry);
        var record = new SpecificCaseInsensitivePropertyRecord { UserId = 7 };
        var expected = SerializeSpecificAvroRecord(record);
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(record, ref buffer, CreateContext());

        await Assert.That(buffer.WrittenSpan.Slice(5).SequenceEqual(expected)).IsTrue();
    }

    [Test]
    public async Task Serializer_SpecificRecord_OverridablePropertyFailsDuringPreparation()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        GC.KeepAlive(new SpecificVirtualPropertyDerivedRecord());

        var exception = Assert.Throws<NotSupportedException>(
            () => GC.KeepAlive(new AvroSchemaRegistrySerializer<SpecificVirtualPropertyRecord>(schemaRegistry)));

        await Assert.That(exception!.Message).Contains("property 'Count' has an overridable getter");
    }

    [Test]
    public async Task Serializer_SpecificRecord_AmbiguousPropertyFailsDuringPreparation()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();

        var exception = Assert.Throws<NotSupportedException>(
            () => GC.KeepAlive(new AvroSchemaRegistrySerializer<SpecificAmbiguousPropertyRecord>(schemaRegistry)));

        await Assert.That(exception!.Message).Contains("multiple public properties match 'count' ignoring case");
    }

    [Test]
    public async Task Serializer_SpecificRecord_PropertyCannotMatchMultipleSchemaFields()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();

        var exception = Assert.Throws<NotSupportedException>(
            () => GC.KeepAlive(new AvroSchemaRegistrySerializer<SpecificAliasedPropertyRecord>(schemaRegistry)));

        await Assert.That(exception!.Message).Contains("property 'Name' also matches another schema field");
    }

    [Test]
    public async Task Serializer_UsesTopicNameStrategy_ByDefault()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("my-topic");

        // Act
        serializer.Serialize(record, ref buffer, context);

        // Assert - schema should be registered under "my-topic-value"
        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("my-topic-value");
    }

    [Test]
    public async Task Serializer_UsesKeySubject_ForKeyComponent()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "test");

        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("my-topic", isKey: true);

        // Act
        serializer.Serialize(record, ref buffer, context);

        // Assert - schema should be registered under "my-topic-key"
        var subjects = await schemaRegistry.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains("my-topic-key");
    }

    [Test]
    public async Task Deserializer_ThrowsOnInvalidMagicByte()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        var invalidData = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01, 0x00 }; // Wrong magic byte
        var context = CreateContext();

        // Act & Assert
        await Assert.That(() => deserializer.Deserialize(invalidData, context))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("magic byte");
    }

    [Test]
    public async Task Deserializer_ThrowsOnTooShortData()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        var shortData = new byte[] { 0x00, 0x01, 0x02 }; // Less than 5 bytes
        var context = CreateContext();

        // Act & Assert
        await Assert.That(() => deserializer.Deserialize(shortData, context))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("too short");
    }

    [Test]
    public async Task Config_AutoRegisterSchemas_DefaultsToTrue()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.AutoRegisterSchemas).IsTrue();
    }

    [Test]
    public async Task Config_SubjectNameStrategy_DefaultsToTopicName()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.SubjectNameStrategy).IsEqualTo(SubjectNameStrategy.TopicName);
    }

    [Test]
    public async Task Config_UseLatestVersion_DefaultsToFalse()
    {
        var config = new AvroSerializerConfig();
        await Assert.That(config.UseLatestVersion).IsFalse();
    }

    [Test]
    public async Task Serializer_WarmupAsync_PreCachesSchemaId()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "warmup");

        // Act - warm up the cache
        var warmupSchemaId = await serializer.WarmupAsync("warmup-topic", record, isKey: false);

        // Serialize using the warmed-up cache
        var buffer = new ArrayBufferWriter<byte>();
        var context = CreateContext("warmup-topic");
        serializer.Serialize(record, ref buffer, context);

        // Assert - the schema ID from warmup should match the one used in serialization
        var serializedSchemaId = BinaryPrimitives.ReadInt32BigEndian(buffer.WrittenSpan.Slice(1, 4));
        await Assert.That(serializedSchemaId).IsEqualTo(warmupSchemaId);
    }

    [Test]
    public async Task Deserializer_WarmupAsync_PreCachesSchema()
    {
        // Arrange
        using var schemaRegistry = new MockSchemaRegistryClient();

        // Pre-register schema
        var schemaObj = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("test-topic-value", schemaObj);

        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        // Act - warm up the cache
        var warmedSchema = await deserializer.WarmupAsync(schemaId);

        // Assert - schema was fetched and cached
        await Assert.That(warmedSchema).IsNotNull();
        await Assert.That(warmedSchema.Fullname).IsEqualTo("test.SimpleRecord");

        // Construct wire format manually using the known schemaId to avoid
        // non-determinism from a second serializer interacting with the mock registry
        var avroSchema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var originalRecord = new GenericRecord(avroSchema!);
        originalRecord.Add("id", 42);
        originalRecord.Add("name", "warmup-test");

        var avroPayload = SerializeAvroRecord(originalRecord, avroSchema!);
        var wireFormat = new byte[1 + 4 + avroPayload.Length];
        wireFormat[0] = 0x00; // Magic byte
        BinaryPrimitives.WriteInt32BigEndian(wireFormat.AsSpan(1, 4), schemaId);
        avroPayload.CopyTo(wireFormat.AsSpan(5));

        // Deserialize using the warmed-up cache
        var desContext = CreateContext();
        var result = deserializer.Deserialize(wireFormat, desContext);

        // Verify deserialization worked correctly
        await Assert.That((int)result["id"]!).IsEqualTo(42);
        await Assert.That((string)result["name"]!).IsEqualTo("warmup-test");
    }

    [Test]
    public async Task Serializer_WarmupAsync_RetriesAfterTransientSchemaIdFailure()
    {
        using var schemaRegistry = new MockSchemaRegistryClient
        {
            GetOrRegisterSchemaFailuresRemaining = 1
        };
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "retry");

        await Assert.That(async () => await serializer.WarmupAsync("retry-topic", record))
            .Throws<SchemaRegistryException>();

        var schemaId = await serializer.WarmupAsync("retry-topic", record);

        await Assert.That(schemaId).IsGreaterThan(0);
        await Assert.That(schemaRegistry.GetOrRegisterSchemaCallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Serializer_WarmupAsync_DoesNotBindSharedFetchToFirstCallerCancellation()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        schemaRegistry.BlockNextGetOrRegisterSchema();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "shared-cancellation");

        using var firstWaiterCts = new CancellationTokenSource();
        var firstWaiter = serializer.WarmupAsync("shared-topic", record, cancellationToken: firstWaiterCts.Token);
        await schemaRegistry.WaitForBlockedGetOrRegisterSchemaAsync(TimeSpan.FromSeconds(2));

        var secondWaiter = serializer.WarmupAsync("shared-topic", record);
        try
        {
            firstWaiterCts.Cancel();

            await Assert.That(async () => await firstWaiter).Throws<OperationCanceledException>();
        }
        finally
        {
            schemaRegistry.ReleaseBlockedGetOrRegisterSchema();
        }

        var schemaId = await secondWaiter.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(schemaId).IsGreaterThan(0);
        await Assert.That(schemaRegistry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Serializer_PrepareAsync_IsAsynchronous_WhenSchemaNotYetCached()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        schemaRegistry.BlockNextGetOrRegisterSchema();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "prepare");

        // Cold path: the schema fetch is in flight, so preparation must be genuinely asynchronous
        // (not blocking a thread) - that is the whole point of the hook.
        var prepare = serializer.PrepareAsync(record, CreateContext("prepare-topic"));
        await schemaRegistry.WaitForBlockedGetOrRegisterSchemaAsync(TimeSpan.FromSeconds(2));
        await Assert.That(prepare.IsCompleted).IsFalse();

        schemaRegistry.ReleaseBlockedGetOrRegisterSchema();
        await prepare;

        await Assert.That(schemaRegistry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Serializer_PrepareAsync_WhenAlreadyPrepared_CompletesSynchronouslyAndSerializeDoesNotRefetch()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(schemaRegistry);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "prepare");

        var context = CreateContext("prepare-topic");

        // First prepare warms the subject cache with a single registry call.
        await serializer.PrepareAsync(record, context);
        await Assert.That(schemaRegistry.GetOrRegisterSchemaCallCount).IsEqualTo(1);

        // Once warmed, prepare completes synchronously (no allocation, no registry call) so the
        // producer's fast path stays synchronous.
        var warm = serializer.PrepareAsync(record, context);
        await Assert.That(warm.IsCompletedSuccessfully).IsTrue();
        await warm;

        // And the synchronous serialize now runs against the cached schema id without re-fetching.
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(record, ref buffer, context);

        await Assert.That(schemaRegistry.GetOrRegisterSchemaCallCount).IsEqualTo(1);
        await Assert.That(buffer.WrittenCount).IsGreaterThan(5); // magic byte + 4-byte schema id + payload
    }

    [Test]
    public async Task Deserializer_WarmupAsync_RetriesAfterTransientSchemaFetchFailure()
    {
        using var schemaRegistry = new MockSchemaRegistryClient
        {
            GetSchemaFailuresRemaining = 1
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("retry-topic-value", new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        });
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);

        await Assert.That(async () => await deserializer.WarmupAsync(schemaId))
            .Throws<SchemaRegistryException>();

        var schema = await deserializer.WarmupAsync(schemaId);

        await Assert.That(schema.Fullname).IsEqualTo("test.SimpleRecord");
        await Assert.That(schemaRegistry.GetSchemaCallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Deserializer_CachesGenericDatumReader_ForSameSchemaPair()
    {
        using var schemaRegistry = new MockSchemaRegistryClient();

        var schemaObj = new RegistrySchema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };
        var schemaId = await schemaRegistry.RegisterSchemaAsync("test-topic-value", schemaObj);

        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(schemaRegistry);
        await deserializer.WarmupAsync(schemaId);

        var avroSchema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record1 = new GenericRecord(avroSchema!);
        record1.Add("id", 1);
        record1.Add("name", "first");

        var record2 = new GenericRecord(avroSchema!);
        record2.Add("id", 2);
        record2.Add("name", "second");

        var wireFormat1 = CreateWireFormat(schemaId, SerializeAvroRecord(record1, avroSchema!));
        var wireFormat2 = CreateWireFormat(schemaId, SerializeAvroRecord(record2, avroSchema!));
        var context = CreateContext();

        deserializer.Deserialize(wireFormat1, context);
        deserializer.Deserialize(wireFormat2, context);

        await Assert.That(deserializer.CachedGenericReaderCount).IsEqualTo(1);
        await Assert.That(deserializer.CachedSpecificReaderCount).IsEqualTo(0);
    }

    [Test]
    public async Task PooledMemoryStream_ResetWithOffset_ReadsOnlyRequestedRange()
    {
        var stream = new PooledMemoryStream([]);
        var source = new byte[] { 9, 1, 2, 3, 8 };
        var buffer = new byte[5];

        stream.Reset(source, offset: 1, length: 3);
        var count = stream.Read(buffer, 0, buffer.Length);

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(buffer.AsSpan(0, count).ToArray()).IsEquivalentTo(new byte[] { 1, 2, 3 });
    }

    private static byte[] CreateWireFormat(int schemaId, byte[] avroPayload)
    {
        var wireFormat = new byte[1 + 4 + avroPayload.Length];
        wireFormat[0] = 0x00;
        BinaryPrimitives.WriteInt32BigEndian(wireFormat.AsSpan(1, 4), schemaId);
        avroPayload.CopyTo(wireFormat.AsSpan(5));
        return wireFormat;
    }

    private static async Task AssertSerializedPayloadMatchesApache(
        AvroSchemaRegistrySerializer<GenericRecord> serializer,
        Avro.RecordSchema schema,
        GenericRecord record)
    {
        var expected = SerializeAvroRecord(record, schema);
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(record, ref buffer, CreateContext());

        await Assert.That(buffer.WrittenSpan.Slice(5).SequenceEqual(expected)).IsTrue();
    }

    private static byte[] SerializeSpecificAvroRecord(ISpecificRecord record)
    {
        using var stream = new MemoryStream();
        var writer = new SpecificDefaultWriter(record.Schema);
        writer.Write(record.Schema, record, new BinaryEncoder(stream));
        return stream.ToArray();
    }

    private static async Task AssertSerializedPayloadMatches(
        AvroSchemaRegistrySerializer<GenericRecord> serializer,
        Avro.RecordSchema schema,
        GenericRecord actualRecord,
        GenericRecord expectedRecord)
    {
        var expected = SerializeAvroRecord(expectedRecord, schema);
        var buffer = new ArrayBufferWriter<byte>();

        serializer.Serialize(actualRecord, ref buffer, CreateContext());

        await Assert.That(buffer.WrittenSpan.Slice(5).SequenceEqual(expected)).IsTrue();
    }

    private sealed class StringBytesLogicalType() : Avro.Util.LogicalType(LogicalName)
    {
        internal const string LogicalName = "dekaf-string-bytes";

        public override object ConvertToBaseValue(object logicalValue, Avro.LogicalSchema schema) =>
            Encoding.UTF8.GetBytes((string)logicalValue);

        public override object ConvertToLogicalValue(object baseValue, Avro.LogicalSchema schema) =>
            Encoding.UTF8.GetString((byte[])baseValue);

        public override Type GetCSharpType(bool nullible) => typeof(string);

        public override bool IsInstanceOfLogicalType(object logicalValue) =>
            logicalValue is string value && value.StartsWith("logical-", StringComparison.Ordinal);
    }

    private sealed class IntBytesLogicalType() : Avro.Util.LogicalType(LogicalName)
    {
        internal const string LogicalName = "dekaf-int-bytes";

        public override object ConvertToBaseValue(object logicalValue, Avro.LogicalSchema schema) =>
            BitConverter.GetBytes((int)logicalValue);

        public override object ConvertToLogicalValue(object baseValue, Avro.LogicalSchema schema) =>
            BitConverter.ToInt32((byte[])baseValue);

        public override Type GetCSharpType(bool nullible) => typeof(int);

        public override bool IsInstanceOfLogicalType(object logicalValue) => logicalValue is int;
    }

    private sealed class NonGenericIntValues : Collection<int>
    {
        internal NonGenericIntValues() : base(new List<int> { 1, 2 })
        {
        }
    }

    private sealed class IntListBytesLogicalType() : Avro.Util.LogicalType(LogicalName)
    {
        internal const string LogicalName = "dekaf-int-list-bytes";

        public override object ConvertToBaseValue(object logicalValue, Avro.LogicalSchema schema) =>
            new byte[] { (byte)((IList<int>)logicalValue).Count };

        public override object ConvertToLogicalValue(object baseValue, Avro.LogicalSchema schema) =>
            throw new NotSupportedException();

        public override Type GetCSharpType(bool nullible) => typeof(IList<int>);

        public override bool IsInstanceOfLogicalType(object logicalValue) =>
            logicalValue is IList<int> { Count: > 0 } values && values[0] < 0;
    }

    private readonly record struct CustomLogicalValue(int Value);

    private sealed class CustomStructBytesLogicalType() : Avro.Util.LogicalType(LogicalName)
    {
        internal const string LogicalName = "dekaf-custom-struct-bytes";

        public override object ConvertToBaseValue(object logicalValue, Avro.LogicalSchema schema) =>
            BitConverter.GetBytes(((CustomLogicalValue)logicalValue).Value);

        public override object ConvertToLogicalValue(object baseValue, Avro.LogicalSchema schema) =>
            new CustomLogicalValue(BitConverter.ToInt32((byte[])baseValue));

        public override Type GetCSharpType(bool nullible) => typeof(CustomLogicalValue);

        public override bool IsInstanceOfLogicalType(object logicalValue) => logicalValue is CustomLogicalValue;
    }

    private sealed class ComparableBytesLogicalType() : Avro.Util.LogicalType(LogicalName)
    {
        internal const string LogicalName = "dekaf-comparable-bytes";

        public override object ConvertToBaseValue(object logicalValue, Avro.LogicalSchema schema) =>
            Encoding.UTF8.GetBytes((string)logicalValue);

        public override object ConvertToLogicalValue(object baseValue, Avro.LogicalSchema schema) =>
            Encoding.UTF8.GetString((byte[])baseValue);

        public override Type GetCSharpType(bool nullible) => typeof(IComparable);

        public override bool IsInstanceOfLogicalType(object logicalValue) =>
            logicalValue is string value && value.StartsWith("logical-", StringComparison.Ordinal);
    }

    private sealed class IntListStringLogicalType() : Avro.Util.LogicalType(LogicalName)
    {
        internal const string LogicalName = "dekaf-int-list-string";

        public override object ConvertToBaseValue(object logicalValue, Avro.LogicalSchema schema) => "list";

        public override object ConvertToLogicalValue(object baseValue, Avro.LogicalSchema schema) =>
            throw new NotSupportedException();

        public override Type GetCSharpType(bool nullible) => typeof(List<int>);

        public override bool IsInstanceOfLogicalType(object logicalValue) => logicalValue is List<int>;
    }

    private sealed class StringTextLogicalType() : Avro.Util.LogicalType(LogicalName)
    {
        internal const string LogicalName = "dekaf-string-text";

        public override object ConvertToBaseValue(object logicalValue, Avro.LogicalSchema schema) => logicalValue;

        public override object ConvertToLogicalValue(object baseValue, Avro.LogicalSchema schema) => baseValue;

        public override Type GetCSharpType(bool nullible) => typeof(string);

        public override bool IsInstanceOfLogicalType(object logicalValue) =>
            logicalValue is string value && value.StartsWith("text-", StringComparison.Ordinal);
    }

    private sealed class SpecificScalarRecord : ISpecificRecord
    {
        public static readonly AvroSchema _SCHEMA = AvroSchema.Parse(SpecificScalarRecordSchema);

        public bool Enabled { get; init; }
        public int Count { get; init; }
        public long Sequence { get; init; }
        public float Ratio { get; init; }
        public double Total { get; init; }
        public string Name { get; init; } = string.Empty;
        public byte[] Payload { get; init; } = [];
        public AvroSchema Schema => _SCHEMA;

        public object? Get(int fieldPos) => fieldPos switch
        {
            0 => null,
            1 => Enabled,
            2 => Count,
            3 => Sequence,
            4 => Ratio,
            5 => Total,
            6 => Name,
            7 => Payload,
            _ => throw new ArgumentOutOfRangeException(nameof(fieldPos))
        };

        public void Put(int fieldPos, object fieldValue) => throw new NotSupportedException();
    }

    private sealed class SpecificArrayRecord : ISpecificRecord
    {
        public static readonly AvroSchema _SCHEMA = AvroSchema.Parse(SpecificArrayRecordSchema);

        public int[] Values { get; init; } = [];
        public AvroSchema Schema => _SCHEMA;

        public object Get(int fieldPos) => fieldPos == 0
            ? Values
            : throw new ArgumentOutOfRangeException(nameof(fieldPos));

        public void Put(int fieldPos, object fieldValue) => throw new NotSupportedException();
    }

    private sealed class SpecificMissingPropertyRecord : ISpecificRecord
    {
        public static readonly AvroSchema _SCHEMA = AvroSchema.Parse(SpecificMissingPropertySchema);

        public AvroSchema Schema => _SCHEMA;
        public object Get(int fieldPos) => throw new ArgumentOutOfRangeException(nameof(fieldPos));
        public void Put(int fieldPos, object fieldValue) => throw new NotSupportedException();
    }

    private sealed class SpecificMismatchedPropertyRecord : ISpecificRecord
    {
        public static readonly AvroSchema _SCHEMA = AvroSchema.Parse(SpecificMismatchedPropertySchema);

        public long Count { get; init; }
        public AvroSchema Schema => _SCHEMA;
        public object Get(int fieldPos) => fieldPos == 0
            ? Count
            : throw new ArgumentOutOfRangeException(nameof(fieldPos));
        public void Put(int fieldPos, object fieldValue) => throw new NotSupportedException();
    }

    private sealed class SpecificCaseInsensitivePropertyRecord : ISpecificRecord
    {
        public static readonly AvroSchema _SCHEMA = AvroSchema.Parse(SpecificCaseInsensitivePropertySchema);

        public int UserId { get; init; }
        public AvroSchema Schema => _SCHEMA;
        public object Get(int fieldPos) => fieldPos == 0
            ? UserId
            : throw new ArgumentOutOfRangeException(nameof(fieldPos));
        public void Put(int fieldPos, object fieldValue) => throw new NotSupportedException();
    }

    private class SpecificVirtualPropertyRecord : ISpecificRecord
    {
        public static readonly AvroSchema _SCHEMA = AvroSchema.Parse(SpecificVirtualPropertySchema);

        public virtual int Count { get; init; }
        public AvroSchema Schema => _SCHEMA;
        public object Get(int fieldPos) => fieldPos == 0
            ? Count
            : throw new ArgumentOutOfRangeException(nameof(fieldPos));
        public void Put(int fieldPos, object fieldValue) => throw new NotSupportedException();
    }

    private sealed class SpecificVirtualPropertyDerivedRecord : SpecificVirtualPropertyRecord
    {
        public override int Count { get; init; }
    }

    private class SpecificAmbiguousPropertyBase
    {
        public long Count { get; init; }
    }

    private sealed class SpecificAmbiguousPropertyRecord : SpecificAmbiguousPropertyBase, ISpecificRecord
    {
        public static readonly AvroSchema _SCHEMA = AvroSchema.Parse(SpecificAmbiguousPropertySchema);

        public new int Count { get; init; }
        public AvroSchema Schema => _SCHEMA;
        public object Get(int fieldPos) => fieldPos == 0
            ? Count
            : throw new ArgumentOutOfRangeException(nameof(fieldPos));
        public void Put(int fieldPos, object fieldValue) => throw new NotSupportedException();
    }

    private sealed class SpecificAliasedPropertyRecord : ISpecificRecord
    {
        public static readonly AvroSchema _SCHEMA = AvroSchema.Parse(SpecificAliasedPropertySchema);

        public string Name { get; init; } = string.Empty;
        public AvroSchema Schema => _SCHEMA;
        public object Get(int fieldPos) => fieldPos is 0 or 1
            ? Name
            : throw new ArgumentOutOfRangeException(nameof(fieldPos));
        public void Put(int fieldPos, object fieldValue) => throw new NotSupportedException();
    }
}
