using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Dekaf.SchemaRegistry.Protobuf;

internal sealed class ProtobufInlineRuleExecutor(
    ISchemaRegistryClient schemaRegistry,
    MessageDescriptor descriptor) : IInlineValidationRuleExecutor
{
    private static readonly TimeSpan SchemaRegistryTimeout = TimeSpan.FromSeconds(30);
    private readonly ProtobufInlineRuleValidator _validator = new(descriptor);
    private readonly ConcurrentDictionary<int, Schema> _globalSchemas = [];
    private SchemaCacheEntry? _lastSchema;

    internal void Validate(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        Schema? schema,
        bool failFast)
    {
        if (schema is null)
        {
            _validator.Validate(payload, schemaId, failFast);
            return;
        }

        var resolved = ResolveSchema(schemaId, schema);
        if (resolved is null)
            _validator.Validate(payload, schemaId, failFast);
        else
            ((IInlineValidationRuleExecutor)_validator).Validate(
                payload,
                schemaId,
                resolved,
                failFast);
    }

    void IInlineValidationRuleExecutor.Validate(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        Schema schema,
        bool failFast) => Validate(payload, schemaId, schema, failFast);

    private Schema? ResolveSchema(int schemaId, Schema schema)
    {
        var cached = Volatile.Read(ref _lastSchema);
        if (cached is { SchemaId: var cachedSchemaId } && cachedSchemaId == schemaId)
            return cached.Schema;

        var candidate = ProtobufInlineRuleValidator.IsSerializedDescriptor(schema.SchemaString)
            ? schema
            : _globalSchemas.GetOrAdd(
                schemaId,
                static (id, registry) => registry.GetSchemaSync(id, SchemaRegistryTimeout),
                schemaRegistry);
        var resolved = ProtobufInlineRuleValidator.IsSerializedDescriptor(candidate.SchemaString)
            ? candidate
            : null;
        Volatile.Write(ref _lastSchema, new SchemaCacheEntry(schemaId, resolved));
        return resolved;
    }

    private sealed record SchemaCacheEntry(int SchemaId, Schema? Schema);
}

internal sealed class ProtobufInlineRuleValidator : IInlineValidationRuleExecutor
{
    private readonly ProtobufMessageRulePlan _root;
    private readonly string _rootMessageName;
    private readonly string _rootSchema;
    private readonly Dictionary<string, ByteString> _knownFiles;
    private readonly ConcurrentDictionary<int, ProtobufInlineRuleValidator> _schemaValidators = [];
    private SchemaValidatorCacheEntry? _lastSchema;

    internal ProtobufInlineRuleValidator(MessageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _rootMessageName = descriptor.FullName;
        _rootSchema = descriptor.File.SerializedData.ToBase64();
        _knownFiles = CreateKnownFileCatalog(descriptor.File);
        var plans = new Dictionary<MessageDescriptor, ProtobufMessageRulePlan>();
        _root = ProtobufMessageRulePlan.Create(descriptor, plans);
        ProtobufMessageRulePlan.Complete(plans);
    }

    internal void Validate(ReadOnlyMemory<byte> payload, int schemaId, bool failFast)
    {
        List<ValidationRuleError>? violations = null;
        Span<char> initialPath = stackalloc char[256];
        var path = new ProtobufValidationPath(initialPath);
        try
        {
            _root.Validate(
                payload,
                schemaId,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                failFast,
                ref violations,
                ref path);
        }
        finally
        {
            path.Dispose();
        }

        if (violations is not null)
            throw new ValidationRulesFailedException(violations);
    }

    internal static bool IsSerializedDescriptor(ReadOnlySpan<char> schema)
    {
        for (var index = 0; index < schema.Length; index++)
        {
            var character = schema[index];
            if ((uint)(character - 'A') <= 'Z' - 'A' ||
                (uint)(character - 'a') <= 'z' - 'a' ||
                (uint)(character - '0') <= 9 ||
                character is '+' or '/' or '=' or ' ' or '\t' or '\r' or '\n')
            {
                continue;
            }

            return false;
        }
        return schema.Length != 0;
    }

    void IInlineValidationRuleExecutor.Validate(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        Schema schema,
        bool failFast)
    {
        var cached = Volatile.Read(ref _lastSchema);
        var validator = cached is { SchemaId: var cachedSchemaId }
            && cachedSchemaId == schemaId
                ? cached.Validator
                : ResolveSchemaValidator(schemaId, schema);
        validator.Validate(payload, schemaId, failFast);
    }

    private ProtobufInlineRuleValidator ResolveSchemaValidator(int schemaId, Schema schema)
    {
        if (schema.SchemaType != SchemaType.Protobuf)
        {
            throw new SchemaRegistryRuleException(
                $"Schema {schemaId} is not a Protobuf schema (type: {schema.SchemaType}).");
        }

        var validator = _schemaValidators.GetOrAdd(
            schemaId,
            static (_, state) => state.Owner.CreateSchemaValidator(state.Schema),
            (Owner: this, Schema: schema));
        Volatile.Write(ref _lastSchema, new SchemaValidatorCacheEntry(schemaId, validator));
        return validator;
    }

