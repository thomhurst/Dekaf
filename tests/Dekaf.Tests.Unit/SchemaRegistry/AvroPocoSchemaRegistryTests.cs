using System.Buffers;
using System.Numerics;
using System.Text;
using Avro;
using Avro.Generic;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.Serialization;

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
        var parsed = Schema.Parse(PocoOrder.AvroCodec.SchemaJson);
        var schemaUtf8 = PocoOrder.AvroCodec.SchemaUtf8.ToArray();

        await Assert.That(parsed.Fullname).IsEqualTo("Dekaf.Tests.PocoOrder");
        await Assert.That(PocoOrder.AvroCodec.SchemaJson).IsEqualTo(PocoOrder.AvroCodec.SchemaJson);
        await Assert.That(schemaUtf8).IsEquivalentTo(Encoding.UTF8.GetBytes(PocoOrder.AvroCodec.SchemaJson));
        await Assert.That(PocoOrder.AvroCodec.ParsingFingerprint64)
            .IsEqualTo(SchemaNormalization.ParsingFingerprint64(parsed));
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
