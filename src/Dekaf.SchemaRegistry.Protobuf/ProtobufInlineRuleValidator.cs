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
    private readonly ProtobufInlineRuleValidator _validator = new(descriptor, schemaRegistry);
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

        Schema candidate;
        try
        {
            candidate = ProtobufInlineRuleValidator.IsSerializedDescriptor(schema.SchemaString)
                ? schema
                : _globalSchemas.GetOrAdd(
                    schemaId,
                    static (id, registry) => registry.GetSchemaSync(id, SchemaRegistryTimeout),
                    schemaRegistry);
        }
        catch (Exception exception) when (exception is TimeoutException or
            HttpRequestException or SchemaRegistryException)
        {
            candidate = schema;
        }
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
    internal const int MaximumValidationDepth = 100;
    private static readonly TimeSpan ReferenceResolutionTimeout = TimeSpan.FromSeconds(30);
    private readonly ProtobufMessageRulePlan _root;
    private readonly string _rootMessageName;
    private readonly string _rootSchema;
    private readonly Dictionary<string, ByteString> _knownFiles;
    private readonly ISchemaRegistryClient? _schemaRegistry;
    private readonly ConcurrentDictionary<int, ProtobufInlineRuleValidator> _schemaValidators = [];
    private SchemaValidatorCacheEntry? _lastSchema;

    internal ProtobufInlineRuleValidator(
        MessageDescriptor descriptor,
        ISchemaRegistryClient? schemaRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _rootMessageName = descriptor.FullName;
        _rootSchema = descriptor.File.SerializedData.ToBase64();
        _knownFiles = CreateKnownFileCatalog(descriptor.File);
        _schemaRegistry = schemaRegistry;
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
                ref path,
                MaximumValidationDepth);
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
        if (schema.References is not { Count: > 0 } &&
            string.Equals(schema.SchemaString, _rootSchema, StringComparison.Ordinal))
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
        var registeredFiles = ResolveReferences(schema);
        AddDependencies(rootProto, files, added, registeredFiles);
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

        return new ProtobufInlineRuleValidator(message, _schemaRegistry);
    }

    private void AddDependencies(
        FileDescriptorProto file,
        List<ByteString> files,
        HashSet<string> added,
        IReadOnlyDictionary<string, ByteString>? registeredFiles)
    {
        for (var index = 0; index < file.Dependency.Count; index++)
        {
            var name = file.Dependency[index];
            if (!added.Add(name))
                continue;
            if (registeredFiles is null || !registeredFiles.TryGetValue(name, out var data))
                _knownFiles.TryGetValue(name, out data);
            if (data is null)
            {
                throw new SchemaRegistryRuleException(
                    $"Registered Protobuf schema dependency '{name}' is unavailable for inline validation.");
            }

            var dependency = FileDescriptorProto.Parser.ParseFrom(data);
            AddDependencies(dependency, files, added, registeredFiles);
            files.Add(data);
        }
    }

    private Dictionary<string, ByteString>? ResolveReferences(Schema schema)
    {
        if (schema.References is not { Count: > 0 } references)
            return null;
        if (_schemaRegistry is null)
        {
            throw new SchemaRegistryRuleException(
                "Registered Protobuf schema references require a Schema Registry client.");
        }

        var files = new Dictionary<string, ByteString>(StringComparer.Ordinal);
        var resolved = new Dictionary<(string Subject, int Version), ResolvedReference>();
        var expanded = new HashSet<(string Subject, int Version)>();
        ResolveReferences(references, files, resolved, expanded);
        return files;
    }

    private void ResolveReferences(
        IReadOnlyList<SchemaReference> references,
        Dictionary<string, ByteString> files,
        Dictionary<(string Subject, int Version), ResolvedReference> resolved,
        HashSet<(string Subject, int Version)> expanded)
    {
        for (var index = 0; index < references.Count; index++)
        {
            var reference = references[index];
            var key = (reference.Subject, reference.Version);
            if (!resolved.TryGetValue(key, out var resolvedReference))
            {
                RegisteredSchema registered;
                using var timeout = new CancellationTokenSource(ReferenceResolutionTimeout);
                try
                {
                    registered = _schemaRegistry!.GetSchemaBySubjectAsync(
                            reference.Subject,
                            reference.Version.ToString(CultureInfo.InvariantCulture),
                            timeout.Token)
                        .WaitAsync(timeout.Token)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"Protobuf schema reference '{reference.Name}' resolution timed out.",
                        exception);
                }

                if (registered.Schema.SchemaType != SchemaType.Protobuf)
                {
                    throw new SchemaRegistryRuleException(
                        $"Protobuf schema reference '{reference.Name}' resolved to {registered.Schema.SchemaType}.");
                }

                ByteString data;
                try
                {
                    data = ByteString.FromBase64(registered.Schema.SchemaString);
                }
                catch (FormatException exception)
                {
                    throw new SchemaRegistryRuleException(
                        $"Could not decode Protobuf schema reference '{reference.Name}'.",
                        exception);
                }
                resolvedReference = new ResolvedReference(registered.Schema, data);
                resolved.Add(key, resolvedReference);
            }

            if (files.TryGetValue(reference.Name, out var existing) &&
                !existing.Span.SequenceEqual(resolvedReference.Data.Span))
            {
                throw new SchemaRegistryRuleException(
                    $"Protobuf schema reference '{reference.Name}' resolves to conflicting versions.");
            }
            files[reference.Name] = resolvedReference.Data;
            if (expanded.Add(key) && resolvedReference.Schema.References is { Count: > 0 } nested)
                ResolveReferences(nested, files, resolved, expanded);
        }
    }

    private readonly record struct ResolvedReference(Schema Schema, ByteString Data);

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
    private readonly Dictionary<int, ProtobufFieldRulePlan> _fields = [];
    private ProtobufFieldRulePlan[] _allFields = [];
    private ProtobufFieldRulePlan[] _ruleFields = [];
    private int _fieldSlotCount;
    private int _oneofCount;
    private bool _usesSizes;
    private bool _hasMaps;
    private bool _requiresRuleSnapshots;

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
                runtimeIndex++,
                descriptor.ContainingOneof?.Index ?? -1);
            _fields.Add(descriptor.FieldNumber, field);
            allFields[index] = field;
            if (!rules.IsEmpty)
                (ruleFields ??= []).Add(field);
            _usesSizes |= descriptor.IsRepeated || rules.UsesSize;
            _hasMaps |= descriptor.IsMap;
            _requiresRuleSnapshots |= rules.RequiresMemberResolution;
        }

        _allFields = allFields;
        _ruleFields = ruleFields is null ? [] : [.. ruleFields];
        _fieldSlotCount = runtimeIndex;
        _oneofCount = _descriptor.Oneofs.Count;
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
        ref ProtobufValidationPath path,
        int remainingDepth)
    {
        if (!HasAnyRules)
            return;
        if (remainingDepth == 0)
        {
            throw new SchemaRegistryRuleException(
                $"Could not evaluate Protobuf validation rules: message recursion exceeds {ProtobufInlineRuleValidator.MaximumValidationDepth} levels.");
        }

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
        var sizes = _usesSizes || _oneofCount != 0
            ? CompiledValidationRule.GetSizeValues(_fieldSlotCount + 1 + _oneofCount)
            : default;
        var oneofOffset = _fieldSlotCount + 1;
        var mergedMessages = new ProtobufMergedMessageBuffers(_fieldSlotCount);
        var reader = new ProtobufValidationWireReader(payload);
        try
        {
            while (reader.TryRead(out var wireField))
            {
                if (!_fields.TryGetValue(wireField.Number, out var field))
                    continue;

                field.Observe(wireField, values, sizes, oneofOffset, ref mergedMessages);
            }

            var singularChildren = new ProtobufSingularChildEntries(_allFields.Length);
            var mapEntries = new ProtobufMapEntries(GetMapEntryCount(sizes));
            try
            {
                singularChildren.Capture(_allFields, values);
                reader = new ProtobufValidationWireReader(payload);
                while (reader.TryRead(out var wireField))
                {
                    if (!_fields.TryGetValue(wireField.Number, out var field) ||
                        !field.Descriptor.IsMap ||
                        wireField.WireType != ProtobufWireType.LengthDelimited ||
                        !field.TryGetMapValue(wireField.Payload, out var mapKey, out var mapPayload))
                    {
                        continue;
                    }
                    mapEntries.Add(field, mapKey, mapPayload);
                }
                if (_hasMaps)
                    mapEntries.ApplyUniqueSizes(_allFields, sizes);

                var ruleFields = new ProtobufRuleFieldEntries(
                    _ruleFields.Length,
                    _requiresRuleSnapshots);
                try
                {
                    ruleFields.Capture(_ruleFields, values, sizes);
                    for (var index = 0; index < _ruleFields.Length; index++)
                    {
                        var field = _ruleFields[index];
                        if (!ruleFields.TryGet(index, field, values, sizes, out var ruleValue,
                                out var messagePayload, out var collectionSize))
                        {
                            continue;
                        }

                        var mark = path.Length;
                        path.AppendField(field.Descriptor.Name);
                        field.Rules.Evaluate(
                            ruleValue,
                            messagePayload,
                            schemaId,
                            now,
                            failFast,
                            ref violations,
                            ref path,
                            collectionSize);
                        path.Truncate(mark);
                        if (failFast && violations is not null)
                            return;
                    }
                }
                finally
                {
                    ruleFields.Dispose();
                }

                for (var index = 0; index < singularChildren.Count; index++)
                {
                    ref readonly var entry = ref singularChildren[index];

                    var mark = path.Length;
                    path.AppendField(entry.Field.Descriptor.Name);
                    entry.Field.Child!.Validate(
                        entry.Payload,
                        schemaId,
                        now,
                        failFast,
                        ref violations,
                        ref path,
                        remainingDepth - 1);
                    path.Truncate(mark);
                    if (failFast && violations is not null)
                        return;
                }

                ValidateRepeatedChildren(
                    payload,
                    mapEntries,
                    schemaId,
                    now,
                    failFast,
                    ref violations,
                    ref path,
                    remainingDepth);
            }
            finally
            {
                singularChildren.Dispose();
                mapEntries.Dispose();
            }
        }
        finally
        {
            mergedMessages.Dispose();
        }
    }

    private int GetMapEntryCount(ValidationCelSizeValues sizes)
    {
        if (!_hasMaps)
            return 0;
        var count = 0;
        for (var index = 0; index < _allFields.Length; index++)
        {
            var field = _allFields[index];
            if (field.Descriptor.IsMap && sizes.TryGet(field.RuntimeIndex + 1, out var fieldCount))
                count = checked(count + fieldCount);
        }
        return count;
    }

    private void ValidateRepeatedChildren(
        ReadOnlyMemory<byte> payload,
        ProtobufMapEntries mapEntries,
        int schemaId,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        ref ProtobufValidationPath path,
        int remainingDepth)
    {
        var reader = new ProtobufValidationWireReader(payload);
        Span<int> initialIndexes = stackalloc int[8];
        var repeatedIndexes = new ProtobufRepeatedIndexes(initialIndexes);
        try
        {
            while (reader.TryRead(out var wireField))
            {
                if (!_fields.TryGetValue(wireField.Number, out var field) ||
                    field.Descriptor.IsMap ||
                    !field.Descriptor.IsRepeated ||
                    field.Child is not { HasAnyRules: true } ||
                    wireField.WireType != ProtobufWireType.LengthDelimited)
                {
                    continue;
                }

                var mark = path.Length;
                path.AppendField(field.Descriptor.Name);
                path.AppendIndex(repeatedIndexes.Take(field.RuntimeIndex));
                field.Child.Validate(
                    wireField.Payload,
                    schemaId,
                    now,
                    failFast,
                    ref violations,
                    ref path,
                    remainingDepth - 1);
                path.Truncate(mark);
                if (failFast && violations is not null)
                    return;
            }

            for (var index = 0; index < mapEntries.Count; index++)
            {
                ref readonly var entry = ref mapEntries[index];
                if (entry.Field.Child is not { HasAnyRules: true } child)
                    continue;
                var mark = path.Length;
                path.AppendField(entry.Field.Descriptor.Name);
                path.AppendMapKey(entry.Key);
                child.Validate(
                    entry.Payload,
                    schemaId,
                    now,
                    failFast,
                    ref violations,
                    ref path,
                    remainingDepth - 1);
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
    int runtimeIndex,
    int oneofIndex)
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
        var reader = new ProtobufValidationWireReader(entryPayload);
        while (reader.TryRead(out var field))
        {
            if (field.Number == mapKey!.FieldNumber)
                key = ProtobufValidationValueDecoder.Decode(mapKey, field);
            else if (field.Number == mapValue!.FieldNumber &&
                     field.WireType == ProtobufWireType.LengthDelimited)
            {
                valuePayload = field.Payload;
            }
        }
        return true;
    }

    internal void Observe(
        ProtobufValidationWireField field,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes,
        int oneofOffset,
        ref ProtobufMergedMessageBuffers mergedMessages)
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

        if (oneofIndex >= 0)
        {
            var oneofSlot = oneofOffset + oneofIndex;
            if (sizes.TryGet(oneofSlot, out var previous) && previous != RuntimeIndex)
            {
                values.Clear(previous);
                mergedMessages.Clear(previous);
            }
            sizes.Set(oneofSlot, RuntimeIndex);
        }

        var decoded = ProtobufValidationValueDecoder.Decode(Descriptor, field);
        if (Descriptor.FieldType is FieldType.Message or FieldType.Group &&
            values.IsSet(RuntimeIndex))
        {
            decoded = decoded with
            {
                Utf8Literal = mergedMessages.Merge(
                    RuntimeIndex,
                    values.GetValue(RuntimeIndex, default).Utf8Literal,
                    decoded.Utf8Literal)
            };
        }
        values.SetValue(RuntimeIndex, decoded);
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

internal ref struct ProtobufMergedMessageBuffers
{
    private readonly int _fieldCount;
    private byte[][]? _buffers;
    private int[]? _lengths;

    internal ProtobufMergedMessageBuffers(int fieldCount)
    {
        _fieldCount = fieldCount;
        _buffers = null;
        _lengths = null;
    }

    internal ReadOnlyMemory<byte> Merge(
        int fieldIndex,
        ReadOnlyMemory<byte> previous,
        ReadOnlyMemory<byte> current)
    {
        EnsureSlots();
        var buffers = _buffers!;
        var lengths = _lengths!;
        var buffer = buffers[fieldIndex];
        var previousLength = lengths[fieldIndex];
        if (buffer is null)
        {
            buffer = ArrayPool<byte>.Shared.Rent(checked(previous.Length + current.Length));
            previous.Span.CopyTo(buffer);
            previousLength = previous.Length;
            buffers[fieldIndex] = buffer;
        }
        else if (buffer.Length - previousLength < current.Length)
        {
            var replacement = ArrayPool<byte>.Shared.Rent(checked(previousLength + current.Length));
            buffer.AsSpan(0, previousLength).CopyTo(replacement);
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = replacement;
            buffers[fieldIndex] = buffer;
        }

        current.Span.CopyTo(buffer.AsSpan(previousLength));
        var length = checked(previousLength + current.Length);
        lengths[fieldIndex] = length;
        return buffer.AsMemory(0, length);
    }

    internal void Clear(int fieldIndex)
    {
        if (_buffers?[fieldIndex] is not { } buffer)
            return;
        ArrayPool<byte>.Shared.Return(buffer);
        _buffers[fieldIndex] = null!;
        _lengths![fieldIndex] = 0;
    }

    private void EnsureSlots()
    {
        if (_buffers is not null)
            return;
        _buffers = ArrayPool<byte[]>.Shared.Rent(_fieldCount);
        Array.Clear(_buffers, 0, _fieldCount);
        _lengths = ArrayPool<int>.Shared.Rent(_fieldCount);
        Array.Clear(_lengths, 0, _fieldCount);
    }

    public void Dispose()
    {
        if (_buffers is null)
            return;
        for (var index = 0; index < _fieldCount; index++)
        {
            if (_buffers[index] is { } buffer)
                ArrayPool<byte>.Shared.Return(buffer);
        }
        ArrayPool<byte[]>.Shared.Return(_buffers, clearArray: true);
        ArrayPool<int>.Shared.Return(_lengths!);
        _buffers = null;
        _lengths = null;
    }
}

internal struct ProtobufMapEntry
{
    internal ProtobufFieldRulePlan Field;
    internal ValidationCelValue Key;
    internal ReadOnlyMemory<byte> Payload;
}

internal struct ProtobufSingularChildEntry
{
    internal ProtobufFieldRulePlan Field;
    internal ReadOnlyMemory<byte> Payload;
}

internal struct ProtobufRuleFieldEntry
{
    internal ValidationCelValue Value;
    internal ReadOnlyMemory<byte> MessagePayload;
    internal int CollectionSize;
    internal bool IsSet;
}

internal ref struct ProtobufRuleFieldEntries
{
    private readonly int _count;
    private bool _capture;
    private ProtobufRuleFieldEntry[]? _entries;

    internal ProtobufRuleFieldEntries(int count, bool capture)
    {
        _count = count;
        _capture = capture;
        _entries = null;
    }

    internal void Capture(
        ReadOnlySpan<ProtobufFieldRulePlan> fields,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        for (var index = 0; index < fields.Length; index++)
            fields[index].ApplyDefault(values, sizes);
        if (!_capture)
            return;

        _capture = false;
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (field.Rules.RequiresMemberResolution && values.IsSet(field.RuntimeIndex))
            {
                _capture = true;
                break;
            }
        }
        if (!_capture)
            return;

        _entries = ArrayPool<ProtobufRuleFieldEntry>.Shared.Rent(_count);
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            ref var entry = ref _entries[index];
            entry.IsSet = values.IsSet(field.RuntimeIndex);
            if (!entry.IsSet)
                continue;
            entry.Value = field.GetRuleValue(values);
            entry.MessagePayload = field.GetMessagePayload(values);
            entry.CollectionSize = field.GetCollectionSize(sizes);
        }
    }

    internal bool TryGet(
        int index,
        ProtobufFieldRulePlan field,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes,
        out ValidationCelValue value,
        out ReadOnlyMemory<byte> messagePayload,
        out int collectionSize)
    {
        if (_capture)
        {
            ref readonly var entry = ref _entries![index];
            value = entry.Value;
            messagePayload = entry.MessagePayload;
            collectionSize = entry.CollectionSize;
            return entry.IsSet;
        }

        if (!values.IsSet(field.RuntimeIndex))
        {
            value = default;
            messagePayload = default;
            collectionSize = default;
            return false;
        }
        value = field.GetRuleValue(values);
        messagePayload = field.GetMessagePayload(values);
        collectionSize = field.GetCollectionSize(sizes);
        return true;
    }

    public void Dispose()
    {
        if (_entries is not null)
            ArrayPool<ProtobufRuleFieldEntry>.Shared.Return(_entries, clearArray: true);
        _entries = null;
    }
}

