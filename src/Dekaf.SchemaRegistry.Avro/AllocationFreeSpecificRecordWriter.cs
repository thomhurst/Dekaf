using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Dekaf.SchemaRegistry.Avro;

internal sealed unsafe class AllocationFreeSpecificRecordWriter<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>
{
    private readonly FieldPlan[] _fields;

    private AllocationFreeSpecificRecordWriter(FieldPlan[] fields) => _fields = fields;

    internal static AllocationFreeSpecificRecordWriter<T> Create(global::Avro.Schema schema) =>
        Create(schema, typeof(T));

    internal static AllocationFreeSpecificRecordWriter<T> Create(
        global::Avro.Schema schema,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
        Type recordType)
    {
        if (recordType.IsValueType || !typeof(T).IsAssignableFrom(recordType))
        {
            throw new NotSupportedException(
                $"SpecificRecord type {recordType} must be a reference type assignable to {typeof(T)} for allocation-free serialization.");
        }

        if (schema is not global::Avro.RecordSchema recordSchema)
        {
            throw new NotSupportedException(
                $"SpecificRecord type {recordType} must expose an Avro record schema.");
        }

        var fields = new FieldPlan[recordSchema.Fields.Count];
        for (var i = 0; i < fields.Length; i++)
            fields[i] = CreateFieldPlan(recordSchema.Fields[i], recordType);

        return new AllocationFreeSpecificRecordWriter<T>(fields);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Write(T value, AllocationFreeBinaryEncoder encoder)
    {
        var fields = _fields;
        for (var i = 0; i < fields.Length; i++)
        {
            ref readonly var field = ref fields[i];
            switch (field.Kind)
            {
                case FieldKind.Null:
                    encoder.WriteNull();
                    break;
                case FieldKind.Boolean:
                    encoder.WriteBoolean(((delegate* managed<T, bool>)field.Getter)(value));
                    break;
                case FieldKind.Int:
                    encoder.WriteInt(((delegate* managed<T, int>)field.Getter)(value));
                    break;
                case FieldKind.Long:
                    encoder.WriteLong(((delegate* managed<T, long>)field.Getter)(value));
                    break;
                case FieldKind.Float:
                    encoder.WriteFloat(((delegate* managed<T, float>)field.Getter)(value));
                    break;
                case FieldKind.Double:
                    encoder.WriteDouble(((delegate* managed<T, double>)field.Getter)(value));
                    break;
                case FieldKind.String:
                    encoder.WriteString(((delegate* managed<T, string>)field.Getter)(value));
                    break;
                case FieldKind.Bytes:
                    encoder.WriteBytes(((delegate* managed<T, byte[]>)field.Getter)(value));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown SpecificRecord field kind {field.Kind}.");
            }
        }
    }

    private static FieldPlan CreateFieldPlan(
        global::Avro.Field field,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type recordType)
    {
        var kind = GetFieldKind(field, recordType);
        if (kind == FieldKind.Null)
            return new FieldPlan(kind, 0);

        var property = FindProperty(recordType, field.Name);
        if (property?.GetMethod is not { IsStatic: false, IsAbstract: false } getter ||
            property.GetIndexParameters().Length != 0)
        {
            throw UnsupportedField(
                recordType,
                field,
                $"a readable public property named '{field.Name}' was not found");
        }

        var expectedType = GetExpectedType(kind);
        if (property.PropertyType != expectedType)
        {
            throw UnsupportedField(
                recordType,
                field,
                $"property '{property.Name}' has type {property.PropertyType}, expected {expectedType}");
        }

        return new FieldPlan(kind, getter.MethodHandle.GetFunctionPointer());
    }

    private static PropertyInfo? FindProperty(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type recordType,
        string name)
    {
        var property = recordType.GetProperty(name);
        if (property is not null)
            return property;

        var properties = recordType.GetProperties();
        for (var i = 0; i < properties.Length; i++)
        {
            if (string.Equals(properties[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return properties[i];
        }

        return null;
    }

    private static FieldKind GetFieldKind(global::Avro.Field field, Type recordType) => field.Schema.Tag switch
    {
        global::Avro.Schema.Type.Null => FieldKind.Null,
        global::Avro.Schema.Type.Boolean => FieldKind.Boolean,
        global::Avro.Schema.Type.Int => FieldKind.Int,
        global::Avro.Schema.Type.Long => FieldKind.Long,
        global::Avro.Schema.Type.Float => FieldKind.Float,
        global::Avro.Schema.Type.Double => FieldKind.Double,
        global::Avro.Schema.Type.String => FieldKind.String,
        global::Avro.Schema.Type.Bytes => FieldKind.Bytes,
        _ => throw UnsupportedField(recordType, field, $"schema type {field.Schema.Tag} is not supported")
    };

    private static Type GetExpectedType(FieldKind kind) => kind switch
    {
        FieldKind.Boolean => typeof(bool),
        FieldKind.Int => typeof(int),
        FieldKind.Long => typeof(long),
        FieldKind.Float => typeof(float),
        FieldKind.Double => typeof(double),
        FieldKind.String => typeof(string),
        FieldKind.Bytes => typeof(byte[]),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static NotSupportedException UnsupportedField(
        Type recordType,
        global::Avro.Field field,
        string reason) =>
        new(
            $"SpecificRecord field '{field.Name}' on {recordType} cannot use allocation-free serialization: " +
            $"{reason}. Use GenericRecord or a supported scalar SpecificRecord shape.");

    private readonly record struct FieldPlan(FieldKind Kind, nint Getter);

    private enum FieldKind : byte
    {
        Null,
        Boolean,
        Int,
        Long,
        Float,
        Double,
        String,
        Bytes
    }
}
