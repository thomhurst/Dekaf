using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AvroSchema = global::Avro.Schema;

namespace Dekaf.SchemaRegistry.Avro;

internal sealed class AvroInlineRuleValidator
{
    [ThreadStatic]
    private static char[]? t_pathBuffer;

    private readonly AvroValueRulePlan _root;

    internal AvroInlineRuleValidator(AvroSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var plans = new Dictionary<AvroSchema, AvroValueRulePlan>(AvroSchemaReferenceComparer.Instance);
        _root = AvroValueRulePlan.Create(schema, plans);
        AvroValueRulePlan.Complete(plans);
    }

    internal void Validate(ReadOnlyMemory<byte> payload, int schemaId, bool failFast)
    {
        List<ValidationRuleError>? violations = null;
        var path = new AvroValidationPath(t_pathBuffer ??= new char[256]);
        var reader = new AvroValidationReader(payload);
        try
        {
            _root.Validate(
                ref reader,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                failFast,
                ref violations,
                ref path);
            if (!reader.End)
                throw InvalidPayload("trailing bytes remain after the root value");
        }
        finally
        {
            path.Dispose();
        }

        if (violations is not null)
            throw new ValidationRulesFailedException(violations);
    }

    private static SchemaRegistryRuleException InvalidPayload(string reason) =>
        new($"Could not evaluate Avro validation rules: {reason}.");
}

internal sealed class AvroValueRulePlan
{
    private readonly AvroSchema _schema;
    private AvroCompiledRuleSet _schemaRules = AvroCompiledRuleSet.Empty;
    private AvroFieldRulePlan[] _fields = [];
    private AvroValueRulePlan[] _children = [];

    private AvroValueRulePlan(AvroSchema schema) => _schema = Unwrap(schema);

    internal bool HasAnyRules { get; private set; }

    internal static AvroValueRulePlan Create(
        AvroSchema schema,
        Dictionary<AvroSchema, AvroValueRulePlan> plans)
    {
        schema = Unwrap(schema);
        if (plans.TryGetValue(schema, out var existing))
            return existing;

        var plan = new AvroValueRulePlan(schema);
        plans.Add(schema, plan);
        plan.Initialize(plans);
        return plan;
    }

    private void Initialize(Dictionary<AvroSchema, AvroValueRulePlan> plans)
    {
        _schemaRules = AvroCompiledRuleSet.Compile(
            AvroInlineRuleParser.ReadRules(_schema),
            _schema as global::Avro.RecordSchema);

        switch (_schema)
        {
            case global::Avro.RecordSchema record:
                _fields = new AvroFieldRulePlan[record.Fields.Count];
                for (var index = 0; index < record.Fields.Count; index++)
                {
                    var field = record.Fields[index];
                    var fieldSchema = Unwrap(field.Schema);
                    var child = Create(fieldSchema, plans);
                    _fields[index] = new AvroFieldRulePlan(
                        field,
                        AvroCompiledRuleSet.Compile(
                            AvroInlineRuleParser.ReadRules(field),
                            FindRecord(fieldSchema)),
                        child);
                }
                break;
            case global::Avro.ArraySchema array:
                _children = [Create(array.ItemSchema, plans)];
                break;
            case global::Avro.MapSchema map:
                _children = [Create(map.ValueSchema, plans)];
                break;
            case global::Avro.UnionSchema union:
                _children = new AvroValueRulePlan[union.Count];
                for (var index = 0; index < union.Count; index++)
                    _children[index] = Create(union[index], plans);
                break;
        }

        HasAnyRules = !_schemaRules.IsEmpty;
        for (var index = 0; index < _fields.Length; index++)
            HasAnyRules |= !_fields[index].Rules.IsEmpty;
    }