    private ProtobufInlineRuleValidator CreateSchemaValidator(Schema schema)
    {
        if (string.Equals(schema.SchemaString, _rootSchema, StringComparison.Ordinal))
            return this;

        ByteString rootData;
        FileDescriptorProto rootProto;
        try
        {
            rootData = ByteString.FromBase64(schema.SchemaString);
            rootProto = FileDescriptorProto.Parser.ParseFrom(rootData);
        }
        catch (Exception exception) when (exception is FormatException or InvalidProtocolBufferException)
        {
            throw new SchemaRegistryRuleException(
                "Could not decode the registered Protobuf descriptor for inline validation.",
                exception);
        }

        var files = new List<ByteString>();
        var added = new HashSet<string>(StringComparer.Ordinal);
        AddDependencies(rootProto, files, added);
        files.Add(rootData);
        IReadOnlyList<FileDescriptor> descriptors;
        try
        {
            descriptors = FileDescriptor.BuildFromByteStrings(files);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidProtocolBufferException)
        {
            throw new SchemaRegistryRuleException(
                "Could not build the registered Protobuf descriptor for inline validation.",
                exception);
        }

        FileDescriptor? root = null;
        for (var index = 0; index < descriptors.Count; index++)
        {
            if (descriptors[index].Name == rootProto.Name)
            {
                root = descriptors[index];
                break;
            }
        }

        var message = root is null ? null : FindMessage(root.MessageTypes, _rootMessageName);
        if (message is null)
        {
            throw new SchemaRegistryRuleException(
                $"Registered Protobuf schema does not contain message '{_rootMessageName}'.");
        }

        return new ProtobufInlineRuleValidator(message);
    }

    private void AddDependencies(
        FileDescriptorProto file,
        List<ByteString> files,
        HashSet<string> added)
    {
        for (var index = 0; index < file.Dependency.Count; index++)
        {
            var name = file.Dependency[index];
            if (!added.Add(name))
                continue;
            if (!_knownFiles.TryGetValue(name, out var data))
            {
                throw new SchemaRegistryRuleException(
                    $"Registered Protobuf schema dependency '{name}' is unavailable for inline validation.");
            }

            var dependency = FileDescriptorProto.Parser.ParseFrom(data);
            AddDependencies(dependency, files, added);
            files.Add(data);
        }
    }

    private static Dictionary<string, ByteString> CreateKnownFileCatalog(FileDescriptor root)
    {
        var files = new Dictionary<string, ByteString>(StringComparer.Ordinal);
        AddFile(root, files);
        return files;

        static void AddFile(FileDescriptor file, Dictionary<string, ByteString> files)
        {
            if (!files.TryAdd(file.Name, file.SerializedData))
                return;
            for (var index = 0; index < file.Dependencies.Count; index++)
                AddFile(file.Dependencies[index], files);
        }
    }

    private static MessageDescriptor? FindMessage(
        IList<MessageDescriptor> messages,
        string fullName)
    {
        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];
            if (message.FullName == fullName)
                return message;
            var nested = FindMessage(message.NestedTypes, fullName);
            if (nested is not null)
                return nested;
        }
        return null;
    }

    private sealed record SchemaValidatorCacheEntry(
        int SchemaId,
        ProtobufInlineRuleValidator Validator);
}

internal sealed class ProtobufMessageRulePlan
{
    private readonly MessageDescriptor _descriptor;
    private ProtobufCompiledRuleSet _messageRules = ProtobufCompiledRuleSet.Empty;
    private Dictionary<int, ProtobufFieldRulePlan> _fields = [];
    private ProtobufFieldRulePlan[] _allFields = [];
    private ProtobufFieldRulePlan[] _ruleFields = [];
    private int _fieldSlotCount;
    private bool _usesSizes;

    internal bool HasAnyRules { get; private set; }

    private ProtobufMessageRulePlan(MessageDescriptor descriptor) => _descriptor = descriptor;

    internal static ProtobufMessageRulePlan Create(
        MessageDescriptor descriptor,
        Dictionary<MessageDescriptor, ProtobufMessageRulePlan> plans)
    {
        if (plans.TryGetValue(descriptor, out var existing))
            return existing;

        var plan = new ProtobufMessageRulePlan(descriptor);
        plans.Add(descriptor, plan);
        plan.Initialize(plans);
        return plan;
    }

