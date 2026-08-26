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

    internal bool HasAnyRules => _root.HasAnyRules;

    internal void Validate(ReadOnlyMemory<byte> payload, int schemaId, bool failFast)
    {
        List<ValidationRuleError>? violations = null;
        var path = new AvroValidationPath(t_pathBuffer ??= new char[256]);
        var reader = new AvroValidationReader(payload);
        var valueResolutionDepth = CompiledValidationRule.ValueResolutionDepth;
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
            if (CompiledValidationRule.ValueResolutionDepth != valueResolutionDepth)
                CompiledValidationRule.RestoreValueResolutionDepth(valueResolutionDepth);
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
    private enum ValidationStrategy : byte
    {
        Standard,
        AggregateRoot,
        AggregateRootAndMembers,
        DeferredRecord,
        DeferredMembers
    }

    private readonly AvroSchema _schema;
    private readonly AvroSchema _ruleSchema;
    private readonly ReadOnlyMemory<byte>[]? _enumSymbols;
    private AvroCompiledRuleSet _schemaRules = AvroCompiledRuleSet.Empty;
    private AvroFieldRulePlan[] _fields = [];
    private AvroValueRulePlan[] _children = [];
    private int[] _deferredSchemaRuleFieldIndexes = [];
    private bool _hasNestedRules;
    private ValidationStrategy _validationStrategy;

    private AvroValueRulePlan(AvroSchema schema)
    {
        _schema = Unwrap(schema);
        _ruleSchema = schema;
        _enumSymbols = AvroValidationValueDecoder.EncodeEnumSymbols(_schema);
    }

    internal bool HasAnyRules { get; private set; }

    internal static AvroValueRulePlan Create(
        AvroSchema schema,
        Dictionary<AvroSchema, AvroValueRulePlan> plans)
    {
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
            AvroInlineRuleParser.ReadRules(_ruleSchema),
            _schema);

        switch (_schema)
        {
            case global::Avro.RecordSchema record:
                _fields = new AvroFieldRulePlan[record.Fields.Count];
                for (var index = 0; index < record.Fields.Count; index++)
                {
                    var field = record.Fields[index];
                    var child = Create(field.Schema, plans);
                    _fields[index] = new AvroFieldRulePlan(
                        field,
                        AvroCompiledRuleSet.Compile(
                            AvroInlineRuleParser.ReadRules(field),
                            field.Schema),
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
        {
            if (_fields[index].Rules.IsEmpty)
                continue;
            HasAnyRules = true;
            _hasNestedRules = true;
        }
    }

    internal static void Complete(Dictionary<AvroSchema, AvroValueRulePlan> plans)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var plan in plans.Values)
            {
                if (!plan._hasNestedRules)
                {
                    for (var index = 0; index < plan._fields.Length; index++)
                    {
                        if (!plan._fields[index].Child.HasAnyRules)
                            continue;
                        plan._hasNestedRules = true;
                        break;
                    }
                    for (var index = 0; !plan._hasNestedRules && index < plan._children.Length; index++)
                        plan._hasNestedRules = plan._children[index].HasAnyRules;
                }
                if (!plan.HasAnyRules && plan._hasNestedRules)
                {
                    plan.HasAnyRules = true;
                    changed = true;
                }
            }
        }

        foreach (var plan in plans.Values)
        {
            plan.InitializeDeferredSchemaRuleFields();
            plan._validationStrategy = plan.GetValidationStrategy();
        }
    }

    private ValidationStrategy GetValidationStrategy()
    {
        if (_schemaRules.UsesRootValue
            && _schemaRules.HasMembers
            && _schema is global::Avro.RecordSchema or global::Avro.MapSchema)
        {
            return ValidationStrategy.AggregateRootAndMembers;
        }
        if (!_hasNestedRules || _schemaRules.IsEmpty)
            return ValidationStrategy.Standard;
        if (_schemaRules.UsesRootValue
            && !_schemaRules.HasMembers
            && _schema is global::Avro.RecordSchema or global::Avro.ArraySchema or global::Avro.MapSchema)
        {
            return ValidationStrategy.AggregateRoot;
        }
        if (!_schemaRules.UsesRootValue && _schemaRules.HasMembers)
        {
            return _schema switch
            {
                global::Avro.RecordSchema => ValidationStrategy.DeferredRecord,
                global::Avro.MapSchema or global::Avro.UnionSchema => ValidationStrategy.DeferredMembers,
                _ => ValidationStrategy.Standard
            };
        }
        return ValidationStrategy.Standard;
    }

    private void InitializeDeferredSchemaRuleFields()
    {
        var lastMemberFieldIndex = _schemaRules.LastRecordMemberIndex;
        if (lastMemberFieldIndex <= 0)
            return;

        List<int>? deferred = null;
        for (var index = 0; index < lastMemberFieldIndex; index++)
        {
            var field = _fields[index];
            if (field.Rules.IsEmpty && !field.Child.HasAnyRules)
                continue;
            (deferred ??= []).Add(index);
        }
        if (deferred is not null)
            _deferredSchemaRuleFieldIndexes = [.. deferred];
    }

    internal ValidationCelValue Validate(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        if (!HasAnyRules)
            return ReadValue(ref reader);

        if (_validationStrategy != ValidationStrategy.Standard)
        {
            if (_validationStrategy == ValidationStrategy.AggregateRoot)
            {
                return ValidateAggregateWithRootRules(
                    ref reader,
                    now,
                    failFast,
                    ref violations,
                    ref path);
            }

            if (_validationStrategy == ValidationStrategy.AggregateRootAndMembers)
            {
                return ValidateAggregateWithRootAndMemberRules(
                    ref reader,
                    now,
                    failFast,
                    ref violations,
                    ref path);
            }

            if (_validationStrategy == ValidationStrategy.DeferredMembers)
            {
                return ValidateWithDeferredMemberRules(
                    ref reader,
                    now,
                    failFast,
                    ref violations,
                    ref path);
            }

            return ValidateRecordWithDeferredNestedRules(
                ref reader,
                now,
                failFast,
                ref violations,
                ref path);
        }

        var value = ValidationCelValue.Missing;
        var preview = default(AvroValidationReader);
        if (!_schemaRules.IsEmpty)
        {
            var start = reader.Position;
            preview = reader;
            if (_schemaRules.UsesRootValue)
            {
                value = ReadValue(ref preview);
                var payload = preview.Source.Slice(start, preview.Position - start);
                var rootSize = value.SizeIndex == 0
                    ? AvroValidationValueDecoder.Count(_schema, payload)
                    : -1;
                _schemaRules.Evaluate(
                    value,
                    payload,
                    now,
                    failFast,
                    ref violations,
                    ref path,
                    rootSize);
            }
            else
            {
                _schemaRules.EvaluateWithoutRoot(
                    ref preview,
                    _schema,
                    now,
                    failFast,
                    ref violations,
                    ref path);
            }
            if (failFast && violations is not null)
            {
                reader = preview;
                return value;
            }
            if (!_hasNestedRules)
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
                _ = ValidateArray(ref reader, now, failFast, ref violations, ref path);
                break;
            case global::Avro.MapSchema:
                _ = ValidateMap(ref reader, now, failFast, ref violations, ref path);
                break;
            case global::Avro.UnionSchema:
                ValidateUnion(ref reader, now, failFast, ref violations, ref path);
                break;
            default:
                if (_schemaRules.IsEmpty)
                    value = ReadValue(ref reader);
                else
                    reader = preview;
                break;
        }
        return value;
    }

    private ValidationCelValue ValidateWithDeferredMemberRules(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        var resolution = _schemaRules.BeginMemberResolution();
        var start = reader.Position;
        List<ValidationRuleError>? nestedViolations = null;
        switch (_schema)
        {
            case global::Avro.MapSchema:
                ValidateMapWithMemberResolution(
                    ref reader,
                    resolution,
                    now,
                    ref nestedViolations,
                    ref path);
                break;
            case global::Avro.UnionSchema:
                ValidateUnionWithMemberResolution(
                    ref reader,
                    resolution,
                    now,
                    ref nestedViolations,
                    ref path);
                break;
            default:
                throw new InvalidOperationException("Deferred member validation requires a map or union schema.");
        }

        _schemaRules.EvaluateResolvedWithoutRoot(
            resolution,
            reader.Position - start,
            now,
            failFast,
            ref violations,
            ref path);
        resolution.Dispose();
        AppendNestedViolations(nestedViolations, failFast, ref violations);
        return ValidationCelValue.Missing;
    }

    private ValidationCelValue ValidateAggregateWithRootAndMemberRules(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        var resolution = _schemaRules.BeginMemberResolution();
        var start = reader.Position;
        List<ValidationRuleError>? nestedViolations = null;
        int rootSize;
        ValidationCelValueKind valueKind;
        switch (_schema)
        {
            case global::Avro.RecordSchema:
                ValidateRecordWithMemberResolution(
                    ref reader,
                    resolution,
                    now,
                    ref nestedViolations,
                    ref path);
                rootSize = _fields.Length;
                valueKind = ValidationCelValueKind.Object;
                break;
            case global::Avro.MapSchema:
                rootSize = ValidateMapWithMemberResolution(
                    ref reader,
                    resolution,
                    now,
                    ref nestedViolations,
                    ref path);
                valueKind = ValidationCelValueKind.Object;
                break;
            default:
                throw new InvalidOperationException(
                    "Mixed root/member validation requires a record or map schema.");
        }

        var payload = reader.Source.Slice(start, reader.Position - start);
        var value = ValidationCelValue.FromCollection(valueKind, payload, sizeIndex: 0);
        _schemaRules.EvaluateResolved(
            value,
            payload,
            resolution,
            rootSize,
            now,
            failFast,
            ref violations,
            ref path);
        resolution.Dispose();
        AppendNestedViolations(nestedViolations, failFast, ref violations);
        return value;
    }

    private void ValidateRecordWithMemberResolution(
        ref AvroValidationReader reader,
        AvroMemberResolution resolution,
        long now,
        ref List<ValidationRuleError>? nestedViolations,
        scoped ref AvroValidationPath path)
    {
        for (var index = 0; index < _fields.Length; index++)
        {
            var start = reader.Position;
            var value = ValidateRecordFieldAndCapture(
                _fields[index],
                ref reader,
                now,
                ref nestedViolations,
                ref path,
                out var size);
            _schemaRules.ResolveRecordField(
                index,
                reader.Source.Slice(start, reader.Position - start),
                value,
                size,
                resolution);
        }
    }

    private static ValidationCelValue ValidateRecordFieldAndCapture(
        AvroFieldRulePlan field,
        ref AvroValidationReader reader,
        long now,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path,
        out int size)
    {
        if (!field.Rules.IsEmpty)
        {
            if (CanFuseFieldRules(field))
            {
                var fieldMark = path.Length;
                path.AppendField(field.Field.Name);
                var capturedValue = EvaluateFieldRulesWithNestedTraversal(
                    field,
                    ref reader,
                    now,
                    failFast: false,
                    ref violations,
                    ref path,
                    out size);
                path.Truncate(fieldMark);
                return capturedValue;
            }

            ValidateDeferredRecordField(
                field,
                ref reader,
                now,
                failFast: false,
                ref violations,
                ref path);
            size = -1;
            return ValidationCelValue.Missing;
        }

        var mark = path.Length;
        path.AppendField(field.Field.Name);
        var value = field.Child.ValidateAndCapture(
            ref reader,
            now,
            ref violations,
            ref path,
            out size);
        path.Truncate(mark);
        return value;
    }

    private static bool CanFuseFieldRules(AvroFieldRulePlan field) =>
        field.Child.HasAnyRules &&
        field.Child._schemaRules.IsEmpty &&
        field.Rules.UsesRootValue &&
        !field.Rules.HasMembers &&
        field.Child._schema is global::Avro.RecordSchema or global::Avro.ArraySchema or global::Avro.MapSchema;

    private static ValidationCelValue EvaluateFieldRulesWithNestedTraversal(
        AvroFieldRulePlan field,
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path,
        out int size)
    {
        var start = reader.Position;
        List<ValidationRuleError>? nestedViolations = null;
        var value = field.Child.ValidateAndCapture(
            ref reader,
            now,
            ref nestedViolations,
            ref path,
            out size);
        var payload = reader.Source.Slice(start, reader.Position - start);
        field.Rules.Evaluate(
            value,
            payload,
            now,
            failFast,
            ref violations,
            ref path,
            size);
        AppendNestedViolations(nestedViolations, failFast, ref violations);
        return value;
    }

    private ValidationCelValue ValidateAndCapture(
        ref AvroValidationReader reader,
        long now,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path,
        out int size)
    {
        if (_schemaRules.IsEmpty)
        {
            var start = reader.Position;
            ValidationCelValueKind kind;
            switch (_schema)
            {
                case global::Avro.RecordSchema:
                    ValidateRecord(ref reader, now, failFast: false, ref violations, ref path);
                    size = _fields.Length;
                    kind = ValidationCelValueKind.Object;
                    break;
                case global::Avro.ArraySchema:
                    size = ValidateArray(ref reader, now, failFast: false, ref violations, ref path);
                    kind = ValidationCelValueKind.Array;
                    break;
                case global::Avro.MapSchema:
                    size = ValidateMap(ref reader, now, failFast: false, ref violations, ref path);
                    kind = ValidationCelValueKind.Object;
                    break;
                default:
                    size = -1;
                    return Validate(
                        ref reader,
                        now,
                        failFast: false,
                        ref violations,
                        ref path);
            }
            return ValidationCelValue.FromCollection(
                kind,
                reader.Source.Slice(start, reader.Position - start),
                sizeIndex: 0);
        }

        var valueStart = reader.Position;
        var value = Validate(
            ref reader,
            now,
            failFast: false,
            ref violations,
            ref path);
        size = value.SizeIndex == 0
            ? AvroValidationValueDecoder.Count(
                _schema,
                reader.Source.Slice(valueStart, reader.Position - valueStart))
            : -1;
        return value;
    }

    private void ValidateUnionWithMemberResolution(
        ref AvroValidationReader reader,
        AvroMemberResolution resolution,
        long now,
        ref List<ValidationRuleError>? nestedViolations,
        scoped ref AvroValidationPath path)
    {
        var union = (global::Avro.UnionSchema)_schema;
        var branch = reader.ReadLong();
        if ((ulong)branch >= (ulong)union.Count)
            throw InvalidPayload($"invalid union index {branch}");

        var payloadStart = reader.Position;
        _ = _children[(int)branch].Validate(
            ref reader,
            now,
            failFast: false,
            ref nestedViolations,
            ref path);
        _schemaRules.ResolveUnionBranch(
            (int)branch,
            reader.Source.Slice(payloadStart, reader.Position - payloadStart),
            resolution);
    }

    private int ValidateMapWithMemberResolution(
        ref AvroValidationReader reader,
        AvroMemberResolution resolution,
        long now,
        ref List<ValidationRuleError>? nestedViolations,
        scoped ref AvroValidationPath path)
    {
        var valuePlan = _children[0];
        var itemCount = 0;
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return itemCount;
            for (long index = 0; index < count; index++)
            {
                itemCount = checked(itemCount + 1);
                var key = reader.ReadLengthPrefixed();
                var valueStart = reader.Position;
                var mark = path.Length;
                path.AppendMapKey(key.Span);
                var value = valuePlan.ValidateAndCapture(
                    ref reader,
                    now,
                    ref nestedViolations,
                    ref path,
                    out var size);
                path.Truncate(mark);
                _schemaRules.ResolveMapEntry(
                    key,
                    reader.Source.Slice(valueStart, reader.Position - valueStart),
                    value,
                    size,
                    resolution);
            }
        }
    }

    private static void AppendNestedViolations(
        List<ValidationRuleError>? nestedViolations,
        bool failFast,
        ref List<ValidationRuleError>? violations)
    {
        if (nestedViolations is null || (failFast && violations is not null))
            return;
        violations ??= new List<ValidationRuleError>(failFast ? 1 : nestedViolations.Count);
        if (failFast)
        {
            violations.Add(nestedViolations[0]);
            return;
        }
        for (var index = 0; index < nestedViolations.Count; index++)
            violations.Add(nestedViolations[index]);
    }

    private ValidationCelValue ValidateAggregateWithRootRules(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        var start = reader.Position;
        List<ValidationRuleError>? nestedViolations = null;
        int rootSize;
        ValidationCelValueKind valueKind;
        switch (_schema)
        {
            case global::Avro.RecordSchema:
                ValidateRecord(ref reader, now, failFast: false, ref nestedViolations, ref path);
                rootSize = _fields.Length;
                valueKind = ValidationCelValueKind.Object;
                break;
            case global::Avro.ArraySchema:
                rootSize = ValidateArray(ref reader, now, failFast: false, ref nestedViolations, ref path);
                valueKind = ValidationCelValueKind.Array;
                break;
            case global::Avro.MapSchema:
                rootSize = ValidateMap(ref reader, now, failFast: false, ref nestedViolations, ref path);
                valueKind = ValidationCelValueKind.Object;
                break;
            default:
                throw new InvalidOperationException("Root aggregate validation requires a record, array, or map schema.");
        }

        var payload = reader.Source.Slice(start, reader.Position - start);
        var value = ValidationCelValue.FromCollection(valueKind, payload, sizeIndex: 0);
        _schemaRules.Evaluate(
            value,
            payload,
            now,
            failFast,
            ref violations,
            ref path,
            rootSize);
        AppendNestedViolations(nestedViolations, failFast, ref violations);
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
            if (field.Rules.IsEmpty)
            {
                _ = field.Child.Validate(ref reader, now, failFast, ref violations, ref path);
            }
            else if (CanFuseFieldRules(field))
            {
                _ = EvaluateFieldRulesWithNestedTraversal(
                    field,
                    ref reader,
                    now,
                    failFast,
                    ref violations,
                    ref path,
                    out _);
            }
            else
            {
                var fieldStart = reader.Position;
                var preview = reader;
                var value = field.Child.ReadValue(ref preview);
                var payload = preview.Source.Slice(fieldStart, preview.Position - fieldStart);
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

                if ((failFast && violations is not null) || !field.Child.HasAnyRules)
                    reader = preview;
                else
                    _ = field.Child.Validate(ref reader, now, failFast, ref violations, ref path);
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

    private int ValidateArray(
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
                return itemIndex;
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
                    return itemIndex;
                }
            }
        }
    }

    private int ValidateMap(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        var valuePlan = _children[0];
        var itemCount = 0;
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return itemCount;
            for (long index = 0; index < count; index++)
            {
                itemCount = checked(itemCount + 1);
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
                    return itemCount;
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

    private ValidationCelValue ValidateRecordWithDeferredNestedRules(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        // Resolve only the prefix needed by the record rule, evaluate that rule,
        // then validate deferred fields in wire order. This preserves parent-first
        // failures without rescanning the complete record on valid payloads.
        var lastMemberFieldIndex = _schemaRules.LastRecordMemberIndex;
        if (lastMemberFieldIndex < 0)
        {
            var resolution = _schemaRules.BeginMemberResolution();
            _schemaRules.EvaluateResolvedWithoutRoot(
                resolution,
                payloadLength: 0,
                now,
                failFast,
                ref violations,
                ref path);
            resolution.Dispose();
            if (failFast && violations is not null)
            {
                AvroValidationValueDecoder.Skip(_schema, ref reader);
                return ValidationCelValue.Missing;
            }

            ValidateRecord(ref reader, now, failFast, ref violations, ref path);
            return ValidationCelValue.Missing;
        }

        var deferredIndexes = _deferredSchemaRuleFieldIndexes;
        var offsetCount = deferredIndexes.Length * 2;
        int[]? rentedOffsets = null;
        Span<int> offsets = offsetCount <= 128
            ? stackalloc int[offsetCount]
            : (rentedOffsets = ArrayPool<int>.Shared.Rent(offsetCount));
        try
        {
            var resolution = _schemaRules.BeginMemberResolution();
            var recordStart = reader.Position;
            var lastFieldPayload = _schemaRules.ResolveRecordPrefix(
                ref reader,
                deferredIndexes,
                offsets,
                resolution);

            _schemaRules.EvaluateResolvedWithoutRoot(
                resolution,
                reader.Position - recordStart,
                now,
                failFast,
                ref violations,
                ref path);
            resolution.Dispose();
            if (failFast && violations is not null)
            {
                SkipRecordFields(ref reader, lastMemberFieldIndex + 1);
                return ValidationCelValue.Missing;
            }

            for (var index = 0; index < deferredIndexes.Length; index++)
            {
                var fieldReader = new AvroValidationReader(
                    reader.Source.Slice(offsets[index * 2], offsets[index * 2 + 1]));
                ValidateDeferredRecordField(
                    _fields[deferredIndexes[index]],
                    ref fieldReader,
                    now,
                    failFast,
                    ref violations,
                    ref path);
                if (failFast && violations is not null)
                {
                    SkipRecordFields(ref reader, lastMemberFieldIndex + 1);
                    return ValidationCelValue.Missing;
                }
            }

            var lastField = _fields[lastMemberFieldIndex];
            if (!lastField.Rules.IsEmpty || lastField.Child.HasAnyRules)
            {
                var fieldReader = new AvroValidationReader(
                    reader.Source.Slice(lastFieldPayload.Start, lastFieldPayload.Length));
                ValidateDeferredRecordField(
                    lastField,
                    ref fieldReader,
                    now,
                    failFast,
                    ref violations,
                    ref path);
                if (failFast && violations is not null)
                {
                    SkipRecordFields(ref reader, lastMemberFieldIndex + 1);
                    return ValidationCelValue.Missing;
                }
            }

            for (var index = lastMemberFieldIndex + 1; index < _fields.Length; index++)
            {
                ValidateDeferredRecordField(
                    _fields[index],
                    ref reader,
                    now,
                    failFast,
                    ref violations,
                    ref path);
                if (failFast && violations is not null)
                {
                    SkipRecordFields(ref reader, index + 1);
                    break;
                }
            }
            return ValidationCelValue.Missing;
        }
        finally
        {
            if (rentedOffsets is not null)
                ArrayPool<int>.Shared.Return(rentedOffsets);
        }
    }

    private void SkipRecordFields(ref AvroValidationReader reader, int startIndex)
    {
        for (var index = startIndex; index < _fields.Length; index++)
            AvroValidationValueDecoder.Skip(_fields[index].Field.Schema, ref reader);
    }

    private static void ValidateDeferredRecordField(
        AvroFieldRulePlan field,
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        var mark = path.Length;
        path.AppendField(field.Field.Name);
        if (field.Rules.IsEmpty)
        {
            _ = field.Child.Validate(ref reader, now, failFast, ref violations, ref path);
        }
        else if (CanFuseFieldRules(field))
        {
            _ = EvaluateFieldRulesWithNestedTraversal(
                field,
                ref reader,
                now,
                failFast,
                ref violations,
                ref path,
                out _);
        }
        else
        {
            var fieldStart = reader.Position;
            var preview = reader;
            var value = field.Child.ReadValue(ref preview);
            var payload = preview.Source.Slice(fieldStart, preview.Position - fieldStart);
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
            if ((failFast && violations is not null) || !field.Child.HasAnyRules)
                reader = preview;
            else
                _ = field.Child.Validate(ref reader, now, failFast, ref violations, ref path);
        }
        path.Truncate(mark);
    }

    private ValidationCelValue ReadValue(ref AvroValidationReader reader)
    {
        if (_schema is not global::Avro.UnionSchema union)
            return AvroValidationValueDecoder.Read(_schema, _enumSymbols, ref reader);

        var branch = reader.ReadLong();
        if ((ulong)branch >= (ulong)union.Count)
            throw InvalidPayload($"invalid union index {branch}");
        return _children[(int)branch].ReadValue(ref reader);
    }

    internal static AvroSchema Unwrap(AvroSchema schema) =>
        schema is global::Avro.LogicalSchema logical ? logical.BaseSchema : schema;

    private static SchemaRegistryRuleException InvalidPayload(string reason) =>
        new($"Could not evaluate Avro validation rules: {reason}.");
}

internal sealed record AvroFieldRulePlan(
    global::Avro.Field Field,
    AvroCompiledRuleSet Rules,
    AvroValueRulePlan Child);

internal readonly record struct AvroMemberResolution(
    ValidationCelMemberValues Members,
    ValidationCelSizeValues Sizes,
    ValidationCelValueResolution ValueResolution) : IDisposable
{
    public void Dispose() => ValueResolution.Dispose();
}

internal readonly record struct AvroFieldPayload(int Start, int Length);

internal sealed class AvroCompiledRuleSet
{
    internal static AvroCompiledRuleSet Empty { get; } = new(
        [], null, false, false, false, false, 0, null, new AvroAggregateEqualityComparerFactory());

    private readonly CompiledValidationRule[] _rules;
    private readonly AvroMemberResolver? _members;
    private readonly AvroAggregateEqualityComparer? _rootAggregateComparer;
    private readonly AvroAggregateEqualityComparer?[]? _rootUnionComparers;
    private readonly bool _usesCachedEquality;
    private readonly bool _usesRootAggregateEquality;
    private readonly int _memberCount;
    private readonly int _lastRecordMemberIndex;

    private AvroCompiledRuleSet(
        CompiledValidationRule[] rules,
        AvroMemberResolver? members,
        bool usesRootValue,
        bool usesSize,
        bool usesCachedEquality,
        bool usesRootAggregateEquality,
        int memberCount,
        AvroSchema? valueSchema,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        _rules = rules;
        _members = members;
        UsesRootValue = usesRootValue;
        UsesSize = usesSize;
        _usesCachedEquality = usesCachedEquality;
        _usesRootAggregateEquality = usesRootAggregateEquality;
        _memberCount = memberCount;
        _lastRecordMemberIndex = members?.LastRecordMemberIndex ?? -1;
        if (!usesRootAggregateEquality || valueSchema is null)
            return;

        valueSchema = AvroValueRulePlan.Unwrap(valueSchema);
        _rootAggregateComparer = aggregateComparerFactory.Create(valueSchema);
        if (valueSchema is not global::Avro.UnionSchema union)
            return;

        _rootUnionComparers = new AvroAggregateEqualityComparer?[union.Count];
        for (var index = 0; index < union.Count; index++)
        {
            _rootUnionComparers[index] = aggregateComparerFactory.Create(
                AvroValueRulePlan.Unwrap(union[index]));
        }
    }

    internal bool IsEmpty => _rules.Length == 0;
    internal bool UsesRootValue { get; }
    internal bool UsesSize { get; }
    internal bool HasMembers => _members is not null;
    internal int LastRecordMemberIndex => _lastRecordMemberIndex;

    internal static AvroCompiledRuleSet Compile(
        IReadOnlyList<ValidationRule> rules,
        AvroSchema valueSchema)
    {
        if (rules.Count == 0)
            return Empty;

        var memberIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var memberPaths = new List<byte[][]>();
        var usedMemberIndexes = new HashSet<int>();
        var compiled = new CompiledValidationRule[rules.Count];
        var usesSize = false;
        var usesRootValue = false;
        var usesCachedEquality = false;
        var usesRootAggregateEquality = false;
        for (var index = 0; index < rules.Count; index++)
        {
            var rule = CompiledValidationRule.Compile(
                rules[index],
                memberIndexes,
                memberPaths,
                usedMemberIndexes);
            compiled[index] = rule;
            usesRootValue |= rule.UsesRootValue;
            usesSize |= rule.UsesSize;
            usesCachedEquality |= rule.UsesCachedEquality;
            usesRootAggregateEquality |= rule.UsesRootAggregateEquality;
        }

        var aggregateComparerFactory = new AvroAggregateEqualityComparerFactory();
        var members = usedMemberIndexes.Count == 0
            ? null
            : AvroMemberResolver.Create(
                valueSchema,
                memberPaths,
                usedMemberIndexes,
                aggregateComparerFactory);
        return new AvroCompiledRuleSet(
            compiled,
            members,
            usesRootValue,
            usesSize,
            usesCachedEquality,
            usesRootAggregateEquality,
            memberPaths.Count,
            valueSchema,
            aggregateComparerFactory);
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
        var isNested = CompiledValidationRule.HasActiveValueResolution;
        var valueResolution = isNested
            ? CompiledValidationRule.BeginValueResolution()
            : default;
        var memberValues = _memberCount == 0
            ? default
            : isNested
                ? CompiledValidationRule.GetMemberValues(_memberCount, valueResolution)
                : CompiledValidationRule.GetMemberValues(_memberCount);
        var sizes = UsesSize || _members is not null || rootSize >= 0
            ? isNested
                ? CompiledValidationRule.GetSizeValues(_memberCount + 1, valueResolution)
                : CompiledValidationRule.GetSizeValues(_memberCount + 1)
            : default;
        if (rootSize >= 0)
            sizes.Set(0, rootSize);
        _members?.Resolve(payload, memberValues, sizes);
        var equalityGeneration = _usesCachedEquality
            ? CompiledValidationRule.BeginEqualityResolution()
            : 0;
        var rootAggregateComparer = _usesRootAggregateEquality
            ? GetRootAggregateComparer(payload)
            : null;

        ValidationCelStrings.Begin(_memberCount + 1, payload.Length);
        try
        {
            for (var index = 0; index < _rules.Length; index++)
            {
                var rule = _rules[index];
                try
                {
                    var result = rule.Evaluate(
                        value,
                        now,
                        memberValues,
                        sizes,
                        equalityGeneration,
                        rootAggregateComparer);
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
                    break;
            }
        }
        finally
        {
            ValidationCelStrings.End();
        }
        if (isNested)
            valueResolution.Dispose();
    }

    internal void EvaluateResolved(
        ValidationCelValue value,
        ReadOnlyMemory<byte> payload,
        AvroMemberResolution resolution,
        int rootSize,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        resolution.Sizes.Set(0, rootSize);
        var equalityGeneration = _usesCachedEquality
            ? CompiledValidationRule.BeginEqualityResolution()
            : 0;
        var rootAggregateComparer = _usesRootAggregateEquality
            ? GetRootAggregateComparer(payload)
            : null;

        ValidationCelStrings.Begin(_memberCount + 1, payload.Length);
        try
        {
            for (var index = 0; index < _rules.Length; index++)
            {
                var rule = _rules[index];
                try
                {
                    var result = rule.Evaluate(
                        value,
                        now,
                        resolution.Members,
                        resolution.Sizes,
                        equalityGeneration,
                        rootAggregateComparer);
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

    internal void EvaluateWithoutRoot(
        ref AvroValidationReader reader,
        AvroSchema valueSchema,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        var resolution = BeginMemberResolution();
        var start = reader.Position;
        if (_members is null)
            AvroValidationValueDecoder.Skip(valueSchema, ref reader);
        else
            _members.Resolve(ref reader, resolution.Members, resolution.Sizes);

        EvaluateResolvedWithoutRoot(
            resolution,
            reader.Position - start,
            now,
            failFast,
            ref violations,
            ref path);
        resolution.Dispose();
    }

    internal AvroMemberResolution BeginMemberResolution()
    {
        var valueResolution = CompiledValidationRule.BeginValueResolution();
        return new AvroMemberResolution(
            _memberCount == 0
                ? default
                : CompiledValidationRule.GetMemberValues(_memberCount, valueResolution),
            UsesSize || _members is not null
                ? CompiledValidationRule.GetSizeValues(_memberCount + 1, valueResolution)
                : default,
            valueResolution);
    }

    internal AvroFieldPayload ResolveRecordPrefix(
        ref AvroValidationReader reader,
        int[] deferredFieldIndexes,
        scoped Span<int> deferredFieldOffsets,
        AvroMemberResolution resolution) =>
        _members!.ResolveRecordPrefix(
            ref reader,
            _lastRecordMemberIndex,
            deferredFieldIndexes,
            deferredFieldOffsets,
            resolution.Members,
            resolution.Sizes);

    internal void ResolveMapEntry(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> payload,
        AvroMemberResolution resolution) =>
        _members!.ResolveMapEntry(key, payload, resolution.Members, resolution.Sizes);

    internal void ResolveMapEntry(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> payload,
        ValidationCelValue value,
        int size,
        AvroMemberResolution resolution) =>
        _members!.ResolveMapEntry(
            key,
            payload,
            value,
            size,
            resolution.Members,
            resolution.Sizes);

    internal void ResolveRecordField(
        int fieldIndex,
        ReadOnlyMemory<byte> payload,
        ValidationCelValue value,
        int size,
        AvroMemberResolution resolution) =>
        _members!.ResolveRecordField(
            fieldIndex,
            payload,
            value,
            size,
            resolution.Members,
            resolution.Sizes);

    internal void ResolveUnionBranch(
        int branch,
        ReadOnlyMemory<byte> payload,
        AvroMemberResolution resolution) =>
        _members!.ResolveUnionBranch(branch, payload, resolution.Members, resolution.Sizes);

    internal void EvaluateResolvedWithoutRoot(
        AvroMemberResolution resolution,
        int payloadLength,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        var equalityGeneration = _usesCachedEquality
            ? CompiledValidationRule.BeginEqualityResolution()
            : 0;

        ValidationCelStrings.Begin(_memberCount + 1, payloadLength);
        try
        {
            for (var index = 0; index < _rules.Length; index++)
            {
                var rule = _rules[index];
                try
                {
                    var result = rule.Evaluate(
                        ValidationCelValue.Missing,
                        now,
                        resolution.Members,
                        resolution.Sizes,
                        equalityGeneration,
                        rootAggregateComparer: null);
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

    private AvroAggregateEqualityComparer? GetRootAggregateComparer(ReadOnlyMemory<byte> payload)
    {
        if (_rootUnionComparers is null)
            return _rootAggregateComparer;

        var reader = new AvroValidationReader(payload);
        var branch = reader.ReadLong();
        if ((ulong)branch >= (ulong)_rootUnionComparers.Length)
            throw new SchemaRegistryRuleException(
                $"Could not evaluate Avro validation rules: invalid union index {branch}.");
        return _rootUnionComparers[(int)branch];
    }
}

internal sealed class AvroMemberResolver
{
    private readonly AvroSchema _valueSchema;
    private readonly AvroAggregateEqualityComparerFactory _aggregateComparerFactory;
    private readonly AvroMemberResolver?[]? _unionBranches;
    private readonly AvroMemberNode?[] _fields;
    private readonly Dictionary<ReadOnlyMemory<byte>, AvroMemberNode>? _mapValues;

    internal int LastRecordMemberIndex
    {
        get
        {
            for (var index = _fields.Length - 1; index >= 0; index--)
            {
                if (_fields[index]?.HasTargets == true)
                    return index;
            }
            return -1;
        }
    }

    private AvroMemberResolver(
        global::Avro.RecordSchema recordSchema,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        _valueSchema = recordSchema;
        _aggregateComparerFactory = aggregateComparerFactory;
        _fields = new AvroMemberNode?[recordSchema.Fields.Count];
    }

    private AvroMemberResolver(
        global::Avro.UnionSchema unionSchema,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        _valueSchema = unionSchema;
        _aggregateComparerFactory = aggregateComparerFactory;
        _unionBranches = new AvroMemberResolver?[unionSchema.Count];
        _fields = [];
    }

    private AvroMemberResolver(
        global::Avro.MapSchema mapSchema,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        _valueSchema = mapSchema;
        _aggregateComparerFactory = aggregateComparerFactory;
        _fields = [];
        _mapValues = new Dictionary<ReadOnlyMemory<byte>, AvroMemberNode>(AvroUtf8MemoryComparer.Instance);
    }

    internal static AvroMemberResolver? Create(
        AvroSchema valueSchema,
        IReadOnlyList<byte[][]> paths,
        IReadOnlyCollection<int> usedIndexes,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        valueSchema = AvroValueRulePlan.Unwrap(valueSchema);
        if (valueSchema is global::Avro.RecordSchema record)
            return CreateRecord(record, paths, usedIndexes, aggregateComparerFactory);
        if (valueSchema is global::Avro.MapSchema map)
            return CreateMap(map, paths, usedIndexes, aggregateComparerFactory);
        if (valueSchema is not global::Avro.UnionSchema union)
            return null;

        var resolver = new AvroMemberResolver(union, aggregateComparerFactory);
        var resolvedIndexes = new bool[paths.Count];
        for (var branchIndex = 0; branchIndex < union.Count; branchIndex++)
        {
            var branch = AvroValueRulePlan.Unwrap(union[branchIndex]);
            if (branch is not (global::Avro.RecordSchema or global::Avro.MapSchema))
                continue;

            AvroMemberResolver? branchResolver = null;
            foreach (var memberIndex in usedIndexes)
            {
                var path = paths[memberIndex];
                if (!CanResolve(branch, path, depth: 0))
                    continue;

                if (branch is global::Avro.RecordSchema branchRecord)
                {
                    branchResolver ??= CreateRecord(branchRecord, aggregateComparerFactory);
                    branchResolver.Add(branchRecord, path, memberIndex, depth: 0);
                }
                else
                {
                    var branchMap = (global::Avro.MapSchema)branch;
                    branchResolver ??= CreateMap(branchMap, aggregateComparerFactory);
                    branchResolver.Add(branchMap, path, memberIndex, depth: 0);
                }
                resolvedIndexes[memberIndex] = true;
            }
            resolver._unionBranches![branchIndex] = branchResolver;
        }

        foreach (var memberIndex in usedIndexes)
        {
            if (!resolvedIndexes[memberIndex])
            {
                throw new SchemaRegistryRuleException(
                    $"Avro validation rule refers to unknown field '{Encoding.UTF8.GetString(paths[memberIndex][0])}' on every record union branch.");
            }
        }
        return resolver;
    }

    private static AvroMemberResolver CreateRecord(
        global::Avro.RecordSchema record,
        IReadOnlyList<byte[][]> paths,
        IReadOnlyCollection<int> usedIndexes,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        var resolver = CreateRecord(record, aggregateComparerFactory);
        foreach (var memberIndex in usedIndexes)
            resolver.Add(record, paths[memberIndex], memberIndex, depth: 0);
        return resolver;
    }

    private static AvroMemberResolver CreateRecord(
        global::Avro.RecordSchema record,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        var resolver = new AvroMemberResolver(record, aggregateComparerFactory);
        resolver.SetSchemas(record);
        return resolver;
    }

    private static AvroMemberResolver CreateMap(
        global::Avro.MapSchema map,
        IReadOnlyList<byte[][]> paths,
        IReadOnlyCollection<int> usedIndexes,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        var resolver = CreateMap(map, aggregateComparerFactory);
        foreach (var memberIndex in usedIndexes)
            resolver.Add(map, paths[memberIndex], memberIndex, depth: 0);
        return resolver;
    }

    private static AvroMemberResolver CreateMap(
        global::Avro.MapSchema map,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory) =>
        new(map, aggregateComparerFactory);

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
        var node = _fields[field.Pos] ??= new AvroMemberNode(_aggregateComparerFactory);
        if (depth == path.Length - 1)
        {
            node.AddMemberIndex(memberIndex);
            return;
        }
        node.AddChild(field.Schema, path, memberIndex, depth + 1);
    }

    private void Add(
        global::Avro.MapSchema schema,
        byte[][] path,
        int memberIndex,
        int depth)
    {
        var key = (ReadOnlyMemory<byte>)path[depth];
        if (!_mapValues!.TryGetValue(key, out var node))
        {
            node = new AvroMemberNode(_aggregateComparerFactory) { Schema = schema.ValueSchema };
            _mapValues.Add(key, node);
        }
        if (depth == path.Length - 1)
        {
            node.AddMemberIndex(memberIndex);
            return;
        }
        node.AddChild(schema.ValueSchema, path, memberIndex, depth + 1);
    }

    internal void Resolve(
        ReadOnlyMemory<byte> payload,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        var reader = new AvroValidationReader(payload);
        Resolve(ref reader, values, sizes);
    }

    internal void Resolve(
        ref AvroValidationReader reader,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        if (_mapValues is not null)
        {
            ResolveMap((global::Avro.MapSchema)_valueSchema, ref reader, values, sizes);
            return;
        }
        if (_unionBranches is not null)
        {
            var union = (global::Avro.UnionSchema)_valueSchema;
            var branch = reader.ReadLong();
            if ((ulong)branch >= (ulong)union.Count)
                throw new SchemaRegistryRuleException(
                    $"Could not evaluate Avro validation rules: invalid union index {branch}.");
            var resolver = _unionBranches[(int)branch];
            if (resolver is null)
                AvroValidationValueDecoder.Skip(union[(int)branch], ref reader);
            else
                resolver.Resolve(ref reader, values, sizes);
            return;
        }
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

    private void ResolveMap(
        global::Avro.MapSchema map,
        ref AvroValidationReader reader,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return;
            for (long index = 0; index < count; index++)
            {
                var key = reader.ReadLengthPrefixed();
                var valueStart = reader.Position;
                AvroValidationValueDecoder.Skip(map.ValueSchema, ref reader);
                if (_mapValues!.TryGetValue(key, out var node))
                {
                    node.Resolve(
                        reader.Source.Slice(valueStart, reader.Position - valueStart),
                        values,
                        sizes);
                }
            }
        }
    }

    internal void ResolveMapEntry(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> payload,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        if (_mapValues!.TryGetValue(key, out var node))
            node.Resolve(payload, values, sizes);
    }

    internal void ResolveMapEntry(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> payload,
        ValidationCelValue value,
        int size,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        if (_mapValues!.TryGetValue(key, out var node))
            node.ResolveCaptured(payload, value, size, values, sizes);
    }

    internal void ResolveRecordField(
        int fieldIndex,
        ReadOnlyMemory<byte> payload,
        ValidationCelValue value,
        int size,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        var node = _fields[fieldIndex]
            ?? throw new InvalidOperationException("Avro validation member resolver is incomplete.");
        if (node.HasTargets)
            node.ResolveCaptured(payload, value, size, values, sizes);
    }

    internal void ResolveUnionBranch(
        int branch,
        ReadOnlyMemory<byte> payload,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        var resolver = _unionBranches![branch];
        if (resolver is not null)
            resolver.Resolve(payload, values, sizes);
    }

    internal AvroFieldPayload ResolveRecordPrefix(
        ref AvroValidationReader reader,
        int lastFieldIndex,
        int[] deferredFieldIndexes,
        scoped Span<int> deferredFieldOffsets,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        var deferredPosition = 0;
        var lastFieldStart = 0;
        var lastFieldLength = 0;
        for (var index = 0; index <= lastFieldIndex; index++)
        {
            var fieldStart = reader.Position;
            var node = _fields[index]
                ?? throw new InvalidOperationException("Avro validation member resolver is incomplete.");
            var schema = node.Schema!;
            AvroValidationValueDecoder.Skip(schema, ref reader);
            var fieldLength = reader.Position - fieldStart;
            if (index == lastFieldIndex)
            {
                lastFieldStart = fieldStart;
                lastFieldLength = fieldLength;
            }
            else if (deferredPosition < deferredFieldIndexes.Length
                && deferredFieldIndexes[deferredPosition] == index)
            {
                deferredFieldOffsets[deferredPosition * 2] = fieldStart;
                deferredFieldOffsets[deferredPosition * 2 + 1] = fieldLength;
                deferredPosition++;
            }
            if (node.HasTargets)
            {
                node.Resolve(
                    reader.Source.Slice(fieldStart, fieldLength),
                    values,
                    sizes);
            }
        }
        return new AvroFieldPayload(lastFieldStart, lastFieldLength);
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

    private static bool CanResolve(
        AvroSchema schema,
        byte[][] path,
        int depth)
    {
        schema = AvroValueRulePlan.Unwrap(schema);
        if (schema is global::Avro.RecordSchema record)
        {
            var field = FindField(record, Encoding.UTF8.GetString(path[depth]));
            return field is not null &&
                (depth == path.Length - 1 || CanResolve(field.Schema, path, depth + 1));
        }
        if (schema is global::Avro.MapSchema map)
            return depth == path.Length - 1 || CanResolve(map.ValueSchema, path, depth + 1);
        if (schema is not global::Avro.UnionSchema union)
            return false;
        for (var index = 0; index < union.Count; index++)
        {
            if (CanResolve(union[index], path, depth))
                return true;
        }
        return false;
    }

    internal void SetSchemas(global::Avro.RecordSchema schema)
    {
        for (var index = 0; index < schema.Fields.Count; index++)
            (_fields[index] ??= new AvroMemberNode(_aggregateComparerFactory)).Schema =
                schema.Fields[index].Schema;
    }

    private sealed class AvroMemberNode(
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        private readonly Dictionary<AvroSchema, AvroMemberResolver> _children =
            new(AvroSchemaReferenceComparer.Instance);
        private AvroSchema? _schema;
        private AvroAggregateEqualityComparer? _aggregateComparer;
        private AvroAggregateEqualityComparer?[]? _unionComparers;
        private ReadOnlyMemory<byte>[]? _enumSymbols;
        private ReadOnlyMemory<byte>[]?[]? _unionEnumSymbols;
        private int[]? _additionalMemberIndexes;

        internal AvroSchema? Schema
        {
            get => _schema;
            set
            {
                _schema = value;
                var schema = AvroValueRulePlan.Unwrap(value!);
                _aggregateComparer = aggregateComparerFactory.Create(schema);
                _enumSymbols = AvroValidationValueDecoder.EncodeEnumSymbols(schema);
                if (schema is not global::Avro.UnionSchema union)
                    return;
                _unionComparers = new AvroAggregateEqualityComparer?[union.Count];
                _unionEnumSymbols = new ReadOnlyMemory<byte>[]?[union.Count];
                for (var index = 0; index < union.Count; index++)
                {
                    var branch = AvroValueRulePlan.Unwrap(union[index]);
                    _unionComparers[index] = aggregateComparerFactory.Create(branch);
                    _unionEnumSymbols[index] = AvroValidationValueDecoder.EncodeEnumSymbols(branch);
                }
            }
        }
        internal int MemberIndex { get; private set; } = -1;
        internal bool HasTargets => MemberIndex >= 0 || _children.Count != 0;

        internal void AddMemberIndex(int memberIndex)
        {
            if (MemberIndex < 0)
            {
                MemberIndex = memberIndex;
                return;
            }
            if (MemberIndex == memberIndex)
                return;

            var indexes = _additionalMemberIndexes;
            if (indexes is null)
            {
                _additionalMemberIndexes = [memberIndex];
                return;
            }
            for (var index = 0; index < indexes.Length; index++)
            {
                if (indexes[index] == memberIndex)
                    return;
            }
            Array.Resize(ref _additionalMemberIndexes, indexes.Length + 1);
            _additionalMemberIndexes[^1] = memberIndex;
        }

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
                for (var index = 0; index < union.Count; index++)
                {
                    var branch = AvroValueRulePlan.Unwrap(union[index]);
                    if (branch is not (global::Avro.RecordSchema or global::Avro.MapSchema))
                        continue;
                    if (!CanResolve(branch, path, depth))
                        continue;
                    AddResolverChild(branch, path, memberIndex, depth);
                    found = true;
                }
                if (found)
                    return;
            }
            else if (schema is global::Avro.RecordSchema or global::Avro.MapSchema)
            {
                AddResolverChild(schema, path, memberIndex, depth);
                return;
            }

            throw new SchemaRegistryRuleException(
                "Avro validation member path can only descend through records, maps, or their unions.");
        }

        private void AddResolverChild(
            AvroSchema schema,
            byte[][] path,
            int memberIndex,
            int depth)
        {
            if (!_children.TryGetValue(schema, out var child))
            {
                child = schema switch
                {
                    global::Avro.RecordSchema record => CreateRecord(record, aggregateComparerFactory),
                    global::Avro.MapSchema map => CreateMap(map, aggregateComparerFactory),
                    _ => throw new InvalidOperationException("Avro validation member resolver child is unsupported.")
                };
                _children.Add(schema, child);
            }
            if (schema is global::Avro.RecordSchema recordSchema)
                child.Add(recordSchema, path, memberIndex, depth);
            else
                child.Add((global::Avro.MapSchema)schema, path, memberIndex, depth);
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
                var value = ReadValue(schema, ref valueReader, out var aggregateComparer);
                var additionalIndexes = _additionalMemberIndexes;
                if (additionalIndexes is null)
                {
                    if (value.SizeIndex == 0)
                    {
                        var sizeIndex = MemberIndex + 1;
                        sizes.Set(sizeIndex, AvroValidationValueDecoder.Count(schema, payload));
                        value = value with { SizeIndex = sizeIndex };
                    }
                    values.SetValue(MemberIndex, value, aggregateComparer);
                }
                else
                {
                    var size = -1;
                    if (value.SizeIndex == 0)
                        size = AvroValidationValueDecoder.Count(schema, payload);
                    SetMemberValue(MemberIndex, value, size, values, sizes, aggregateComparer);
                    for (var index = 0; index < additionalIndexes.Length; index++)
                    {
                        SetMemberValue(
                            additionalIndexes[index],
                            value,
                            size,
                            values,
                            sizes,
                            aggregateComparer);
                    }
                }
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
            if (_children.TryGetValue(schema, out var child))
            {
                child.Resolve(
                    childReader.Source.Slice(childReader.Position),
                    values,
                    sizes);
            }
        }

        internal void ResolveCaptured(
            ReadOnlyMemory<byte> payload,
            ValidationCelValue value,
            int size,
            ValidationCelMemberValues values,
            ValidationCelSizeValues sizes)
        {
            var schema = AvroValueRulePlan.Unwrap(Schema!);
            if (value.Kind == ValidationCelValueKind.Missing || schema is global::Avro.UnionSchema)
            {
                Resolve(payload, values, sizes);
                return;
            }

            if (MemberIndex >= 0)
            {
                SetMemberValue(MemberIndex, value, size, values, sizes, _aggregateComparer);
                var additionalIndexes = _additionalMemberIndexes;
                if (additionalIndexes is not null)
                {
                    for (var index = 0; index < additionalIndexes.Length; index++)
                    {
                        SetMemberValue(
                            additionalIndexes[index],
                            value,
                            size,
                            values,
                            sizes,
                            _aggregateComparer);
                    }
                }
            }

            if (_children.Count == 0)
                return;
            if (_children.TryGetValue(schema, out var child))
                child.Resolve(payload, values, sizes);
        }

        private static void SetMemberValue(
            int memberIndex,
            ValidationCelValue value,
            int size,
            ValidationCelMemberValues values,
            ValidationCelSizeValues sizes,
            AvroAggregateEqualityComparer? aggregateComparer)
        {
            if (size >= 0)
            {
                var sizeIndex = memberIndex + 1;
                sizes.Set(sizeIndex, size);
                value = value with { SizeIndex = sizeIndex };
            }
            values.SetValue(memberIndex, value, aggregateComparer);
        }

        private ValidationCelValue ReadValue(
            AvroSchema schema,
            ref AvroValidationReader reader,
            out AvroAggregateEqualityComparer? aggregateComparer)
        {
            schema = AvroValueRulePlan.Unwrap(schema);
            if (schema is not global::Avro.UnionSchema union)
            {
                aggregateComparer = _aggregateComparer;
                return AvroValidationValueDecoder.Read(schema, _enumSymbols, ref reader);
            }

            var branch = reader.ReadLong();
            if ((ulong)branch >= (ulong)union.Count)
                throw new SchemaRegistryRuleException(
                    $"Could not evaluate Avro validation rules: invalid union index {branch}.");
            aggregateComparer = _unionComparers![(int)branch];
            return AvroValidationValueDecoder.Read(
                union[(int)branch],
                _unionEnumSymbols![(int)branch],
                ref reader);
        }
    }
}

internal sealed class AvroUtf8MemoryComparer : IEqualityComparer<ReadOnlyMemory<byte>>
{
    internal static readonly AvroUtf8MemoryComparer Instance = new();

    private AvroUtf8MemoryComparer() { }

    public bool Equals(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) =>
        left.Span.SequenceEqual(right.Span);

    public int GetHashCode(ReadOnlyMemory<byte> value)
    {
        var hash = 2166136261u;
        var span = value.Span;
        for (var index = 0; index < span.Length; index++)
            hash = (hash ^ span[index]) * 16777619u;
        return unchecked((int)hash);
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
    internal static ReadOnlyMemory<byte>[]? EncodeEnumSymbols(AvroSchema schema)
    {
        schema = AvroValueRulePlan.Unwrap(schema);
        if (schema is not global::Avro.EnumSchema enumeration)
            return null;

        var symbols = new ReadOnlyMemory<byte>[enumeration.Symbols.Count];
        for (var index = 0; index < symbols.Length; index++)
            symbols[index] = Encoding.UTF8.GetBytes(enumeration.Symbols[index]);
        return symbols;
    }

    internal static ValidationCelValue Read(
        AvroSchema schema,
        ReadOnlyMemory<byte>[]? enumSymbols,
        ref AvroValidationReader reader)
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
                return ValidationCelValue.FromNumber(reader.ReadLong());
            case AvroSchema.Type.Enumeration:
                var enumeration = (global::Avro.EnumSchema)schema;
                var symbolIndex = reader.ReadLong();
                if ((ulong)symbolIndex >= (ulong)enumeration.Symbols.Count)
                    throw InvalidPayload($"invalid enum index {symbolIndex}");
                if (enumSymbols is null)
                    throw new InvalidOperationException("Avro enum symbols were not compiled for validation.");
                return ValidationCelValue.FromUtf8String(enumSymbols[(int)symbolIndex]);
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
                return ValidationCelValue.FromCollection(
                    ValidationCelValueKind.Object,
                    reader.Source.Slice(recordStart, reader.Position - recordStart),
                    0);
            case AvroSchema.Type.Array:
            {
                var collectionStart = reader.Position;
                SkipCollection(((global::Avro.ArraySchema)schema).ItemSchema, isMap: false, ref reader);
                return ValidationCelValue.FromCollection(
                    ValidationCelValueKind.Array,
                    reader.Source.Slice(collectionStart, reader.Position - collectionStart),
                    0);
            }
            case AvroSchema.Type.Map:
            {
                var collectionStart = reader.Position;
                SkipCollection(((global::Avro.MapSchema)schema).ValueSchema, isMap: true, ref reader);
                return ValidationCelValue.FromCollection(
                    ValidationCelValueKind.Object,
                    reader.Source.Slice(collectionStart, reader.Position - collectionStart),
                    0);
            }
            case AvroSchema.Type.Union:
                var union = (global::Avro.UnionSchema)schema;
                var branch = reader.ReadLong();
                if ((ulong)branch >= (ulong)union.Count)
                    throw InvalidPayload($"invalid union index {branch}");
                return Read(union[(int)branch], enumSymbols, ref reader);
            default:
                throw InvalidPayload($"unsupported schema type {schema.Tag}");
        }
    }

    internal static void Skip(AvroSchema schema, ref AvroValidationReader reader)
    {
        schema = AvroValueRulePlan.Unwrap(schema);
        switch (schema.Tag)
        {
            case AvroSchema.Type.Null:
                return;
            case AvroSchema.Type.Boolean:
                _ = reader.Read(1);
                return;
            case AvroSchema.Type.Int:
            case AvroSchema.Type.Long:
                _ = reader.ReadLong();
                return;
            case AvroSchema.Type.Enumeration:
                var symbolIndex = reader.ReadLong();
                if ((ulong)symbolIndex >= (ulong)((global::Avro.EnumSchema)schema).Symbols.Count)
                    throw InvalidPayload($"invalid enum index {symbolIndex}");
                return;
            case AvroSchema.Type.Float:
                _ = reader.Read(sizeof(float));
                return;
            case AvroSchema.Type.Double:
                _ = reader.Read(sizeof(double));
                return;
            case AvroSchema.Type.String:
            case AvroSchema.Type.Bytes:
                _ = reader.ReadLengthPrefixed();
                return;
            case AvroSchema.Type.Fixed:
                _ = reader.Read(((global::Avro.FixedSchema)schema).Size);
                return;
            case AvroSchema.Type.Record:
            case AvroSchema.Type.Error:
                var record = (global::Avro.RecordSchema)schema;
                for (var index = 0; index < record.Fields.Count; index++)
                    Skip(record.Fields[index].Schema, ref reader);
                return;
            case AvroSchema.Type.Array:
                SkipCollection(((global::Avro.ArraySchema)schema).ItemSchema, isMap: false, ref reader);
                return;
            case AvroSchema.Type.Map:
                SkipCollection(((global::Avro.MapSchema)schema).ValueSchema, isMap: true, ref reader);
                return;
            case AvroSchema.Type.Union:
                var union = (global::Avro.UnionSchema)schema;
                var branch = reader.ReadLong();
                if ((ulong)branch >= (ulong)union.Count)
                    throw InvalidPayload($"invalid union index {branch}");
                Skip(union[(int)branch], ref reader);
                return;
            default:
                throw InvalidPayload($"unsupported schema type {schema.Tag}");
        }
    }

    internal static int Count(AvroSchema schema, ReadOnlyMemory<byte> payload)
    {
        schema = AvroValueRulePlan.Unwrap(schema);
        var reader = new AvroValidationReader(payload);
        if (schema is global::Avro.UnionSchema union)
        {
            var branch = reader.ReadLong();
            if ((ulong)branch >= (ulong)union.Count)
                throw InvalidPayload($"invalid union index {branch}");
            schema = AvroValueRulePlan.Unwrap(union[(int)branch]);
        }
        if (schema is global::Avro.RecordSchema record)
            return record.Fields.Count;
        if (schema is not (global::Avro.ArraySchema or global::Avro.MapSchema))
            return -1;
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
