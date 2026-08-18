using Avro;
using System.Globalization;
using AvroSchema = Avro.Schema;

namespace Dekaf.SchemaRegistry.Avro.Poco;

internal static class AvroPocoReaderPlanBuilder
{
    internal static AvroPocoReaderPlan Build<T, TCodec>(string writerSchemaJson)
        where TCodec : struct, IAvroPocoCodec<T>
    {
        var writerSchema = AvroSchema.Parse(writerSchemaJson) as RecordSchema
            ?? throw new InvalidOperationException("POCO Avro writer schema must be a record.");
        if (!string.Equals(writerSchema.Fullname, TCodec.FullName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Writer record '{writerSchema.Fullname}' is incompatible with generated POCO record '{TCodec.FullName}'.");
        }
        return BuildRecord(writerSchema, TCodec.Fields.Span);
    }

    private static AvroPocoReaderPlan BuildRecord(
        RecordSchema writerSchema,
        ReadOnlySpan<AvroPocoField> readerFields)
    {
        var operations = new AvroPocoReadOperation[writerSchema.Count];
        var matched = new bool[readerFields.Length];

        for (var writerIndex = 0; writerIndex < writerSchema.Count; writerIndex++)
        {
            var writerField = writerSchema.Fields[writerIndex];
            var readerIndex = FindReaderField(readerFields, writerField.Name);
            if (readerIndex < 0)
            {
                operations[writerIndex] = new AvroPocoReadOperation(-1, BuildSkipNode(writerField.Schema));
                continue;
            }

            var readerField = readerFields[readerIndex];
            if (matched[readerIndex])
            {
                throw new InvalidOperationException(
                    $"More than one writer field resolves to generated POCO field '{readerField.Name}'.");
            }
            var writerNode = BuildNode(writerField.Schema, readerField.Type);
            operations[writerIndex] = new AvroPocoReadOperation(readerIndex, writerNode);
            matched[readerIndex] = true;
        }

        for (var readerIndex = 0; readerIndex < matched.Length; readerIndex++)
        {
            if (!matched[readerIndex] && readerFields[readerIndex].DefaultJson is null)
            {
                throw new InvalidOperationException(
                    $"Writer schema '{writerSchema.Fullname}' omits required POCO field " +
                    $"'{readerFields[readerIndex].Name}' and no Avro default is configured.");
            }
        }

        return new AvroPocoReaderPlan(operations);
    }

    private static int FindReaderField(ReadOnlySpan<AvroPocoField> fields, string writerName)
    {
        for (var index = 0; index < fields.Length; index++)
        {
            ref readonly var field = ref fields[index];
            if (string.Equals(field.Name, writerName, StringComparison.Ordinal))
                return index;

            var aliases = field.Aliases.Span;
            for (var aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
            {
                if (string.Equals(aliases[aliasIndex], writerName, StringComparison.Ordinal))
                    return index;
            }
        }

        return -1;
    }

    private static AvroPocoReadNode BuildNode(AvroSchema writer, AvroPocoType reader)
    {
        if (writer is LogicalSchema logical)
            return BuildLogicalNode(logical, reader);

        if (writer is UnionSchema union)
        {
            var readerIsUnion = reader.Kind == AvroPocoTypeKind.Union;
            var readerBranches = readerIsUnion
                ? reader.Branches.Span
                : new ReadOnlySpan<AvroPocoType>([reader]);
            var branches = new AvroPocoReadNode[union.Count];
            var requiresWriterUnionDispatch = !readerIsUnion;
            for (var index = 0; index < union.Count; index++)
            {
                var writerBranch = union[index];
                var readerBranchIndex = TryFindCompatibleReaderIndex(writerBranch, readerBranches);
                if (readerBranchIndex < 0)
                {
                    branches[index] = new AvroPocoReadNode(AvroPocoTypeKind.Null);
                    requiresWriterUnionDispatch = true;
                    continue;
                }
                branches[index] = BuildNode(writerBranch, readerBranches[readerBranchIndex]);
                branches[index].ReaderUnionBranchIndex = readerBranchIndex;
                requiresWriterUnionDispatch |= branches[index].RequiresWriterUnionDispatch;
            }

            return new AvroPocoReadNode(AvroPocoTypeKind.Union)
            {
                Branches = branches,
                RequiresWriterUnionDispatch = requiresWriterUnionDispatch
            };
        }

        if (reader.Kind == AvroPocoTypeKind.Union)
        {
            var readerBranchIndex = FindCompatibleReaderIndex(writer, reader.Branches.Span);
            var node = BuildNode(writer, reader.Branches.Span[readerBranchIndex]);
            node.ReaderUnionBranchIndex = readerBranchIndex;
            return node;
        }

        return writer switch
        {
            RecordSchema record when reader.Kind == AvroPocoTypeKind.Record &&
                                     string.Equals(record.Fullname, reader.FullName, StringComparison.Ordinal) =>
                BuildRecordNode(record, reader),
            EnumSchema @enum when reader.Kind == AvroPocoTypeKind.Enum &&
                                  string.Equals(@enum.Fullname, reader.FullName, StringComparison.Ordinal) =>
                new AvroPocoReadNode(AvroPocoTypeKind.Enum)
                {
                    EnumMap = BuildEnumMap(@enum, reader.Symbols.Span)
                },
            ArraySchema array when reader.Kind == AvroPocoTypeKind.Array =>
                BuildCollectionNode(AvroPocoTypeKind.Array, array.ItemSchema, reader.Item!),
            MapSchema map when reader.Kind == AvroPocoTypeKind.Map =>
                BuildCollectionNode(AvroPocoTypeKind.Map, map.ValueSchema, reader.Item!),
            _ when IsPrimitiveCompatible(writer.Tag, reader.Kind) =>
                new AvroPocoReadNode(ToKind(writer.Tag)),
            _ => throw Incompatible(writer, reader)
        };
    }

    private static AvroPocoReadNode BuildRecordNode(RecordSchema writer, AvroPocoType reader)
    {
        var plan = BuildRecord(writer, reader.Fields.Span);
        return new AvroPocoReadNode(AvroPocoTypeKind.Record)
        {
            RecordPlan = plan,
            Fields = BuildSkipNode(writer).Fields,
            RequiresWriterUnionDispatch = plan.RequiresWriterUnionDispatch
        };
    }

    private static AvroPocoReadNode BuildCollectionNode(
        AvroPocoTypeKind kind,
        AvroSchema writerItem,
        AvroPocoType readerItem)
    {
        var item = BuildNode(writerItem, readerItem);
        return new AvroPocoReadNode(kind)
        {
            Item = item,
            RequiresWriterUnionDispatch = item.RequiresWriterUnionDispatch
        };
    }

    private static AvroPocoReadNode BuildLogicalNode(LogicalSchema writer, AvroPocoType reader)
    {
        var kind = writer.LogicalTypeName switch
        {
            "date" => AvroPocoTypeKind.Date,
            "time-millis" => AvroPocoTypeKind.TimeMilliseconds,
            "time-micros" => AvroPocoTypeKind.TimeMicroseconds,
            "timestamp-millis" => AvroPocoTypeKind.TimestampMilliseconds,
            "timestamp-micros" => AvroPocoTypeKind.TimestampMicroseconds,
            "uuid" => AvroPocoTypeKind.Uuid,
            "decimal" => AvroPocoTypeKind.Decimal,
            _ => throw new InvalidOperationException(
                $"Writer schema uses unsupported logical type '{writer.LogicalTypeName}'.")
        };

        if (reader.Kind != kind || kind == AvroPocoTypeKind.Decimal && !DecimalMatches(writer, reader))
            throw Incompatible(writer, reader);
        return new AvroPocoReadNode(kind)
        {
            FixedSize = writer.BaseSchema is FixedSchema fixedSchema ? fixedSchema.Size : 0
        };
    }

    private static int FindCompatibleReaderIndex(
        AvroSchema writer,
        ReadOnlySpan<AvroPocoType> readers)
    {
        var index = TryFindCompatibleReaderIndex(writer, readers);
        if (index >= 0)
            return index;

        throw new InvalidOperationException(
            $"Writer Avro type '{writer.Tag}' has no compatible generated POCO union branch.");
    }

    private static int TryFindCompatibleReaderIndex(
        AvroSchema writer,
        ReadOnlySpan<AvroPocoType> readers)
    {
        for (var index = 0; index < readers.Length; index++)
        {
            if (IsCompatible(writer, readers[index]))
                return index;
        }

        return -1;
    }

    private static bool IsCompatible(AvroSchema writer, AvroPocoType reader)
    {
        if (writer is LogicalSchema logical)
        {
            var compatible = logical.LogicalTypeName switch
            {
                "date" => reader.Kind == AvroPocoTypeKind.Date,
                "time-millis" => reader.Kind == AvroPocoTypeKind.TimeMilliseconds,
                "time-micros" => reader.Kind == AvroPocoTypeKind.TimeMicroseconds,
                "timestamp-millis" => reader.Kind == AvroPocoTypeKind.TimestampMilliseconds,
                "timestamp-micros" => reader.Kind == AvroPocoTypeKind.TimestampMicroseconds,
                "uuid" => reader.Kind == AvroPocoTypeKind.Uuid,
                "decimal" => reader.Kind == AvroPocoTypeKind.Decimal,
                _ => false
            };
            return compatible && (logical.LogicalTypeName != "decimal" || DecimalMatches(logical, reader));
        }

        if (reader.Kind == AvroPocoTypeKind.Union)
        {
            var branches = reader.Branches.Span;
            for (var index = 0; index < branches.Length; index++)
            {
                if (IsCompatible(writer, branches[index]))
                    return true;
            }
            return false;
        }

        return writer switch
        {
            RecordSchema record => reader.Kind == AvroPocoTypeKind.Record &&
                string.Equals(record.Fullname, reader.FullName, StringComparison.Ordinal),
            EnumSchema @enum => reader.Kind == AvroPocoTypeKind.Enum &&
                string.Equals(@enum.Fullname, reader.FullName, StringComparison.Ordinal),
            ArraySchema array => reader.Kind == AvroPocoTypeKind.Array &&
                IsCompatible(array.ItemSchema, reader.Item!),
            MapSchema map => reader.Kind == AvroPocoTypeKind.Map &&
                IsCompatible(map.ValueSchema, reader.Item!),
            _ => IsPrimitiveCompatible(writer.Tag, reader.Kind)
        };
    }

    private static bool IsPrimitiveCompatible(AvroSchema.Type writer, AvroPocoTypeKind reader) =>
        writer switch
        {
            AvroSchema.Type.Null => reader == AvroPocoTypeKind.Null,
            AvroSchema.Type.Boolean => reader == AvroPocoTypeKind.Boolean,
            AvroSchema.Type.Int => reader is AvroPocoTypeKind.Int or AvroPocoTypeKind.Long or
                AvroPocoTypeKind.Float or AvroPocoTypeKind.Double,
            AvroSchema.Type.Long => reader is AvroPocoTypeKind.Long or AvroPocoTypeKind.Float or
                AvroPocoTypeKind.Double,
            AvroSchema.Type.Float => reader is AvroPocoTypeKind.Float or AvroPocoTypeKind.Double,
            AvroSchema.Type.Double => reader == AvroPocoTypeKind.Double,
            AvroSchema.Type.Bytes => reader is AvroPocoTypeKind.Bytes or AvroPocoTypeKind.String,
            AvroSchema.Type.String => reader is AvroPocoTypeKind.String or AvroPocoTypeKind.Bytes,
            _ => false
        };

    private static AvroPocoReadNode BuildSkipNode(AvroSchema writer) =>
        BuildSkipNode(writer, new HashSet<RecordSchema>(ReferenceEqualityComparer.Instance));

    private static AvroPocoReadNode BuildSkipNode(
        AvroSchema writer,
        HashSet<RecordSchema> activeRecords)
    {
        if (writer is LogicalSchema logical)
        {
            AvroPocoTypeKind? kind = logical.LogicalTypeName switch
            {
                "date" => AvroPocoTypeKind.Date,
                "time-millis" => AvroPocoTypeKind.TimeMilliseconds,
                "time-micros" => AvroPocoTypeKind.TimeMicroseconds,
                "timestamp-millis" => AvroPocoTypeKind.TimestampMilliseconds,
                "timestamp-micros" => AvroPocoTypeKind.TimestampMicroseconds,
                "uuid" => AvroPocoTypeKind.Uuid,
                "decimal" => AvroPocoTypeKind.Decimal,
                _ => null
            };
            if (kind is not { } logicalKind)
                return BuildSkipNode(logical.BaseSchema, activeRecords);

            return new AvroPocoReadNode(logicalKind)
            {
                FixedSize = logical.BaseSchema is FixedSchema fixedSchema ? fixedSchema.Size : 0
            };
        }

        return writer switch
        {
            RecordSchema record => BuildSkipRecord(record, activeRecords),
            ArraySchema array => new AvroPocoReadNode(AvroPocoTypeKind.Array)
            {
                Item = BuildSkipNode(array.ItemSchema, activeRecords)
            },
            MapSchema map => new AvroPocoReadNode(AvroPocoTypeKind.Map)
            {
                Item = BuildSkipNode(map.ValueSchema, activeRecords)
            },
            UnionSchema union => new AvroPocoReadNode(AvroPocoTypeKind.Union)
            {
                Branches = BuildSkipBranches(union, activeRecords)
            },
            EnumSchema => new AvroPocoReadNode(AvroPocoTypeKind.Enum),
            FixedSchema fixedSchema => new AvroPocoReadNode(AvroPocoTypeKind.Bytes)
            {
                FixedSize = fixedSchema.Size
            },
            _ => new AvroPocoReadNode(ToKind(writer.Tag))
        };
    }

    private static AvroPocoReadNode BuildSkipRecord(
        RecordSchema record,
        HashSet<RecordSchema> activeRecords)
    {
        if (!activeRecords.Add(record))
        {
            throw new InvalidOperationException(
                $"Recursive writer record '{record.Fullname}' cannot be skipped by a generated POCO reader.");
        }

        try
        {
            return new AvroPocoReadNode(AvroPocoTypeKind.Record)
            {
                Fields = BuildSkipFields(record, activeRecords)
            };
        }
        finally
        {
            activeRecords.Remove(record);
        }
    }

    private static AvroPocoReadNode[] BuildSkipFields(
        RecordSchema record,
        HashSet<RecordSchema> activeRecords)
    {
        var nodes = new AvroPocoReadNode[record.Count];
        for (var index = 0; index < nodes.Length; index++)
            nodes[index] = BuildSkipNode(record.Fields[index].Schema, activeRecords);
        return nodes;
    }

    private static AvroPocoReadNode[] BuildSkipBranches(
        UnionSchema union,
        HashSet<RecordSchema> activeRecords)
    {
        var nodes = new AvroPocoReadNode[union.Count];
        for (var index = 0; index < nodes.Length; index++)
            nodes[index] = BuildSkipNode(union[index], activeRecords);
        return nodes;
    }

    private static int[] BuildEnumMap(EnumSchema writer, ReadOnlySpan<string> readerSymbols)
    {
        var map = new int[writer.Count];
        for (var writerIndex = 0; writerIndex < writer.Count; writerIndex++)
        {
            var symbol = writer[writerIndex];
            var readerIndex = readerSymbols.IndexOf(symbol);
            map[writerIndex] = readerIndex;
        }
        return map;
    }

    private static AvroPocoTypeKind ToKind(AvroSchema.Type type) => type switch
    {
        AvroSchema.Type.Null => AvroPocoTypeKind.Null,
        AvroSchema.Type.Boolean => AvroPocoTypeKind.Boolean,
        AvroSchema.Type.Int => AvroPocoTypeKind.Int,
        AvroSchema.Type.Long => AvroPocoTypeKind.Long,
        AvroSchema.Type.Float => AvroPocoTypeKind.Float,
        AvroSchema.Type.Double => AvroPocoTypeKind.Double,
        AvroSchema.Type.Bytes => AvroPocoTypeKind.Bytes,
        AvroSchema.Type.String => AvroPocoTypeKind.String,
        _ => throw new InvalidOperationException($"Unsupported Avro writer type '{type}'.")
    };

    private static InvalidOperationException Incompatible(AvroSchema writer, AvroPocoType reader) =>
        new($"Writer Avro type '{writer.Tag}' is incompatible with generated POCO type '{reader.Kind}'.");

    private static bool DecimalMatches(LogicalSchema writer, AvroPocoType reader) =>
        int.TryParse(writer.GetProperty("precision"), NumberStyles.None, CultureInfo.InvariantCulture, out var precision) &&
        int.TryParse(writer.GetProperty("scale") ?? "0", NumberStyles.None, CultureInfo.InvariantCulture, out var scale) &&
        precision == reader.Precision && scale == reader.Scale;
}