    private void Initialize(Dictionary<MessageDescriptor, ProtobufMessageRulePlan> plans)
    {
        _messageRules = ProtobufCompiledRuleSet.Compile(
            ProtobufMetaRuleParser.ReadRules(_descriptor.GetOptions()),
            _descriptor);

        var fields = _descriptor.Fields.InFieldNumberOrder();
        var allFields = new ProtobufFieldRulePlan[fields.Count];
        List<ProtobufFieldRulePlan>? ruleFields = null;
        var runtimeIndex = 0;
        for (var index = 0; index < fields.Count; index++)
        {
            var descriptor = fields[index];
            var rules = ProtobufCompiledRuleSet.Compile(
                ProtobufMetaRuleParser.ReadRules(descriptor.GetOptions()),
                descriptor is { IsRepeated: false, FieldType: FieldType.Message }
                    ? descriptor.MessageType
                    : null);
            var mapKey = descriptor.IsMap
                ? descriptor.MessageType.FindFieldByNumber(1)
                : null;
            var mapValue = descriptor.IsMap
                ? descriptor.MessageType.FindFieldByNumber(2)
                : null;
            var childDescriptor = mapValue is { FieldType: FieldType.Message or FieldType.Group }
                ? mapValue.MessageType
                : descriptor is { IsMap: false, FieldType: FieldType.Message or FieldType.Group }
                    ? descriptor.MessageType
                    : null;
            var child = childDescriptor is null ? null : Create(childDescriptor, plans);
            var field = new ProtobufFieldRulePlan(
                descriptor,
                rules,
                child,
                mapKey,
                mapValue,
                runtimeIndex++);
            _fields.Add(descriptor.FieldNumber, field);
            allFields[index] = field;
            if (!rules.IsEmpty)
                (ruleFields ??= []).Add(field);
            _usesSizes |= descriptor.IsRepeated || rules.UsesSize;
        }

        _allFields = allFields;
        _ruleFields = ruleFields is null ? [] : [.. ruleFields];
        _fieldSlotCount = runtimeIndex;
        _usesSizes |= _messageRules.UsesSize;
        HasAnyRules = !_messageRules.IsEmpty || _ruleFields.Length != 0;
    }

    internal static void Complete(Dictionary<MessageDescriptor, ProtobufMessageRulePlan> plans)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var plan in plans.Values)
            {
                if (plan.HasAnyRules)
                    continue;

                for (var index = 0; index < plan._allFields.Length; index++)
                {
                    if (plan._allFields[index].Child is not { HasAnyRules: true })
                        continue;

                    plan.HasAnyRules = true;
                    changed = true;
                    break;
                }
            }
        }
    }

    internal void Validate(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        ref ProtobufValidationPath path)
    {
        if (!HasAnyRules)
            return;

        if (!_messageRules.IsEmpty)
        {
            _messageRules.Evaluate(
                ValidationCelValue.FromCollection(ValidationCelValueKind.Object, 0),
                payload,
                schemaId,
                now,
                failFast,
                ref violations,
                ref path);
            if (failFast && violations is not null)
                return;
        }

        if (_fields.Count == 0)
            return;

        var values = CompiledValidationRule.GetMemberValues(_fieldSlotCount);
        var sizes = _usesSizes
            ? CompiledValidationRule.GetSizeValues(_fieldSlotCount + 1)
            : default;
        var reader = new ProtobufValidationWireReader(payload);
        while (reader.TryRead(out var wireField))
        {
            if (!_fields.TryGetValue(wireField.Number, out var field))
                continue;

            field.Observe(wireField, values, sizes);
        }

        for (var index = 0; index < _ruleFields.Length; index++)
        {
            var field = _ruleFields[index];
            field.ApplyDefault(values, sizes);
            if (!values.IsSet(field.RuntimeIndex))
                continue;

            var mark = path.Length;
            path.AppendField(field.Descriptor.Name);
            field.Rules.Evaluate(
                field.GetRuleValue(values),
                field.GetMessagePayload(values),
                schemaId,
                now,
                failFast,
                ref violations,
                ref path,
                field.GetCollectionSize(sizes));
            path.Truncate(mark);
            if (failFast && violations is not null)
                return;
        }

        reader = new ProtobufValidationWireReader(payload);
        Span<int> initialIndexes = stackalloc int[8];
        var repeatedIndexes = new ProtobufRepeatedIndexes(initialIndexes);
        try
        {
            while (reader.TryRead(out var wireField))
            {
                if (!_fields.TryGetValue(wireField.Number, out var field) ||
                    field.Child is not { HasAnyRules: true })
                    continue;
                if (wireField.WireType != ProtobufWireType.LengthDelimited)
                    continue;

                var mark = path.Length;
                path.AppendField(field.Descriptor.Name);
                var childPayload = wireField.Payload;
                if (field.Descriptor.IsMap)
                {
                    if (!field.TryGetMapValue(wireField.Payload, out var mapKey, out childPayload))
                    {
                        path.Truncate(mark);
                        continue;
                    }
                    path.AppendMapKey(mapKey);
                }
                else if (field.Descriptor.IsRepeated)
                    path.AppendIndex(repeatedIndexes.Take(field.RuntimeIndex));
                field.Child.Validate(
                    childPayload,
                    schemaId,
                    now,
                    failFast,
                    ref violations,
                    ref path);
                path.Truncate(mark);
                if (failFast && violations is not null)
                    return;
            }
        }
        finally
        {
            repeatedIndexes.Dispose();
        }
    }
}

