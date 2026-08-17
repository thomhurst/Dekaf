using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using AvroSchema = Avro.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.SchemaRegistry.Avro;

internal sealed class AvroTaggedFieldTransformer : ISchemaRegistryTaggedFieldTransformer
{
    [ThreadStatic]
    private static ConditionalWeakTable<AvroTaggedFieldTransformer, Workspace>? t_workspaces;

    private static readonly ConditionalWeakTable<AvroSchema, SchemaTransformers> Transformers = new();

    private readonly AvroSchema _schema;
    private readonly RegistrySchema _registrySchema;
    private readonly ConditionalWeakTable<SchemaRule, RulePlan> _plans = new();
    private readonly ConditionalWeakTable<SchemaRule, RulePlan>.CreateValueCallback _createPlan;

    private AvroTaggedFieldTransformer(AvroSchema schema, RegistrySchema registrySchema)
    {
        _schema = schema;
        _registrySchema = registrySchema;
        _createPlan = CreatePlan;
    }

    internal static AvroTaggedFieldTransformer Get(AvroSchema schema, RegistrySchema registrySchema) =>
        Transformers.GetValue(schema, static value => new SchemaTransformers(value)).Get(registrySchema);

    public ReadOnlyMemory<byte> Transform<TState>(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        TState state,
        SchemaRegistryFieldTransform<TState> transform)
    {
        var plan = _plans.GetValue(context.Rule, _createPlan);
        if (plan.Targets.Count == 0)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{context.Rule.Name}' did not match any Avro field tags.");
        }

