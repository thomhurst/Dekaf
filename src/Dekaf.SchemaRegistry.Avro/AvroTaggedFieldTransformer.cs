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
    private static Workspace? t_workspace;

    private static readonly ConditionalWeakTable<AvroSchema, SchemaTransformers> Transformers = new();

    private readonly AvroSchema _schema;
    private readonly RegistrySchema _registrySchema;
    private readonly RulePlanCache _plans;

    private AvroTaggedFieldTransformer(
        AvroSchema schema,
        AvroSchema tagSchema,
        RegistrySchema registrySchema)
    {
        _schema = schema;
        _registrySchema = registrySchema;
        _plans = new RulePlanCache(schema, tagSchema, registrySchema);
    }

    internal static AvroTaggedFieldTransformer Get(
        AvroSchema schema,
        RegistrySchema registrySchema,
        AvroSchema? tagSchema = null) =>
        Transformers.GetValue(schema, static value => new SchemaTransformers(value))
            .Get(registrySchema, tagSchema ?? schema);

    public ReadOnlyMemory<byte> Transform<TState>(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        TState state,
        SchemaRegistryFieldTransform<TState> transform)
    {
        var plan = _plans.Get(
            context.Rule,
            context.PayloadContext.Schema?.RuleSet?.HasFixedRuleCollections != true);
        var workspace = GetWorkspace();
        try
        {
            workspace.Reset(payload.Span, payload.Length + 128);
            var reader = new AvroReader(payload);
            TransformValue(_schema, target: false, plan, ref reader, workspace, context, state, transform);
            if (!workspace.MatchedTarget && !plan.HasTarget(context.Rule))
            {
                throw new SchemaRegistryRuleException(
                    $"Schema Registry rule '{context.Rule.Name}' did not match any Avro field tags.");
            }
            if (!reader.End)
            {
                throw new SchemaRegistryRuleException(
                    $"Schema Registry rule '{context.Rule.Name}' encountered trailing Avro payload bytes.");
            }

            return workspace.WrittenMemory;
        }
        finally
        {
            workspace.ReleaseConsumedOversizedOutput(payload.Span);
            workspace.ReleaseOversizedTemporary();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Workspace GetWorkspace() => t_workspace ??= new Workspace();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReleaseOversizedOutputs() => t_workspace?.ReleaseOversizedOutputs();

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
                if (target)
                    ThrowUnsupported(context, schema.Tag);
                CopyFixed(ref reader, output, 1);
                return;
            case AvroSchema.Type.Int:
            case AvroSchema.Type.Long:
            case AvroSchema.Type.Enumeration:
                if (target)
                    ThrowUnsupported(context, schema.Tag);
                CopyLong(ref reader, output);
                return;
            case AvroSchema.Type.Float:
                if (target)
                    ThrowUnsupported(context, schema.Tag);
                CopyFixed(ref reader, output, sizeof(float));
                return;
            case AvroSchema.Type.Double:
                if (target)
                    ThrowUnsupported(context, schema.Tag);
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
            var target = targets.IsTarget(field.Pos, context.Rule);
            output.MatchedTarget |= target;
            TransformValue(
                field.Schema,
                target,
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
        private readonly AvroSchema _schema;

        public SchemaTransformers(AvroSchema schema) => _schema = schema;

        public AvroTaggedFieldTransformer Get(RegistrySchema registrySchema, AvroSchema tagSchema)
        {
            if (_values.TryGetValue(registrySchema, out var transformer))
                return transformer;
            return GetSlow(registrySchema, tagSchema);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private AvroTaggedFieldTransformer GetSlow(RegistrySchema registrySchema, AvroSchema tagSchema) =>
            _values.GetValue(registrySchema, value =>
                new AvroTaggedFieldTransformer(_schema, tagSchema, value));
    }

    private sealed class RulePlanCache
    {
        private readonly AvroSchema _schema;
        private readonly AvroSchema _tagSchema;
        private readonly RegistrySchema _registrySchema;
        private readonly ConditionalWeakTable<SchemaRule, RulePlan> _fixedPlans = new();
        private readonly ConditionalWeakTable<SchemaRule, MutableRulePlan> _mutablePlans = new();
        private readonly ConditionalWeakTable<SchemaRule, RulePlan>.CreateValueCallback _createFixedPlan;
        private readonly ConditionalWeakTable<SchemaRule, MutableRulePlan>.CreateValueCallback _createMutablePlan;

        public RulePlanCache(
            AvroSchema schema,
            AvroSchema tagSchema,
            RegistrySchema registrySchema)
        {
            _schema = schema;
            _tagSchema = tagSchema;
            _registrySchema = registrySchema;
            _createFixedPlan = CreateFixedPlan;
            _createMutablePlan = CreateMutablePlan;
        }

        public RulePlan Get(SchemaRule rule, bool mutableTags) => mutableTags
            ? _mutablePlans.GetValue(rule, _createMutablePlan).Get()
            : _fixedPlans.GetValue(rule, _createFixedPlan);

        private RulePlan CreateFixedPlan(SchemaRule rule) =>
            RulePlan.Create(_schema, _tagSchema, _registrySchema, rule, mutableTags: false);

        private MutableRulePlan CreateMutablePlan(SchemaRule rule) =>
            new(_schema, _tagSchema, _registrySchema, rule);
    }

    private sealed class MutableRulePlan
    {
        private readonly object _gate = new();
        private readonly AvroSchema _schema;
        private readonly AvroSchema _tagSchema;
        private readonly RegistrySchema _registrySchema;
        private readonly SchemaRule _rule;
        private PlanState _state;

        public MutableRulePlan(
            AvroSchema schema,
            AvroSchema tagSchema,
            RegistrySchema registrySchema,
            SchemaRule rule)
        {
            _schema = schema;
            _tagSchema = tagSchema;
            _registrySchema = registrySchema;
            _rule = rule;
            _state = CreateState();
        }

        public RulePlan Get()
        {
            var state = Volatile.Read(ref _state);
            if (MetadataIsCurrent(state))
                return state.Plan;

            lock (_gate)
            {
                state = Volatile.Read(ref _state);
                if (!MetadataIsCurrent(state))
                {
                    state = CreateState();
                    Volatile.Write(ref _state, state);
                }

                return state.Plan;
            }
        }

        private bool MetadataIsCurrent(PlanState state)
        {
            var tags = _registrySchema.Metadata?.Tags;
            if (tags is null)
                return state.MetadataKind == MetadataTagsKind.None;
            if (tags.Count != state.MetadataCount)
                return false;

            return state.MetadataKind switch
            {
                MetadataTagsKind.Dictionary =>
                    tags is Dictionary<string, IReadOnlySet<string>> dictionary
                    && Volatile.Read(ref GetDictionaryVersion(dictionary)) == state.MetadataVersion,
                MetadataTagsKind.SortedDictionary =>
                    tags is SortedDictionary<string, IReadOnlySet<string>> && SortedMetadataIsCurrent(state),
                MetadataTagsKind.Immutable =>
                    tags is FrozenDictionary<string, IReadOnlySet<string>>
                        or IImmutableDictionary<string, IReadOnlySet<string>>,
                _ => false
            };
        }

        private PlanState CreateState()
        {
            var plan = RulePlan.Create(_schema, _tagSchema, _registrySchema, _rule, mutableTags: true);
            var tags = _registrySchema.Metadata?.Tags;
            if (tags is null)
                return new PlanState(plan, MetadataTagsKind.None, 0, 0, default);

            return tags switch
            {
                Dictionary<string, IReadOnlySet<string>> dictionary => new PlanState(
                    plan,
                    MetadataTagsKind.Dictionary,
                    tags.Count,
                    Volatile.Read(ref GetDictionaryVersion(dictionary)),
                    default),
                SortedDictionary<string, IReadOnlySet<string>> sortedDictionary => CreateSortedState(
                    plan,
                    sortedDictionary),
                FrozenDictionary<string, IReadOnlySet<string>> or
                    IImmutableDictionary<string, IReadOnlySet<string>> => new PlanState(
                        plan,
                        MetadataTagsKind.Immutable,
                        tags.Count,
                        0,
                        default),
                _ => throw new SchemaRegistryRuleException(
                    $"Caller-owned Avro CSFLE metadata tag map type '{tags.GetType().FullName}' cannot be tracked for mutation without a per-message scan. " +
                    "Use Dictionary<string, IReadOnlySet<string>>, SortedDictionary<string, IReadOnlySet<string>>, " +
                    "FrozenDictionary<string, IReadOnlySet<string>>, or IImmutableDictionary<string, IReadOnlySet<string>>.")
            };
        }

        private static PlanState CreateSortedState(
            RulePlan plan,
            SortedDictionary<string, IReadOnlySet<string>> tags)
        {
            var version = tags.GetEnumerator();
            while (version.MoveNext())
                _ = version.Current;
            return new PlanState(plan, MetadataTagsKind.SortedDictionary, tags.Count, 0, version);
        }

        private static bool SortedMetadataIsCurrent(PlanState state)
        {
            try
            {
                var version = state.SortedMetadataVersion;
                return !version.MoveNext();
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private sealed class PlanState(
            RulePlan plan,
            MetadataTagsKind metadataKind,
            int metadataCount,
            int metadataVersion,
            SortedDictionary<string, IReadOnlySet<string>>.Enumerator sortedMetadataVersion)
        {
            public RulePlan Plan { get; } = plan;
            public MetadataTagsKind MetadataKind { get; } = metadataKind;
            public int MetadataCount { get; } = metadataCount;
            public int MetadataVersion { get; } = metadataVersion;
            public SortedDictionary<string, IReadOnlySet<string>>.Enumerator SortedMetadataVersion { get; } =
                sortedMetadataVersion;
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
        public RecordTargets GetTargets(global::Avro.RecordSchema schema) => records[schema];

        public bool HasTarget(SchemaRule rule)
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

        public static RulePlan Create(
            AvroSchema schema,
            AvroSchema tagSchema,
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
                tagSchema,
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
            AvroSchema? tagSchema,
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
            if (tagSchema is global::Avro.LogicalSchema tagLogical)
                tagSchema = tagLogical.BaseSchema;

            if (!visited.Add(schema))
                return;

            switch (schema)
            {
                case global::Avro.RecordSchema record:
                    var tagRecord = tagSchema as global::Avro.RecordSchema;
                    var fields = record.Fields;
                    var targets = new bool[fields.Count];
                    MutableFieldTarget?[]? mutableRecordTargets = mutableTags
                        ? new MutableFieldTarget?[fields.Count]
                        : null;
                    records.Add(record, new RecordTargets(targets, mutableRecordTargets));
                    for (var i = 0; i < fields.Count; i++)
                    {
                        var field = fields[i];
                        var tagField = FindTagField(tagRecord, field.Name);
                        var fullName = tagField is null
                            ? record.Fullname + "." + field.Name
                            : tagRecord!.Fullname + "." + tagField.Name;
                        if (mutableTags)
                        {
                            var target = MutableFieldTarget.Create(
                                tagField,
                                metadata,
                                fullName,
                                rule);
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
                            var target = InlineTagsOverlap(tagField, rule.Tags!)
                                || MetadataTagsOverlap(metadata, fullName, rule.Tags!);
                            targets[field.Pos] = target;
                            hasTargets |= target;
                        }

                        Visit(
                            field.Schema,
                            tagField?.Schema,
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
                    Visit(
                        array.ItemSchema,
                        (tagSchema as global::Avro.ArraySchema)?.ItemSchema,
                        metadata,
                        rule,
                        mutableTags,
                        records,
                        mutableTargets,
                        visited,
                        ref hasTargets);
                    break;
                case global::Avro.MapSchema map:
                    Visit(
                        map.ValueSchema,
                        (tagSchema as global::Avro.MapSchema)?.ValueSchema,
                        metadata,
                        rule,
                        mutableTags,
                        records,
                        mutableTargets,
                        visited,
                        ref hasTargets);
                    break;
                case global::Avro.UnionSchema union:
                    var tagUnion = tagSchema as global::Avro.UnionSchema;
                    for (var i = 0; i < union.Count; i++)
                    {
                        Visit(
                            union[i],
                            FindUnionBranch(union[i], tagUnion),
                            metadata,
                            rule,
                            mutableTags,
                            records,
                            mutableTargets,
                            visited,
                            ref hasTargets);
                    }
                    break;
            }
        }

        private static global::Avro.Field? FindTagField(
            global::Avro.RecordSchema? record,
            string payloadFieldName)
        {
            if (record is null)
                return null;
            if (record.TryGetField(payloadFieldName, out var field))
                return field;

            var fields = record.Fields;
            for (var i = 0; i < fields.Count; i++)
            {
                var candidate = fields[i];
                var aliases = candidate.Aliases;
                if (aliases is null)
                    continue;
                for (var aliasIndex = 0; aliasIndex < aliases.Count; aliasIndex++)
                {
                    if (string.Equals(aliases[aliasIndex], payloadFieldName, StringComparison.Ordinal))
                        return candidate;
                }
            }

            return null;
        }

        private static AvroSchema? FindUnionBranch(
            AvroSchema branch,
            global::Avro.UnionSchema? tagUnion)
        {
            if (tagUnion is null)
                return null;

            for (var i = 0; i < tagUnion.Count; i++)
            {
                var candidate = tagUnion[i];
                if (branch.Tag != candidate.Tag)
                    continue;
                if (branch is global::Avro.NamedSchema namedBranch &&
                    candidate is global::Avro.NamedSchema namedCandidate &&
                    !NamedSchemaMatches(namedBranch, namedCandidate))
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static bool NamedSchemaMatches(
            global::Avro.NamedSchema payloadBranch,
            global::Avro.NamedSchema ruleOwnerBranch)
        {
            if (string.Equals(payloadBranch.Fullname, ruleOwnerBranch.Fullname, StringComparison.Ordinal))
                return true;

            var aliases = AvroSchemaLogicalAccessors.GetAliases(ruleOwnerBranch);
            if (aliases is null)
                return false;
            for (var i = 0; i < aliases.Count; i++)
            {
                if (string.Equals(payloadBranch.Fullname, aliases[i].Fullname, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool InlineTagsOverlap(global::Avro.Field? field, IReadOnlySet<string> ruleTags)
        {
            var tagsJson = field?.GetProperty("confluent:tags");
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
        private readonly object _gate = new();
        private readonly int[] _metadataVersions = CaptureVersions(metadataTags);
        private readonly int[] _pendingMetadataVersions = new int[metadataTags.Length];
        private int _ruleTagsVersion = GetSetVersion(rule.Tags!);
        private int _isTarget =
            TagsOverlap(inlineTags, rule.Tags!) || MetadataTagsOverlap(metadataTags, rule.Tags!) ? 1 : 0;

        public bool IsTarget => Volatile.Read(ref _isTarget) != 0;

        public static MutableFieldTarget? Create(
            global::Avro.Field? field,
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

        public bool Refresh(SchemaRule rule)
        {
            var ruleTags = rule.Tags!;
            var ruleVersion = GetSetVersion(ruleTags);
            if (VersionsAreCurrent(ruleVersion))
                return IsTarget;

            lock (_gate)
            {
                ruleVersion = GetSetVersion(ruleTags);
                if (VersionsAreCurrent(ruleVersion))
                    return IsTarget;

                for (var i = 0; i < metadataTags.Length; i++)
                    _pendingMetadataVersions[i] = GetSetVersion(metadataTags[i]);

                var current = TagsOverlap(inlineTags, ruleTags) || MetadataTagsOverlap(metadataTags, ruleTags);
                Volatile.Write(ref _isTarget, current ? 1 : 0);
                Volatile.Write(ref _ruleTagsVersion, ruleVersion);
                for (var i = 0; i < metadataTags.Length; i++)
                    Volatile.Write(ref _metadataVersions[i], _pendingMetadataVersions[i]);

                return current;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool VersionsAreCurrent(int ruleVersion)
        {
            if (ruleVersion != Volatile.Read(ref _ruleTagsVersion))
                return false;
            for (var i = 0; i < metadataTags.Length; i++)
            {
                if (GetSetVersion(metadataTags[i]) != Volatile.Read(ref _metadataVersions[i]))
                    return false;
            }

            return true;
        }

        private static string[] ReadInlineTags(global::Avro.Field? field)
        {
            var tagsJson = field?.GetProperty("confluent:tags");
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
        private int _oversizedOutputMask;
        private int _length;
        private int _temporaryLength;

        public bool MatchedTarget { get; set; }

        public ReadOnlyMemory<byte> WrittenMemory
        {
            get
            {
                var output = _outputs[_outputSlot]!;
                return new ReadOnlyMemory<byte>(output, 0, _length);
            }
        }

        public void Reset(ReadOnlySpan<byte> input, int minimumCapacity)
        {
            if (_oversizedOutputMask != 0)
                ReleaseInactiveOversizedOutputs(input);
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
            MatchedTarget = false;
            EnsureOutput(minimumCapacity);
        }

        private void ReleaseInactiveOversizedOutputs(ReadOnlySpan<byte> input)
        {
            for (var i = 0; i < _outputs.Length; i++)
            {
                var slotMask = 1 << i;
                if ((_oversizedOutputMask & slotMask) == 0)
                    continue;
                var output = _outputs[i];
                if (input.Overlaps(output))
                    continue;

                _outputs[i] = null;
                _outputLengths[i] = 0;
                _oversizedOutputMask &= ~slotMask;
                ArrayPool<byte>.Shared.Return(output!, clearArray: true);
            }
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

        public void ReleaseOversizedTemporary()
        {
            var temporary = _temporary;
            if (temporary is null || temporary.Length <= MaxRetainedBufferSize)
                return;

            _temporary = null;
            _temporaryLength = 0;
            ArrayPool<byte>.Shared.Return(temporary, clearArray: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReleaseConsumedOversizedOutput(ReadOnlySpan<byte> input)
        {
            if (_oversizedOutputMask != 0)
                ReleaseConsumedOversizedOutputSlow(input);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReleaseOversizedOutputs()
        {
            if (_oversizedOutputMask != 0)
                ReleaseOversizedOutputsSlow();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ReleaseOversizedOutputsSlow()
        {
            for (var i = 0; i < _outputs.Length; i++)
            {
                var slotMask = 1 << i;
                if ((_oversizedOutputMask & slotMask) == 0)
                    continue;

                var output = _outputs[i]!;
                _outputs[i] = null;
                _outputLengths[i] = 0;
                ArrayPool<byte>.Shared.Return(output, clearArray: true);
            }

            _oversizedOutputMask = 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ReleaseConsumedOversizedOutputSlow(ReadOnlySpan<byte> input)
        {
            for (var i = 0; i < _outputs.Length; i++)
            {
                var slotMask = 1 << i;
                if ((_oversizedOutputMask & slotMask) == 0)
                    continue;
                var output = _outputs[i];
                if (!input.Overlaps(output))
                    continue;

                _outputs[i] = null;
                _outputLengths[i] = 0;
                _oversizedOutputMask &= ~slotMask;
                ArrayPool<byte>.Shared.Return(output!, clearArray: true);
            }
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
            var slotMask = 1 << _outputSlot;
            if (replacement.Length > MaxRetainedBufferSize)
                _oversizedOutputMask |= slotMask;
            else
                _oversizedOutputMask &= ~slotMask;
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
    private readonly ConditionalWeakTable<AvroSchema, SerializerPayloadTransformers> _serializerPayloads = new();
    private readonly ConditionalWeakTable<RegistrySchema, PayloadSchemaTransformers> _payloadSchemas = new();
    private SerializerTransformerEntry? _lastSerializerTransformer;
    private PayloadSchemaTransformers? _lastPayloadSchema;

    public ISchemaRegistryTaggedFieldTransformer Get(
        RegistrySchema payloadSchema,
        RegistrySchema? ruleOwnerSchema = null)
    {
        var payloadTransformers = Volatile.Read(ref _lastPayloadSchema);
        if (payloadTransformers is null ||
            !ReferenceEquals(payloadTransformers.PayloadSchema, payloadSchema))
        {
            payloadTransformers = _payloadSchemas.GetValue(
                payloadSchema,
                static value => new PayloadSchemaTransformers(value));
            Volatile.Write(ref _lastPayloadSchema, payloadTransformers);
        }

        return payloadTransformers.Get(ruleOwnerSchema ?? payloadSchema);
    }

    internal AvroTaggedFieldTransformer Get(RegistrySchema schema, AvroSchema avroSchema)
    {
        var entry = Volatile.Read(ref _lastSerializerTransformer);
        if (entry is not null &&
            ReferenceEquals(entry.Schema, schema) &&
            ReferenceEquals(entry.PayloadSchema, avroSchema))
        {
            return entry.Transformer;
        }

        entry = _serializerPayloads.GetValue(
                avroSchema,
                static value => new SerializerPayloadTransformers(value))
            .Get(schema);
        Volatile.Write(ref _lastSerializerTransformer, entry);
        return entry.Transformer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReleaseOversizedOutputs() => AvroTaggedFieldTransformer.ReleaseOversizedOutputs();

    private sealed class SerializerPayloadTransformers
    {
        private readonly ConditionalWeakTable<RegistrySchema, SerializerTransformerEntry> _owners = new();
        private readonly ConditionalWeakTable<RegistrySchema, SerializerTransformerEntry>.CreateValueCallback _create;
        private readonly AvroSchema _payloadSchema;
        private SerializerTransformerEntry? _lastOwner;

        public SerializerPayloadTransformers(AvroSchema payloadSchema)
        {
            _payloadSchema = payloadSchema;
            _create = Create;
        }

        public SerializerTransformerEntry Get(RegistrySchema owner)
        {
            var entry = Volatile.Read(ref _lastOwner);
            if (entry is null || !ReferenceEquals(entry.Schema, owner))
            {
                entry = _owners.GetValue(owner, _create);
                Volatile.Write(ref _lastOwner, entry);
            }

            return entry;
        }

        private SerializerTransformerEntry Create(RegistrySchema owner) => new(
            owner,
            _payloadSchema,
            AvroTaggedFieldTransformer.Get(
                _payloadSchema,
                owner,
                AvroSchema.Parse(owner.SchemaString)));
    }

    private sealed class PayloadSchemaTransformers
    {
        private readonly AvroSchema _payloadSchema;
        private readonly ConditionalWeakTable<RegistrySchema, TransformerEntry> _owners = new();
        private readonly ConditionalWeakTable<RegistrySchema, TransformerEntry>.CreateValueCallback _create;
        private TransformerEntry? _lastOwner;

        public PayloadSchemaTransformers(RegistrySchema payloadSchema)
        {
            PayloadSchema = payloadSchema;
            _payloadSchema = AvroSchema.Parse(payloadSchema.SchemaString);
            _create = Create;
        }

        public RegistrySchema PayloadSchema { get; }

        public AvroTaggedFieldTransformer Get(RegistrySchema owner)
        {
            var entry = Volatile.Read(ref _lastOwner);
            if (entry is null || !ReferenceEquals(entry.Schema, owner))
            {
                entry = _owners.GetValue(owner, _create);
                Volatile.Write(ref _lastOwner, entry);
            }

            return entry.Transformer;
        }

        private TransformerEntry Create(RegistrySchema owner) => new(
            owner,
            AvroTaggedFieldTransformer.Get(
                _payloadSchema,
                owner,
                AvroSchema.Parse(owner.SchemaString)));
    }

    private sealed class TransformerEntry(
        RegistrySchema schema,
        AvroTaggedFieldTransformer transformer)
    {
        public RegistrySchema Schema { get; } = schema;

        public AvroTaggedFieldTransformer Transformer { get; } = transformer;
    }

    private sealed class SerializerTransformerEntry(
        RegistrySchema schema,
        AvroSchema payloadSchema,
        AvroTaggedFieldTransformer transformer)
    {
        public RegistrySchema Schema { get; } = schema;

        public AvroSchema PayloadSchema { get; } = payloadSchema;

        public AvroTaggedFieldTransformer Transformer { get; } = transformer;
    }
}