    internal static void Complete(Dictionary<AvroSchema, AvroValueRulePlan> plans)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var plan in plans.Values)
            {
                if (plan.HasAnyRules)
                    continue;
                for (var index = 0; index < plan._fields.Length; index++)
                {
                    if (plan._fields[index].Child.HasAnyRules)
                    {
                        plan.HasAnyRules = true;
                        changed = true;
                        break;
                    }
                }
                for (var index = 0; !plan.HasAnyRules && index < plan._children.Length; index++)
                {
                    if (plan._children[index].HasAnyRules)
                    {
                        plan.HasAnyRules = true;
                        changed = true;
                    }
                }
            }
        }
    }

    internal ValidationCelValue Validate(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        if (!HasAnyRules)
            return AvroValidationValueDecoder.Read(_schema, ref reader);

        var start = reader.Position;
        var preview = reader;
        var value = AvroValidationValueDecoder.Read(_schema, ref preview);
        var payload = preview.Source.Slice(start, preview.Position - start);
        var rootSize = value.SizeIndex == 0 ? AvroValidationValueDecoder.Count(_schema, payload) : -1;
        if (!_schemaRules.IsEmpty)
        {
            _schemaRules.Evaluate(
                value,
                payload,
                now,
                failFast,
                ref violations,
                ref path,
                rootSize);
            if (failFast && violations is not null)
            {
                reader = preview;
                return value;
            }
        }

        switch (_schema)
        {
            case global::Avro.RecordSchema:
                ValidateRecord(ref reader, now, failFast, ref violations, ref path);
                break;
            case global::Avro.ArraySchema:
                ValidateArray(ref reader, now, failFast, ref violations, ref path);
                break;
            case global::Avro.MapSchema:
                ValidateMap(ref reader, now, failFast, ref violations, ref path);
                break;
            case global::Avro.UnionSchema:
                ValidateUnion(ref reader, now, failFast, ref violations, ref path);
                break;
            default:
                reader = preview;
                break;
        }
        return value;
    }

    private void ValidateRecord(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        for (var index = 0; index < _fields.Length; index++)
        {
            var field = _fields[index];
            var mark = path.Length;
            path.AppendField(field.Field.Name);
            var fieldStart = reader.Position;
            var preview = reader;
            var value = AvroValidationValueDecoder.Read(field.Field.Schema, ref preview);
            var payload = preview.Source.Slice(fieldStart, preview.Position - fieldStart);
            if (!field.Rules.IsEmpty)
            {
                field.Rules.Evaluate(
                    value,
                    payload,
                    now,
                    failFast,
                    ref violations,
                    ref path,
                    value.SizeIndex == 0
                        ? AvroValidationValueDecoder.Count(field.Field.Schema, payload)
                        : -1);
            }

            if (!(failFast && violations is not null))
            {
                _ = field.Child.Validate(
                    ref reader,
                    now,
                    failFast,
                    ref violations,
                    ref path);
            }
            else
            {
                reader = preview;
            }
            path.Truncate(mark);
            if (failFast && violations is not null)
            {
                for (var remaining = index + 1; remaining < _fields.Length; remaining++)
                    AvroValidationValueDecoder.Skip(_fields[remaining].Field.Schema, ref reader);
                return;
            }
        }
    }

    private void ValidateUnion(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        var union = (global::Avro.UnionSchema)_schema;
        var branch = reader.ReadLong();
        if ((ulong)branch >= (ulong)union.Count)
            throw InvalidPayload($"invalid union index {branch}");
        _ = _children[(int)branch].Validate(
            ref reader,
            now,
            failFast,
            ref violations,
            ref path);
    }

    private void ValidateArray(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        var item = _children[0];
        var itemIndex = 0;
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return;
            for (long index = 0; index < count; index++)
            {
                var mark = path.Length;
                path.AppendIndex(itemIndex++);
                _ = item.Validate(ref reader, now, failFast, ref violations, ref path);
                path.Truncate(mark);
                if (failFast && violations is not null)
                {
                    for (var remaining = index + 1; remaining < count; remaining++)
                        AvroValidationValueDecoder.Skip(item._schema, ref reader);
                    SkipRemainingCollection(item._schema, ref reader);
                    return;
                }
            }
        }
    }

    private void ValidateMap(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        var valuePlan = _children[0];
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return;
            for (long index = 0; index < count; index++)
            {
                var key = reader.ReadLengthPrefixed();
                var mark = path.Length;
                path.AppendMapKey(key.Span);
                _ = valuePlan.Validate(ref reader, now, failFast, ref violations, ref path);
                path.Truncate(mark);
                if (failFast && violations is not null)
                {
                    for (var remaining = index + 1; remaining < count; remaining++)
                    {
                        _ = reader.ReadLengthPrefixed();
                        AvroValidationValueDecoder.Skip(valuePlan._schema, ref reader);
                    }
                    SkipRemainingMap(valuePlan._schema, ref reader);
                    return;
                }
            }
        }
    }

    private static void SkipRemainingCollection(AvroSchema item, ref AvroValidationReader reader)
    {
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return;
            for (long index = 0; index < count; index++)
                AvroValidationValueDecoder.Skip(item, ref reader);
        }
    }

    private static void SkipRemainingMap(AvroSchema value, ref AvroValidationReader reader)
    {
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return;
            for (long index = 0; index < count; index++)
            {
                _ = reader.ReadLengthPrefixed();
                AvroValidationValueDecoder.Skip(value, ref reader);
            }
        }
    }

    internal static AvroSchema Unwrap(AvroSchema schema) =>
        schema is global::Avro.LogicalSchema logical ? logical.BaseSchema : schema;

    internal static global::Avro.RecordSchema? FindRecord(AvroSchema schema)
    {
        schema = Unwrap(schema);
        if (schema is global::Avro.RecordSchema record)
            return record;
        if (schema is not global::Avro.UnionSchema union)
            return null;
        global::Avro.RecordSchema? result = null;
        for (var index = 0; index < union.Count; index++)
        {
            var candidate = FindRecord(union[index]);
            if (candidate is null)
                continue;
            if (result is not null && !ReferenceEquals(result, candidate))
                return null;
            result = candidate;
        }
        return result;
    }

    private static SchemaRegistryRuleException InvalidPayload(string reason) =>
        new($"Could not evaluate Avro validation rules: {reason}.");
}