internal sealed class ProtobufFieldRulePlan(
    FieldDescriptor descriptor,
    ProtobufCompiledRuleSet rules,
    ProtobufMessageRulePlan? child,
    FieldDescriptor? mapKey,
    FieldDescriptor? mapValue,
    int runtimeIndex)
{
    internal FieldDescriptor Descriptor { get; } = descriptor;
    internal ProtobufCompiledRuleSet Rules { get; } = rules;
    internal ProtobufMessageRulePlan? Child { get; } = child;
    internal int RuntimeIndex { get; } = runtimeIndex;

    internal bool TryGetMapValue(
        ReadOnlyMemory<byte> entryPayload,
        out ValidationCelValue key,
        out ReadOnlyMemory<byte> valuePayload)
    {
        key = ProtobufValidationValueDecoder.Default(mapKey!);
        valuePayload = default;
        var hasValue = false;
        var reader = new ProtobufValidationWireReader(entryPayload);
        while (reader.TryRead(out var field))
        {
            if (field.Number == mapKey!.FieldNumber)
                key = ProtobufValidationValueDecoder.Decode(mapKey, field);
            else if (field.Number == mapValue!.FieldNumber &&
                     field.WireType == ProtobufWireType.LengthDelimited)
            {
                valuePayload = field.Payload;
                hasValue = true;
            }
        }
        return hasValue;
    }

    internal void Observe(
        ProtobufValidationWireField field,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        if (Descriptor.IsRepeated)
        {
            var count = field.WireType == ProtobufWireType.LengthDelimited &&
                        ProtobufValidationValueDecoder.IsPackable(Descriptor)
                ? ProtobufValidationValueDecoder.CountPacked(Descriptor, field.Payload)
                : 1;
            if (!sizes.TryGet(RuntimeIndex + 1, out var current))
                current = 0;
            sizes.Set(RuntimeIndex + 1, checked(current + count));
            values.SetValue(
                RuntimeIndex,
                ValidationCelValue.FromCollection(ValidationCelValueKind.Array, RuntimeIndex + 1));
            return;
        }

        values.SetValue(RuntimeIndex, ProtobufValidationValueDecoder.Decode(Descriptor, field));
    }

    internal void ApplyDefault(ValidationCelMemberValues values, ValidationCelSizeValues sizes)
    {
        if (values.IsSet(RuntimeIndex))
            return;
        if (Descriptor.IsRepeated)
        {
            sizes.Set(RuntimeIndex + 1, 0);
            values.SetValue(
                RuntimeIndex,
                ValidationCelValue.FromCollection(ValidationCelValueKind.Array, RuntimeIndex + 1));
        }
        else if (!Descriptor.HasPresence)
        {
            values.SetValue(RuntimeIndex, ProtobufValidationValueDecoder.Default(Descriptor));
        }
    }

    internal ReadOnlyMemory<byte> GetMessagePayload(ValidationCelMemberValues values)
    {
        if (Descriptor is not { IsRepeated: false, FieldType: FieldType.Message } ||
            !values.IsSet(RuntimeIndex))
        {
            return default;
        }

        return values.GetValue(RuntimeIndex, default).Utf8Literal;
    }

    internal ValidationCelValue GetRuleValue(ValidationCelMemberValues values)
    {
        var value = values.GetValue(RuntimeIndex, default);
        return Descriptor.IsRepeated ? value with { SizeIndex = 0 } : value;
    }

    internal int GetCollectionSize(ValidationCelSizeValues sizes)
    {
        if (!Descriptor.IsRepeated)
            return -1;
        return sizes.TryGet(RuntimeIndex + 1, out var count) ? count : 0;
    }
}

internal sealed class ProtobufCompiledRuleSet
{
    internal static ProtobufCompiledRuleSet Empty { get; } = new([], null, false, false, 0);

    private readonly CompiledValidationRule[] _rules;
    private readonly ProtobufMemberResolver? _members;
    private readonly bool _usesCachedEquality;
    private readonly int _memberCount;

    private ProtobufCompiledRuleSet(
        CompiledValidationRule[] rules,
        ProtobufMemberResolver? members,
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

    internal static ProtobufCompiledRuleSet Compile(
        IReadOnlyList<ValidationRule> rules,
        MessageDescriptor? valueDescriptor = null)
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

        var members = usedMemberIndexes.Count == 0 || valueDescriptor is null
            ? null
            : ProtobufMemberResolver.Create(valueDescriptor, memberPaths, usedMemberIndexes);
        return new ProtobufCompiledRuleSet(
            compiled,
            members,
            usesSize,
            usesCachedEquality,
            memberPaths.Count);
    }

    internal static ProtobufCompiledRuleSet Compile(IReadOnlyList<ValidationRule> rules) =>
        Compile(rules, valueDescriptor: null);

    internal void Evaluate(
        ValidationCelValue value,
        ReadOnlyMemory<byte> messagePayload,
        int schemaId,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        ref ProtobufValidationPath path,
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
        if (_members is not null)
            _members.Resolve(messagePayload, memberValues, sizes);
        var equalityGeneration = _usesCachedEquality
            ? CompiledValidationRule.BeginEqualityResolution()
            : 0;

        ValidationCelStrings.Begin(_memberCount + 1, messagePayload.Length);
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

                if (failFast && violations is not null)
                    return;
            }
        }
        finally
        {
            ValidationCelStrings.End();
        }
    }
}

