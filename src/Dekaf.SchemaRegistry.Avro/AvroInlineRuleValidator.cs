using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AvroSchema = global::Avro.Schema;

namespace Dekaf.SchemaRegistry.Avro;

internal sealed class AvroInlineRuleValidator
{
    internal const int MaximumValidationDepth = 100;

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
    private readonly bool _requiresBoundedValueDecoding;
    private readonly bool _requiresValidationDepthGuard;
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
        _requiresBoundedValueDecoding = AvroAggregateEqualityComparer.IsRecursive(_schema);
        _requiresValidationDepthGuard =
            _schema is global::Avro.RecordSchema &&
            _requiresBoundedValueDecoding;
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
            return ReadRuleValue(
                ref reader,
                path.RemainingValidationDepth,
                needsSize: false,
                out _);

        // Keep the non-recursive dispatch inline: routing it through ValidateCore regresses
        // warmed validation. Recursive schemas alone pay for the guarded helper call.
        if (_requiresValidationDepthGuard)
        {
            return ValidateWithDepthGuard(
                ref reader,
                now,
                failFast,
                ref violations,
                ref path);
        }

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
                value = ReadRuleValue(
                    ref preview,
                    path.RemainingValidationDepth,
                    _schemaRules.UsesRootSize,
                    out var rootSize);
                var payload = preview.Source.Slice(start, preview.Position - start);
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
                    this,
                    path.RemainingValidationDepth,
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
                    value = ReadRuleValue(
                        ref reader,
                        path.RemainingValidationDepth,
                        needsSize: false,
                        out _);
                else
                    reader = preview;
                break;
        }
        return value;
    }

    private ValidationCelValue ValidateWithDepthGuard(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        path.EnterValidation();
        // A thrown validation exception terminates the root operation and disposes the path;
        // only successful traversal needs to restore the sibling depth.
        var value = ValidateCore(
            ref reader,
            now,
            failFast,
            ref violations,
            ref path);
        path.ExitValidation();
        return value;
    }

    private ValidationCelValue ValidateCore(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
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
                var valueDecodingDepth = CurrentValueDecodingDepth(ref path);
                value = ReadRuleValue(
                    ref preview,
                    valueDecodingDepth,
                    _schemaRules.UsesRootSize,
                    out var rootSize);
                var payload = preview.Source.Slice(start, preview.Position - start);
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
                    this,
                    CurrentValueDecodingDepth(ref path),
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
                    value = ReadRuleValue(
                        ref reader,
                        CurrentValueDecodingDepth(ref path),
                        needsSize: false,
                        out _);
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
        using var resolution = _schemaRules.BeginMemberResolution();
        List<ValidationRuleError>? nestedViolations = null;
        var start = reader.Position;
        switch (_schema)
        {
            case global::Avro.MapSchema:
                ValidateMapWithMemberResolution(
                    ref reader,
                    _schemaRules,
                    resolution,
                    now,
                    failFast,
                    ref nestedViolations,
                    ref path);
                break;
            case global::Avro.UnionSchema:
                ValidateUnionWithMemberResolution(
                    ref reader,
                    _schemaRules,
                    resolution,
                    now,
                    failFast,
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
        using var resolution = _schemaRules.BeginMemberResolution();
        List<ValidationRuleError>? nestedViolations = null;
        var start = reader.Position;
        int rootSize;
        switch (_schema)
        {
            case global::Avro.RecordSchema:
                ValidateRecordWithMemberResolution(
                    ref reader,
                    _schemaRules,
                    resolution,
                    now,
                    failFast,
                    ref nestedViolations,
                    ref path);
                rootSize = _fields.Length;
                break;
            case global::Avro.MapSchema:
                rootSize = ValidateMapWithMemberResolution(
                    ref reader,
                    _schemaRules,
                    resolution,
                    now,
                    failFast,
                    ref nestedViolations,
                    ref path);
                break;
            default:
                throw new InvalidOperationException(
                    "Mixed root/member validation requires a record or map schema.");
        }
        var payload = reader.Source.Slice(start, reader.Position - start);
        var value = ValidationCelValue.FromCollection(ValidationCelValueKind.Object, payload, sizeIndex: 0);
        _schemaRules.EvaluateResolved(
            value,
            payload,
            resolution,
            rootSize,
            now,
            failFast,
            ref violations,
            ref path);
        AppendNestedViolations(nestedViolations, failFast, ref violations);
        return value;
    }

    private void ValidateRecordWithMemberResolution(
        ref AvroValidationReader reader,
        AvroCompiledRuleSet rules,
        AvroMemberResolution resolution,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? nestedViolations,
        scoped ref AvroValidationPath path)
    {
        for (var index = 0; index < _fields.Length; index++)
        {
            var start = reader.Position;
            var field = _fields[index];
            var needsSize = rules.NeedsRecordFieldSize(index);
            ValidationCelValue value;
            int size;
            if (failFast && nestedViolations is not null)
            {
                value = CaptureRecordField(
                    field,
                    ref reader,
                    path.RemainingValidationDepth,
                    needsSize,
                    out size);
            }
            else
            {
                value = ValidateRecordFieldAndCapture(
                    field,
                    ref reader,
                    now,
                    failFast,
                    needsSize,
                    ref nestedViolations,
                    ref path,
                    out size);
            }
            rules.ResolveRecordField(
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
        bool failFast,
        bool needsSize,
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
                    failFast,
                    ref violations,
                    ref path,
                    out size);
                path.Truncate(fieldMark);
                return capturedValue;
            }

            return ValidateDeferredRecordField(
                field,
                ref reader,
                now,
                failFast,
                needsSize,
                ref violations,
                ref path,
                out size);
        }

        var mark = path.Length;
        path.AppendField(field.Field.Name);
        var value = field.Child.ValidateAndCapture(
            ref reader,
            now,
            failFast,
            needsSize,
            ref violations,
            ref path,
            out size);
        path.Truncate(mark);
        return value;
    }

    private static bool CanFuseFieldRules(AvroFieldRulePlan field) =>
        (field.Child.HasAnyRules || field.Rules.HasMembers) &&
        field.Child._schemaRules.IsEmpty &&
        field.Rules.UsesRootValue &&
        (field.Rules.HasMembers
            ? field.Child._schema is global::Avro.RecordSchema or global::Avro.MapSchema or global::Avro.UnionSchema
            : field.Child._schema is global::Avro.RecordSchema or global::Avro.ArraySchema or global::Avro.MapSchema);

    private static ValidationCelValue EvaluateFieldRulesWithNestedTraversal(
        AvroFieldRulePlan field,
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path,
        out int size)
    {
        if (field.Rules.HasMembers)
        {
            return EvaluateMemberFieldRulesWithNestedTraversal(
                field,
                ref reader,
                now,
                failFast,
                ref violations,
                ref path,
                out size);
        }

        var start = reader.Position;
        List<ValidationRuleError>? nestedViolations = null;
        var value = field.Child.ValidateAndCapture(
            ref reader,
            now,
            failFast,
            field.Rules.UsesRootSize,
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
        bool failFast,
        bool needsSize,
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
                    ValidateRecord(ref reader, now, failFast, ref violations, ref path);
                    size = _fields.Length;
                    kind = ValidationCelValueKind.Object;
                    break;
                case global::Avro.ArraySchema:
                    size = ValidateArray(ref reader, now, failFast, ref violations, ref path);
                    kind = ValidationCelValueKind.Array;
                    break;
                case global::Avro.MapSchema:
                    size = ValidateMap(ref reader, now, failFast, ref violations, ref path);
                    kind = ValidationCelValueKind.Object;
                    break;
                default:
                    size = -1;
                    return Validate(
                        ref reader,
                        now,
                        failFast,
                        ref violations,
                        ref path);
            }
            return ValidationCelValue.FromCollection(
                kind,
                reader.Source.Slice(start, reader.Position - start),
                sizeIndex: 0);
        }

        if (needsSize && _validationStrategy == ValidationStrategy.Standard)
        {
            return ValidateSchemaRulesAndCapture(
                ref reader,
                now,
                failFast,
                ref violations,
                ref path,
                out size);
        }

        var valueStart = reader.Position;
        var value = Validate(
            ref reader,
            now,
            failFast,
            ref violations,
            ref path);
        size = needsSize && value.SizeIndex == 0
            ? CountValue(
                reader.Source.Slice(valueStart, reader.Position - valueStart),
                CurrentValueDecodingDepth(ref path))
            : -1;
        return value;
    }

    private ValidationCelValue ValidateSchemaRulesAndCapture(
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path,
        out int size)
    {
        var start = reader.Position;
        var preview = reader;
        var value = ReadRuleValue(
            ref preview,
            CurrentValueDecodingDepth(ref path),
            needsSize: true,
            out size);
        var payload = preview.Source.Slice(start, preview.Position - start);
        _schemaRules.Evaluate(
            value,
            payload,
            now,
            failFast,
            ref violations,
            ref path,
            _schemaRules.UsesRootSize ? size : -1);
        if ((failFast && violations is not null) || !_hasNestedRules)
        {
            reader = preview;
            return value;
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
                reader = preview;
                break;
        }
        return value;
    }

    private static ValidationCelValue EvaluateMemberFieldRulesWithNestedTraversal(
        AvroFieldRulePlan field,
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path,
        out int size)
    {
        using var resolution = field.Rules.BeginIsolatedMemberResolution();
        List<ValidationRuleError>? nestedViolations = null;
        var child = field.Child;
        var start = reader.Position;
        ValidationCelValue value;
        switch (child._schema)
        {
            case global::Avro.RecordSchema:
                child.ValidateRecordWithMemberResolution(
                    ref reader,
                    field.Rules,
                    resolution,
                    now,
                    failFast,
                    ref nestedViolations,
                    ref path);
                size = child._fields.Length;
                value = ValidationCelValue.FromCollection(
                    ValidationCelValueKind.Object,
                    reader.Source.Slice(start, reader.Position - start),
                    sizeIndex: 0);
                break;
            case global::Avro.MapSchema:
                size = child.ValidateMapWithMemberResolution(
                    ref reader,
                    field.Rules,
                    resolution,
                    now,
                    failFast,
                    ref nestedViolations,
                    ref path);
                value = ValidationCelValue.FromCollection(
                    ValidationCelValueKind.Object,
                    reader.Source.Slice(start, reader.Position - start),
                    sizeIndex: 0);
                break;
            case global::Avro.UnionSchema:
                value = child.ValidateUnionFieldRulesWithMemberTraversal(
                    ref reader,
                    field.Rules.MemberResolver,
                    resolution,
                    now,
                    failFast,
                    ref nestedViolations,
                    ref path,
                    out size);
                break;
            default:
                throw new InvalidOperationException(
                    "Fused member field validation requires a record, map, or union schema.");
        }

        var payload = reader.Source.Slice(start, reader.Position - start);
        field.Rules.EvaluateResolved(
            value,
            payload,
            resolution,
            size,
            now,
            failFast,
            ref violations,
            ref path);
        AppendNestedViolations(nestedViolations, failFast, ref violations);
        return value;
    }

    private ValidationCelValue ValidateUnionFieldRulesWithMemberTraversal(
        ref AvroValidationReader reader,
        AvroMemberResolver memberResolver,
        AvroMemberResolution resolution,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? nestedViolations,
        scoped ref AvroValidationPath path,
        out int size)
    {
        var union = (global::Avro.UnionSchema)_schema;
        var branch = reader.ReadLong();
        if ((ulong)branch >= (ulong)union.Count)
            throw InvalidPayload($"invalid union index {branch}");

        var child = _children[(int)branch];
        var start = reader.Position;
        var branchResolver = memberResolver.GetUnionBranchResolver((int)branch);
        if (branchResolver is not null &&
            child._schemaRules.IsEmpty)
        {
            switch (child._schema)
            {
                case global::Avro.RecordSchema:
                    child.ValidateRecordWithMemberResolver(
                        ref reader,
                        branchResolver,
                        resolution,
                        now,
                        failFast,
                        ref nestedViolations,
                        ref path);
                    size = child._fields.Length;
                    break;
                case global::Avro.MapSchema:
                    size = child.ValidateMapWithMemberResolver(
                        ref reader,
                        branchResolver,
                        resolution,
                        now,
                        failFast,
                        ref nestedViolations,
                        ref path);
                    break;
                default:
                    goto Deferred;
            }
            return ValidationCelValue.FromCollection(
                ValidationCelValueKind.Object,
                reader.Source.Slice(start, reader.Position - start),
                sizeIndex: 0);
        }

Deferred:
        var value = child.ValidateAndCapture(
            ref reader,
            now,
            failFast,
            needsSize: false,
            ref nestedViolations,
            ref path,
            out size);
        branchResolver?.Resolve(
            reader.Source.Slice(start, reader.Position - start),
            resolution.Members,
            resolution.Sizes);
        return value;
    }

    private ValidationCelValue ValidateAndCaptureWithMemberNode(
        ref AvroValidationReader reader,
        AvroMemberResolver.AvroMemberNode memberNode,
        AvroMemberResolution resolution,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? nestedViolations,
        scoped ref AvroValidationPath path,
        out int size)
    {
        var start = reader.Position;
        if (_schemaRules.IsEmpty &&
            memberNode.TryGetChildResolver(_schema, out var childResolver))
        {
            ValidationCelValue value;
            switch (_schema)
            {
                case global::Avro.RecordSchema:
                    ValidateRecordWithMemberResolver(
                        ref reader,
                        childResolver,
                        resolution,
                        now,
                        failFast,
                        ref nestedViolations,
                        ref path);
                    size = _fields.Length;
                    break;
                case global::Avro.MapSchema:
                    size = ValidateMapWithMemberResolver(
                        ref reader,
                        childResolver,
                        resolution,
                        now,
                        failFast,
                        ref nestedViolations,
                        ref path);
                    break;
                default:
                    goto Deferred;
            }
            value = ValidationCelValue.FromCollection(
                ValidationCelValueKind.Object,
                reader.Source.Slice(start, reader.Position - start),
                sizeIndex: 0);
            memberNode.ResolveCapturedValue(
                reader.Source.Slice(start, reader.Position - start),
                value,
                size,
                resolution.Members,
                resolution.Sizes);
            return value;
        }

        if (_schemaRules.IsEmpty && _schema is global::Avro.UnionSchema union)
        {
            var branch = reader.ReadLong();
            if ((ulong)branch >= (ulong)union.Count)
                throw InvalidPayload($"invalid union index {branch}");

            var child = _children[(int)branch];
            var childStart = reader.Position;
            if (child._schemaRules.IsEmpty &&
                memberNode.TryGetChildResolver(child._schema, out childResolver))
            {
                switch (child._schema)
                {
                    case global::Avro.RecordSchema:
                        child.ValidateRecordWithMemberResolver(
                            ref reader,
                            childResolver,
                            resolution,
                            now,
                            failFast,
                            ref nestedViolations,
                            ref path);
                        size = child._fields.Length;
                        break;
                    case global::Avro.MapSchema:
                        size = child.ValidateMapWithMemberResolver(
                            ref reader,
                            childResolver,
                            resolution,
                            now,
                            failFast,
                            ref nestedViolations,
                            ref path);
                        break;
                    default:
                        goto DeferredBranch;
                }
                var value = ValidationCelValue.FromCollection(
                    ValidationCelValueKind.Object,
                    reader.Source.Slice(childStart, reader.Position - childStart),
                    sizeIndex: 0);
                memberNode.ResolveCapturedValue(
                    reader.Source.Slice(start, reader.Position - start),
                    value,
                    size,
                    resolution.Members,
                    resolution.Sizes);
                return value;
            }

DeferredBranch:
            var branchValue = child.ValidateAndCapture(
                ref reader,
                now,
                failFast,
                memberNode.NeedsSize,
                ref nestedViolations,
                ref path,
                out size);
            memberNode.ResolveCaptured(
                reader.Source.Slice(start, reader.Position - start),
                branchValue,
                size,
                resolution.Members,
                resolution.Sizes);
            return branchValue;
        }

Deferred:
        var capturedValue = ValidateAndCapture(
            ref reader,
            now,
            failFast,
            memberNode.NeedsSize,
            ref nestedViolations,
            ref path,
            out size);
        memberNode.ResolveCaptured(
            reader.Source.Slice(start, reader.Position - start),
            capturedValue,
            size,
            resolution.Members,
            resolution.Sizes);
        return capturedValue;
    }

    private void ValidateRecordWithMemberResolver(
        ref AvroValidationReader reader,
        AvroMemberResolver memberResolver,
        AvroMemberResolution resolution,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? nestedViolations,
        scoped ref AvroValidationPath path)
    {
        if (memberResolver.HasDescendantTargets)
        {
            ValidateRecordWithDescendantMemberResolver(
                ref reader,
                memberResolver,
                resolution,
                now,
                failFast,
                ref nestedViolations,
                ref path);
            return;
        }

        for (var index = 0; index < _fields.Length; index++)
        {
            var start = reader.Position;
            var field = _fields[index];
            var needsSize = memberResolver.NeedsRecordFieldSize(index);
            ValidationCelValue value;
            int size;
            if (failFast && nestedViolations is not null)
            {
                value = CaptureRecordField(
                    field,
                    ref reader,
                    path.RemainingValidationDepth,
                    needsSize,
                    out size);
            }
            else
                value = ValidateRecordFieldAndCapture(
                    field,
                    ref reader,
                    now,
                    failFast,
                    needsSize,
                    ref nestedViolations,
                    ref path,
                    out size);
            memberResolver.ResolveRecordField(
                index,
                reader.Source.Slice(start, reader.Position - start),
                value,
                size,
                resolution.Members,
                resolution.Sizes);
        }
    }

    private void ValidateRecordWithDescendantMemberResolver(
        ref AvroValidationReader reader,
        AvroMemberResolver memberResolver,
        AvroMemberResolution resolution,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? nestedViolations,
        scoped ref AvroValidationPath path)
    {
        for (var index = 0; index < _fields.Length; index++)
        {
            var start = reader.Position;
            var field = _fields[index];
            var fieldResolver = memberResolver.GetRecordFieldResolver(index);
            var needsSize = fieldResolver.NeedsSize;
            ValidationCelValue value;
            int size;
            if (failFast && nestedViolations is not null)
            {
                value = CaptureRecordField(
                    field,
                    ref reader,
                    path.RemainingValidationDepth,
                    needsSize,
                    out size);
            }
            else if (field.Rules.IsEmpty && fieldResolver.HasChildren)
            {
                var mark = path.Length;
                path.AppendField(field.Field.Name);
                value = field.Child.ValidateAndCaptureWithMemberNode(
                    ref reader,
                    fieldResolver,
                    resolution,
                    now,
                    failFast,
                    ref nestedViolations,
                    ref path,
                    out size);
                path.Truncate(mark);
                continue;
            }
            else
                value = ValidateRecordFieldAndCapture(
                    field,
                    ref reader,
                    now,
                    failFast,
                    needsSize,
                    ref nestedViolations,
                    ref path,
                    out size);
            if (fieldResolver.HasTargets)
            {
                fieldResolver.ResolveCaptured(
                    reader.Source.Slice(start, reader.Position - start),
                    value,
                    size,
                    resolution.Members,
                    resolution.Sizes);
            }
        }
    }

    private int ValidateMapWithMemberResolver(
        ref AvroValidationReader reader,
        AvroMemberResolver memberResolver,
        AvroMemberResolution resolution,
        long now,
        bool failFast,
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
                memberResolver.TryGetMapEntryResolver(key, out var valueResolver);
                var valueStart = reader.Position;
                if (failFast && nestedViolations is not null)
                {
                    valuePlan.SkipValue(ref reader, path.RemainingValidationDepth);
                    valueResolver?.Resolve(
                        reader.Source.Slice(valueStart, reader.Position - valueStart),
                        resolution.Members,
                        resolution.Sizes);
                    continue;
                }

                var mark = path.Length;
                path.AppendMapKey(key.Span);
                ValidationCelValue value;
                int size;
                if (valueResolver?.HasChildren == true)
                {
                    value = valuePlan.ValidateAndCaptureWithMemberNode(
                        ref reader,
                        valueResolver,
                        resolution,
                        now,
                        failFast,
                        ref nestedViolations,
                        ref path,
                        out size);
                }
                else
                {
                    value = valuePlan.ValidateAndCapture(
                        ref reader,
                        now,
                        failFast,
                        valueResolver?.NeedsSize ?? false,
                        ref nestedViolations,
                        ref path,
                        out size);
                    valueResolver?.ResolveCaptured(
                        reader.Source.Slice(valueStart, reader.Position - valueStart),
                        value,
                        size,
                        resolution.Members,
                        resolution.Sizes);
                }
                path.Truncate(mark);
            }
        }
    }

    private void ValidateUnionWithMemberResolution(
        ref AvroValidationReader reader,
        AvroCompiledRuleSet rules,
        AvroMemberResolution resolution,
        long now,
        bool failFast,
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
            failFast,
            ref nestedViolations,
            ref path);
        rules.ResolveUnionBranch(
            (int)branch,
            reader.Source.Slice(payloadStart, reader.Position - payloadStart),
            resolution);
    }

    private int ValidateMapWithMemberResolution(
        ref AvroValidationReader reader,
        AvroCompiledRuleSet rules,
        AvroMemberResolution resolution,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? nestedViolations,
        scoped ref AvroValidationPath path)
    {
        if (failFast)
        {
            return ValidateMapWithFailFastMemberResolution(
                ref reader,
                rules,
                resolution,
                now,
                ref nestedViolations,
                ref path);
        }
        if (rules.HasMapEntrySizeDemand)
        {
            return ValidateMapWithSizedMemberResolution(
                ref reader,
                rules,
                resolution,
                now,
                ref nestedViolations,
                ref path);
        }

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
                    failFast: false,
                    needsSize: false,
                    ref nestedViolations,
                    ref path,
                    out var size);
                path.Truncate(mark);
                rules.ResolveMapEntry(
                    key,
                    reader.Source.Slice(valueStart, reader.Position - valueStart),
                    value,
                    size,
                    resolution);
            }
        }
    }

    private int ValidateMapWithSizedMemberResolution(
        ref AvroValidationReader reader,
        AvroCompiledRuleSet rules,
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
                rules.TryGetMapEntryResolver(key, out var memberResolver);
                var valueStart = reader.Position;
                var mark = path.Length;
                path.AppendMapKey(key.Span);
                var value = valuePlan.ValidateAndCapture(
                    ref reader,
                    now,
                    failFast: false,
                    memberResolver?.NeedsSize ?? false,
                    ref nestedViolations,
                    ref path,
                    out var size);
                path.Truncate(mark);
                memberResolver?.ResolveCaptured(
                    reader.Source.Slice(valueStart, reader.Position - valueStart),
                    value,
                    size,
                    resolution.Members,
                    resolution.Sizes);
            }
        }
    }

    private int ValidateMapWithFailFastMemberResolution(
        ref AvroValidationReader reader,
        AvroCompiledRuleSet rules,
        AvroMemberResolution resolution,
        long now,
        ref List<ValidationRuleError>? nestedViolations,
        scoped ref AvroValidationPath path)
    {
        var valuePlan = _children[0];
        var hasSizeDemand = rules.HasMapEntrySizeDemand;
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
                AvroMemberResolver.AvroMemberNode? memberResolver = null;
                if (hasSizeDemand)
                    rules.TryGetMapEntryResolver(key, out memberResolver);
                var valueStart = reader.Position;
                if (nestedViolations is not null)
                {
                    valuePlan.SkipValue(ref reader, path.RemainingValidationDepth);
                    ResolveMapEntry(
                        hasSizeDemand,
                        key,
                        memberResolver,
                        reader.Source.Slice(valueStart, reader.Position - valueStart),
                        rules,
                        resolution);
                    continue;
                }

                var mark = path.Length;
                path.AppendMapKey(key.Span);
                var value = valuePlan.ValidateAndCapture(
                    ref reader,
                    now,
                    failFast: true,
                    memberResolver?.NeedsSize ?? false,
                    ref nestedViolations,
                    ref path,
                    out var size);
                path.Truncate(mark);
                var payload = reader.Source.Slice(valueStart, reader.Position - valueStart);
                if (hasSizeDemand)
                {
                    memberResolver?.ResolveCaptured(
                        payload,
                        value,
                        size,
                        resolution.Members,
                        resolution.Sizes);
                }
                else
                {
                    rules.ResolveMapEntry(key, payload, value, size, resolution);
                }
            }
        }
    }

    private static void ResolveMapEntry(
        bool hasSizeDemand,
        ReadOnlyMemory<byte> key,
        AvroMemberResolver.AvroMemberNode? memberResolver,
        ReadOnlyMemory<byte> payload,
        AvroCompiledRuleSet rules,
        AvroMemberResolution resolution)
    {
        if (hasSizeDemand)
            memberResolver?.Resolve(payload, resolution.Members, resolution.Sizes);
        else
            rules.ResolveMapEntry(key, payload, resolution);
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
                ValidateRecord(ref reader, now, failFast, ref nestedViolations, ref path);
                rootSize = _fields.Length;
                valueKind = ValidationCelValueKind.Object;
                break;
            case global::Avro.ArraySchema:
                rootSize = ValidateArray(ref reader, now, failFast, ref nestedViolations, ref path);
                valueKind = ValidationCelValueKind.Array;
                break;
            case global::Avro.MapSchema:
                rootSize = ValidateMap(ref reader, now, failFast, ref nestedViolations, ref path);
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
                var value = field.Child.ReadRuleValue(
                    ref preview,
                    path.RemainingValidationDepth,
                    field.Rules.UsesRootSize,
                    out var rootSize);
                var payload = preview.Source.Slice(fieldStart, preview.Position - fieldStart);
                field.Rules.Evaluate(
                    value,
                    payload,
                    now,
                    failFast,
                    ref violations,
                    ref path,
                    rootSize);

                if ((failFast && violations is not null) || !field.Child.HasAnyRules)
                    reader = preview;
                else
                    _ = field.Child.Validate(ref reader, now, failFast, ref violations, ref path);
            }
            path.Truncate(mark);
            if (failFast && violations is not null)
            {
                for (var remaining = index + 1; remaining < _fields.Length; remaining++)
                {
                    _fields[remaining].Child.SkipValue(
                        ref reader,
                        path.RemainingValidationDepth);
                }
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
                    var remainingInBlock = count - index - 1;
                    for (long remaining = 0; remaining < remainingInBlock; remaining++)
                        item.SkipValue(ref reader, path.RemainingValidationDepth);
                    itemIndex = checked(itemIndex + (int)remainingInBlock +
                        SkipRemainingCollection(
                            item,
                            ref reader,
                            path.RemainingValidationDepth));
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
                    var remainingInBlock = count - index - 1;
                    for (long remaining = 0; remaining < remainingInBlock; remaining++)
                    {
                        _ = reader.ReadLengthPrefixed();
                        valuePlan.SkipValue(ref reader, path.RemainingValidationDepth);
                    }
                    itemCount = checked(itemCount + (int)remainingInBlock +
                        SkipRemainingMap(
                            valuePlan,
                            ref reader,
                            path.RemainingValidationDepth));
                    return itemCount;
                }
            }
        }
    }

    private static int SkipRemainingCollection(
        AvroValueRulePlan item,
        ref AvroValidationReader reader,
        int remainingValidationDepth)
    {
        var itemCount = 0;
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return itemCount;
            itemCount = checked(itemCount + (int)count);
            for (long index = 0; index < count; index++)
                item.SkipValue(ref reader, remainingValidationDepth);
        }
    }

    private static int SkipRemainingMap(
        AvroValueRulePlan value,
        ref AvroValidationReader reader,
        int remainingValidationDepth)
    {
        var itemCount = 0;
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return itemCount;
            itemCount = checked(itemCount + (int)count);
            for (long index = 0; index < count; index++)
            {
                _ = reader.ReadLengthPrefixed();
                value.SkipValue(ref reader, remainingValidationDepth);
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
            {
                using var resolution = _schemaRules.BeginMemberResolution();
                _schemaRules.EvaluateResolvedWithoutRoot(
                    resolution,
                    payloadLength: 0,
                    now,
                    failFast,
                    ref violations,
                    ref path);
            }
            if (failFast && violations is not null)
            {
                SkipValue(ref reader, CurrentValueDecodingDepth(ref path));
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
            AvroFieldPayload lastFieldPayload;
            {
                using var resolution = _schemaRules.BeginMemberResolution();
                var recordStart = reader.Position;
                lastFieldPayload = _schemaRules.ResolveRecordPrefix(
                    ref reader,
                    deferredIndexes,
                    offsets,
                    path.RemainingValidationDepth,
                    resolution);

                _schemaRules.EvaluateResolvedWithoutRoot(
                    resolution,
                    reader.Position - recordStart,
                    now,
                    failFast,
                    ref violations,
                    ref path);
            }
            if (failFast && violations is not null)
            {
                SkipRecordFields(
                    ref reader,
                    lastMemberFieldIndex + 1,
                    path.RemainingValidationDepth);
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
                    needsSize: false,
                    ref violations,
                    ref path,
                    out _);
                if (failFast && violations is not null)
                {
                    SkipRecordFields(
                        ref reader,
                        lastMemberFieldIndex + 1,
                        path.RemainingValidationDepth);
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
                    needsSize: false,
                    ref violations,
                    ref path,
                    out _);
                if (failFast && violations is not null)
                {
                    SkipRecordFields(
                        ref reader,
                        lastMemberFieldIndex + 1,
                        path.RemainingValidationDepth);
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
                    needsSize: false,
                    ref violations,
                    ref path,
                    out _);
                if (failFast && violations is not null)
                {
                    SkipRecordFields(
                        ref reader,
                        index + 1,
                        path.RemainingValidationDepth);
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

    private void SkipRecordFields(
        ref AvroValidationReader reader,
        int startIndex,
        int remainingValidationDepth)
    {
        for (var index = startIndex; index < _fields.Length; index++)
            _fields[index].Child.SkipValue(ref reader, remainingValidationDepth);
    }

    private static ValidationCelValue ValidateDeferredRecordField(
        AvroFieldRulePlan field,
        ref AvroValidationReader reader,
        long now,
        bool failFast,
        bool needsSize,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path,
        out int size)
    {
        var mark = path.Length;
        path.AppendField(field.Field.Name);
        ValidationCelValue value;
        if (field.Rules.IsEmpty)
        {
            value = field.Child.Validate(ref reader, now, failFast, ref violations, ref path);
            size = -1;
        }
        else if (CanFuseFieldRules(field))
        {
            value = EvaluateFieldRulesWithNestedTraversal(
                field,
                ref reader,
                now,
                failFast,
                ref violations,
                ref path,
                out size);
        }
        else
        {
            var fieldStart = reader.Position;
            var preview = reader;
            value = field.Child.ReadRuleValue(
                ref preview,
                path.RemainingValidationDepth,
                needsSize || field.Rules.UsesRootSize,
                out size);
            var payload = preview.Source.Slice(fieldStart, preview.Position - fieldStart);
            field.Rules.Evaluate(
                value,
                payload,
                now,
                failFast,
                ref violations,
                ref path,
                size);
            if ((failFast && violations is not null) || !field.Child.HasAnyRules)
            {
                reader = preview;
            }
            else
            {
                var capturedValue = field.Child.ValidateAndCapture(
                    ref reader,
                    now,
                    failFast,
                    needsSize && size < 0,
                    ref violations,
                    ref path,
                    out var capturedSize);
                if (capturedValue.Kind != ValidationCelValueKind.Missing)
                    value = capturedValue;
                if (size < 0)
                    size = capturedSize;
            }
        }
        path.Truncate(mark);
        return value;
    }

    private static ValidationCelValue CaptureRecordField(
        AvroFieldRulePlan field,
        ref AvroValidationReader reader,
        int remainingValidationDepth,
        bool needsSize,
        out int size)
    {
        return field.Child.ReadRuleValue(
            ref reader,
            remainingValidationDepth,
            needsSize,
            out size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CurrentValueDecodingDepth(scoped ref AvroValidationPath path) =>
        path.RemainingValidationDepth + (_requiresValidationDepthGuard ? 1 : 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SkipValue(ref AvroValidationReader reader, int remainingValidationDepth)
    {
        if (_requiresBoundedValueDecoding)
        {
            AvroValidationValueDecoder.SkipBounded(
                _schema,
                ref reader,
                remainingValidationDepth);
            return;
        }
        AvroValidationValueDecoder.Skip(_schema, ref reader);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CountValue(ReadOnlyMemory<byte> payload, int remainingValidationDepth) =>
        _requiresBoundedValueDecoding
            ? AvroValidationValueDecoder.CountBounded(
                _schema,
                payload,
                remainingValidationDepth)
            : AvroValidationValueDecoder.Count(_schema, payload);

    private ValidationCelValue ReadRuleValue(
        ref AvroValidationReader reader,
        int remainingValidationDepth,
        bool needsSize,
        out int size)
    {
        if (_schema is global::Avro.UnionSchema union)
        {
            var branch = reader.ReadLong();
            if ((ulong)branch >= (ulong)union.Count)
                throw InvalidPayload($"invalid union index {branch}");
            return _children[(int)branch].ReadRuleValue(
                ref reader,
                remainingValidationDepth,
                needsSize,
                out size);
        }

        return _requiresBoundedValueDecoding
            ? AvroValidationValueDecoder.ReadBounded(
                _schema,
                _enumSymbols,
                ref reader,
                remainingValidationDepth,
                needsSize,
                out size)
            : AvroValidationValueDecoder.Read(
                _schema,
                _enumSymbols,
                ref reader,
                needsSize,
                out size);
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
    ValidationCelValueResolution ValueResolution,
    int IsolatedIndex = -1) : IDisposable
{
    public void Dispose()
    {
        if (IsolatedIndex < 0)
            ValueResolution.Dispose();
        else
            AvroCompiledRuleSet.EndIsolatedMemberResolution(IsolatedIndex);
    }
}

internal readonly record struct AvroFieldPayload(int Start, int Length);

internal sealed class AvroCompiledRuleSet
{
    internal static AvroCompiledRuleSet Empty { get; } = new(
        [], null, false, false, false, false, false, 0, null, new AvroAggregateEqualityComparerFactory());

    private readonly CompiledValidationRule[] _rules;
    private readonly AvroMemberResolver? _members;
    private readonly AvroAggregateEqualityComparer? _rootAggregateComparer;
    private readonly AvroAggregateEqualityComparer?[]? _rootUnionComparers;
    private readonly bool _usesCachedEquality;
    private readonly bool _usesRootAggregateEquality;
    private readonly int _memberCount;
    private readonly int _lastRecordMemberIndex;

    [ThreadStatic]
    private static ValidationCelValueResolutionFrame[]? t_isolatedMemberResolutionFrames;

    [ThreadStatic]
    private static int t_isolatedMemberResolutionDepth;

    private AvroCompiledRuleSet(
        CompiledValidationRule[] rules,
        AvroMemberResolver? members,
        bool usesRootValue,
        bool usesRootSize,
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
        UsesRootSize = usesRootSize;
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
    internal bool UsesRootSize { get; }
    internal bool UsesSize { get; }
    internal bool HasMembers => _members is not null;
    internal AvroMemberResolver MemberResolver => _members!;
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
        var sizedMemberIndexes = new HashSet<int>();
        var compiled = new CompiledValidationRule[rules.Count];
        var usesSize = false;
        var usesRootValue = false;
        var usesRootSize = false;
        var usesCachedEquality = false;
        var usesRootAggregateEquality = false;
        for (var index = 0; index < rules.Count; index++)
        {
            var rule = CompiledValidationRule.Compile(
                rules[index],
                memberIndexes,
                memberPaths,
                usedMemberIndexes,
                sizedMemberIndexes: sizedMemberIndexes);
            compiled[index] = rule;
            usesRootValue |= rule.UsesRootValue;
            usesRootSize |= rule.UsesRootSize;
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
                sizedMemberIndexes,
                aggregateComparerFactory);
        return new AvroCompiledRuleSet(
            compiled,
            members,
            usesRootValue,
            usesRootSize,
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
        try
        {
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
        }
        finally
        {
            if (isNested)
                valueResolution.Dispose();
        }
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
        AvroValueRulePlan valuePlan,
        int remainingValidationDepth,
        long now,
        bool failFast,
        ref List<ValidationRuleError>? violations,
        scoped ref AvroValidationPath path)
    {
        using var resolution = BeginMemberResolution();
        var start = reader.Position;
        if (_members is null)
            valuePlan.SkipValue(ref reader, remainingValidationDepth);
        else
        {
            _members.Resolve(
                ref reader,
                resolution.Members,
                resolution.Sizes,
                remainingValidationDepth);
        }

        EvaluateResolvedWithoutRoot(
            resolution,
            reader.Position - start,
            now,
            failFast,
            ref violations,
            ref path);
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

    internal AvroMemberResolution BeginIsolatedMemberResolution()
    {
        var index = t_isolatedMemberResolutionDepth++;
        var frames = t_isolatedMemberResolutionFrames;
        if (frames is null || frames.Length <= index)
        {
            Array.Resize(
                ref t_isolatedMemberResolutionFrames,
                Math.Max(index + 1, 4));
            frames = t_isolatedMemberResolutionFrames;
        }

        ref var frame = ref frames[index];
        var members = GetIsolatedMemberValues(_memberCount, ref frame);
        var sizes = UsesSize || _members is not null
            ? GetIsolatedSizeValues(_memberCount + 1, ref frame)
            : default;
        return new AvroMemberResolution(members, sizes, default, index);
    }

    private static ValidationCelMemberValues GetIsolatedMemberValues(
        int count,
        ref ValidationCelValueResolutionFrame frame)
    {
        if (count == 0)
            return default;

        var values = frame.MemberValues;
        if (values is null || values.Length < count)
            frame.MemberValues = values = new ValidationCelMemberSlot[Math.Max(count, 8)];
        frame.MemberGeneration = unchecked(frame.MemberGeneration + 1);
        if (frame.MemberGeneration == 0)
        {
            Array.Clear(values);
            frame.MemberGeneration = 1;
        }
        return new ValidationCelMemberValues(values, frame.MemberGeneration);
    }

    private static ValidationCelSizeValues GetIsolatedSizeValues(
        int count,
        ref ValidationCelValueResolutionFrame frame)
    {
        var values = frame.SizeValues;
        if (values is null || values.Length < count)
            frame.SizeValues = values = new ValidationCelSizeSlot[Math.Max(count, 8)];
        frame.SizeGeneration = unchecked(frame.SizeGeneration + 1);
        if (frame.SizeGeneration == 0)
        {
            Array.Clear(values);
            frame.SizeGeneration = 1;
        }
        return new ValidationCelSizeValues(values, frame.SizeGeneration);
    }

    internal static void EndIsolatedMemberResolution(int index)
    {
        if (t_isolatedMemberResolutionDepth != index + 1)
        {
            throw new InvalidOperationException(
                "Isolated Avro member resolutions must be released in reverse order.");
        }
        t_isolatedMemberResolutionDepth--;
    }

    internal AvroFieldPayload ResolveRecordPrefix(
        ref AvroValidationReader reader,
        int[] deferredFieldIndexes,
        scoped Span<int> deferredFieldOffsets,
        int remainingValidationDepth,
        AvroMemberResolution resolution) =>
        _members!.ResolveRecordPrefix(
            ref reader,
            _lastRecordMemberIndex,
            deferredFieldIndexes,
            deferredFieldOffsets,
            remainingValidationDepth,
            resolution.Members,
            resolution.Sizes);

    internal bool HasMapEntrySizeDemand => _members!.HasMapEntrySizeDemand;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetMapEntryResolver(
        ReadOnlyMemory<byte> key,
        out AvroMemberResolver.AvroMemberNode? memberResolver) =>
        _members!.TryGetMapEntryResolver(key, out memberResolver);

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

    internal bool NeedsRecordFieldSize(int fieldIndex) =>
        _members!.NeedsRecordFieldSize(fieldIndex);

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
    private readonly bool _requiresValidationDepthGuard;
    private readonly bool _requiresBoundedMapValueDecoding;
    private readonly bool[]? _unionRequiresBoundedValueDecoding;
    private bool _hasMapEntrySizeDemand;
    private bool _hasDescendantTargets;

    internal bool HasMapEntrySizeDemand => _hasMapEntrySizeDemand;
    internal bool HasDescendantTargets => _hasDescendantTargets;

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
        _requiresValidationDepthGuard = AvroAggregateEqualityComparer.IsRecursive(recordSchema);
    }

    private AvroMemberResolver(
        global::Avro.UnionSchema unionSchema,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        _valueSchema = unionSchema;
        _aggregateComparerFactory = aggregateComparerFactory;
        _unionBranches = new AvroMemberResolver?[unionSchema.Count];
        _unionRequiresBoundedValueDecoding = new bool[unionSchema.Count];
        for (var index = 0; index < unionSchema.Count; index++)
        {
            _unionRequiresBoundedValueDecoding[index] =
                AvroAggregateEqualityComparer.IsRecursive(unionSchema[index]);
        }
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
        _requiresBoundedMapValueDecoding =
            AvroAggregateEqualityComparer.IsRecursive(mapSchema.ValueSchema);
    }

    internal static AvroMemberResolver? Create(
        AvroSchema valueSchema,
        IReadOnlyList<byte[][]> paths,
        IReadOnlyCollection<int> usedIndexes,
        IReadOnlySet<int> sizedIndexes,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        valueSchema = AvroValueRulePlan.Unwrap(valueSchema);
        if (valueSchema is global::Avro.RecordSchema record)
            return CreateRecord(record, paths, usedIndexes, sizedIndexes, aggregateComparerFactory);
        if (valueSchema is global::Avro.MapSchema map)
            return CreateMap(map, paths, usedIndexes, sizedIndexes, aggregateComparerFactory);
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
                    branchResolver.Add(
                        branchRecord,
                        path,
                        memberIndex,
                        sizedIndexes.Contains(memberIndex),
                        depth: 0);
                }
                else
                {
                    var branchMap = (global::Avro.MapSchema)branch;
                    branchResolver ??= CreateMap(branchMap, aggregateComparerFactory);
                    branchResolver.Add(
                        branchMap,
                        path,
                        memberIndex,
                        sizedIndexes.Contains(memberIndex),
                        depth: 0);
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
        IReadOnlySet<int> sizedIndexes,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        var resolver = CreateRecord(record, aggregateComparerFactory);
        foreach (var memberIndex in usedIndexes)
            resolver.Add(
                record,
                paths[memberIndex],
                memberIndex,
                sizedIndexes.Contains(memberIndex),
                depth: 0);
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
        IReadOnlySet<int> sizedIndexes,
        AvroAggregateEqualityComparerFactory aggregateComparerFactory)
    {
        var resolver = CreateMap(map, aggregateComparerFactory);
        foreach (var memberIndex in usedIndexes)
            resolver.Add(
                map,
                paths[memberIndex],
                memberIndex,
                sizedIndexes.Contains(memberIndex),
                depth: 0);
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
        bool needsSize,
        int depth)
    {
        var name = Encoding.UTF8.GetString(path[depth]);
        var field = FindField(schema, name)
            ?? throw new SchemaRegistryRuleException(
                $"Avro validation rule refers to unknown field '{name}' on '{schema.Fullname}'.");
        var node = _fields[field.Pos] ??= new AvroMemberNode(_aggregateComparerFactory);
        if (depth == path.Length - 1)
        {
            node.AddMemberIndex(memberIndex, needsSize);
            return;
        }
        _hasDescendantTargets = true;
        node.AddChild(field.Schema, path, memberIndex, needsSize, depth + 1);
    }

    private void Add(
        global::Avro.MapSchema schema,
        byte[][] path,
        int memberIndex,
        bool needsSize,
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
            node.AddMemberIndex(memberIndex, needsSize);
            _hasMapEntrySizeDemand |= needsSize;
            return;
        }
        _hasDescendantTargets = true;
        node.AddChild(schema.ValueSchema, path, memberIndex, needsSize, depth + 1);
    }

    internal void Resolve(
        ReadOnlyMemory<byte> payload,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes)
    {
        var reader = new AvroValidationReader(payload);
        Resolve(
            ref reader,
            values,
            sizes,
            AvroInlineRuleValidator.MaximumValidationDepth);
    }

    internal void Resolve(
        ref AvroValidationReader reader,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes,
        int remainingValidationDepth)
    {
        if (_mapValues is not null)
        {
            ResolveMap(
                (global::Avro.MapSchema)_valueSchema,
                ref reader,
                values,
                sizes,
                remainingValidationDepth);
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
            {
                SkipValue(
                    union[(int)branch],
                    _unionRequiresBoundedValueDecoding![(int)branch],
                    ref reader,
                    remainingValidationDepth);
            }
            else
                resolver.Resolve(ref reader, values, sizes, remainingValidationDepth);
            return;
        }
        if (_requiresValidationDepthGuard)
        {
            remainingValidationDepth =
                AvroValidationValueDecoder.ConsumeRecordDepth(remainingValidationDepth);
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
                node.SkipValue(ref reader, remainingValidationDepth);
                node.Resolve(
                    reader.Source.Slice(start, reader.Position - start),
                    values,
                    sizes);
            }
            else
            {
                node.SkipValue(ref reader, remainingValidationDepth);
            }
        }
    }

    private void ResolveMap(
        global::Avro.MapSchema map,
        ref AvroValidationReader reader,
        ValidationCelMemberValues values,
        ValidationCelSizeValues sizes,
        int remainingValidationDepth)
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
                SkipValue(
                    map.ValueSchema,
                    _requiresBoundedMapValueDecoding,
                    ref reader,
                    remainingValidationDepth);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SkipValue(
        AvroSchema schema,
        bool requiresBoundedValueDecoding,
        ref AvroValidationReader reader,
        int remainingValidationDepth)
    {
        if (requiresBoundedValueDecoding)
        {
            AvroValidationValueDecoder.SkipBounded(
                schema,
                ref reader,
                remainingValidationDepth);
            return;
        }
        AvroValidationValueDecoder.Skip(schema, ref reader);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetMapEntryResolver(
        ReadOnlyMemory<byte> key,
        out AvroMemberNode? memberResolver) =>
        _mapValues!.TryGetValue(key, out memberResolver);

    internal AvroMemberResolver? GetUnionBranchResolver(int branch) =>
        _unionBranches![branch];

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

    internal AvroMemberNode GetRecordFieldResolver(int fieldIndex) =>
        _fields[fieldIndex]
            ?? throw new InvalidOperationException("Avro validation member resolver is incomplete.");

    internal bool NeedsRecordFieldSize(int fieldIndex) =>
        _fields[fieldIndex]?.NeedsSize ?? false;

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
        int remainingValidationDepth,
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
            node.SkipValue(ref reader, remainingValidationDepth);
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

    internal sealed class AvroMemberNode(
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
        private bool _needsSize;
        private bool _requiresBoundedValueDecoding;
        private bool[]? _unionRequiresBoundedValueDecoding;

        internal AvroSchema? Schema
        {
            get => _schema;
            set
            {
                _schema = value;
                var schema = AvroValueRulePlan.Unwrap(value!);
                _aggregateComparer = aggregateComparerFactory.Create(schema);
                _enumSymbols = AvroValidationValueDecoder.EncodeEnumSymbols(schema);
                _requiresBoundedValueDecoding = AvroAggregateEqualityComparer.IsRecursive(schema);
                if (schema is not global::Avro.UnionSchema union)
                    return;
                _unionComparers = new AvroAggregateEqualityComparer?[union.Count];
                _unionEnumSymbols = new ReadOnlyMemory<byte>[]?[union.Count];
                _unionRequiresBoundedValueDecoding = new bool[union.Count];
                for (var index = 0; index < union.Count; index++)
                {
                    var branch = AvroValueRulePlan.Unwrap(union[index]);
                    _unionComparers[index] = aggregateComparerFactory.Create(branch);
                    _unionEnumSymbols[index] = AvroValidationValueDecoder.EncodeEnumSymbols(branch);
                    _unionRequiresBoundedValueDecoding[index] =
                        AvroAggregateEqualityComparer.IsRecursive(branch);
                }
            }
        }
        internal int MemberIndex { get; private set; } = -1;
        internal bool HasTargets => MemberIndex >= 0 || _children.Count != 0;
        internal bool HasChildren => _children.Count != 0;
        internal bool NeedsSize => _needsSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SkipValue(ref AvroValidationReader reader, int remainingValidationDepth)
        {
            if (_requiresBoundedValueDecoding)
            {
                AvroValidationValueDecoder.SkipBounded(
                    Schema!,
                    ref reader,
                    remainingValidationDepth);
                return;
            }
            AvroValidationValueDecoder.Skip(Schema!, ref reader);
        }

        internal bool TryGetChildResolver(
            AvroSchema schema,
            out AvroMemberResolver childResolver) =>
            _children.TryGetValue(AvroValueRulePlan.Unwrap(schema), out childResolver!);

        internal void AddMemberIndex(int memberIndex, bool needsSize)
        {
            _needsSize |= needsSize;
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
            bool needsSize,
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
                    AddResolverChild(branch, path, memberIndex, needsSize, depth);
                    found = true;
                }
                if (found)
                    return;
            }
            else if (schema is global::Avro.RecordSchema or global::Avro.MapSchema)
            {
                AddResolverChild(schema, path, memberIndex, needsSize, depth);
                return;
            }

            throw new SchemaRegistryRuleException(
                "Avro validation member path can only descend through records, maps, or their unions.");
        }

        private void AddResolverChild(
            AvroSchema schema,
            byte[][] path,
            int memberIndex,
            bool needsSize,
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
                child.Add(recordSchema, path, memberIndex, needsSize, depth);
            else
                child.Add((global::Avro.MapSchema)schema, path, memberIndex, needsSize, depth);
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
                var value = ReadValue(
                    schema,
                    ref valueReader,
                    _needsSize,
                    out var size,
                    out var aggregateComparer);
                var additionalIndexes = _additionalMemberIndexes;
                if (additionalIndexes is null)
                {
                    var sizeIndex = MemberIndex + 1;
                    if (size >= 0)
                        sizes.Set(sizeIndex, size);
                    if (value.Kind is ValidationCelValueKind.Array or ValidationCelValueKind.Object &&
                        value.SizeIndex == 0)
                    {
                        value = value with { SizeIndex = sizeIndex };
                    }
                    values.SetValue(MemberIndex, value, aggregateComparer);
                }
                else
                {
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
            if (value.Kind == ValidationCelValueKind.Missing)
            {
                Resolve(payload, values, sizes);
                return;
            }

            var aggregateComparer = _aggregateComparer;
            if (schema is global::Avro.UnionSchema union)
            {
                var branchReader = new AvroValidationReader(payload);
                var branch = branchReader.ReadLong();
                if ((ulong)branch >= (ulong)union.Count)
                    throw new SchemaRegistryRuleException(
                        $"Could not evaluate Avro validation rules: invalid union index {branch}.");
                schema = AvroValueRulePlan.Unwrap(union[(int)branch]);
                aggregateComparer = _unionComparers![(int)branch];
                payload = branchReader.Source.Slice(branchReader.Position);
            }

            if (MemberIndex >= 0)
            {
                SetMemberValue(MemberIndex, value, size, values, sizes, aggregateComparer);
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
                            aggregateComparer);
                    }
                }
            }

            if (_children.Count == 0)
                return;
            if (_children.TryGetValue(schema, out var child))
                child.Resolve(payload, values, sizes);
        }

        internal void ResolveCapturedValue(
            ReadOnlyMemory<byte> payload,
            ValidationCelValue value,
            int size,
            ValidationCelMemberValues values,
            ValidationCelSizeValues sizes)
        {
            if (MemberIndex < 0)
                return;

            var schema = AvroValueRulePlan.Unwrap(Schema!);
            var aggregateComparer = _aggregateComparer;
            if (schema is global::Avro.UnionSchema union)
            {
                var branchReader = new AvroValidationReader(payload);
                var branch = branchReader.ReadLong();
                if ((ulong)branch >= (ulong)union.Count)
                    throw new SchemaRegistryRuleException(
                        $"Could not evaluate Avro validation rules: invalid union index {branch}.");
                aggregateComparer = _unionComparers![(int)branch];
            }

            SetMemberValue(MemberIndex, value, size, values, sizes, aggregateComparer);
            var additionalIndexes = _additionalMemberIndexes;
            if (additionalIndexes is null)
                return;
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

        private static void SetMemberValue(
            int memberIndex,
            ValidationCelValue value,
            int size,
            ValidationCelMemberValues values,
            ValidationCelSizeValues sizes,
            AvroAggregateEqualityComparer? aggregateComparer)
        {
            var sizeIndex = memberIndex + 1;
            if (size >= 0)
                sizes.Set(sizeIndex, size);
            if (value.Kind is ValidationCelValueKind.Array or ValidationCelValueKind.Object &&
                value.SizeIndex == 0)
            {
                value = value with { SizeIndex = sizeIndex };
            }
            values.SetValue(memberIndex, value, aggregateComparer);
        }

        private ValidationCelValue ReadValue(
            AvroSchema schema,
            ref AvroValidationReader reader,
            bool needsSize,
            out int size,
            out AvroAggregateEqualityComparer? aggregateComparer)
        {
            schema = AvroValueRulePlan.Unwrap(schema);
            if (schema is not global::Avro.UnionSchema union)
            {
                aggregateComparer = _aggregateComparer;
                return _requiresBoundedValueDecoding
                    ? AvroValidationValueDecoder.ReadBounded(
                        schema,
                        _enumSymbols,
                        ref reader,
                        AvroInlineRuleValidator.MaximumValidationDepth,
                        needsSize,
                        out size)
                    : AvroValidationValueDecoder.Read(
                        schema,
                        _enumSymbols,
                        ref reader,
                        needsSize,
                        out size);
            }

            var branch = reader.ReadLong();
            if ((ulong)branch >= (ulong)union.Count)
                throw new SchemaRegistryRuleException(
                    $"Could not evaluate Avro validation rules: invalid union index {branch}.");
            aggregateComparer = _unionComparers![(int)branch];
            return _unionRequiresBoundedValueDecoding![(int)branch]
                ? AvroValidationValueDecoder.ReadBounded(
                    union[(int)branch],
                    _unionEnumSymbols![(int)branch],
                    ref reader,
                    AvroInlineRuleValidator.MaximumValidationDepth,
                    needsSize,
                    out size)
                : AvroValidationValueDecoder.Read(
                    union[(int)branch],
                    _unionEnumSymbols![(int)branch],
                    ref reader,
                    needsSize,
                    out size);
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
        return Read(schema, enumSymbols, ref reader, needsSize: false, out _);
    }

    internal static ValidationCelValue Read(
        AvroSchema schema,
        ReadOnlyMemory<byte>[]? enumSymbols,
        ref AvroValidationReader reader,
        bool needsSize,
        out int size)
    {
        schema = AvroValueRulePlan.Unwrap(schema);
        size = -1;
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
                if (needsSize)
                    size = record.Fields.Count;
                return ValidationCelValue.FromCollection(
                    ValidationCelValueKind.Object,
                    reader.Source.Slice(recordStart, reader.Position - recordStart),
                    0);
            case AvroSchema.Type.Array:
            {
                var collectionStart = reader.Position;
                var itemSchema = ((global::Avro.ArraySchema)schema).ItemSchema;
                if (needsSize)
                    size = SkipCollectionAndCount(itemSchema, isMap: false, ref reader);
                else
                    SkipCollection(itemSchema, isMap: false, ref reader);
                return ValidationCelValue.FromCollection(
                    ValidationCelValueKind.Array,
                    reader.Source.Slice(collectionStart, reader.Position - collectionStart),
                    0);
            }
            case AvroSchema.Type.Map:
            {
                var collectionStart = reader.Position;
                var valueSchema = ((global::Avro.MapSchema)schema).ValueSchema;
                if (needsSize)
                    size = SkipCollectionAndCount(valueSchema, isMap: true, ref reader);
                else
                    SkipCollection(valueSchema, isMap: true, ref reader);
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
                return Read(union[(int)branch], enumSymbols, ref reader, needsSize, out size);
            default:
                throw InvalidPayload($"unsupported schema type {schema.Tag}");
        }
    }

    internal static ValidationCelValue ReadBounded(
        AvroSchema schema,
        ReadOnlyMemory<byte>[]? enumSymbols,
        ref AvroValidationReader reader,
        int remainingValidationDepth,
        bool needsSize,
        out int size)
    {
        schema = AvroValueRulePlan.Unwrap(schema);
        size = -1;
        switch (schema.Tag)
        {
            case AvroSchema.Type.Record:
            case AvroSchema.Type.Error:
            {
                var start = reader.Position;
                SkipBounded(schema, ref reader, remainingValidationDepth);
                if (needsSize)
                    size = ((global::Avro.RecordSchema)schema).Fields.Count;
                return ValidationCelValue.FromCollection(
                    ValidationCelValueKind.Object,
                    reader.Source.Slice(start, reader.Position - start),
                    0);
            }
            case AvroSchema.Type.Array:
            {
                var start = reader.Position;
                var itemSchema = ((global::Avro.ArraySchema)schema).ItemSchema;
                if (needsSize)
                    size = SkipBoundedCollectionAndCount(
                        itemSchema,
                        isMap: false,
                        ref reader,
                        remainingValidationDepth);
                else
                    SkipBoundedCollection(
                        itemSchema,
                        isMap: false,
                        ref reader,
                        remainingValidationDepth);
                return ValidationCelValue.FromCollection(
                    ValidationCelValueKind.Array,
                    reader.Source.Slice(start, reader.Position - start),
                    0);
            }
            case AvroSchema.Type.Map:
            {
                var start = reader.Position;
                var valueSchema = ((global::Avro.MapSchema)schema).ValueSchema;
                if (needsSize)
                    size = SkipBoundedCollectionAndCount(
                        valueSchema,
                        isMap: true,
                        ref reader,
                        remainingValidationDepth);
                else
                    SkipBoundedCollection(
                        valueSchema,
                        isMap: true,
                        ref reader,
                        remainingValidationDepth);
                return ValidationCelValue.FromCollection(
                    ValidationCelValueKind.Object,
                    reader.Source.Slice(start, reader.Position - start),
                    0);
            }
            case AvroSchema.Type.Union:
            {
                var union = (global::Avro.UnionSchema)schema;
                var branch = reader.ReadLong();
                if ((ulong)branch >= (ulong)union.Count)
                    throw InvalidPayload($"invalid union index {branch}");
                return ReadBounded(
                    union[(int)branch],
                    enumSymbols,
                    ref reader,
                    remainingValidationDepth,
                    needsSize,
                    out size);
            }
            default:
                return Read(schema, enumSymbols, ref reader, needsSize, out size);
        }
    }

    internal static void SkipBounded(
        AvroSchema schema,
        ref AvroValidationReader reader,
        int remainingValidationDepth)
    {
        SkipBoundedCore(schema, ref reader, remainingValidationDepth);
    }

    private static void SkipBoundedCore(
        AvroSchema schema,
        ref AvroValidationReader reader,
        int remainingValidationDepth)
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
            {
                remainingValidationDepth = ConsumeRecordDepth(remainingValidationDepth);
                var record = (global::Avro.RecordSchema)schema;
                for (var index = 0; index < record.Fields.Count; index++)
                {
                    SkipBoundedCore(
                        record.Fields[index].Schema,
                        ref reader,
                        remainingValidationDepth);
                }
                return;
            }
            case AvroSchema.Type.Array:
                SkipBoundedCollection(
                    ((global::Avro.ArraySchema)schema).ItemSchema,
                    isMap: false,
                    ref reader,
                    remainingValidationDepth);
                return;
            case AvroSchema.Type.Map:
                SkipBoundedCollection(
                    ((global::Avro.MapSchema)schema).ValueSchema,
                    isMap: true,
                    ref reader,
                    remainingValidationDepth);
                return;
            case AvroSchema.Type.Union:
                var union = (global::Avro.UnionSchema)schema;
                var branch = reader.ReadLong();
                if ((ulong)branch >= (ulong)union.Count)
                    throw InvalidPayload($"invalid union index {branch}");
                SkipBoundedCore(
                    union[(int)branch],
                    ref reader,
                    remainingValidationDepth);
                return;
            default:
                throw InvalidPayload($"unsupported schema type {schema.Tag}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ConsumeRecordDepth(int remainingValidationDepth)
    {
        if (remainingValidationDepth == 0)
            ThrowExcessiveValidationDepth();
        return remainingValidationDepth - 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowExcessiveValidationDepth() =>
        throw new SchemaRegistryRuleException(
            $"Could not evaluate Avro validation rules: value recursion exceeds {AvroInlineRuleValidator.MaximumValidationDepth} levels.");

    private static void SkipBoundedCollection(
        AvroSchema valueSchema,
        bool isMap,
        ref AvroValidationReader reader,
        int remainingValidationDepth)
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
                SkipBoundedCore(valueSchema, ref reader, remainingValidationDepth);
            }
        }
    }

    private static int SkipBoundedCollectionAndCount(
        AvroSchema valueSchema,
        bool isMap,
        ref AvroValidationReader reader,
        int remainingValidationDepth)
    {
        var itemCount = 0L;
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return checked((int)itemCount);
            itemCount = checked(itemCount + count);
            for (long index = 0; index < count; index++)
            {
                if (isMap)
                    _ = reader.ReadLengthPrefixed();
                SkipBoundedCore(valueSchema, ref reader, remainingValidationDepth);
            }
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

    internal static int CountBounded(
        AvroSchema schema,
        ReadOnlyMemory<byte> payload,
        int remainingValidationDepth)
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
        if (schema is global::Avro.ArraySchema array)
        {
            return SkipBoundedCollectionAndCount(
                array.ItemSchema,
                isMap: false,
                ref reader,
                remainingValidationDepth);
        }
        if (schema is global::Avro.MapSchema map)
        {
            return SkipBoundedCollectionAndCount(
                map.ValueSchema,
                isMap: true,
                ref reader,
                remainingValidationDepth);
        }
        return -1;
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

    private static int SkipCollectionAndCount(
        AvroSchema valueSchema,
        bool isMap,
        ref AvroValidationReader reader)
    {
        var itemCount = 0L;
        while (true)
        {
            var count = reader.ReadCollectionCount();
            if (count == 0)
                return checked((int)itemCount);
            itemCount = checked(itemCount + count);
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
    private int _remainingValidationDepth;

    internal AvroValidationPath(Span<char> initialBuffer)
    {
        _buffer = initialBuffer;
        _rented = null;
        _remainingValidationDepth = AvroInlineRuleValidator.MaximumValidationDepth;
        _buffer[0] = '$';
        Length = 1;
    }

    internal int Length { get; private set; }
    internal int RemainingValidationDepth => _remainingValidationDepth;

    internal void EnterValidation()
    {
        if (_remainingValidationDepth == 0)
        {
            throw new SchemaRegistryRuleException(
                $"Could not evaluate Avro validation rules: value recursion exceeds {AvroInlineRuleValidator.MaximumValidationDepth} levels.");
        }
        _remainingValidationDepth--;
    }

    internal void ExitValidation() => _remainingValidationDepth++;

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
        if (characterCount > (int.MaxValue - 4) / 2)
        {
            throw new SchemaRegistryRuleException(
                "Could not evaluate Avro validation rules: map key is too large.");
        }
        EnsureCapacity(characterCount * 2 + 4);
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