internal sealed record AvroFieldRulePlan(
    global::Avro.Field Field,
    AvroCompiledRuleSet Rules,
    AvroValueRulePlan Child);

internal sealed class AvroCompiledRuleSet
{
    internal static AvroCompiledRuleSet Empty { get; } = new([], null, false, false, 0);

    private readonly CompiledValidationRule[] _rules;
    private readonly AvroMemberResolver? _members;
    private readonly bool _usesCachedEquality;
    private readonly int _memberCount;

    private AvroCompiledRuleSet(
        CompiledValidationRule[] rules,
        AvroMemberResolver? members,
        bool usesSize,
        bool usesCachedEquality,
        int memberCount)
    {
        _rules = rules;
        _members = members;
        UsesSize = usesSize;
        _usesCachedEquality = usesCachedEquality;
        _memberCount = memberCount;
    }

    internal bool IsEmpty => _rules.Length == 0;
    internal bool UsesSize { get; }

    internal static AvroCompiledRuleSet Compile(
        IReadOnlyList<ValidationRule> rules,
        global::Avro.RecordSchema? valueSchema)
    {
        if (rules.Count == 0)
            return Empty;

        var memberIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var memberPaths = new List<byte[][]>();
        var usedMemberIndexes = new HashSet<int>();
        var compiled = new CompiledValidationRule[rules.Count];
        var usesSize = false;
        var usesCachedEquality = false;
        for (var index = 0; index < rules.Count; index++)
        {
            var rule = CompiledValidationRule.Compile(
                rules[index],
                memberIndexes,
                memberPaths,
                usedMemberIndexes);
            compiled[index] = rule;
            usesSize |= rule.UsesSize;
            usesCachedEquality |= rule.UsesCachedEquality;
        }

        var members = usedMemberIndexes.Count == 0 || valueSchema is null
            ? null
            : AvroMemberResolver.Create(valueSchema, memberPaths, usedMemberIndexes);
        return new AvroCompiledRuleSet(
            compiled,
            members,
            usesSize,
            usesCachedEquality,
            memberPaths.Count);
    }