        var workspace = (t_workspaces ??= new()).GetValue(this, static _ => new Workspace());
        workspace.Reset(payload.Length + 128);
        var reader = new AvroReader(payload);
        TransformValue(_schema, target: false, plan, ref reader, workspace, context, state, transform);
        if (!reader.End)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{context.Rule.Name}' encountered trailing Avro payload bytes.");
        }

        return workspace.WrittenMemory;
    }

    private RulePlan CreatePlan(SchemaRule rule) => RulePlan.Create(_schema, _registrySchema, rule);

    private static void TransformValue<TState>(
        AvroSchema schema,
        bool target,
        RulePlan plan,
        ref AvroReader reader,
        Workspace output,
        SchemaRegistryRuleHandlerContext context,
        TState state,
        SchemaRegistryFieldTransform<TState> transform)
    {
        if (schema is global::Avro.LogicalSchema logicalSchema)
        {
            TransformValue(logicalSchema.BaseSchema, target, plan, ref reader, output, context, state, transform);
            return;
        }

        switch (schema.Tag)
        {
            case AvroSchema.Type.Null:
                return;
            case AvroSchema.Type.Boolean:
                CopyFixed(ref reader, output, 1);
                return;
            case AvroSchema.Type.Int:
            case AvroSchema.Type.Long:
            case AvroSchema.Type.Enumeration:
                CopyLong(ref reader, output);
                return;
            case AvroSchema.Type.Float:
                CopyFixed(ref reader, output, sizeof(float));
                return;
            case AvroSchema.Type.Double:
                CopyFixed(ref reader, output, sizeof(double));
                return;
            case AvroSchema.Type.String:
                TransformLengthPrefixed(target, encodeBase64: true, ref reader, output, context, state, transform);
                return;
            case AvroSchema.Type.Bytes:
                TransformLengthPrefixed(target, encodeBase64: false, ref reader, output, context, state, transform);
                return;
            case AvroSchema.Type.Fixed:
                if (target)
                    ThrowUnsupported(context, schema.Tag);
                CopyFixed(ref reader, output, ((global::Avro.FixedSchema)schema).Size);
                return;
            case AvroSchema.Type.Record:
            case AvroSchema.Type.Error:
                if (target)
                    ThrowUnsupported(context, schema.Tag);
                TransformRecord((global::Avro.RecordSchema)schema, plan, ref reader, output, context, state, transform);
                return;
            case AvroSchema.Type.Array:
                TransformArray((global::Avro.ArraySchema)schema, target, plan, ref reader, output, context, state, transform);
                return;
            case AvroSchema.Type.Map:
                TransformMap((global::Avro.MapSchema)schema, target, plan, ref reader, output, context, state, transform);
                return;
            case AvroSchema.Type.Union:
                TransformUnion((global::Avro.UnionSchema)schema, target, plan, ref reader, output, context, state, transform);
                return;
            default:
                throw new SchemaRegistryRuleException(
                    $"Schema Registry rule '{context.Rule.Name}' encountered unsupported Avro type {schema.Tag}.");
        }
    }

    private static void TransformRecord<TState>(
        global::Avro.RecordSchema schema,
        RulePlan plan,
        ref AvroReader reader,
        Workspace output,
        SchemaRegistryRuleHandlerContext context,
        TState state,
        SchemaRegistryFieldTransform<TState> transform)
    {
        var fields = schema.Fields;
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            TransformValue(field.Schema, plan.Targets.Contains(field), plan, ref reader, output, context, state, transform);
        }
    }

    private static void TransformUnion<TState>(
        global::Avro.UnionSchema schema,
        bool target,
        RulePlan plan,
        ref AvroReader reader,
        Workspace output,
        SchemaRegistryRuleHandlerContext context,
        TState state,
        SchemaRegistryFieldTransform<TState> transform)
    {
        var start = reader.Position;
        var branch = reader.ReadLong();
        output.Append(reader.Slice(start).Span);
        if ((ulong)branch >= (ulong)schema.Count)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{context.Rule.Name}' encountered invalid Avro union index {branch}.");
        }

        TransformValue(schema[(int)branch], target, plan, ref reader, output, context, state, transform);
    }

    private static void TransformArray<TState>(
        global::Avro.ArraySchema schema,
        bool target,
        RulePlan plan,
        ref AvroReader reader,
        Workspace output,
        SchemaRegistryRuleHandlerContext context,
        TState state,
        SchemaRegistryFieldTransform<TState> transform)
    {
        while (true)
        {
            var countStart = reader.Position;
            var count = reader.ReadLong();
            if (count == 0)
            {
                output.Append(reader.Slice(countStart).Span);
                return;
            }

            if (count < 0)
            {
                if (count == long.MinValue)
                    ThrowInvalidBlock(context);
                count = -count;
                _ = reader.ReadLength();
                output.WriteLong(count);
            }
            else
            {
                output.Append(reader.Slice(countStart).Span);
            }

            for (long i = 0; i < count; i++)
                TransformValue(schema.ItemSchema, target, plan, ref reader, output, context, state, transform);
        }
    }

    private static void TransformMap<TState>(
        global::Avro.MapSchema schema,
        bool target,
        RulePlan plan,
        ref AvroReader reader,
        Workspace output,
        SchemaRegistryRuleHandlerContext context,
        TState state,
        SchemaRegistryFieldTransform<TState> transform)
    {
        while (true)
        {
            var countStart = reader.Position;
            var count = reader.ReadLong();
            if (count == 0)
            {
                output.Append(reader.Slice(countStart).Span);
                return;
            }

            if (count < 0)
            {
                if (count == long.MinValue)
                    ThrowInvalidBlock(context);
                count = -count;
                _ = reader.ReadLength();
                output.WriteLong(count);
            }
            else
            {
                output.Append(reader.Slice(countStart).Span);
            }

            for (long i = 0; i < count; i++)
            {
                TransformLengthPrefixed(
                    target: false,
                    encodeBase64: false,
                    ref reader,
                    output,
                    context,
                    state,
                    transform);
                TransformValue(schema.ValueSchema, target, plan, ref reader, output, context, state, transform);
            }
        }
    }

    private static void TransformLengthPrefixed<TState>(
        bool target,
        bool encodeBase64,
        ref AvroReader reader,
        Workspace output,
        SchemaRegistryRuleHandlerContext context,
        TState state,
        SchemaRegistryFieldTransform<TState> transform)
    {
        var start = reader.Position;
        var length = reader.ReadLength();
        var value = reader.Read(length);
        if (!target)
        {
            output.Append(reader.Slice(start).Span);
            return;
        }

        ReadOnlyMemory<byte> transformed;
        if (encodeBase64 && context.Direction == SchemaRegistryRuleDirection.Read)
        {
            var decoded = output.DecodeBase64(value.Span, context.Rule.Name);
            transformed = transform(decoded, context, state);
        }
        else
        {
            transformed = transform(value, context, state);
        }

        if (encodeBase64 && context.Direction == SchemaRegistryRuleDirection.Write)
        {
            var encoded = output.EncodeBase64(transformed.Span);
            output.WriteLong(encoded.Length);
            output.Append(encoded.Span);
        }
        else
        {
            output.WriteLong(transformed.Length);
            output.Append(transformed.Span);
        }
    }

    private static void CopyLong(ref AvroReader reader, Workspace output)
    {
        var start = reader.Position;
        _ = reader.ReadLong();
        output.Append(reader.Slice(start).Span);
    }

    private static void CopyFixed(ref AvroReader reader, Workspace output, int length) =>
        output.Append(reader.Read(length).Span);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnsupported(SchemaRegistryRuleHandlerContext context, AvroSchema.Type type) =>
        throw new SchemaRegistryRuleException(
            $"Schema Registry rule '{context.Rule.Name}' can only encrypt Avro string and bytes fields; tagged {type} is unsupported.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidBlock(SchemaRegistryRuleHandlerContext context) =>
        throw new SchemaRegistryRuleException(
            $"Schema Registry rule '{context.Rule.Name}' encountered an invalid Avro collection block.");

    private sealed class SchemaTransformers
    {
        private readonly ConditionalWeakTable<RegistrySchema, AvroTaggedFieldTransformer> _values = new();
        private readonly ConditionalWeakTable<RegistrySchema, AvroTaggedFieldTransformer>.CreateValueCallback _create;

        public SchemaTransformers(AvroSchema schema) =>
            _create = value => new AvroTaggedFieldTransformer(schema, value);

        public AvroTaggedFieldTransformer Get(RegistrySchema registrySchema) =>
            _values.GetValue(registrySchema, _create);
    }

    private sealed class RulePlan(HashSet<global::Avro.Field> targets)
    {
        public HashSet<global::Avro.Field> Targets { get; } = targets;

        public static RulePlan Create(AvroSchema schema, RegistrySchema registrySchema, SchemaRule rule)
        {
            var targets = new HashSet<global::Avro.Field>(ReferenceEqualityComparer.Instance);
            var visited = new HashSet<AvroSchema>(ReferenceEqualityComparer.Instance);
            Visit(schema, registrySchema.Metadata?.Tags, rule.Tags!, targets, visited);
            return new RulePlan(targets);
        }

        private static void Visit(
            AvroSchema schema,
            IReadOnlyDictionary<string, IReadOnlySet<string>>? metadata,
            IReadOnlySet<string> ruleTags,
            HashSet<global::Avro.Field> targets,
            HashSet<AvroSchema> visited)
        {
            if (schema is global::Avro.LogicalSchema logical)
                schema = logical.BaseSchema;

            if (!visited.Add(schema))
                return;

            switch (schema)
            {
                case global::Avro.RecordSchema record:
                    var fields = record.Fields;
                    for (var i = 0; i < fields.Count; i++)
                    {
                        var field = fields[i];
                        var fullName = record.Fullname + "." + field.Name;
                        if (InlineTagsOverlap(field, ruleTags) || MetadataTagsOverlap(metadata, fullName, ruleTags))
                            targets.Add(field);
                        Visit(field.Schema, metadata, ruleTags, targets, visited);
                    }
                    break;
                case global::Avro.ArraySchema array:
                    Visit(array.ItemSchema, metadata, ruleTags, targets, visited);
                    break;
                case global::Avro.MapSchema map:
                    Visit(map.ValueSchema, metadata, ruleTags, targets, visited);
                    break;
                case global::Avro.UnionSchema union:
                    for (var i = 0; i < union.Count; i++)
                        Visit(union[i], metadata, ruleTags, targets, visited);
                    break;
            }
        }

        private static bool InlineTagsOverlap(global::Avro.Field field, IReadOnlySet<string> ruleTags)
        {
            var tagsJson = field.GetProperty("confluent:tags");
            if (string.IsNullOrEmpty(tagsJson))
                return false;

            using var document = JsonDocument.Parse(tagsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var tag in document.RootElement.EnumerateArray())
            {
                if (tag.ValueKind == JsonValueKind.String && ruleTags.Contains(tag.GetString()!))
                    return true;
            }

            return false;
        }

        private static bool MetadataTagsOverlap(
            IReadOnlyDictionary<string, IReadOnlySet<string>>? metadata,
            string fullName,
            IReadOnlySet<string> ruleTags)
        {
            if (metadata is null)
                return false;

            foreach (var (pattern, tags) in metadata)
            {
                if (GlobMatches(pattern, fullName) && TagsOverlap(tags, ruleTags))
                    return true;
            }

            return false;
        }

        private static bool TagsOverlap(IReadOnlySet<string> left, IReadOnlySet<string> right)
        {
            var smaller = left.Count <= right.Count ? left : right;
            var larger = ReferenceEquals(smaller, left) ? right : left;
            foreach (var tag in smaller)
            {
                if (larger.Contains(tag))
                    return true;
            }

            return false;
        }

        private static bool GlobMatches(string pattern, string value)
        {
            var patternIndex = 0;
            var valueIndex = 0;
            var starIndex = -1;
            var starValueIndex = -1;
            var starCrossesSegments = false;
            while (valueIndex < value.Length)
            {
                if (patternIndex < pattern.Length && pattern[patternIndex] == value[valueIndex])
                {
                    patternIndex++;
                    valueIndex++;
                    continue;
                }

                if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                {
                    starIndex = patternIndex++;
                    starCrossesSegments = patternIndex < pattern.Length && pattern[patternIndex] == '*';
                    if (starCrossesSegments)
                        patternIndex++;
                    starValueIndex = valueIndex;
                    continue;
                }

                if (starIndex >= 0 && (starCrossesSegments || value[starValueIndex] != '.'))
                {
                    valueIndex = ++starValueIndex;
                    patternIndex = starIndex + (starCrossesSegments ? 2 : 1);
                    continue;
                }

                return false;
            }

            while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                patternIndex++;
            return patternIndex == pattern.Length;
        }
    }

    private sealed class Workspace
    {
        private byte[]? _output;
        private byte[]? _temporary;
        private int _length;
        private int _temporaryLength;

        public ReadOnlyMemory<byte> WrittenMemory => new(_output!, 0, _length);

        public void Reset(int minimumCapacity)
        {
            if (_output is not null && _length > 0)
                CryptographicOperations.ZeroMemory(_output.AsSpan(0, _length));
            _length = 0;
            EnsureOutput(minimumCapacity);
        }

        public void Append(ReadOnlySpan<byte> value)
        {
            EnsureOutput(checked(_length + value.Length));
            value.CopyTo(_output.AsSpan(_length));
            _length += value.Length;
        }

        public void WriteLong(long value)
        {
            EnsureOutput(checked(_length + 10));
            var encoded = (ulong)((value << 1) ^ (value >> 63));
            while ((encoded & ~0x7FUL) != 0)
            {
                _output![_length++] = (byte)((encoded & 0x7F) | 0x80);
                encoded >>= 7;
            }

            _output![_length++] = (byte)encoded;
        }

        public ReadOnlyMemory<byte> DecodeBase64(ReadOnlySpan<byte> value, string ruleName)
        {
            var temporary = GetTemporary(Base64.GetMaxDecodedFromUtf8Length(value.Length));
            var status = Base64.DecodeFromUtf8(value, temporary, out var consumed, out var written);
            if (status != OperationStatus.Done || consumed != value.Length)
            {
                throw new SchemaRegistryRuleException(
                    $"Schema Registry rule '{ruleName}' encountered invalid Base64 in an encrypted Avro string field.");
            }

            _temporaryLength = written;
            return new ReadOnlyMemory<byte>(_temporary!, 0, written);
        }

        public ReadOnlyMemory<byte> EncodeBase64(ReadOnlySpan<byte> value)
        {
            var temporary = GetTemporary(Base64.GetMaxEncodedToUtf8Length(value.Length));
            var status = Base64.EncodeToUtf8(value, temporary, out var consumed, out var written);
            if (status != OperationStatus.Done || consumed != value.Length)
                throw new InvalidOperationException("Could not encode the encrypted Avro string field as Base64.");
            _temporaryLength = written;
            return new ReadOnlyMemory<byte>(_temporary!, 0, written);
        }

        private Span<byte> GetTemporary(int minimumLength)
        {
            if (_temporary is not null && _temporaryLength > 0)
                CryptographicOperations.ZeroMemory(_temporary.AsSpan(0, _temporaryLength));
            _temporaryLength = 0;
            if (_temporary is null || _temporary.Length < minimumLength)
            {
                if (_temporary is not null)
                    ArrayPool<byte>.Shared.Return(_temporary, clearArray: true);
                _temporary = ArrayPool<byte>.Shared.Rent(minimumLength);
            }

            return _temporary.AsSpan(0, minimumLength);
        }

        private void EnsureOutput(int minimumLength)
        {
            if (_output is not null && _output.Length >= minimumLength)
                return;
            var replacement = ArrayPool<byte>.Shared.Rent(minimumLength);
            if (_output is not null)
            {
                _output.AsSpan(0, _length).CopyTo(replacement);
                ArrayPool<byte>.Shared.Return(_output, clearArray: true);
            }
            _output = replacement;
        }
    }

    private ref struct AvroReader(ReadOnlyMemory<byte> payload)
    {
        private readonly ReadOnlyMemory<byte> _payload = payload;

        public int Position { get; private set; }

        public bool End => Position == _payload.Length;

        public ReadOnlyMemory<byte> Read(int length)
        {
            if (length < 0 || length > _payload.Length - Position)
                throw new SchemaRegistryRuleException("Avro payload ended before the tagged field transform completed.");
            var value = _payload.Slice(Position, length);
            Position += length;
            return value;
        }

        public int ReadLength()
        {
            var length = ReadLong();
            if ((ulong)length > int.MaxValue)
                throw new SchemaRegistryRuleException($"Invalid Avro length {length}.");
            return (int)length;
        }

        public long ReadLong()
        {
            ulong encoded = 0;
            for (var shift = 0; shift < 70; shift += 7)
            {
                if (Position >= _payload.Length)
                    throw new SchemaRegistryRuleException("Avro payload ended inside a variable-length integer.");
                var current = _payload.Span[Position++];
                if (shift == 63 && current > 1)
                    throw new SchemaRegistryRuleException("Avro payload contains an invalid variable-length integer.");
                encoded |= (ulong)(current & 0x7F) << shift;
                if ((current & 0x80) == 0)
                    return (long)(encoded >> 1) ^ -((long)encoded & 1);
            }

            throw new SchemaRegistryRuleException("Avro payload contains an invalid variable-length integer.");
        }

        public ReadOnlyMemory<byte> Slice(int start) => _payload.Slice(start, Position - start);
    }
}

