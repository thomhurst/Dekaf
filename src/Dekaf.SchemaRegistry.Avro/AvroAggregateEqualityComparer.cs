using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using AvroSchema = global::Avro.Schema;

namespace Dekaf.SchemaRegistry.Avro;

internal sealed class AvroAggregateEqualityComparerFactory
{
    private readonly List<SchemaGroup> _groups = [];
    private readonly List<AvroAggregateEqualityComparer> _comparers = [];

    internal AvroAggregateEqualityComparer? Create(AvroSchema schema)
    {
        if (schema.Tag is not (AvroSchema.Type.Record or AvroSchema.Type.Error or
            AvroSchema.Type.Array or AvroSchema.Type.Map))
        {
            return null;
        }

        SchemaGroup? selectedGroup = null;
        var rawEqualityCompatible = true;
        for (var index = 0; index < _groups.Count; index++)
        {
            var group = _groups[index];
            if (AvroSchemaLogicalComparer.Instance.Equals(schema, group.Schema) ||
                AvroValueSchemaComparer.AreCelCompatible(schema, group.Schema))
            {
                selectedGroup = group;
                rawEqualityCompatible = AvroValueSchemaComparer.HaveSameEncoding(schema, group.Schema);
                break;
            }
        }

        if (selectedGroup is null)
        {
            selectedGroup = new SchemaGroup(schema);
            _groups.Add(selectedGroup);
        }

        var comparer = new AvroAggregateEqualityComparer(
            schema,
            selectedGroup,
            rawEqualityCompatible);
        for (var index = 0; index < _comparers.Count; index++)
        {
            var existing = _comparers[index];
            if (AvroSchemaLogicalComparer.Instance.Equals(schema, existing.Schema) ||
                AvroValueSchemaComparer.AreCelCompatible(schema, existing.Schema))
            {
                comparer.AddCompatible(existing);
                existing.AddCompatible(comparer);
            }
        }
        _comparers.Add(comparer);
        return comparer;
    }

    private sealed class SchemaGroup(AvroSchema schema)
    {
        internal AvroSchema Schema { get; } = schema;
    }
}