    internal void Evaluate(
        ValidationCelValue value,
        ReadOnlyMemory<byte> payload,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path,
        int rootSize = -1)
    {
        var memberValues = _memberCount == 0
            ? default
            : CompiledValidationRule.GetMemberValues(_memberCount);
        var sizes = UsesSize || _members is not null
            ? CompiledValidationRule.GetSizeValues(_memberCount + 1)
            : default;
        if (rootSize >= 0)
            sizes.Set(0, rootSize);
        _members?.Resolve(payload, memberValues, sizes);
        var equalityGeneration = _usesCachedEquality
            ? CompiledValidationRule.BeginEqualityResolution()
            : 0;

        ValidationCelStrings.Begin(_memberCount + 1, payload.Length);
        try
        {
            for (var index = 0; index < _rules.Length; index++)
            {
                var rule = _rules[index];
                try
                {
                    var result = rule.Evaluate(value, now, memberValues, sizes, equalityGeneration);
                    if (result.Kind == ValidationResultKind.Boolean ? result.Boolean : result.String!.Length == 0)
                        continue;
                    (violations ??= []).Add(new ValidationRuleError(
                        rule.Rule,
                        path.ToString(),
                        result.Kind == ValidationResultKind.String ? result.String : null));
                }
                catch (SchemaRegistryRuleException exception)
                {
                    (violations ??= []).Add(new ValidationRuleError(
                        rule.Rule,
                        path.ToString(),
                        cause: exception));
                }

                if (failFast)
                    return;
            }
        }
        finally
        {
            ValidationCelStrings.End();
        }
    }
}

internal sealed class AvroMemberResolver
{
    private readonly AvroMemberNode?[] _fields;

    private AvroMemberResolver(global::Avro.RecordSchema schema) =>
        _fields = new AvroMemberNode?[schema.Fields.Count];

    internal static AvroMemberResolver Create(
        global::Avro.RecordSchema schema,
        IReadOnlyList<byte[][]> paths,
        IReadOnlyCollection<int> usedIndexes)
    {
        var resolver = new AvroMemberResolver(schema);
        resolver.SetSchemas(schema);
        foreach (var memberIndex in usedIndexes)
            resolver.Add(schema, paths[memberIndex], memberIndex, depth: 0);
        return resolver;
    }

    private void Add(
        global::Avro.RecordSchema schema,
        byte[][] path,
        int memberIndex,
        int depth)
    {
        var name = Encoding.UTF8.GetString(path[depth]);
        var field = FindField(schema, name)
            ?? throw new SchemaRegistryRuleException(
                $"Avro validation rule refers to unknown field '{name}' on '{schema.Fullname}'.");
        var node = _fields[field.Pos] ??= new AvroMemberNode();
        if (depth == path.Length - 1)
        {
            node.MemberIndex = memberIndex;
            return;
        }
        node.AddChild(field.Schema, path, memberIndex, depth + 1);
    }

    internal void Resolve(
        ReadOnlyMemory<byte> payload,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        var reader = new AvroValidationReader(payload);
        for (var index = 0; index < _fields.Length; index++)
        {
            var node = _fields[index];
            var schema = node?.Schema;
            if (schema is null)
                throw new InvalidOperationException("Avro validation member resolver is incomplete.");
            if (node!.HasTargets)
            {
                var start = reader.Position;
                AvroValidationValueDecoder.Skip(schema, ref reader);
                node.Resolve(
                    reader.Source.Slice(start, reader.Position - start),
                    values,
                    sizes);
            }
            else
            {
                AvroValidationValueDecoder.Skip(schema, ref reader);
            }
        }
    }

    private static global::Avro.Field? FindField(global::Avro.RecordSchema schema, string name)
    {
        for (var index = 0; index < schema.Fields.Count; index++)
        {
            var field = schema.Fields[index];
            if (string.Equals(field.Name, name, StringComparison.Ordinal))
                return field;
            var aliases = field.Aliases;
            if (aliases is null)
                continue;
            for (var aliasIndex = 0; aliasIndex < aliases.Count; aliasIndex++)
            {
                if (string.Equals(aliases[aliasIndex], name, StringComparison.Ordinal))
                    return field;
            }
        }
        return null;
    }

    internal void SetSchemas(global::Avro.RecordSchema schema)
    {
        for (var index = 0; index < schema.Fields.Count; index++)
            (_fields[index] ??= new AvroMemberNode()).Schema = schema.Fields[index].Schema;
    }

    private sealed class AvroMemberNode
    {
        private readonly Dictionary<AvroSchema, AvroMemberResolver> _children =
            new(AvroSchemaReferenceComparer.Instance);

        internal AvroSchema? Schema { get; set; }
        internal int MemberIndex { get; set; } = -1;
        internal bool HasTargets => MemberIndex >= 0 || _children.Count != 0;

