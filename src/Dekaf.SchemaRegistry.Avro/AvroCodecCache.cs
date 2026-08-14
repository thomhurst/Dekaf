using System.Runtime.CompilerServices;
using Avro;
using Avro.IO;
using Newtonsoft.Json.Linq;
using AvroSchema = Avro.Schema;

namespace Dekaf.SchemaRegistry.Avro;

internal readonly record struct AvroSchemaPair(AvroSchema WriterSchema, AvroSchema ReaderSchema);

internal sealed class AvroSchemaReferenceComparer : IEqualityComparer<AvroSchema>
{
    internal static readonly AvroSchemaReferenceComparer Instance = new();

    private AvroSchemaReferenceComparer() { }

    public bool Equals(AvroSchema? x, AvroSchema? y) => ReferenceEquals(x, y);

    public int GetHashCode(AvroSchema obj) => RuntimeHelpers.GetHashCode(obj);
}

internal sealed class AvroSchemaLogicalComparer : IEqualityComparer<AvroSchema>
{
    internal static readonly AvroSchemaLogicalComparer Instance = new();

    private static readonly JTokenEqualityComparer JTokenComparer = new();

    [ThreadStatic]
    private static AvroSchemaComparisonState? _state;

    private AvroSchemaLogicalComparer() { }

    public bool Equals(AvroSchema? x, AvroSchema? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null || x.Tag != y.Tag)
            return false;