internal ref struct ProtobufSingularChildEntries
{
    private readonly int _capacity;
    private ProtobufSingularChildEntry[]? _entries;

    internal ProtobufSingularChildEntries(int capacity)
    {
        _capacity = capacity;
        _entries = null;
        Count = 0;
    }

    internal int Count { get; private set; }

    internal ref readonly ProtobufSingularChildEntry this[int index] => ref _entries![index];

    internal void Capture(
        ReadOnlySpan<ProtobufFieldRulePlan> fields,
        ValidationCelMemberValues values)
    {
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (field.Descriptor.IsRepeated ||
                field.Child is not { HasAnyRules: true } ||
                !values.IsSet(field.RuntimeIndex))
            {
                continue;
            }

            _entries ??= ArrayPool<ProtobufSingularChildEntry>.Shared.Rent(_capacity);
            ref var entry = ref _entries[Count++];
            entry.Field = field;
            entry.Payload = field.GetMessagePayload(values);
        }
    }

    public void Dispose()
    {
        if (_entries is not null)
            ArrayPool<ProtobufSingularChildEntry>.Shared.Return(_entries, clearArray: true);
        _entries = null;
        Count = 0;
    }
}

internal ref struct ProtobufMapEntries
{
    private ProtobufMapEntry[]? _entries;
    private int[]? _buckets;
    private int _bucketCount;

    internal ProtobufMapEntries(int capacity)
    {
        Count = 0;
        if (capacity == 0)
        {
            _entries = null;
            _buckets = null;
            _bucketCount = 0;
            return;
        }

        _entries = ArrayPool<ProtobufMapEntry>.Shared.Rent(capacity);
        var bucketCount = 4;
        while (bucketCount < capacity * 2)
            bucketCount <<= 1;
        _buckets = ArrayPool<int>.Shared.Rent(bucketCount);
        _bucketCount = bucketCount;
        Array.Clear(_buckets, 0, _bucketCount);
    }

    internal int Count { get; private set; }

    internal ref readonly ProtobufMapEntry this[int index] => ref _entries![index];

    internal void Add(
        ProtobufFieldRulePlan field,
        ValidationCelValue key,
        ReadOnlyMemory<byte> payload)
    {
        EnsureCapacity(Count + 1);
        var buckets = _buckets!;
        var bucket = (int)(Hash(field.RuntimeIndex, key) & (uint)(_bucketCount - 1));
        while (buckets[bucket] != 0)
        {
            ref var existing = ref _entries![buckets[bucket] - 1];
            if (existing.Field.RuntimeIndex == field.RuntimeIndex && KeysEqual(existing.Key, key))
            {
                existing.Key = key;
                existing.Payload = payload;
                return;
            }
            bucket = (bucket + 1) & (_bucketCount - 1);
        }

        ref var entry = ref _entries![Count];
        entry.Field = field;
        entry.Key = key;
        entry.Payload = payload;
        buckets[bucket] = ++Count;
    }

    private void EnsureCapacity(int requiredCount)
    {
        if (_entries is not null &&
            requiredCount <= _entries.Length &&
            requiredCount * 2 <= _bucketCount)
        {
            return;
        }

        var entries = ArrayPool<ProtobufMapEntry>.Shared.Rent(requiredCount);
        if (_entries is not null)
            _entries.AsSpan(0, Count).CopyTo(entries);
        var bucketCount = 4;
        while (bucketCount < requiredCount * 2)
            bucketCount <<= 1;
        var buckets = ArrayPool<int>.Shared.Rent(bucketCount);
        Array.Clear(buckets, 0, bucketCount);
        for (var index = 0; index < Count; index++)
        {
            ref readonly var entry = ref entries[index];
            var bucket = (int)(Hash(entry.Field.RuntimeIndex, entry.Key) & (uint)(bucketCount - 1));
            while (buckets[bucket] != 0)
                bucket = (bucket + 1) & (bucketCount - 1);
            buckets[bucket] = index + 1;
        }

        if (_entries is not null)
            ArrayPool<ProtobufMapEntry>.Shared.Return(_entries, clearArray: true);
        if (_buckets is not null)
            ArrayPool<int>.Shared.Return(_buckets);
        _entries = entries;
        _buckets = buckets;
        _bucketCount = bucketCount;
    }

    internal void ApplyUniqueSizes(
        ReadOnlySpan<ProtobufFieldRulePlan> fields,
        ValidationCelSizeValues sizes)
    {
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (field.Descriptor.IsMap)
                sizes.Set(field.RuntimeIndex + 1, 0);
        }
        for (var index = 0; index < Count; index++)
        {
            var fieldIndex = _entries![index].Field.RuntimeIndex + 1;
            _ = sizes.TryGet(fieldIndex, out var count);
            sizes.Set(fieldIndex, count + 1);
        }
    }

    private static uint Hash(int fieldIndex, ValidationCelValue key)
    {
        var hash = unchecked((uint)fieldIndex * 16777619u) ^ (uint)key.Kind;
        if (key.Kind == ValidationCelValueKind.Boolean)
            return (hash ^ (key.Boolean ? 1u : 0u)) * 16777619u;
        if (key.Kind == ValidationCelValueKind.Number)
            return (hash ^ unchecked((uint)key.Number.GetHashCode())) * 16777619u;
        var bytes = key.Utf8Literal.Span;
        for (var index = 0; index < bytes.Length; index++)
            hash = (hash ^ bytes[index]) * 16777619u;
        return hash;
    }

    private static bool KeysEqual(ValidationCelValue left, ValidationCelValue right)
    {
        if (left.Kind != right.Kind)
            return false;
        return left.Kind switch
        {
            ValidationCelValueKind.Boolean => left.Boolean == right.Boolean,
            ValidationCelValueKind.Number => left.Number == right.Number,
            ValidationCelValueKind.String or ValidationCelValueKind.Bytes =>
                left.Utf8Literal.Span.SequenceEqual(right.Utf8Literal.Span),
            _ => false
        };
    }

    public void Dispose()
    {
        if (_entries is not null)
            ArrayPool<ProtobufMapEntry>.Shared.Return(_entries, clearArray: true);
        if (_buckets is not null)
            ArrayPool<int>.Shared.Return(_buckets);
        _entries = null;
        _buckets = null;
        _bucketCount = 0;
        Count = 0;
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
    internal bool RequiresMemberResolution => _members is not null;

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
    private readonly int _oneofCount;
    private ProtobufMemberNode[] _nodes = [];

    private ProtobufMemberResolver(MessageDescriptor descriptor) =>
        _oneofCount = descriptor.Oneofs.Count;

    internal static ProtobufMemberResolver Create(
        MessageDescriptor descriptor,
        IReadOnlyList<byte[][]> paths,
        IReadOnlyCollection<int> usedIndexes)
    {
        var resolver = new ProtobufMemberResolver(descriptor);
        foreach (var memberIndex in usedIndexes)
            resolver.Add(descriptor, paths[memberIndex], memberIndex, depth: 0);
        resolver.Freeze();
        return resolver;
    }

    private void Freeze()
    {
        _nodes = [.. _fields.Values];
        for (var index = 0; index < _nodes.Length; index++)
        {
            _nodes[index].RuntimeIndex = index;
            _nodes[index].Child?.Freeze();
        }
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
        if (field.ContainingOneof is { } oneof)
        {
            for (var index = 0; index < oneof.Fields.Count; index++)
            {
                var sibling = oneof.Fields[index];
                _fields.TryAdd(sibling.FieldNumber, new ProtobufMemberNode(sibling));
            }
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
        node.Child ??= new ProtobufMemberResolver(field.MessageType);
        node.Child.Add(field.MessageType, path, memberIndex, depth + 1);
    }

    internal void Resolve(
        ReadOnlyMemory<byte> payload,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        var nestedPayloads = new ProtobufNestedMemberPayloads(_nodes.Length);
        Span<int> initialOneofs = stackalloc int[8];
        var oneofs = new ProtobufOneofSelections(_oneofCount, initialOneofs);
        var reader = new ProtobufValidationWireReader(payload);
        try
        {
            while (reader.TryRead(out var field))
            {
                if (!_fields.TryGetValue(field.Number, out var node))
                    continue;

                var previous = oneofs.Select(node.OneofIndex, node.RuntimeIndex);
                if (previous >= 0 && previous != node.RuntimeIndex)
                    _nodes[previous].Clear(values, ref nestedPayloads);

                node.Observe(field, values, sizes, ref nestedPayloads);
            }

            for (var index = 0; index < _nodes.Length; index++)
            {
                var node = _nodes[index];
                node.ApplyDefault(values, sizes);
                if (node.Child is not null && nestedPayloads.TryGet(index, out var childPayload))
                    node.Child.Resolve(childPayload, values, sizes);
            }
        }
        finally
        {
            oneofs.Dispose();
            nestedPayloads.Dispose();
        }
    }
}

internal sealed class ProtobufMemberNode(FieldDescriptor descriptor)
{
    internal int MemberIndex { get; set; } = -1;
    internal int RuntimeIndex { get; set; }
    internal int OneofIndex { get; } = descriptor.ContainingOneof?.Index ?? -1;
    internal ProtobufMemberResolver? Child { get; set; }

    internal void Observe(
        ProtobufValidationWireField field,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes,
        ref ProtobufNestedMemberPayloads nestedPayloads)
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
                var decoded = ProtobufValidationValueDecoder.Decode(descriptor, field);
                if (descriptor.FieldType is FieldType.Message or FieldType.Group &&
                    values.IsSet(MemberIndex))
                {
                    decoded = decoded with
                    {
                        Utf8Literal = nestedPayloads.Merge(
                            RuntimeIndex,
                            values.GetValue(MemberIndex, default).Utf8Literal,
                            decoded.Utf8Literal)
                    };
                }
                values.SetValue(MemberIndex, decoded);
            }
        }

        if (Child is not null && field.WireType == ProtobufWireType.LengthDelimited)
            nestedPayloads.Observe(RuntimeIndex, field.Payload);
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

    internal void Clear(
        ValidationCelMemberValues values,
        ref ProtobufNestedMemberPayloads nestedPayloads)
    {
        if (MemberIndex >= 0)
            values.Clear(MemberIndex);
        nestedPayloads.Clear(RuntimeIndex);
    }
}