        internal void AddChild(
            AvroSchema schema,
            byte[][] path,
            int memberIndex,
            int depth)
        {
            schema = AvroValueRulePlan.Unwrap(schema);
            if (schema is global::Avro.UnionSchema union)
            {
                var found = false;
                var nextName = Encoding.UTF8.GetString(path[depth]);
                for (var index = 0; index < union.Count; index++)
                {
                    if (AvroValueRulePlan.Unwrap(union[index]) is not global::Avro.RecordSchema branch)
                        continue;
                    if (FindField(branch, nextName) is null)
                        continue;
                    AddRecordChild(branch, path, memberIndex, depth);
                    found = true;
                }
                if (found)
                    return;
            }
            else if (schema is global::Avro.RecordSchema record)
            {
                AddRecordChild(record, path, memberIndex, depth);
                return;
            }

            throw new SchemaRegistryRuleException(
                "Avro validation member path can only descend through records or record unions.");
        }

        private void AddRecordChild(
            global::Avro.RecordSchema record,
            byte[][] path,
            int memberIndex,
            int depth)
        {
            if (!_children.TryGetValue(record, out var child))
            {
                child = new AvroMemberResolver(record);
                child.SetSchemas(record);
                _children.Add(record, child);
            }
            child.Add(record, path, memberIndex, depth);
        }

        internal void Resolve(
            ReadOnlyMemory<byte> payload,
            ValidationCelMemberValues values,
            ValidationCelSizeValues sizes)
        {
            var schema = Schema!;
            if (MemberIndex >= 0)
            {
                var valueReader = new AvroValidationReader(payload);
                var value = AvroValidationValueDecoder.Read(schema, ref valueReader);
                if (value.SizeIndex == 0)
                {
                    var sizeIndex = MemberIndex + 1;
                    sizes.Set(sizeIndex, AvroValidationValueDecoder.Count(schema, payload));
                    value = value with { SizeIndex = sizeIndex };
                }
                values.SetValue(MemberIndex, value);
            }

            if (_children.Count == 0)
                return;
            var childReader = new AvroValidationReader(payload);
            schema = AvroValueRulePlan.Unwrap(schema);
            if (schema is global::Avro.UnionSchema union)
            {
                var branch = childReader.ReadLong();
                if ((ulong)branch >= (ulong)union.Count)
                    throw new SchemaRegistryRuleException(
                        $"Could not evaluate Avro validation rules: invalid union index {branch}.");
                schema = AvroValueRulePlan.Unwrap(union[(int)branch]);
            }
            if (schema is global::Avro.RecordSchema record && _children.TryGetValue(record, out var child))
            {
                child.Resolve(
                    childReader.Source.Slice(childReader.Position),
                    values,
                    sizes);
            }
        }
    }
}

internal static class AvroInlineRuleParser
{
    internal static IReadOnlyList<ValidationRule> ReadRules(AvroSchema schema) =>
        ReadRules(schema.GetProperty("confluent:rules"), "schema");

    internal static IReadOnlyList<ValidationRule> ReadRules(global::Avro.Field field) =>
        ReadRules(field.GetProperty("confluent:rules"), $"field '{field.Name}'");

    private static IReadOnlyList<ValidationRule> ReadRules(string? json, string owner)
    {
        if (string.IsNullOrEmpty(json))
            return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw InvalidDeclaration($"Avro {owner} 'confluent:rules' must be an array");
            var rules = new List<ValidationRule>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    throw InvalidDeclaration($"Avro {owner} 'confluent:rules' entries must be objects");
                rules.Add(new ValidationRule
                {
                    Name = GetOptionalString(element, "name"),
                    Doc = GetOptionalString(element, "doc"),
                    Expr = GetOptionalString(element, "expr"),
                    Sql = GetOptionalString(element, "sql")
                });
            }
            return rules;
        }
        catch (JsonException exception)
        {
            throw new SchemaRegistryRuleException(
                $"Could not parse Avro {owner} 'confluent:rules'.",
                exception);
        }
    }

    private static string? GetOptionalString(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static SchemaRegistryRuleException InvalidDeclaration(string message) => new(message + ".");
}