        var state = _state ??= new AvroSchemaComparisonState();
        state.EqualityPairs.Clear();
        try
        {
            return EqualsCore(x, y, state);
        }
        finally
        {
            state.EqualityPairs.Clear();
        }
    }

    public int GetHashCode(AvroSchema schema)
    {
        var state = _state ??= new AvroSchemaComparisonState();
        state.HashedSchemas.Clear();
        try
        {
            return GetHashCodeCore(schema, state);
        }
        finally
        {
            state.HashedSchemas.Clear();
        }
    }

    private static bool EqualsCore(
        AvroSchema x,
        AvroSchema y,
        AvroSchemaComparisonState state)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x.Tag != y.Tag)
            return false;

        var pair = default(AvroSchemaPair);
        var pairAdded = false;
        if (x is NamedSchema xNamed)
        {
            if (y is not NamedSchema yNamed || !NamedSchemaEquals(xNamed, yNamed))
                return false;

            pair = new AvroSchemaPair(x, y);
            if (!state.EqualityPairs.Add(pair))
                return true;
            pairAdded = true;
        }

        try
        {
            if (!PropertiesEqual(
                    AvroSchemaLogicalAccessors.GetProperties(x),
                    AvroSchemaLogicalAccessors.GetProperties(y)))
            {
                return false;
            }

            return x.Tag switch
            {
                AvroSchema.Type.Record or AvroSchema.Type.Error =>
                    RecordSchemaEquals((RecordSchema)x, (RecordSchema)y, state),
                AvroSchema.Type.Enumeration => EnumSchemaEquals((EnumSchema)x, (EnumSchema)y),
                AvroSchema.Type.Array => EqualsCore(
                    ((ArraySchema)x).ItemSchema,
                    ((ArraySchema)y).ItemSchema,
                    state),
                AvroSchema.Type.Map => EqualsCore(
                    ((MapSchema)x).ValueSchema,
                    ((MapSchema)y).ValueSchema,
                    state),
                AvroSchema.Type.Union => UnionSchemaEquals((UnionSchema)x, (UnionSchema)y, state),
                AvroSchema.Type.Fixed => ((FixedSchema)x).Size == ((FixedSchema)y).Size,
                AvroSchema.Type.Logical => LogicalSchemaEquals((LogicalSchema)x, (LogicalSchema)y, state),
                _ => true
            };
        }
        finally
        {
            if (pairAdded)
                state.EqualityPairs.Remove(pair);
        }
    }

    private static bool NamedSchemaEquals(NamedSchema x, NamedSchema y) =>
        string.Equals(x.Fullname, y.Fullname, StringComparison.Ordinal) &&
        OptionalStringEquals(x.Documentation, y.Documentation) &&
        SchemaNameListEquals(
            AvroSchemaLogicalAccessors.GetAliases(x),
            AvroSchemaLogicalAccessors.GetAliases(y));

    private static bool RecordSchemaEquals(
        RecordSchema x,
        RecordSchema y,
        AvroSchemaComparisonState state)
    {
        if (AvroSchemaLogicalAccessors.GetIsRequest(x) !=
                AvroSchemaLogicalAccessors.GetIsRequest(y) ||
            x.Fields.Count != y.Fields.Count)
        {
            return false;
        }

        for (var i = 0; i < x.Fields.Count; i++)
        {
            if (!FieldEquals(x.Fields[i], y.Fields[i], state))
                return false;
        }

        return true;
    }

    private static bool FieldEquals(Field x, Field y, AvroSchemaComparisonState state) =>
        string.Equals(x.Name, y.Name, StringComparison.Ordinal) &&
        OptionalStringEquals(x.Documentation, y.Documentation) &&
        JTokenComparer.Equals(x.DefaultValue, y.DefaultValue) &&
        x.Ordering == y.Ordering &&
        StringListEquals(x.Aliases, y.Aliases) &&
        PropertiesEqual(
            AvroSchemaLogicalAccessors.GetProperties(x),
            AvroSchemaLogicalAccessors.GetProperties(y)) &&
        EqualsCore(x.Schema, y.Schema, state);

    private static bool EnumSchemaEquals(EnumSchema x, EnumSchema y) =>
        StringListEquals(x.Symbols, y.Symbols) &&
        string.Equals(x.Default, y.Default, StringComparison.Ordinal);

    private static bool UnionSchemaEquals(
        UnionSchema x,
        UnionSchema y,
        AvroSchemaComparisonState state)
    {
        if (x.Schemas.Count != y.Schemas.Count)
            return false;

        for (var i = 0; i < x.Schemas.Count; i++)
        {
            if (!EqualsCore(x.Schemas[i], y.Schemas[i], state))
                return false;
        }

        return true;
    }

    private static bool LogicalSchemaEquals(
        LogicalSchema x,
        LogicalSchema y,
        AvroSchemaComparisonState state) =>
        string.Equals(x.LogicalTypeName, y.LogicalTypeName, StringComparison.Ordinal) &&
        EqualsCore(x.BaseSchema, y.BaseSchema, state);

    private static bool PropertiesEqual(PropertyMap? x, PropertyMap? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || x.Count == 0)
            return y is null || y.Count == 0;
        if (y is null || y.Count == 0 || x.Count != y.Count)
            return false;

        foreach (var property in x)
        {
            if (!y.TryGetValue(property.Key, out var value) ||
                !string.Equals(property.Value, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SchemaNameListEquals(IList<SchemaName>? x, IList<SchemaName>? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null || x.Count != y.Count)
            return false;

        for (var i = 0; i < x.Count; i++)
        {
            if (!string.Equals(x[i].Fullname, y[i].Fullname, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool StringListEquals(IList<string>? x, IList<string>? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null || x.Count != y.Count)
            return false;

        for (var i = 0; i < x.Count; i++)
        {
            if (!string.Equals(x[i], y[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static int GetHashCodeCore(AvroSchema schema, AvroSchemaComparisonState state)
    {
        var tracksPath = schema is NamedSchema;
        if (tracksPath && !state.HashedSchemas.Add(schema))
            return Combine((int)schema.Tag, GetStringHashCode(((NamedSchema)schema).Fullname));

        try
        {
            var hash = Combine(
                (int)schema.Tag,
                GetPropertiesHashCode(AvroSchemaLogicalAccessors.GetProperties(schema)));

            if (schema is NamedSchema namedSchema)
            {
                hash = Combine(hash, GetStringHashCode(namedSchema.Fullname));
                hash = Combine(hash, GetOptionalStringHashCode(namedSchema.Documentation));
                hash = Combine(
                    hash,
                    GetSchemaNameListHashCode(AvroSchemaLogicalAccessors.GetAliases(namedSchema)));
            }

            return schema.Tag switch
            {
                AvroSchema.Type.Record or AvroSchema.Type.Error =>
                    GetRecordSchemaHashCode(hash, (RecordSchema)schema, state),
                AvroSchema.Type.Enumeration => GetEnumSchemaHashCode(hash, (EnumSchema)schema),
                AvroSchema.Type.Array => Combine(
                    hash,
                    GetHashCodeCore(((ArraySchema)schema).ItemSchema, state)),
                AvroSchema.Type.Map => Combine(
                    hash,
                    GetHashCodeCore(((MapSchema)schema).ValueSchema, state)),
                AvroSchema.Type.Union => GetUnionSchemaHashCode(hash, (UnionSchema)schema, state),
                AvroSchema.Type.Fixed => Combine(hash, ((FixedSchema)schema).Size),
                AvroSchema.Type.Logical => GetLogicalSchemaHashCode(hash, (LogicalSchema)schema, state),
                _ => hash
            };
        }
        finally
        {
            if (tracksPath)
                state.HashedSchemas.Remove(schema);
        }
    }

    private static int GetRecordSchemaHashCode(
        int hash,
        RecordSchema schema,
        AvroSchemaComparisonState state)
    {
        hash = Combine(hash, AvroSchemaLogicalAccessors.GetIsRequest(schema) ? 1 : 0);
        hash = Combine(hash, schema.Fields.Count);
        for (var i = 0; i < schema.Fields.Count; i++)
            hash = Combine(hash, GetFieldHashCode(schema.Fields[i], state));
        return hash;
    }

    private static int GetFieldHashCode(Field field, AvroSchemaComparisonState state)
    {
        var hash = Combine(GetStringHashCode(field.Name), GetOptionalStringHashCode(field.Documentation));
        hash = Combine(
            hash,
            field.DefaultValue is null ? 0 : JTokenComparer.GetHashCode(field.DefaultValue));
        hash = Combine(hash, (int?)field.Ordering ?? -1);
        hash = Combine(hash, GetStringListHashCode(field.Aliases));
        hash = Combine(
            hash,
            GetPropertiesHashCode(AvroSchemaLogicalAccessors.GetProperties(field)));
        return Combine(hash, GetHashCodeCore(field.Schema, state));
    }

    private static int GetEnumSchemaHashCode(int hash, EnumSchema schema)
    {
        hash = Combine(hash, GetStringListHashCode(schema.Symbols));
        return Combine(hash, GetStringHashCode(schema.Default));
    }

    private static int GetUnionSchemaHashCode(
        int hash,
        UnionSchema schema,
        AvroSchemaComparisonState state)
    {
        hash = Combine(hash, schema.Schemas.Count);
        for (var i = 0; i < schema.Schemas.Count; i++)
            hash = Combine(hash, GetHashCodeCore(schema.Schemas[i], state));
        return hash;
    }

    private static int GetLogicalSchemaHashCode(
        int hash,
        LogicalSchema schema,
        AvroSchemaComparisonState state)
    {
        hash = Combine(hash, GetStringHashCode(schema.LogicalTypeName));
        return Combine(hash, GetHashCodeCore(schema.BaseSchema, state));
    }

    private static int GetPropertiesHashCode(PropertyMap? properties)
    {
        if (properties is null)
            return 0;

        var sum = 0;
        var xor = 0;
        foreach (var property in properties)
        {
            var propertyHash = Combine(
                GetStringHashCode(property.Key),
                GetStringHashCode(property.Value));
            sum = unchecked(sum + propertyHash);
            xor ^= propertyHash;
        }

        return Combine(properties.Count, Combine(sum, xor));
    }

    private static int GetSchemaNameListHashCode(IList<SchemaName>? names)
    {
        if (names is null)
            return 0;

        var hash = names.Count;
        for (var i = 0; i < names.Count; i++)
            hash = Combine(hash, GetStringHashCode(names[i].Fullname));
        return hash;
    }

    private static int GetStringListHashCode(IList<string>? values)
    {
        if (values is null)
            return 0;

        var hash = values.Count;
        for (var i = 0; i < values.Count; i++)
            hash = Combine(hash, GetStringHashCode(values[i]));
        return hash;
    }

    private static int GetStringHashCode(string? value) =>
        value is null ? 0 : StringComparer.Ordinal.GetHashCode(value);

    private static bool OptionalStringEquals(string? x, string? y) =>
        string.IsNullOrEmpty(x)
            ? string.IsNullOrEmpty(y)
            : string.Equals(x, y, StringComparison.Ordinal);

    private static int GetOptionalStringHashCode(string? value) =>
        string.IsNullOrEmpty(value) ? 0 : StringComparer.Ordinal.GetHashCode(value);

    private static int Combine(int hash, int value) =>
        unchecked((hash * 16777619) ^ value);

    private sealed class AvroSchemaComparisonState
    {
        internal HashSet<AvroSchema> HashedSchemas { get; } =
            new(AvroSchemaReferenceComparer.Instance);

        internal HashSet<AvroSchemaPair> EqualityPairs { get; } =
            new(AvroSchemaPairReferenceComparer.Instance);
    }
}

internal static class AvroSchemaLogicalAccessors
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<Props>k__BackingField")]
    internal static extern ref PropertyMap? GetProperties(AvroSchema schema);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "aliases")]
    internal static extern ref IList<SchemaName>? GetAliases(NamedSchema schema);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Props")]
    internal static extern ref PropertyMap? GetProperties(Field field);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "request")]
    internal static extern ref bool GetIsRequest(RecordSchema schema);
}

internal sealed class AvroSchemaPairReferenceComparer : IEqualityComparer<AvroSchemaPair>
{
    internal static readonly AvroSchemaPairReferenceComparer Instance = new();

    private AvroSchemaPairReferenceComparer() { }

    public bool Equals(AvroSchemaPair x, AvroSchemaPair y) =>
        ReferenceEquals(x.WriterSchema, y.WriterSchema) &&
        ReferenceEquals(x.ReaderSchema, y.ReaderSchema);

    public int GetHashCode(AvroSchemaPair obj) =>
        HashCode.Combine(
            RuntimeHelpers.GetHashCode(obj.WriterSchema),
            RuntimeHelpers.GetHashCode(obj.ReaderSchema));
}

internal sealed class AvroSerializationThreadState
{
    internal AvroSerializationThreadState()
    {
        BufferedStream = new PooledMemoryStream([]);
        BufferedEncoder = new BinaryEncoder(BufferedStream);
        DirectStream = new FixedMemoryStream();
        DirectEncoder = new BinaryEncoder(DirectStream);
    }

    internal PooledMemoryStream BufferedStream { get; }
    internal BinaryEncoder BufferedEncoder { get; }
    internal FixedMemoryStream DirectStream { get; }
    internal BinaryEncoder DirectEncoder { get; }
    internal int PayloadSizeHint { get; set; } = 1024;
}

internal sealed class AvroDeserializationThreadState
{
    internal AvroDeserializationThreadState()
    {
        Stream = new PooledMemoryStream([]);
        Decoder = new BinaryDecoder(Stream);
    }

    internal PooledMemoryStream Stream { get; }
    internal BinaryDecoder Decoder { get; }
}

internal static class AvroCodecThreadStateCache
{
    [ThreadStatic]
    internal static AvroSerializationThreadState? Serialization;

    [ThreadStatic]
    internal static AvroDeserializationThreadState? Deserialization;
}
