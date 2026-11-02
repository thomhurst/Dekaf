using System.Globalization;

namespace Dekaf.SchemaRegistry.Avro.Poco;

internal static class AvroPocoReaderPlanBuilder
{
    internal static AvroPocoReaderPlan Build<T, TCodec>(
        string writerSchemaJson,
        AvroWriterSchemaNames? names = null)
        where TCodec : struct, IAvroPocoCodec<T> =>
        Build<T, TCodec>(AvroWriterSchema.Parse(writerSchemaJson, names));

    /// <summary>Builds a plan from an Apache.Avro schema that rule processing already resolved.</summary>
    /// <remarks>Apache.Avro writes every named type in full, so its JSON needs no reference names.</remarks>
    internal static AvroPocoReaderPlan Build<T, TCodec>(global::Avro.RecordSchema writerSchema)
        where TCodec : struct, IAvroPocoCodec<T> =>
        Build<T, TCodec>(writerSchema.ToString());

    internal static AvroPocoReaderPlan Build<T, TCodec>(AvroWriterSchema writerSchema)
        where TCodec : struct, IAvroPocoCodec<T>
    {
        if (writerSchema.Tag != AvroWriterSchemaTag.Record)
            throw new InvalidOperationException("POCO Avro writer schema must be a record.");
        if (!string.Equals(writerSchema.Fullname, TCodec.FullName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Writer record '{writerSchema.Fullname}' is incompatible with generated POCO record '{TCodec.FullName}'.");
        }
        return BuildRecord(writerSchema, TCodec.Fields.Span);
    }

    private static AvroPocoReaderPlan BuildRecord(
        AvroWriterSchema writerSchema,
        ReadOnlySpan<AvroPocoField> readerFields)
    {
        var writerFields = writerSchema.Fields;
        var operations = new AvroPocoReadOperation[writerFields.Length];
        var matched = new bool[readerFields.Length];

        for (var writerIndex = 0; writerIndex < writerFields.Length; writerIndex++)
        {
            var writerField = writerFields[writerIndex];
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

    private static AvroPocoReadNode BuildNode(AvroWriterSchema writer, AvroPocoType reader)
    {
        if (writer.Tag == AvroWriterSchemaTag.Logical)
            return BuildLogicalNode(writer, reader);

        if (writer.Tag == AvroWriterSchemaTag.Union)
        {
            var readerIsUnion = reader.Kind == AvroPocoTypeKind.Union;
            var readerBranches = readerIsUnion
                ? reader.Branches.Span
                : new ReadOnlySpan<AvroPocoType>([reader]);
            var writerBranches = writer.Branches;
            var branches = new AvroPocoReadNode[writerBranches.Length];
            var requiresWriterUnionDispatch = !readerIsUnion;
            for (var index = 0; index < writerBranches.Length; index++)
            {
                var writerBranch = writerBranches[index];
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

        return writer.Tag switch
        {
            AvroWriterSchemaTag.Record when reader.Kind == AvroPocoTypeKind.Record &&
                                            string.Equals(writer.Fullname, reader.FullName, StringComparison.Ordinal) =>
                BuildRecordNode(writer, reader),
            AvroWriterSchemaTag.Enumeration when reader.Kind == AvroPocoTypeKind.Enum &&
                                                 string.Equals(writer.Fullname, reader.FullName, StringComparison.Ordinal) =>
                new AvroPocoReadNode(AvroPocoTypeKind.Enum)
                {
                    EnumMap = BuildEnumMap(writer, reader.Symbols.Span),
                    EnumSymbolCount = writer.Symbols.Length
                },
            AvroWriterSchemaTag.Array when reader.Kind == AvroPocoTypeKind.Array =>
                BuildCollectionNode(AvroPocoTypeKind.Array, writer.Child!, reader.Item!),
            AvroWriterSchemaTag.Map when reader.Kind == AvroPocoTypeKind.Map =>
                BuildCollectionNode(AvroPocoTypeKind.Map, writer.Child!, reader.Item!),
            _ when IsPrimitiveCompatible(writer.Tag, reader.Kind) =>
                new AvroPocoReadNode(ToKind(writer.Tag)),
            _ => throw Incompatible(writer, reader)
        };
    }

    private static AvroPocoReadNode BuildRecordNode(AvroWriterSchema writer, AvroPocoType reader)
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
        AvroWriterSchema writerItem,
        AvroPocoType readerItem)
    {
        var item = BuildNode(writerItem, readerItem);
        return new AvroPocoReadNode(kind)
        {
            Item = item,
            RequiresWriterUnionDispatch = item.RequiresWriterUnionDispatch
        };
    }

    private static AvroPocoReadNode BuildLogicalNode(AvroWriterSchema writer, AvroPocoType reader)
    {
        if (reader.Kind == AvroPocoTypeKind.Union)
        {
            var readerBranchIndex = FindCompatibleReaderIndex(writer, reader.Branches.Span);
            var node = BuildLogicalNode(writer, reader.Branches.Span[readerBranchIndex]);
            node.ReaderUnionBranchIndex = readerBranchIndex;
            return node;
        }

        var kind = GetLogicalKind(writer.LogicalTypeName!);

        if (kind is not { } logicalKind)
            return BuildNode(writer.Child!, reader);

        if (reader.Kind != logicalKind)
        {
            if (IsLogicalKind(reader.Kind))
                throw Incompatible(writer, reader);

            return BuildNode(writer.Child!, reader);
        }
        if (logicalKind == AvroPocoTypeKind.Decimal && !DecimalMatches(writer, reader))
            throw Incompatible(writer, reader);
        return new AvroPocoReadNode(logicalKind)
        {
            FixedSize = GetFixedSize(writer.Child!)
        };
    }

    private static int GetFixedSize(AvroWriterSchema schema) =>
        schema.Tag == AvroWriterSchemaTag.Fixed ? schema.Size : 0;

    private static int FindCompatibleReaderIndex(
        AvroWriterSchema writer,
        ReadOnlySpan<AvroPocoType> readers)
    {
        var index = TryFindCompatibleReaderIndex(writer, readers);
        if (index >= 0)
            return index;

        throw new InvalidOperationException(
            $"Writer Avro type '{writer.Tag}' has no compatible generated POCO union branch.");
    }

    private static int TryFindCompatibleReaderIndex(
        AvroWriterSchema writer,
        ReadOnlySpan<AvroPocoType> readers)
    {
        for (var index = 0; index < readers.Length; index++)
        {
            if (IsExactMatch(writer, readers[index]))
                return index;
        }

        for (var index = 0; index < readers.Length; index++)
        {
            if (IsCompatible(writer, readers[index]))
                return index;
        }

        return -1;
    }

    private static bool IsExactMatch(AvroWriterSchema writer, AvroPocoType reader)
    {
        if (writer.Tag == AvroWriterSchemaTag.Logical)
        {
            var logicalKind = GetLogicalKind(writer.LogicalTypeName!);
            return logicalKind is null
                ? IsExactMatch(writer.Child!, reader)
                : reader.Kind == logicalKind && IsCompatible(writer, reader);
        }

        var kind = writer.Tag switch
        {
            AvroWriterSchemaTag.Null => AvroPocoTypeKind.Null,
            AvroWriterSchemaTag.Boolean => AvroPocoTypeKind.Boolean,
            AvroWriterSchemaTag.Int => AvroPocoTypeKind.Int,
            AvroWriterSchemaTag.Long => AvroPocoTypeKind.Long,
            AvroWriterSchemaTag.Float => AvroPocoTypeKind.Float,
            AvroWriterSchemaTag.Double => AvroPocoTypeKind.Double,
            AvroWriterSchemaTag.Bytes => AvroPocoTypeKind.Bytes,
            AvroWriterSchemaTag.String => AvroPocoTypeKind.String,
            AvroWriterSchemaTag.Record => AvroPocoTypeKind.Record,
            AvroWriterSchemaTag.Enumeration => AvroPocoTypeKind.Enum,
            AvroWriterSchemaTag.Array => AvroPocoTypeKind.Array,
            AvroWriterSchemaTag.Map => AvroPocoTypeKind.Map,
            _ => (AvroPocoTypeKind?)null
        };
        return reader.Kind == kind && IsCompatible(writer, reader);
    }

    private static bool IsCompatible(AvroWriterSchema writer, AvroPocoType reader)
    {
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

        if (writer.Tag == AvroWriterSchemaTag.Logical)
        {
            var logicalKind = GetLogicalKind(writer.LogicalTypeName!);
            if (logicalKind is null)
                return IsCompatible(writer.Child!, reader);
            if (reader.Kind != logicalKind)
                return !IsLogicalKind(reader.Kind) && IsCompatible(writer.Child!, reader);
            return logicalKind != AvroPocoTypeKind.Decimal || DecimalMatches(writer, reader);
        }

        return writer.Tag switch
        {
            AvroWriterSchemaTag.Record => reader.Kind == AvroPocoTypeKind.Record &&
                string.Equals(writer.Fullname, reader.FullName, StringComparison.Ordinal),
            AvroWriterSchemaTag.Enumeration => reader.Kind == AvroPocoTypeKind.Enum &&
                string.Equals(writer.Fullname, reader.FullName, StringComparison.Ordinal),
            AvroWriterSchemaTag.Array => reader.Kind == AvroPocoTypeKind.Array &&
                IsCompatible(writer.Child!, reader.Item!),
            AvroWriterSchemaTag.Map => reader.Kind == AvroPocoTypeKind.Map &&
                IsCompatible(writer.Child!, reader.Item!),
            _ => IsPrimitiveCompatible(writer.Tag, reader.Kind)
        };
    }

    private static bool IsPrimitiveCompatible(AvroWriterSchemaTag writer, AvroPocoTypeKind reader) =>
        writer switch
        {
            AvroWriterSchemaTag.Null => reader == AvroPocoTypeKind.Null,
            AvroWriterSchemaTag.Boolean => reader == AvroPocoTypeKind.Boolean,
            AvroWriterSchemaTag.Int => reader is AvroPocoTypeKind.Int or AvroPocoTypeKind.Long or
                AvroPocoTypeKind.Float or AvroPocoTypeKind.Double or AvroPocoTypeKind.Date or
                AvroPocoTypeKind.TimeMilliseconds,
            AvroWriterSchemaTag.Long => reader is AvroPocoTypeKind.Long or AvroPocoTypeKind.Float or
                AvroPocoTypeKind.Double or AvroPocoTypeKind.TimeMicroseconds or
                AvroPocoTypeKind.TimestampMilliseconds or AvroPocoTypeKind.TimestampMicroseconds,
            AvroWriterSchemaTag.Float => reader is AvroPocoTypeKind.Float or AvroPocoTypeKind.Double,
            AvroWriterSchemaTag.Double => reader == AvroPocoTypeKind.Double,
            AvroWriterSchemaTag.Bytes => reader is AvroPocoTypeKind.Bytes or AvroPocoTypeKind.String,
            AvroWriterSchemaTag.String => reader is AvroPocoTypeKind.String or AvroPocoTypeKind.Bytes or
                AvroPocoTypeKind.Uuid,
            _ => false
        };

    private static AvroPocoReadNode BuildSkipNode(AvroWriterSchema writer) =>
        BuildSkipNode(
            writer,
            new Dictionary<AvroWriterSchema, AvroPocoReadNode>(ReferenceEqualityComparer.Instance));

    private static AvroPocoReadNode BuildSkipNode(
        AvroWriterSchema writer,
        Dictionary<AvroWriterSchema, AvroPocoReadNode> records)
    {
        if (writer.Tag == AvroWriterSchemaTag.Logical)
        {
            var kind = GetLogicalKind(writer.LogicalTypeName!);
            if (kind is not { } logicalKind)
                return BuildSkipNode(writer.Child!, records);

            return new AvroPocoReadNode(logicalKind)
            {
                FixedSize = GetFixedSize(writer.Child!)
            };
        }

        return writer.Tag switch
        {
            AvroWriterSchemaTag.Record => BuildSkipRecord(writer, records),
            AvroWriterSchemaTag.Array => BuildSkipCollection(writer, records),
            AvroWriterSchemaTag.Map => new AvroPocoReadNode(AvroPocoTypeKind.Map)
            {
                Item = BuildSkipNode(writer.Child!, records)
            },
            AvroWriterSchemaTag.Union => new AvroPocoReadNode(AvroPocoTypeKind.Union)
            {
                Branches = BuildSkipBranches(writer, records)
            },
            AvroWriterSchemaTag.Enumeration => new AvroPocoReadNode(AvroPocoTypeKind.Enum)
            {
                EnumSymbolCount = writer.Symbols.Length
            },
            AvroWriterSchemaTag.Fixed => new AvroPocoReadNode(AvroPocoTypeKind.Bytes)
            {
                FixedSize = writer.Size
            },
            _ => new AvroPocoReadNode(ToKind(writer.Tag))
            {
                IsZeroWidth = writer.Tag == AvroWriterSchemaTag.Null
            }
        };
    }

    private static AvroPocoReadNode BuildSkipRecord(
        AvroWriterSchema record,
        Dictionary<AvroWriterSchema, AvroPocoReadNode> records)
    {
        if (records.TryGetValue(record, out var existing))
            return existing;

        var node = new AvroPocoReadNode(AvroPocoTypeKind.Record);
        records.Add(record, node);
        node.Fields = BuildSkipFields(record, records);
        node.IsZeroWidth = AreZeroWidth(node.Fields.Span);
        return node;
    }

    private static AvroPocoReadNode BuildSkipCollection(
        AvroWriterSchema array,
        Dictionary<AvroWriterSchema, AvroPocoReadNode> records)
    {
        var item = BuildSkipNode(array.Child!, records);
        return new AvroPocoReadNode(item.IsZeroWidth
            ? AvroPocoTypeKind.ZeroWidthArray
            : AvroPocoTypeKind.Array)
        {
            Item = item
        };
    }

    private static bool AreZeroWidth(ReadOnlySpan<AvroPocoReadNode> nodes)
    {
        for (var index = 0; index < nodes.Length; index++)
        {
            if (!nodes[index].IsZeroWidth)
                return false;
        }

        return true;
    }

    private static AvroPocoReadNode[] BuildSkipFields(
        AvroWriterSchema record,
        Dictionary<AvroWriterSchema, AvroPocoReadNode> records)
    {
        var fields = record.Fields;
        var nodes = new AvroPocoReadNode[fields.Length];
        for (var index = 0; index < nodes.Length; index++)
            nodes[index] = BuildSkipNode(fields[index].Schema, records);
        return nodes;
    }

    private static AvroPocoReadNode[] BuildSkipBranches(
        AvroWriterSchema union,
        Dictionary<AvroWriterSchema, AvroPocoReadNode> records)
    {
        var branches = union.Branches;
        var nodes = new AvroPocoReadNode[branches.Length];
        for (var index = 0; index < nodes.Length; index++)
            nodes[index] = BuildSkipNode(branches[index], records);
        return nodes;
    }

    private static int[] BuildEnumMap(AvroWriterSchema writer, ReadOnlySpan<string> readerSymbols)
    {
        var symbols = writer.Symbols;
        var map = new int[symbols.Length];
        for (var writerIndex = 0; writerIndex < symbols.Length; writerIndex++)
            map[writerIndex] = readerSymbols.IndexOf(symbols[writerIndex]);
        return map;
    }

    private static AvroPocoTypeKind ToKind(AvroWriterSchemaTag type) => type switch
    {
        AvroWriterSchemaTag.Null => AvroPocoTypeKind.Null,
        AvroWriterSchemaTag.Boolean => AvroPocoTypeKind.Boolean,
        AvroWriterSchemaTag.Int => AvroPocoTypeKind.Int,
        AvroWriterSchemaTag.Long => AvroPocoTypeKind.Long,
        AvroWriterSchemaTag.Float => AvroPocoTypeKind.Float,
        AvroWriterSchemaTag.Double => AvroPocoTypeKind.Double,
        AvroWriterSchemaTag.Bytes => AvroPocoTypeKind.Bytes,
        AvroWriterSchemaTag.String => AvroPocoTypeKind.String,
        _ => throw new InvalidOperationException($"Unsupported Avro writer type '{type}'.")
    };

    private static InvalidOperationException Incompatible(AvroWriterSchema writer, AvroPocoType reader) =>
        new($"Writer Avro type '{writer.Tag}' is incompatible with generated POCO type '{reader.Kind}'.");

    private static AvroPocoTypeKind? GetLogicalKind(string logicalTypeName) => logicalTypeName switch
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

    private static bool IsLogicalKind(AvroPocoTypeKind kind) =>
        kind is AvroPocoTypeKind.Date or
            AvroPocoTypeKind.TimeMilliseconds or AvroPocoTypeKind.TimeMicroseconds or
            AvroPocoTypeKind.TimestampMilliseconds or AvroPocoTypeKind.TimestampMicroseconds or
            AvroPocoTypeKind.Decimal or AvroPocoTypeKind.Uuid;

    private static bool DecimalMatches(AvroWriterSchema writer, AvroPocoType reader) =>
        int.TryParse(writer.GetPropertyText("precision"), NumberStyles.None, CultureInfo.InvariantCulture, out var precision) &&
        int.TryParse(writer.GetPropertyText("scale") ?? "0", NumberStyles.None, CultureInfo.InvariantCulture, out var scale) &&
        precision == reader.Precision && scale == reader.Scale;
}