internal sealed class AvroAggregateEqualityComparer(
    AvroSchema schema,
    object schemaToken,
    bool rawEqualityCompatible) : IValidationCelAggregateComparer
{
    private const int MaximumMapEntryCount = 1_048_576;
    private const int StackRecordFieldCount = 16;
    private const int StackMapEntryCount = 16;
    private const int StackMapBucketCount = 32;

    private readonly AvroSchema _schema = schema;
    private readonly HashSet<AvroSchema> _compatibleSchemas =
        new(AvroSchemaReferenceComparer.Instance) { schema };
    private readonly bool _requiresSemanticEquality = ContainsFloating(schema, []);
    private readonly object _schemaToken = schemaToken;

    object? IValidationCelAggregateComparer.RawEqualityToken =>
        _requiresSemanticEquality || !rawEqualityCompatible ? null : _schemaToken;

    bool IValidationCelAggregateComparer.AreEqual(
        ReadOnlyMemory<byte> left,
        IValidationCelAggregateComparer rightComparer,
        ReadOnlyMemory<byte> right)
    {
        if (rightComparer is not AvroAggregateEqualityComparer avroRight ||
            !_compatibleSchemas.Contains(avroRight._schema))
            return false;

        var leftReader = new AvroValidationReader(left);
        var rightReader = new AvroValidationReader(right);
        return AreEqual(_schema, avroRight._schema, ref leftReader, ref rightReader) &&
            leftReader.End && rightReader.End;
    }

    internal AvroSchema Schema => _schema;

    internal void AddCompatible(AvroAggregateEqualityComparer comparer) =>
        _compatibleSchemas.Add(comparer._schema);

    private static bool AreEqual(
        AvroSchema leftSchema,
        AvroSchema rightSchema,
        ref AvroValidationReader left,
        ref AvroValidationReader right)
    {
        leftSchema = AvroValueRulePlan.Unwrap(leftSchema);
        rightSchema = AvroValueRulePlan.Unwrap(rightSchema);
        if (leftSchema is global::Avro.UnionSchema leftUnion)
        {
            var branch = left.ReadLong();
            if ((ulong)branch >= (ulong)leftUnion.Count)
                return false;
            leftSchema = AvroValueRulePlan.Unwrap(leftUnion[(int)branch]);
        }
        if (rightSchema is global::Avro.UnionSchema rightUnion)
        {
            var branch = right.ReadLong();
            if ((ulong)branch >= (ulong)rightUnion.Count)
                return false;
            rightSchema = AvroValueRulePlan.Unwrap(rightUnion[(int)branch]);
        }
        if (IsNumber(leftSchema.Tag) && IsNumber(rightSchema.Tag))
        {
            return ValidationCelBinaryNode.NumbersAreEqual(
                ReadNumber(leftSchema.Tag, ref left),
                ReadNumber(rightSchema.Tag, ref right));
        }
        if (IsBytes(leftSchema.Tag) && IsBytes(rightSchema.Tag))
        {
            return ReadBytes(leftSchema, ref left).Span.SequenceEqual(
                ReadBytes(rightSchema, ref right).Span);
        }
        if (IsString(leftSchema.Tag) && IsString(rightSchema.Tag))
            return StringValuesAreEqual(leftSchema, rightSchema, ref left, ref right);
        if (leftSchema.Tag != rightSchema.Tag &&
            !(leftSchema.Tag is AvroSchema.Type.Record or AvroSchema.Type.Error &&
              rightSchema.Tag is AvroSchema.Type.Record or AvroSchema.Type.Error))
        {
            return false;
        }

        switch (leftSchema.Tag)
        {
            case AvroSchema.Type.Null:
                return true;
            case AvroSchema.Type.Boolean:
                return (left.Read(1).Span[0] != 0) == (right.Read(1).Span[0] != 0);
            case AvroSchema.Type.Record:
            case AvroSchema.Type.Error:
                var leftRecord = (global::Avro.RecordSchema)leftSchema;
                var rightRecord = (global::Avro.RecordSchema)rightSchema;
                return ReferenceEquals(leftRecord, rightRecord)
                    ? RecordsAreEqualInOrder(leftRecord, ref left, ref right)
                    : RecordsAreEqualByName(leftRecord, rightRecord, ref left, ref right);
            case AvroSchema.Type.Array:
                return ArraysAreEqual(
                    ((global::Avro.ArraySchema)leftSchema).ItemSchema,
                    ((global::Avro.ArraySchema)rightSchema).ItemSchema,
                    ref left,
                    ref right);
            case AvroSchema.Type.Map:
                return MapsAreEqual(
                    ((global::Avro.MapSchema)leftSchema).ValueSchema,
                    ((global::Avro.MapSchema)rightSchema).ValueSchema,
                    ref left,
                    ref right);
            default:
                throw InvalidPayload($"unsupported schema type {leftSchema.Tag}");
        }
    }

    private static bool RecordsAreEqualInOrder(
        global::Avro.RecordSchema schema,
        ref AvroValidationReader left,
        ref AvroValidationReader right)
    {
        for (var index = 0; index < schema.Fields.Count; index++)
        {
            var fieldSchema = schema.Fields[index].Schema;
            if (!AreEqual(fieldSchema, fieldSchema, ref left, ref right))
                return false;
        }
        return true;
    }

    private static bool RecordsAreEqualByName(
        global::Avro.RecordSchema leftSchema,
        global::Avro.RecordSchema rightSchema,
        ref AvroValidationReader left,
        ref AvroValidationReader right)
    {
        RecordFieldPayload[]? rented = null;
        Span<RecordFieldPayload> rightFields = rightSchema.Fields.Count <= StackRecordFieldCount
            ? stackalloc RecordFieldPayload[StackRecordFieldCount]
            : (rented = ArrayPool<RecordFieldPayload>.Shared.Rent(rightSchema.Fields.Count));
        try
        {
            for (var index = 0; index < rightSchema.Fields.Count; index++)
            {
                var start = right.Position;
                AvroValidationValueDecoder.Skip(rightSchema.Fields[index].Schema, ref right);
                rightFields[index] = new RecordFieldPayload(start, right.Position - start);
            }

            for (var index = 0; index < leftSchema.Fields.Count; index++)
            {
                var leftField = leftSchema.Fields[index];
                if (!rightSchema.TryGetField(leftField.Name, out var rightField))
                    return false;
                var payload = rightFields[rightField.Pos];
                var rightFieldReader = new AvroValidationReader(
                    right.Source.Slice(payload.Start, payload.Length));
                if (!AreEqual(
                        leftField.Schema,
                        rightField.Schema,
                        ref left,
                        ref rightFieldReader) ||
                    !rightFieldReader.End)
                {
                    return false;
                }
            }
            return true;
        }
        finally
        {
            if (rented is not null)
                ArrayPool<RecordFieldPayload>.Shared.Return(rented);
        }
    }

    private static bool IsNumber(AvroSchema.Type type) =>
        type is AvroSchema.Type.Int or AvroSchema.Type.Long or
            AvroSchema.Type.Float or AvroSchema.Type.Double;

    private static ValidationCelValue ReadNumber(
        AvroSchema.Type type,
        ref AvroValidationReader reader) => type switch
    {
        AvroSchema.Type.Int or AvroSchema.Type.Long =>
            ValidationCelValue.FromNumber(reader.ReadLong()),
        AvroSchema.Type.Float => ValidationCelValue.FromFloating(
            BinaryPrimitives.ReadSingleLittleEndian(reader.Read(sizeof(float)).Span)),
        AvroSchema.Type.Double => ValidationCelValue.FromFloating(
            BinaryPrimitives.ReadDoubleLittleEndian(reader.Read(sizeof(double)).Span)),
        _ => throw new InvalidOperationException("Numeric Avro schema expected.")
    };

    private static bool IsBytes(AvroSchema.Type type) =>
        type is AvroSchema.Type.Bytes or AvroSchema.Type.Fixed;

    private static ReadOnlyMemory<byte> ReadBytes(
        AvroSchema schema,
        ref AvroValidationReader reader) => schema.Tag == AvroSchema.Type.Bytes
        ? reader.ReadLengthPrefixed()
        : reader.Read(((global::Avro.FixedSchema)schema).Size);

    private static bool IsString(AvroSchema.Type type) =>
        type is AvroSchema.Type.String or AvroSchema.Type.Enumeration;

    private static bool StringValuesAreEqual(
        AvroSchema leftSchema,
        AvroSchema rightSchema,
        ref AvroValidationReader left,
        ref AvroValidationReader right)
    {
        if (leftSchema is global::Avro.EnumSchema leftEnumeration)
        {
            var leftSymbol = left.ReadLong();
            if ((ulong)leftSymbol >= (ulong)leftEnumeration.Symbols.Count)
                return false;
            if (rightSchema is global::Avro.EnumSchema rightEnumeration)
            {
                var rightSymbol = right.ReadLong();
                return (ulong)rightSymbol < (ulong)rightEnumeration.Symbols.Count &&
                    string.Equals(
                        leftEnumeration.Symbols[(int)leftSymbol],
                        rightEnumeration.Symbols[(int)rightSymbol],
                        StringComparison.Ordinal);
            }
            return Utf8Equals(
                leftEnumeration.Symbols[(int)leftSymbol],
                right.ReadLengthPrefixed().Span);
        }

        var leftValue = left.ReadLengthPrefixed();
        if (rightSchema is not global::Avro.EnumSchema enumeration)
            return leftValue.Span.SequenceEqual(right.ReadLengthPrefixed().Span);
        var symbol = right.ReadLong();
        return (ulong)symbol < (ulong)enumeration.Symbols.Count &&
            Utf8Equals(enumeration.Symbols[(int)symbol], leftValue.Span);
    }

    private static bool Utf8Equals(string text, ReadOnlySpan<byte> value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(text);
        if (byteCount != value.Length)
            return false;

        byte[]? rented = null;
        Span<byte> encoded = byteCount <= 256
            ? stackalloc byte[byteCount]
            : (rented = ArrayPool<byte>.Shared.Rent(byteCount));
        try
        {
            Encoding.UTF8.GetBytes(text.AsSpan(), encoded);
            return encoded[..byteCount].SequenceEqual(value);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static bool ContainsFloating(
        AvroSchema schema,
        HashSet<AvroSchema> visited)
    {
        schema = AvroValueRulePlan.Unwrap(schema);
        if (schema.Tag is AvroSchema.Type.Float or AvroSchema.Type.Double)
            return true;
        if (!visited.Add(schema))
            return false;

        return schema switch
        {
            global::Avro.RecordSchema record => ContainsFloating(record, visited),
            global::Avro.ArraySchema array => ContainsFloating(array.ItemSchema, visited),
            global::Avro.MapSchema map => ContainsFloating(map.ValueSchema, visited),
            global::Avro.UnionSchema union => ContainsFloating(union, visited),
            _ => false
        };
    }

    private static bool ContainsFloating(
        global::Avro.RecordSchema record,
        HashSet<AvroSchema> visited)
    {
        for (var index = 0; index < record.Fields.Count; index++)
        {
            if (ContainsFloating(record.Fields[index].Schema, visited))
                return true;
        }
        return false;
    }

    private static bool ContainsFloating(
        global::Avro.UnionSchema union,
        HashSet<AvroSchema> visited)
    {
        for (var index = 0; index < union.Count; index++)
        {
            if (ContainsFloating(union[index], visited))
                return true;
        }
        return false;
    }

    private static bool ArraysAreEqual(
        AvroSchema leftItemSchema,
        AvroSchema rightItemSchema,
        ref AvroValidationReader left,
        ref AvroValidationReader right)
    {
        var leftRemaining = 0L;
        var rightRemaining = 0L;
        while (true)
        {
            if (leftRemaining == 0)
                leftRemaining = left.ReadCollectionCount();
            if (rightRemaining == 0)
                rightRemaining = right.ReadCollectionCount();
            if (leftRemaining == 0 || rightRemaining == 0)
                return leftRemaining == rightRemaining;
            if (!AreEqual(leftItemSchema, rightItemSchema, ref left, ref right))
                return false;
            leftRemaining--;
            rightRemaining--;
        }
    }

    private static bool MapsAreEqual(
        AvroSchema leftValueSchema,
        AvroSchema rightValueSchema,
        ref AvroValidationReader left,
        ref AvroValidationReader right)
    {
        var leftPayload = left.Source.Slice(left.Position);
        var rightPayload = right.Source.Slice(right.Position);
        var leftCount = CountMapEntries(leftValueSchema, leftPayload);
        var rightCount = CountMapEntries(rightValueSchema, rightPayload);
        if (leftCount != rightCount)
            return false;

        MapEntry[]? rentedEntries = null;
        int[]? rentedBuckets = null;
        Span<MapEntry> entries = leftCount <= StackMapEntryCount
            ? stackalloc MapEntry[StackMapEntryCount]
            : (rentedEntries = ArrayPool<MapEntry>.Shared.Rent(leftCount));
        Span<int> buckets = leftCount <= StackMapEntryCount
            ? stackalloc int[StackMapBucketCount]
            : (rentedBuckets = ArrayPool<int>.Shared.Rent(leftCount));
        buckets.Fill(-1);
        try
        {
            var leftSource = left.Source;
            ReadMapEntries(leftValueSchema, ref left, entries, buckets);
            return MatchMapEntries(
                leftValueSchema,
                rightValueSchema,
                leftSource,
                ref right,
                leftCount,
                entries,
                buckets);
        }
        finally
        {
            if (rentedEntries is not null)
                ArrayPool<MapEntry>.Shared.Return(rentedEntries);
            if (rentedBuckets is not null)
                ArrayPool<int>.Shared.Return(rentedBuckets);
        }
    }

    private static int CountMapEntries(AvroSchema valueSchema, ReadOnlyMemory<byte> payload)
    {
        var reader = new AvroValidationReader(payload);
        var total = 0L;
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return checked((int)total);
            total = checked(total + count);
            if (total > MaximumMapEntryCount)
            {
                throw InvalidPayload(
                    $"map entry count exceeds the supported limit of {MaximumMapEntryCount}");
            }
            for (long index = 0; index < count; index++)
            {
                _ = reader.ReadLengthPrefixed();
                AvroValidationValueDecoder.Skip(valueSchema, ref reader);
            }
        }
    }

    private static void ReadMapEntries(
        AvroSchema valueSchema,
        ref AvroValidationReader reader,
        scoped Span<MapEntry> entries,
        scoped Span<int> buckets)
    {
        var entryIndex = 0;
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return;
            for (long index = 0; index < count; index++)
            {
                var key = reader.ReadLengthPrefixed();
                var keyStart = reader.Position - key.Length;
                var valueStart = reader.Position;
                AvroValidationValueDecoder.Skip(valueSchema, ref reader);
                var hash = Hash(key.Span);
                var bucket = (int)(hash % (uint)buckets.Length);
                entries[entryIndex] = new MapEntry(
                    keyStart,
                    key.Length,
                    valueStart,
                    reader.Position - valueStart,
                    buckets[bucket]);
                buckets[bucket] = entryIndex++;
            }
        }
    }

    private static bool MatchMapEntries(
        AvroSchema leftValueSchema,
        AvroSchema rightValueSchema,
        ReadOnlyMemory<byte> leftSource,
        ref AvroValidationReader reader,
        int entryCount,
        scoped Span<MapEntry> entries,
        scoped ReadOnlySpan<int> buckets)
    {
        var matched = 0;
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return matched == entryCount;
            for (long index = 0; index < count; index++)
            {
                var key = reader.ReadLengthPrefixed();
                var valueStart = reader.Position;
                AvroValidationValueDecoder.Skip(rightValueSchema, ref reader);
                var valueLength = reader.Position - valueStart;
                var bucket = (int)(Hash(key.Span) % (uint)buckets.Length);
                var entryIndex = buckets[bucket];
                while (entryIndex >= 0)
                {
                    ref var entry = ref entries[entryIndex];
                    if (!entry.Matched && key.Span.SequenceEqual(
                            leftSource.Span.Slice(entry.KeyStart, entry.KeyLength)))
                    {
                        var leftValue = new AvroValidationReader(
                            leftSource.Slice(entry.ValueStart, entry.ValueLength));
                        var rightValue = new AvroValidationReader(
                            reader.Source.Slice(valueStart, valueLength));
                        if (AreEqual(
                                leftValueSchema,
                                rightValueSchema,
                                ref leftValue,
                                ref rightValue) &&
                            leftValue.End && rightValue.End)
                        {
                            entry.Matched = true;
                            matched++;
                            break;
                        }
                    }
                    entryIndex = entry.Next;
                }
                if (entryIndex < 0)
                    return false;
            }
        }
    }

    private static uint Hash(ReadOnlySpan<byte> value)
    {
        var hash = 2166136261u;
        for (var index = 0; index < value.Length; index++)
            hash = (hash ^ value[index]) * 16777619u;
        return hash;
    }

    private static SchemaRegistryRuleException InvalidPayload(string reason) =>
        new($"Could not evaluate Avro validation rules: {reason}.");

    private struct MapEntry(
        int keyStart,
        int keyLength,
        int valueStart,
        int valueLength,
        int next)
    {
        internal int KeyStart { get; } = keyStart;
        internal int KeyLength { get; } = keyLength;
        internal int ValueStart { get; } = valueStart;
        internal int ValueLength { get; } = valueLength;
        internal int Next { get; } = next;
        internal bool Matched { get; set; }
    }

    private readonly record struct RecordFieldPayload(int Start, int Length);
}

