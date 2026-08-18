using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using Avro;
using Avro.Generic;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.Serialization;
using ISchemaRegistryRuleExecutor = Dekaf.SchemaRegistry.ISchemaRegistryRuleExecutor;
using ISchemaRegistryRuleHandler = Dekaf.SchemaRegistry.ISchemaRegistryRuleHandler;
using SchemaRegistryRuleContext = Dekaf.SchemaRegistry.SchemaRegistryRuleContext;
using SchemaRegistryRuleExecutor = Dekaf.SchemaRegistry.SchemaRegistryRuleExecutor;
using SchemaRegistryRuleHandlerContext = Dekaf.SchemaRegistry.SchemaRegistryRuleHandlerContext;
using SchemaRule = Dekaf.SchemaRegistry.SchemaRule;
using SchemaRuleKind = Dekaf.SchemaRegistry.SchemaRuleKind;
using SchemaRuleMode = Dekaf.SchemaRegistry.SchemaRuleMode;
using SchemaRuleSet = Dekaf.SchemaRegistry.SchemaRuleSet;
using SubjectNameStrategy = Dekaf.SchemaRegistry.SubjectNameStrategy;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class AvroPocoSchemaRegistryTests
{
    private static readonly string[] LegacyValues = ["ignored", "field"];
    private static readonly byte[] PositiveDecimalWire = [0, 0, 0, 0, 1, 0x04, 0x30, 0x39];
    private static readonly byte[] NegativeDecimalWire = [0, 0, 0, 0, 1, 0x04, 0xCF, 0xC7];
    private static readonly byte[] EmptyCollectionsWire = [0, 0, 0, 0, 1, 0, 0, 0, 0x0E];
    private static readonly byte[] MultipleCollectionBlocksWire =
    [
        0, 0, 0, 0, 1,
        0x02, 0x02, 0x04, 0x04, 0x06, 0,
        0x02, 0x02, (byte)'a', 0x02, 0x02, (byte)'b', 0,
        0x02, 0x02, (byte)'x', 0x02, 0x02, 0x02, (byte)'y', 0x04, 0
    ];

    [Test]
    public async Task GeneratedCodec_RoundTripsSupportedShapes()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoOrder.CreateAvroSerializer(registry);
        await using var deserializer = PocoOrder.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-orders",
            Component = SerializationComponent.Value
        };
        var expected = new PocoOrder
        {
            Id = 42,
            Customer = "Ada",
            OptionalNote = "first",
            Status = PocoStatus.Ready,
            Scores = [1, 2, 3],
            Tags = ["math", "code"],
            Totals = new Dictionary<string, long> { ["net"] = 40, ["tax"] = 2 },
            Address = new PocoAddress { City = "London", PostCode = "SW1" },
            Created = DateTime.UnixEpoch.AddTicks(123_456_780),
            CorrelationId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
            Amount = 12345.67m
        };

        await serializer.WarmupAsync(context.Topic);
        var destination = new ArrayBufferWriter<byte>();
        serializer.Serialize(expected, ref destination, context);
        var actual = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Id).IsEqualTo(expected.Id);
        await Assert.That(actual.Customer).IsEqualTo(expected.Customer);
        await Assert.That(actual.OptionalNote).IsEqualTo(expected.OptionalNote);
        await Assert.That(actual.Status).IsEqualTo(expected.Status);
        await Assert.That(actual.Scores).IsEquivalentTo(expected.Scores);
        await Assert.That(actual.Tags).IsEquivalentTo(expected.Tags);
        await Assert.That(actual.Totals).IsEquivalentTo(expected.Totals);
        await Assert.That(actual.Address).IsEqualTo(expected.Address);
        await Assert.That(actual.Created).IsEqualTo(expected.Created);
        await Assert.That(actual.CorrelationId).IsEqualTo(expected.CorrelationId);
        await Assert.That(actual.Amount).IsEqualTo(expected.Amount);
    }

    [Test]
    public async Task GeneratedCodec_DistinguishesRecordUnionBranches()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoEnvelope.CreateAvroSerializer(registry);
        await using var deserializer = PocoEnvelope.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-envelopes",
            Component = SerializationComponent.Value
        };
        var expected = new PocoEnvelope
        {
            Party = new PocoContact { Name = "Ada", Email = "ada@example.test" }
        };

        await serializer.WarmupAsync(context.Topic);
        var destination = new ArrayBufferWriter<byte>();
        serializer.Serialize(expected, ref destination, context);
        var actual = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Party).IsTypeOf<PocoContact>();
        await Assert.That((PocoContact)actual.Party).IsEqualTo((PocoContact)expected.Party);
    }

    [Test]
    public async Task GeneratedSchema_IsDeterministicAndValidAvro()
    {
        const string expectedSchemaJson =
            """
            {"type":"record","name":"PocoOrder","namespace":"Dekaf.Tests","fields":[{"name":"Id","type":"int"},{"name":"Customer","type":"string"},{"name":"OptionalNote","type":["null","string"],"default":null},{"name":"Status","type":{"type":"enum","name":"PocoStatus","namespace":"Dekaf.Tests.Unit.SchemaRegistry","symbols":["Pending","Ready","Complete"]}},{"name":"Scores","type":{"type":"array","items":"int"}},{"name":"Tags","type":{"type":"array","items":"string"}},{"name":"Totals","type":{"type":"map","values":"long"}},{"name":"Address","type":{"type":"record","name":"PocoAddress","namespace":"Dekaf.Tests","fields":[{"name":"City","type":"string"},{"name":"PostCode","type":"string"}]}},{"name":"Created","type":{"type":"long","logicalType":"timestamp-micros"}},{"name":"CorrelationId","type":{"type":"string","logicalType":"uuid"}},{"name":"Amount","type":{"type":"bytes","logicalType":"decimal","precision":10,"scale":2}}]}
            """;
        var parsed = Schema.Parse(PocoOrder.AvroCodec.SchemaJson);
        var schemaUtf8 = PocoOrder.AvroCodec.SchemaUtf8.ToArray();

        await Assert.That(parsed.Fullname).IsEqualTo("Dekaf.Tests.PocoOrder");
        await Assert.That(PocoOrder.AvroCodec.SchemaJson).IsEqualTo(expectedSchemaJson);
        await Assert.That(schemaUtf8).IsEquivalentTo(Encoding.UTF8.GetBytes(PocoOrder.AvroCodec.SchemaJson));
        await Assert.That(PocoOrder.AvroCodec.ParsingFingerprint64)
            .IsEqualTo(SchemaNormalization.ParsingFingerprint64(parsed));
    }

    [Test]
    public async Task GeneratedCodec_RoundTripsNestedMaps()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoNestedMap.CreateAvroSerializer(registry);
        await using var deserializer = PocoNestedMap.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-nested-map",
            Component = SerializationComponent.Value
        };
        var expected = new PocoNestedMap
        {
            Values = new Dictionary<string, Dictionary<string, int>>
            {
                ["outer"] = new Dictionary<string, int> { ["inner"] = 42 }
            }
        };
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(expected, ref destination, context);
        var actual = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Values["outer"]["inner"]).IsEqualTo(42);
    }

    [Test]
    public async Task GeneratedCodec_ConvertsUnspecifiedDateTimeUsingLocalTimeZone()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoTemporal.CreateAvroSerializer(registry);
        await using var deserializer = PocoTemporal.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-temporal",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();
        var value = new PocoTemporal
        {
            Timestamp = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Unspecified),
            Time = TimeSpan.Zero
        };

        serializer.Serialize(value, ref destination, context);
        var actual = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Timestamp).IsEqualTo(value.Timestamp.ToUniversalTime());
    }

    [Test]
    public async Task GeneratedCodec_RoundTripsValidTemporalValues()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoTemporal.CreateAvroSerializer(registry);
        await using var deserializer = PocoTemporal.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-temporal-roundtrip",
            Component = SerializationComponent.Value
        };
        var expected = new PocoTemporal
        {
            Timestamp = DateTime.UnixEpoch.AddTicks(123_456_780),
            Time = TimeSpan.FromTicks(234_567_890)
        };
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(expected, ref destination, context);
        var actual = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Timestamp).IsEqualTo(expected.Timestamp);
        await Assert.That(actual.Time).IsEqualTo(expected.Time);
    }

    [Test]
    [Arguments(-1L)]
    [Arguments(TimeSpan.TicksPerDay)]
    public async Task GeneratedCodec_RejectsOutOfRangeTimeSpan(long ticks)
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoTemporal.CreateAvroSerializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-temporal-range",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();
        var value = new PocoTemporal
        {
            Timestamp = DateTime.UnixEpoch,
            Time = TimeSpan.FromTicks(ticks)
        };

        await Assert.That(() => serializer.Serialize(value, ref destination, context))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("time-micros");
    }

    [Test]
    [Arguments(-1L)]
    [Arguments(86_400_000_000L)]
    public async Task GeneratedCodec_RejectsOutOfRangeTimeMicrosPayload(long microseconds)
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoTemporal.CreateAvroSerializer(registry);
        await using var deserializer = PocoTemporal.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-temporal-read-range",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();
        serializer.Serialize(new PocoTemporal { Timestamp = DateTime.UnixEpoch }, ref destination, context);
        var malformedPayload = CreateTemporalPayload(destination.WrittenSpan[..5], microseconds);

        await Assert.That(() => deserializer.Deserialize(malformedPayload, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("time-micros");
    }

    [Test]
    public async Task GeneratedSchema_ExcludesNonPublicContractMembers()
    {
        await Assert.That(PocoPublicContract.AvroCodec.SchemaJson).IsEqualTo(
            """
            {"type":"record","name":"PocoPublicContract","namespace":"Dekaf.Tests","fields":[{"name":"Id","type":"int"}]}
            """);
        await Assert.That(Schema.Parse(PocoReadonlyRecord.AvroCodec.SchemaJson).Fullname)
            .IsEqualTo("Dekaf.Tests.PocoReadonlyRecord");
    }

    [Test]
    public async Task GeneratedCodec_WritesExactConfluentWireBytes()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoWireRecord.CreateAvroSerializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-wire",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(new PocoWireRecord { Id = 42, Name = "A" }, ref destination, context);

        ReadOnlySpan<byte> expected = [0, 0, 0, 0, 1, 0x54, 0x02, (byte)'A'];
        await Assert.That(destination.WrittenSpan.SequenceEqual(expected)).IsTrue();
    }

    [Test]
    public async Task GeneratedCodec_WritesDeterministicSignedDecimalBytes()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoDecimal.CreateAvroSerializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-decimal-wire",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(new PocoDecimal { Amount = 123.45m }, ref destination, context);
        await Assert.That(destination.WrittenSpan.SequenceEqual(PositiveDecimalWire)).IsTrue();

        destination.Clear();
        serializer.Serialize(new PocoDecimal { Amount = -123.45m }, ref destination, context);
        await Assert.That(destination.WrittenSpan.SequenceEqual(NegativeDecimalWire)).IsTrue();
    }

    [Test]
    public async Task GeneratedCodec_WritesOneTerminatorForEmptyCollections()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoEmptyCollections.CreateAvroSerializer(registry);
        await using var deserializer = PocoEmptyCollections.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-empty-collections",
            Component = SerializationComponent.Value
        };
        var value = new PocoEmptyCollections
        {
            Items = [],
            Names = [],
            Values = [],
            Tail = 7
        };
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(value, ref destination, context);
        var actual = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(destination.WrittenSpan.SequenceEqual(EmptyCollectionsWire)).IsTrue();
        await Assert.That(actual.Items).IsEmpty();
        await Assert.That(actual.Names).IsEmpty();
        await Assert.That(actual.Values).IsEmpty();
        await Assert.That(actual.Tail).IsEqualTo(7);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task GeneratedCodec_RejectsCollectionCountExceedingRemainingPayload(int collectionIndex)
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoBlocks.CreateAvroSerializer(registry);
        await using var deserializer = PocoBlocks.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-malformed-collection-count",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();
        serializer.Serialize(new PocoBlocks { Items = [], Names = [], Values = [] }, ref destination, context);
        var payload = new byte[10 + collectionIndex];
        destination.WrittenSpan[..5].CopyTo(payload);
        ReadOnlySpan<byte> maximumCount = [0xFE, 0xFF, 0xFF, 0xFF, 0x0F];
        maximumCount.CopyTo(payload.AsSpan(5 + collectionIndex));

        await Assert.That(() => deserializer.Deserialize(payload, context))
            .Throws<EndOfStreamException>()
            .WithMessageContaining("payload ended before the value was complete");
    }

    [Test]
    public async Task GeneratedCodec_SerializationAllocatesZeroAfterWarmup()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoOrder.CreateAvroSerializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-allocation",
            Component = SerializationComponent.Value
        };
        var value = new PocoOrder
        {
            Id = 42,
            Customer = "Ada",
            OptionalNote = null,
            Status = PocoStatus.Ready,
            Scores = [1, 2, 3],
            Tags = ["math", "code"],
            Totals = new Dictionary<string, long> { ["net"] = 40, ["tax"] = 2 },
            Address = new PocoAddress { City = "London", PostCode = "SW1" },
            Created = DateTime.UnixEpoch,
            CorrelationId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
            Amount = 12345.67m
        };
        var destination = new ArrayBufferWriter<byte>(1024);

        await serializer.WarmupAsync(context.Topic);
        for (var index = 0; index < 16; index++)
        {
            serializer.Serialize(value, ref destination, context);
            destination.Clear();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++)
        {
            serializer.Serialize(value, ref destination, context);
            destination.Clear();
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task GeneratedCodec_GrowingPayloadRetryDoesNotAllocate()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoGrowingPayload.CreateAvroSerializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-growing-allocation",
            Component = SerializationComponent.Value
        };
        var destination = new ExactSizeBufferWriter(4096);

        serializer.Serialize(new PocoGrowingPayload { Value = "small" }, ref destination, context);
        destination.Clear();
        var value = new PocoGrowingPayload { Value = new string('x', 1024) };

        var before = GC.GetAllocatedBytesForCurrentThread();
        serializer.Serialize(value, ref destination, context);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task GeneratedCodec_UsesActualVarintWidthForExactPayload()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoArrayTerminatorPayload.CreateAvroSerializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-exact-varint",
            Component = SerializationComponent.Value
        };
        var value = new PocoArrayTerminatorPayload { Values = new int[512] };
        var destination = new ExactSizeBufferWriter(2048);

        serializer.Serialize(value, ref destination, context);
        destination.Clear();
        serializer.Serialize(value, ref destination, context);

        await Assert.That(destination.GetMemoryCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task GeneratedCodec_RetainsLargePayloadSizeHint()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoLargeGrowingPayload.CreateAvroSerializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-large-growing-hint",
            Component = SerializationComponent.Value
        };
        var value = new PocoLargeGrowingPayload { Value = new string('x', 1024 * 1024 + 1) };
        var destination = new ExactSizeBufferWriter(2 * 1024 * 1024 + 16);

        serializer.Serialize(value, ref destination, context);
        destination.Clear();
        serializer.Serialize(value, ref destination, context);

        await Assert.That(destination.GetMemoryCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task GeneratedCodec_RulesPathLargePayloadAllocatesZeroAfterWarmup()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoLargeGrowingPayload.CreateAvroSerializer(
            registry,
            new AvroSerializerConfig { RuleExecutor = PassThroughRuleExecutor.Instance });
        var context = new SerializationContext
        {
            Topic = "poco-large-rules-allocation",
            Component = SerializationComponent.Value
        };
        var value = new PocoLargeGrowingPayload { Value = new string('x', 1024 * 1024 + 1) };
        var destination = new ExactSizeBufferWriter(2 * 1024 * 1024 + 16);

        serializer.Serialize(value, ref destination, context);
        destination.Clear();
        serializer.Serialize(value, ref destination, context);
        destination.Clear();

        var before = GC.GetAllocatedBytesForCurrentThread();
        serializer.Serialize(value, ref destination, context);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(destination.WrittenCount).IsGreaterThan(1024 * 1024);
    }

    [Test]
    public async Task GeneratedCodec_RulesPathSharesRetainedBufferAcrossCodecTypes()
    {
        using var registry = new MockSchemaRegistryClient();
        var config = new AvroSerializerConfig { RuleExecutor = PassThroughRuleExecutor.Instance };
        await using var first = PocoGrowingPayload.CreateAvroSerializer(registry, config);
        await using var second = PocoSecondGrowingPayload.CreateAvroSerializer(registry, config);
        var firstContext = new SerializationContext
        {
            Topic = "poco-shared-rule-buffer-first",
            Component = SerializationComponent.Value
        };
        var secondContext = new SerializationContext
        {
            Topic = "poco-shared-rule-buffer-second",
            Component = SerializationComponent.Value
        };
        var value = new string('x', 512 * 1024);
        var firstValue = new PocoGrowingPayload { Value = value };
        var secondWarmupValue = new PocoSecondGrowingPayload { Value = "small" };
        var secondValue = new PocoSecondGrowingPayload { Value = value };
        var firstDestination = new ExactSizeBufferWriter(1024 * 1024 + 16);
        var secondDestination = new ExactSizeBufferWriter(1024 * 1024 + 16);

        await first.WarmupAsync(firstContext.Topic);
        await second.WarmupAsync(secondContext.Topic);
        second.Serialize(secondWarmupValue, ref secondDestination, secondContext);
        secondDestination.Clear();
        first.Serialize(firstValue, ref firstDestination, firstContext);

        var before = GC.GetAllocatedBytesForCurrentThread();
        second.Serialize(secondValue, ref secondDestination, secondContext);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    [Arguments(SubjectNameStrategy.TopicRecordName, false)]
    [Arguments(SubjectNameStrategy.TopicRecordName, true)]
    [Arguments(SubjectNameStrategy.RecordName, true)]
    public async Task GeneratedCodec_RulesPathSubjectResolutionAllocatesZeroAfterWarmup(
        SubjectNameStrategy strategy,
        bool useLegacySubjectNames)
    {
        using var registry = new MockSchemaRegistryClient();
        var serializerConfig = new AvroSerializerConfig
        {
            SubjectNameStrategy = strategy,
            UseLegacySubjectNames = useLegacySubjectNames
        };
        var deserializerConfig = new AvroDeserializerConfig
        {
            SubjectNameStrategy = strategy,
            UseLegacySubjectNames = useLegacySubjectNames,
            RuleExecutor = PassThroughRuleExecutor.Instance
        };
        await using var serializer = PocoReadonlyRecord.CreateAvroSerializer(registry, serializerConfig);
        await using var deserializer = PocoReadonlyRecord.CreateAvroDeserializer(registry, deserializerConfig);
        var context = new SerializationContext
        {
            Topic = $"poco-subject-allocation-{strategy}-{useLegacySubjectNames}",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>(16);
        serializer.Serialize(new PocoReadonlyRecord { Id = 42 }, ref destination, context);

        for (var index = 0; index < 16; index++)
            _ = deserializer.Deserialize(destination.WrittenMemory, context);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++)
            _ = deserializer.Deserialize(destination.WrittenMemory, context);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task GeneratedCodec_ResolvesAliasesDefaultsPromotionsAndSkippedFields()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"},{"name":"legacy_values","type":{"type":"array","items":"string"}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var writer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var reader = PocoEvolved.CreateAvroDeserializer(registry);
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var generic = new GenericRecord(writerSchema);
        generic.Add("legacy_id", 42);
        generic.Add("legacy_values", LegacyValues);
        var context = new SerializationContext
        {
            Topic = "poco-evolution",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        writer.Serialize(generic, ref destination, context);
        var actual = reader.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Id).IsEqualTo(42L);
        await Assert.That(actual.Note).IsEqualTo("added-by-reader");
    }

    [Test]
    public async Task GeneratedCodec_PromotesBetweenStringAndBytes()
    {
        const string bytesWriterSchemaJson =
            """
            {"type":"record","name":"PocoStringPromotion","namespace":"Dekaf.Tests","fields":[{"name":"Value","type":"bytes"}]}
            """;
        const string stringWriterSchemaJson =
            """
            {"type":"record","name":"PocoBytesPromotion","namespace":"Dekaf.Tests","fields":[{"name":"Value","type":"string"}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var writer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var stringReader = PocoStringPromotion.CreateAvroDeserializer(registry);
        await using var bytesReader = PocoBytesPromotion.CreateAvroDeserializer(registry);
        var destination = new ArrayBufferWriter<byte>();

        var bytesSchema = (RecordSchema)Schema.Parse(bytesWriterSchemaJson);
        var bytesRecord = new GenericRecord(bytesSchema);
        bytesRecord.Add("Value", Encoding.UTF8.GetBytes("bytes-to-string"));
        var bytesContext = new SerializationContext
        {
            Topic = "poco-bytes-to-string",
            Component = SerializationComponent.Value
        };
        writer.Serialize(bytesRecord, ref destination, bytesContext);
        var promotedString = stringReader.Deserialize(destination.WrittenMemory, bytesContext);

        destination.Clear();
        var stringSchema = (RecordSchema)Schema.Parse(stringWriterSchemaJson);
        var stringRecord = new GenericRecord(stringSchema);
        stringRecord.Add("Value", "string-to-bytes");
        var stringContext = new SerializationContext
        {
            Topic = "poco-string-to-bytes",
            Component = SerializationComponent.Value
        };
        writer.Serialize(stringRecord, ref destination, stringContext);
        var promotedBytes = bytesReader.Deserialize(destination.WrittenMemory, stringContext);

        await Assert.That(promotedString.Value).IsEqualTo("bytes-to-string");
        await Assert.That(promotedBytes.Value).IsEquivalentTo(Encoding.UTF8.GetBytes("string-to-bytes"));
    }

    [Test]
    public async Task GeneratedCodec_RejectsRecursiveSkippedWriterRecordWithoutStackOverflow()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"},{"name":"recursive","type":["null","Dekaf.Tests.PocoEvolved"]}]}
            """;

        await Assert.That(() =>
                AvroPocoReaderPlanBuilder.Build<PocoEvolved, PocoEvolved.AvroCodec>(writerSchemaJson))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("Recursive writer record");
    }

    [Test]
    public async Task GeneratedCodec_PassesTimeoutTokenToPlanFetch()
    {
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-plan-timeout-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = PocoWireRecord.AvroCodec.SchemaJson
            });
        registry.BlockNextGetSchema();
        await using var reader = PocoWireRecord.CreateAvroDeserializer(registry);
        var warmup = reader.WarmupAsync(schemaId);

        await registry.WaitForBlockedGetSchemaAsync(TimeSpan.FromSeconds(5));
        try
        {
            await Assert.That(registry.LastGetSchemaCancellationToken.CanBeCanceled).IsTrue();
        }
        finally
        {
            registry.ReleaseBlockedGetSchema();
        }
        await warmup;
    }

    [Test]
    public async Task GeneratedCodec_RejectsDecimalPrecisionOrScaleMismatch()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoDecimal","namespace":"Dekaf.Tests","fields":[{"name":"amount","type":{"type":"bytes","logicalType":"decimal","precision":9,"scale":3}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var writer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var reader = PocoDecimal.CreateAvroDeserializer(registry);
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var generic = new GenericRecord(writerSchema);
        generic.Add("amount", new AvroDecimal(new BigInteger(123_456), 3));
        var context = new SerializationContext
        {
            Topic = "poco-decimal-evolution",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        writer.Serialize(generic, ref destination, context);

        await Assert.That(() => reader.Deserialize(destination.WrittenMemory, context))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GeneratedCodec_ReadsFixedDecimalAndSkipsRemovedFixedField()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoFixedEvolution","namespace":"Dekaf.Tests","fields":[{"name":"Amount","type":{"type":{"type":"fixed","name":"AmountBytes","size":4},"logicalType":"decimal","precision":9,"scale":2}},{"name":"removed","type":{"type":"fixed","name":"RemovedBytes","size":3}},{"name":"Tail","type":"int"}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var writer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var reader = PocoFixedEvolution.CreateAvroDeserializer(registry);
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var generic = new GenericRecord(writerSchema);
        generic.Add("Amount", new AvroDecimal(new BigInteger(12_345), 2));
        generic.Add("removed", new GenericFixed((FixedSchema)writerSchema.Fields[1].Schema, [1, 2, 3]));
        generic.Add("Tail", 42);
        var context = new SerializationContext
        {
            Topic = "poco-fixed-evolution",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        writer.Serialize(generic, ref destination, context);
        var actual = reader.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Amount).IsEqualTo(123.45m);
        await Assert.That(actual.Tail).IsEqualTo(42);
    }

    [Test]
    public async Task GeneratedCodec_RejectsDifferentLatestWriterSchema()
    {
        const string latestSchemaJson =
            """
            {"type":"record","name":"PocoWireRecord","namespace":"Dekaf.Tests","fields":[{"name":"Id","type":"int"},{"name":"Name","type":"string"},{"name":"Added","type":"int","default":0}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await registry.RegisterSchemaAsync(
            "poco-latest-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = latestSchemaJson
            });
        await using var serializer = PocoWireRecord.CreateAvroSerializer(
            registry,
            new AvroSerializerConfig { UseLatestVersion = true });

        await Assert.That(async () => await serializer.WarmupAsync("poco-latest"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GeneratedCodec_RejectsLatestSchemaWithDifferentLogicalMetadata()
    {
        const string latestSchemaJson =
            """
            {"type":"record","name":"PocoDecimal","namespace":"Dekaf.Tests","fields":[{"name":"Amount","type":{"type":"bytes","logicalType":"decimal","precision":10,"scale":3}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await registry.RegisterSchemaAsync(
            "poco-latest-decimal-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = latestSchemaJson
            });
        await using var serializer = PocoDecimal.CreateAvroSerializer(
            registry,
            new AvroSerializerConfig { UseLatestVersion = true });

        await Assert.That(async () => await serializer.WarmupAsync("poco-latest-decimal"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GeneratedCodec_UsesIncomingWriterPlanWhenMigrationHasNoRules()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        _ = await registry.RegisterSchemaAsync(
            "poco-migration-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        _ = await registry.RegisterSchemaAsync(
            "poco-migration-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = PocoEvolved.AvroCodec.SchemaJson
            });
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var deserializer = PocoEvolved.CreateAvroDeserializer(
            registry,
            new AvroDeserializerConfig { UseLatestVersion = true });
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var generic = new GenericRecord(writerSchema);
        generic.Add("legacy_id", 42);
        var context = new SerializationContext
        {
            Topic = "poco-migration",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();
        serializer.Serialize(generic, ref destination, context);

        var actual = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Id).IsEqualTo(42L);
        await Assert.That(actual.Note).IsEqualTo("added-by-reader");
    }

    [Test]
    public async Task GeneratedCodec_SkipsUnsupportedLogicalFieldByBaseType()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"},{"name":"removed_local_timestamp","type":{"type":"long","logicalType":"local-timestamp-micros"}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var writer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var reader = PocoEvolved.CreateAvroDeserializer(registry);
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var generic = new GenericRecord(writerSchema);
        generic.Add("legacy_id", 42);
        generic.Add("removed_local_timestamp", new DateTime(2026, 8, 18, 1, 0, 0));
        var context = new SerializationContext
        {
            Topic = "poco-skip-logical",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        writer.Serialize(generic, ref destination, context);
        var actual = reader.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Id).IsEqualTo(42L);
        await Assert.That(actual.Note).IsEqualTo("added-by-reader");
    }

    [Test]
    public async Task GeneratedCodec_UsesIncomingWriterPlanWhenMigrationHasOnlyConditions()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"}]}
            """;
        var migrationRule = new SchemaRule
        {
            Name = "validate-layout",
            Kind = SchemaRuleKind.Condition,
            Mode = SchemaRuleMode.Upgrade,
            Type = PassThroughMigrationHandler.RuleType
        };
        using var registry = new MockSchemaRegistryClient();
        _ = await registry.RegisterSchemaAsync(
            "poco-condition-migration-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        _ = await registry.RegisterSchemaAsync(
            "poco-condition-migration-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = PocoEvolved.AvroCodec.SchemaJson,
                RuleSet = new SchemaRuleSet { MigrationRules = [migrationRule] }
            });
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        var executor = new SchemaRegistryRuleExecutor([new PassThroughMigrationHandler()]);
        await using var deserializer = PocoEvolved.CreateAvroDeserializer(
            registry,
            new AvroDeserializerConfig
            {
                UseLatestVersion = true,
                RuleExecutor = executor
            });
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var generic = new GenericRecord(writerSchema);
        generic.Add("legacy_id", 42);
        var context = new SerializationContext
        {
            Topic = "poco-condition-migration",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();
        serializer.Serialize(generic, ref destination, context);

        var actual = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Id).IsEqualTo(42L);
        await Assert.That(actual.Note).IsEqualTo("added-by-reader");
    }

    [Test]
    public async Task GeneratedCodec_UsesTargetWriterPlanAfterActiveMigration()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"}]}
            """;
        var migrationRule = new SchemaRule
        {
            Name = "rewrite-layout",
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.Upgrade,
            Type = FixedPayloadMigrationHandler.RuleType
        };
        using var registry = new MockSchemaRegistryClient();
        _ = await registry.RegisterSchemaAsync(
            "poco-active-migration-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        _ = await registry.RegisterSchemaAsync(
            "poco-active-migration-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = PocoEvolved.AvroCodec.SchemaJson,
                RuleSet = new SchemaRuleSet { MigrationRules = [migrationRule] }
            });
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        var migratedPayload = new byte[64];
        var migratedWriter = new AvroValueWriter(migratedPayload);
        PocoEvolved.AvroCodec.Write(
            ref migratedWriter,
            new PocoEvolved { Id = 42, Note = "migrated-layout" });
        var executor = new SchemaRegistryRuleExecutor(
            [new FixedPayloadMigrationHandler(migratedPayload.AsMemory(0, migratedWriter.WrittenCount))]);
        await using var deserializer = PocoEvolved.CreateAvroDeserializer(
            registry,
            new AvroDeserializerConfig
            {
                UseLatestVersion = true,
                RuleExecutor = executor
            });
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var generic = new GenericRecord(writerSchema);
        generic.Add("legacy_id", 42);
        var context = new SerializationContext
        {
            Topic = "poco-active-migration",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();
        serializer.Serialize(generic, ref destination, context);

        var actual = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Id).IsEqualTo(42L);
        await Assert.That(actual.Note).IsEqualTo("migrated-layout");
    }

    [Test]
    public async Task GeneratedCodec_UsesLastTransformedWriterPlanBeforeUntransformedTail()
    {
        const string v1SchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"}]}
            """;
        const string v2SchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"id","type":"long"},{"name":"note","type":"string"}]}
            """;
        const string v3SchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"id","type":"long"},{"name":"note","type":"string"},{"name":"tail","type":"int","default":0}]}
            """;
        var migrationRule = new SchemaRule
        {
            Name = "rewrite-v2-layout",
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.Upgrade,
            Type = FixedPayloadMigrationHandler.RuleType
        };
        using var registry = new MockSchemaRegistryClient { SupportsDeletedVersionLookup = true };
        _ = await registry.RegisterSchemaAsync(
            "poco-migration-tail-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = v1SchemaJson
            });
        _ = await registry.RegisterSchemaAsync(
            "poco-migration-tail-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = v2SchemaJson,
                RuleSet = new SchemaRuleSet { MigrationRules = [migrationRule] }
            });
        _ = await registry.RegisterSchemaAsync(
            "poco-migration-tail-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = v3SchemaJson
            });
        var migratedPayload = new byte[64];
        var migratedWriter = new AvroValueWriter(migratedPayload);
        PocoEvolved.AvroCodec.Write(
            ref migratedWriter,
            new PocoEvolved { Id = 42, Note = "v2-layout" });
        var executor = new SchemaRegistryRuleExecutor(
            [new FixedPayloadMigrationHandler(migratedPayload.AsMemory(0, migratedWriter.WrittenCount))]);
        await using var writer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var reader = PocoEvolved.CreateAvroDeserializer(
            registry,
            new AvroDeserializerConfig
            {
                UseLatestVersion = true,
                RuleExecutor = executor
            });
        var writerSchema = (RecordSchema)Schema.Parse(v1SchemaJson);
        var generic = new GenericRecord(writerSchema);
        generic.Add("legacy_id", 42);
        var context = new SerializationContext
        {
            Topic = "poco-migration-tail",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();
        writer.Serialize(generic, ref destination, context);

        var actual = reader.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Id).IsEqualTo(42L);
        await Assert.That(actual.Note).IsEqualTo("v2-layout");

        await using var genericReader = new AvroSchemaRegistryDeserializer<GenericRecord>(
            registry,
            new AvroDeserializerConfig
            {
                UseLatestVersion = true,
                RuleExecutor = executor
            });
        var genericActual = genericReader.Deserialize(destination.WrittenMemory, context);

        await Assert.That((long)genericActual["id"]!).IsEqualTo(42L);
        await Assert.That((string)genericActual["note"]!).IsEqualTo("v2-layout");
        await Assert.That((int)genericActual["tail"]!).IsEqualTo(0);
    }

    [Test]
    public async Task GeneratedCodec_WithoutAutoRegistrationLooksUpGeneratedSchema()
    {
        const string differentSchemaJson =
            """
            {"type":"record","name":"PocoWireRecord","namespace":"Dekaf.Tests","fields":[{"name":"Id","type":"int"},{"name":"Name","type":"string"},{"name":"Added","type":"int","default":0}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var expectedId = await registry.RegisterSchemaAsync(
            "poco-lookup-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = PocoWireRecord.AvroCodec.SchemaJson
            });
        await registry.RegisterSchemaAsync(
            "poco-lookup-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = differentSchemaJson
            });
        await using var serializer = PocoWireRecord.CreateAvroSerializer(
            registry,
            new AvroSerializerConfig { AutoRegisterSchemas = false });

        var actualId = await serializer.WarmupAsync("poco-lookup");

        await Assert.That(actualId).IsEqualTo(expectedId);
    }

    [Test]
    public async Task GeneratedCodec_ReadsMultipleCollectionBlocks()
    {
        using var registry = new MockSchemaRegistryClient();
        await registry.RegisterSchemaAsync(
            "poco-blocks-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = PocoBlocks.AvroCodec.SchemaJson
            });
        await using var reader = PocoBlocks.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-blocks",
            Component = SerializationComponent.Value
        };

        var actual = reader.Deserialize(MultipleCollectionBlocksWire, context);

        await Assert.That(actual.Items).IsEquivalentTo([1, 2, 3]);
        await Assert.That(actual.Names).IsEquivalentTo(["a", "b"]);
        await Assert.That(actual.Values).IsEquivalentTo(new Dictionary<string, long> { ["x"] = 1, ["y"] = 2 });
    }

    [Test]
    public async Task GeneratedCodec_ResolvesWriterUnionsForNonUnionFieldsAndCollections()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoWriterUnions","namespace":"Dekaf.Tests","fields":[{"name":"scalar","type":["int","long"]},{"name":"items","type":{"type":"array","items":["int","long"]}},{"name":"values","type":{"type":"map","values":["int","long"]}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var writer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var reader = PocoWriterUnions.CreateAvroDeserializer(registry);
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var generic = new GenericRecord(writerSchema);
        generic.Add("scalar", 42);
        generic.Add("items", new object[] { 1, 2L });
        generic.Add("values", new Dictionary<string, object> { ["int"] = 3, ["long"] = 4L });
        var context = new SerializationContext
        {
            Topic = "poco-writer-unions",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        writer.Serialize(generic, ref destination, context);
        var actual = reader.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Scalar).IsEqualTo(42D);
        await Assert.That(actual.Items).IsEquivalentTo([1D, 2D]);
        await Assert.That(actual.Values).IsEquivalentTo(
            new Dictionary<string, double> { ["int"] = 3D, ["long"] = 4D });
    }

    [Test]
    public async Task GeneratedCodec_DefersIncompatibleWriterUnionBranchUntilSelected()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoWriterUnions","namespace":"Dekaf.Tests","fields":[{"name":"scalar","type":["int","string"]},{"name":"items","type":{"type":"array","items":["int","string"]}},{"name":"values","type":{"type":"map","values":["int","string"]}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var writer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var reader = PocoWriterUnions.CreateAvroDeserializer(registry);
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var compatible = new GenericRecord(writerSchema);
        compatible.Add("scalar", 42);
        compatible.Add("items", new object[] { 1, 2 });
        compatible.Add("values", new Dictionary<string, object> { ["int"] = 3 });
        var incompatible = new GenericRecord(writerSchema);
        incompatible.Add("scalar", "not-a-number");
        incompatible.Add("items", Array.Empty<object>());
        incompatible.Add("values", new Dictionary<string, object>());
        var incompatibleArray = new GenericRecord(writerSchema);
        incompatibleArray.Add("scalar", 42);
        incompatibleArray.Add("items", new object[] { 1, "not-a-number" });
        incompatibleArray.Add("values", new Dictionary<string, object>());
        var incompatibleMap = new GenericRecord(writerSchema);
        incompatibleMap.Add("scalar", 42);
        incompatibleMap.Add("items", Array.Empty<object>());
        incompatibleMap.Add("values", new Dictionary<string, object> { ["bad"] = "not-a-number" });
        var context = new SerializationContext
        {
            Topic = "poco-writer-union-compatibility",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        writer.Serialize(compatible, ref destination, context);
        var actual = reader.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Scalar).IsEqualTo(42D);
        await Assert.That(actual.Items).IsEquivalentTo([1D, 2D]);
        await Assert.That(actual.Values).IsEquivalentTo(new Dictionary<string, double> { ["int"] = 3D });

        destination.Clear();
        writer.Serialize(incompatible, ref destination, context);
        await Assert.That(() => reader.Deserialize(destination.WrittenMemory, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("no generated POCO target");

        destination.Clear();
        writer.Serialize(incompatibleArray, ref destination, context);
        await Assert.That(() => reader.Deserialize(destination.WrittenMemory, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("no generated POCO target");

        destination.Clear();
        writer.Serialize(incompatibleMap, ref destination, context);
        await Assert.That(() => reader.Deserialize(destination.WrittenMemory, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("no generated POCO target");
    }

    [Test]
    public async Task GeneratedCodec_DefersMissingWriterEnumSymbolUntilSelected()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEnumEvolution","namespace":"Dekaf.Tests","fields":[{"name":"status","type":{"type":"enum","name":"PocoNarrowStatus","namespace":"Dekaf.Tests.Unit.SchemaRegistry","symbols":["Current","Removed"]}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var writer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var reader = PocoEnumEvolution.CreateAvroDeserializer(registry);
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var enumSchema = (EnumSchema)writerSchema["status"].Schema;
        var compatible = new GenericRecord(writerSchema);
        compatible.Add("status", new GenericEnum(enumSchema, "Current"));
        var incompatible = new GenericRecord(writerSchema);
        incompatible.Add("status", new GenericEnum(enumSchema, "Removed"));
        var context = new SerializationContext
        {
            Topic = "poco-enum-evolution",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        writer.Serialize(compatible, ref destination, context);
        var actual = reader.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Status).IsEqualTo(PocoNarrowStatus.Current);

        destination.Clear();
        writer.Serialize(incompatible, ref destination, context);
        await Assert.That(() => reader.Deserialize(destination.WrittenMemory, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("no generated POCO target");
    }

    private static byte[] CreateTemporalPayload(ReadOnlySpan<byte> wireHeader, long microseconds)
    {
        const int maximumEncodedLongBytes = 10;
        var payload = new byte[wireHeader.Length + maximumEncodedLongBytes * 2];
        wireHeader.CopyTo(payload);
        var writer = new AvroValueWriter(payload.AsSpan(wireHeader.Length));
        writer.WriteInt64(0);
        writer.WriteInt64(microseconds);
        Array.Resize(ref payload, wireHeader.Length + writer.WrittenCount);
        return payload;
    }

    private sealed class PassThroughMigrationHandler : ISchemaRegistryRuleHandler
    {
        internal const string RuleType = "PASS_THROUGH_MIGRATION";

        public string Type => RuleType;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> source,
            SchemaRegistryRuleHandlerContext context) => source;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> source,
            SchemaRegistryRuleHandlerContext context) => source;
    }

    private sealed class PassThroughRuleExecutor : ISchemaRegistryRuleExecutor
    {
        internal static PassThroughRuleExecutor Instance { get; } = new();

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;
    }

    private sealed class FixedPayloadMigrationHandler(ReadOnlyMemory<byte> payload) : ISchemaRegistryRuleHandler
    {
        internal const string RuleType = "FIXED_AVRO_PAYLOAD";

        public string Type => RuleType;

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> source,
            SchemaRegistryRuleHandlerContext context) => payload;

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> source,
            SchemaRegistryRuleHandlerContext context) => payload;
    }

    private sealed class ExactSizeBufferWriter(int capacity) : IBufferWriter<byte>
    {
        private readonly byte[] _buffer = GC.AllocateUninitializedArray<byte>(capacity);

        public int WrittenCount { get; private set; }
        public int GetMemoryCallCount { get; private set; }

        public void Advance(int count)
        {
            if ((uint)count > (uint)(_buffer.Length - WrittenCount))
                throw new ArgumentOutOfRangeException(nameof(count));
            WrittenCount += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            GetMemoryCallCount++;
            return _buffer.AsMemory(WrittenCount, GetLength(sizeHint));
        }

        public Span<byte> GetSpan(int sizeHint = 0) =>
            _buffer.AsSpan(WrittenCount, GetLength(sizeHint));

        internal void Clear()
        {
            WrittenCount = 0;
            GetMemoryCallCount = 0;
        }

        private int GetLength(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            var length = Math.Max(sizeHint, 1);
            if (length > _buffer.Length - WrittenCount)
                throw new ArgumentException("Requested buffer exceeds remaining capacity.", nameof(sizeHint));
            return length;
        }
    }
}

[AvroRecord(Name = "PocoOrder", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoOrder
{
    [AvroField(Order = 0)]
    public required int Id { get; init; }

    [AvroField(Order = 1)]
    public required string Customer { get; init; }

    [AvroField(Order = 2, DefaultJson = "null")]
    public string? OptionalNote { get; init; }

    [AvroField(Order = 3)]
    public PocoStatus Status { get; init; }

    [AvroField(Order = 4)]
    public required int[] Scores { get; init; }

    [AvroField(Order = 5)]
    public required List<string> Tags { get; init; }

    [AvroField(Order = 6)]
    public required Dictionary<string, long> Totals { get; init; }

    [AvroField(Order = 7)]
    public required PocoAddress Address { get; init; }

    [AvroField(Order = 8)]
    public DateTime Created { get; init; }

    [AvroField(Order = 9)]
    public Guid CorrelationId { get; init; }

    [AvroField(Order = 10, Precision = 10, Scale = 2)]
    public decimal Amount { get; init; }
}

[AvroRecord(Name = "PocoAddress", Namespace = "Dekaf.Tests")]
internal sealed partial record PocoAddress
{
    public required string City { get; init; }
    public required string PostCode { get; init; }
}

internal enum PocoStatus
{
    Pending,
    Ready,
    Complete
}

internal enum PocoNarrowStatus
{
    Current
}

[AvroRecord(Name = "PocoEnumEvolution", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoEnumEvolution
{
    [AvroField(Name = "status", Order = 0)]
    public PocoNarrowStatus Status { get; init; }
}

[AvroRecord(Name = "PocoEnvelope", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoEnvelope
{
    [AvroField(UnionTypes = [typeof(PocoAddress), typeof(PocoContact)])]
    public required object Party { get; init; }
}

[AvroRecord(Name = "PocoContact", Namespace = "Dekaf.Tests")]
internal sealed partial record PocoContact
{
    public required string Name { get; init; }
    public required string Email { get; init; }
}

[AvroRecord(Name = "PocoEvolved", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoEvolved
{
#pragma warning disable CA1861 // Attribute arrays are emitted as metadata, not allocated per call.
    [AvroField(Name = "id", Aliases = ["legacy_id"], Order = 0)]
#pragma warning restore CA1861
    public long Id { get; init; }

    [AvroField(Name = "note", DefaultJson = "\"added-by-reader\"", Order = 1)]
    public required string Note { get; init; }
}

[AvroRecord(Name = "PocoDecimal", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoDecimal
{
    [AvroField(Precision = 10, Scale = 2)]
    public decimal Amount { get; init; }
}

[AvroRecord(Name = "PocoWireRecord", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoWireRecord
{
    [AvroField(Name = "id", Order = 0)]
    public int Id { get; init; }

    [AvroField(Name = "name", Order = 1)]
    public required string Name { get; init; }
}

[AvroRecord(Name = "PocoBlocks", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoBlocks
{
    [AvroField(Name = "items", Order = 0)]
    public required int[] Items { get; init; }

    [AvroField(Name = "names", Order = 1)]
    public required List<string> Names { get; init; }

    [AvroField(Name = "values", Order = 2)]
    public required Dictionary<string, long> Values { get; init; }
}

[AvroRecord(Name = "PocoEmptyCollections", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoEmptyCollections
{
    [AvroField(Name = "items", Order = 0)]
    public required int[] Items { get; init; }

    [AvroField(Name = "names", Order = 1)]
    public required List<string> Names { get; init; }

    [AvroField(Name = "values", Order = 2)]
    public required Dictionary<string, long> Values { get; init; }

    [AvroField(Name = "tail", Order = 3)]
    public int Tail { get; init; }
}

[AvroRecord(Name = "PocoWriterUnions", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoWriterUnions
{
    [AvroField(Name = "scalar", Order = 0)]
    public double Scalar { get; init; }

    [AvroField(Name = "items", Order = 1)]
    public required double[] Items { get; init; }

    [AvroField(Name = "values", Order = 2)]
    public required Dictionary<string, double> Values { get; init; }
}

[AvroRecord(Name = "PocoGrowingPayload", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoGrowingPayload
{
    public required string Value { get; init; }
}

[AvroRecord(Name = "PocoSecondGrowingPayload", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoSecondGrowingPayload
{
    public required string Value { get; init; }
}

[AvroRecord(Name = "PocoArrayTerminatorPayload", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoArrayTerminatorPayload
{
    public required int[] Values { get; init; }
}

[AvroRecord(Name = "PocoStringPromotion", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoStringPromotion
{
    public required string Value { get; init; }
}

[AvroRecord(Name = "PocoBytesPromotion", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoBytesPromotion
{
    public required byte[] Value { get; init; }
}

[AvroRecord(Name = "PocoLargeGrowingPayload", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoLargeGrowingPayload
{
    public required string Value { get; init; }
}

[AvroRecord(Name = "PocoNestedMap", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoNestedMap
{
    public required Dictionary<string, Dictionary<string, int>> Values { get; init; }
}

[AvroRecord(Name = "PocoTemporal", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoTemporal
{
    [AvroField(Order = 0)]
    public DateTime Timestamp { get; init; }

    [AvroField(Order = 1)]
    public TimeSpan Time { get; init; }
}

[AvroRecord(Name = "PocoPublicContract", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoPublicContract
{
    private int State { get; set; }
    public string Hidden { get; private set; } = string.Empty;
    public int Id { get; init; }
}

[AvroRecord(Name = "PocoReadonlyRecord", Namespace = "Dekaf.Tests")]
internal readonly partial record struct PocoReadonlyRecord
{
    public int Id { get; init; }
}

[AvroRecord(Name = "PocoFixedEvolution", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoFixedEvolution
{
    [AvroField(Order = 0, Precision = 9, Scale = 2)]
    public decimal Amount { get; init; }

    [AvroField(Order = 1)]
    public int Tail { get; init; }
}