internal ref struct AvroValidationReader(ReadOnlyMemory<byte> source)
{
    internal ReadOnlyMemory<byte> Source { get; } = source;
    internal int Position { get; private set; }
    internal bool End => Position == Source.Length;

    internal ReadOnlyMemory<byte> Read(int length)
    {
        if (length < 0 || length > Source.Length - Position)
            throw InvalidPayload("payload ended before the value completed");
        var result = Source.Slice(Position, length);
        Position += length;
        return result;
    }

    internal long ReadLong()
    {
        ulong encoded = 0;
        for (var shift = 0; shift < 70; shift += 7)
        {
            if (Position >= Source.Length)
                throw InvalidPayload("payload ended inside a variable-length integer");
            var current = Source.Span[Position++];
            if (shift == 63 && current > 1)
                throw InvalidPayload("variable-length integer is invalid");
            encoded |= (ulong)(current & 0x7f) << shift;
            if ((current & 0x80) == 0)
                return (long)(encoded >> 1) ^ -((long)encoded & 1);
        }
        throw InvalidPayload("variable-length integer is invalid");
    }

    internal int ReadLength()
    {
        var length = ReadLong();
        if ((ulong)length > int.MaxValue)
            throw InvalidPayload($"length {length} is invalid");
        return (int)length;
    }

    internal ReadOnlyMemory<byte> ReadLengthPrefixed() => Read(ReadLength());

    internal long ReadCollectionCount()
    {
        var count = ReadLong();
        if (count >= 0)
            return count;
        if (count == long.MinValue)
            throw InvalidPayload("collection block count is invalid");
        count = -count;
        var byteCount = ReadLong();
        if (byteCount < 0 || byteCount > Source.Length - Position)
            throw InvalidPayload("collection block byte count is invalid");
        return count;
    }

    private static SchemaRegistryRuleException InvalidPayload(string reason) =>
        new($"Could not evaluate Avro validation rules: {reason}.");
}

internal static class AvroValidationValueDecoder
{
    internal static ValidationCelValue Read(AvroSchema schema, ref AvroValidationReader reader)
    {
        schema = AvroValueRulePlan.Unwrap(schema);
        switch (schema.Tag)
        {
            case AvroSchema.Type.Null:
                return ValidationCelValue.Null;
            case AvroSchema.Type.Boolean:
                return ValidationCelValue.FromBoolean(reader.Read(1).Span[0] != 0);
            case AvroSchema.Type.Int:
            case AvroSchema.Type.Long:
            case AvroSchema.Type.Enumeration:
                return ValidationCelValue.FromNumber(reader.ReadLong());
            case AvroSchema.Type.Float:
                return ValidationCelValue.FromFloating(
                    BinaryPrimitives.ReadSingleLittleEndian(reader.Read(sizeof(float)).Span));
            case AvroSchema.Type.Double:
                return ValidationCelValue.FromFloating(
                    BinaryPrimitives.ReadDoubleLittleEndian(reader.Read(sizeof(double)).Span));
            case AvroSchema.Type.String:
                return ValidationCelValue.FromUtf8String(reader.ReadLengthPrefixed());
            case AvroSchema.Type.Bytes:
                return ValidationCelValue.FromBytes(reader.ReadLengthPrefixed());
            case AvroSchema.Type.Fixed:
                return ValidationCelValue.FromBytes(reader.Read(((global::Avro.FixedSchema)schema).Size));
            case AvroSchema.Type.Record:
            case AvroSchema.Type.Error:
                var recordStart = reader.Position;
                var record = (global::Avro.RecordSchema)schema;
                for (var index = 0; index < record.Fields.Count; index++)
                    Skip(record.Fields[index].Schema, ref reader);
                return new ValidationCelValue(
                    ValidationCelValueKind.Object,
                    default,
                    false,
                    0,
                    null,
                    reader.Source.Slice(recordStart, reader.Position - recordStart));
            case AvroSchema.Type.Array:
                SkipCollection(((global::Avro.ArraySchema)schema).ItemSchema, isMap: false, ref reader);
                return ValidationCelValue.FromCollection(ValidationCelValueKind.Array, 0);
            case AvroSchema.Type.Map:
                SkipCollection(((global::Avro.MapSchema)schema).ValueSchema, isMap: true, ref reader);
                return ValidationCelValue.FromCollection(ValidationCelValueKind.Object, 0);
            case AvroSchema.Type.Union:
                var union = (global::Avro.UnionSchema)schema;
                var branch = reader.ReadLong();
                if ((ulong)branch >= (ulong)union.Count)
                    throw InvalidPayload($"invalid union index {branch}");
                return Read(union[(int)branch], ref reader);
            default:
                throw InvalidPayload($"unsupported schema type {schema.Tag}");
        }
    }