internal sealed class ProtobufMemberResolver
{
    private readonly Dictionary<int, ProtobufMemberNode> _fields = [];
    private ProtobufMemberNode[] _nodes = [];

    internal static ProtobufMemberResolver Create(
        MessageDescriptor descriptor,
        IReadOnlyList<byte[][]> paths,
        IReadOnlyCollection<int> usedIndexes)
    {
        var resolver = new ProtobufMemberResolver();
        foreach (var memberIndex in usedIndexes)
            resolver.Add(descriptor, paths[memberIndex], memberIndex, depth: 0);
        resolver.Freeze();
        return resolver;
    }

    private void Freeze()
    {
        _nodes = [.. _fields.Values];
        for (var index = 0; index < _nodes.Length; index++)
            _nodes[index].Child?.Freeze();
    }

    private void Add(MessageDescriptor descriptor, byte[][] path, int memberIndex, int depth)
    {
        var name = Encoding.UTF8.GetString(path[depth]);
        var field = descriptor.FindFieldByName(name)
            ?? throw new SchemaRegistryRuleException(
                $"Protobuf validation rule refers to unknown field '{name}' on '{descriptor.FullName}'.");
        if (!_fields.TryGetValue(field.FieldNumber, out var node))
        {
            node = new ProtobufMemberNode(field);
            _fields.Add(field.FieldNumber, node);
        }

        if (depth == path.Length - 1)
        {
            node.MemberIndex = memberIndex;
            return;
        }

        if (field is not { IsRepeated: false, FieldType: FieldType.Message })
        {
            throw new SchemaRegistryRuleException(
                $"Protobuf validation member path cannot descend through '{field.FullName}'.");
        }
        node.Child ??= new ProtobufMemberResolver();
        node.Child.Add(field.MessageType, path, memberIndex, depth + 1);
    }

    internal void Resolve(
        ReadOnlyMemory<byte> payload,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        var reader = new ProtobufValidationWireReader(payload);
        while (reader.TryRead(out var field))
        {
            if (!_fields.TryGetValue(field.Number, out var node))
                continue;

            node.Observe(field, values, sizes);
        }

        for (var index = 0; index < _nodes.Length; index++)
            _nodes[index].ApplyDefault(values, sizes);
    }
}

internal sealed class ProtobufMemberNode(FieldDescriptor descriptor)
{
    internal int MemberIndex { get; set; } = -1;
    internal ProtobufMemberResolver? Child { get; set; }

    internal void Observe(
        ProtobufValidationWireField field,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        if (MemberIndex >= 0)
        {
            if (descriptor.IsRepeated)
            {
                var count = field.WireType == ProtobufWireType.LengthDelimited &&
                            ProtobufValidationValueDecoder.IsPackable(descriptor)
                    ? ProtobufValidationValueDecoder.CountPacked(descriptor, field.Payload)
                    : 1;
                if (!sizes.TryGet(MemberIndex + 1, out var current))
                    current = 0;
                sizes.Set(MemberIndex + 1, checked(current + count));
                values.SetValue(
                    MemberIndex,
                    ValidationCelValue.FromCollection(ValidationCelValueKind.Array, MemberIndex + 1));
            }
            else
            {
                values.SetValue(MemberIndex, ProtobufValidationValueDecoder.Decode(descriptor, field));
            }
        }

        if (Child is not null && field.WireType == ProtobufWireType.LengthDelimited)
            Child.Resolve(field.Payload, values, sizes);
    }

    internal void ApplyDefault(
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        if (MemberIndex < 0 || values.IsSet(MemberIndex))
            return;
        if (descriptor.IsRepeated)
        {
            sizes.Set(MemberIndex + 1, 0);
            values.SetValue(
                MemberIndex,
                ValidationCelValue.FromCollection(ValidationCelValueKind.Array, MemberIndex + 1));
        }
        else if (!descriptor.HasPresence)
        {
            values.SetValue(MemberIndex, ProtobufValidationValueDecoder.Default(descriptor));
        }
    }
}

