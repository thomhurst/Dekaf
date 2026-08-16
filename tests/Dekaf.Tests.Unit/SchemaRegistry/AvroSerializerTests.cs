using System.Buffers;
using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Numerics;
using Avro.Generic;
using Avro.IO;
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
}
