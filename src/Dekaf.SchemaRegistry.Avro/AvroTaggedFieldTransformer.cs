using System.Buffers;
using System.Buffers.Text;
using System.Collections.Frozen;
using System.Collections.Immutable;
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
    private readonly RulePlanCache _plans;

    private AvroTaggedFieldTransformer(AvroSchema schema, RegistrySchema registrySchema)
    {
        _schema = schema;
        _registrySchema = registrySchema;
        _plans = new RulePlanCache(schema, registrySchema);
    }

    internal static AvroTaggedFieldTransformer Get(AvroSchema schema, RegistrySchema registrySchema) =>
        Transformers.GetValue(schema, static value => new SchemaTransformers(value)).Get(registrySchema);

    public ReadOnlyMemory<byte> Transform<TState>(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        TState state,
        SchemaRegistryFieldTransform<TState> transform)
    {
        var plan = _plans.Get(
            context.Rule,
            context.PayloadContext.Schema?.RuleSet?.HasFixedRuleCollections != true);
        if (!plan.HasTargets(context.Rule))
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{context.Rule.Name}' did not match any Avro field tags.");
        }

        var workspace = GetWorkspace(this);
        workspace.Reset(payload.Span, payload.Length + 128);
        var reader = new AvroReader(payload);
        TransformValue(_schema, target: false, plan, ref reader, workspace, context, state, transform);
        if (!reader.End)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{context.Rule.Name}' encountered trailing Avro payload bytes.");
        }

        return workspace.WrittenMemory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Workspace GetWorkspace(AvroTaggedFieldTransformer transformer) =>
        (t_workspaces ??= new()).GetValue(transformer, static _ => new Workspace());

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
        var targets = plan.GetTargets(schema);
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            TransformValue(
                field.Schema,
                targets.IsTarget(field.Pos, context.Rule),
                plan,
                ref reader,
                output,
                context,
                state,
                transform);
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

    private sealed class RulePlanCache
    {
        private readonly AvroSchema _schema;
        private readonly RegistrySchema _registrySchema;
        private readonly ConditionalWeakTable<SchemaRule, RulePlan> _fixedPlans = new();
        private readonly ConditionalWeakTable<SchemaRule, MutableRulePlan> _mutablePlans = new();
        private readonly ConditionalWeakTable<SchemaRule, RulePlan>.CreateValueCallback _createFixedPlan;
        private readonly ConditionalWeakTable<SchemaRule, MutableRulePlan>.CreateValueCallback _createMutablePlan;

        public RulePlanCache(AvroSchema schema, RegistrySchema registrySchema)
        {
            _schema = schema;
            _registrySchema = registrySchema;
            _createFixedPlan = CreateFixedPlan;
            _createMutablePlan = CreateMutablePlan;
        }

        public RulePlan Get(SchemaRule rule, bool mutableTags) => mutableTags
            ? _mutablePlans.GetValue(rule, _createMutablePlan).Get()
            : _fixedPlans.GetValue(rule, _createFixedPlan);

        private RulePlan CreateFixedPlan(SchemaRule rule) =>
            RulePlan.Create(_schema, _registrySchema, rule, mutableTags: false);

        private MutableRulePlan CreateMutablePlan(SchemaRule rule) =>
            new(_schema, _registrySchema, rule);
    }

    private sealed class MutableRulePlan
    {
        private readonly object _gate = new();
        private readonly AvroSchema _schema;
        private readonly RegistrySchema _registrySchema;
        private readonly SchemaRule _rule;
        private RulePlan _plan;
        private int _metadataCount;
        private int _metadataVersion;
        private SortedDictionary<string, IReadOnlySet<string>>.Enumerator _sortedMetadataVersion;
        private MetadataTagsKind _metadataKind;

        public MutableRulePlan(AvroSchema schema, RegistrySchema registrySchema, SchemaRule rule)
        {
            _schema = schema;
            _registrySchema = registrySchema;
            _rule = rule;
            _plan = RulePlan.Create(schema, registrySchema, rule, mutableTags: true);
            CaptureMetadataVersion();
        }

        public RulePlan Get()
        {
            if (MetadataIsCurrent())
                return _plan;

            lock (_gate)
            {
                if (!MetadataIsCurrent())
                {
                    _plan = RulePlan.Create(_schema, _registrySchema, _rule, mutableTags: true);
                    CaptureMetadataVersion();
                }

                return _plan;
            }
        }

        private bool MetadataIsCurrent()
        {
            var tags = _registrySchema.Metadata?.Tags;
            if (tags is null)
                return _metadataKind == MetadataTagsKind.None;
            if (tags.Count != _metadataCount)
                return false;

            return _metadataKind switch
            {
                MetadataTagsKind.Dictionary =>
                    tags is Dictionary<string, IReadOnlySet<string>> dictionary
                    && Volatile.Read(ref GetDictionaryVersion(dictionary)) == _metadataVersion,
                MetadataTagsKind.SortedDictionary =>
                    tags is SortedDictionary<string, IReadOnlySet<string>> && SortedMetadataIsCurrent(),
                MetadataTagsKind.Immutable =>
                    tags is FrozenDictionary<string, IReadOnlySet<string>>
                        or IImmutableDictionary<string, IReadOnlySet<string>>,
                _ => false
            };
        }

        private void CaptureMetadataVersion()
        {
            var tags = _registrySchema.Metadata?.Tags;
            if (tags is null)
            {
                _metadataKind = MetadataTagsKind.None;
                _metadataCount = 0;
                return;
            }

            _metadataCount = tags.Count;
            switch (tags)
            {
                case Dictionary<string, IReadOnlySet<string>> dictionary:
                    _metadataKind = MetadataTagsKind.Dictionary;
                    _metadataVersion = Volatile.Read(ref GetDictionaryVersion(dictionary));
                    break;
                case SortedDictionary<string, IReadOnlySet<string>> sortedDictionary:
                    _metadataKind = MetadataTagsKind.SortedDictionary;
                    _sortedMetadataVersion = sortedDictionary.GetEnumerator();
                    while (_sortedMetadataVersion.MoveNext())
                        _ = _sortedMetadataVersion.Current;
                    break;
                case FrozenDictionary<string, IReadOnlySet<string>> or
                    IImmutableDictionary<string, IReadOnlySet<string>>:
                    _metadataKind = MetadataTagsKind.Immutable;
                    break;
                default:
                    throw new SchemaRegistryRuleException(
                        $"Caller-owned Avro CSFLE metadata tag map type '{tags.GetType().FullName}' cannot be tracked for mutation without a per-message scan. " +
                        "Use Dictionary<string, IReadOnlySet<string>>, SortedDictionary<string, IReadOnlySet<string>>, " +
                        "FrozenDictionary<string, IReadOnlySet<string>>, or IImmutableDictionary<string, IReadOnlySet<string>>.");
            }
        }

        private bool SortedMetadataIsCurrent()
        {
            try
            {
                var version = _sortedMetadataVersion;
                return !version.MoveNext();
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    private enum MetadataTagsKind : byte
    {
        None,
        Dictionary,
        SortedDictionary,
        Immutable
    }

    private sealed class RulePlan(
        Dictionary<global::Avro.RecordSchema, RecordTargets> records,
        MutableFieldTarget[]? mutableTargets,
        bool hasFixedTargets)
    {
        public bool HasTargets(SchemaRule rule)
        {
            if (mutableTargets is null)
                return hasFixedTargets;

            for (var i = 0; i < mutableTargets.Length; i++)
            {
                if (mutableTargets[i].Refresh(rule))
                    return true;
            }

            return false;
        }

        public RecordTargets GetTargets(global::Avro.RecordSchema schema) => records[schema];

        public static RulePlan Create(
            AvroSchema schema,
            RegistrySchema registrySchema,
            SchemaRule rule,
            bool mutableTags)
        {
            var records = new Dictionary<global::Avro.RecordSchema, RecordTargets>(ReferenceEqualityComparer.Instance);
            List<MutableFieldTarget>? mutableTargets = mutableTags ? [] : null;
            var visited = new HashSet<AvroSchema>(ReferenceEqualityComparer.Instance);
            var hasTargets = false;
            Visit(
                schema,
                registrySchema.Metadata?.Tags,
                rule,
                mutableTags,
                records,
                mutableTargets,
                visited,
                ref hasTargets);
            return new RulePlan(records, mutableTargets?.ToArray(), hasTargets);
        }

        private static void Visit(
            AvroSchema schema,
            IReadOnlyDictionary<string, IReadOnlySet<string>>? metadata,
            SchemaRule rule,
            bool mutableTags,
            Dictionary<global::Avro.RecordSchema, RecordTargets> records,
            List<MutableFieldTarget>? mutableTargets,
            HashSet<AvroSchema> visited,
            ref bool hasTargets)
        {
            if (schema is global::Avro.LogicalSchema logical)
                schema = logical.BaseSchema;

            if (!visited.Add(schema))
                return;

            switch (schema)
            {
                case global::Avro.RecordSchema record:
                    var fields = record.Fields;
                    var targets = new bool[fields.Count];
                    MutableFieldTarget?[]? mutableRecordTargets = mutableTags
                        ? new MutableFieldTarget?[fields.Count]
                        : null;
                    records.Add(record, new RecordTargets(targets, mutableRecordTargets));
                    for (var i = 0; i < fields.Count; i++)
                    {
                        var field = fields[i];
                        var fullName = record.Fullname + "." + field.Name;
                        if (mutableTags)
                        {
                            var target = MutableFieldTarget.Create(field, metadata, fullName, rule);
                            if (target is not null)
                            {
                                mutableRecordTargets![field.Pos] = target;
                                mutableTargets!.Add(target);
                                targets[field.Pos] = target.IsTarget;
                                hasTargets |= target.IsTarget;
                            }
                        }
                        else
                        {
                            var target = InlineTagsOverlap(field, rule.Tags!)
                                || MetadataTagsOverlap(metadata, fullName, rule.Tags!);
                            targets[field.Pos] = target;
                            hasTargets |= target;
                        }

                        Visit(
                            field.Schema,
                            metadata,
                            rule,
                            mutableTags,
                            records,
                            mutableTargets,
                            visited,
                            ref hasTargets);
                    }
                    break;
                case global::Avro.ArraySchema array:
                    Visit(array.ItemSchema, metadata, rule, mutableTags, records, mutableTargets, visited, ref hasTargets);
                    break;
                case global::Avro.MapSchema map:
                    Visit(map.ValueSchema, metadata, rule, mutableTags, records, mutableTargets, visited, ref hasTargets);
                    break;
                case global::Avro.UnionSchema union:
                    for (var i = 0; i < union.Count; i++)
                        Visit(union[i], metadata, rule, mutableTags, records, mutableTargets, visited, ref hasTargets);
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

        internal static bool TagsOverlap(IReadOnlySet<string> left, IReadOnlySet<string> right)
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

        internal static bool GlobMatches(string pattern, string value)
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

    private sealed class RecordTargets(bool[] targets, MutableFieldTarget?[]? mutableTargets)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsTarget(int position, SchemaRule rule)
        {
            var mutableTarget = mutableTargets?[position];
            return mutableTarget is null ? targets[position] : mutableTarget.Refresh(rule);
        }
    }

    private sealed class MutableFieldTarget(
        string[] inlineTags,
        IReadOnlySet<string>[] metadataTags,
        SchemaRule rule)
    {
        private readonly int[] _metadataVersions = CaptureVersions(metadataTags);
        private int _ruleTagsVersion = GetSetVersion(rule.Tags!);

        public bool IsTarget { get; private set; } =
            TagsOverlap(inlineTags, rule.Tags!) || MetadataTagsOverlap(metadataTags, rule.Tags!);

        public static MutableFieldTarget? Create(
            global::Avro.Field field,
            IReadOnlyDictionary<string, IReadOnlySet<string>>? metadata,
            string fullName,
            SchemaRule rule)
        {
            var inlineTags = ReadInlineTags(field);
            List<IReadOnlySet<string>>? matchingMetadata = null;
            if (metadata is not null)
            {
                foreach (var (pattern, tags) in metadata)
                {
                    if (!RulePlan.GlobMatches(pattern, fullName))
                        continue;
                    _ = GetSetVersion(tags);
                    (matchingMetadata ??= []).Add(tags);
                }
            }

            if (inlineTags.Length == 0 && matchingMetadata is null)
                return null;

            _ = GetSetVersion(rule.Tags!);
            return new MutableFieldTarget(inlineTags, matchingMetadata?.ToArray() ?? [], rule);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Refresh(SchemaRule rule)
        {
            var ruleTags = rule.Tags!;
            var ruleVersion = GetSetVersion(ruleTags);
            var changed = ruleVersion != Volatile.Read(ref _ruleTagsVersion);
            for (var i = 0; i < metadataTags.Length; i++)
            {
                var version = GetSetVersion(metadataTags[i]);
                if (version == Volatile.Read(ref _metadataVersions[i]))
                    continue;

                _metadataVersions[i] = version;
                changed = true;
            }

            if (!changed)
                return IsTarget;

            _ruleTagsVersion = ruleVersion;
            IsTarget = TagsOverlap(inlineTags, ruleTags) || MetadataTagsOverlap(metadataTags, ruleTags);
            return IsTarget;
        }

        private static string[] ReadInlineTags(global::Avro.Field field)
        {
            var tagsJson = field.GetProperty("confluent:tags");
            if (string.IsNullOrEmpty(tagsJson))
                return [];

            using var document = JsonDocument.Parse(tagsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var tags = new List<string>();
            foreach (var tag in document.RootElement.EnumerateArray())
            {
                if (tag.ValueKind == JsonValueKind.String)
                    tags.Add(tag.GetString()!);
            }

            return tags.ToArray();
        }

        private static int[] CaptureVersions(IReadOnlySet<string>[] tags)
        {
            var versions = new int[tags.Length];
            for (var i = 0; i < tags.Length; i++)
                versions[i] = GetSetVersion(tags[i]);
            return versions;
        }

        private static bool MetadataTagsOverlap(
            IReadOnlySet<string>[] metadataTags,
            IReadOnlySet<string> ruleTags)
        {
            for (var i = 0; i < metadataTags.Length; i++)
            {
                if (RulePlan.TagsOverlap(metadataTags[i], ruleTags))
                    return true;
            }

            return false;
        }

        private static bool TagsOverlap(string[] tags, IReadOnlySet<string> ruleTags)
        {
            for (var i = 0; i < tags.Length; i++)
            {
                if (ruleTags.Contains(tags[i]))
                    return true;
            }

            return false;
        }
    }

    private static int GetSetVersion(IReadOnlySet<string> tags) => tags switch
    {
        HashSet<string> hashSet => Volatile.Read(ref GetHashSetVersion(hashSet)),
        SortedSet<string> sortedSet => Volatile.Read(ref GetSortedSetVersion(sortedSet)),
        FrozenSet<string> or IImmutableSet<string> => 0,
        _ => throw new SchemaRegistryRuleException(
            $"Caller-owned Avro CSFLE tag set type '{tags.GetType().FullName}' cannot be tracked for mutation without a per-message scan. " +
            "Use HashSet<string>, SortedSet<string>, FrozenSet<string>, or IImmutableSet<string>.")
    };

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_version")]
    private static extern ref int GetDictionaryVersion(Dictionary<string, IReadOnlySet<string>> dictionary);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_version")]
    private static extern ref int GetHashSetVersion(HashSet<string> hashSet);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "version")]
    private static extern ref int GetSortedSetVersion(SortedSet<string> sortedSet);

    private sealed class Workspace
    {
        private const int MaxRetainedBufferSize = 1024 * 1024;
        private readonly byte[]?[] _outputs = new byte[2][];
        private readonly int[] _outputLengths = new int[2];
        private byte[]? _temporary;
        private int _outputSlot;
        private int _length;
        private int _temporaryLength;

        public ReadOnlyMemory<byte> WrittenMemory
        {
            get
            {
                var output = _outputs[_outputSlot]!;
                var memory = new ReadOnlyMemory<byte>(output, 0, _length);
                if (output.Length > MaxRetainedBufferSize)
                    _outputs[_outputSlot] = null;
                return memory;
            }
        }

        public void Reset(ReadOnlySpan<byte> input, int minimumCapacity)
        {
            var nextSlot = _outputSlot;
            if (_outputs[nextSlot] is not null && input.Overlaps(_outputs[nextSlot]))
                nextSlot ^= 1;
            _outputSlot = nextSlot;

            var output = _outputs[_outputSlot];
            var outputLength = _outputLengths[_outputSlot];
            if (output is not null && outputLength > 0)
                CryptographicOperations.ZeroMemory(output.AsSpan(0, outputLength));
            _outputLengths[_outputSlot] = 0;
            _length = 0;
            EnsureOutput(minimumCapacity);
        }

        public void Append(ReadOnlySpan<byte> value)
        {
            EnsureOutput(checked(_length + value.Length));
            value.CopyTo(_outputs[_outputSlot].AsSpan(_length));
            _length += value.Length;
            _outputLengths[_outputSlot] = _length;
        }

        public void WriteLong(long value)
        {
            EnsureOutput(checked(_length + 10));
            var encoded = (ulong)((value << 1) ^ (value >> 63));
            while ((encoded & ~0x7FUL) != 0)
            {
                _outputs[_outputSlot]![_length++] = (byte)((encoded & 0x7F) | 0x80);
                encoded >>= 7;
            }

            _outputs[_outputSlot]![_length++] = (byte)encoded;
            _outputLengths[_outputSlot] = _length;
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
            var output = _outputs[_outputSlot];
            if (output is not null && output.Length >= minimumLength)
                return;
            var replacement = ArrayPool<byte>.Shared.Rent(minimumLength);
            if (output is not null)
            {
                output.AsSpan(0, _length).CopyTo(replacement);
                ArrayPool<byte>.Shared.Return(output, clearArray: true);
            }
            _outputs[_outputSlot] = replacement;
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
    private Entry? _last;

    public ISchemaRegistryTaggedFieldTransformer Get(RegistrySchema schema)
    {
        var last = Volatile.Read(ref _last);
        if (last is not null && ReferenceEquals(last.Schema, schema))
            return last.Transformer;

        var transformer = _transformers.GetValue(schema, static value =>
            AvroTaggedFieldTransformer.Get(AvroSchema.Parse(value.SchemaString), value));
        Volatile.Write(ref _last, new Entry(schema, transformer));
        return transformer;
    }

    internal AvroTaggedFieldTransformer Get(RegistrySchema schema, AvroSchema avroSchema)
    {
        var last = Volatile.Read(ref _last);
        if (last is not null && ReferenceEquals(last.Schema, schema))
            return last.Transformer;

        var transformer = AvroTaggedFieldTransformer.Get(avroSchema, schema);
        Volatile.Write(ref _last, new Entry(schema, transformer));
        return transformer;
    }

    private sealed class Entry(RegistrySchema schema, AvroTaggedFieldTransformer transformer)
    {
        public RegistrySchema Schema { get; } = schema;

        public AvroTaggedFieldTransformer Transformer { get; } = transformer;
    }
}