internal static class AvroValueSchemaComparer
{
    [ThreadStatic]
    private static HashSet<AvroSchemaPair>? t_pairs;

    internal static bool AreCelCompatible(AvroSchema left, AvroSchema right)
    {
        if (ReferenceEquals(left, right))
            return true;
        var pairs = t_pairs ??= new HashSet<AvroSchemaPair>(AvroSchemaPairReferenceComparer.Instance);
        pairs.Clear();
        try
        {
            return AreCelCompatible(left, right, pairs);
        }
        finally
        {
            pairs.Clear();
        }
    }

    internal static bool HaveSameEncoding(AvroSchema left, AvroSchema right)
    {
        if (ReferenceEquals(left, right))
            return true;
        var pairs = t_pairs ??= new HashSet<AvroSchemaPair>(AvroSchemaPairReferenceComparer.Instance);
        pairs.Clear();
        try
        {
            return HaveSameEncoding(left, right, pairs);
        }
        finally
        {
            pairs.Clear();
        }
    }

    private static bool HaveSameEncoding(
        AvroSchema left,
        AvroSchema right,
        HashSet<AvroSchemaPair> pairs)
    {
        left = AvroValueRulePlan.Unwrap(left);
        right = AvroValueRulePlan.Unwrap(right);
        if (ReferenceEquals(left, right))
            return true;
        if (left.Tag is AvroSchema.Type.Int or AvroSchema.Type.Long &&
            right.Tag is AvroSchema.Type.Int or AvroSchema.Type.Long)
        {
            return true;
        }
        if (left.Tag != right.Tag &&
            !(left.Tag is AvroSchema.Type.Record or AvroSchema.Type.Error &&
              right.Tag is AvroSchema.Type.Record or AvroSchema.Type.Error))
        {
            return false;
        }

        var pair = new AvroSchemaPair(left, right);
        if (!pairs.Add(pair))
            return true;

        try
        {
            return left.Tag switch
            {
                AvroSchema.Type.Record or AvroSchema.Type.Error => RecordsHaveSameEncoding(
                    (global::Avro.RecordSchema)left,
                    (global::Avro.RecordSchema)right,
                    pairs),
                AvroSchema.Type.Array => HaveSameEncoding(
                    ((global::Avro.ArraySchema)left).ItemSchema,
                    ((global::Avro.ArraySchema)right).ItemSchema,
                    pairs),
                AvroSchema.Type.Map => HaveSameEncoding(
                    ((global::Avro.MapSchema)left).ValueSchema,
                    ((global::Avro.MapSchema)right).ValueSchema,
                    pairs),
                AvroSchema.Type.Union => UnionsHaveSameEncoding(
                    (global::Avro.UnionSchema)left,
                    (global::Avro.UnionSchema)right,
                    pairs),
                AvroSchema.Type.Fixed =>
                    ((global::Avro.FixedSchema)left).Size == ((global::Avro.FixedSchema)right).Size,
                AvroSchema.Type.Enumeration => EnumsAreEqual(
                    (global::Avro.EnumSchema)left,
                    (global::Avro.EnumSchema)right),
                _ => true
            };
        }
        finally
        {
            pairs.Remove(pair);
        }
    }