internal sealed class AvroTaggedFieldTransformerProvider : ISchemaRegistryTaggedFieldTransformerProvider
{
    private readonly ConditionalWeakTable<RegistrySchema, AvroTaggedFieldTransformer> _transformers = new();
    private RegistrySchema? _lastSchema;
    private AvroTaggedFieldTransformer? _lastTransformer;

    public ISchemaRegistryTaggedFieldTransformer Get(RegistrySchema schema)
    {
        var lastSchema = Volatile.Read(ref _lastSchema);
        var lastTransformer = Volatile.Read(ref _lastTransformer);
        if (ReferenceEquals(lastSchema, schema) && lastTransformer is not null)
            return lastTransformer;

        var transformer = _transformers.GetValue(schema, static value =>
            AvroTaggedFieldTransformer.Get(AvroSchema.Parse(value.SchemaString), value));
        Publish(schema, transformer);
        return transformer;
    }

    internal AvroTaggedFieldTransformer Get(RegistrySchema schema, AvroSchema avroSchema)
    {
        var lastSchema = Volatile.Read(ref _lastSchema);
        var lastTransformer = Volatile.Read(ref _lastTransformer);
        if (ReferenceEquals(lastSchema, schema) && lastTransformer is not null)
            return lastTransformer;

        var transformer = AvroTaggedFieldTransformer.Get(avroSchema, schema);
        Publish(schema, transformer);
        return transformer;
    }

    private void Publish(RegistrySchema schema, AvroTaggedFieldTransformer transformer)
    {
        Volatile.Write(ref _lastTransformer, transformer);
        Volatile.Write(ref _lastSchema, schema);
    }
}
