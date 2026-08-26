using System.Buffers;
using System.Buffers.Binary;
using AvroSchema = global::Avro.Schema;

namespace Dekaf.SchemaRegistry.Avro;

internal sealed class AvroAggregateEqualityComparer(AvroSchema schema) : IValidationCelAggregateComparer
{
    private const int StackMapEntryCount = 16;
    private const int StackMapBucketCount = 32;

    private readonly AvroSchema _schema = schema;
    private readonly bool _requiresSemanticEquality = ContainsFloating(schema, []);

    internal static AvroAggregateEqualityComparer? Create(AvroSchema schema) => schema.Tag switch
    {
        AvroSchema.Type.Record or AvroSchema.Type.Error or
        AvroSchema.Type.Array or AvroSchema.Type.Map => new(schema),
        _ => null
    };

    bool IValidationCelAggregateComparer.RequiresSemanticEquality => _requiresSemanticEquality;

    bool IValidationCelAggregateComparer.AreEqual(
        ReadOnlyMemory<byte> left,
        IValidationCelAggregateComparer rightComparer,
        ReadOnlyMemory<byte> right)
    {
        if (rightComparer is not AvroAggregateEqualityComparer avroRight ||
            (!AvroSchemaLogicalComparer.Instance.Equals(_schema, avroRight._schema) &&
             !AvroValueSchemaComparer.AreEqual(_schema, avroRight._schema)))
        {
            return false;
        }

        var leftReader = new AvroValidationReader(left);
        var rightReader = new AvroValidationReader(right);
        return AreEqual(_schema, ref leftReader, ref rightReader) &&
            leftReader.End && rightReader.End;
    }

    private static bool AreEqual(
        AvroSchema schema,
        ref AvroValidationReader left,
        ref AvroValidationReader right)
    {
        schema = AvroValueRulePlan.Unwrap(schema);
        switch (schema.Tag)
        {
            case AvroSchema.Type.Null:
                return true;
            case AvroSchema.Type.Boolean:
                return (left.Read(1).Span[0] != 0) == (right.Read(1).Span[0] != 0);
            case AvroSchema.Type.Int:
            case AvroSchema.Type.Long:
            case AvroSchema.Type.Enumeration:
                return left.ReadLong() == right.ReadLong();
            case AvroSchema.Type.Float:
                var leftFloat = BinaryPrimitives.ReadSingleLittleEndian(left.Read(sizeof(float)).Span);
                var rightFloat = BinaryPrimitives.ReadSingleLittleEndian(right.Read(sizeof(float)).Span);
                return !float.IsNaN(leftFloat) && leftFloat.CompareTo(rightFloat) == 0;
            case AvroSchema.Type.Double:
                var leftDouble = BinaryPrimitives.ReadDoubleLittleEndian(left.Read(sizeof(double)).Span);
                var rightDouble = BinaryPrimitives.ReadDoubleLittleEndian(right.Read(sizeof(double)).Span);
                return !double.IsNaN(leftDouble) && leftDouble.CompareTo(rightDouble) == 0;
            case AvroSchema.Type.String:
            case AvroSchema.Type.Bytes:
                return left.ReadLengthPrefixed().Span.SequenceEqual(right.ReadLengthPrefixed().Span);
            case AvroSchema.Type.Fixed:
                var size = ((global::Avro.FixedSchema)schema).Size;
                return left.Read(size).Span.SequenceEqual(right.Read(size).Span);
            case AvroSchema.Type.Record:
            case AvroSchema.Type.Error:
                var record = (global::Avro.RecordSchema)schema;
                for (var index = 0; index < record.Fields.Count; index++)
                {
                    if (!AreEqual(record.Fields[index].Schema, ref left, ref right))
                        return false;
                }
                return true;
            case AvroSchema.Type.Array:
                return ArraysAreEqual(
                    ((global::Avro.ArraySchema)schema).ItemSchema,
                    ref left,
                    ref right);
            case AvroSchema.Type.Map:
                return MapsAreEqual(
                    ((global::Avro.MapSchema)schema).ValueSchema,
                    ref left,
                    ref right);
            case AvroSchema.Type.Union:
                var union = (global::Avro.UnionSchema)schema;
                var leftBranch = left.ReadLong();
                var rightBranch = right.ReadLong();
                if (leftBranch != rightBranch || (ulong)leftBranch >= (ulong)union.Count)
                    return false;
                return AreEqual(union[(int)leftBranch], ref left, ref right);
            default:
                throw InvalidPayload($"unsupported schema type {schema.Tag}");
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
        AvroSchema itemSchema,
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
            if (!AreEqual(itemSchema, ref left, ref right))
                return false;
            leftRemaining--;
            rightRemaining--;
        }
    }