internal static class ProtobufValidationValueDecoder
{
    internal static ValidationCelValue Decode(
        FieldDescriptor descriptor,
        ProtobufValidationWireField field)
    {
        if (descriptor.FieldType == FieldType.Message)
            return DecodeMessage(descriptor.MessageType, field.Payload);
        return descriptor.FieldType switch
        {
            FieldType.Double => ValidationCelValue.FromFloating(
                BitConverter.Int64BitsToDouble(unchecked((long)field.Fixed64))),
            FieldType.Float => ValidationCelValue.FromFloating(
                BitConverter.Int32BitsToSingle(unchecked((int)field.Fixed32))),
            FieldType.Int64 => ValidationCelValue.FromNumber(unchecked((long)field.Varint)),
            FieldType.UInt64 => ValidationCelValue.FromNumber(field.Varint),
            FieldType.Int32 => ValidationCelValue.FromNumber(unchecked((int)field.Varint)),
            FieldType.Fixed64 => ValidationCelValue.FromNumber(field.Fixed64),
            FieldType.Fixed32 => ValidationCelValue.FromNumber(field.Fixed32),
            FieldType.Bool => ValidationCelValue.FromBoolean(field.Varint != 0),
            FieldType.String => ValidationCelValue.FromUtf8String(field.Payload),
            FieldType.Bytes => ValidationCelValue.FromBytes(field.Payload),
            FieldType.UInt32 => ValidationCelValue.FromNumber(unchecked((uint)field.Varint)),
            FieldType.SFixed32 => ValidationCelValue.FromNumber(unchecked((int)field.Fixed32)),
            FieldType.SFixed64 => ValidationCelValue.FromNumber(unchecked((long)field.Fixed64)),
            FieldType.SInt32 => ValidationCelValue.FromNumber(DecodeZigZag32(field.Varint)),
            FieldType.SInt64 => ValidationCelValue.FromNumber(DecodeZigZag64(field.Varint)),
            FieldType.Enum => ValidationCelValue.FromNumber(unchecked((int)field.Varint)),
            _ => ValidationCelValue.Missing
        };
    }

    internal static ValidationCelValue Default(FieldDescriptor descriptor) => descriptor.FieldType switch
    {
        FieldType.Double or FieldType.Float => ValidationCelValue.FromFloating(0),
        FieldType.Int64 or FieldType.UInt64 or FieldType.Int32 or FieldType.Fixed64 or
            FieldType.Fixed32 or FieldType.UInt32 or FieldType.SFixed32 or FieldType.SFixed64 or
            FieldType.SInt32 or FieldType.SInt64 or FieldType.Enum => ValidationCelValue.FromNumber(0),
        FieldType.Bool => ValidationCelValue.False,
        FieldType.String => ValidationCelValue.FromUtf8String(default),
        FieldType.Bytes => ValidationCelValue.FromBytes(default),
        _ => ValidationCelValue.Missing
    };

    internal static int CountPacked(FieldDescriptor descriptor, ReadOnlyMemory<byte> payload)
    {
        var wireType = descriptor.FieldType switch
        {
            FieldType.Double or FieldType.Fixed64 or FieldType.SFixed64 => ProtobufWireType.Fixed64,
            FieldType.Float or FieldType.Fixed32 or FieldType.SFixed32 => ProtobufWireType.Fixed32,
            _ => ProtobufWireType.Varint
        };
        if (wireType == ProtobufWireType.Fixed64)
            return payload.Length / sizeof(ulong);
        if (wireType == ProtobufWireType.Fixed32)
            return payload.Length / sizeof(uint);

        var count = 0;
        var span = payload.Span;
        var offset = 0;
        while (offset < span.Length)
        {
            _ = ProtobufValidationWireReader.ReadVarint(span, ref offset);
            count++;
        }
        return count;
    }

    internal static bool IsPackable(FieldDescriptor descriptor) => descriptor.FieldType is
        FieldType.Double or FieldType.Float or FieldType.Int64 or FieldType.UInt64 or
        FieldType.Int32 or FieldType.Fixed64 or FieldType.Fixed32 or FieldType.Bool or
        FieldType.UInt32 or FieldType.Enum or FieldType.SFixed32 or FieldType.SFixed64 or
        FieldType.SInt32 or FieldType.SInt64;

    private static ValidationCelValue DecodeMessage(
        MessageDescriptor descriptor,
        ReadOnlyMemory<byte> payload)
    {
        if (descriptor.FullName == "google.protobuf.Timestamp")
            return ValidationCelValue.FromNumber(ReadSecondsAndNanos(payload));
        if (descriptor.FullName == "google.protobuf.Duration")
            return ValidationCelValue.FromNumber(ReadSecondsAndNanos(payload));
        if (IsWrapper(descriptor.FullName) && descriptor.FindFieldByNumber(1) is { } valueField)
        {
            var reader = new ProtobufValidationWireReader(payload);
            while (reader.TryRead(out var field))
            {
                if (field.Number == 1)
                    return Decode(valueField, field);
            }
            return Default(valueField);
        }

        return new ValidationCelValue(
            ValidationCelValueKind.Object,
            default,
            false,
            0,
            null,
            payload);
    }

    private static decimal ReadSecondsAndNanos(ReadOnlyMemory<byte> payload)
    {
        long seconds = 0;
        var nanos = 0;
        var reader = new ProtobufValidationWireReader(payload);
        while (reader.TryRead(out var field))
        {
            if (field.Number == 1)
                seconds = unchecked((long)field.Varint);
            else if (field.Number == 2)
                nanos = unchecked((int)field.Varint);
        }
        return seconds * 1_000m + nanos / 1_000_000m;
    }

    private static bool IsWrapper(string fullName) => fullName is
        "google.protobuf.DoubleValue" or
        "google.protobuf.FloatValue" or
        "google.protobuf.Int64Value" or
        "google.protobuf.UInt64Value" or
        "google.protobuf.Int32Value" or
        "google.protobuf.UInt32Value" or
        "google.protobuf.BoolValue" or
        "google.protobuf.StringValue" or
        "google.protobuf.BytesValue";

