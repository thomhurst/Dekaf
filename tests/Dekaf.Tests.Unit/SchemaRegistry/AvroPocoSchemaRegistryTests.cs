using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Avro;
using Avro.Generic;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.Serialization;
using NSubstitute;
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
    public async Task GeneratedCodec_ResolvesReferencedWriterRecordDuringWarmup()
    {
        const string addressSchemaJson =
            """
            {"type":"record","name":"PocoAddress","namespace":"Dekaf.Tests","fields":[{"name":"City","type":"string"},{"name":"PostCode","type":"string"}]}
            """;
        const string rootSchemaJson =
            """
            {"type":"record","name":"PocoReferencedRoot","namespace":"Dekaf.Tests","fields":[{"name":"Address","type":"Dekaf.Tests.PocoAddress"}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        _ = await registry.RegisterSchemaAsync(
            "poco-address-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = addressSchemaJson
            });
        var rootSchemaId = await registry.RegisterSchemaAsync(
            "poco-referenced-root-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = rootSchemaJson,
                References =
                [
                    new Dekaf.SchemaRegistry.SchemaReference
                    {
                        Name = "Dekaf.Tests.PocoAddress",
                        Subject = "poco-address-value",
                        Version = 1
                    }
                ]
            });
        await using var deserializer = PocoReferencedRoot.CreateAvroDeserializer(registry);
        var wire = new byte[64];
        BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(1, 4), rootSchemaId);
        var writer = new AvroValueWriter(wire.AsSpan(5));
        writer.WriteString("London");
        writer.WriteString("SW1");
        var wireLength = 5 + writer.WrittenCount;

        await deserializer.WarmupAsync(rootSchemaId);
        var actual = deserializer.Deserialize(
            wire.AsMemory(0, wireLength),
            new SerializationContext
            {
                Topic = "poco-referenced-root",
                Component = SerializationComponent.Value
            });

        await Assert.That(actual.Address.City).IsEqualTo("London");
        await Assert.That(actual.Address.PostCode).IsEqualTo("SW1");
    }

    [Test]
    public async Task GeneratedCodec_ContextWarmupRetainsReferencedPlanThroughTaggedRules()
    {
        const string addressSchemaJson =
            """
            {"type":"record","name":"PocoAddress","namespace":"Dekaf.Tests","fields":[{"name":"City","type":"string"},{"name":"PostCode","type":"string"}]}
            """;
        const string rootSchemaJson =
            """
            {"type":"record","name":"PocoReferencedRoot","namespace":"Dekaf.Tests","fields":[{"name":"Address","type":"Dekaf.Tests.PocoAddress"}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        _ = await registry.RegisterSchemaAsync(
            "poco-tagged-address-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = addressSchemaJson
            });
        var rootSchemaId = await registry.RegisterSchemaAsync(
            "poco-tagged-referenced-root-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = rootSchemaJson,
                References =
                [
                    new Dekaf.SchemaRegistry.SchemaReference
                    {
                        Name = "Dekaf.Tests.PocoAddress",
                        Subject = "poco-tagged-address-value",
                        Version = 1
                    }
                ],
                RuleSet = new SchemaRuleSet { DomainRules = [] }
            });
        var evictionSchemaIds = new int[
            AvroPocoSchemaRegistryDeserializer<PocoReferencedRoot, PocoReferencedRoot.AvroCodec>.MaxCachedPlans];
        for (var index = 0; index < evictionSchemaIds.Length; index++)
        {
            evictionSchemaIds[index] = await registry.RegisterSchemaAsync(
                "poco-tagged-plan-eviction-value",
                new Dekaf.SchemaRegistry.Schema
                {
                    SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                    SchemaString = PocoReferencedRoot.AvroCodec.SchemaJson
                });
        }
        var ruleExecutor = new CapturingTaggedFieldRuleExecutor();
        var nonCachingRegistry = CreateNonCachingRegistry(registry);
        await using var deserializer = PocoReferencedRoot.CreateAvroDeserializer(
            nonCachingRegistry,
            new AvroDeserializerConfig { RuleExecutor = ruleExecutor });
        var context = new SerializationContext
        {
            Topic = "poco-tagged-referenced-root",
            Component = SerializationComponent.Value
        };
        var wire = new byte[64];
        BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(1, 4), rootSchemaId);
        var writer = new AvroValueWriter(wire.AsSpan(5));
        writer.WriteString("London");
        writer.WriteString("SW1");
        var wireLength = 5 + writer.WrittenCount;

        await deserializer.WarmupAsync(rootSchemaId, context);
        nonCachingRegistry.ClearReceivedCalls();
        ruleExecutor.BeforeDeserialize = () =>
        {
            foreach (var schemaId in evictionSchemaIds)
                deserializer.WarmupAsync(schemaId).GetAwaiter().GetResult();
            nonCachingRegistry.ClearReceivedCalls();
        };
        var actual = deserializer.Deserialize(wire.AsMemory(0, wireLength), context);

        await Assert.That(actual.Address.City).IsEqualTo("London");
        await Assert.That(actual.Address.PostCode).IsEqualTo("SW1");
        await Assert.That(ruleExecutor.DeserializedContextHadTransformer).IsTrue();
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .GetSchemaAsync(default, default(CancellationToken));
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .GetSchemaAsync(default, default!, default(CancellationToken));
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .GetSchemaBySubjectAsync(default!, default!, default(CancellationToken));
    }

    [Test]
    public async Task GeneratedCodec_ContextWarmupPreparesPublicRulesPath()
    {
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-context-warmup-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = PocoWireRecord.AvroCodec.SchemaJson
            });
        var nonCachingRegistry = CreateNonCachingRegistry(registry);
        await using var deserializer = PocoWireRecord.CreateAvroDeserializer(
            nonCachingRegistry,
            new AvroDeserializerConfig { RuleExecutor = PassThroughRuleExecutor.Instance });
        var context = new SerializationContext
        {
            Topic = "poco-context-warmup",
            Component = SerializationComponent.Value
        };
        var wire = new byte[64];
        BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(wire.AsSpan(5));
        PocoWireRecord.AvroCodec.Write(ref writer, new PocoWireRecord { Id = 42, Name = "Ada" });
        var wireLength = 5 + writer.WrittenCount;

        await deserializer.WarmupAsync(schemaId, context);
        nonCachingRegistry.ClearReceivedCalls();
        var actual = deserializer.Deserialize(wire.AsMemory(0, wireLength), context);

        await Assert.That(actual.Id).IsEqualTo(42);
        await Assert.That(actual.Name).IsEqualTo("Ada");
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .GetSchemaAsync(default, default(CancellationToken));
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .GetSchemaAsync(default, default!, default(CancellationToken));
    }

    [Test]
    public async Task GeneratedCodec_ContextWarmupResolvesReferencedMigrationTarget()
    {
        const string inlineRootSchemaJson =
            """
            {"type":"record","name":"PocoReferencedRoot","namespace":"Dekaf.Tests","fields":[{"name":"Address","type":{"type":"record","name":"PocoAddress","fields":[{"name":"City","type":"string"},{"name":"PostCode","type":"string"}]}}]}
            """;
        const string addressSchemaJson =
            """
            {"type":"record","name":"PocoAddress","namespace":"Dekaf.Tests","fields":[{"name":"City","type":"string"},{"name":"PostCode","type":"string"}]}
            """;
        const string referencedRootSchemaJson =
            """
            {"type":"record","name":"PocoReferencedRoot","namespace":"Dekaf.Tests","fields":[{"name":"Address","type":"Dekaf.Tests.PocoAddress"}]}
            """;
        var migrationRule = new SchemaRule
        {
            Name = "rewrite-referenced-layout",
            Kind = SchemaRuleKind.Transform,
            Mode = SchemaRuleMode.Upgrade,
            Type = FixedPayloadMigrationHandler.RuleType
        };
        using var registry = new MockSchemaRegistryClient();
        var writerSchemaId = await registry.RegisterSchemaAsync(
            "poco-migration-referenced-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = inlineRootSchemaJson
            });
        _ = await registry.RegisterSchemaAsync(
            "poco-migration-referenced-address-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = addressSchemaJson
            });
        _ = await registry.RegisterSchemaAsync(
            "poco-migration-referenced-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = referencedRootSchemaJson,
                References =
                [
                    new Dekaf.SchemaRegistry.SchemaReference
                    {
                        Name = "Dekaf.Tests.PocoAddress",
                        Subject = "poco-migration-referenced-address-value",
                        Version = 1
                    }
                ],
                RuleSet = new SchemaRuleSet { MigrationRules = [migrationRule] }
            });
        var migratedPayload = new byte[64];
        var migratedWriter = new AvroValueWriter(migratedPayload);
        PocoReferencedRoot.AvroCodec.Write(
            ref migratedWriter,
            new PocoReferencedRoot
            {
                Address = new PocoAddress { City = "London", PostCode = "SW1" }
            });
        var handler = new FixedPayloadMigrationHandler(
            migratedPayload.AsMemory(0, migratedWriter.WrittenCount));
        var nonCachingRegistry = CreateNonCachingRegistry(registry);
        await using var deserializer = PocoReferencedRoot.CreateAvroDeserializer(
            nonCachingRegistry,
            new AvroDeserializerConfig
            {
                UseLatestVersion = true,
                RuleExecutor = new SchemaRegistryRuleExecutor([handler])
            });
        var context = new SerializationContext
        {
            Topic = "poco-migration-referenced",
            Component = SerializationComponent.Value
        };
        var wire = new byte[64];
        BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(1, 4), writerSchemaId);
        var writer = new AvroValueWriter(wire.AsSpan(5));
        writer.WriteString("old-city");
        writer.WriteString("old-postcode");
        var wireLength = 5 + writer.WrittenCount;

        await deserializer.WarmupAsync(writerSchemaId, context);
        nonCachingRegistry.ClearReceivedCalls();
        var actual = deserializer.Deserialize(wire.AsMemory(0, wireLength), context);

        await Assert.That(actual.Address.City).IsEqualTo("London");
        await Assert.That(actual.Address.PostCode).IsEqualTo("SW1");
        await Assert.That(handler.ContextHadTaggedFieldTransformer).IsTrue();
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .GetSchemaAsync(default, default(CancellationToken));
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .GetSchemaBySubjectAsync(default!, default!, default(CancellationToken));
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
    [Arguments(DateTimeKind.Unspecified)]
    [Arguments(DateTimeKind.Local)]
    public async Task GeneratedCodec_RejectsNonUtcDateTime(DateTimeKind kind)
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoTemporal.CreateAvroSerializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-temporal",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();
        var value = new PocoTemporal
        {
            Timestamp = new DateTime(2026, 8, 17, 12, 0, 0, kind),
            Time = TimeSpan.Zero
        };

        await Assert.That(() => serializer.Serialize(value, ref destination, context))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("timestamp-micros requires a UTC DateTime");
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
    public async Task GeneratedCodec_FloorsPreEpochDateTimeOffsetToMicroseconds()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoDateTimeOffsetTemporal.CreateAvroSerializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-datetime-offset-floor",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(
            new PocoDateTimeOffsetTemporal { Timestamp = DateTimeOffset.UnixEpoch.AddTicks(-1) },
            ref destination,
            context);
        var reader = new AvroValueReader(destination.WrittenSpan[5..]);

        await Assert.That(reader.ReadInt64()).IsEqualTo(-1L);
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
            .Throws<InvalidDataException>()
            .WithMessageContaining("supported limit");
    }

    [Test]
    public async Task GeneratedCodec_RejectsOversizedCollectionDuringSerialization()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoBlocks.CreateAvroSerializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-oversized-collection-write",
            Component = SerializationComponent.Value
        };
        var value = new PocoBlocks
        {
            Items = new int[1_048_577],
            Names = [],
            Values = []
        };
        var destination = new ArrayBufferWriter<byte>();

        await Assert.That(() => serializer.Serialize(value, ref destination, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("supported limit");
    }

    [Test]
    public async Task GeneratedCodec_ReadsZeroWidthWriterRecordArrays()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoZeroWidthCollection","namespace":"Dekaf.Tests","fields":[{"name":"items","type":{"type":"array","items":{"type":"record","name":"PocoZeroWidthItem","fields":[]}}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var writer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var reader = PocoZeroWidthCollection.CreateAvroDeserializer(registry);
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var itemSchema = (RecordSchema)((ArraySchema)writerSchema["items"].Schema).ItemSchema;
        var root = new GenericRecord(writerSchema);
        root.Add("items", new object[] { new GenericRecord(itemSchema), new GenericRecord(itemSchema) });
        var context = new SerializationContext
        {
            Topic = "poco-zero-width-array",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        writer.Serialize(root, ref destination, context);
        var actual = reader.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Items.Length).IsEqualTo(2);
        await Assert.That(actual.Items[0].Value).IsEqualTo("reader-default");
        await Assert.That(actual.Items[1].Value).IsEqualTo("reader-default");
    }

    [Test]
    public async Task GeneratedCodec_RejectsZeroWidthArrayAllocationAmplification()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoLargeZeroWidthCollection","namespace":"Dekaf.Tests","fields":[{"name":"items","type":{"type":"array","items":{"type":"record","name":"PocoLargeZeroWidthItem","fields":[]}}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-large-zero-width-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoLargeZeroWidthCollection.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-large-zero-width",
            Component = SerializationComponent.Value
        };
        var payload = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteBlockCount(140_000);
        Array.Resize(ref payload, 5 + writer.WrittenCount);

        await Assert.That(() => reader.Deserialize(payload, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("allocation exceeds");
    }

    [Test]
    public async Task GeneratedCodec_RejectsZeroWidthReferenceRecordAllocationAmplification()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoLargeZeroWidthReferenceCollection","namespace":"Dekaf.Tests","fields":[{"name":"items","type":{"type":"array","items":{"type":"record","name":"PocoLargeZeroWidthReferenceItem","fields":[]}}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-large-zero-width-reference-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoLargeZeroWidthReferenceCollection.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-large-zero-width-reference",
            Component = SerializationComponent.Value
        };
        var payload = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteBlockCount(140_000);
        Array.Resize(ref payload, 5 + writer.WrittenCount);

        await Assert.That(() => reader.Deserialize(payload, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("allocation exceeds");
    }

    [Test]
    public async Task GeneratedCodec_RejectsNestedZeroWidthReferenceRecordAllocationAmplification()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoNestedZeroWidthReferenceCollection","namespace":"Dekaf.Tests","fields":[{"name":"items","type":{"type":"array","items":{"type":"record","name":"PocoNestedZeroWidthReferenceItem","fields":[{"name":"child","type":{"type":"record","name":"PocoNestedZeroWidthReferenceChild","fields":[]}}]}}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-nested-zero-width-reference-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoNestedZeroWidthReferenceCollection.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-nested-zero-width-reference",
            Component = SerializationComponent.Value
        };
        var payload = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteBlockCount(140_000);
        Array.Resize(ref payload, 5 + writer.WrittenCount);

        await Assert.That(() => reader.Deserialize(payload, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("allocation exceeds");
    }

    [Test]
    public async Task GeneratedCodec_RejectsByteArrayDefaultAllocationAmplification()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoByteArrayDefaultCollection","namespace":"Dekaf.Tests","fields":[{"name":"values","type":{"type":"array","items":{"type":"record","name":"PocoByteArrayDefaultItem","fields":[]}}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-byte-array-default-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoByteArrayDefaultCollection.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-byte-array-default",
            Component = SerializationComponent.Value
        };
        var payload = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteBlockCount(60_000);
        writer.WriteBlockEnd();
        Array.Resize(ref payload, 5 + writer.WrittenCount);

        await Assert.That(() => reader.Deserialize(payload, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("allocation exceeds");
    }

    [Test]
    public async Task GeneratedCodec_RejectsUnboundedConstructorAllocationAmplification()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoAllocatingConstructorCollection","namespace":"Dekaf.Tests","fields":[{"name":"values","type":{"type":"array","items":{"type":"record","name":"PocoAllocatingConstructorItem","fields":[]}}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-allocating-constructor-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoAllocatingConstructorCollection.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-allocating-constructor",
            Component = SerializationComponent.Value
        };
        var payload = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteBlockCount(10_000);
        writer.WriteBlockEnd();
        Array.Resize(ref payload, 5 + writer.WrittenCount);

        await Assert.That(() => reader.Deserialize(payload, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("allocation exceeds");
    }

    [Test]
    public async Task GeneratedCodec_StopsReadingLaterMembersAfterWriteOverflow()
    {
        var value = new PocoWriteOverflowRecord { First = new string('a', 1024) };
        Span<byte> destination = stackalloc byte[8];
        var writer = new AvroValueWriter(destination);

        PocoWriteOverflowRecord.AvroCodec.Write(ref writer, value);

        await Assert.That(writer.IsComplete).IsFalse();
        await Assert.That(value.LaterMemberReadCount).IsEqualTo(0);
    }

    [Test]
    public async Task GeneratedCodec_StopsBeforeComplexMemberAfterFixedWriteOverflow()
    {
        var value = new PocoFixedWriteOverflowRecord { First = 1, Later = "unread" };
        Span<byte> destination = [];
        var writer = new AvroValueWriter(destination);

        PocoFixedWriteOverflowRecord.AvroCodec.Write(ref writer, value);

        await Assert.That(writer.IsComplete).IsFalse();
        await Assert.That(value.LaterMemberReadCount).IsEqualTo(0);
    }

    [Test]
    public async Task GeneratedCodec_StopsCollectionAfterExpensiveItemOverflow()
    {
        var first = new PocoCollectionOverflowItem { Value = new string('a', 1024) };
        var second = new PocoCollectionOverflowItem { Value = "unread" };
        var value = new PocoCollectionOverflowRecord { Items = [first, second] };
        Span<byte> destination = stackalloc byte[8];
        var writer = new AvroValueWriter(destination);

        PocoCollectionOverflowRecord.AvroCodec.Write(ref writer, value);

        await Assert.That(writer.IsComplete).IsFalse();
        await Assert.That(first.ValueReadCount).IsEqualTo(1);
        await Assert.That(second.ValueReadCount).IsEqualTo(0);
    }

    [Test]
    public async Task GeneratedCodec_RejectsZeroWidthMapAllocationAmplification()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoLargeZeroWidthMap","namespace":"Dekaf.Tests","fields":[{"name":"values","type":{"type":"map","values":{"type":"record","name":"PocoLargeZeroWidthItem","fields":[]}}}]}
            """;
        const int declaredCount = 140_000;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-large-zero-width-map-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoLargeZeroWidthMap.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-large-zero-width-map",
            Component = SerializationComponent.Value
        };
        var payload = new byte[5 + declaredCount + 8];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteBlockCount(declaredCount);

        await Assert.That(() => reader.Deserialize(payload, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("allocation exceeds");
    }

    [Test]
    public async Task GeneratedCodec_RejectsMultiBlockMapAllocationAmplification()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoLargeZeroWidthMap","namespace":"Dekaf.Tests","fields":[{"name":"values","type":{"type":"map","values":{"type":"record","name":"PocoLargeZeroWidthItem","fields":[]}}}]}
            """;
        const int secondBlockCount = 140_000;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-large-zero-width-multi-block-map-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoLargeZeroWidthMap.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-large-zero-width-multi-block-map",
            Component = SerializationComponent.Value
        };
        var payload = new byte[5 + secondBlockCount + 32];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteBlockCount(1);
        writer.WriteString("first");
        writer.WriteBlockCount(secondBlockCount);

        await Assert.That(() => reader.Deserialize(payload, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("allocation exceeds");
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
    [NotInParallel]
    public async Task GeneratedCodec_DirectPathAlternatingOversizedPayloadUsesBoundedSmallHint()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = new AvroPocoSchemaRegistrySerializer<
            CountingPocoPayload,
            CountingPocoPayloadCodec>(registry);
        var context = new SerializationContext
        {
            Topic = "poco-alternating-large-direct",
            Component = SerializationComponent.Value
        };
        var small = new CountingPocoPayload { Value = "small" };
        var large = new CountingPocoPayload { Value = new string('x', 1024 * 1024 + 1) };
        var destination = new ExactSizeBufferWriter(2 * 1024 * 1024 + 16);

        serializer.Serialize(large, ref destination, context);
        destination.Clear();
        serializer.Serialize(small, ref destination, context);
        destination.Clear();
        CountingPocoPayloadCodec.ResetWriteCount();

        for (var index = 0; index < 100; index++)
        {
            serializer.Serialize(large, ref destination, context);
            destination.Clear();
            serializer.Serialize(small, ref destination, context);
            await Assert.That(destination.LastMemorySizeHint).IsLessThanOrEqualTo(1024 * 1024);
            destination.Clear();
        }

        await Assert.That(CountingPocoPayloadCodec.WriteCount).IsEqualTo(200);
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
        destination.Clear();
        serializer.Serialize(value, ref destination, context);

        await Assert.That(destination.GetMemoryCallCount).IsEqualTo(1);
    }

    [Test]
    public async Task GeneratedCodec_RoundTripsNullableStructRecord()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoNullableStructRecord.CreateAvroSerializer(registry);
        await using var deserializer = PocoNullableStructRecord.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-nullable-struct-record",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();
        var expected = new PocoNullableStructRecord
        {
            Value = new PocoReadonlyRecord { Id = 42 }
        };

        serializer.Serialize(expected, ref destination, context);
        var actual = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Value).IsEqualTo(expected.Value);
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
    [NotInParallel]
    public async Task GeneratedCodec_RulesPathAlternatingOversizedPayloadUsesSingleTraversal()
    {
        using var registry = new MockSchemaRegistryClient();
        var ruleExecutor = new CapturingPayloadCapacityRuleExecutor();
        await using var serializer = new AvroPocoSchemaRegistrySerializer<
            CountingPocoPayload,
            CountingPocoPayloadCodec>(
            registry,
            new AvroSerializerConfig { RuleExecutor = ruleExecutor });
        var context = new SerializationContext
        {
            Topic = "poco-alternating-large-rules",
            Component = SerializationComponent.Value
        };
        var small = new CountingPocoPayload { Value = "small" };
        var large = new CountingPocoPayload { Value = new string('x', 1024 * 1024 + 1) };
        var destination = new ExactSizeBufferWriter(2 * 1024 * 1024 + 16);

        serializer.Serialize(large, ref destination, context);
        destination.Clear();
        serializer.Serialize(small, ref destination, context);
        destination.Clear();
        CountingPocoPayloadCodec.ResetWriteCount();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
        {
            serializer.Serialize(large, ref destination, context);
            destination.Clear();
            serializer.Serialize(small, ref destination, context);
            destination.Clear();
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(CountingPocoPayloadCodec.WriteCount).IsEqualTo(200);
        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(ruleExecutor.LastPayloadBufferCapacity).IsLessThanOrEqualTo(1024 * 1024);
    }

    [Test]
    public async Task GeneratedCodec_RulesPathReentrantSerializationPreservesOuterPayload()
    {
        using var registry = new MockSchemaRegistryClient();
        var executor = new ReentrantRuleExecutor();
        await using var serializer = PocoReadonlyRecord.CreateAvroSerializer(
            registry,
            new AvroSerializerConfig { RuleExecutor = executor });
        await using var deserializer = PocoReadonlyRecord.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-reentrant-rules",
            Component = SerializationComponent.Value
        };
        var outerDestination = new ArrayBufferWriter<byte>();
        var nestedDestination = new ArrayBufferWriter<byte>();
        executor.Reenter = () =>
        {
            nestedDestination.Clear();
            serializer.Serialize(new PocoReadonlyRecord { Id = 7 }, ref nestedDestination, context);
        };

        serializer.Serialize(new PocoReadonlyRecord { Id = 42 }, ref outerDestination, context);

        var outer = deserializer.Deserialize(outerDestination.WrittenMemory, context);
        var nested = deserializer.Deserialize(nestedDestination.WrittenMemory, context);
        await Assert.That(outer.Id).IsEqualTo(42);
        await Assert.That(nested.Id).IsEqualTo(7);
    }

    [Test]
    public async Task GeneratedCodec_RuleContextsProvideTaggedFieldTransformer()
    {
        using var registry = new MockSchemaRegistryClient();
        _ = await registry.RegisterSchemaAsync(
            "poco-tagged-rule-context-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = PocoWireRecord.AvroCodec.SchemaJson,
                RuleSet = new SchemaRuleSet
                {
                    DomainRules =
                    [
                        new SchemaRule
                        {
                            Name = "tagged-fields",
                            Kind = SchemaRuleKind.Transform,
                            Mode = SchemaRuleMode.WriteRead,
                            Type = "CAPTURE",
                            Tags = new HashSet<string>(StringComparer.Ordinal) { "PII" }
                        }
                    ]
                }
            });
        var executor = new CapturingTaggedFieldRuleExecutor();
        var serializerConfig = new AvroSerializerConfig
        {
            UseLatestVersion = true,
            RuleExecutor = executor
        };
        var deserializerConfig = new AvroDeserializerConfig { RuleExecutor = executor };
        await using var serializer = PocoWireRecord.CreateAvroSerializer(registry, serializerConfig);
        await using var deserializer = PocoWireRecord.CreateAvroDeserializer(registry, deserializerConfig);
        var context = new SerializationContext
        {
            Topic = "poco-tagged-rule-context",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        serializer.Serialize(new PocoWireRecord { Id = 42, Name = "secret" }, ref destination, context);
        _ = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(executor.SerializedContextHadTransformer).IsTrue();
        await Assert.That(executor.DeserializedContextHadTransformer).IsTrue();
    }

    [Test]
    public async Task GeneratedCodec_RulesPathReentrantGrowthAllocatesZeroAfterWarmup()
    {
        using var registry = new MockSchemaRegistryClient();
        var executor = new ReentrantRuleExecutor();
        var config = new AvroSerializerConfig { RuleExecutor = executor };
        await using var outerSerializer = PocoReadonlyRecord.CreateAvroSerializer(registry, config);
        await using var nestedSerializer = PocoGrowingPayload.CreateAvroSerializer(registry, config);
        var outerContext = new SerializationContext
        {
            Topic = "poco-reentrant-growth-outer",
            Component = SerializationComponent.Value
        };
        var nestedContext = new SerializationContext
        {
            Topic = "poco-reentrant-growth-nested",
            Component = SerializationComponent.Value
        };
        var outerValue = new PocoReadonlyRecord { Id = 42 };
        var nestedValue = new PocoGrowingPayload { Value = new string('x', 64 * 1024) };
        var outerDestination = new ExactSizeBufferWriter(128);
        var nestedDestination = new ExactSizeBufferWriter(128 * 1024);
        executor.Reenter = () =>
        {
            nestedDestination.Clear();
            nestedSerializer.Serialize(nestedValue, ref nestedDestination, nestedContext);
        };

        await outerSerializer.WarmupAsync(outerContext.Topic);
        await nestedSerializer.WarmupAsync(nestedContext.Topic);
        for (var index = 0; index < 16; index++)
        {
            outerDestination.Clear();
            outerSerializer.Serialize(outerValue, ref outerDestination, outerContext);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
        {
            outerDestination.Clear();
            outerSerializer.Serialize(outerValue, ref outerDestination, outerContext);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task GeneratedCodec_RulesPathIsolatesSizingStateAcrossCodecTypes()
    {
        using var registry = new MockSchemaRegistryClient();
        var config = new AvroSerializerConfig { RuleExecutor = PassThroughRuleExecutor.Instance };
        await using var first = new AvroPocoSchemaRegistrySerializer<
            CountingPocoPayload,
            CountingPocoPayloadCodec>(registry, config);
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
        var value = new string('x', 1024 * 1024 + 1);
        var firstLarge = new CountingPocoPayload { Value = value };
        var firstSmall = new CountingPocoPayload { Value = "small" };
        var secondLarge = new PocoSecondGrowingPayload { Value = value };
        var secondSmall = new PocoSecondGrowingPayload { Value = "small" };
        var firstDestination = new ExactSizeBufferWriter(2 * 1024 * 1024 + 16);
        var secondDestination = new ExactSizeBufferWriter(2 * 1024 * 1024 + 16);

        await first.WarmupAsync(firstContext.Topic);
        await second.WarmupAsync(secondContext.Topic);
        first.Serialize(firstLarge, ref firstDestination, firstContext);
        firstDestination.Clear();
        first.Serialize(firstSmall, ref firstDestination, firstContext);
        firstDestination.Clear();
        second.Serialize(secondLarge, ref secondDestination, secondContext);
        secondDestination.Clear();
        second.Serialize(secondSmall, ref secondDestination, secondContext);
        secondDestination.Clear();
        CountingPocoPayloadCodec.ResetWriteCount();

        var before = GC.GetAllocatedBytesForCurrentThread();
        second.Serialize(secondLarge, ref secondDestination, secondContext);
        first.Serialize(firstLarge, ref firstDestination, firstContext);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(CountingPocoPayloadCodec.WriteCount).IsEqualTo(1);
        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task GeneratedCodec_RulesPathIsolatesSizingStateAcrossSerializerInstances()
    {
        using var registry = new MockSchemaRegistryClient();
        var config = new AvroSerializerConfig { RuleExecutor = PassThroughRuleExecutor.Instance };
        await using var first = new AvroPocoSchemaRegistrySerializer<
            CountingPocoPayload,
            CountingPocoPayloadCodec>(registry, config);
        await using var second = new AvroPocoSchemaRegistrySerializer<
            CountingPocoPayload,
            CountingPocoPayloadCodec>(registry, config);
        var firstContext = new SerializationContext
        {
            Topic = "poco-instance-buffer-first",
            Component = SerializationComponent.Value
        };
        var secondContext = new SerializationContext
        {
            Topic = "poco-instance-buffer-second",
            Component = SerializationComponent.Value
        };
        var large = new CountingPocoPayload { Value = new string('x', 1024 * 1024 + 1) };
        var small = new CountingPocoPayload { Value = "small" };
        var firstDestination = new ExactSizeBufferWriter(2 * 1024 * 1024 + 16);
        var secondDestination = new ExactSizeBufferWriter(2 * 1024 * 1024 + 16);

        await first.WarmupAsync(firstContext.Topic);
        await second.WarmupAsync(secondContext.Topic);
        first.Serialize(large, ref firstDestination, firstContext);
        firstDestination.Clear();
        first.Serialize(small, ref firstDestination, firstContext);
        firstDestination.Clear();
        second.Serialize(small, ref secondDestination, secondContext);
        secondDestination.Clear();
        CountingPocoPayloadCodec.ResetWriteCount();

        var before = GC.GetAllocatedBytesForCurrentThread();
        first.Serialize(large, ref firstDestination, firstContext);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(CountingPocoPayloadCodec.WriteCount).IsEqualTo(1);
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
        var schemaId = BinaryPrimitives.ReadInt32BigEndian(destination.WrittenSpan.Slice(1, 4));
        await deserializer.WarmupAsync(schemaId, context);

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
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task GeneratedCodec_RejectsOutOfRangeWriterIntPromotion(int invalidFieldIndex)
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoNumericPromotions","namespace":"Dekaf.Tests","fields":[{"name":"LongValue","type":"int"},{"name":"FloatValue","type":"int"},{"name":"DoubleValue","type":"int"}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-numeric-promotions-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoNumericPromotions.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-numeric-promotions",
            Component = SerializationComponent.Value
        };
        var payload = new byte[40];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        for (var fieldIndex = 0; fieldIndex < 3; fieldIndex++)
            writer.WriteInt64(fieldIndex == invalidFieldIndex ? (long)int.MaxValue + 1 : 0);
        Array.Resize(ref payload, 5 + writer.WrittenCount);

        await Assert.That(() => reader.Deserialize(payload, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("Int32 range");
    }

    [Test]
    [Arguments("int")]
    [Arguments("date")]
    [Arguments("time-millis")]
    public async Task GeneratedCodec_RejectsOutOfRangeSkippedWriterInt(string writerType)
    {
        var removedFieldType = writerType == "int"
            ? "\"int\""
            : $"{{\"type\":\"int\",\"logicalType\":\"{writerType}\"}}";
        var writerSchemaJson =
            $$"""
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"},{"name":"removed","type":{{removedFieldType}}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-skipped-int-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoEvolved.CreateAvroDeserializer(registry);
        var payload = new byte[32];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt32(42);
        writer.WriteInt64((long)int.MaxValue + 1);
        var context = new SerializationContext
        {
            Topic = "poco-skipped-int",
            Component = SerializationComponent.Value
        };
        var payloadLength = 5 + writer.WrittenCount;

        await Assert.That(() => reader.Deserialize(payload.AsMemory(0, payloadLength), context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("Int32 range");
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
    public async Task GeneratedCodec_RejectsInvalidUtf8WhenPromotingStringToBytes()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoBytesPromotion","namespace":"Dekaf.Tests","fields":[{"name":"Value","type":"string"}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-invalid-string-to-bytes-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoBytesPromotion.CreateAvroDeserializer(registry);
        var payload = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt32(1);
        var invalidByteIndex = 5 + writer.WrittenCount;
        payload[invalidByteIndex] = 0xFF;
        var context = new SerializationContext
        {
            Topic = "poco-invalid-string-to-bytes",
            Component = SerializationComponent.Value
        };

        await Assert.That(() => reader.Deserialize(payload.AsMemory(0, invalidByteIndex + 1), context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("UTF-8");
    }

    [Test]
    public async Task GeneratedCodec_SkipsFiniteRecursiveWriterRecord()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"},{"name":"recursive","type":["null","Dekaf.Tests.PocoEvolved"]}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var writer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var reader = PocoEvolved.CreateAvroDeserializer(registry);
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var nested = new GenericRecord(writerSchema);
        nested.Add("legacy_id", 7);
        nested.Add("recursive", null);
        var root = new GenericRecord(writerSchema);
        root.Add("legacy_id", 42);
        root.Add("recursive", nested);
        var context = new SerializationContext
        {
            Topic = "poco-recursive-evolution",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        writer.Serialize(root, ref destination, context);
        var actual = reader.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Id).IsEqualTo(42L);
        await Assert.That(actual.Note).IsEqualTo("added-by-reader");
    }

    [Test]
    public async Task GeneratedCodec_RejectsInvalidOrdinalInSkippedWriterEnum()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"},{"name":"removed","type":{"type":"enum","name":"RemovedStatus","symbols":["A","B"]}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-skipped-enum-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoEvolved.CreateAvroDeserializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-skipped-enum",
            Component = SerializationComponent.Value
        };
        var payload = new byte[16];
        payload[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt32(42);
        writer.WriteIndex(2);
        Array.Resize(ref payload, 5 + writer.WrittenCount);

        await Assert.That(() => reader.Deserialize(payload, context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("index is out of range");
    }

    [Test]
    public async Task GeneratedCodec_RejectsInvalidUtf16String()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var serializer = PocoGrowingPayload.CreateAvroSerializer(registry);
        var context = new SerializationContext
        {
            Topic = "poco-invalid-utf16",
            Component = SerializationComponent.Value
        };
        await serializer.WarmupAsync(context.Topic);
        var destination = new ArrayBufferWriter<byte>();
        var value = new PocoGrowingPayload { Value = "\uD800" };

        await Assert.That(() => serializer.Serialize(value, ref destination, context))
            .Throws<EncoderFallbackException>();
    }

    [Test]
    public async Task GeneratedCodec_RejectsInvalidUtf8String()
    {
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-invalid-utf8-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = PocoWireRecord.AvroCodec.SchemaJson
            });
        await using var reader = PocoWireRecord.CreateAvroDeserializer(registry);
        var payload = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt32(42);
        writer.WriteInt32(1);
        var invalidByteIndex = 5 + writer.WrittenCount;
        payload[invalidByteIndex] = 0xFF;
        var context = new SerializationContext
        {
            Topic = "poco-invalid-utf8",
            Component = SerializationComponent.Value
        };

        await Assert.That(() => reader.Deserialize(payload.AsMemory(0, invalidByteIndex + 1), context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("UTF-8");
    }

    [Test]
    [Arguments("string")]
    [Arguments("uuid")]
    public async Task GeneratedCodec_RejectsInvalidUtf8InSkippedString(string writerType)
    {
        var removedFieldType = writerType == "string"
            ? "\"string\""
            : "{\"type\":\"string\",\"logicalType\":\"uuid\"}";
        var writerSchemaJson =
            $$"""
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"},{"name":"removed","type":{{removedFieldType}}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-invalid-skipped-string-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoEvolved.CreateAvroDeserializer(registry);
        var payload = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt32(42);
        writer.WriteInt32(1);
        var invalidByteIndex = 5 + writer.WrittenCount;
        payload[invalidByteIndex] = 0xFF;
        var context = new SerializationContext
        {
            Topic = "poco-invalid-skipped-string",
            Component = SerializationComponent.Value
        };

        await Assert.That(() => reader.Deserialize(payload.AsMemory(0, invalidByteIndex + 1), context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("UTF-8");
    }

    [Test]
    public async Task GeneratedCodec_RejectsInvalidUtf8InSkippedMapKey()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"},{"name":"removed","type":{"type":"map","values":"null"}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-invalid-skipped-map-key-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoEvolved.CreateAvroDeserializer(registry);
        var payload = new byte[24];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt32(42);
        writer.WriteBlockCount(1);
        writer.WriteInt32(1);
        var invalidByteIndex = 5 + writer.WrittenCount;
        payload[invalidByteIndex] = 0xFF;
        var context = new SerializationContext
        {
            Topic = "poco-invalid-skipped-map-key",
            Component = SerializationComponent.Value
        };

        await Assert.That(() => reader.Deserialize(payload.AsMemory(0, invalidByteIndex + 1), context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("UTF-8");
    }

    [Test]
    public async Task GeneratedCodec_RejectsExcessiveRecursiveSkipDepth()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"},{"name":"recursive","type":["null","Dekaf.Tests.PocoEvolved"]}]}
            """;
        const int nestedRecordCount = 300;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-recursive-depth-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoEvolved.CreateAvroDeserializer(registry);
        var payload = new byte[5 + (nestedRecordCount + 1) * 2];
        payload[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        for (var index = 0; index < nestedRecordCount; index++)
        {
            writer.WriteInt32(0);
            writer.WriteIndex(1);
        }
        writer.WriteInt32(0);
        writer.WriteIndex(0);
        var context = new SerializationContext
        {
            Topic = "poco-recursive-depth",
            Component = SerializationComponent.Value
        };
        var payloadLength = 5 + writer.WrittenCount;

        await Assert.That(() => reader.Deserialize(payload.AsMemory(0, payloadLength), context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("nesting exceeds");
    }

    [Test]
    public async Task GeneratedCodec_RejectsExcessiveCompositeSkipDepth()
    {
        const int nestedNodeCount = 300;
        var kinds = new AvroPocoTypeKind[nestedNodeCount];
        var node = new AvroPocoReadNode(AvroPocoTypeKind.Null);
        for (var index = 0; index < kinds.Length; index++)
        {
            kinds[index] = (index % 3) switch
            {
                0 => AvroPocoTypeKind.Array,
                1 => AvroPocoTypeKind.Map,
                _ => AvroPocoTypeKind.Union
            };
            node = kinds[index] switch
            {
                AvroPocoTypeKind.Array => new AvroPocoReadNode(AvroPocoTypeKind.Array) { Item = node },
                AvroPocoTypeKind.Map => new AvroPocoReadNode(AvroPocoTypeKind.Map) { Item = node },
                _ => new AvroPocoReadNode(AvroPocoTypeKind.Union) { Branches = new[] { node } }
            };
        }

        var payload = new byte[nestedNodeCount * 2];
        var writer = new AvroValueWriter(payload);
        for (var index = kinds.Length - 1; index >= 0; index--)
        {
            switch (kinds[index])
            {
                case AvroPocoTypeKind.Array:
                    writer.WriteBlockCount(1);
                    break;
                case AvroPocoTypeKind.Map:
                    writer.WriteBlockCount(1);
                    writer.WriteString(string.Empty);
                    break;
                case AvroPocoTypeKind.Union:
                    writer.WriteIndex(0);
                    break;
            }
        }
        var payloadLength = writer.WrittenCount;

        await Assert.That(() =>
            {
                var reader = new AvroValueReader(payload.AsSpan(0, payloadLength));
                reader.Skip(node);
            })
            .Throws<InvalidDataException>()
            .WithMessageContaining("nesting exceeds");
    }

    [Test]
    public async Task GeneratedCodec_RejectsCumulativeRemovedArrayCount()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"},{"name":"removed","type":{"type":"array","items":"null"}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-removed-array-count-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoEvolved.CreateAvroDeserializer(registry);
        var payload = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt32(0);
        writer.WriteBlockCount(1_048_576);
        writer.WriteBlockCount(1);
        writer.WriteBlockEnd();
        var context = new SerializationContext
        {
            Topic = "poco-removed-array-count",
            Component = SerializationComponent.Value
        };
        var payloadLength = 5 + writer.WrittenCount;

        await Assert.That(() => reader.Deserialize(payload.AsMemory(0, payloadLength), context))
            .Throws<InvalidDataException>()
            .WithMessageContaining("supported limit");
    }

    [Test]
    [Arguments("null", "\"null\"")]
    [Arguments("empty-record", "{\"type\":\"record\",\"name\":\"RemovedEmptyRecord\",\"fields\":[]}")]
    public async Task GeneratedCodec_SkipsLargeZeroWidthRemovedArray(string caseName, string itemSchema)
    {
        var writerSchemaJson = string.Concat(
            """{"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"},{"name":"removed","type":{"type":"array","items":""",
            itemSchema,
            """}}]}""");
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            $"poco-zero-width-{caseName}-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoEvolved.CreateAvroDeserializer(registry);
        var payload = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt32(42);
        writer.WriteBlockCount(1_048_576);
        writer.WriteBlockEnd();
        var context = new SerializationContext
        {
            Topic = $"poco-zero-width-{caseName}",
            Component = SerializationComponent.Value
        };

        var actual = reader.Deserialize(payload.AsMemory(0, 5 + writer.WrittenCount), context);

        await Assert.That(actual.Id).IsEqualTo(42L);
        await Assert.That(actual.Note).IsEqualTo("added-by-reader");
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
    public async Task GeneratedCodec_AsyncPreparationFetchesPlanWithoutBlockingDeserialize()
    {
        var registry = Substitute.For<Dekaf.SchemaRegistry.ISchemaRegistryClient>();
        var schema = new Dekaf.SchemaRegistry.Schema
        {
            SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
            SchemaString = PocoWireRecord.AvroCodec.SchemaJson
        };
        var schemaFetch = new TaskCompletionSource<Dekaf.SchemaRegistry.Schema>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        registry.GetSchemaAsync(42, Arg.Any<CancellationToken>()).Returns(schemaFetch.Task);
        await using var reader = PocoWireRecord.CreateAvroDeserializer(registry);
        ReadOnlyMemory<byte> payload = new byte[] { 0, 0, 0, 0, 42, 84, 6, (byte)'A', (byte)'d', (byte)'a' };
        var context = new SerializationContext
        {
            Topic = "poco-sync-cache-miss",
            Component = SerializationComponent.Value
        };

        await Assert.That(() => reader.Deserialize(payload, context))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("not cached");

        var preparation = ((IAsyncDeserializerPreparer<PocoWireRecord>)reader).PrepareAsync(payload, context);
        await Assert.That(preparation.IsCompleted).IsFalse();

        schemaFetch.SetResult(schema);
        await preparation;
        var actual = reader.Deserialize(payload, context);

        await Assert.That(actual.Id).IsEqualTo(42);
        await Assert.That(actual.Name).IsEqualTo("Ada");
        _ = registry.Received(1).GetSchemaAsync(42, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GeneratedCodec_ContextWarmupWarmsScopedSchemaAndMigrationPlan()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var writerSchemaId = await registry.RegisterSchemaAsync(
            "poco-async-migration-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        _ = await registry.RegisterSchemaAsync(
            "poco-async-migration-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = PocoEvolved.AvroCodec.SchemaJson
            });
        var nonCachingRegistry = CreateNonCachingRegistry(registry);
        await using var reader = PocoEvolved.CreateAvroDeserializer(
            nonCachingRegistry,
            new AvroDeserializerConfig { UseLatestVersion = true });
        var payload = new byte[32];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), writerSchemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt32(42);
        var context = new SerializationContext
        {
            Topic = "poco-async-migration",
            Component = SerializationComponent.Value
        };
        var data = payload.AsMemory(0, 5 + writer.WrittenCount);

        await reader.WarmupAsync(writerSchemaId, context);
        nonCachingRegistry.ClearReceivedCalls();
        var actual = reader.Deserialize(data, context);

        await Assert.That(actual.Id).IsEqualTo(42L);
        await Assert.That(actual.Note).IsEqualTo("added-by-reader");
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .GetSchemaAsync(default, default(CancellationToken));
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .GetSchemaAsync(default, default!, default(CancellationToken));
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .GetSchemaBySubjectAsync(default!, default!, default(CancellationToken));
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .LookupSchemaAsync(default!, default!, default, default, default(CancellationToken));
    }

    [Test]
    public async Task GeneratedCodec_AsyncPreparationRefreshesExpiredMigrationPlan()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":"int"}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var writerSchemaId = await registry.RegisterSchemaAsync(
            "poco-expired-migration-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        _ = await registry.RegisterSchemaAsync(
            "poco-expired-migration-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = PocoEvolved.AvroCodec.SchemaJson
            });
        var nonCachingRegistry = CreateNonCachingRegistry(registry, latestCacheTtlSecs: 0);
        await using var reader = PocoEvolved.CreateAvroDeserializer(
            nonCachingRegistry,
            new AvroDeserializerConfig { UseLatestVersion = true });
        var payload = new byte[32];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), writerSchemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt32(42);
        var context = new SerializationContext
        {
            Topic = "poco-expired-migration",
            Component = SerializationComponent.Value
        };
        var preparer = (IAsyncDeserializerPreparer<PocoEvolved>)reader;
        var data = payload.AsMemory(0, 5 + writer.WrittenCount);

        await preparer.PrepareAsync(data, context);
        nonCachingRegistry.ClearReceivedCalls();
        await Assert.That(preparer.TryDeserialize(data, context, out _)).IsTrue();
        await Assert.That(preparer.TryDeserialize(data, context, out _)).IsFalse();
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .GetSchemaBySubjectAsync(default!, default!, default(CancellationToken));

        await preparer.PrepareAsync(data, context);
        _ = nonCachingRegistry.ReceivedWithAnyArgs()
            .GetSchemaBySubjectAsync(default!, default!, default(CancellationToken));
        nonCachingRegistry.ClearReceivedCalls();
        await Assert.That(preparer.TryDeserialize(data, context, out var actual)).IsTrue();
        await Assert.That(actual.Id).IsEqualTo(42L);
        _ = nonCachingRegistry.DidNotReceiveWithAnyArgs()
            .GetSchemaBySubjectAsync(default!, default!, default(CancellationToken));
    }

    [Test]
    public async Task GeneratedCodec_BoundsCompletedReaderPlans()
    {
        using var registry = new MockSchemaRegistryClient();
        await using var reader = PocoWireRecord.CreateAvroDeserializer(registry);
        var schema = new Dekaf.SchemaRegistry.Schema
        {
            SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
            SchemaString = PocoWireRecord.AvroCodec.SchemaJson
        };
        var schemaIds = new int[AvroPocoSchemaRegistryDeserializer<PocoWireRecord, PocoWireRecord.AvroCodec>
            .MaxCachedPlans + 1];

        for (var index = 0; index < schemaIds.Length; index++)
        {
            schemaIds[index] = await registry.RegisterSchemaAsync("poco-plan-cache-value", schema);
            await reader.WarmupAsync(schemaIds[index]);
        }

        await Assert.That(reader.CachedPlanCount)
            .IsEqualTo(AvroPocoSchemaRegistryDeserializer<PocoWireRecord, PocoWireRecord.AvroCodec>.MaxCachedPlans);
        var fetchCount = registry.GetSchemaCallCount;

        await reader.WarmupAsync(schemaIds[0]);

        await Assert.That(registry.GetSchemaCallCount).IsEqualTo(fetchCount + 1);
        await Assert.That(reader.CachedPlanCount)
            .IsEqualTo(AvroPocoSchemaRegistryDeserializer<PocoWireRecord, PocoWireRecord.AvroCodec>.MaxCachedPlans);
    }

    [Test]
    public async Task GeneratedCodec_RulesPathRebuildsEvictedPlanWithoutRegistryCache()
    {
        using var registry = new MockSchemaRegistryClient();
        var schema = new Dekaf.SchemaRegistry.Schema
        {
            SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
            SchemaString = PocoWireRecord.AvroCodec.SchemaJson
        };
        var schemaIds = new int[
            AvroPocoSchemaRegistryDeserializer<PocoWireRecord, PocoWireRecord.AvroCodec>.MaxCachedPlans + 1];
        for (var index = 0; index < schemaIds.Length; index++)
            schemaIds[index] = await registry.RegisterSchemaAsync("poco-plan-eviction-value", schema);

        var nonCachingRegistry = CreateNonCachingRegistry(registry);
        await using var reader = PocoWireRecord.CreateAvroDeserializer(
            nonCachingRegistry,
            new AvroDeserializerConfig { RuleExecutor = PassThroughRuleExecutor.Instance });
        for (var index = 0; index < schemaIds.Length; index++)
            await reader.WarmupAsync(schemaIds[index]);

        var payload = new byte[64];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaIds[0]);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        PocoWireRecord.AvroCodec.Write(ref writer, new PocoWireRecord { Id = 42, Name = "revisited" });
        var context = new SerializationContext
        {
            Topic = "poco-plan-eviction",
            Component = SerializationComponent.Value
        };
        var payloadLength = 5 + writer.WrittenCount;

        var actual = reader.Deserialize(payload.AsMemory(0, payloadLength), context);

        await Assert.That(actual.Id).IsEqualTo(42);
        await Assert.That(actual.Name).IsEqualTo("revisited");
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
    public async Task GeneratedCodec_ValidatesLatestSchemaWithReferences()
    {
        const string addressSchemaJson =
            """
            {"type":"record","name":"PocoAddress","namespace":"Dekaf.Tests","fields":[{"name":"City","type":"string"},{"name":"PostCode","type":"string"}]}
            """;
        const string rootSchemaJson =
            """
            {"type":"record","name":"PocoReferencedRoot","namespace":"Dekaf.Tests","fields":[{"name":"Address","type":"Dekaf.Tests.PocoAddress"}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        _ = await registry.RegisterSchemaAsync(
            "poco-referenced-latest-address-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = addressSchemaJson
            });
        _ = await registry.RegisterSchemaAsync(
            "poco-referenced-latest-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = rootSchemaJson,
                References =
                [
                    new Dekaf.SchemaRegistry.SchemaReference
                    {
                        Name = "Dekaf.Tests.PocoAddress",
                        Subject = "poco-referenced-latest-address-value",
                        Version = 1
                    }
                ]
            });
        await using var serializer = PocoReferencedRoot.CreateAvroSerializer(
            registry,
            new AvroSerializerConfig { UseLatestVersion = true });

        await serializer.WarmupAsync("poco-referenced-latest");
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
    public async Task GeneratedCodec_ReadsUnsupportedLogicalFieldByBaseType()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":{"type":"long","logicalType":"local-timestamp-micros"}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-read-logical-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoEvolved.CreateAvroDeserializer(registry);
        var payload = new byte[15];
        payload[0] = 0;
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt64(42);
        var context = new SerializationContext
        {
            Topic = "poco-read-logical",
            Component = SerializationComponent.Value
        };

        var actual = reader.Deserialize(payload.AsMemory(0, 5 + writer.WrittenCount), context);

        await Assert.That(actual.Id).IsEqualTo(42L);
        await Assert.That(actual.Note).IsEqualTo("added-by-reader");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task GeneratedCodec_ReadsRecognizedLogicalFieldByBaseType(bool writerUnion)
    {
        const string directWriterSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":{"type":"long","logicalType":"timestamp-micros"}}]}
            """;
        const string unionWriterSchemaJson =
            """
            {"type":"record","name":"PocoEvolved","namespace":"Dekaf.Tests","fields":[{"name":"legacy_id","type":["null",{"type":"long","logicalType":"timestamp-micros"}]}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-read-recognized-logical-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerUnion ? unionWriterSchemaJson : directWriterSchemaJson
            });
        await using var reader = PocoEvolved.CreateAvroDeserializer(registry);
        var payload = new byte[16];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        if (writerUnion)
            writer.WriteIndex(1);
        writer.WriteInt64(1234);
        var context = new SerializationContext
        {
            Topic = "poco-read-recognized-logical",
            Component = SerializationComponent.Value
        };
        var payloadLength = 5 + writer.WrittenCount;

        var actual = reader.Deserialize(payload.AsMemory(0, payloadLength), context);

        await Assert.That(actual.Id).IsEqualTo(1234L);
        await Assert.That(actual.Note).IsEqualTo("added-by-reader");
    }

    [Test]
    public async Task GeneratedCodec_ReadsRecognizedLogicalFieldIntoUnderlyingUnion()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoLogicalBaseUnion","namespace":"Dekaf.Tests","fields":[{"name":"Value","type":{"type":"long","logicalType":"timestamp-micros"}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-logical-base-union-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoLogicalBaseUnion.CreateAvroDeserializer(registry);
        var payload = new byte[15];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt64(1234);
        var context = new SerializationContext
        {
            Topic = "poco-logical-base-union",
            Component = SerializationComponent.Value
        };

        var actual = reader.Deserialize(payload.AsMemory(0, 5 + writer.WrittenCount), context);

        await Assert.That(actual.Value).IsEqualTo(1234L);
    }

    [Test]
    public async Task GeneratedCodec_ReadsPrimitiveFieldAsLogicalType()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoTimestampEvolution","namespace":"Dekaf.Tests","fields":[{"name":"Value","type":"long"}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-primitive-logical-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoTimestampEvolution.CreateAvroDeserializer(registry);
        var payload = new byte[15];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt64(1234);
        var context = new SerializationContext
        {
            Topic = "poco-primitive-logical",
            Component = SerializationComponent.Value
        };

        var actual = reader.Deserialize(payload.AsMemory(0, 5 + writer.WrittenCount), context);

        await Assert.That(actual.Value).IsEqualTo(DateTime.UnixEpoch.AddTicks(12_340));
    }

    [Test]
    public async Task GeneratedCodec_RejectsDifferentTimestampLogicalUnits()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoTimestampEvolution","namespace":"Dekaf.Tests","fields":[{"name":"Value","type":{"type":"long","logicalType":"timestamp-millis"}}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        var schemaId = await registry.RegisterSchemaAsync(
            "poco-timestamp-unit-value",
            new Dekaf.SchemaRegistry.Schema
            {
                SchemaType = Dekaf.SchemaRegistry.SchemaType.Avro,
                SchemaString = writerSchemaJson
            });
        await using var reader = PocoTimestampEvolution.CreateAvroDeserializer(registry);
        var payload = new byte[15];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(1, 4), schemaId);
        var writer = new AvroValueWriter(payload.AsSpan(5));
        writer.WriteInt64(1000);
        var context = new SerializationContext
        {
            Topic = "poco-timestamp-unit",
            Component = SerializationComponent.Value
        };
        var payloadLength = 5 + writer.WrittenCount;

        await Assert.That(() => reader.Deserialize(payload.AsMemory(0, payloadLength), context))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("incompatible");
    }

    [Test]
    public async Task GeneratedCodec_DefersMismatchedLogicalWriterUnionBranchUntilSelected()
    {
        const string writerSchemaJson =
            """
            {"type":"record","name":"PocoLogicalUnion","namespace":"Dekaf.Tests","fields":[{"name":"Value","type":[{"type":"long","logicalType":"timestamp-millis"},"string"]}]}
            """;
        using var registry = new MockSchemaRegistryClient();
        await using var writer = new AvroSchemaRegistrySerializer<GenericRecord>(registry);
        await using var reader = new AvroPocoSchemaRegistryDeserializer<
            PocoLogicalUnionValue,
            PocoLogicalUnionCodec>(registry);
        var writerSchema = (RecordSchema)Schema.Parse(writerSchemaJson);
        var expected = new GenericRecord(writerSchema);
        expected.Add("Value", "compatible");
        var context = new SerializationContext
        {
            Topic = "poco-logical-union",
            Component = SerializationComponent.Value
        };
        var destination = new ArrayBufferWriter<byte>();

        writer.Serialize(expected, ref destination, context);
        var actual = reader.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Value).IsEqualTo("compatible");
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
        var writerSchemaId = await registry.RegisterSchemaAsync(
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
        var migrationHandler = new FixedPayloadMigrationHandler(
            migratedPayload.AsMemory(0, migratedWriter.WrittenCount));
        var executor = new SchemaRegistryRuleExecutor([migrationHandler]);
        var nonCachingRegistry = CreateNonCachingRegistry(registry);
        await using var deserializer = PocoEvolved.CreateAvroDeserializer(
            nonCachingRegistry,
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
        await deserializer.WarmupAsync(writerSchemaId);

        var actual = deserializer.Deserialize(destination.WrittenMemory, context);

        await Assert.That(actual.Id).IsEqualTo(42L);
        await Assert.That(actual.Note).IsEqualTo("migrated-layout");
        await Assert.That(migrationHandler.ContextHadTaggedFieldTransformer).IsTrue();
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

    private static Dekaf.SchemaRegistry.ISchemaRegistryClient CreateNonCachingRegistry(
        MockSchemaRegistryClient registry,
        int latestCacheTtlSecs = -1)
    {
        var client = Substitute.For<Dekaf.SchemaRegistry.ISchemaRegistryClient>();
        client.LatestCacheTtlSecs.Returns(latestCacheTtlSecs);
        client.GetSchemaAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => registry.GetSchemaAsync(
                call.ArgAt<int>(0),
                call.ArgAt<CancellationToken>(1)));
        client.GetSchemaAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => registry.GetSchemaAsync(
                call.ArgAt<int>(0),
                call.ArgAt<string>(1),
                call.ArgAt<CancellationToken>(2)));
        client.GetSchemaBySubjectAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call => registry.GetSchemaBySubjectAsync(
                call.ArgAt<string>(0),
                call.ArgAt<string>(1),
                call.ArgAt<CancellationToken>(2)));
        client.GetSchemaBySubjectAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(call => registry.GetSchemaBySubjectAsync(
                call.ArgAt<string>(0),
                call.ArgAt<string>(1),
                call.ArgAt<bool>(2),
                call.ArgAt<CancellationToken>(3)));
        client.LookupSchemaAsync(
                Arg.Any<string>(),
                Arg.Any<Dekaf.SchemaRegistry.Schema>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(call => registry.LookupSchemaAsync(
                call.ArgAt<string>(0),
                call.ArgAt<Dekaf.SchemaRegistry.Schema>(1),
                call.ArgAt<bool>(2),
                call.ArgAt<bool>(3),
                call.ArgAt<CancellationToken>(4)));
        return client;
    }

    private sealed class CapturingTaggedFieldRuleExecutor : ISchemaRegistryRuleExecutor
    {
        internal bool SerializedContextHadTransformer { get; private set; }
        internal bool DeserializedContextHadTransformer { get; private set; }
        internal Action? BeforeDeserialize { get; set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            SerializedContextHadTransformer = context.TaggedFieldTransformer is not null;
            return payload;
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            BeforeDeserialize?.Invoke();
            DeserializedContextHadTransformer = context.TaggedFieldTransformer is not null;
            return payload;
        }
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

    private sealed class CapturingPayloadCapacityRuleExecutor : ISchemaRegistryRuleExecutor
    {
        internal int LastPayloadBufferCapacity { get; private set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            LastPayloadBufferCapacity = MemoryMarshal.TryGetArray(payload, out var segment)
                ? segment.Array!.Length
                : payload.Length;
            return payload;
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;
    }

    private sealed class ReentrantRuleExecutor : ISchemaRegistryRuleExecutor
    {
        private bool _isReentrant;

        internal Action? Reenter { get; set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context)
        {
            if (_isReentrant)
                return payload;

            _isReentrant = true;
            try
            {
                Reenter!();
            }
            finally
            {
                _isReentrant = false;
            }

            return payload;
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> payload,
            SchemaRegistryRuleContext context) => payload;
    }

    private sealed class FixedPayloadMigrationHandler(ReadOnlyMemory<byte> payload) : ISchemaRegistryRuleHandler
    {
        internal const string RuleType = "FIXED_AVRO_PAYLOAD";

        public string Type => RuleType;
        internal bool ContextHadTaggedFieldTransformer { get; private set; }

        public ReadOnlyMemory<byte> TransformSerializedPayload(
            ReadOnlyMemory<byte> source,
            SchemaRegistryRuleHandlerContext context)
        {
            ContextHadTaggedFieldTransformer |= context.PayloadContext.TaggedFieldTransformer is not null;
            return payload;
        }

        public ReadOnlyMemory<byte> TransformDeserializedPayload(
            ReadOnlyMemory<byte> source,
            SchemaRegistryRuleHandlerContext context)
        {
            ContextHadTaggedFieldTransformer |= context.PayloadContext.TaggedFieldTransformer is not null;
            return payload;
        }
    }

    private sealed class ExactSizeBufferWriter(int capacity) : IBufferWriter<byte>
    {
        private readonly byte[] _buffer = GC.AllocateUninitializedArray<byte>(capacity);

        public int WrittenCount { get; private set; }
        public int GetMemoryCallCount { get; private set; }
        public int LastMemorySizeHint { get; private set; }

        public void Advance(int count)
        {
            if ((uint)count > (uint)(_buffer.Length - WrittenCount))
                throw new ArgumentOutOfRangeException(nameof(count));
            WrittenCount += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            GetMemoryCallCount++;
            LastMemorySizeHint = sizeHint;
            return _buffer.AsMemory(WrittenCount, GetLength(sizeHint));
        }

        public Span<byte> GetSpan(int sizeHint = 0) =>
            _buffer.AsSpan(WrittenCount, GetLength(sizeHint));

        internal void Clear()
        {
            WrittenCount = 0;
            GetMemoryCallCount = 0;
            LastMemorySizeHint = 0;
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

[AvroRecord(Name = "PocoReferencedRoot", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoReferencedRoot
{
    public required PocoAddress Address { get; init; }
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

[AvroRecord(Name = "PocoZeroWidthCollection", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoZeroWidthCollection
{
    [AvroField(Name = "items", Order = 0)]
    public required PocoZeroWidthItem[] Items { get; init; }
}

[AvroRecord(Name = "PocoZeroWidthItem", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoZeroWidthItem
{
    [AvroField(DefaultJson = "\"reader-default\"")]
    public required string Value { get; init; }
}

[AvroRecord(Name = "PocoLargeZeroWidthCollection", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoLargeZeroWidthCollection
{
    [AvroField(Name = "items")]
    public required PocoLargeZeroWidthItem[] Items { get; init; }
}

[AvroRecord(Name = "PocoLargeZeroWidthReferenceCollection", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoLargeZeroWidthReferenceCollection
{
    [AvroField(Name = "items")]
    public required PocoLargeZeroWidthReferenceItem[] Items { get; init; }
}

[AvroRecord(Name = "PocoLargeZeroWidthReferenceItem", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoLargeZeroWidthReferenceItem
{
    [AvroField(DefaultJson = "0", Order = 0)]
    public long Field0 { get; init; }

    [AvroField(DefaultJson = "0", Order = 1)]
    public long Field1 { get; init; }

    [AvroField(DefaultJson = "0", Order = 2)]
    public long Field2 { get; init; }

    [AvroField(DefaultJson = "0", Order = 3)]
    public long Field3 { get; init; }

    [AvroField(DefaultJson = "0", Order = 4)]
    public long Field4 { get; init; }

    [AvroField(DefaultJson = "0", Order = 5)]
    public long Field5 { get; init; }

    [AvroField(DefaultJson = "0", Order = 6)]
    public long Field6 { get; init; }

    [AvroField(DefaultJson = "0", Order = 7)]
    public long Field7 { get; init; }
}

[AvroRecord(Name = "PocoNestedZeroWidthReferenceCollection", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoNestedZeroWidthReferenceCollection
{
    [AvroField(Name = "items")]
    public required PocoNestedZeroWidthReferenceItem[] Items { get; init; }
}

[AvroRecord(Name = "PocoNestedZeroWidthReferenceItem", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoNestedZeroWidthReferenceItem
{
    [AvroField(Name = "child")]
    public required PocoNestedZeroWidthReferenceChild Child { get; init; }
}

[AvroRecord(Name = "PocoNestedZeroWidthReferenceChild", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoNestedZeroWidthReferenceChild
{
    [AvroField(DefaultJson = "0", Order = 0)]
    public long Field0 { get; init; }

    [AvroField(DefaultJson = "0", Order = 1)]
    public long Field1 { get; init; }

    [AvroField(DefaultJson = "0", Order = 2)]
    public long Field2 { get; init; }

    [AvroField(DefaultJson = "0", Order = 3)]
    public long Field3 { get; init; }

    [AvroField(DefaultJson = "0", Order = 4)]
    public long Field4 { get; init; }

    [AvroField(DefaultJson = "0", Order = 5)]
    public long Field5 { get; init; }

    [AvroField(DefaultJson = "0", Order = 6)]
    public long Field6 { get; init; }

    [AvroField(DefaultJson = "0", Order = 7)]
    public long Field7 { get; init; }
}

[AvroRecord(Name = "PocoByteArrayDefaultCollection", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoByteArrayDefaultCollection
{
    [AvroField(Name = "values")]
    public required PocoByteArrayDefaultItem[] Values { get; init; }
}

[AvroRecord(Name = "PocoByteArrayDefaultItem", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoByteArrayDefaultItem
{
    [AvroField(DefaultJson = "\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz\"")]
    public byte[] Data { get; init; } = null!;
}

[AvroRecord(Name = "PocoAllocatingConstructorCollection", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoAllocatingConstructorCollection
{
    [AvroField(Name = "values")]
    public required PocoAllocatingConstructorItem[] Values { get; init; }
}

[AvroRecord(Name = "PocoAllocatingConstructorItem", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoAllocatingConstructorItem
{
    public PocoAllocatingConstructorItem() => Hidden = new byte[4096];

    [AvroField(DefaultJson = "0")]
    public long Value { get; init; }

    [AvroIgnore]
    public byte[] Hidden { get; }
}

[AvroRecord(Name = "PocoWriteOverflowRecord", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoWriteOverflowRecord
{
    private int _laterMemberReadCount;
    private int _later;

    [AvroField(Order = 0)]
    public required string First { get; init; }

    [AvroField(Order = 1)]
    public int Later
    {
        get
        {
            _laterMemberReadCount++;
            return _later;
        }
        init => _later = value;
    }

    [AvroIgnore]
    public int LaterMemberReadCount => _laterMemberReadCount;
}

[AvroRecord(Name = "PocoFixedWriteOverflowRecord", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoFixedWriteOverflowRecord
{
    private int _laterMemberReadCount;
    private string _later = string.Empty;

    [AvroField(Order = 0)]
    public int First { get; init; }

    [AvroField(Order = 1)]
    public string Later
    {
        get
        {
            _laterMemberReadCount++;
            return _later;
        }
        init => _later = value;
    }

    [AvroIgnore]
    public int LaterMemberReadCount => _laterMemberReadCount;
}

[AvroRecord(Name = "PocoCollectionOverflowRecord", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoCollectionOverflowRecord
{
    public required PocoCollectionOverflowItem[] Items { get; init; }
}

[AvroRecord(Name = "PocoCollectionOverflowItem", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoCollectionOverflowItem
{
    private int _valueReadCount;
    private string _value = string.Empty;

    public string Value
    {
        get
        {
            _valueReadCount++;
            return _value;
        }
        init => _value = value;
    }

    [AvroIgnore]
    public int ValueReadCount => _valueReadCount;
}

[AvroRecord(Name = "PocoLargeZeroWidthMap", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoLargeZeroWidthMap
{
    [AvroField(Name = "values")]
    public required Dictionary<string, PocoLargeZeroWidthItem> Values { get; init; }
}

[AvroRecord(Name = "PocoLargeZeroWidthItem", Namespace = "Dekaf.Tests")]
internal partial record struct PocoLargeZeroWidthItem
{
    [AvroField(DefaultJson = "0", Order = 0)]
    public long Field0 { get; init; }

    [AvroField(DefaultJson = "0", Order = 1)]
    public long Field1 { get; init; }

    [AvroField(DefaultJson = "0", Order = 2)]
    public long Field2 { get; init; }

    [AvroField(DefaultJson = "0", Order = 3)]
    public long Field3 { get; init; }

    [AvroField(DefaultJson = "0", Order = 4)]
    public long Field4 { get; init; }

    [AvroField(DefaultJson = "0", Order = 5)]
    public long Field5 { get; init; }

    [AvroField(DefaultJson = "0", Order = 6)]
    public long Field6 { get; init; }

    [AvroField(DefaultJson = "0", Order = 7)]
    public long Field7 { get; init; }
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

[AvroRecord(Name = "PocoNumericPromotions", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoNumericPromotions
{
    public long LongValue { get; init; }
    public float FloatValue { get; init; }
    public double DoubleValue { get; init; }
}

[AvroRecord(Name = "PocoGrowingPayload", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoGrowingPayload
{
    public required string Value { get; init; }
}

internal sealed class CountingPocoPayload
{
    public required string Value { get; init; }
}

internal readonly struct CountingPocoPayloadCodec : IAvroPocoCodec<CountingPocoPayload>
{
    private static readonly AvroPocoField[] s_fields =
    [
        new(
            "Value",
            ReadOnlyMemory<string>.Empty,
            defaultJson: null,
            new AvroPocoType(AvroPocoTypeKind.String))
    ];

    [ThreadStatic]
    private static int t_writeCount;

    public static string SchemaJson =>
        """
        {"type":"record","name":"CountingPocoPayload","namespace":"Dekaf.Tests","fields":[{"name":"Value","type":"string"}]}
        """;

    public static ReadOnlySpan<byte> SchemaUtf8 =>
        """{"type":"record","name":"CountingPocoPayload","namespace":"Dekaf.Tests","fields":[{"name":"Value","type":"string"}]}"""u8;

    public static long ParsingFingerprint64 => 0;

    public static string FullName => "Dekaf.Tests.CountingPocoPayload";

    public static ReadOnlyMemory<AvroPocoField> Fields => s_fields;

    internal static int WriteCount => t_writeCount;

    internal static void ResetWriteCount() => t_writeCount = 0;

    public static void Write(ref AvroValueWriter writer, CountingPocoPayload value)
    {
        t_writeCount++;
        writer.WriteString(value.Value);
    }

    public static CountingPocoPayload Read(ref AvroValueReader reader, AvroPocoReaderPlan plan) =>
        throw new NotSupportedException();
}

[AvroRecord(Name = "PocoLogicalBaseUnion", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoLogicalBaseUnion
{
    public long? Value { get; init; }
}

internal sealed class PocoLogicalUnionValue
{
    public required object Value { get; init; }
}

internal readonly struct PocoLogicalUnionCodec : IAvroPocoCodec<PocoLogicalUnionValue>
{
    private const string ReaderSchemaJson =
        """
        {"type":"record","name":"PocoLogicalUnion","namespace":"Dekaf.Tests","fields":[{"name":"Value","type":[{"type":"long","logicalType":"timestamp-micros"},"string"]}]}
        """;

    private static readonly AvroPocoType[] s_branches =
    [
        new(AvroPocoTypeKind.TimestampMicroseconds),
        new(AvroPocoTypeKind.String)
    ];

    private static readonly AvroPocoField[] s_fields =
    [
        new(
            "Value",
            ReadOnlyMemory<string>.Empty,
            defaultJson: null,
            new AvroPocoType(AvroPocoTypeKind.Union, branches: s_branches))
    ];

    public static string SchemaJson => ReaderSchemaJson;

    public static ReadOnlySpan<byte> SchemaUtf8 =>
        """{"type":"record","name":"PocoLogicalUnion","namespace":"Dekaf.Tests","fields":[{"name":"Value","type":[{"type":"long","logicalType":"timestamp-micros"},"string"]}]}"""u8;

    public static long ParsingFingerprint64 => 0;

    public static string FullName => "Dekaf.Tests.PocoLogicalUnion";

    public static ReadOnlyMemory<AvroPocoField> Fields => s_fields;

    public static void Write(ref AvroValueWriter writer, PocoLogicalUnionValue value) =>
        throw new NotSupportedException();

    public static PocoLogicalUnionValue Read(ref AvroValueReader reader, AvroPocoReaderPlan plan)
    {
        var branches = plan.GetOperation(0).WriterType.Branches.Span;
        var branch = branches[reader.ReadIndex(branches.Length)];
        object value = branch.ReaderUnionBranchIndex switch
        {
            0 => DateTime.UnixEpoch.AddTicks(checked(reader.ReadInt64() * 10L)),
            1 => reader.ReadString(),
            _ => throw new InvalidDataException("Writer union branch has no generated POCO target.")
        };
        return new PocoLogicalUnionValue { Value = value };
    }
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

[AvroRecord(Name = "PocoDateTimeOffsetTemporal", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoDateTimeOffsetTemporal
{
    public DateTimeOffset Timestamp { get; init; }
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

[AvroRecord(Name = "PocoNullableStructRecord", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoNullableStructRecord
{
    public PocoReadonlyRecord? Value { get; init; }
}

[AvroRecord(Name = "PocoTimestampEvolution", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoTimestampEvolution
{
    public DateTime Value { get; init; }
}

[AvroRecord(Name = "PocoFixedEvolution", Namespace = "Dekaf.Tests")]
internal sealed partial class PocoFixedEvolution
{
    [AvroField(Order = 0, Precision = 9, Scale = 2)]
    public decimal Amount { get; init; }

    [AvroField(Order = 1)]
    public int Tail { get; init; }
}