    internal static void Skip(AvroSchema schema, ref AvroValidationReader reader) =>
        _ = Read(schema, ref reader);

    internal static int Count(AvroSchema schema, ReadOnlyMemory<byte> payload)
    {
        schema = AvroValueRulePlan.Unwrap(schema);
        if (schema is not (global::Avro.ArraySchema or global::Avro.MapSchema))
            return -1;
        var reader = new AvroValidationReader(payload);
        var count = 0L;
        while (true)
        {
            var blockCount = reader.ReadCollectionCount();
            if (blockCount == 0)
                return checked((int)count);
            count = checked(count + blockCount);
            for (long index = 0; index < blockCount; index++)
            {
                if (schema is global::Avro.MapSchema map)
                {
                    _ = reader.ReadLengthPrefixed();
                    Skip(map.ValueSchema, ref reader);
                }
                else
                {
                    Skip(((global::Avro.ArraySchema)schema).ItemSchema, ref reader);
                }
            }
        }
    }

    private static void SkipCollection(
        AvroSchema valueSchema,
        bool isMap,
        ref AvroValidationReader reader)
    {
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return;
            for (long index = 0; index < count; index++)
            {
                if (isMap)
                    _ = reader.ReadLengthPrefixed();
                Skip(valueSchema, ref reader);
            }
        }
    }

    private static SchemaRegistryRuleException InvalidPayload(string reason) =>
        new($"Could not evaluate Avro validation rules: {reason}.");
}

internal ref struct AvroValidationPath
{
    private Span<char> _buffer;
    private char[]? _rented;

    internal AvroValidationPath(Span<char> initialBuffer)
    {
        _buffer = initialBuffer;
        _rented = null;
        _buffer[0] = '$';
        Length = 1;
    }

    internal int Length { get; private set; }

    internal void AppendField(string name)
    {
        EnsureCapacity(name.Length + (Length == 0 ? 0 : 1));
        if (Length != 0)
            _buffer[Length++] = '.';
        name.AsSpan().CopyTo(_buffer[Length..]);
        Length += name.Length;
    }

    internal void AppendIndex(int index)
    {
        Span<char> digits = stackalloc char[11];
        if (!index.TryFormat(digits, out var written, provider: CultureInfo.InvariantCulture))
            throw new InvalidOperationException("Could not format Avro validation path index.");
        EnsureCapacity(written + 2);
        _buffer[Length++] = '[';
        digits[..written].CopyTo(_buffer[Length..]);
        Length += written;
        _buffer[Length++] = ']';
    }

    internal void AppendMapKey(ReadOnlySpan<byte> utf8)
    {
        var characterCount = Encoding.UTF8.GetCharCount(utf8);
        EnsureCapacity(checked(characterCount * 2 + 4));
        _buffer[Length++] = '[';
        _buffer[Length++] = '"';
        var start = Length;
        var decoded = Encoding.UTF8.GetChars(utf8, _buffer[start..]);
        var escapes = 0;
        for (var index = 0; index < decoded; index++)
        {
            if (_buffer[start + index] is '"' or '\\')
                escapes++;
        }
        var source = start + decoded - 1;
        var destination = start + decoded + escapes - 1;
        while (source >= start)
        {
            var character = _buffer[source--];
            _buffer[destination--] = character;
            if (character is '"' or '\\')
                _buffer[destination--] = '\\';
        }
        Length = start + decoded + escapes;
        _buffer[Length++] = '"';
        _buffer[Length++] = ']';
    }

    internal void Truncate(int length) => Length = length;
    public override string ToString() => new(_buffer[..Length]);

    internal void Dispose()
    {
        if (_rented is not null)
            ArrayPool<char>.Shared.Return(_rented);
        _rented = null;
        _buffer = default;
    }

    private void EnsureCapacity(int additional)
    {
        var required = checked(Length + additional);
        if (required <= _buffer.Length)
            return;
        var replacement = ArrayPool<char>.Shared.Rent(Math.Max(required, _buffer.Length * 2));
        _buffer[..Length].CopyTo(replacement);
        if (_rented is not null)
            ArrayPool<char>.Shared.Return(_rented);
        _rented = replacement;
        _buffer = replacement;
    }
}
