using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using global::Avro.Generic;
using global::Avro.Util;

namespace Dekaf.SchemaRegistry.Avro;

internal sealed class AllocationFreeGenericRecordWriter(global::Avro.RecordSchema schema)
{
    private const int StackBufferSize = 256;
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LocalUnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly ConditionalWeakTable<Type, ListElement> ListElements = new();
    private readonly global::Avro.RecordSchema _schema = schema;
    private readonly WriterCache _writerCache = WriterCache.Create(schema);
    private ListElement? _lastListElement;

    internal void Write(GenericRecord record, AllocationFreeBinaryEncoder encoder) =>
        WriteRecord(_schema, record, encoder, validateSchema: false);

    private void WriteValue(global::Avro.Schema schema, object? value, AllocationFreeBinaryEncoder encoder)
    {
        switch (schema.Tag)
        {
            case global::Avro.Schema.Type.Null:
                if (value is not null)
                    throw TypeMismatch(value, "null");
                encoder.WriteNull();
                return;

            case global::Avro.Schema.Type.Boolean:
                if (value is not bool boolean)
                    throw TypeMismatch(value, "boolean");
                encoder.WriteBoolean(boolean);
                return;

            case global::Avro.Schema.Type.Int:
                if (value is not int integer)
                    throw TypeMismatch(value, "int");
                encoder.WriteInt(integer);
                return;

            case global::Avro.Schema.Type.Long:
                if (value is not long longInteger)
                    throw TypeMismatch(value, "long");
                encoder.WriteLong(longInteger);
                return;

            case global::Avro.Schema.Type.Float:
                if (value is not float single)
                    throw TypeMismatch(value, "float");
                encoder.WriteFloat(single);
                return;

            case global::Avro.Schema.Type.Double:
                if (value is not double doubleValue)
                    throw TypeMismatch(value, "double");
                encoder.WriteDouble(doubleValue);
                return;

            case global::Avro.Schema.Type.String:
                if (value is not string text)
                    throw TypeMismatch(value, "string");
                encoder.WriteString(text);
                return;

            case global::Avro.Schema.Type.Bytes:
                if (value is not byte[] bytes)
                    throw TypeMismatch(value, "bytes");
                encoder.WriteBytes(bytes);
                return;

            case global::Avro.Schema.Type.Record:
            case global::Avro.Schema.Type.Error:
                WriteRecord((global::Avro.RecordSchema)schema, value, encoder);
                return;

            case global::Avro.Schema.Type.Enumeration:
                WriteEnum((global::Avro.EnumSchema)schema, value, encoder);
                return;

            case global::Avro.Schema.Type.Fixed:
                WriteFixed((global::Avro.FixedSchema)schema, value, encoder);
                return;

            case global::Avro.Schema.Type.Array:
                WriteArray((global::Avro.ArraySchema)schema, value, encoder);
                return;

            case global::Avro.Schema.Type.Map:
                WriteMap((global::Avro.MapSchema)schema, value, encoder);
                return;

            case global::Avro.Schema.Type.Union:
                WriteUnion((global::Avro.UnionSchema)schema, value, encoder);
                return;

            case global::Avro.Schema.Type.Logical:
                WriteLogical((global::Avro.LogicalSchema)schema, value, encoder);
                return;

            default:
                throw new global::Avro.AvroTypeException($"Unsupported Avro schema type {schema.Tag}.");
        }
    }

