using Avro;
using Dekaf.SchemaRegistry.Avro;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class AvroSchemaLogicalComparerTests
{
    [Test]
    public async Task SharedAndDuplicatedNamedSchemas_HaveEqualHashes()
    {
        var sharedChild = CreateChildSchema();
        var shared = CreateRootSchema(sharedChild, sharedChild);
        var duplicated = CreateRootSchema(CreateChildSchema(), CreateChildSchema());
        var comparer = AvroSchemaLogicalComparer.Instance;

        await Assert.That(comparer.Equals(shared, duplicated)).IsTrue();
        await Assert.That(comparer.GetHashCode(shared)).IsEqualTo(comparer.GetHashCode(duplicated));

        var schemas = new Dictionary<AvroSchema, int>(comparer)
        {
            [shared] = 1,
            [duplicated] = 2
        };
        await Assert.That(schemas.Count).IsEqualTo(1);
    }

    [Test]
    public async Task LogicalTypeParameters_ArePartOfSchemaIdentity()
    {
        var scaleTwo = AvroSchema.Parse(
            "{\"type\":\"bytes\",\"logicalType\":\"decimal\",\"precision\":8,\"scale\":2}");
        var scaleThree = AvroSchema.Parse(
            "{\"type\":\"bytes\",\"logicalType\":\"decimal\",\"precision\":8,\"scale\":3}");
        var comparer = AvroSchemaLogicalComparer.Instance;

        await Assert.That(comparer.Equals(scaleTwo, scaleThree)).IsFalse();

        var schemas = new Dictionary<AvroSchema, int>(comparer)
        {
            [scaleTwo] = 1,
            [scaleThree] = 2
        };
        await Assert.That(schemas.Count).IsEqualTo(2);
    }

    [Test]
    public async Task FieldOrdering_IsPartOfSchemaIdentity()
    {
        var ascending = AvroSchema.Parse(
            """{"type":"record","name":"Ordered","fields":[{"name":"value","type":"int","order":"ascending"}]}""");
        var descending = AvroSchema.Parse(
            """{"type":"record","name":"Ordered","fields":[{"name":"value","type":"int","order":"descending"}]}""");
        var comparer = AvroSchemaLogicalComparer.Instance;

        await Assert.That(comparer.Equals(ascending, descending)).IsFalse();
        await Assert.That(comparer.GetHashCode(ascending)).IsNotEqualTo(comparer.GetHashCode(descending));

        var schemas = new Dictionary<AvroSchema, int>(comparer)
        {
            [ascending] = 1,
            [descending] = 2
        };
        await Assert.That(schemas.Count).IsEqualTo(2);
    }

    private static RecordSchema CreateChildSchema() =>
        RecordSchema.Create(
            "Child",
            [new Field(AvroSchema.Parse("\"int\""), "value", 0)],
            "Dekaf.Tests");

    private static RecordSchema CreateRootSchema(AvroSchema first, AvroSchema second) =>
        RecordSchema.Create(
            "Root",
            [new Field(first, "first", 0), new Field(second, "second", 1)],
            "Dekaf.Tests");
}