    private static int DecodeZigZag32(ulong value) =>
        unchecked((int)((value >> 1) ^ (ulong)-(long)(value & 1)));

    private static long DecodeZigZag64(ulong value) =>
        unchecked((long)((value >> 1) ^ (ulong)-(long)(value & 1)));
}

internal static class ProtobufMetaRuleParser
{
    private const int MetaExtensionNumber = 1088;

    internal static IReadOnlyList<ValidationRule> ReadRules(IMessage? options)
    {
        if (options is null)
            return [];
        var data = options.ToByteArray();
        var reader = new ProtobufValidationWireReader(data);
        List<ValidationRule>? rules = null;
        while (reader.TryRead(out var field))
        {
            if (field is { Number: MetaExtensionNumber, WireType: ProtobufWireType.LengthDelimited })
                ReadMeta(field.Payload, ref rules);
        }
        return rules ?? [];
    }

    private static void ReadMeta(ReadOnlyMemory<byte> payload, ref List<ValidationRule>? rules)
    {
        var reader = new ProtobufValidationWireReader(payload);
        while (reader.TryRead(out var field))
        {
            if (field is { Number: 4, WireType: ProtobufWireType.LengthDelimited })
                (rules ??= []).Add(ReadRule(field.Payload));
        }
    }

    private static ValidationRule ReadRule(ReadOnlyMemory<byte> payload)
    {
        string? name = null;
        string? doc = null;
        string? expression = null;
        string? sql = null;
        var reader = new ProtobufValidationWireReader(payload);
        while (reader.TryRead(out var field))
        {
            if (field.WireType != ProtobufWireType.LengthDelimited)
                continue;
            var value = Encoding.UTF8.GetString(field.Payload.Span);
            switch (field.Number)
            {
                case 1:
                    name = value;
                    break;
                case 2:
                    doc = value;
                    break;
                case 3:
                    expression = value;
                    break;
                case 4:
                    sql = value;
                    break;
            }
        }

        return new ValidationRule { Name = name, Doc = doc, Expr = expression, Sql = sql };
    }
}

internal enum ProtobufWireType : byte
{
    Varint = 0,
    Fixed64 = 1,
    LengthDelimited = 2,
    StartGroup = 3,
    EndGroup = 4,
    Fixed32 = 5
}

internal readonly record struct ProtobufValidationWireField(
    int Number,
    ProtobufWireType WireType,
    ReadOnlyMemory<byte> Payload,
    ulong Varint,
    ulong Fixed64,
    uint Fixed32);

internal ref struct ProtobufValidationWireReader
{
    private readonly ReadOnlyMemory<byte> _source;
    private int _offset;

    internal ProtobufValidationWireReader(ReadOnlyMemory<byte> source)
    {
        _source = source;
        _offset = 0;
    }

    internal bool TryRead(out ProtobufValidationWireField field)
    {
        var span = _source.Span;
        if (_offset == span.Length)
        {
            field = default;
            return false;
        }

        var tag = ReadVarint(span, ref _offset);
        var number = checked((int)(tag >> 3));
        var wireType = (ProtobufWireType)(tag & 7);
        if (number == 0)
            throw InvalidPayload("field number 0");

        switch (wireType)
        {
            case ProtobufWireType.Varint:
                field = new(number, wireType, default, ReadVarint(span, ref _offset), 0, 0);
                return true;
            case ProtobufWireType.Fixed64:
                EnsureRemaining(span, sizeof(ulong));
                var fixed64 = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(_offset, sizeof(ulong)));
                _offset += sizeof(ulong);
                field = new(number, wireType, default, 0, fixed64, 0);
                return true;
            case ProtobufWireType.LengthDelimited:
                var length = ReadVarint(span, ref _offset);
                if (length > int.MaxValue)
                    throw InvalidPayload("length exceeds Int32.MaxValue");
                EnsureRemaining(span, (int)length);
                var payload = _source.Slice(_offset, (int)length);
                _offset += (int)length;
                field = new(number, wireType, payload, 0, 0, 0);
                return true;
            case ProtobufWireType.Fixed32:
                EnsureRemaining(span, sizeof(uint));
                var fixed32 = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(_offset, sizeof(uint)));
                _offset += sizeof(uint);
                field = new(number, wireType, default, 0, 0, fixed32);
                return true;
            case ProtobufWireType.StartGroup:
                SkipGroup(span, number);
                field = new(number, wireType, default, 0, 0, 0);
                return true;
            case ProtobufWireType.EndGroup:
                throw InvalidPayload("unexpected end-group tag");
            default:
                throw InvalidPayload("unknown wire type");
        }
    }

    internal static ulong ReadVarint(ReadOnlySpan<byte> source, ref int offset)
    {
        ulong value = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            if ((uint)offset >= (uint)source.Length)
                throw InvalidPayload("truncated varint");
            var current = source[offset++];
            value |= (ulong)(current & 0x7f) << shift;
            if ((current & 0x80) == 0)
                return value;
        }
        throw InvalidPayload("varint exceeds 10 bytes");
    }

    private void SkipGroup(ReadOnlySpan<byte> source, int groupNumber)
    {
        while (_offset < source.Length)
        {
            var tag = ReadVarint(source, ref _offset);
            var number = checked((int)(tag >> 3));
            var wireType = (ProtobufWireType)(tag & 7);
            if (wireType == ProtobufWireType.EndGroup)
            {
                if (number != groupNumber)
                    throw InvalidPayload("mismatched end-group tag");
                return;
            }

            SkipValue(source, number, wireType);
        }
        throw InvalidPayload("unterminated group");
    }

    private void SkipValue(ReadOnlySpan<byte> source, int number, ProtobufWireType wireType)
    {
        switch (wireType)
        {
            case ProtobufWireType.Varint:
                _ = ReadVarint(source, ref _offset);
                break;
            case ProtobufWireType.Fixed64:
                EnsureRemaining(source, sizeof(ulong));
                _offset += sizeof(ulong);
                break;
            case ProtobufWireType.LengthDelimited:
                var length = ReadVarint(source, ref _offset);
                if (length > int.MaxValue)
                    throw InvalidPayload("length exceeds Int32.MaxValue");
                EnsureRemaining(source, (int)length);
                _offset += (int)length;
                break;
            case ProtobufWireType.StartGroup:
                SkipGroup(source, number);
                break;
            case ProtobufWireType.Fixed32:
                EnsureRemaining(source, sizeof(uint));
                _offset += sizeof(uint);
                break;
            default:
                throw InvalidPayload("unknown wire type");
        }
    }

    private void EnsureRemaining(ReadOnlySpan<byte> source, int length)
    {
        if (length < 0 || _offset > source.Length - length)
            throw InvalidPayload("truncated field");
    }

    private static SchemaRegistryRuleException InvalidPayload(string reason) =>
        new($"Could not evaluate Protobuf validation rules: {reason}.");
}

