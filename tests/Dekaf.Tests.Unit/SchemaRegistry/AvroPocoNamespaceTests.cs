using System.Buffers;
using Avro;
using Dekaf.SchemaRegistry.Avro.Poco;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.SchemaRegistry
{
    public sealed class AvroPocoNamespaceTests
    {
        [Test]
        public async Task ApacheAvro_ExplicitEmptyNamespaceInheritsEnclosingNamespace()
        {
            const string schemaJson =
                """
                {"type":"record","name":"Parent","namespace":"com.acme","fields":[{"name":"Item","type":{"type":"record","name":"Child","namespace":"","fields":[{"name":"Id","type":"int"}]}},{"name":"State","type":{"type":"enum","name":"Status","namespace":"","symbols":["Ready"]}}]}
                """;

            var schema = (RecordSchema)Schema.Parse(schemaJson);

            await Assert.That(schema.Fields[0].Schema.Fullname).IsEqualTo("com.acme.Child");
            await Assert.That(schema.Fields[1].Schema.Fullname).IsEqualTo("com.acme.Status");
        }

        [Test]
        public async Task GeneratedCodec_EmptyNamespaceRoundTripsAndMatchesParsedNames()
        {
            var schema = (RecordSchema)Schema.Parse(PocoEmptyNamespaceRoot.AvroCodec.SchemaJson);
            var fields = PocoEmptyNamespaceRoot.AvroCodec.Fields;

            await Assert.That(schema.Fullname).IsEqualTo(PocoEmptyNamespaceRoot.AvroCodec.FullName);
            await Assert.That(schema.Fields[0].Schema.Fullname).IsEqualTo(PocoGlobalNamespaceItem.AvroCodec.FullName);
            await Assert.That(schema.Fields[0].Schema.Fullname).IsEqualTo(fields.Span[0].Type.FullName);
            await Assert.That(schema.Fields[1].Schema.Fullname).IsEqualTo(PocoNamespacedItem.AvroCodec.FullName);
            await Assert.That(((ArraySchema)schema.Fields[2].Schema).ItemSchema.Fullname)
                .IsEqualTo(fields.Span[2].Type.Item!.FullName);
            await Assert.That(schema.Fields[3].Schema.Fullname).IsEqualTo(nameof(PocoGlobalNamespaceStatus));
            await Assert.That(schema.Fields[3].Schema.Fullname).IsEqualTo(fields.Span[3].Type.FullName);
            await Assert.That(schema.Fields[4].Schema.Fullname).IsEqualTo(fields.Span[4].Type.FullName);
            await Assert.That(PocoEmptyNamespaceRoot.AvroCodec.ParsingFingerprint64)
                .IsEqualTo(SchemaNormalization.ParsingFingerprint64(schema));

            using var registry = new MockSchemaRegistryClient();
            await using var serializer = PocoEmptyNamespaceRoot.CreateAvroSerializer(registry);
            await using var deserializer = PocoEmptyNamespaceRoot.CreateAvroDeserializer(registry);
            var context = new SerializationContext
            {
                Topic = "poco-empty-namespace",
                Component = SerializationComponent.Value
            };
            var expected = new PocoEmptyNamespaceRoot
            {
                Item = new PocoGlobalNamespaceItem { Id = 42 },
                Namespaced = new PocoNamespacedItem { Id = 17 },
                Items = [new PocoGlobalNamespaceItem { Id = 84 }],
                State = PocoGlobalNamespaceStatus.Ready,
                OtherState = PocoGlobalNamespaceStatus.Pending
            };
            await serializer.WarmupAsync(context.Topic);
            var destination = new ArrayBufferWriter<byte>();
            serializer.Serialize(expected, ref destination, context);
            var actual = deserializer.Deserialize(destination.WrittenMemory, context);

            await Assert.That(actual.Item.Id).IsEqualTo(expected.Item.Id);
            await Assert.That(actual.Namespaced.Id).IsEqualTo(expected.Namespaced.Id);
            await Assert.That(actual.Items.Length).IsEqualTo(1);
            await Assert.That(actual.Items[0].Id).IsEqualTo(expected.Items[0].Id);
            await Assert.That(actual.State).IsEqualTo(expected.State);
            await Assert.That(actual.OtherState).IsEqualTo(expected.OtherState);
        }
    }

    [AvroRecord(Namespace = "")]
    internal sealed partial class PocoEmptyNamespaceRoot
    {
        public PocoGlobalNamespaceItem Item { get; set; } = new();
        public PocoNamespacedItem Namespaced { get; set; } = new();
        public PocoGlobalNamespaceItem[] Items { get; set; } = [];
        public PocoGlobalNamespaceStatus State { get; set; }
        public PocoGlobalNamespaceStatus OtherState { get; set; }
    }

    [AvroRecord(Namespace = "com.acme")]
    internal sealed partial class PocoNamespacedItem
    {
        public int Id { get; set; }
    }
}

[AvroRecord]
internal sealed partial class PocoGlobalNamespaceItem
{
    public int Id { get; set; }
}

internal enum PocoGlobalNamespaceStatus
{
    Pending,
    Ready
}