    private static bool RecordsHaveSameEncoding(
        global::Avro.RecordSchema left,
        global::Avro.RecordSchema right,
        HashSet<AvroSchemaPair> pairs)
    {
        if (left.Fields.Count != right.Fields.Count)
            return false;
        for (var index = 0; index < left.Fields.Count; index++)
        {
            if (!string.Equals(
                    left.Fields[index].Name,
                    right.Fields[index].Name,
                    StringComparison.Ordinal) ||
                !HaveSameEncoding(left.Fields[index].Schema, right.Fields[index].Schema, pairs))
                return false;
        }
        return true;
    }

    private static bool UnionsHaveSameEncoding(
        global::Avro.UnionSchema left,
        global::Avro.UnionSchema right,
        HashSet<AvroSchemaPair> pairs)
    {
        if (left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++)
        {
            if (!HaveSameEncoding(left[index], right[index], pairs))
                return false;
        }
        return true;
    }

    private static bool AreCelCompatible(
        AvroSchema left,
        AvroSchema right,
        HashSet<AvroSchemaPair> pairs)
    {
        left = AvroValueRulePlan.Unwrap(left);
        right = AvroValueRulePlan.Unwrap(right);
        if (ReferenceEquals(left, right))
            return true;
        if (left is global::Avro.UnionSchema leftUnion)
            return AnyBranchIsCompatible(leftUnion, right, pairs);
        if (right is global::Avro.UnionSchema rightUnion)
            return AnyBranchIsCompatible(rightUnion, left, pairs);
        if (IsNumber(left.Tag) && IsNumber(right.Tag) ||
            IsBytes(left.Tag) && IsBytes(right.Tag) ||
            IsString(left.Tag) && IsString(right.Tag))
        {
            return true;
        }
        if (left.Tag != right.Tag &&
            !(left.Tag is AvroSchema.Type.Record or AvroSchema.Type.Error &&
              right.Tag is AvroSchema.Type.Record or AvroSchema.Type.Error))
        {
            return false;
        }

        var pair = new AvroSchemaPair(left, right);
        if (!pairs.Add(pair))
            return true;

        try
        {
            return left.Tag switch
            {
                AvroSchema.Type.Record or AvroSchema.Type.Error => RecordsAreEqual(
                    (global::Avro.RecordSchema)left,
                    (global::Avro.RecordSchema)right,
                    pairs),
                AvroSchema.Type.Array => AreCelCompatible(
                    ((global::Avro.ArraySchema)left).ItemSchema,
                    ((global::Avro.ArraySchema)right).ItemSchema,
                    pairs),
                AvroSchema.Type.Map => AreCelCompatible(
                    ((global::Avro.MapSchema)left).ValueSchema,
                    ((global::Avro.MapSchema)right).ValueSchema,
                    pairs),
                AvroSchema.Type.Fixed =>
                    ((global::Avro.FixedSchema)left).Size == ((global::Avro.FixedSchema)right).Size,
                _ => true
            };
        }
        finally
        {
            pairs.Remove(pair);
        }
    }

