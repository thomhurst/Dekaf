using System.Buffers;
using System.Globalization;
using global::Avro.Generic;
using global::Avro.Util;

namespace Dekaf.SchemaRegistry.Avro;

internal sealed class AllocationFreeGenericRecordWriter(global::Avro.RecordSchema schema)
{
    private const int StackBufferSize = 256;
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly global::Avro.RecordSchema _schema = schema;

    internal void Write(GenericRecord record, AllocationFreeBinaryEncoder encoder) =>
        WriteRecord(ReferenceEquals(record.Schema, _schema) ? _schema : record.Schema, record, encoder);

    private static void WriteValue(global::Avro.Schema schema, object? value, AllocationFreeBinaryEncoder encoder)
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

    private static void WriteRecord(global::Avro.RecordSchema schema, object? value, AllocationFreeBinaryEncoder encoder)
    {
        if (value is not GenericRecord record ||
            (!ReferenceEquals(record.Schema, schema) && !record.Schema.Equals(schema)))
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

    private static void WriteArray(global::Avro.ArraySchema schema, object? value, AllocationFreeBinaryEncoder encoder)
    {
        if (value is not Array array)
            throw TypeMismatch(value, "array");

        encoder.WriteArrayStart();
        encoder.SetItemCount(array.Length);

        WriteTypedArray(schema.ItemSchema, array, encoder);

        encoder.WriteArrayEnd();
    }

    private static void WriteTypedArray(global::Avro.Schema itemSchema, Array array, AllocationFreeBinaryEncoder encoder)
    {
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

        for (var i = 0; i < array.Length; i++)
        {
            encoder.StartItem();
            WriteValue(itemSchema, array.GetValue(i), encoder);
        }
    }

    private static void WriteMap(global::Avro.MapSchema schema, object? value, AllocationFreeBinaryEncoder encoder)
    {
        if (value is not IDictionary<string, object> map)
            throw TypeMismatch(value, "map");

        encoder.WriteMapStart();
        encoder.SetItemCount(map.Count);

        if (value is Dictionary<string, object> dictionary)
        {
            foreach (var entry in dictionary)
            {
                encoder.StartItem();
                encoder.WriteString(entry.Key);
                WriteValue(schema.ValueSchema, entry.Value, encoder);
            }
        }
        else
        {
            var count = map.Count;
            var entries = ArrayPool<KeyValuePair<string, object>>.Shared.Rent(count);
            try
            {
                map.CopyTo(entries, 0);
                for (var i = 0; i < count; i++)
                {
                    var entry = entries[i];
                    encoder.StartItem();
                    encoder.WriteString(entry.Key);
                    WriteValue(schema.ValueSchema, entry.Value, encoder);
                }
            }
            finally
            {
                entries.AsSpan(0, count).Clear();
                ArrayPool<KeyValuePair<string, object>>.Shared.Return(entries);
            }
        }

        encoder.WriteMapEnd();
    }

    private static void WriteUnion(global::Avro.UnionSchema schema, object? value, AllocationFreeBinaryEncoder encoder)
    {
        for (var i = 0; i < schema.Count; i++)
        {
            var branch = schema[i];
            if (!Matches(branch, value))
                continue;

            encoder.WriteUnionIndex(i);
            WriteValue(branch, value, encoder);
            return;
        }

        throw new global::Avro.AvroException($"Cannot find a union match for {value?.GetType()} in {schema}.");
    }

    private static bool Matches(global::Avro.Schema schema, object? value)
    {
        if (value is null)
            return schema.Tag == global::Avro.Schema.Type.Null;

        return schema.Tag switch
        {
            global::Avro.Schema.Type.Null => false,
            global::Avro.Schema.Type.Boolean => value is bool,
            global::Avro.Schema.Type.Int => value is int,
            global::Avro.Schema.Type.Long => value is long,
            global::Avro.Schema.Type.Float => value is float,
            global::Avro.Schema.Type.Double => value is double,
            global::Avro.Schema.Type.Bytes => value is byte[],
            global::Avro.Schema.Type.String => value is string,
            global::Avro.Schema.Type.Record or global::Avro.Schema.Type.Error =>
                value is GenericRecord record &&
                record.Schema.SchemaName.Equals(((global::Avro.RecordSchema)schema).SchemaName),
            global::Avro.Schema.Type.Enumeration =>
                value is GenericEnum genericEnum &&
                genericEnum.Schema.SchemaName.Equals(((global::Avro.EnumSchema)schema).SchemaName),
            global::Avro.Schema.Type.Array => value is Array and not byte[],
            global::Avro.Schema.Type.Map => value is IDictionary<string, object>,
            global::Avro.Schema.Type.Union => false,
            global::Avro.Schema.Type.Fixed =>
                value is GenericFixed fixedValue &&
                fixedValue.Schema.SchemaName.Equals(((global::Avro.FixedSchema)schema).SchemaName),
            global::Avro.Schema.Type.Logical => ((global::Avro.LogicalSchema)schema).LogicalType.IsInstanceOfLogicalType(value),
            _ => throw new global::Avro.AvroException($"Unknown Avro schema type {schema.Tag}.")
        };
    }

    private static void WriteLogical(
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
            case TimestampMillisecond or LocalTimestampMillisecond:
                encoder.WriteLong((long)(value.ToUniversalTime() - UnixEpoch).TotalMilliseconds);
                return;
            case TimestampMicrosecond or LocalTimestampMicrosecond:
                encoder.WriteLong((value.ToUniversalTime() - UnixEpoch).Ticks / 10);
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
            : int.Parse(scaleProperty, CultureInfo.CurrentCulture);
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

            var fixedBytes = bytes.Slice(0, fixedSize);
            var source = raw.Slice(Math.Max(0, written - fixedSize), Math.Min(written, fixedSize));
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

    private static void ValidateTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero || value >= TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(value), "Avro time values must be within one day.");
    }

    private static global::Avro.AvroException TypeMismatch(object? value, string schemaType) =>
        new(
            $"Value of type {value?.GetType().ToString() ?? "null"} cannot be written as Avro {schemaType}.");
}