internal ref struct ProtobufValidationPath
{
    private Span<char> _buffer;
    private char[]? _rented;

    internal ProtobufValidationPath(Span<char> initialBuffer)
    {
        _buffer = initialBuffer;
        _rented = null;
        Length = 0;
    }

    internal int Length { get; private set; }

    internal void AppendField(string name)
    {
        var separatorLength = Length == 0 ? 0 : 1;
        EnsureCapacity(separatorLength + name.Length);
        if (separatorLength != 0)
            _buffer[Length++] = '.';
        name.AsSpan().CopyTo(_buffer[Length..]);
        Length += name.Length;
    }

    internal void AppendIndex(int index)
    {
        Span<char> digits = stackalloc char[11];
        if (!index.TryFormat(digits, out var written, provider: CultureInfo.InvariantCulture))
            throw new InvalidOperationException("Could not format Protobuf validation path index.");
        EnsureCapacity(written + 2);
        _buffer[Length++] = '[';
        digits[..written].CopyTo(_buffer[Length..]);
        Length += written;
        _buffer[Length++] = ']';
    }

    internal void AppendMapKey(ValidationCelValue key)
    {
        if (key.Kind == ValidationCelValueKind.String)
        {
            AppendStringMapKey(key.Utf8Literal.Span);
            return;
        }

        Span<char> formatted = stackalloc char[32];
        bool success;
        int written;
        if (key.Kind == ValidationCelValueKind.Boolean)
        {
            var value = key.Boolean ? "true" : "false";
            value.AsSpan().CopyTo(formatted);
            written = value.Length;
            success = true;
        }
        else
        {
            success = key.Number.TryFormat(
                formatted,
                out written,
                provider: CultureInfo.InvariantCulture);
        }
        if (!success)
            throw new InvalidOperationException("Could not format Protobuf validation map key.");

        EnsureCapacity(written + 2);
        _buffer[Length++] = '[';
        formatted[..written].CopyTo(_buffer[Length..]);
        Length += written;
        _buffer[Length++] = ']';
    }

    private void AppendStringMapKey(ReadOnlySpan<byte> utf8)
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

    private void EnsureCapacity(int additionalLength)
    {
        var required = checked(Length + additionalLength);
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

internal ref struct ProtobufRepeatedIndexes
{
    private Span<int> _indexes;
    private int[]? _rented;

    internal ProtobufRepeatedIndexes(Span<int> initialIndexes)
    {
        _indexes = initialIndexes;
        _indexes.Clear();
        _rented = null;
    }

    internal int Take(int index)
    {
        EnsureCapacity(index + 1);
        return _indexes[index]++;
    }

    internal void Dispose()
    {
        if (_rented is not null)
            ArrayPool<int>.Shared.Return(_rented, clearArray: true);
        _rented = null;
        _indexes = default;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _indexes.Length)
            return;
        var replacement = ArrayPool<int>.Shared.Rent(Math.Max(required, _indexes.Length * 2));
        replacement.AsSpan().Clear();
        _indexes.CopyTo(replacement);
        if (_rented is not null)
            ArrayPool<int>.Shared.Return(_rented, clearArray: true);
        _rented = replacement;
        _indexes = replacement;
    }
}