    private static bool AnyBranchIsCompatible(
        global::Avro.UnionSchema union,
        AvroSchema other,
        HashSet<AvroSchemaPair> pairs)
    {
        for (var index = 0; index < union.Count; index++)
        {
            if (AreCelCompatible(union[index], other, pairs))
                return true;
        }
        return false;
    }

    private static bool IsNumber(AvroSchema.Type type) =>
        type is AvroSchema.Type.Int or AvroSchema.Type.Long or
            AvroSchema.Type.Float or AvroSchema.Type.Double;

    private static bool IsBytes(AvroSchema.Type type) =>
        type is AvroSchema.Type.Bytes or AvroSchema.Type.Fixed;

    private static bool IsString(AvroSchema.Type type) =>
        type is AvroSchema.Type.String or AvroSchema.Type.Enumeration;

    private static bool RecordsAreEqual(
        global::Avro.RecordSchema left,
        global::Avro.RecordSchema right,
        HashSet<AvroSchemaPair> pairs)
    {
        if (left.Fields.Count != right.Fields.Count)
            return false;
        for (var index = 0; index < left.Fields.Count; index++)
        {
            var leftField = left.Fields[index];
            if (!right.TryGetField(leftField.Name, out var rightField) ||
                !AreCelCompatible(leftField.Schema, rightField.Schema, pairs))
            {
                return false;
            }
        }
        return true;
    }

    private static bool EnumsAreEqual(
        global::Avro.EnumSchema left,
        global::Avro.EnumSchema right)
    {
        if (left.Symbols.Count != right.Symbols.Count)
            return false;
        for (var index = 0; index < left.Symbols.Count; index++)
        {
            if (!string.Equals(left.Symbols[index], right.Symbols[index], StringComparison.Ordinal))
                return false;
        }
        return true;
    }

}