    private static bool MapsAreEqual(
        AvroSchema valueSchema,
        ref AvroValidationReader left,
        ref AvroValidationReader right)
    {
        var leftPayload = left.Source.Slice(left.Position);
        var rightPayload = right.Source.Slice(right.Position);
        var leftCount = CountMapEntries(valueSchema, leftPayload);
        var rightCount = CountMapEntries(valueSchema, rightPayload);
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
            ReadMapEntries(valueSchema, ref left, entries, buckets);
            return MatchMapEntries(
                valueSchema,
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
        AvroSchema valueSchema,
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
                AvroValidationValueDecoder.Skip(valueSchema, ref reader);
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
                        if (AreEqual(valueSchema, ref leftValue, ref rightValue) &&
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
}

internal static class AvroValueSchemaComparer
{
    [ThreadStatic]
    private static HashSet<AvroSchemaPair>? t_pairs;

    internal static bool AreEqual(AvroSchema left, AvroSchema right)
    {
        if (ReferenceEquals(left, right))
            return true;
        var pairs = t_pairs ??= new HashSet<AvroSchemaPair>(AvroSchemaPairReferenceComparer.Instance);
        pairs.Clear();
        try
        {
            return AreEqual(left, right, pairs);
        }
        finally
        {
            pairs.Clear();
        }
    }

    private static bool AreEqual(
        AvroSchema left,
        AvroSchema right,
        HashSet<AvroSchemaPair> pairs)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left.Tag != right.Tag)
            return false;

        var pair = default(AvroSchemaPair);
        var pairAdded = false;
        if (left is global::Avro.NamedSchema leftNamed)
        {
            if (right is not global::Avro.NamedSchema rightNamed ||
                !string.Equals(leftNamed.Fullname, rightNamed.Fullname, StringComparison.Ordinal))
            {
                return false;
            }

            pair = new AvroSchemaPair(left, right);
            if (!pairs.Add(pair))
                return true;
            pairAdded = true;
        }

        try
        {
            return left.Tag switch
            {
                AvroSchema.Type.Record or AvroSchema.Type.Error => RecordsAreEqual(
                    (global::Avro.RecordSchema)left,
                    (global::Avro.RecordSchema)right,
                    pairs),
                AvroSchema.Type.Enumeration => EnumsAreEqual(
                    (global::Avro.EnumSchema)left,
                    (global::Avro.EnumSchema)right),
                AvroSchema.Type.Array => AreEqual(
                    ((global::Avro.ArraySchema)left).ItemSchema,
                    ((global::Avro.ArraySchema)right).ItemSchema,
                    pairs),
                AvroSchema.Type.Map => AreEqual(
                    ((global::Avro.MapSchema)left).ValueSchema,
                    ((global::Avro.MapSchema)right).ValueSchema,
                    pairs),
                AvroSchema.Type.Union => UnionsAreEqual(
                    (global::Avro.UnionSchema)left,
                    (global::Avro.UnionSchema)right,
                    pairs),
                AvroSchema.Type.Fixed =>
                    ((global::Avro.FixedSchema)left).Size == ((global::Avro.FixedSchema)right).Size,
                AvroSchema.Type.Logical =>
                    string.Equals(
                        ((global::Avro.LogicalSchema)left).LogicalTypeName,
                        ((global::Avro.LogicalSchema)right).LogicalTypeName,
                        StringComparison.Ordinal) &&
                    AreEqual(
                        ((global::Avro.LogicalSchema)left).BaseSchema,
                        ((global::Avro.LogicalSchema)right).BaseSchema,
                        pairs),
                _ => true
            };
        }
        finally
        {
            if (pairAdded)
                pairs.Remove(pair);
        }
    }

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
            var rightField = right.Fields[index];
            if (!string.Equals(leftField.Name, rightField.Name, StringComparison.Ordinal) ||
                !AreEqual(leftField.Schema, rightField.Schema, pairs))
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

    private static bool UnionsAreEqual(
        global::Avro.UnionSchema left,
        global::Avro.UnionSchema right,
        HashSet<AvroSchemaPair> pairs)
    {
        if (left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++)
        {
            if (!AreEqual(left[index], right[index], pairs))
                return false;
        }
        return true;
    }
}
