using System.Buffers;
using System.Buffers.Binary;
using Avro;
using Avro.Generic;
using Avro.IO;
using Avro.Specific;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class AvroInlineRuleValidatorTests
{
    private const string IntegrationSchema = """
        {
          "type": "record",
          "name": "IntegratedRuleRecord",
          "fields": [{
            "name": "age",
            "type": "int",
            "confluent:rules": [{ "name": "age", "expr": "this >= 0" }]
          }]
        }
        """;

    [Test]
    public async Task Validate_AggregatesRecordFieldArrayAndMapRules()
    {
        const string schemaText = """
            {
              "type": "record",
              "name": "InlineRuleRecord",
              "confluent:rules": [{ "name": "root", "expr": "this.name != 'forbidden'" }],
              "fields": [
                {
                  "name": "name",
                  "type": "string",
                  "confluent:rules": [{ "name": "name", "doc": "name required", "expr": "size(this) > 0" }]
                },
                {
                  "name": "items",
                  "type": {
                    "type": "array",
                    "items": {
                      "type": "int",
                      "confluent:rules": [{ "name": "positive", "expr": "this >= 0" }]
                    }
                  }
                },
                {
                  "name": "labels",
                  "type": {
                    "type": "map",
                    "values": {
                      "type": "string",
                      "confluent:rules": [{ "name": "label", "expr": "size(this) > 0" }]
                    }
                  }
                }
              ]
            }
            """;
        var schema = (RecordSchema)AvroSchema.Parse(schemaText);
        var record = new GenericRecord(schema);
        record.Add("name", "forbidden");
        record.Add("items", new[] { 1, -1 });
        record.Add("labels", new Dictionary<string, string> { ["region"] = "" });
        var validator = new AvroInlineRuleValidator(schema);

        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            validator.Validate(Serialize(record, schema), 17, failFast: false));

        await Assert.That(exception.Violations.Count).IsEqualTo(3);
        await Assert.That(exception.Message).Contains("$: root");
        await Assert.That(exception.Message).Contains("$.items[1]: positive");
        await Assert.That(exception.Message).Contains("$.labels[\"region\"]: label");
    }

    [Test]
    public async Task Validate_RecordMemberAliasesAndNullableNestedRecord()
    {
        const string schemaText = """
            {
              "type": "record",
              "name": "AliasRuleRecord",
              "confluent:rules": [{ "name": "alias", "expr": "this.oldName == 'ok' && this.child.code == 7" }],
              "fields": [
                { "name": "name", "aliases": ["oldName"], "type": "string" },
                {
                  "name": "child",
                  "type": ["null", {
                    "type": "record",
                    "name": "Child",
                    "fields": [{ "name": "code", "type": "int" }]
                  }]
                }
              ]
            }
            """;
        var schema = (RecordSchema)AvroSchema.Parse(schemaText);
        var childSchema = (RecordSchema)((UnionSchema)schema.Fields[1].Schema)[1];
        var child = new GenericRecord(childSchema);
        child.Add("code", 7);
        var record = new GenericRecord(schema);
        record.Add("name", "ok");
        record.Add("child", child);
        var validator = new AvroInlineRuleValidator(schema);

        validator.Validate(Serialize(record, schema), 18, failFast: false);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Serializer_EnabledValidationRejectsInvalidGenericRecord()
    {
        var schema = (RecordSchema)AvroSchema.Parse(IntegrationSchema);
        var record = new GenericRecord(schema);
        record.Add("age", -1);
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            new MockSchemaRegistryClient(),
            new AvroSerializerConfig
            {
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        var destination = new ArrayBufferWriter<byte>();

        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            serializer.Serialize(record, ref destination, CreateContext()));

        await Assert.That(exception.Violations[0].Rule.Name).IsEqualTo("age");
        await Assert.That(destination.WrittenCount).IsEqualTo(0);
    }

    [Test]
    public async Task Deserializer_EnabledValidationRejectsInvalidGenericRecord()
    {
        using var registry = new MockSchemaRegistryClient();
        var registrySchema = new Dekaf.SchemaRegistry.Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = IntegrationSchema
        };
        var schemaId = await registry.RegisterSchemaAsync("validation-topic-value", registrySchema);
        var schema = (RecordSchema)AvroSchema.Parse(IntegrationSchema);
        var record = new GenericRecord(schema);
        record.Add("age", -1);
        var payload = Serialize(record, schema);
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(
            registry,
            new AvroDeserializerConfig
            {
                ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
            });

        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            deserializer.Deserialize(CreateWireBytes(schemaId, payload), CreateContext()));

        await Assert.That(exception.Violations[0].Rule.Name).IsEqualTo("age");
    }

    [Test]
    public async Task InvalidExecutionModeAndCustomRuleExecutorAreRejected()
    {
        var invalidMode = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new AvroSchemaRegistrySerializer<GenericRecord>(
                new MockSchemaRegistryClient(),
                new AvroSerializerConfig
                {
                    ValidationRulesExecution = (ValidationRulesExecution)int.MaxValue
                }));
        var customExecutor = Assert.Throws<NotSupportedException>(() =>
            _ = new AvroSchemaRegistryDeserializer<GenericRecord>(
                new MockSchemaRegistryClient(),
                new AvroDeserializerConfig
                {
                    RuleExecutor = new PassThroughRuleExecutor(),
                    ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
                }));

        await Assert.That(invalidMode.ParamName).IsEqualTo("execution");
        await Assert.That(customExecutor.Message).Contains("SchemaRegistryRuleExecutor");
    }

    [Test]
    public async Task PocoSerializerAndDeserializer_ApplyInlineRulesToGeneratedBytes()
    {
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "validation-topic-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = SchemaType.Avro,
                SchemaString = InlineRulePocoCodec.SchemaJson
            });
        var serializerConfig = new AvroSerializerConfig
        {
            AutoRegisterSchemas = false,
            ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
        };
        await using var serializer = new AvroPocoSchemaRegistrySerializer<
            InlineRulePoco,
            InlineRulePocoCodec>(registry, serializerConfig);
        var destination = new ArrayBufferWriter<byte>();

        Assert.Throws<ValidationRulesFailedException>(() =>
            serializer.Serialize(new InlineRulePoco(-1), ref destination, CreateContext()));

        var payload = new byte[] { 1 };
        await using var deserializer = new AvroPocoSchemaRegistryDeserializer<
            InlineRulePoco,
            InlineRulePocoCodec>(
                registry,
                new AvroDeserializerConfig
                {
                    ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
                });
        Assert.Throws<ValidationRulesFailedException>(() =>
            deserializer.Deserialize(CreateWireBytes(schemaId, payload), CreateContext()));

        await Assert.That(destination.WrittenCount).IsEqualTo(0);
    }

    [Test]
    public async Task InlineRules_RunAtConfiguredDomainAndEncodingBoundaries()
    {
        using var registry = new MockSchemaRegistryClient();
        var schema = (RecordSchema)AvroSchema.Parse(IntegrationSchema);
        var validRecord = new GenericRecord(schema);
        validRecord.Add("age", 1);
        var invalidRecord = new GenericRecord(schema);
        invalidRecord.Add("age", -1);
        var validPayload = Serialize(validRecord, schema);
        var invalidPayload = Serialize(invalidRecord, schema);
        var calls = new List<string>();
        var schemaId = await registry.RegisterSchemaAsync(
            "validation-topic-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = SchemaType.Avro,
                SchemaString = IntegrationSchema,
                RuleSet = new SchemaRuleSet
                {
                    DomainRules = [CreateRule("domain", "DOMAIN", SchemaRuleMode.WriteRead)],
                    EncodingRules = [CreateRule("encoding", "ENCODING", SchemaRuleMode.WriteRead)],
                    HasFixedRuleCollections = true
                }
            });
        var executor = new SchemaRegistryRuleExecutor([
            new ReplacingRuleHandler("DOMAIN", validPayload, validPayload, calls),
            new ReplacingRuleHandler("ENCODING", "encoded"u8.ToArray(), invalidPayload, calls)
        ]);
        await using var afterSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            registry,
            new AvroSerializerConfig
            {
                AutoRegisterSchemas = false,
                RuleExecutor = executor,
                ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
            });
        var destination = new ArrayBufferWriter<byte>();

        afterSerializer.Serialize(invalidRecord, ref destination, CreateContext());

        await Assert.That(calls).IsEquivalentTo(["domain", "encoding"]);
        calls.Clear();
        await using var beforeSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(
            registry,
            new AvroSerializerConfig
            {
                AutoRegisterSchemas = false,
                RuleExecutor = executor,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        var rejected = new ArrayBufferWriter<byte>();
        Assert.Throws<ValidationRulesFailedException>(() =>
            beforeSerializer.Serialize(invalidRecord, ref rejected, CreateContext()));
        await Assert.That(calls).IsEmpty();

        await using var afterDeserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(
            registry,
            new AvroDeserializerConfig
            {
                RuleExecutor = executor,
                ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
            });
        var result = afterDeserializer.Deserialize(
            CreateWireBytes(schemaId, "encoded"u8),
            CreateContext());
        await Assert.That(result["age"]).IsEqualTo(1);
        await Assert.That(calls).IsEquivalentTo(["encoding", "domain"]);
        calls.Clear();
        await using var beforeDeserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(
            registry,
            new AvroDeserializerConfig
            {
                RuleExecutor = executor,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        Assert.Throws<ValidationRulesFailedException>(() => beforeDeserializer.Deserialize(
            CreateWireBytes(schemaId, "encoded"u8),
            CreateContext()));
        await Assert.That(calls).IsEquivalentTo(["encoding"]);
    }

    [Test]
    public async Task Deserializer_InlineRulesHonorMigrationBoundary()
    {
        using var registry = new MockSchemaRegistryClient();
        var schema = (RecordSchema)AvroSchema.Parse(IntegrationSchema);
        var invalid = new GenericRecord(schema);
        invalid.Add("age", -1);
        var valid = new GenericRecord(schema);
        valid.Add("age", 1);
        var writerSchemaId = await registry.RegisterSchemaAsync(
            "validation-topic-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = SchemaType.Avro,
                SchemaString = IntegrationSchema
            });
        _ = await registry.RegisterSchemaAsync(
            "validation-topic-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = SchemaType.Avro,
                SchemaString = IntegrationSchema,
                RuleSet = new SchemaRuleSet
                {
                    MigrationRules = [CreateRule("upgrade", "MIGRATION", SchemaRuleMode.Upgrade)]
                }
            });
        var calls = new List<string>();
        var executor = new SchemaRegistryRuleExecutor([
            new ReplacingRuleHandler("MIGRATION", Serialize(valid, schema), Serialize(valid, schema), calls)
        ]);
        var wire = CreateWireBytes(writerSchemaId, Serialize(invalid, schema));
        await using var after = new AvroSchemaRegistryDeserializer<GenericRecord>(
            registry,
            new AvroDeserializerConfig
            {
                UseLatestVersion = true,
                RuleExecutor = executor,
                ValidationRulesExecution = ValidationRulesExecution.AfterDomainRules
            });

        var result = after.Deserialize(wire, CreateContext());

        await Assert.That(result["age"]).IsEqualTo(1);
        await Assert.That(calls).IsEquivalentTo(["upgrade"]);
        calls.Clear();
        await using var before = new AvroSchemaRegistryDeserializer<GenericRecord>(
            registry,
            new AvroDeserializerConfig
            {
                UseLatestVersion = true,
                RuleExecutor = executor,
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        Assert.Throws<ValidationRulesFailedException>(() => before.Deserialize(wire, CreateContext()));
        await Assert.That(calls).IsEmpty();
    }

    [Test]
    public async Task Validate_WarmedValidPayloadAllocatesZeroBytes()
    {
        var schema = (RecordSchema)AvroSchema.Parse(IntegrationSchema);
        var record = new GenericRecord(schema);
        record.Add("age", 42);
        var payload = Serialize(record, schema);
        var validator = new AvroInlineRuleValidator(schema);
        validator.Validate(payload, 23, failFast: false);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            validator.Validate(payload, 23, failFast: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_UnionBranchWithoutReferencedMemberTreatsItAsMissing()
    {
        const string schemaText = """
            {
              "type": "record",
              "name": "UnionMissingRecord",
              "confluent:rules": [{ "name": "missing", "expr": "!has(this.value.code)" }],
              "fields": [{
                "name": "value",
                "type": [
                  { "type": "record", "name": "WithCode", "fields": [{ "name": "code", "type": "int" }] },
                  { "type": "record", "name": "WithoutCode", "fields": [{ "name": "name", "type": "string" }] }
                ]
              }]
            }
            """;
        var schema = (RecordSchema)AvroSchema.Parse(schemaText);
        var branch = (RecordSchema)((UnionSchema)schema.Fields[0].Schema)[1];
        var value = new GenericRecord(branch);
        value.Add("name", "dekaf");
        var record = new GenericRecord(schema);
        record.Add("value", value);

        new AvroInlineRuleValidator(schema).Validate(Serialize(record, schema), 24, failFast: false);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Validate_ResolvedNamedReferenceUsesReferencedRules()
    {
        var names = new SchemaNames();
        _ = AvroSchema.Parse(
            """
            {"type":"record","name":"ReferencedChild","namespace":"Dekaf.Tests","fields":[{"name":"code","type":"int","confluent:rules":[{"name":"code","expr":"this > 0"}]}]}
            """,
            names);
        var schema = (RecordSchema)AvroSchema.Parse(
            """
            {"type":"record","name":"ReferenceRoot","namespace":"Dekaf.Tests","fields":[{"name":"child","type":"Dekaf.Tests.ReferencedChild"}]}
            """,
            names);
        var childSchema = (RecordSchema)schema.Fields[0].Schema;
        var child = new GenericRecord(childSchema);
        child.Add("code", -1);
        var record = new GenericRecord(schema);
        record.Add("child", child);

        var exception = Assert.Throws<ValidationRulesFailedException>(() =>
            new AvroInlineRuleValidator(schema).Validate(Serialize(record, schema), 25, failFast: false));

        await Assert.That(exception.Message).Contains("$.child.code: code");
    }

    [Test]
    public async Task SpecificRecordSerializer_UsesSameBinaryValidationPlan()
    {
        await using var serializer = new AvroSchemaRegistrySerializer<InlineSpecificRecord>(
            new MockSchemaRegistryClient(),
            new AvroSerializerConfig
            {
                ValidationRulesExecution = ValidationRulesExecution.BeforeDomainRules
            });
        var destination = new ArrayBufferWriter<byte>();

        Assert.Throws<ValidationRulesFailedException>(() =>
            serializer.Serialize(new InlineSpecificRecord { Age = -1 }, ref destination, CreateContext()));

        await Assert.That(destination.WrittenCount).IsEqualTo(0);
    }

    [Test]
    public async Task Provider_WarmedRegisteredSchemaLookupAllocatesZeroBytes()
    {
        var avroSchema = AvroSchema.Parse(IntegrationSchema);
        var registrySchema = new Dekaf.SchemaRegistry.Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = IntegrationSchema
        };
        var provider = new AvroInlineRuleValidatorProvider();
        _ = provider.Register(registrySchema, avroSchema);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
            _ = provider.Register(registrySchema, avroSchema);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    private static byte[] Serialize(GenericRecord record, RecordSchema schema)
    {
        using var stream = new MemoryStream();
        var encoder = new BinaryEncoder(stream);
        new GenericDatumWriter<GenericRecord>(schema).Write(record, encoder);
        encoder.Flush();
        return stream.ToArray();
    }

    private static byte[] CreateWireBytes(int schemaId, ReadOnlySpan<byte> payload)
    {
        var result = new byte[5 + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(1, 4), schemaId);
        payload.CopyTo(result.AsSpan(5));
        return result;
    }

    private static SerializationContext CreateContext() => new()
    {
        Topic = "validation-topic",
        Component = SerializationComponent.Value
    };

    private static SchemaRule CreateRule(string name, string type, SchemaRuleMode mode) => new()
    {
        Name = name,
        Type = type,
        Kind = SchemaRuleKind.Transform,
        Mode = mode
    };

    private sealed class ReplacingRuleHandler(
        string type,
        ReadOnlyMemory<byte> serializedReplacement,
        ReadOnlyMemory<byte> deserializedReplacement,
        List<string> calls) : ISchemaRegistryRuleHandler
    {
        public string Type => type;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context)
        {
            calls.Add(context.Rule.Name);
            return serializedReplacement;
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleHandlerContext context)
        {
            calls.Add(context.Rule.Name);
            return deserializedReplacement;
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

internal sealed record InlineRulePoco(int Age);

internal readonly struct InlineRulePocoCodec : IAvroPocoCodec<InlineRulePoco>
{
    private static readonly AvroPocoField[] s_fields =
    [
        new(
            "age",
            ReadOnlyMemory<string>.Empty,
            defaultJson: null,
            new AvroPocoType(AvroPocoTypeKind.Int))
    ];

    public static string SchemaJson =>
        """
        {"type":"record","name":"InlineRulePoco","namespace":"Dekaf.Tests","fields":[{"name":"age","type":"int","confluent:rules":[{"name":"age","expr":"this >= 0"}]}]}
        """;

    public static ReadOnlySpan<byte> SchemaUtf8 =>
        """{"type":"record","name":"InlineRulePoco","namespace":"Dekaf.Tests","fields":[{"name":"age","type":"int","confluent:rules":[{"name":"age","expr":"this >= 0"}]}]}"""u8;

    public static long ParsingFingerprint64 => 0;
    public static string FullName => "Dekaf.Tests.InlineRulePoco";
    public static ReadOnlyMemory<AvroPocoField> Fields => s_fields;

    public static void Write(ref AvroValueWriter writer, InlineRulePoco value) =>
        writer.WriteInt32(value.Age);

    public static InlineRulePoco Read(ref AvroValueReader reader, AvroPocoReaderPlan plan) =>
        new(reader.ReadInt32());
}

internal sealed class InlineSpecificRecord : ISpecificRecord
{
    public static readonly AvroSchema _SCHEMA = AvroSchema.Parse(
        """
        {"type":"record","name":"InlineSpecificRecord","namespace":"Dekaf.Tests","fields":[{"name":"age","type":"int","confluent:rules":[{"name":"age","expr":"this >= 0"}]}]}
        """);

    public int Age { get; set; }
    public AvroSchema Schema => _SCHEMA;

    public object Get(int fieldPos)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(fieldPos, 0);
        return Age;
    }

    public void Put(int fieldPos, object fieldValue)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(fieldPos, 0);
        Age = (int)fieldValue;
    }
}
