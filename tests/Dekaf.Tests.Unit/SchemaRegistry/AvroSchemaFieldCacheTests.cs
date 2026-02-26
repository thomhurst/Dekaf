using System.Reflection;
using Dekaf.SchemaRegistry.Avro;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class AvroSchemaFieldCacheTests
{
    /// <summary>
    /// A fake Avro-generated type that has a public static <c>_SCHEMA</c> field.
    /// </summary>
    private sealed class TypeWithSchemaField
    {
        public static readonly string _SCHEMA = "{}";
    }

    /// <summary>
    /// A type without a <c>_SCHEMA</c> field at all.
    /// </summary>
    private sealed class TypeWithoutSchemaField
    {
        public int SomeProperty { get; set; }
    }

    [Test]
    public async Task GetSchemaField_ReturnsSameInstance_OnRepeatedCalls()
    {
        // Act
        var first = AvroSchemaFieldCache.GetSchemaField(typeof(TypeWithSchemaField));
        var second = AvroSchemaFieldCache.GetSchemaField(typeof(TypeWithSchemaField));

        // Assert - should return the exact same FieldInfo instance (cached)
        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNotNull();
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }

    [Test]
    public async Task GetSchemaField_ReturnsFieldInfo_ForTypeWithSchemaField()
    {
        // Act
        var fieldInfo = AvroSchemaFieldCache.GetSchemaField(typeof(TypeWithSchemaField));

        // Assert
        await Assert.That(fieldInfo).IsNotNull();
        await Assert.That(fieldInfo!.Name).IsEqualTo("_SCHEMA");
        await Assert.That(fieldInfo.IsStatic).IsTrue();
        await Assert.That(fieldInfo.IsPublic).IsTrue();
    }

    [Test]
    public async Task GetSchemaField_ReturnsNull_ForTypeWithoutSchemaField()
    {
        // Act
        var fieldInfo = AvroSchemaFieldCache.GetSchemaField(typeof(TypeWithoutSchemaField));

        // Assert
        await Assert.That(fieldInfo).IsNull();
    }

    [Test]
    public async Task GetSchemaField_ReturnsNull_ForBuiltInType()
    {
        // Act - string has no _SCHEMA field
        var fieldInfo = AvroSchemaFieldCache.GetSchemaField(typeof(string));

        // Assert
        await Assert.That(fieldInfo).IsNull();
    }
}