internal ref struct ProtobufOneofSelections
{
    private Span<int> _selections;
    private int[]? _rented;

    internal ProtobufOneofSelections(int count, Span<int> initial)
    {
        if (count <= initial.Length)
        {
            _selections = initial[..count];
            _rented = null;
        }
        else
        {
            _rented = ArrayPool<int>.Shared.Rent(count);
            _selections = _rented.AsSpan(0, count);
        }
        _selections.Fill(-1);
    }

    internal int Select(int oneofIndex, int runtimeIndex)
    {
        if (oneofIndex < 0)
            return -1;
        ref var selected = ref _selections[oneofIndex];
        var previous = selected;
        selected = runtimeIndex;
        return previous;
    }

    public void Dispose()
    {
        if (_rented is not null)
            ArrayPool<int>.Shared.Return(_rented);
        _rented = null;
        _selections = default;
    }
}

internal ref struct ProtobufNestedMemberPayloads
{
    private readonly int _count;
    private ReadOnlyMemory<byte>[]? _payloads;
    private byte[]? _observed;
    private ProtobufMergedMessageBuffers _childMerged;
    private ProtobufMergedMessageBuffers _memberMerged;

    internal ProtobufNestedMemberPayloads(int count)
    {
        _count = count;
        _payloads = null;
        _observed = null;
        _childMerged = new ProtobufMergedMessageBuffers(count);
        _memberMerged = new ProtobufMergedMessageBuffers(count);
    }

    internal void Observe(int index, ReadOnlyMemory<byte> payload)
    {
        EnsurePayloads();
        ref var current = ref _payloads![index];
        current = _observed![index] == 0
            ? payload
            : _childMerged.Merge(index, current, payload);
        _observed[index] = 1;
    }

    internal ReadOnlyMemory<byte> Merge(
        int index,
        ReadOnlyMemory<byte> previous,
        ReadOnlyMemory<byte> current) => _memberMerged.Merge(index, previous, current);

    internal void Clear(int index)
    {
        if (_payloads is not null)
        {
            _payloads[index] = default;
            _observed![index] = 0;
        }
        _childMerged.Clear(index);
        _memberMerged.Clear(index);
    }

    internal bool TryGet(int index, out ReadOnlyMemory<byte> payload)
    {
        if (_payloads is null || _observed![index] == 0)
        {
            payload = default;
            return false;
        }
        payload = _payloads[index];
        return true;
    }

    private void EnsurePayloads()
    {
        if (_payloads is not null)
            return;
        _payloads = ArrayPool<ReadOnlyMemory<byte>>.Shared.Rent(_count);
        Array.Clear(_payloads, 0, _count);
        _observed = ArrayPool<byte>.Shared.Rent(_count);
        Array.Clear(_observed, 0, _count);
    }

    public void Dispose()
    {
        _childMerged.Dispose();
        _memberMerged.Dispose();
        if (_payloads is not null)
            ArrayPool<ReadOnlyMemory<byte>>.Shared.Return(_payloads, clearArray: true);
        if (_observed is not null)
            ArrayPool<byte>.Shared.Return(_observed);
        _payloads = null;
        _observed = null;
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