    private void WriteRecord(
        global::Avro.RecordSchema schema,
        object? value,
        AllocationFreeBinaryEncoder encoder,
        bool validateSchema = true)
    {
        if (value is not GenericRecord record ||
            (validateSchema && !ReferenceEquals(record.Schema, schema) && !record.Schema.Equals(schema)))
            throw TypeMismatch(value, "record");

        var fields = schema.Fields;
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            WriteValue(field.Schema, record.GetValue(field.Pos), encoder);
        }
    }

    private static void WriteEnum(global::Avro.EnumSchema schema, object? value, AllocationFreeBinaryEncoder encoder)
    {
        if (value is not GenericEnum genericEnum ||
            (!ReferenceEquals(genericEnum.Schema, schema) && !genericEnum.Schema.Equals(schema)))
            throw TypeMismatch(value, "enum");

        encoder.WriteEnum(schema.Ordinal(genericEnum.Value));
    }

    private static void WriteFixed(global::Avro.FixedSchema schema, object? value, AllocationFreeBinaryEncoder encoder)
    {
        if (value is not GenericFixed fixedValue ||
            (!ReferenceEquals(fixedValue.Schema, schema) && !fixedValue.Schema.Equals(schema)))
            throw TypeMismatch(value, "fixed");

        encoder.WriteFixed(fixedValue.Value);
    }

    private void WriteArray(global::Avro.ArraySchema schema, object? value, AllocationFreeBinaryEncoder encoder)
    {
        encoder.WriteArrayStart();
        switch (value)
        {
            case Array array:
                encoder.SetItemCount(array.Length);
                WriteTypedArray(schema.ItemSchema, array, encoder);
                break;
            case IList list:
                encoder.SetItemCount(list.Count);
                WriteTypedList(schema.ItemSchema, list, encoder);
                break;
            default:
                throw TypeMismatch(value, "array");
        }

        encoder.WriteArrayEnd();
    }

    private void WriteTypedArray(global::Avro.Schema itemSchema, Array array, AllocationFreeBinaryEncoder encoder)
    {
        if (itemSchema is global::Avro.UnionSchema unionSchema &&
            TryWriteValueTypeUnionArray(unionSchema, array, encoder))
        {
            return;
        }

        switch (itemSchema.Tag)
        {
            case global::Avro.Schema.Type.Boolean when array is bool[] booleans:
                for (var i = 0; i < booleans.Length; i++)
                {
                    encoder.StartItem();
                    encoder.WriteBoolean(booleans[i]);
                }
                return;

            case global::Avro.Schema.Type.Int when array is int[] integers:
                for (var i = 0; i < integers.Length; i++)
                {
                    encoder.StartItem();
                    encoder.WriteInt(integers[i]);
                }
                return;

            case global::Avro.Schema.Type.Long when array is long[] longIntegers:
                for (var i = 0; i < longIntegers.Length; i++)
                {
                    encoder.StartItem();
                    encoder.WriteLong(longIntegers[i]);
                }
                return;

            case global::Avro.Schema.Type.Float when array is float[] singles:
                for (var i = 0; i < singles.Length; i++)
                {
                    encoder.StartItem();
                    encoder.WriteFloat(singles[i]);
                }
                return;

            case global::Avro.Schema.Type.Double when array is double[] doubles:
                for (var i = 0; i < doubles.Length; i++)
                {
                    encoder.StartItem();
                    encoder.WriteDouble(doubles[i]);
                }
                return;

            case global::Avro.Schema.Type.String when array is string[] strings:
                for (var i = 0; i < strings.Length; i++)
                {
                    encoder.StartItem();
                    encoder.WriteString(strings[i]);
                }
                return;

            case global::Avro.Schema.Type.Bytes when array is byte[][] byteArrays:
                for (var i = 0; i < byteArrays.Length; i++)
                {
                    encoder.StartItem();
                    encoder.WriteBytes(byteArrays[i]);
                }
                return;

            case global::Avro.Schema.Type.Logical when array is DateTime[] dateTimes:
                for (var i = 0; i < dateTimes.Length; i++)
                {
                    encoder.StartItem();
                    WriteDateTime((global::Avro.LogicalSchema)itemSchema, dateTimes[i], encoder);
                }
                return;

            case global::Avro.Schema.Type.Logical when array is TimeSpan[] timeSpans:
                for (var i = 0; i < timeSpans.Length; i++)
                {
                    encoder.StartItem();
                    WriteTimeSpan((global::Avro.LogicalSchema)itemSchema, timeSpans[i], encoder);
                }
                return;

            case global::Avro.Schema.Type.Logical when array is Guid[] guids:
                for (var i = 0; i < guids.Length; i++)
                {
                    encoder.StartItem();
                    WriteUuid((global::Avro.LogicalSchema)itemSchema, guids[i], encoder);
                }
                return;

            case global::Avro.Schema.Type.Logical when array is global::Avro.AvroDecimal[] decimals:
                for (var i = 0; i < decimals.Length; i++)
                {
                    encoder.StartItem();
                    WriteDecimal((global::Avro.LogicalSchema)itemSchema, decimals[i], encoder);
                }
                return;
        }

        if (array is object?[] objects)
        {
            for (var i = 0; i < objects.Length; i++)
            {
                encoder.StartItem();
                WriteValue(itemSchema, objects[i], encoder);
            }
            return;
        }

        if (array.GetType().GetElementType()?.IsValueType == true)
        {
            throw new global::Avro.AvroTypeException(
                $"Array element type {array.GetType().GetElementType()} is not supported for Avro {itemSchema.Tag} items.");
        }

        for (var i = 0; i < array.Length; i++)
        {
            encoder.StartItem();
            WriteValue(itemSchema, array.GetValue(i), encoder);
        }
    }

    private void WriteTypedList(global::Avro.Schema itemSchema, IList list, AllocationFreeBinaryEncoder encoder)
    {
        if (itemSchema is global::Avro.UnionSchema unionSchema &&
            TryWriteValueTypeUnionList(unionSchema, list, encoder))
        {
            return;
        }

        switch (itemSchema.Tag)
        {
            case global::Avro.Schema.Type.Boolean when list is List<bool> values:
                WriteListItems<bool, BooleanValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Int when list is List<int> values:
                WriteListItems<int, IntValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Long when list is List<long> values:
                WriteListItems<long, LongValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Float when list is List<float> values:
                WriteListItems<float, FloatValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Double when list is List<double> values:
                WriteListItems<double, DoubleValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.String when list is List<string> values:
                WriteListItems<string, StringValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Bytes when list is List<byte[]> values:
                WriteListItems<byte[], BytesValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Logical when list is List<DateTime> values:
                WriteListItems<DateTime, DateTimeValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Logical when list is List<TimeSpan> values:
                WriteListItems<TimeSpan, TimeSpanValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Logical when list is List<Guid> values:
                WriteListItems<Guid, GuidValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Logical when list is List<global::Avro.AvroDecimal> values:
                WriteListItems<global::Avro.AvroDecimal, DecimalValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Boolean when list is IList<bool> values:
                WriteListItems<bool, BooleanValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Int when list is IList<int> values:
                WriteListItems<int, IntValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Long when list is IList<long> values:
                WriteListItems<long, LongValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Float when list is IList<float> values:
                WriteListItems<float, FloatValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Double when list is IList<double> values:
                WriteListItems<double, DoubleValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.String when list is IList<string> values:
                WriteListItems<string, StringValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Bytes when list is IList<byte[]> values:
                WriteListItems<byte[], BytesValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Logical when list is IList<DateTime> values:
                WriteListItems<DateTime, DateTimeValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Logical when list is IList<TimeSpan> values:
                WriteListItems<TimeSpan, TimeSpanValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Logical when list is IList<Guid> values:
                WriteListItems<Guid, GuidValueWriter>(itemSchema, values, encoder);
                return;
            case global::Avro.Schema.Type.Logical when list is IList<global::Avro.AvroDecimal> values:
                WriteListItems<global::Avro.AvroDecimal, DecimalValueWriter>(itemSchema, values, encoder);
                return;
        }

        var listType = list.GetType();
        var cache = Volatile.Read(ref _lastListElement);
        if (cache is null || !ReferenceEquals(cache.ListType, listType))
        {
            cache = ListElements.GetValue(
                listType,
                static type => new ListElement(type, FindListElement(type)));
            Volatile.Write(ref _lastListElement, cache);
        }

        if (cache.ElementType is { IsValueType: true } elementType)
        {
            throw new global::Avro.AvroTypeException(
                $"List element type {elementType} is not supported for Avro {itemSchema.Tag} items.");
        }

        for (var i = 0; i < list.Count; i++)
        {
            encoder.StartItem();
            WriteValue(itemSchema, list[i], encoder);
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "A live IList instance retains the implemented interface needed for this rejection check.")]
    private static Type? FindListElement(Type listType)
    {
        var interfaces = listType.GetInterfaces();
        for (var index = 0; index < interfaces.Length; index++)
        {
            var candidate = interfaces[index];
            if (!candidate.IsGenericType
                || candidate.GetGenericTypeDefinition() != typeof(IList<>))
            {
                continue;
            }

            return candidate.GetGenericArguments()[0];
        }

        return null;
    }

    private static void WriteListItems<T, TWriter>(
        global::Avro.Schema itemSchema,
        List<T> values,
        AllocationFreeBinaryEncoder encoder)
        where TWriter : struct, IValueWriter<T>
    {
        for (var i = 0; i < values.Count; i++)
        {
            encoder.StartItem();
            TWriter.Write(itemSchema, values[i], encoder);
        }
    }

    private static void WriteListItems<T, TWriter>(
        global::Avro.Schema itemSchema,
        IList<T> values,
        AllocationFreeBinaryEncoder encoder)
        where TWriter : struct, IValueWriter<T>
    {
        for (var i = 0; i < values.Count; i++)
        {
            encoder.StartItem();
            TWriter.Write(itemSchema, values[i], encoder);
        }
    }

    private bool TryWriteValueTypeUnionArray(
        global::Avro.UnionSchema schema,
        Array array,
        AllocationFreeBinaryEncoder encoder)
    {
        switch (array)
        {
            case bool[] values: WriteUnionArray<bool, BooleanValueWriter>(schema, values, encoder); return true;
            case bool?[] values: WriteNullableUnionArray<bool, BooleanValueWriter>(schema, values, encoder); return true;
            case int[] values: WriteUnionArray<int, IntValueWriter>(schema, values, encoder); return true;
            case int?[] values: WriteNullableUnionArray<int, IntValueWriter>(schema, values, encoder); return true;
            case long[] values: WriteUnionArray<long, LongValueWriter>(schema, values, encoder); return true;
            case long?[] values: WriteNullableUnionArray<long, LongValueWriter>(schema, values, encoder); return true;
            case float[] values: WriteUnionArray<float, FloatValueWriter>(schema, values, encoder); return true;
            case float?[] values: WriteNullableUnionArray<float, FloatValueWriter>(schema, values, encoder); return true;
            case double[] values: WriteUnionArray<double, DoubleValueWriter>(schema, values, encoder); return true;
            case double?[] values: WriteNullableUnionArray<double, DoubleValueWriter>(schema, values, encoder); return true;
            case DateTime[] values: WriteUnionArray<DateTime, DateTimeValueWriter>(schema, values, encoder); return true;
            case DateTime?[] values: WriteNullableUnionArray<DateTime, DateTimeValueWriter>(schema, values, encoder); return true;
            case TimeSpan[] values: WriteUnionArray<TimeSpan, TimeSpanValueWriter>(schema, values, encoder); return true;
            case TimeSpan?[] values: WriteNullableUnionArray<TimeSpan, TimeSpanValueWriter>(schema, values, encoder); return true;
            case Guid[] values: WriteUnionArray<Guid, GuidValueWriter>(schema, values, encoder); return true;
            case Guid?[] values: WriteNullableUnionArray<Guid, GuidValueWriter>(schema, values, encoder); return true;
            case global::Avro.AvroDecimal[] values: WriteUnionArray<global::Avro.AvroDecimal, DecimalValueWriter>(schema, values, encoder); return true;
            case global::Avro.AvroDecimal?[] values: WriteNullableUnionArray<global::Avro.AvroDecimal, DecimalValueWriter>(schema, values, encoder); return true;
            default: return false;
        }
    }

    private bool TryWriteValueTypeUnionList(
        global::Avro.UnionSchema schema,
        IList list,
        AllocationFreeBinaryEncoder encoder)
    {
        switch (list)
        {
            case List<bool> values: WriteUnionList<bool, BooleanValueWriter>(schema, values, encoder); return true;
            case List<bool?> values: WriteNullableUnionList<bool, BooleanValueWriter>(schema, values, encoder); return true;
            case List<int> values: WriteUnionList<int, IntValueWriter>(schema, values, encoder); return true;
            case List<int?> values: WriteNullableUnionList<int, IntValueWriter>(schema, values, encoder); return true;
            case List<long> values: WriteUnionList<long, LongValueWriter>(schema, values, encoder); return true;
            case List<long?> values: WriteNullableUnionList<long, LongValueWriter>(schema, values, encoder); return true;
            case List<float> values: WriteUnionList<float, FloatValueWriter>(schema, values, encoder); return true;
            case List<float?> values: WriteNullableUnionList<float, FloatValueWriter>(schema, values, encoder); return true;
            case List<double> values: WriteUnionList<double, DoubleValueWriter>(schema, values, encoder); return true;
            case List<double?> values: WriteNullableUnionList<double, DoubleValueWriter>(schema, values, encoder); return true;
            case List<DateTime> values: WriteUnionList<DateTime, DateTimeValueWriter>(schema, values, encoder); return true;
            case List<DateTime?> values: WriteNullableUnionList<DateTime, DateTimeValueWriter>(schema, values, encoder); return true;
            case List<TimeSpan> values: WriteUnionList<TimeSpan, TimeSpanValueWriter>(schema, values, encoder); return true;
            case List<TimeSpan?> values: WriteNullableUnionList<TimeSpan, TimeSpanValueWriter>(schema, values, encoder); return true;
            case List<Guid> values: WriteUnionList<Guid, GuidValueWriter>(schema, values, encoder); return true;
            case List<Guid?> values: WriteNullableUnionList<Guid, GuidValueWriter>(schema, values, encoder); return true;
            case List<global::Avro.AvroDecimal> values: WriteUnionList<global::Avro.AvroDecimal, DecimalValueWriter>(schema, values, encoder); return true;
            case List<global::Avro.AvroDecimal?> values: WriteNullableUnionList<global::Avro.AvroDecimal, DecimalValueWriter>(schema, values, encoder); return true;
            case IList<bool> values: WriteUnionList<bool, BooleanValueWriter>(schema, values, encoder); return true;
            case IList<bool?> values: WriteNullableUnionList<bool, BooleanValueWriter>(schema, values, encoder); return true;
            case IList<int> values: WriteUnionList<int, IntValueWriter>(schema, values, encoder); return true;
            case IList<int?> values: WriteNullableUnionList<int, IntValueWriter>(schema, values, encoder); return true;
            case IList<long> values: WriteUnionList<long, LongValueWriter>(schema, values, encoder); return true;
            case IList<long?> values: WriteNullableUnionList<long, LongValueWriter>(schema, values, encoder); return true;
            case IList<float> values: WriteUnionList<float, FloatValueWriter>(schema, values, encoder); return true;
            case IList<float?> values: WriteNullableUnionList<float, FloatValueWriter>(schema, values, encoder); return true;
            case IList<double> values: WriteUnionList<double, DoubleValueWriter>(schema, values, encoder); return true;
            case IList<double?> values: WriteNullableUnionList<double, DoubleValueWriter>(schema, values, encoder); return true;
            case IList<DateTime> values: WriteUnionList<DateTime, DateTimeValueWriter>(schema, values, encoder); return true;
            case IList<DateTime?> values: WriteNullableUnionList<DateTime, DateTimeValueWriter>(schema, values, encoder); return true;
            case IList<TimeSpan> values: WriteUnionList<TimeSpan, TimeSpanValueWriter>(schema, values, encoder); return true;
            case IList<TimeSpan?> values: WriteNullableUnionList<TimeSpan, TimeSpanValueWriter>(schema, values, encoder); return true;
            case IList<Guid> values: WriteUnionList<Guid, GuidValueWriter>(schema, values, encoder); return true;
            case IList<Guid?> values: WriteNullableUnionList<Guid, GuidValueWriter>(schema, values, encoder); return true;
            case IList<global::Avro.AvroDecimal> values: WriteUnionList<global::Avro.AvroDecimal, DecimalValueWriter>(schema, values, encoder); return true;
            case IList<global::Avro.AvroDecimal?> values: WriteNullableUnionList<global::Avro.AvroDecimal, DecimalValueWriter>(schema, values, encoder); return true;
            default: return false;
        }
    }

    private void WriteUnionArray<T, TWriter>(
        global::Avro.UnionSchema schema,
        T[] values,
        AllocationFreeBinaryEncoder encoder)
        where T : struct
        where TWriter : struct, IValueWriter<T>
    {
        if (values.Length == 0)
            return;

        var branch = _writerCache.GetUnion(schema).GetValueBranch(typeof(T));
        for (var i = 0; i < values.Length; i++)
        {
            encoder.StartItem();
            encoder.WriteUnionIndex(branch.Index);
            TWriter.Write(branch.Schema, values[i], encoder);
        }
    }

    private void WriteNullableUnionArray<T, TWriter>(
        global::Avro.UnionSchema schema,
        T?[] values,
        AllocationFreeBinaryEncoder encoder)
        where T : struct
        where TWriter : struct, IValueWriter<T>
    {
        var union = _writerCache.GetUnion(schema);
        if (!union.TryGetValueBranch(typeof(T), out var branch))
        {
            WriteNullableUnionArrayWithoutValueBranch(union, values, encoder);
            return;
        }

        for (var i = 0; i < values.Length; i++)
        {
            encoder.StartItem();
            var value = values[i];
            if (!value.HasValue)
            {
                if (union.NullIndex < 0)
                    throw TypeMismatch(null, "union");
                encoder.WriteUnionIndex(union.NullIndex);
                continue;
            }
            encoder.WriteUnionIndex(branch.Index);
            TWriter.Write(branch.Schema, value.GetValueOrDefault(), encoder);
        }
    }

    private void WriteUnionList<T, TWriter>(
        global::Avro.UnionSchema schema,
        List<T> values,
        AllocationFreeBinaryEncoder encoder)
        where T : struct
        where TWriter : struct, IValueWriter<T>
    {
        if (values.Count == 0)
            return;

        var branch = _writerCache.GetUnion(schema).GetValueBranch(typeof(T));
        for (var i = 0; i < values.Count; i++)
        {
            encoder.StartItem();
            encoder.WriteUnionIndex(branch.Index);
            TWriter.Write(branch.Schema, values[i], encoder);
        }
    }

    private void WriteNullableUnionList<T, TWriter>(
        global::Avro.UnionSchema schema,
        List<T?> values,
        AllocationFreeBinaryEncoder encoder)
        where T : struct
        where TWriter : struct, IValueWriter<T>
    {
        var union = _writerCache.GetUnion(schema);
        if (!union.TryGetValueBranch(typeof(T), out var branch))
        {
            WriteNullableUnionListWithoutValueBranch(union, values, encoder);
            return;
        }

        for (var i = 0; i < values.Count; i++)
        {
            encoder.StartItem();
            var value = values[i];
            if (!value.HasValue)
            {
                if (union.NullIndex < 0)
                    throw TypeMismatch(null, "union");
                encoder.WriteUnionIndex(union.NullIndex);
                continue;
            }
            encoder.WriteUnionIndex(branch.Index);
            TWriter.Write(branch.Schema, value.GetValueOrDefault(), encoder);
        }
    }

    private void WriteUnionList<T, TWriter>(
        global::Avro.UnionSchema schema,
        IList<T> values,
        AllocationFreeBinaryEncoder encoder)
        where T : struct
        where TWriter : struct, IValueWriter<T>
    {
        if (values.Count == 0)
            return;

        var branch = _writerCache.GetUnion(schema).GetValueBranch(typeof(T));
        for (var i = 0; i < values.Count; i++)
        {
            encoder.StartItem();
            encoder.WriteUnionIndex(branch.Index);
            TWriter.Write(branch.Schema, values[i], encoder);
        }
    }

    private void WriteNullableUnionList<T, TWriter>(
        global::Avro.UnionSchema schema,
        IList<T?> values,
        AllocationFreeBinaryEncoder encoder)
        where T : struct
        where TWriter : struct, IValueWriter<T>
    {
        var union = _writerCache.GetUnion(schema);
        if (!union.TryGetValueBranch(typeof(T), out var branch))
        {
            WriteNullableUnionListWithoutValueBranch(union, values, encoder);
            return;
        }

        for (var i = 0; i < values.Count; i++)
        {
            encoder.StartItem();
            var value = values[i];
            if (!value.HasValue)
            {
                if (union.NullIndex < 0)
                    throw TypeMismatch(null, "union");
                encoder.WriteUnionIndex(union.NullIndex);
                continue;
            }
            encoder.WriteUnionIndex(branch.Index);
            TWriter.Write(branch.Schema, value.GetValueOrDefault(), encoder);
        }
    }

    private static void WriteNullableUnionArrayWithoutValueBranch<T>(
        UnionBranchCache union,
        T?[] values,
        AllocationFreeBinaryEncoder encoder)
        where T : struct
    {
        for (var i = 0; i < values.Length; i++)
        {
            encoder.StartItem();
            if (values[i].HasValue)
                _ = union.GetValueBranch(typeof(T));
            if (union.NullIndex < 0)
                throw TypeMismatch(null, "union");
            encoder.WriteUnionIndex(union.NullIndex);
        }
    }

    private static void WriteNullableUnionListWithoutValueBranch<T>(
        UnionBranchCache union,
        IList<T?> values,
        AllocationFreeBinaryEncoder encoder)
        where T : struct
    {
        for (var i = 0; i < values.Count; i++)
        {
            encoder.StartItem();
            if (values[i].HasValue)
                _ = union.GetValueBranch(typeof(T));
            if (union.NullIndex < 0)
                throw TypeMismatch(null, "union");
            encoder.WriteUnionIndex(union.NullIndex);
        }
    }

    private interface IValueWriter<T>
    {
        static abstract void Write(global::Avro.Schema schema, T value, AllocationFreeBinaryEncoder encoder);
    }

    private readonly struct BooleanValueWriter : IValueWriter<bool>
    {
        public static void Write(global::Avro.Schema schema, bool value, AllocationFreeBinaryEncoder encoder) => encoder.WriteBoolean(value);
    }

    private readonly struct IntValueWriter : IValueWriter<int>
    {
        public static void Write(global::Avro.Schema schema, int value, AllocationFreeBinaryEncoder encoder) => encoder.WriteInt(value);
    }

    private readonly struct LongValueWriter : IValueWriter<long>
    {
        public static void Write(global::Avro.Schema schema, long value, AllocationFreeBinaryEncoder encoder) => encoder.WriteLong(value);
    }

    private readonly struct FloatValueWriter : IValueWriter<float>
    {
        public static void Write(global::Avro.Schema schema, float value, AllocationFreeBinaryEncoder encoder) => encoder.WriteFloat(value);
    }

    private readonly struct DoubleValueWriter : IValueWriter<double>
    {
        public static void Write(global::Avro.Schema schema, double value, AllocationFreeBinaryEncoder encoder) => encoder.WriteDouble(value);
    }

    private readonly struct StringValueWriter : IValueWriter<string>
    {
        public static void Write(global::Avro.Schema schema, string value, AllocationFreeBinaryEncoder encoder) => encoder.WriteString(value);
    }

    private readonly struct BytesValueWriter : IValueWriter<byte[]>
    {
        public static void Write(global::Avro.Schema schema, byte[] value, AllocationFreeBinaryEncoder encoder) => encoder.WriteBytes(value);
    }

    private readonly struct DateTimeValueWriter : IValueWriter<DateTime>
    {
        public static void Write(global::Avro.Schema schema, DateTime value, AllocationFreeBinaryEncoder encoder) =>
            WriteDateTime((global::Avro.LogicalSchema)schema, value, encoder);
    }

    private readonly struct TimeSpanValueWriter : IValueWriter<TimeSpan>
    {
        public static void Write(global::Avro.Schema schema, TimeSpan value, AllocationFreeBinaryEncoder encoder) =>
            WriteTimeSpan((global::Avro.LogicalSchema)schema, value, encoder);
    }

    private readonly struct GuidValueWriter : IValueWriter<Guid>
    {
        public static void Write(global::Avro.Schema schema, Guid value, AllocationFreeBinaryEncoder encoder) =>
            WriteUuid((global::Avro.LogicalSchema)schema, value, encoder);
    }

    private readonly struct DecimalValueWriter : IValueWriter<global::Avro.AvroDecimal>
    {
        public static void Write(global::Avro.Schema schema, global::Avro.AvroDecimal value, AllocationFreeBinaryEncoder encoder) =>
            WriteDecimal((global::Avro.LogicalSchema)schema, value, encoder);
    }

    private void WriteMap(global::Avro.MapSchema schema, object? value, AllocationFreeBinaryEncoder encoder)
    {
        if (value is not Dictionary<string, object> map)
        {
            throw new global::Avro.AvroTypeException(
                $"Avro map values must use {typeof(Dictionary<string, object>)} to preserve zero-allocation serialization; received {value?.GetType()}.");
        }

        encoder.WriteMapStart();
        encoder.SetItemCount(map.Count);
        foreach (var entry in map)
        {
            encoder.StartItem();
            encoder.WriteString(entry.Key);
            WriteValue(schema.ValueSchema, entry.Value, encoder);
        }

        encoder.WriteMapEnd();
    }

    private void WriteUnion(global::Avro.UnionSchema schema, object? value, AllocationFreeBinaryEncoder encoder)
    {
        var union = _writerCache.GetUnion(schema);
        var branch = union.HasConditionalBranches
            ? union.ResolveConditional(value)
            : union.Resolve(value);
        encoder.WriteUnionIndex(branch.Index);
        WriteValue(branch.Schema, value, encoder);
    }

    private void WriteLogical(
        global::Avro.LogicalSchema schema,
        object? value,
        AllocationFreeBinaryEncoder encoder)
    {
        switch (schema.LogicalType)
        {
            case Date or TimestampMillisecond or LocalTimestampMillisecond or
                TimestampMicrosecond or LocalTimestampMicrosecond:
                if (value is not DateTime date)
                    throw TypeMismatch(value, schema.LogicalTypeName);
                WriteDateTime(schema, date, encoder);
                return;

            case TimeMillisecond or TimeMicrosecond:
                if (value is not TimeSpan time)
                    throw TypeMismatch(value, schema.LogicalTypeName);
                WriteTimeSpan(schema, time, encoder);
                return;

            case Uuid:
                if (value is not Guid guid)
                    throw TypeMismatch(value, "uuid");
                WriteUuid(schema, guid, encoder);
                return;

            case global::Avro.Util.Decimal:
                if (value is not global::Avro.AvroDecimal decimalValue)
                    throw TypeMismatch(value, "decimal");
                WriteDecimal(schema, decimalValue, encoder);
                return;

            default:
                WriteValue(
                    schema.BaseSchema,
                    schema.LogicalType.ConvertToBaseValue(value, schema),
                    encoder);
                return;
        }
    }

    private static void WriteDateTime(
        global::Avro.LogicalSchema schema,
        DateTime value,
        AllocationFreeBinaryEncoder encoder)
    {
        switch (schema.LogicalType)
        {
            case Date:
                encoder.WriteInt((value.Date - UnixEpoch).Days);
                return;
            case TimestampMillisecond:
                encoder.WriteLong((long)(value.ToUniversalTime() - UnixEpoch).TotalMilliseconds);
                return;
            case TimestampMicrosecond:
                encoder.WriteLong((value.ToUniversalTime() - UnixEpoch).Ticks / 10);
                return;
            case LocalTimestampMillisecond:
                encoder.WriteLong((long)(AsLocalWallClock(value) - LocalUnixEpoch).TotalMilliseconds);
                return;
            case LocalTimestampMicrosecond:
                encoder.WriteLong((AsLocalWallClock(value) - LocalUnixEpoch).Ticks / 10);
                return;
            default:
                throw TypeMismatch(value, schema.LogicalTypeName);
        }
    }

    private static void WriteTimeSpan(
        global::Avro.LogicalSchema schema,
        TimeSpan value,
        AllocationFreeBinaryEncoder encoder)
    {
        ValidateTime(value);
        switch (schema.LogicalType)
        {
            case TimeMillisecond:
                encoder.WriteInt((int)value.TotalMilliseconds);
                return;
            case TimeMicrosecond:
                encoder.WriteLong(value.Ticks / 10);
                return;
            default:
                throw TypeMismatch(value, schema.LogicalTypeName);
        }
    }

    private static void WriteUuid(
        global::Avro.LogicalSchema schema,
        Guid value,
        AllocationFreeBinaryEncoder encoder)
    {
        if (schema.LogicalType is not Uuid)
            throw TypeMismatch(value, schema.LogicalTypeName);

        Span<char> uuid = stackalloc char[36];
        if (!value.TryFormat(uuid, out var charsWritten, "D"))
            throw new InvalidOperationException("Unable to format an Avro UUID.");
        encoder.WriteString(uuid.Slice(0, charsWritten));
    }

    private static void WriteDecimal(
        global::Avro.LogicalSchema schema,
        global::Avro.AvroDecimal value,
        AllocationFreeBinaryEncoder encoder)
    {
        var scaleProperty = schema.GetProperty("scale");
        var schemaScale = string.IsNullOrEmpty(scaleProperty)
            ? 0
            : int.Parse(scaleProperty, CultureInfo.InvariantCulture);
        if (value.Scale != schemaScale)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"The decimal value has a scale of {value.Scale} which cannot be encoded against " +
                $"a logical 'decimal' with a scale of {schemaScale}");
        }

        var byteCount = value.UnscaledValue.GetByteCount(isUnsigned: false);
        var fixedSize = schema.BaseSchema is global::Avro.FixedSchema fixedSchema ? fixedSchema.Size : 0;
        var bufferSize = Math.Max(byteCount, fixedSize);
        byte[]? rented = null;
        Span<byte> bytes = bufferSize <= StackBufferSize
            ? stackalloc byte[StackBufferSize]
            : (rented = ArrayPool<byte>.Shared.Rent(bufferSize));

        try
        {
            var raw = bytes.Slice(0, byteCount);
            if (!value.UnscaledValue.TryWriteBytes(raw, out var written, isUnsigned: false, isBigEndian: true))
                throw new InvalidOperationException("Unable to encode an Avro decimal.");

            if (fixedSize == 0)
            {
                encoder.WriteBytes(raw.Slice(0, written));
                return;
            }

            if (written > fixedSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"The decimal value requires {written} bytes and cannot fit in a fixed size of {fixedSize} bytes.");
            }

            var fixedBytes = bytes.Slice(0, fixedSize);
            var source = raw.Slice(0, written);
            var paddingLength = fixedSize - source.Length;
            source.CopyTo(fixedBytes.Slice(paddingLength));
            fixedBytes.Slice(0, paddingLength).Fill(value.UnscaledValue.Sign < 0 ? byte.MaxValue : (byte)0);
            encoder.WriteFixed(fixedBytes);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static DateTime AsLocalWallClock(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    private static void ValidateTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero || value >= TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(value), "Avro time values must be within one day.");
    }

    private sealed class WriterCache
    {
        private readonly Dictionary<global::Avro.UnionSchema, UnionBranchCache> _unions =
            new(ReferenceComparer<global::Avro.UnionSchema>.Instance);
        private UnionBranchCache? _lastUnion;

        public static WriterCache Create(global::Avro.Schema schema)
        {
            var cache = new WriterCache();
            var visited = new HashSet<global::Avro.Schema>(ReferenceComparer<global::Avro.Schema>.Instance);
            cache.AddSchema(schema, visited);
            return cache;
        }

        public UnionBranchCache GetUnion(global::Avro.UnionSchema schema)
        {
            var last = Volatile.Read(ref _lastUnion);
            if (last is not null && ReferenceEquals(last.Schema, schema))
                return last;

            var union = _unions[schema];
            Volatile.Write(ref _lastUnion, union);
            return union;
        }

        private void AddSchema(
            global::Avro.Schema schema,
            HashSet<global::Avro.Schema> visited)
        {
            if (!visited.Add(schema))
                return;

            switch (schema.Tag)
            {
                case global::Avro.Schema.Type.Record:
                case global::Avro.Schema.Type.Error:
                    var fields = ((global::Avro.RecordSchema)schema).Fields;
                    for (var i = 0; i < fields.Count; i++)
                        AddSchema(fields[i].Schema, visited);
                    break;

                case global::Avro.Schema.Type.Array:
                    AddSchema(((global::Avro.ArraySchema)schema).ItemSchema, visited);
                    break;

                case global::Avro.Schema.Type.Map:
                    AddSchema(((global::Avro.MapSchema)schema).ValueSchema, visited);
                    break;

                case global::Avro.Schema.Type.Union:
                    var union = (global::Avro.UnionSchema)schema;
                    _unions.Add(union, new UnionBranchCache(union));
                    for (var i = 0; i < union.Count; i++)
                        AddSchema(union[i], visited);
                    break;

                case global::Avro.Schema.Type.Logical:
                    var logical = (global::Avro.LogicalSchema)schema;
                    AddSchema(logical.BaseSchema, visited);
                    break;
            }
        }
    }

    private sealed class UnionBranchCache
    {
        private readonly global::Avro.UnionSchema _schema;
        private readonly Dictionary<Type, UnionBranch> _typedBranches = new();
        private readonly Dictionary<global::Avro.SchemaName, UnionBranch> _recordBranches = new();
        private readonly Dictionary<global::Avro.SchemaName, UnionBranch> _enumBranches = new();
        private readonly Dictionary<global::Avro.SchemaName, UnionBranch> _fixedBranches = new();
        private readonly Dictionary<Type, ConditionalUnionBranch>? _conditionalBranches;
        private readonly Dictionary<Type, MultipleConditionalUnionBranches>? _multipleConditionalBranches;
        private readonly UnionBranch _arrayBranch;
        private readonly UnionBranch _booleanBranch;
        private readonly UnionBranch _bytesBranch;
        private readonly UnionBranch _dateTimeBranch;
        private readonly UnionBranch _decimalBranch;
        private readonly UnionBranch _doubleBranch;
        private readonly UnionBranch _floatBranch;
        private readonly UnionBranch _guidBranch;
        private readonly UnionBranch _intBranch;
        private readonly UnionBranch _longBranch;
        private readonly UnionBranch _mapBranch;
        private readonly UnionBranch _stringBranch;
        private readonly UnionBranch _timeSpanBranch;

        public UnionBranchCache(global::Avro.UnionSchema schema)
        {
            _schema = schema;
            NullIndex = -1;
            UnionBranch booleanBranch = default;
            UnionBranch bytesBranch = default;
            UnionBranch dateTimeBranch = default;
            UnionBranch decimalBranch = default;
            UnionBranch doubleBranch = default;
            UnionBranch floatBranch = default;
            UnionBranch guidBranch = default;
            UnionBranch intBranch = default;
            UnionBranch longBranch = default;
            UnionBranch stringBranch = default;
            UnionBranch timeSpanBranch = default;
            Dictionary<Type, ConditionalUnionBranchBuilder>? conditionalBranchBuilders = null;
            for (var i = 0; i < schema.Count; i++)
            {
                var branch = schema[i];
                var resolved = new UnionBranch(i, branch);
                switch (branch.Tag)
                {
                    case global::Avro.Schema.Type.Null:
                        NullIndex = i;
                        break;
                    case global::Avro.Schema.Type.Boolean:
                        SetCompatibleBranch(typeof(bool), ref booleanBranch, resolved, null, ref conditionalBranchBuilders);
                        break;
                    case global::Avro.Schema.Type.Int:
                        SetCompatibleBranch(typeof(int), ref intBranch, resolved, null, ref conditionalBranchBuilders);
                        break;
                    case global::Avro.Schema.Type.Long:
                        SetCompatibleBranch(typeof(long), ref longBranch, resolved, null, ref conditionalBranchBuilders);
                        break;
                    case global::Avro.Schema.Type.Float:
                        SetCompatibleBranch(typeof(float), ref floatBranch, resolved, null, ref conditionalBranchBuilders);
                        break;
                    case global::Avro.Schema.Type.Double:
                        SetCompatibleBranch(typeof(double), ref doubleBranch, resolved, null, ref conditionalBranchBuilders);
                        break;
                    case global::Avro.Schema.Type.Bytes:
                        SetCompatibleBranch(typeof(byte[]), ref bytesBranch, resolved, null, ref conditionalBranchBuilders);
                        break;
                    case global::Avro.Schema.Type.String:
                        SetCompatibleBranch(typeof(string), ref stringBranch, resolved, null, ref conditionalBranchBuilders);
                        break;
                    case global::Avro.Schema.Type.Record:
                    case global::Avro.Schema.Type.Error:
                        _recordBranches.TryAdd(((global::Avro.RecordSchema)branch).SchemaName, resolved);
                        break;
                    case global::Avro.Schema.Type.Enumeration:
                        _enumBranches.TryAdd(((global::Avro.EnumSchema)branch).SchemaName, resolved);
                        break;
                    case global::Avro.Schema.Type.Fixed:
                        _fixedBranches.TryAdd(((global::Avro.FixedSchema)branch).SchemaName, resolved);
                        break;
                    case global::Avro.Schema.Type.Array:
                        if (_arrayBranch.Schema is null)
                            _arrayBranch = resolved;
                        break;
                    case global::Avro.Schema.Type.Map:
                        if (_mapBranch.Schema is null)
                            _mapBranch = resolved;
                        break;
                    case global::Avro.Schema.Type.Logical:
                        var logical = (global::Avro.LogicalSchema)branch;
                        var type = logical.LogicalType.GetCSharpType(nullible: false);
                        var conditional = IsSpecializedLogicalType(logical.LogicalType) ? null : logical;
                        if (type == typeof(bool))
                        {
                            SetCompatibleBranch(type, ref booleanBranch, resolved, conditional, ref conditionalBranchBuilders);
                        }
                        else if (type == typeof(int))
                        {
                            SetCompatibleBranch(type, ref intBranch, resolved, conditional, ref conditionalBranchBuilders);
                        }
                        else if (type == typeof(long))
                        {
                            SetCompatibleBranch(type, ref longBranch, resolved, conditional, ref conditionalBranchBuilders);
                        }
                        else if (type == typeof(float))
                        {
                            SetCompatibleBranch(type, ref floatBranch, resolved, conditional, ref conditionalBranchBuilders);
                        }
                        else if (type == typeof(double))
                        {
                            SetCompatibleBranch(type, ref doubleBranch, resolved, conditional, ref conditionalBranchBuilders);
                        }
                        else if (type == typeof(byte[]))
                        {
                            SetCompatibleBranch(type, ref bytesBranch, resolved, conditional, ref conditionalBranchBuilders);
                        }
                        else if (type == typeof(string))
                        {
                            SetCompatibleBranch(type, ref stringBranch, resolved, conditional, ref conditionalBranchBuilders);
                        }
                        else if (type == typeof(DateTime))
                        {
                            SetCompatibleBranch(type, ref dateTimeBranch, resolved, conditional, ref conditionalBranchBuilders);
                        }
                        else if (type == typeof(TimeSpan))
                        {
                            SetCompatibleBranch(type, ref timeSpanBranch, resolved, conditional, ref conditionalBranchBuilders);
                        }
                        else if (type == typeof(Guid))
                        {
                            SetCompatibleBranch(type, ref guidBranch, resolved, conditional, ref conditionalBranchBuilders);
                        }
                        else if (type == typeof(global::Avro.AvroDecimal))
                        {
                            SetCompatibleBranch(type, ref decimalBranch, resolved, conditional, ref conditionalBranchBuilders);
                        }
                        else
                        {
                            _typedBranches.TryGetValue(type, out var typedBranch);
                            SetCompatibleBranch(
                                type,
                                ref typedBranch,
                                resolved,
                                conditional,
                                ref conditionalBranchBuilders);
                            _typedBranches[type] = typedBranch;
                        }
                        break;
                }
            }

            _booleanBranch = booleanBranch;
            _intBranch = intBranch;
            _longBranch = longBranch;
            _floatBranch = floatBranch;
            _doubleBranch = doubleBranch;
            _bytesBranch = bytesBranch;
            _stringBranch = stringBranch;
            _dateTimeBranch = dateTimeBranch;
            _timeSpanBranch = timeSpanBranch;
            _guidBranch = guidBranch;
            _decimalBranch = decimalBranch;
            _conditionalBranches = BuildConditionalBranches(
                conditionalBranchBuilders,
                out _multipleConditionalBranches);
        }

        public global::Avro.UnionSchema Schema => _schema;

        public bool HasConditionalBranches => _conditionalBranches is not null;

        public int NullIndex { get; }

        public bool TryGetValueBranch(Type type, out UnionBranch branch)
        {
            if (HasConditionalValueBranch(type))
            {
                branch = default;
                return false;
            }

            if (type == typeof(bool)) branch = _booleanBranch;
            else if (type == typeof(int)) branch = _intBranch;
            else if (type == typeof(long)) branch = _longBranch;
            else if (type == typeof(float)) branch = _floatBranch;
            else if (type == typeof(double)) branch = _doubleBranch;
            else if (type == typeof(DateTime)) branch = _dateTimeBranch;
            else if (type == typeof(TimeSpan)) branch = _timeSpanBranch;
            else if (type == typeof(Guid)) branch = _guidBranch;
            else if (type == typeof(global::Avro.AvroDecimal)) branch = _decimalBranch;
            else if (!_typedBranches.TryGetValue(type, out branch)) branch = default;

            return branch.Schema is not null;
        }

        public UnionBranch GetValueBranch(Type type)
        {
            ThrowIfConditionalValueBranch(type);

            UnionBranch branch;
            if (type == typeof(bool)) branch = _booleanBranch;
            else if (type == typeof(int)) branch = _intBranch;
            else if (type == typeof(long)) branch = _longBranch;
            else if (type == typeof(float)) branch = _floatBranch;
            else if (type == typeof(double)) branch = _doubleBranch;
            else if (type == typeof(DateTime)) branch = _dateTimeBranch;
            else if (type == typeof(TimeSpan)) branch = _timeSpanBranch;
            else if (type == typeof(Guid)) branch = _guidBranch;
            else if (type == typeof(global::Avro.AvroDecimal)) branch = _decimalBranch;
            else if (!_typedBranches.TryGetValue(type, out branch)) branch = default;

            if (branch.Schema is not null)
                return branch;

            throw new global::Avro.AvroTypeException($"Union {_schema} has no branch for {type}.");
        }

        public UnionBranch Resolve(object? value)
        {
            if (value is null)
            {
                if (NullIndex >= 0)
                    return new UnionBranch(NullIndex, _schema[NullIndex]);
                throw NoMatch(value);
            }

            switch (value)
            {
                case bool when _booleanBranch.Schema is not null: return _booleanBranch;
                case int when _intBranch.Schema is not null: return _intBranch;
                case long when _longBranch.Schema is not null: return _longBranch;
                case float when _floatBranch.Schema is not null: return _floatBranch;
                case double when _doubleBranch.Schema is not null: return _doubleBranch;
                case byte[] when _bytesBranch.Schema is not null: return _bytesBranch;
                case string when _stringBranch.Schema is not null: return _stringBranch;
                case DateTime when _dateTimeBranch.Schema is not null: return _dateTimeBranch;
                case TimeSpan when _timeSpanBranch.Schema is not null: return _timeSpanBranch;
                case Guid when _guidBranch.Schema is not null: return _guidBranch;
                case global::Avro.AvroDecimal when _decimalBranch.Schema is not null: return _decimalBranch;
                case GenericRecord record when _recordBranches.TryGetValue(record.Schema.SchemaName, out var branch):
                    return FirstCompatibleTypedBranch(value.GetType(), branch);
                case GenericEnum genericEnum when _enumBranches.TryGetValue(genericEnum.Schema.SchemaName, out var branch):
                    return FirstCompatibleTypedBranch(value.GetType(), branch);
                case GenericFixed fixedValue when _fixedBranches.TryGetValue(fixedValue.Schema.SchemaName, out var branch):
                    return FirstCompatibleTypedBranch(value.GetType(), branch);
                case (Array or IList) and not byte[] when _arrayBranch.Schema is not null:
                    return FirstCompatibleTypedBranch(value.GetType(), _arrayBranch);
                case IDictionary<string, object> when _mapBranch.Schema is not null:
                    return FirstCompatibleTypedBranch(value.GetType(), _mapBranch);
                default:
                    return _typedBranches.TryGetValue(value.GetType(), out var typedBranch)
                        ? typedBranch
                        : throw NoMatch(value);
            }
        }

        public UnionBranch ResolveConditional(object? value)
        {
            if (value is not null &&
                _conditionalBranches!.TryGetValue(value.GetType(), out var conditional))
            {
                var fallback = GetConditionalFallback(value, conditional.Fallback);
                if (fallback.Schema is not null && fallback.Index < conditional.Primary.Index)
                    return fallback;

                if (conditional.LogicalSchema.LogicalType.IsInstanceOfLogicalType(value))
                    return conditional.Primary;

                if (fallback.Schema is not null)
                    return fallback;
                throw NoMatch(value);
            }

            if (value is not null &&
                _multipleConditionalBranches?.TryGetValue(value.GetType(), out var multiple) == true)
            {
                var candidates = multiple.Candidates;
                var fallback = GetConditionalFallback(value, multiple.Fallback);
                for (var i = 0; i < candidates.Length; i++)
                {
                    var candidate = candidates[i];
                    if (fallback.Schema is not null && fallback.Index < candidate.Branch.Index)
                        return fallback;
                    if (candidate.LogicalSchema.LogicalType.IsInstanceOfLogicalType(value))
                        return candidate.Branch;
                }
                if (fallback.Schema is not null)
                    return fallback;
                throw NoMatch(value);
            }

            return Resolve(value);
        }

        private UnionBranch FirstCompatibleTypedBranch(Type type, UnionBranch schemaBranch) =>
            _typedBranches.TryGetValue(type, out var typedBranch) && typedBranch.Index < schemaBranch.Index
                ? typedBranch
                : schemaBranch;

        private UnionBranch GetConditionalFallback(object value, UnionBranch fallback)
        {
            var structural = ResolveStructuralBranch(value);
            return structural.Schema is not null &&
                   (fallback.Schema is null || structural.Index < fallback.Index)
                ? structural
                : fallback;
        }

        private UnionBranch ResolveStructuralBranch(object value) => value switch
        {
            GenericRecord record when _recordBranches.TryGetValue(record.Schema.SchemaName, out var branch) => branch,
            GenericEnum genericEnum when _enumBranches.TryGetValue(genericEnum.Schema.SchemaName, out var branch) => branch,
            GenericFixed fixedValue when _fixedBranches.TryGetValue(fixedValue.Schema.SchemaName, out var branch) => branch,
            (Array or IList) and not byte[] => _arrayBranch,
            IDictionary<string, object> => _mapBranch,
            _ => default
        };

        private static void SetCompatibleBranch(
            Type type,
            ref UnionBranch target,
            UnionBranch branch,
            global::Avro.LogicalSchema? conditional,
            ref Dictionary<Type, ConditionalUnionBranchBuilder>? conditionalBranchBuilders)
        {
            if (target.Schema is null)
            {
                target = branch;
                if (conditional is not null)
                {
                    conditionalBranchBuilders ??= new Dictionary<Type, ConditionalUnionBranchBuilder>();
                    var builder = new ConditionalUnionBranchBuilder();
                    builder.Candidates.Add(new ConditionalUnionCandidate(branch, conditional));
                    conditionalBranchBuilders.Add(type, builder);
                }
                return;
            }

            if (conditionalBranchBuilders is null ||
                !conditionalBranchBuilders.TryGetValue(type, out var existing) ||
                existing.Fallback.Schema is not null)
                return;

            if (conditional is not null)
            {
                existing.Candidates.Add(new ConditionalUnionCandidate(branch, conditional));
                return;
            }

            existing.Fallback = branch;
        }

        private static Dictionary<Type, ConditionalUnionBranch>? BuildConditionalBranches(
            Dictionary<Type, ConditionalUnionBranchBuilder>? builders,
            out Dictionary<Type, MultipleConditionalUnionBranches>? multipleBranches)
        {
            multipleBranches = null;
            if (builders is null)
                return null;

            var branches = new Dictionary<Type, ConditionalUnionBranch>(builders.Count);
            foreach (var (type, builder) in builders)
            {
                if (builder.Candidates.Count == 1)
                {
                    var candidate = builder.Candidates[0];
                    branches.Add(
                        type,
                        new ConditionalUnionBranch(candidate.Branch, candidate.LogicalSchema, builder.Fallback));
                    continue;
                }

                multipleBranches ??= new Dictionary<Type, MultipleConditionalUnionBranches>();
                multipleBranches.Add(
                    type,
                    new MultipleConditionalUnionBranches(builder.Candidates.ToArray(), builder.Fallback));
            }

            return branches;
        }

        private void ThrowIfConditionalValueBranch(Type type)
        {
            if (HasConditionalValueBranch(type))
            {
                throw new global::Avro.AvroTypeException(
                    $"Union {_schema} uses a value-dependent custom logical branch for {type}; " +
                    "allocation-free value-type collection serialization cannot evaluate it without boxing.");
            }
        }

        private bool HasConditionalValueBranch(Type type) =>
            _conditionalBranches?.ContainsKey(type) == true ||
            _multipleConditionalBranches?.ContainsKey(type) == true;

        private static bool IsSpecializedLogicalType(global::Avro.Util.LogicalType logicalType) =>
            logicalType is Date or TimestampMillisecond or LocalTimestampMillisecond or
                TimestampMicrosecond or LocalTimestampMicrosecond or
                TimeMillisecond or TimeMicrosecond or Uuid or global::Avro.Util.Decimal;

        private global::Avro.AvroException NoMatch(object? value) =>
            new($"Cannot find a union match for {value?.GetType()} in {_schema}.");
    }

    private readonly record struct UnionBranch(int Index, global::Avro.Schema Schema);

    private readonly record struct ConditionalUnionCandidate(
        UnionBranch Branch,
        global::Avro.LogicalSchema LogicalSchema);

    private readonly record struct ConditionalUnionBranch(
        UnionBranch Primary,
        global::Avro.LogicalSchema LogicalSchema,
        UnionBranch Fallback);

    private readonly record struct MultipleConditionalUnionBranches(
        ConditionalUnionCandidate[] Candidates,
        UnionBranch Fallback);

    private sealed class ConditionalUnionBranchBuilder
    {
        public List<ConditionalUnionCandidate> Candidates { get; } = [];
        public UnionBranch Fallback { get; set; }
    }

    private sealed class ListElement(Type listType, Type? elementType)
    {
        internal Type ListType { get; } = listType;
        internal Type? ElementType { get; } = elementType;
    }

    private sealed class ReferenceComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceComparer<T> Instance = new();

        public bool Equals(T? left, T? right) => ReferenceEquals(left, right);

        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }

    private static global::Avro.AvroException TypeMismatch(object? value, string schemaType) =>
        new(
            $"Value of type {value?.GetType().ToString() ?? "null"} cannot be written as Avro {schemaType}.");
}
