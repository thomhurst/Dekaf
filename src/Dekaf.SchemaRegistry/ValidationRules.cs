using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Dekaf.Errors;
using static Dekaf.SchemaRegistry.ValidationCelHelpers;

namespace Dekaf.SchemaRegistry;

internal interface IInlineValidationRuleExecutor
{
    void Validate(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        string? subject,
        Schema schema,
        bool failFast);
}

/// <summary>
/// Selects when inline schema validation rules run relative to domain rules.
/// </summary>
public enum ValidationRulesExecution
{
    /// <summary>Inline validation rules are not evaluated.</summary>
    Disabled,

    /// <summary>Inline validation rules run before domain rules.</summary>
    BeforeDomainRules,

    /// <summary>Inline validation rules run after domain rules.</summary>
    AfterDomainRules
}

/// <summary>An inline CHECK rule declared by a schema.</summary>
public sealed class ValidationRule
{
    /// <summary>The rule name.</summary>
    public string? Name { get; init; }

    /// <summary>Human-readable rule documentation.</summary>
    public string? Doc { get; init; }

    /// <summary>The CEL expression.</summary>
    public string? Expr { get; init; }

    /// <summary>The equivalent SQL expression, when supplied.</summary>
    public string? Sql { get; init; }
}

/// <summary>One inline validation rule violation.</summary>
public sealed class ValidationRuleError
{
    /// <summary>Creates a validation rule error.</summary>
    public ValidationRuleError(
        ValidationRule rule,
        string fieldPath,
        string? message = null,
        Exception? cause = null)
    {
        Rule = rule ?? throw new ArgumentNullException(nameof(rule));
        FieldPath = fieldPath ?? throw new ArgumentNullException(nameof(fieldPath));
        Message = message;
        Cause = cause;
    }

    /// <summary>The rule that failed.</summary>
    public ValidationRule Rule { get; }

    /// <summary>The dotted path of the value that failed.</summary>
    public string FieldPath { get; }

    /// <summary>An optional dynamic message returned by the rule.</summary>
    public string? Message { get; }

    /// <summary>The evaluation exception, when evaluation failed.</summary>
    public Exception? Cause { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var path = string.IsNullOrEmpty(FieldPath) ? "<root>" : FieldPath;
        var name = string.IsNullOrEmpty(Rule.Name) ? "unnamed" : Rule.Name;
        var detail = !string.IsNullOrEmpty(Message)
            ? Message
            : !string.IsNullOrEmpty(Rule.Doc)
                ? Rule.Doc
                : !string.IsNullOrEmpty(Rule.Sql)
                    ? Rule.Sql
                    : Rule.Expr;
        return Cause is null
            ? $"{path}: {name}: {detail}"
            : $"{path}: {name}: {detail} (caused by: {Cause.Message})";
    }
}

/// <summary>Thrown when one or more inline validation rules fail.</summary>
public sealed class ValidationRulesFailedException : KafkaException
{
    /// <summary>Creates an aggregate validation exception.</summary>
    public ValidationRulesFailedException(IReadOnlyList<ValidationRuleError> violations)
        : base(BuildMessage(violations))
    {
        Violations = violations ?? throw new ArgumentNullException(nameof(violations));
    }

    /// <summary>Every violation found during the schema walk.</summary>
    public IReadOnlyList<ValidationRuleError> Violations { get; }

    private static string BuildMessage(IReadOnlyList<ValidationRuleError>? violations)
    {
        if (violations is null || violations.Count == 0)
            return "Validation rule failed (no detail)";

        var builder = new StringBuilder();
        builder.Append("Validation rule failed (");
        builder.Append(violations.Count.ToString(CultureInfo.InvariantCulture));
        builder.Append(violations.Count == 1 ? " violation):" : " violations):");
        for (var index = 0; index < violations.Count; index++)
        {
            builder.Append("\n  - ");
            builder.Append(violations[index]);
        }

        return builder.ToString();
    }
}

internal enum ValidationResultKind : byte
{
    Boolean,
    String
}

internal readonly record struct ValidationResult(
    ValidationResultKind Kind,
    bool Boolean,
    string? String)
{
    internal static ValidationResult FromBoolean(bool value) =>
        new(ValidationResultKind.Boolean, value, null);

    internal static ValidationResult FromString(string value) =>
        new(ValidationResultKind.String, false, value);
}

internal sealed class CompiledValidationRule
{
    [ThreadStatic]
    private static ValidationCelMemberSlot[]? t_memberValues;

    [ThreadStatic]
    private static uint t_memberGeneration;

    [ThreadStatic]
    private static uint t_memberResolutionEpoch;

    [ThreadStatic]
    private static uint t_resolvedMemberEpoch;

    [ThreadStatic]
    private static uint t_resolvedMemberGeneration;

    [ThreadStatic]
    private static int t_resolvedMemberGroupId;

    [ThreadStatic]
    private static int t_resolvedMemberStart;

    [ThreadStatic]
    private static int t_resolvedMemberLength;

    [ThreadStatic]
    private static ValidationCelSizeSlot[]? t_sizes;

    [ThreadStatic]
    private static uint t_sizeGeneration;

    [ThreadStatic]
    private static ValidationCelEqualitySlot[]? t_equalities;

    [ThreadStatic]
    private static ValidationCelValueResolutionFrame[]? t_valueResolutionFrames;

    [ThreadStatic]
    private static int t_valueResolutionDepth;

    [ThreadStatic]
    private static ValidationCelEqualitySlots t_pairEqualities;

    [ThreadStatic]
    private static uint t_equalityGeneration;

    private readonly ValidationCelNode? _expression;
    private readonly string? _compilationError;

    private CompiledValidationRule(
        ValidationRule rule,
        ValidationCelNode? expression,
        bool usesRootValue = false,
        bool usesRootSize = false,
        bool usesSize = false,
        ValidationCelEqualityPair[]? equalityPairs = null,
        bool usesCachedEquality = false,
        bool usesRootAggregateEquality = false,
        string? compilationError = null)
    {
        Rule = rule;
        _expression = expression;
        UsesRootValue = usesRootValue;
        UsesRootSize = usesRootSize;
        UsesSize = usesSize;
        EqualityPairs = equalityPairs ?? [];
        UsesCachedEquality = usesCachedEquality || EqualityPairs.Length != 0;
        UsesRootAggregateEquality = usesRootAggregateEquality;
        _compilationError = compilationError;
    }

    internal ValidationRule Rule { get; }
    internal bool UsesRootValue { get; }
    internal bool UsesRootSize { get; }
    internal bool UsesSize { get; }
    internal ValidationCelEqualityPair[] EqualityPairs { get; }
    internal bool UsesCachedEquality { get; }
    internal bool UsesRootAggregateEquality { get; }

    internal static CompiledValidationRule Compile(
        ValidationRule rule,
        Dictionary<string, int> memberIndexes,
        List<byte[][]> memberPaths,
        HashSet<int> usedMemberIndexes,
        int equalityIndexOffset = 0,
        Dictionary<ValidationCelEqualityOperands, int>? equalityIndexes = null,
        HashSet<int>? sizedMemberIndexes = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (string.IsNullOrWhiteSpace(rule.Expr))
        {
            return new CompiledValidationRule(
                rule,
                expression: null,
                compilationError: $"Validation rule '{rule.Name ?? "unnamed"}' has no expression.");
        }

        try
        {
            var parser = new ValidationCelParser(
                rule.Expr,
                memberIndexes,
                memberPaths,
                usedMemberIndexes,
                equalityIndexOffset,
                equalityIndexes,
                sizedMemberIndexes);
            var expression = parser.Parse();
            return new CompiledValidationRule(
                rule,
                expression,
                parser.UsesRootValue,
                parser.UsesRootSize,
                parser.UsesSize,
                parser.EqualityPairs,
                parser.UsesCachedEquality,
                parser.UsesRootAggregateEquality);
        }
        catch (SchemaRegistryRuleException exception)
        {
            return new CompiledValidationRule(
                rule,
                expression: null,
                compilationError: $"Could not compile validation rule '{rule.Name ?? "unnamed"}': {exception.Message}");
        }
    }

    internal ValidationResult Evaluate(
        ReadOnlyMemory<byte> value,
        long nowUnixMilliseconds,
        ValidationCelMemberValues memberValues,
        ValidationCelSizeValues sizes,
        uint equalityGeneration)
        => EvaluateCore(
            value,
            default,
            useTypedValues: false,
            nowUnixMilliseconds,
            memberValues,
            sizes,
            equalityGeneration,
            rootAggregateComparer: null);

    internal ValidationResult Evaluate(
        ValidationCelValue value,
        long nowUnixMilliseconds,
        ValidationCelMemberValues memberValues,
        ValidationCelSizeValues sizes,
        uint equalityGeneration,
        IValidationCelAggregateComparer? rootAggregateComparer = null)
        => EvaluateCore(
            default,
            value,
            useTypedValues: true,
            nowUnixMilliseconds,
            memberValues,
            sizes,
            equalityGeneration,
            rootAggregateComparer);

    private ValidationResult EvaluateCore(
        ReadOnlyMemory<byte> value,
        ValidationCelValue typedValue,
        bool useTypedValues,
        long nowUnixMilliseconds,
        ValidationCelMemberValues memberValues,
        ValidationCelSizeValues sizes,
        uint equalityGeneration,
        IValidationCelAggregateComparer? rootAggregateComparer)
    {
        if (_compilationError is not null)
            throw new SchemaRegistryRuleException(_compilationError);

        try
        {
            var result = _expression!.Evaluate(new ValidationCelContext(
                value,
                typedValue,
                useTypedValues,
                nowUnixMilliseconds,
                memberValues,
                sizes,
                equalityGeneration,
                rootAggregateComparer));
            return result.Kind switch
            {
                ValidationCelValueKind.Boolean => ValidationResult.FromBoolean(result.Boolean),
                ValidationCelValueKind.String => ValidationResult.FromString(result.GetString()),
                _ => throw new SchemaRegistryRuleException(
                    $"Validation rule '{Rule.Name ?? "unnamed"}' must return bool or string.")
            };
        }
        catch (SchemaRegistryRuleException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SchemaRegistryRuleException(
                $"Could not evaluate validation rule '{Rule.Name ?? "unnamed"}'.",
                exception);
        }
    }

    internal static ValidationCelMemberValues GetMemberValues(int count)
    {
        var values = t_memberValues;
        if (values is null || values.Length < count)
            t_memberValues = values = new ValidationCelMemberSlot[Math.Max(count, 8)];

        var generation = unchecked(++t_memberGeneration);
        if (generation == 0)
        {
            Array.Clear(values);
            generation = ++t_memberGeneration;
        }
        return new ValidationCelMemberValues(values, generation);
    }

    internal static ValidationCelSizeValues GetSizeValues(int count)
    {
        var sizes = t_sizes;
        if (sizes is null || sizes.Length < count)
            t_sizes = sizes = new ValidationCelSizeSlot[Math.Max(count, 8)];

        var generation = unchecked(++t_sizeGeneration);
        if (generation == 0)
        {
            Array.Clear(sizes);
            generation = ++t_sizeGeneration;
        }
        return new ValidationCelSizeValues(sizes, generation);
    }

    internal static ValidationCelValueResolution BeginValueResolution()
    {
        var index = t_valueResolutionDepth++;
        var frameIndex = index - 1;
        var frames = t_valueResolutionFrames;
        if (frameIndex >= 0 && (frames is null || frames.Length <= frameIndex))
            GrowValueResolutionFrames(frameIndex + 1);
        return new ValidationCelValueResolution(index);
    }

    internal static bool HasActiveValueResolution => t_valueResolutionDepth != 0;

    internal static int ValueResolutionDepth => t_valueResolutionDepth;

    internal static void RestoreValueResolutionDepth(int depth) =>
        t_valueResolutionDepth = depth;

    internal static ValidationCelMemberValues GetMemberValues(
        int count,
        ValidationCelValueResolution resolution)
    {
        if (resolution.Index == 0)
            return GetMemberValues(count);

        var index = resolution.Index - 1;
        ref var frame = ref t_valueResolutionFrames![index];
        var values = frame.MemberValues;
        if (values is null || values.Length < count)
            frame.MemberValues = values = new ValidationCelMemberSlot[Math.Max(count, 8)];

        ref var generation = ref frame.MemberGeneration;
        generation = unchecked(generation + 1);
        if (generation == 0)
        {
            Array.Clear(values);
            generation = 1;
        }
        return new ValidationCelMemberValues(values, generation);
    }

    internal static ValidationCelSizeValues GetSizeValues(
        int count,
        ValidationCelValueResolution resolution)
    {
        if (resolution.Index == 0)
            return GetSizeValues(count);

        var index = resolution.Index - 1;
        ref var frame = ref t_valueResolutionFrames![index];
        var sizes = frame.SizeValues;
        if (sizes is null || sizes.Length < count)
            frame.SizeValues = sizes = new ValidationCelSizeSlot[Math.Max(count, 8)];

        ref var generation = ref frame.SizeGeneration;
        generation = unchecked(generation + 1);
        if (generation == 0)
        {
            Array.Clear(sizes);
            generation = 1;
        }
        return new ValidationCelSizeValues(sizes, generation);
    }

    internal static void EndValueResolution(ValidationCelValueResolution resolution)
    {
        if (t_valueResolutionDepth != resolution.Index + 1)
            throw new InvalidOperationException("CEL value resolutions must be released in reverse order.");
        t_valueResolutionDepth--;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GrowValueResolutionFrames(int count)
    {
        var length = Math.Max(count, 4);
        Array.Resize(ref t_valueResolutionFrames, length);
    }

    internal static uint BeginEqualityResolution()
    {
        var equalities = t_equalities;
        if (equalities is null)
            t_equalities = equalities = new ValidationCelEqualitySlot[8];
        if (unchecked(++t_equalityGeneration) == 0)
        {
            Array.Clear(equalities);
            t_pairEqualities = default;
            t_equalityGeneration = 1;
        }
        return t_equalityGeneration;
    }

    internal static bool TryGetEquality(
        uint equalityGeneration,
        int equalityIndex,
        out bool value)
    {
        if (equalityGeneration == 0)
        {
            value = false;
            return false;
        }
        EnsureEqualityCapacity(equalityIndex);
        ref readonly var slot = ref t_equalities![equalityIndex];
        value = slot.Value;
        return slot.Generation == equalityGeneration;
    }

    internal static void SetEquality(
        uint equalityGeneration,
        int equalityIndex,
        bool value)
    {
        if (equalityGeneration == 0)
            return;
        EnsureEqualityCapacity(equalityIndex);
        ref var slot = ref t_equalities![equalityIndex];
        slot.Value = value;
        slot.Generation = equalityGeneration;
    }

    internal static bool TryGetEquality(
        uint equalityGeneration,
        int leftIndex,
        int rightIndex,
        out bool value)
    {
        NormalizeEqualityIndexes(ref leftIndex, ref rightIndex);
        ref readonly var slot = ref t_pairEqualities[GetEqualitySlot(leftIndex, rightIndex)];
        value = slot.Value;
        return slot.Generation == equalityGeneration &&
            slot.LeftIndex == leftIndex &&
            slot.RightIndex == rightIndex;
    }

    internal static void SetEquality(
        uint equalityGeneration,
        int leftIndex,
        int rightIndex,
        bool value)
    {
        NormalizeEqualityIndexes(ref leftIndex, ref rightIndex);
        ref var slot = ref t_pairEqualities[GetEqualitySlot(leftIndex, rightIndex)];
        slot.LeftIndex = leftIndex;
        slot.RightIndex = rightIndex;
        slot.Value = value;
        slot.Generation = equalityGeneration;
    }

    private static void NormalizeEqualityIndexes(ref int leftIndex, ref int rightIndex)
    {
        if (leftIndex > rightIndex)
            (leftIndex, rightIndex) = (rightIndex, leftIndex);
    }

    private static int GetEqualitySlot(int leftIndex, int rightIndex) =>
        (int)(((uint)leftIndex * 397u ^ (uint)rightIndex) & 7u);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnsureEqualityCapacity(int equalityIndex)
    {
        if ((uint)equalityIndex < (uint)t_equalities!.Length)
            return;
        var equalities = t_equalities;
        Array.Resize(ref equalities, Math.Max(equalityIndex + 1, equalities.Length * 2));
        t_equalities = equalities;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ValidationCelSizeValues GrowSizeValues(
        ValidationCelSizeValues current,
        int count)
    {
        var sizes = t_sizes!;
        Array.Resize(ref sizes, Math.Max(count, sizes.Length * 2));
        t_sizes = sizes;
        return new ValidationCelSizeValues(sizes, current.Generation);
    }

    internal static void BeginMemberResolution()
    {
        if (unchecked(++t_memberResolutionEpoch) == 0)
        {
            t_memberResolutionEpoch = 1;
            t_resolvedMemberEpoch = 0;
        }
    }

    internal static ValidationCelMemberValues GetOrResolveMemberValues(
        ValidationCelMemberTable memberTable,
        int memberGroupId,
        int valueStart,
        ReadOnlyMemory<byte> value)
    {
        if (t_resolvedMemberEpoch == t_memberResolutionEpoch &&
            t_resolvedMemberGeneration == t_memberGeneration &&
            t_resolvedMemberGroupId == memberGroupId &&
            t_resolvedMemberStart == valueStart &&
            t_resolvedMemberLength == value.Length)
        {
            return new ValidationCelMemberValues(t_memberValues!, t_memberGeneration);
        }

        var memberValues = GetMemberValues(memberTable.Count);
        memberTable.Resolve(value, memberValues);
        t_resolvedMemberEpoch = t_memberResolutionEpoch;
        t_resolvedMemberGeneration = t_memberGeneration;
        t_resolvedMemberGroupId = memberGroupId;
        t_resolvedMemberStart = valueStart;
        t_resolvedMemberLength = value.Length;
        return memberValues;
    }
}

internal readonly struct ValidationCelValueResolution(int index) : IDisposable
{
    internal int Index { get; } = index;

    public void Dispose() => CompiledValidationRule.EndValueResolution(this);
}

internal struct ValidationCelValueResolutionFrame
{
    internal ValidationCelMemberSlot[]? MemberValues;
    internal uint MemberGeneration;
    internal ValidationCelSizeSlot[]? SizeValues;
    internal uint SizeGeneration;
}

internal readonly record struct ValidationCelContext(
    ReadOnlyMemory<byte> This,
    ValidationCelValue TypedThis,
    bool UsesTypedValues,
    long NowUnixMilliseconds,
    ValidationCelMemberValues MemberValues,
    ValidationCelSizeValues Sizes,
    uint EqualityGeneration,
    IValidationCelAggregateComparer? RootAggregateComparer);

internal struct ValidationCelMemberSlot
{
    internal int Start;
    internal int Length;
    internal ValidationCelValue Value;
    internal IValidationCelAggregateComparer? AggregateComparer;
    internal bool IsTyped;
    internal bool IsPresent;
    internal uint Generation;
}

internal readonly struct ValidationCelMemberValues(
    ValidationCelMemberSlot[] values,
    uint generation)
{
    internal bool IsSet(int index) => values[index].Generation == generation;

    internal bool IsPresent(int index)
    {
        ref readonly var value = ref values[index];
        return value.Generation == generation && value.IsPresent;
    }

    internal ReadOnlyMemory<byte> Get(int index, ReadOnlyMemory<byte> source)
    {
        ref readonly var value = ref values[index];
        return value.Generation == generation
            ? source.Slice(value.Start, value.Length)
            : default;
    }

    internal ValidationCelValue GetValue(int index, ReadOnlyMemory<byte> source)
    {
        ref readonly var value = ref values[index];
        if (value.Generation != generation)
            return ValidationCelValue.Missing;
        return value.IsTyped
            ? value.Value
            : ValidationCelValue.FromJson(source.Slice(value.Start, value.Length), index + 1);
    }

    internal IValidationCelAggregateComparer? GetAggregateComparer(int index)
    {
        ref readonly var value = ref values[index];
        return value.Generation == generation && value.IsTyped
            ? value.AggregateComparer
            : null;
    }

    internal void Set(int index, int start, int length)
    {
        ref var value = ref values[index];
        value.Start = start;
        value.Length = length;
        value.AggregateComparer = null;
        value.IsTyped = false;
        value.IsPresent = true;
        value.Generation = generation;
    }

    internal void SetValue(
        int index,
        ValidationCelValue typedValue,
        IValidationCelAggregateComparer? aggregateComparer = null)
    {
        ref var value = ref values[index];
        value.Value = typedValue;
        value.AggregateComparer = aggregateComparer;
        value.IsTyped = true;
        value.IsPresent = true;
        value.Generation = generation;
    }

    internal void SetDefaultValue(int index, ValidationCelValue typedValue)
    {
        ref var value = ref values[index];
        value.Value = typedValue;
        value.IsTyped = true;
        value.IsPresent = false;
        value.Generation = generation;
    }

    internal void Clear(int index) => values[index].Generation = 0;
}

internal struct ValidationCelSizeSlot
{
    internal int Value;
    internal uint Generation;
}

internal readonly struct ValidationCelSizeValues(
    ValidationCelSizeSlot[] values,
    uint generation)
{
    internal int Capacity => values.Length;
    internal uint Generation => generation;

    internal bool TryGet(int index, out int value)
    {
        ref readonly var slot = ref values[index];
        value = slot.Value;
        return slot.Generation == generation;
    }

    internal void Set(int index, int value)
    {
        ref var slot = ref values[index];
        slot.Value = value;
        slot.Generation = generation;
    }
}

internal struct ValidationCelEqualitySlot
{
    internal int LeftIndex;
    internal int RightIndex;
    internal bool Value;
    internal uint Generation;
}

[InlineArray(8)]
internal struct ValidationCelEqualitySlots
{
    private ValidationCelEqualitySlot _element0;
}

internal readonly record struct ValidationCelEqualityPair(
    int EqualityIndex,
    int LeftValueIndex,
    int RightValueIndex);

internal readonly record struct ValidationCelEqualityOperands(
    int LeftValueIndex,
    int RightValueIndex);

internal static class ValidationCelJsonReader
{
    internal static JsonReaderOptions Options { get; } = new()
    {
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 128
    };
}

internal ref struct JsonObjectPropertyIndex
{
    private const int InitialEntryCapacity = 8;
    private const int InitialBucketCapacity = InitialEntryCapacity * 2;

    [InlineArray(InitialEntryCapacity)]
    private struct InitialEntries
    {
        private Entry _element0;
    }

    [InlineArray(InitialBucketCapacity)]
    private struct InitialBuckets
    {
        private int _element0;
    }

    private InitialEntries _initialEntries;
    private InitialBuckets _initialBuckets;
    private Entry[]? _rentedEntries;
    private int[]? _rentedBuckets;
    private int _entryCapacity;
    private int _bucketCapacity;
    private int _count;

    public JsonObjectPropertyIndex()
    {
        Unsafe.SkipInit(out _initialEntries);
        Unsafe.SkipInit(out _initialBuckets);
        _rentedEntries = null;
        _rentedBuckets = null;
        _entryCapacity = InitialEntryCapacity;
        _bucketCapacity = InitialBucketCapacity;
        _count = 0;
        Span<int> buckets = _initialBuckets;
        buckets.Fill(-1);
    }

    internal readonly int Count => _count;

    internal void Build(ref Utf8JsonReader reader, ReadOnlySpan<byte> source)
    {
        var scan = reader;
        while (scan.Read() && scan.TokenType != JsonTokenType.EndObject)
        {
            if (scan.TokenType != JsonTokenType.PropertyName)
                return;

            AddOrReplace(ref scan, source);
            if (!scan.Read())
                return;
            scan.Skip();
        }
    }

    internal readonly bool IsLast(ref Utf8JsonReader reader, ReadOnlySpan<byte> source)
    {
        var hash = HashName(ref reader);
        var bucket = (int)(hash & (uint)(_bucketCapacity - 1));
        while (true)
        {
            var entryIndex = GetBucket(bucket);
            if (entryIndex < 0)
                return false;

            var entry = GetEntry(entryIndex);
            if (entry.Hash == hash && NameEquals(source, entry, ref reader))
                return entry.TokenStart == checked((int)reader.TokenStartIndex);
            bucket = (bucket + 1) & (_bucketCapacity - 1);
        }
    }

    private void AddOrReplace(ref Utf8JsonReader reader, ReadOnlySpan<byte> source)
    {
        var hash = HashName(ref reader);
        var bucket = FindBucket(source, hash, ref reader, out var entryIndex);
        if (entryIndex >= 0)
        {
            SetEntry(entryIndex, CreateEntry(hash, ref reader));
            return;
        }

        if (_count == _entryCapacity)
        {
            Grow();
            bucket = FindBucket(source, hash, ref reader, out _);
        }

        SetEntry(_count, CreateEntry(hash, ref reader));
        SetBucket(bucket, _count++);
    }

    internal static bool NamesEqual(
        ReadOnlySpan<byte> source,
        ref Utf8JsonReader left,
        ref Utf8JsonReader right)
    {
        var hash = HashName(ref left);
        return hash == HashName(ref right) &&
            NameEquals(source, CreateEntry(hash, ref left), ref right);
    }

    internal void Dispose()
    {
        if (_rentedBuckets is not null)
            ArrayPool<int>.Shared.Return(_rentedBuckets);
        if (_rentedEntries is not null)
            ArrayPool<Entry>.Shared.Return(_rentedEntries);
    }

    private readonly int FindBucket(
        ReadOnlySpan<byte> source,
        uint hash,
        ref Utf8JsonReader reader,
        out int entryIndex)
    {
        var bucket = (int)(hash & (uint)(_bucketCapacity - 1));
        while (true)
        {
            entryIndex = GetBucket(bucket);
            if (entryIndex < 0 ||
                (GetEntry(entryIndex).Hash == hash &&
                 NameEquals(source, GetEntry(entryIndex), ref reader)))
            {
                return bucket;
            }
            bucket = (bucket + 1) & (_bucketCapacity - 1);
        }
    }

    private void Grow()
    {
        var entryCapacity = checked(_entryCapacity * 2);
        var bucketCapacity = checked(entryCapacity * 2);
        var entries = ArrayPool<Entry>.Shared.Rent(entryCapacity);
        var buckets = ArrayPool<int>.Shared.Rent(bucketCapacity);
        var newEntries = entries.AsSpan(0, entryCapacity);
        var newBuckets = buckets.AsSpan(0, bucketCapacity);
        for (var index = 0; index < _count; index++)
            newEntries[index] = GetEntry(index);
        newBuckets.Fill(-1);

        for (var index = 0; index < _count; index++)
        {
            var bucket = (int)(newEntries[index].Hash & (uint)(bucketCapacity - 1));
            while (newBuckets[bucket] >= 0)
                bucket = (bucket + 1) & (bucketCapacity - 1);
            newBuckets[bucket] = index;
        }

        if (_rentedBuckets is not null)
            ArrayPool<int>.Shared.Return(_rentedBuckets);
        if (_rentedEntries is not null)
            ArrayPool<Entry>.Shared.Return(_rentedEntries);
        _rentedEntries = entries;
        _rentedBuckets = buckets;
        _entryCapacity = entryCapacity;
        _bucketCapacity = bucketCapacity;
    }

    private readonly Entry GetEntry(int index) => _rentedEntries is null
        ? _initialEntries[index]
        : _rentedEntries[index];

    private void SetEntry(int index, Entry entry)
    {
        if (_rentedEntries is null)
            _initialEntries[index] = entry;
        else
            _rentedEntries[index] = entry;
    }

    private readonly int GetBucket(int index) => _rentedBuckets is null
        ? _initialBuckets[index]
        : _rentedBuckets[index];

    private void SetBucket(int index, int value)
    {
        if (_rentedBuckets is null)
            _initialBuckets[index] = value;
        else
            _rentedBuckets[index] = value;
    }

    private static Entry CreateEntry(uint hash, ref Utf8JsonReader reader) => new()
    {
        Hash = hash,
        NameStart = checked((int)reader.TokenStartIndex + 1),
        NameLength = reader.ValueSpan.Length,
        TokenStart = checked((int)reader.TokenStartIndex),
        NameEscaped = reader.ValueIsEscaped
    };

    private static bool NameEquals(
        ReadOnlySpan<byte> source,
        Entry entry,
        ref Utf8JsonReader reader)
    {
        if (!entry.NameEscaped)
            return reader.ValueTextEquals(source.Slice(entry.NameStart, entry.NameLength));

        var leftReader = new Utf8JsonReader(
            source.Slice(entry.NameStart - 1, entry.NameLength + 2),
            ValidationCelJsonReader.Options);
        _ = leftReader.Read();
        var maximumLength = leftReader.ValueSpan.Length;
        byte[]? rented = null;
        Span<byte> decoded = maximumLength <= 256
            ? stackalloc byte[maximumLength]
            : (rented = ArrayPool<byte>.Shared.Rent(maximumLength));
        try
        {
            return reader.ValueTextEquals(decoded[..leftReader.CopyString(decoded)]);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static uint HashName(ref Utf8JsonReader reader)
    {
        if (!reader.ValueIsEscaped)
            return Hash(reader.ValueSpan);

        var maximumLength = reader.ValueSpan.Length;
        byte[]? rented = null;
        Span<byte> decoded = maximumLength <= 256
            ? stackalloc byte[maximumLength]
            : (rented = ArrayPool<byte>.Shared.Rent(maximumLength));
        try
        {
            return Hash(decoded[..reader.CopyString(decoded)]);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static uint Hash(ReadOnlySpan<byte> value)
    {
        var hash = 2166136261u;
        for (var index = 0; index < value.Length; index++)
            hash = (hash ^ value[index]) * 16777619u;
        return hash;
    }

    private struct Entry
    {
        internal uint Hash;
        internal int NameStart;
        internal int NameLength;
        internal int TokenStart;
        internal bool NameEscaped;
    }
}

internal sealed class ValidationCelMemberTable
{
    private readonly MemberNode _root;

    internal ValidationCelMemberTable(
        IReadOnlyList<byte[][]> paths,
        IReadOnlyList<int> memberIndexes,
        int valueCount)
    {
        Count = valueCount;
        var root = new MemberNodeBuilder();
        for (var index = 0; index < memberIndexes.Count; index++)
        {
            var memberIndex = memberIndexes[index];
            root.Add(paths[memberIndex], memberIndex, depth: 0);
        }
        _root = root.Build();
    }

    internal int Count { get; }

    internal void Resolve(ReadOnlyMemory<byte> json, ValidationCelMemberValues values)
    {
        var reader = new Utf8JsonReader(json.Span, ValidationCelJsonReader.Options);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            return;

        _root.Resolve(ref reader, json, values);
    }

    private sealed class MemberNode(
        byte[][] names,
        int[] valueIndexes,
        MemberNode?[] children,
        int[] terminalValueIndexes)
    {
        private readonly byte[][] _names = names;
        private readonly int[] _valueIndexes = valueIndexes;
        private readonly MemberNode?[] _children = children;
        private readonly int[] _terminalValueIndexes = terminalValueIndexes;
        // A generation-stamped empty slice records a match while remaining a CEL missing value.
        private readonly int _markerValueIndex = terminalValueIndexes[0];
        private readonly int[] _buckets = CreateBuckets(names);

        internal void Resolve(
            ref Utf8JsonReader reader,
            ReadOnlyMemory<byte> json,
            ValidationCelMemberValues values)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    return;
                var index = Find(ref reader);
                if (!reader.Read())
                    return;
                var start = checked((int)reader.TokenStartIndex);
                if (index >= 0 && _children[index] is { } child)
                {
                    if (values.IsSet(child._markerValueIndex))
                        child.Clear(values);
                    if (reader.TokenType == JsonTokenType.StartObject)
                        child.Resolve(ref reader, json, values);
                    else
                        reader.Skip();
                    if (!values.IsSet(child._markerValueIndex))
                        values.Set(child._markerValueIndex, 0, 0);
                }
                else
                {
                    reader.Skip();
                }

                if (index >= 0 && _valueIndexes[index] >= 0)
                {
                    values.Set(
                        _valueIndexes[index],
                        start,
                        checked((int)reader.BytesConsumed) - start);
                }
            }
        }

        private void Clear(ValidationCelMemberValues values)
        {
            for (var index = 0; index < _terminalValueIndexes.Length; index++)
                values.Clear(_terminalValueIndexes[index]);
        }

        private int Find(ref Utf8JsonReader reader)
        {
            var maximumLength = reader.ValueSpan.Length;
            byte[]? rented = null;
            Span<byte> decoded = reader.ValueIsEscaped
                ? maximumLength <= 256
                    ? stackalloc byte[maximumLength]
                    : (rented = ArrayPool<byte>.Shared.Rent(maximumLength))
                : default;
            try
            {
                var name = reader.ValueIsEscaped
                    ? decoded[..reader.CopyString(decoded)]
                    : reader.ValueSpan;
                var hash = Hash(name);
                var bucket = (int)(hash & (uint)(_buckets.Length - 1));
                while (true)
                {
                    var stored = _buckets[bucket];
                    if (stored == 0)
                        return -1;
                    var index = stored - 1;
                    if (name.SequenceEqual(_names[index]))
                        return index;
                    bucket = (bucket + 1) & (_buckets.Length - 1);
                }
            }
            finally
            {
                if (rented is not null)
                    ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private sealed class MemberNodeBuilder
    {
        private readonly List<byte[]> _names = [];
        private readonly List<int> _valueIndexes = [];
        private readonly List<MemberNodeBuilder?> _children = [];
        private readonly List<int> _terminalValueIndexes = [];

        internal void Add(byte[][] path, int valueIndex, int depth)
        {
            _terminalValueIndexes.Add(valueIndex);
            var memberIndex = FindOrAdd(path[depth]);
            if (depth == path.Length - 1)
            {
                _valueIndexes[memberIndex] = valueIndex;
                return;
            }

            var child = _children[memberIndex] ??= new MemberNodeBuilder();
            child.Add(path, valueIndex, depth + 1);
        }

        internal MemberNode Build()
        {
            var children = new MemberNode?[_children.Count];
            for (var index = 0; index < children.Length; index++)
                children[index] = _children[index]?.Build();
            return new MemberNode(
                [.. _names],
                [.. _valueIndexes],
                children,
                [.. _terminalValueIndexes]);
        }

        private int FindOrAdd(byte[] name)
        {
            for (var index = 0; index < _names.Count; index++)
            {
                if (name.AsSpan().SequenceEqual(_names[index]))
                    return index;
            }

            _names.Add(name);
            _valueIndexes.Add(-1);
            _children.Add(null);
            return _names.Count - 1;
        }
    }

    private static int[] CreateBuckets(byte[][] names)
    {
        var capacity = 4;
        while (capacity < names.Length * 2)
            capacity <<= 1;
        var buckets = new int[capacity];
        for (var index = 0; index < names.Length; index++)
        {
            var bucket = (int)(Hash(names[index]) & (uint)(capacity - 1));
            while (buckets[bucket] != 0)
                bucket = (bucket + 1) & (capacity - 1);
            buckets[bucket] = index + 1;
        }
        return buckets;
    }

    private static uint Hash(ReadOnlySpan<byte> value)
    {
        var hash = 2166136261u;
        for (var index = 0; index < value.Length; index++)
            hash = (hash ^ value[index]) * 16777619u;
        return hash;
    }
}

internal enum ValidationCelValueKind : byte
{
    Missing,
    Null,
    Boolean,
    Number,
    String,
    Bytes,
    Object,
    Array
}

internal interface IValidationCelAggregateComparer
{
    object? RawEqualityToken { get; }

    bool AreEqual(
        ReadOnlyMemory<byte> left,
        IValidationCelAggregateComparer rightComparer,
        ReadOnlyMemory<byte> right);
}

internal readonly record struct ValidationCelValue(
    ValidationCelValueKind Kind,
    ReadOnlyMemory<byte> Json,
    bool Boolean,
    decimal Number,
    string? Literal,
    ReadOnlyMemory<byte> Utf8Literal,
    bool NumberNegated = false,
    int SizeIndex = -1,
    bool IsUtf8Literal = false,
    double Floating = 0,
    bool IsFloating = false,
    bool IsFloatingLiteral = false)
{
    internal static ValidationCelValue Missing { get; } = new(ValidationCelValueKind.Missing, default, false, 0, null, default);
    internal static ValidationCelValue Null { get; } = new(ValidationCelValueKind.Null, default, false, 0, null, default);
    internal static ValidationCelValue True { get; } = new(ValidationCelValueKind.Boolean, default, true, 0, null, default);
    internal static ValidationCelValue False { get; } = new(ValidationCelValueKind.Boolean, default, false, 0, null, default);

    internal static ValidationCelValue FromBoolean(bool value) => value ? True : False;
    internal static ValidationCelValue FromNumber(decimal value) =>
        new(ValidationCelValueKind.Number, default, true, value, null, default);

    internal static ValidationCelValue FromFloating(double value) =>
        new(
            ValidationCelValueKind.Number,
            default,
            false,
            0,
            null,
            default,
            Floating: value,
            IsFloating: true);

    internal static ValidationCelValue NegateNumber(ValidationCelValue value)
    {
        if (value.Kind != ValidationCelValueKind.Number)
            throw Unsupported("CEL arithmetic operators require numeric operands.");
        return value with
        {
            Number = !value.IsFloating && value.Boolean
                ? value.Number == decimal.MinValue ? decimal.MaxValue : -value.Number
                : value.Number,
            Floating = value.IsFloating || value.IsFloatingLiteral
                ? -value.Floating
                : value.Floating,
            NumberNegated = (!value.Json.IsEmpty || !value.Utf8Literal.IsEmpty) &&
                !value.NumberNegated
        };
    }

    internal static ValidationCelValue FromNumberLiteral(string text)
    {
        if (!IsNumberLiteral(text))
            throw Unsupported($"Invalid CEL number '{text}'.");

        var utf8 = Encoding.UTF8.GetBytes(text);
        var hasDecimal = decimal.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number);
        var floating = 0d;
        var isFloatingLiteral = (text.Contains('.') ||
                                 text.Contains('e') ||
                                 text.Contains('E')) &&
            double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out floating);
        return new ValidationCelValue(
            ValidationCelValueKind.Number,
            default,
            hasDecimal,
            number,
            null,
            utf8,
            Floating: floating,
            IsFloatingLiteral: isFloatingLiteral);
    }

    private static bool IsNumberLiteral(ReadOnlySpan<char> text)
    {
        var position = 0;
        while (position < text.Length && IsAsciiDigit(text[position]))
            position++;
        if (position == 0)
            return false;

        if (position < text.Length && text[position] == '.')
        {
            position++;
            while (position < text.Length && IsAsciiDigit(text[position]))
                position++;
        }

        if (position < text.Length && text[position] is 'e' or 'E')
        {
            position++;
            if (position < text.Length && text[position] is '+' or '-')
                position++;
            var exponentStart = position;
            while (position < text.Length && IsAsciiDigit(text[position]))
                position++;
            if (position == exponentStart)
                return false;
        }

        return position == text.Length;
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    internal static ValidationCelValue FromString(string value) =>
        new(ValidationCelValueKind.String, default, false, 0, value, Encoding.UTF8.GetBytes(value));

    internal static ValidationCelValue FromUtf8String(ReadOnlyMemory<byte> value, int sizeIndex = -1) =>
        new(
            ValidationCelValueKind.String,
            default,
            false,
            0,
            null,
            value,
            SizeIndex: sizeIndex,
            IsUtf8Literal: true);

    internal static ValidationCelValue FromBytes(ReadOnlyMemory<byte> value, int sizeIndex = -1) =>
        new(
            ValidationCelValueKind.Bytes,
            default,
            false,
            0,
            null,
            value,
            SizeIndex: sizeIndex,
            IsUtf8Literal: true);

    internal static ValidationCelValue FromCollection(
        ValidationCelValueKind kind,
        int sizeIndex,
        ReadOnlyMemory<byte> binaryPayload = default) =>
        new(kind, default, false, 0, null, binaryPayload, SizeIndex: sizeIndex);

    internal static ValidationCelValue FromCollection(
        ValidationCelValueKind kind,
        ReadOnlyMemory<byte> encoded,
        int sizeIndex) =>
        new(kind, default, false, 0, null, encoded, SizeIndex: sizeIndex);

    internal static ValidationCelValue FromJson(ReadOnlyMemory<byte> json, int sizeIndex = -1)
    {
        if (json.IsEmpty)
            return Missing;
        var reader = new Utf8JsonReader(json.Span, ValidationCelJsonReader.Options);
        if (!reader.Read())
            return Missing;
        return reader.TokenType switch
        {
            JsonTokenType.Null => Null,
            JsonTokenType.True => True,
            JsonTokenType.False => False,
            JsonTokenType.Number => FromJsonNumber(json, ref reader),
            JsonTokenType.String => new ValidationCelValue(
                ValidationCelValueKind.String, json, false, 0, null, default, SizeIndex: sizeIndex),
            JsonTokenType.StartObject => new ValidationCelValue(
                ValidationCelValueKind.Object, json, false, 0, null, default, SizeIndex: sizeIndex),
            JsonTokenType.StartArray => new ValidationCelValue(
                ValidationCelValueKind.Array, json, false, 0, null, default, SizeIndex: sizeIndex),
            _ => Missing
        };
    }

    private static ValidationCelValue FromJsonNumber(
        ReadOnlyMemory<byte> json,
        ref Utf8JsonReader reader)
    {
        var hasDecimal = reader.TryGetDecimal(out var number);
        return new ValidationCelValue(
            ValidationCelValueKind.Number,
            json,
            hasDecimal,
            number,
            null,
            default);
    }

    internal string GetString()
    {
        if (Literal is not null)
            return Literal;
        if (IsUtf8Literal)
            return Encoding.UTF8.GetString(Utf8Literal.Span);
        var reader = new Utf8JsonReader(Json.Span, ValidationCelJsonReader.Options);
        _ = reader.Read();
        return reader.GetString() ?? string.Empty;
    }
}

internal readonly ref struct ValidationCelJsonNumber
{
    private readonly ReadOnlySpan<byte> _value;
    private readonly ReadOnlySpan<byte> _exponentDigits;
    private readonly bool _exponentNegative;
    private readonly long _exponentValue;
    private readonly bool _exponentFitsInt64;
    private readonly int _firstDigit;
    private readonly int _lastDigit;

    internal ValidationCelJsonNumber(ReadOnlySpan<byte> value, bool negated = false)
    {
        _value = value;
        var mantissaStart = value[0] == (byte)'-' ? 1 : 0;
        var mantissaEnd = value.Length;
        var decimalPoint = -1;
        var exponentDigits = default(ReadOnlySpan<byte>);
        var exponentNegative = false;
        var firstDigit = -1;
        var lastDigit = -1;
        var significantLength = 0;
        var digitCount = 0;

        for (var index = mantissaStart; index < value.Length; index++)
        {
            if (value[index] == (byte)'.')
            {
                decimalPoint = index;
            }
            else if (value[index] is (byte)'e' or (byte)'E')
            {
                mantissaEnd = index;
                var exponentStart = index + 1;
                exponentNegative = value[exponentStart] == (byte)'-';
                if (value[exponentStart] is (byte)'+' or (byte)'-')
                    exponentStart++;
                while (exponentStart < value.Length && value[exponentStart] == (byte)'0')
                    exponentStart++;
                exponentDigits = value[exponentStart..];
                break;
            }
            else if (value[index] != (byte)'0')
            {
                firstDigit = firstDigit < 0 ? index : firstDigit;
                significantLength++;
                digitCount = significantLength;
                lastDigit = index;
            }
            else if (firstDigit >= 0)
            {
                significantLength++;
            }
        }

        if (firstDigit < 0)
        {
            Sign = 0;
            DigitCount = 0;
            ExponentAdjustment = 0;
            _exponentDigits = default;
            _exponentNegative = false;
            _exponentValue = 0;
            _exponentFitsInt64 = true;
            _firstDigit = 0;
            _lastDigit = -1;
            return;
        }

        var fractionalDigits = decimalPoint < 0 ? 0 : mantissaEnd - decimalPoint - 1;
        var trailingZeros = significantLength - digitCount;

        Sign = (mantissaStart == 0 ? 1 : -1) * (negated ? -1 : 1);
        DigitCount = digitCount;
        ExponentAdjustment = (long)trailingZeros - fractionalDigits;
        _exponentDigits = exponentDigits;
        _exponentNegative = exponentNegative;
        _exponentFitsInt64 = TryParseExponent(
            exponentDigits,
            exponentNegative,
            out _exponentValue);
        _firstDigit = firstDigit;
        _lastDigit = lastDigit;
    }

    private int Sign { get; }
    private int DigitCount { get; }
    private long ExponentAdjustment { get; }

    internal int CompareTo(ValidationCelJsonNumber other)
    {
        if (Sign != other.Sign)
            return Sign.CompareTo(other.Sign);
        if (Sign == 0)
            return 0;

        var comparison = CompareAdjustedExponents(other);
        if (comparison == 0)
        {
            var digits = GetDigits();
            var otherDigits = other.GetDigits();
            var count = Math.Max(DigitCount, other.DigitCount);
            for (var index = 0; index < count; index++)
            {
                var digit = index < DigitCount && digits.MoveNext() ? digits.Current : (byte)'0';
                var otherDigit = index < other.DigitCount && otherDigits.MoveNext()
                    ? otherDigits.Current
                    : (byte)'0';
                comparison = digit.CompareTo(otherDigit);
                if (comparison != 0)
                    break;
            }
        }

        return Sign > 0 ? comparison : -comparison;
    }

    private DigitEnumerator GetDigits() => new(_value, _firstDigit, _lastDigit);

    private int CompareAdjustedExponents(ValidationCelJsonNumber other)
    {
        if (_exponentFitsInt64 && other._exponentFitsInt64)
        {
            return (_exponentValue + ExponentAdjustment + DigitCount)
                .CompareTo(other._exponentValue + other.ExponentAdjustment + other.DigitCount);
        }

        var leftCapacity = Math.Max(_exponentDigits.Length, 20) + 1;
        var rightCapacity = Math.Max(other._exponentDigits.Length, 20) + 1;
        byte[]? rentedLeft = null;
        byte[]? rentedRight = null;
        Span<byte> left = leftCapacity <= 256
            ? stackalloc byte[leftCapacity]
            : (rentedLeft = ArrayPool<byte>.Shared.Rent(leftCapacity));
        Span<byte> right = rightCapacity <= 256
            ? stackalloc byte[rightCapacity]
            : (rentedRight = ArrayPool<byte>.Shared.Rent(rightCapacity));
        try
        {
            var leftLength = WriteAdjustedExponent(
                _exponentDigits,
                _exponentNegative,
                ExponentAdjustment + DigitCount,
                left,
                out var leftSign);
            var rightLength = WriteAdjustedExponent(
                other._exponentDigits,
                other._exponentNegative,
                other.ExponentAdjustment + other.DigitCount,
                right,
                out var rightSign);
            if (leftSign != rightSign)
                return leftSign.CompareTo(rightSign);
            if (leftSign == 0)
                return 0;

            var comparison = leftLength.CompareTo(rightLength);
            if (comparison == 0)
                comparison = left[..leftLength].SequenceCompareTo(right[..rightLength]);
            return leftSign > 0 ? comparison : -comparison;
        }
        finally
        {
            if (rentedRight is not null)
                ArrayPool<byte>.Shared.Return(rentedRight);
            if (rentedLeft is not null)
                ArrayPool<byte>.Shared.Return(rentedLeft);
        }
    }

    private static bool TryParseExponent(
        ReadOnlySpan<byte> magnitude,
        bool negative,
        out long value)
    {
        if (magnitude.Length > 18)
        {
            value = 0;
            return false;
        }

        value = 0;
        for (var index = 0; index < magnitude.Length; index++)
            value = (value * 10) + magnitude[index] - (byte)'0';
        if (negative)
            value = -value;
        return true;
    }

    private static int WriteAdjustedExponent(
        ReadOnlySpan<byte> magnitude,
        bool negative,
        long adjustment,
        Span<byte> destination,
        out int sign)
    {
        if (magnitude.IsEmpty)
            return WriteInt64(adjustment, destination, out sign);
        if (adjustment == 0)
        {
            magnitude.CopyTo(destination);
            sign = negative ? -1 : 1;
            return magnitude.Length;
        }

        var adjustmentNegative = adjustment < 0;
        var adjustmentMagnitude = adjustmentNegative
            ? (ulong)(-(adjustment + 1)) + 1
            : (ulong)adjustment;
        if (negative == adjustmentNegative)
        {
            sign = negative ? -1 : 1;
            return AddMagnitude(magnitude, adjustmentMagnitude, destination);
        }

        Span<byte> adjustmentText = stackalloc byte[20];
        _ = Utf8Formatter.TryFormat(adjustmentMagnitude, adjustmentText, out var adjustmentLength);
        var comparison = magnitude.Length.CompareTo(adjustmentLength);
        if (comparison == 0)
            comparison = magnitude.SequenceCompareTo(adjustmentText[..adjustmentLength]);
        if (comparison == 0)
        {
            sign = 0;
            return 0;
        }
        if (comparison > 0)
        {
            sign = negative ? -1 : 1;
            return SubtractMagnitude(magnitude, adjustmentMagnitude, destination);
        }

        var rawMagnitude = ParseMagnitude(magnitude);
        sign = adjustmentNegative ? -1 : 1;
        _ = Utf8Formatter.TryFormat(adjustmentMagnitude - rawMagnitude, destination, out var written);
        return written;
    }

    private static int WriteInt64(long value, Span<byte> destination, out int sign)
    {
        if (value == 0)
        {
            sign = 0;
            return 0;
        }

        sign = value < 0 ? -1 : 1;
        var magnitude = value < 0 ? (ulong)(-(value + 1)) + 1 : (ulong)value;
        _ = Utf8Formatter.TryFormat(magnitude, destination, out var written);
        return written;
    }

    private static int AddMagnitude(
        ReadOnlySpan<byte> magnitude,
        ulong addend,
        Span<byte> destination)
    {
        var source = magnitude.Length - 1;
        var write = destination.Length;
        while (source >= 0 || addend != 0)
        {
            var sum = source >= 0 ? magnitude[source--] - (byte)'0' : 0;
            sum += (int)(addend % 10);
            addend /= 10;
            if (sum >= 10)
            {
                sum -= 10;
                addend++;
            }
            destination[--write] = (byte)('0' + sum);
        }

        var length = destination.Length - write;
        destination[write..].CopyTo(destination);
        return length;
    }

    private static int SubtractMagnitude(
        ReadOnlySpan<byte> magnitude,
        ulong subtrahend,
        Span<byte> destination)
    {
        magnitude.CopyTo(destination);
        for (var position = magnitude.Length - 1; position >= 0; position--)
        {
            var difference = destination[position] - (byte)'0' - (int)(subtrahend % 10);
            subtrahend /= 10;
            if (difference < 0)
            {
                difference += 10;
                subtrahend++;
            }
            destination[position] = (byte)('0' + difference);
        }

        var start = 0;
        while (destination[start] == (byte)'0')
            start++;
        var length = magnitude.Length - start;
        destination.Slice(start, length).CopyTo(destination);
        return length;
    }

    private static ulong ParseMagnitude(ReadOnlySpan<byte> magnitude)
    {
        var value = 0UL;
        for (var index = 0; index < magnitude.Length; index++)
            value = (value * 10) + magnitude[index] - (byte)'0';
        return value;
    }

    private ref struct DigitEnumerator
    {
        private readonly ReadOnlySpan<byte> _value;
        private readonly int _last;
        private int _position;

        internal DigitEnumerator(ReadOnlySpan<byte> value, int first, int last)
        {
            _value = value;
            _last = last;
            _position = first - 1;
            Current = 0;
        }

        internal byte Current { get; private set; }

        internal bool MoveNext()
        {
            while (++_position <= _last)
            {
                if (_value[_position] == (byte)'.')
                    continue;
                Current = _value[_position];
                return true;
            }

            return false;
        }
    }
}

internal abstract class ValidationCelNode
{
    internal virtual bool HasAggregateValueIndex => false;
    internal virtual bool HasRootAggregateValueIndex => false;

    internal abstract ValidationCelValue Evaluate(ValidationCelContext context);

    internal virtual ValidationCelValue EvaluateAggregate(
        ValidationCelContext context,
        out int valueIndex)
    {
        valueIndex = -1;
        return Evaluate(context);
    }
}

internal sealed class ValidationCelLiteralNode(ValidationCelValue value) : ValidationCelNode
{
    internal ValidationCelValue Value => value;

    internal override ValidationCelValue Evaluate(ValidationCelContext context) => value;
}

internal sealed class ValidationCelThisNode(int memberIndex) : ValidationCelNode
{
    internal int ValueIndex => memberIndex + 1;
    internal override bool HasAggregateValueIndex => true;
    internal override bool HasRootAggregateValueIndex => memberIndex < 0;

    internal bool IsPresent(ValidationCelContext context) =>
        memberIndex < 0 || context.MemberValues.IsPresent(memberIndex);

    internal override ValidationCelValue Evaluate(ValidationCelContext context) =>
        context.UsesTypedValues
            ? memberIndex < 0
                ? context.TypedThis
                : context.MemberValues.GetValue(memberIndex, context.This)
            : ValidationCelValue.FromJson(
                memberIndex < 0 ? context.This : context.MemberValues.Get(memberIndex, context.This),
                memberIndex + 1);

    internal override ValidationCelValue EvaluateAggregate(
        ValidationCelContext context,
        out int valueIndex)
    {
        valueIndex = ValueIndex;
        return Evaluate(context);
    }
}

internal sealed class ValidationCelNowNode : ValidationCelNode
{
    internal override ValidationCelValue Evaluate(ValidationCelContext context) =>
        ValidationCelValue.FromNumber(context.NowUnixMilliseconds);
}

internal sealed class ValidationCelUnaryNode(ValidationCelTokenKind operation, ValidationCelNode operand)
    : ValidationCelNode
{
    internal override ValidationCelValue Evaluate(ValidationCelContext context)
    {
        var value = operand.Evaluate(context);
        return operation switch
        {
            ValidationCelTokenKind.Not => ValidationCelValue.FromBoolean(!RequireBoolean(value)),
            ValidationCelTokenKind.Minus => ValidationCelValue.NegateNumber(value),
            _ => throw Unsupported("Unsupported unary operator.")
        };
    }
}

internal sealed class ValidationCelBinaryNode(
    ValidationCelTokenKind operation,
    ValidationCelNode left,
    ValidationCelNode right) : ValidationCelNode
{
    private const double MaximumExactlyRepresentableDoubleInteger = 9_007_199_254_740_992d;
    private readonly bool _usesAggregateValueIndexes =
        operation is ValidationCelTokenKind.Equal or ValidationCelTokenKind.NotEqual &&
        left.HasAggregateValueIndex &&
        right.HasAggregateValueIndex;

    internal bool UsesCachedEquality => _usesAggregateValueIndexes;

    internal bool UsesRootAggregateEquality =>
        UsesCachedEquality &&
        (left.HasRootAggregateValueIndex || right.HasRootAggregateValueIndex);

    internal override ValidationCelValue Evaluate(ValidationCelContext context)
    {
        var isEquality = operation is ValidationCelTokenKind.Equal or ValidationCelTokenKind.NotEqual;
        var leftValueIndex = -1;
        var leftValue = isEquality
            ? left.EvaluateAggregate(context, out leftValueIndex)
            : left.Evaluate(context);
        if (operation == ValidationCelTokenKind.And && !RequireBoolean(leftValue))
            return ValidationCelValue.False;
        if (operation == ValidationCelTokenKind.Or && RequireBoolean(leftValue))
            return ValidationCelValue.True;

        var rightValueIndex = -1;
        var rightValue = isEquality
            ? right.EvaluateAggregate(context, out rightValueIndex)
            : right.Evaluate(context);
        return operation switch
        {
            ValidationCelTokenKind.And or ValidationCelTokenKind.Or =>
                ValidationCelValue.FromBoolean(RequireBoolean(rightValue)),
            ValidationCelTokenKind.Equal => ValidationCelValue.FromBoolean(
                AreEqual(leftValue, rightValue, context, leftValueIndex, rightValueIndex)),
            ValidationCelTokenKind.NotEqual => ValidationCelValue.FromBoolean(
                !AreEqual(leftValue, rightValue, context, leftValueIndex, rightValueIndex)),
            ValidationCelTokenKind.Less => ValidationCelValue.FromBoolean(Compare(leftValue, rightValue) is < 0),
            ValidationCelTokenKind.LessOrEqual => ValidationCelValue.FromBoolean(Compare(leftValue, rightValue) is <= 0),
            ValidationCelTokenKind.Greater => ValidationCelValue.FromBoolean(Compare(leftValue, rightValue) is > 0),
            ValidationCelTokenKind.GreaterOrEqual => ValidationCelValue.FromBoolean(Compare(leftValue, rightValue) is >= 0),
            ValidationCelTokenKind.Plus => Add(leftValue, rightValue),
            ValidationCelTokenKind.Minus => Subtract(leftValue, rightValue),
            _ => throw Unsupported("Unsupported binary operator.")
        };
    }

    private static bool AreEqual(
        ValidationCelValue left,
        ValidationCelValue right,
        ValidationCelContext context,
        int leftValueIndex,
        int rightValueIndex)
    {
        if (left.Kind == ValidationCelValueKind.Missing || right.Kind == ValidationCelValueKind.Missing)
            throw Unsupported("Cannot compare a missing CEL member; guard optional members with has(...).");
        if (left.Kind != right.Kind)
            return false;
        return left.Kind switch
        {
            ValidationCelValueKind.Null => true,
            ValidationCelValueKind.Boolean => left.Boolean == right.Boolean,
            ValidationCelValueKind.Number => NumbersAreEqual(left, right),
            ValidationCelValueKind.String => ValidationCelStrings.Evaluate(left, right, ValidationCelStringOperation.Equal),
            ValidationCelValueKind.Bytes => left.Utf8Literal.Span.SequenceEqual(right.Utf8Literal.Span),
            ValidationCelValueKind.Object or ValidationCelValueKind.Array =>
                AreAggregateValuesEqual(left, right, context, leftValueIndex, rightValueIndex),
            _ => false
        };
    }

    private static bool AreAggregateValuesEqual(
        ValidationCelValue left,
        ValidationCelValue right,
        ValidationCelContext context,
        int leftValueIndex,
        int rightValueIndex)
    {
        if (leftValueIndex < 0 || rightValueIndex < 0)
            return CompareAggregateValues(left, right);
        if (CompiledValidationRule.TryGetEquality(
                context.EqualityGeneration,
                leftValueIndex,
                rightValueIndex,
                out var value))
            return value;

        value = CompareAggregateValues(
            left,
            right,
            GetAggregateComparer(context, leftValueIndex),
            GetAggregateComparer(context, rightValueIndex));
        CompiledValidationRule.SetEquality(
            context.EqualityGeneration,
            leftValueIndex,
            rightValueIndex,
            value);
        return value;
    }

    internal static bool NumbersAreEqual(ValidationCelValue left, ValidationCelValue right) =>
        CompareNumbers(left, right) is 0;

    private static IValidationCelAggregateComparer? GetAggregateComparer(
        ValidationCelContext context,
        int valueIndex) => valueIndex == 0
            ? context.RootAggregateComparer
            : context.MemberValues.GetAggregateComparer(valueIndex - 1);

    private static bool CompareAggregateValues(ValidationCelValue left, ValidationCelValue right) =>
        CompareAggregateValues(left, right, leftComparer: null, rightComparer: null);

    private static bool CompareAggregateValues(
        ValidationCelValue left,
        ValidationCelValue right,
        IValidationCelAggregateComparer? leftComparer,
        IValidationCelAggregateComparer? rightComparer)
    {
        if (left.Json.IsEmpty || right.Json.IsEmpty)
        {
            if (!left.Json.IsEmpty || !right.Json.IsEmpty)
                return false;
            if (left.Utf8Literal.Span.SequenceEqual(right.Utf8Literal.Span) &&
                (leftComparer is null
                    ? rightComparer is null
                    : rightComparer is not null &&
                      leftComparer.RawEqualityToken is { } leftToken &&
                      ReferenceEquals(leftToken, rightComparer.RawEqualityToken)))
                return true;
            if (leftComparer is not null && rightComparer is not null)
            {
                return leftComparer.AreEqual(
                    left.Utf8Literal,
                    rightComparer,
                    right.Utf8Literal);
            }
            return false;
        }

        return ValidationCelJsonEquality.AreEqual(left.Json.Span, right.Json.Span);
    }

    private static int? Compare(ValidationCelValue left, ValidationCelValue right)
    {
        if (left.Kind == ValidationCelValueKind.Number && right.Kind == ValidationCelValueKind.Number)
            return CompareNumbers(left, right);
        if (left.Kind == ValidationCelValueKind.String && right.Kind == ValidationCelValueKind.String)
            return ValidationCelStrings.Compare(left, right);
        throw Unsupported("Comparison operands must have matching numeric or string types.");
    }

    private static ValidationCelValue Add(ValidationCelValue left, ValidationCelValue right)
    {
        if (!left.IsFloating && !right.IsFloating)
            return ValidationCelValue.FromNumber(RequireNumber(left) + RequireNumber(right));
        var leftFloating = GetFloatingNumber(left);
        var rightFloating = GetFloatingNumber(right);
        if (!RequiresDecimalMixedArithmetic(left, leftFloating) &&
            !RequiresDecimalMixedArithmetic(right, rightFloating))
        {
            return ValidationCelValue.FromFloating(leftFloating + rightFloating);
        }
        return TryGetDecimalArithmeticNumber(left, out var leftNumber) &&
            TryGetDecimalArithmeticNumber(right, out var rightNumber)
            ? ValidationCelValue.FromNumber(leftNumber + rightNumber)
            : ValidationCelValue.FromFloating(leftFloating + rightFloating);
    }

    private static ValidationCelValue Subtract(ValidationCelValue left, ValidationCelValue right)
    {
        if (!left.IsFloating && !right.IsFloating)
            return ValidationCelValue.FromNumber(RequireNumber(left) - RequireNumber(right));
        var leftFloating = GetFloatingNumber(left);
        var rightFloating = GetFloatingNumber(right);
        if (!RequiresDecimalMixedArithmetic(left, leftFloating) &&
            !RequiresDecimalMixedArithmetic(right, rightFloating))
        {
            return ValidationCelValue.FromFloating(leftFloating - rightFloating);
        }
        return TryGetDecimalArithmeticNumber(left, out var leftNumber) &&
            TryGetDecimalArithmeticNumber(right, out var rightNumber)
            ? ValidationCelValue.FromNumber(leftNumber - rightNumber)
            : ValidationCelValue.FromFloating(leftFloating - rightFloating);
    }

    private static bool RequiresDecimalMixedArithmetic(
        ValidationCelValue value,
        double floating) =>
        !value.IsFloating && Math.Abs(floating) >= MaximumExactlyRepresentableDoubleInteger;

    private static bool TryGetDecimalArithmeticNumber(
        ValidationCelValue value,
        out decimal number)
    {
        if (!value.IsFloating)
        {
            number = RequireNumber(value);
            return true;
        }

        var floating = value.Floating;
        number = 0;
        if (double.IsNaN(floating) || double.IsInfinity(floating) ||
            floating <= (double)decimal.MinValue || floating >= (double)decimal.MaxValue)
        {
            return false;
        }
        number = (decimal)floating;
        return true;
    }

    private static double GetFloatingNumber(ValidationCelValue value) =>
        value.IsFloating || value.IsFloatingLiteral
            ? value.Floating
            : (double)RequireNumber(value);

    private static int? CompareNumbers(ValidationCelValue left, ValidationCelValue right)
    {
        if (left.IsFloating && right.IsFloating)
        {
            if (double.IsNaN(left.Floating) || double.IsNaN(right.Floating))
                return null;
            return left.Floating.CompareTo(right.Floating);
        }
        if (left.IsFloating)
            return CompareFloatingToExact(left.Floating, right);
        if (right.IsFloating)
        {
            var comparison = CompareFloatingToExact(right.Floating, left);
            return comparison.HasValue ? -comparison.Value : null;
        }

        // Number values reuse the Boolean slot to mark a successful decimal parse.
        if (left.Boolean && right.Boolean)
        {
            var comparison = left.Number.CompareTo(right.Number);
            if (comparison != 0 || !HasSourceText(left) && !HasSourceText(right))
                return comparison;
        }

        return CompareExactNumbers(left, right);
    }

    private static int? CompareFloatingToExact(double floating, ValidationCelValue exact)
    {
        if (double.IsNaN(floating))
            return null;
        if (double.IsPositiveInfinity(floating))
            return 1;
        if (double.IsNegativeInfinity(floating))
            return -1;

        if (exact.Boolean && TryConvertIntegralFloating(floating, out var integral))
            return integral.CompareTo(exact.Number);

        Span<byte> floatingBuffer = stackalloc byte[1152];
        var written = FormatExactFloating(floating, floatingBuffer);
        Span<byte> exactBuffer = stackalloc byte[64];
        return new ValidationCelJsonNumber(floatingBuffer[..written], negated: false)
            .CompareTo(new ValidationCelJsonNumber(GetNumberText(exact, exactBuffer), exact.NumberNegated));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertIntegralFloating(double value, out decimal result)
    {
        if (value == 0)
        {
            result = 0;
            return true;
        }

        var bits = BitConverter.DoubleToUInt64Bits(value);
        var binaryExponent = (int)((bits >> 52) & 0x7ff) - 1023;
        if ((uint)binaryExponent > 62)
        {
            result = default;
            return false;
        }

        var significand = (bits & 0x000f_ffff_ffff_ffffUL) | 1UL << 52;
        if (binaryExponent < 52 && (significand & ((1UL << (52 - binaryExponent)) - 1)) != 0)
        {
            result = default;
            return false;
        }

        result = (long)value;
        return true;
    }

    private static int FormatExactFloating(double value, Span<byte> destination)
    {
        const uint limbBase = 1_000_000_000;
        var bits = BitConverter.DoubleToUInt64Bits(value);
        var negative = (bits >> 63) != 0;
        var exponentBits = (int)(bits >> 52) & 0x7ff;
        var significand = bits & 0x000f_ffff_ffff_ffffUL;
        int binaryExponent;
        if (exponentBits == 0)
        {
            binaryExponent = -1074;
        }
        else
        {
            significand |= 1UL << 52;
            binaryExponent = exponentBits - 1023 - 52;
        }
        if (significand == 0)
        {
            destination[0] = (byte)'0';
            return 1;
        }

        Span<uint> limbs = stackalloc uint[128];
        var limbCount = 0;
        while (significand != 0)
        {
            limbs[limbCount++] = (uint)(significand % limbBase);
            significand /= limbBase;
        }

        var multiplier = binaryExponent < 0 ? 5u : 2u;
        var multiplyCount = Math.Abs(binaryExponent);
        for (var multiplication = 0; multiplication < multiplyCount; multiplication++)
        {
            ulong carry = 0;
            for (var index = 0; index < limbCount; index++)
            {
                var product = limbs[index] * (ulong)multiplier + carry;
                limbs[index] = (uint)(product % limbBase);
                carry = product / limbBase;
            }
            if (carry != 0)
                limbs[limbCount++] = (uint)carry;
        }

        Span<byte> digits = stackalloc byte[1100];
        if (!Utf8Formatter.TryFormat(limbs[limbCount - 1], digits, out var digitCount))
            throw Unsupported("CEL floating-point number could not be formatted.");
        for (var index = limbCount - 2; index >= 0; index--)
        {
            if (!Utf8Formatter.TryFormat(
                    limbs[index],
                    digits[digitCount..],
                    out var limbDigits,
                    new StandardFormat('D', 9)))
            {
                throw Unsupported("CEL floating-point number could not be formatted.");
            }
            digitCount += limbDigits;
        }

        var written = 0;
        if (negative)
            destination[written++] = (byte)'-';
        var scale = binaryExponent < 0 ? -binaryExponent : 0;
        if (scale == 0)
        {
            digits[..digitCount].CopyTo(destination[written..]);
            return written + digitCount;
        }
        if (digitCount <= scale)
        {
            destination[written++] = (byte)'0';
            destination[written++] = (byte)'.';
            destination.Slice(written, scale - digitCount).Fill((byte)'0');
            written += scale - digitCount;
            digits[..digitCount].CopyTo(destination[written..]);
            return written + digitCount;
        }

        var integerDigits = digitCount - scale;
        digits[..integerDigits].CopyTo(destination[written..]);
        written += integerDigits;
        destination[written++] = (byte)'.';
        digits.Slice(integerDigits, scale).CopyTo(destination[written..]);
        return written + scale;
    }

    private static int CompareExactNumbers(ValidationCelValue left, ValidationCelValue right)
    {
        Span<byte> leftBuffer = stackalloc byte[64];
        Span<byte> rightBuffer = stackalloc byte[64];
        var leftText = GetNumberText(left, leftBuffer);
        var rightText = GetNumberText(right, rightBuffer);
        return new ValidationCelJsonNumber(leftText, left.NumberNegated)
            .CompareTo(new ValidationCelJsonNumber(rightText, right.NumberNegated));
    }

    private static bool HasSourceText(ValidationCelValue value) =>
        !value.Json.IsEmpty || !value.Utf8Literal.IsEmpty;

    private static ReadOnlySpan<byte> GetNumberText(
        ValidationCelValue value,
        Span<byte> buffer)
    {
        if (!value.Json.IsEmpty)
            return value.Json.Span;
        if (!value.Utf8Literal.IsEmpty)
            return value.Utf8Literal.Span;
        if (Utf8Formatter.TryFormat(value.Number, buffer, out var written))
            return buffer[..written];
        throw Unsupported("CEL number could not be formatted.");
    }
}

internal static class ValidationCelJsonEquality
{
    private const int StackNodeCount = 32;
    private const int StackBucketCount = 128;

    internal static bool AreEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var nodeCount = CountValues(left);
        if (nodeCount == 0)
            return false;

        EqualityNode[]? rentedNodes = null;
        Span<EqualityNode> nodes = nodeCount <= StackNodeCount
            ? stackalloc EqualityNode[nodeCount]
            : (rentedNodes = ArrayPool<EqualityNode>.Shared.Rent(nodeCount));
        nodes = nodes[..nodeCount];

        int[]? rentedBuckets = null;
        try
        {
            if (BuildIndex(left, nodes) != nodeCount)
                return false;

            var bucketCount = ConfigureObjectBuckets(nodes);
            Span<int> buckets = bucketCount <= StackBucketCount
                ? stackalloc int[bucketCount]
                : (rentedBuckets = ArrayPool<int>.Shared.Rent(bucketCount));
            buckets = buckets[..bucketCount];
            buckets.Fill(-1);
            FillObjectBuckets(left, nodes, buckets);

            var rightReader = new Utf8JsonReader(right, ValidationCelJsonReader.Options);
            var equalityEpoch = 0;
            return rightReader.Read() &&
                   NodesEqual(0, left, nodes, buckets, ref rightReader, ref equalityEpoch) &&
                   !rightReader.Read();
        }
        finally
        {
            if (rentedBuckets is not null)
                ArrayPool<int>.Shared.Return(rentedBuckets);
            if (rentedNodes is not null)
                ArrayPool<EqualityNode>.Shared.Return(rentedNodes);
        }
    }

    private static int CountValues(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json, ValidationCelJsonReader.Options);
        var count = 0;
        while (reader.Read())
        {
            if (IsValueToken(reader.TokenType))
                count++;
        }
        return count;
    }

    private static int BuildIndex(ReadOnlySpan<byte> json, Span<EqualityNode> nodes)
    {
        var reader = new Utf8JsonReader(json, ValidationCelJsonReader.Options);
        var nodeCount = 0;
        var parentIndex = -1;
        var nameStart = 0;
        var nameLength = 0;
        var nameHash = 0u;
        var nameEscaped = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                nameStart = checked((int)reader.TokenStartIndex + 1);
                nameLength = reader.ValueSpan.Length;
                nameHash = HashName(ref reader);
                nameEscaped = reader.ValueIsEscaped;
                continue;
            }

            if (reader.TokenType is JsonTokenType.EndArray or JsonTokenType.EndObject)
            {
                parentIndex = nodes[parentIndex].Parent;
                continue;
            }

            if (!IsValueToken(reader.TokenType))
                continue;

            var nodeIndex = nodeCount++;
            nodes[nodeIndex] = new EqualityNode
            {
                TokenType = reader.TokenType,
                ValueStart = checked((int)reader.TokenStartIndex),
                ValueLength = checked((int)(reader.BytesConsumed - reader.TokenStartIndex)),
                Parent = parentIndex,
                FirstChild = -1,
                LastChild = -1,
                NextSibling = -1,
                NameStart = nameStart,
                NameLength = nameLength,
                NameHash = nameHash,
                NameEscaped = nameEscaped
            };

            if (parentIndex >= 0)
            {
                ref var parent = ref nodes[parentIndex];
                if (parent.FirstChild < 0)
                    parent.FirstChild = nodeIndex;
                else
                    nodes[parent.LastChild].NextSibling = nodeIndex;
                parent.LastChild = nodeIndex;
                parent.ChildCount++;
            }

            nameStart = 0;
            nameLength = 0;
            nameHash = 0;
            nameEscaped = false;
            if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
                parentIndex = nodeIndex;
        }

        return nodeCount;
    }

    private static int ConfigureObjectBuckets(Span<EqualityNode> nodes)
    {
        var total = 0;
        for (var index = 0; index < nodes.Length; index++)
        {
            ref var node = ref nodes[index];
            if (node.TokenType != JsonTokenType.StartObject || node.ChildCount == 0)
                continue;

            var capacity = 4;
            var minimum = checked(node.ChildCount * 2);
            while (capacity < minimum)
                capacity = checked(capacity << 1);
            node.BucketStart = total;
            node.BucketCount = capacity;
            total = checked(total + capacity);
        }
        return total;
    }

    private static void FillObjectBuckets(
        ReadOnlySpan<byte> source,
        Span<EqualityNode> nodes,
        Span<int> buckets)
    {
        for (var index = 0; index < nodes.Length; index++)
        {
            ref var parent = ref nodes[index];
            if (parent.BucketCount == 0)
                continue;

            parent.ChildCount = 0;
            for (var childIndex = parent.FirstChild;
                 childIndex >= 0;
                 childIndex = nodes[childIndex].NextSibling)
            {
                var bucket = parent.BucketStart +
                    (int)(nodes[childIndex].NameHash & (uint)(parent.BucketCount - 1));
                while (true)
                {
                    var existingIndex = buckets[bucket];
                    if (existingIndex < 0)
                    {
                        buckets[bucket] = childIndex;
                        parent.ChildCount++;
                        break;
                    }
                    if (nodes[existingIndex].NameHash == nodes[childIndex].NameHash &&
                        IndexedNamesEqual(source, nodes[existingIndex], nodes[childIndex]))
                    {
                        buckets[bucket] = childIndex;
                        break;
                    }
                    bucket = parent.BucketStart +
                        ((bucket - parent.BucketStart + 1) & (parent.BucketCount - 1));
                }
            }
        }
    }

    private static bool NodesEqual(
        int nodeIndex,
        ReadOnlySpan<byte> source,
        scoped Span<EqualityNode> nodes,
        scoped ReadOnlySpan<int> buckets,
        ref Utf8JsonReader rightReader,
        ref int equalityEpoch)
    {
        ref var node = ref nodes[nodeIndex];
        if (node.TokenType != rightReader.TokenType)
            return false;

        return node.TokenType switch
        {
            JsonTokenType.Null => true,
            JsonTokenType.True or JsonTokenType.False => true,
            JsonTokenType.Number => NumberEquals(node, source, ref rightReader),
            JsonTokenType.String => StringEquals(node, source, ref rightReader),
            JsonTokenType.StartArray => ArrayEquals(
                node, source, nodes, buckets, ref rightReader, ref equalityEpoch),
            JsonTokenType.StartObject => ObjectEquals(
                node, source, nodes, buckets, ref rightReader, ref equalityEpoch),
            _ => false
        };
    }

    private static bool NumberEquals(
        EqualityNode node,
        ReadOnlySpan<byte> source,
        ref Utf8JsonReader rightReader)
    {
        var leftReader = ReadNode(node, source);
        return new ValidationCelJsonNumber(leftReader.ValueSpan)
            .CompareTo(new ValidationCelJsonNumber(rightReader.ValueSpan)) == 0;
    }

    private static bool StringEquals(
        EqualityNode node,
        ReadOnlySpan<byte> source,
        ref Utf8JsonReader rightReader)
    {
        var leftReader = ReadNode(node, source);
        return StringsEqual(ref leftReader, ref rightReader);
    }

    private static Utf8JsonReader ReadNode(EqualityNode node, ReadOnlySpan<byte> source)
    {
        var reader = new Utf8JsonReader(
            source.Slice(node.ValueStart, node.ValueLength),
            ValidationCelJsonReader.Options);
        _ = reader.Read();
        return reader;
    }

    private static bool StringsEqual(ref Utf8JsonReader left, ref Utf8JsonReader right)
    {
        if (!left.ValueIsEscaped)
            return right.ValueTextEquals(left.ValueSpan);

        var maximumLength = left.ValueSpan.Length;
        byte[]? rented = null;
        Span<byte> decoded = maximumLength <= 256
            ? stackalloc byte[maximumLength]
            : (rented = ArrayPool<byte>.Shared.Rent(maximumLength));
        try
        {
            var written = left.CopyString(decoded);
            return right.ValueTextEquals(decoded[..written]);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static bool ArrayEquals(
        EqualityNode node,
        ReadOnlySpan<byte> source,
        scoped Span<EqualityNode> nodes,
        scoped ReadOnlySpan<int> buckets,
        ref Utf8JsonReader rightReader,
        ref int equalityEpoch)
    {
        var childIndex = node.FirstChild;
        while (rightReader.Read())
        {
            if (rightReader.TokenType == JsonTokenType.EndArray)
                return childIndex < 0;
            if (childIndex < 0 ||
                !NodesEqual(childIndex, source, nodes, buckets, ref rightReader, ref equalityEpoch))
                return false;
            childIndex = nodes[childIndex].NextSibling;
        }
        return false;
    }

    private static bool ObjectEquals(
        EqualityNode node,
        ReadOnlySpan<byte> source,
        scoped Span<EqualityNode> nodes,
        scoped ReadOnlySpan<int> buckets,
        ref Utf8JsonReader rightReader,
        ref int equalityEpoch)
    {
        var matched = 0;
        var objectEpoch = ++equalityEpoch;
        while (rightReader.Read())
        {
            if (rightReader.TokenType == JsonTokenType.EndObject)
                return matched == node.ChildCount;
            if (rightReader.TokenType != JsonTokenType.PropertyName || node.BucketCount == 0)
                return false;

            var hash = HashName(ref rightReader);
            var bucket = node.BucketStart + (int)(hash & (uint)(node.BucketCount - 1));
            while (true)
            {
                var childIndex = buckets[bucket];
                if (childIndex < 0)
                    return false;
                ref var child = ref nodes[childIndex];
                if (child.NameHash == hash && NameEquals(source, child, ref rightReader))
                {
                    if (!rightReader.Read())
                        return false;

                    var valueReader = rightReader;
                    var equal = NodesEqual(
                        childIndex,
                        source,
                        nodes,
                        buckets,
                        ref valueReader,
                        ref equalityEpoch);
                    if (equal)
                        rightReader = valueReader;
                    else if (rightReader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
                        rightReader.Skip();

                    if (child.SeenEpoch != objectEpoch)
                    {
                        child.SeenEpoch = objectEpoch;
                        if (equal)
                            matched++;
                    }
                    else if (child.Matched != equal)
                    {
                        matched += equal ? 1 : -1;
                    }
                    child.Matched = equal;
                    break;
                }
                bucket = node.BucketStart +
                    ((bucket - node.BucketStart + 1) & (node.BucketCount - 1));
            }
        }
        return false;
    }

    private static bool NameEquals(
        ReadOnlySpan<byte> source,
        EqualityNode node,
        ref Utf8JsonReader rightReader)
    {
        if (!node.NameEscaped)
            return rightReader.ValueTextEquals(source.Slice(node.NameStart, node.NameLength));
        var leftReader = ReadName(node, source);
        return StringsEqual(ref leftReader, ref rightReader);
    }

    private static bool IndexedNamesEqual(
        ReadOnlySpan<byte> source,
        EqualityNode left,
        EqualityNode right)
    {
        if (!left.NameEscaped && !right.NameEscaped)
        {
            return source.Slice(left.NameStart, left.NameLength)
                .SequenceEqual(source.Slice(right.NameStart, right.NameLength));
        }

        var leftReader = ReadName(left, source);
        var rightReader = ReadName(right, source);
        return StringsEqual(ref leftReader, ref rightReader);
    }

    private static Utf8JsonReader ReadName(EqualityNode node, ReadOnlySpan<byte> source)
    {
        var reader = new Utf8JsonReader(
            source.Slice(node.NameStart - 1, node.NameLength + 2),
            ValidationCelJsonReader.Options);
        _ = reader.Read();
        return reader;
    }

    private static uint HashName(ref Utf8JsonReader reader)
    {
        if (!reader.ValueIsEscaped)
            return Hash(reader.ValueSpan);
        var maximumLength = reader.ValueSpan.Length;
        byte[]? rented = null;
        Span<byte> decoded = maximumLength <= 256
            ? stackalloc byte[maximumLength]
            : (rented = ArrayPool<byte>.Shared.Rent(maximumLength));
        try
        {
            return Hash(decoded[..reader.CopyString(decoded)]);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static uint Hash(ReadOnlySpan<byte> value)
    {
        var hash = 2166136261u;
        for (var index = 0; index < value.Length; index++)
            hash = (hash ^ value[index]) * 16777619u;
        return hash;
    }

    private static bool IsValueToken(JsonTokenType tokenType) => tokenType is
        JsonTokenType.Null or
        JsonTokenType.True or
        JsonTokenType.False or
        JsonTokenType.Number or
        JsonTokenType.String or
        JsonTokenType.StartArray or
        JsonTokenType.StartObject;

    private struct EqualityNode
    {
        internal JsonTokenType TokenType;
        internal int ValueStart;
        internal int ValueLength;
        internal int Parent;
        internal int FirstChild;
        internal int LastChild;
        internal int NextSibling;
        internal int ChildCount;
        internal int BucketStart;
        internal int BucketCount;
        internal uint NameHash;
        internal int NameStart;
        internal int NameLength;
        internal bool NameEscaped;
        internal int SeenEpoch;
        internal bool Matched;
    }
}

internal sealed class ValidationCelConditionalNode(
    ValidationCelNode condition,
    ValidationCelNode whenTrue,
    ValidationCelNode whenFalse) : ValidationCelNode
{
    internal override bool HasAggregateValueIndex =>
        whenTrue.HasAggregateValueIndex || whenFalse.HasAggregateValueIndex;

    internal override bool HasRootAggregateValueIndex =>
        whenTrue.HasRootAggregateValueIndex || whenFalse.HasRootAggregateValueIndex;

    internal override ValidationCelValue Evaluate(ValidationCelContext context) =>
        RequireBoolean(condition.Evaluate(context))
            ? whenTrue.Evaluate(context)
            : whenFalse.Evaluate(context);

    internal override ValidationCelValue EvaluateAggregate(
        ValidationCelContext context,
        out int valueIndex) => RequireBoolean(condition.Evaluate(context))
        ? whenTrue.EvaluateAggregate(context, out valueIndex)
        : whenFalse.EvaluateAggregate(context, out valueIndex);
}

internal sealed class ValidationCelFunctionNode(
    string name,
    ValidationCelNode[] arguments,
    ValidationCelNode? receiver = null) : ValidationCelNode
{
    internal override ValidationCelValue Evaluate(ValidationCelContext context)
    {
        if (name == "has")
        {
            RequireArgumentCount(1);
            return ValidationCelValue.FromBoolean(
                context.UsesTypedValues && arguments[0] is ValidationCelThisNode member
                    ? member.IsPresent(context)
                    : arguments[0].Evaluate(context).Kind != ValidationCelValueKind.Missing);
        }

        if (name == "size")
        {
            RequireArgumentCount(1);
            return ValidationCelValue.FromNumber(GetSize(arguments[0].Evaluate(context), context.Sizes));
        }

        if (name is "startsWith" or "endsWith" or "contains")
        {
            var value = receiver is null ? GetArgument(0, 2, context) : receiver.Evaluate(context);
            var candidate = receiver is null ? GetArgument(1, 2, context) : GetArgument(0, 1, context);
            if (value.Kind != ValidationCelValueKind.String || candidate.Kind != ValidationCelValueKind.String)
                throw Unsupported($"CEL function '{name}' requires string operands.");
            return ValidationCelValue.FromBoolean(ValidationCelStrings.Evaluate(value, candidate, name switch
            {
                "startsWith" => ValidationCelStringOperation.StartsWith,
                "endsWith" => ValidationCelStringOperation.EndsWith,
                _ => ValidationCelStringOperation.Contains
            }));
        }

        throw Unsupported($"Unsupported CEL function '{name}'.");
    }

    private ValidationCelValue GetArgument(int index, int expectedCount, ValidationCelContext context)
    {
        RequireArgumentCount(expectedCount);
        return arguments[index].Evaluate(context);
    }

    private void RequireArgumentCount(int expected)
    {
        if (arguments.Length != expected)
            throw Unsupported($"CEL function '{name}' expects {expected.ToString(CultureInfo.InvariantCulture)} argument(s).");
    }

    private static int GetSize(ValidationCelValue value, ValidationCelSizeValues sizes)
    {
        if (value.SizeIndex >= 0 && sizes.TryGet(value.SizeIndex, out var cached))
            return cached;

        var size = GetSizeCore(value);
        if (value.SizeIndex >= 0)
            sizes.Set(value.SizeIndex, size);
        return size;
    }

    private static int GetSizeCore(ValidationCelValue value)
    {
        if (value.Kind == ValidationCelValueKind.String)
            return ValidationCelStrings.GetLength(value);
        if (value.Kind == ValidationCelValueKind.Bytes)
            return value.Utf8Literal.Length;
        if (value.Kind is not (ValidationCelValueKind.Array or ValidationCelValueKind.Object))
            throw Unsupported("CEL function 'size' requires a string, list, or map.");

        var reader = new Utf8JsonReader(value.Json.Span, ValidationCelJsonReader.Options);
        _ = reader.Read();
        var count = 0;
        if (value.Kind == ValidationCelValueKind.Array)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                count++;
                reader.Skip();
            }
            return count;
        }

        var properties = new JsonObjectPropertyIndex();
        try
        {
            properties.Build(ref reader, value.Json.Span);
            return properties.Count;
        }
        finally
        {
            properties.Dispose();
        }
    }
}

internal enum ValidationCelStringOperation : byte
{
    Equal,
    StartsWith,
    EndsWith,
    Contains
}

internal struct ValidationCelStringSlot
{
    internal int Start;
    internal int Length;
    internal uint Generation;
}

internal static class ValidationCelStrings
{
    private const int StackBufferLength = 256;

    [ThreadStatic]
    private static ValidationCelStringSlot[]? t_values;

    [ThreadStatic]
    private static byte[]? t_buffer;

    [ThreadStatic]
    private static int t_valueCount;

    [ThreadStatic]
    private static int t_maximumLength;

    [ThreadStatic]
    private static int t_written;

    [ThreadStatic]
    private static uint t_generation;

    // Short strings keep the stack fast path. Long strings share one payload-sized pooled
    // buffer across sibling rules, so each referenced member is decoded at most once.
    internal static void Begin(int valueCount, int maximumLength)
    {
        t_valueCount = valueCount;
        t_maximumLength = maximumLength;
        t_written = 0;
        if (unchecked(++t_generation) != 0)
            return;

        if (t_values is not null)
            Array.Clear(t_values);
        t_generation = 1;
    }

    internal static void End()
    {
        var buffer = t_buffer;
        t_buffer = null;
        t_valueCount = 0;
        t_maximumLength = 0;
        t_written = 0;
        if (buffer is not null)
            ArrayPool<byte>.Shared.Return(buffer);
    }

    internal static bool Evaluate(
        ValidationCelValue left,
        ValidationCelValue right,
        ValidationCelStringOperation operation)
    {
        var leftMaximum = GetMaximumLength(left);
        var rightMaximum = GetMaximumLength(right);
        if (leftMaximum <= StackBufferLength && rightMaximum <= StackBufferLength)
        {
            Span<byte> leftBuffer = stackalloc byte[leftMaximum];
            Span<byte> rightBuffer = stackalloc byte[rightMaximum];
            return EvaluateCore(
                DecodeUncached(left, leftBuffer),
                DecodeUncached(right, rightBuffer),
                operation);
        }

        return EvaluateCore(Decode(left), Decode(right), operation);
    }

    private static bool EvaluateCore(
        ReadOnlySpan<byte> leftText,
        ReadOnlySpan<byte> rightText,
        ValidationCelStringOperation operation) =>
        operation switch
        {
            ValidationCelStringOperation.Equal => leftText.SequenceEqual(rightText),
            ValidationCelStringOperation.StartsWith => leftText.StartsWith(rightText),
            ValidationCelStringOperation.EndsWith => leftText.EndsWith(rightText),
            ValidationCelStringOperation.Contains => leftText.IndexOf(rightText) >= 0,
            _ => false
        };

    internal static int Compare(ValidationCelValue left, ValidationCelValue right)
    {
        var leftMaximum = GetMaximumLength(left);
        var rightMaximum = GetMaximumLength(right);
        if (leftMaximum <= StackBufferLength && rightMaximum <= StackBufferLength)
        {
            Span<byte> leftBuffer = stackalloc byte[leftMaximum];
            Span<byte> rightBuffer = stackalloc byte[rightMaximum];
            return DecodeUncached(left, leftBuffer)
                .SequenceCompareTo(DecodeUncached(right, rightBuffer));
        }

        var leftText = Decode(left);
        var rightText = Decode(right);
        return leftText.SequenceCompareTo(rightText);
    }

    internal static int GetLength(ValidationCelValue value)
    {
        var maximum = GetMaximumLength(value);
        Span<byte> buffer = maximum <= StackBufferLength
            ? stackalloc byte[maximum]
            : default;
        var text = maximum <= StackBufferLength
            ? DecodeUncached(value, buffer)
            : Decode(value);
        var count = 0;
        while (!text.IsEmpty)
        {
            var status = Rune.DecodeFromUtf8(text, out _, out var consumed);
            if (status != OperationStatus.Done)
                throw Unsupported("CEL string contains invalid UTF-8.");
            text = text[consumed..];
            count++;
        }
        return count;
    }

    private static int GetMaximumLength(ValidationCelValue value) =>
        value.Literal is null && !value.IsUtf8Literal ? value.Json.Length : value.Utf8Literal.Length;

    private static ReadOnlySpan<byte> DecodeUncached(
        ValidationCelValue value,
        Span<byte> destination)
    {
        if (value.Literal is not null || value.IsUtf8Literal)
        {
            value.Utf8Literal.Span.CopyTo(destination);
            return destination[..value.Utf8Literal.Length];
        }

        var reader = new Utf8JsonReader(value.Json.Span, ValidationCelJsonReader.Options);
        _ = reader.Read();
        var written = reader.CopyString(destination);
        return destination[..written];
    }

    private static ReadOnlySpan<byte> Decode(ValidationCelValue value)
    {
        if (value.Literal is not null || value.IsUtf8Literal)
            return value.Utf8Literal.Span;

        var valueIndex = value.SizeIndex;
        if ((uint)valueIndex >= (uint)t_valueCount)
            throw Unsupported("CEL string cache index is invalid.");

        var values = t_values;
        if (values is null || values.Length < t_valueCount)
            t_values = values = new ValidationCelStringSlot[Math.Max(t_valueCount, 8)];

        ref var slot = ref values[valueIndex];
        var buffer = t_buffer;
        if (slot.Generation == t_generation)
            return buffer!.AsSpan(slot.Start, slot.Length);

        if (buffer is null)
            t_buffer = buffer = ArrayPool<byte>.Shared.Rent(Math.Max(t_maximumLength, 1));

        var reader = new Utf8JsonReader(value.Json.Span, ValidationCelJsonReader.Options);
        _ = reader.Read();
        var start = t_written;
        var written = reader.CopyString(buffer.AsSpan(start));
        slot.Start = start;
        slot.Length = written;
        slot.Generation = t_generation;
        t_written += written;
        return buffer.AsSpan(start, written);
    }
}

internal enum ValidationCelTokenKind : byte
{
    End,
    Identifier,
    String,
    Bytes,
    Number,
    True,
    False,
    Null,
    LeftParen,
    RightParen,
    Comma,
    Question,
    Colon,
    Not,
    Minus,
    Plus,
    And,
    Or,
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual
}

internal readonly record struct ValidationCelToken(
    ValidationCelTokenKind Kind,
    string Text,
    ReadOnlyMemory<byte> Bytes = default);

internal sealed class ValidationCelParser
{
    private readonly string _expression;
    private readonly Dictionary<string, int> _memberIndexes;
    private readonly List<byte[][]> _memberPaths;
    private readonly HashSet<int> _usedMemberIndexes;
    private readonly HashSet<int>? _sizedMemberIndexes;
    private readonly List<int>? _sizedMemberAdditions;
    private int _sizeArgumentDepth;
    private int _position;
    private ValidationCelToken _current;

    internal ValidationCelParser(
        string expression,
        Dictionary<string, int> memberIndexes,
        List<byte[][]> memberPaths,
        HashSet<int> usedMemberIndexes,
        int equalityIndexOffset,
        Dictionary<ValidationCelEqualityOperands, int>? equalityIndexes,
        HashSet<int>? sizedMemberIndexes)
    {
        _expression = expression;
        _memberIndexes = memberIndexes;
        _memberPaths = memberPaths;
        _usedMemberIndexes = usedMemberIndexes;
        _equalityIndexOffset = equalityIndexOffset;
        _equalityIndexes = equalityIndexes;
        _sizedMemberIndexes = sizedMemberIndexes;
        _sizedMemberAdditions = sizedMemberIndexes is null ? null : [];
        _current = ReadNextToken();
    }

    internal ValidationCelNode Parse()
    {
        var result = ParseConditional();
        Expect(ValidationCelTokenKind.End);
        return result;
    }

    internal bool UsesSize { get; private set; }
    internal ValidationCelEqualityPair[] EqualityPairs => [.. _equalityPairs];

    private readonly List<ValidationCelEqualityPair> _equalityPairs = [];
    private readonly int _equalityIndexOffset;
    private readonly Dictionary<ValidationCelEqualityOperands, int>? _equalityIndexes;
    internal bool UsesRootValue { get; private set; }
    internal bool UsesRootSize { get; private set; }
    internal bool UsesCachedEquality { get; private set; }
    internal bool UsesRootAggregateEquality { get; private set; }

    private ValidationCelNode ParseConditional()
    {
        var sizedMemberCount = _sizeArgumentDepth == 0
            ? -1
            : _sizedMemberAdditions?.Count ?? 0;
        var usedRootSize = UsesRootSize;
        var condition = ParseOr();
        if (!TryTake(ValidationCelTokenKind.Question))
            return condition;
        if (sizedMemberCount >= 0)
        {
            RollbackSizedMembers(sizedMemberCount);
            UsesRootSize = usedRootSize;
        }
        var whenTrue = ParseConditional();
        Expect(ValidationCelTokenKind.Colon);
        return new ValidationCelConditionalNode(condition, whenTrue, ParseConditional());
    }

    private ValidationCelNode ParseOr()
    {
        var left = ParseAnd();
        while (TryTake(ValidationCelTokenKind.Or))
            left = new ValidationCelBinaryNode(ValidationCelTokenKind.Or, left, ParseAnd());
        return left;
    }

    private ValidationCelNode ParseAnd()
    {
        var left = ParseEquality();
        while (TryTake(ValidationCelTokenKind.And))
            left = new ValidationCelBinaryNode(ValidationCelTokenKind.And, left, ParseEquality());
        return left;
    }

    private ValidationCelNode ParseEquality()
    {
        var left = ParseComparison();
        while (_current.Kind is ValidationCelTokenKind.Equal or ValidationCelTokenKind.NotEqual)
        {
            var operation = Take().Kind;
            var right = ParseComparison();
            var equalityIndex = -1;
            if (left is ValidationCelThisNode leftValue && right is ValidationCelThisNode rightValue)
            {
                equalityIndex = GetEqualityIndex(leftValue.ValueIndex, rightValue.ValueIndex);
                _equalityPairs.Add(new ValidationCelEqualityPair(
                    equalityIndex,
                    leftValue.ValueIndex,
                    rightValue.ValueIndex));
            }
            var equality = new ValidationCelBinaryNode(operation, left, right);
            UsesCachedEquality |= equality.UsesCachedEquality;
            UsesRootAggregateEquality |= equality.UsesRootAggregateEquality;
            left = equality;
        }
        return left;
    }

    private int GetEqualityIndex(int leftValueIndex, int rightValueIndex)
    {
        if (_equalityIndexes is null)
            return _equalityIndexOffset + _equalityPairs.Count;

        var operands = leftValueIndex <= rightValueIndex
            ? new ValidationCelEqualityOperands(leftValueIndex, rightValueIndex)
            : new ValidationCelEqualityOperands(rightValueIndex, leftValueIndex);
        if (_equalityIndexes.TryGetValue(operands, out var equalityIndex))
            return equalityIndex;

        equalityIndex = _equalityIndexes.Count;
        _equalityIndexes.Add(operands, equalityIndex);
        return equalityIndex;
    }

    private ValidationCelNode ParseComparison()
    {
        var left = ParseAdditive();
        while (_current.Kind is ValidationCelTokenKind.Less or ValidationCelTokenKind.LessOrEqual or
               ValidationCelTokenKind.Greater or ValidationCelTokenKind.GreaterOrEqual)
        {
            var operation = Take().Kind;
            left = new ValidationCelBinaryNode(operation, left, ParseAdditive());
        }
        return left;
    }

    private ValidationCelNode ParseAdditive()
    {
        var left = ParseUnary();
        while (_current.Kind is ValidationCelTokenKind.Plus or ValidationCelTokenKind.Minus)
        {
            var operation = Take().Kind;
            left = new ValidationCelBinaryNode(operation, left, ParseUnary());
        }
        return left;
    }

    private ValidationCelNode ParseUnary()
    {
        if (_current.Kind is not (ValidationCelTokenKind.Not or ValidationCelTokenKind.Minus))
            return ParsePrimary();
        return new ValidationCelUnaryNode(Take().Kind, ParseUnary());
    }

    private ValidationCelNode ParsePrimary()
    {
        var token = Take();
        return token.Kind switch
        {
            ValidationCelTokenKind.True => new ValidationCelLiteralNode(ValidationCelValue.True),
            ValidationCelTokenKind.False => new ValidationCelLiteralNode(ValidationCelValue.False),
            ValidationCelTokenKind.Null => new ValidationCelLiteralNode(ValidationCelValue.Null),
            ValidationCelTokenKind.String => new ValidationCelLiteralNode(ValidationCelValue.FromString(token.Text)),
            ValidationCelTokenKind.Bytes => new ValidationCelLiteralNode(
                ValidationCelValue.FromBytes(token.Bytes)),
            ValidationCelTokenKind.Number => new ValidationCelLiteralNode(
                ValidationCelValue.FromNumberLiteral(token.Text)),
            ValidationCelTokenKind.Identifier => ParseIdentifier(token.Text),
            ValidationCelTokenKind.LeftParen => ParseParenthesized(),
            _ => throw Unsupported($"Unexpected token '{token.Text}'.")
        };
    }

    private ValidationCelNode ParseIdentifier(string identifier)
    {
        if (_current.Kind == ValidationCelTokenKind.LeftParen &&
            identifier.StartsWith("this.", StringComparison.Ordinal) &&
            identifier.LastIndexOf('.') is > 3 and var methodSeparator)
        {
            var method = identifier[(methodSeparator + 1)..];
            if (method is "startsWith" or "endsWith" or "contains")
            {
                var receiver = CreateThisNode(identifier[..methodSeparator]);
                _ = TryTake(ValidationCelTokenKind.LeftParen);
                return new ValidationCelFunctionNode(method, ParseArguments(), receiver);
            }
        }

        ValidationCelNode node;
        if (identifier == "this" || identifier.StartsWith("this.", StringComparison.Ordinal))
        {
            node = CreateThisNode(identifier);
        }
        else if (identifier == "now")
        {
            node = new ValidationCelNowNode();
        }
        else if (TryTake(ValidationCelTokenKind.LeftParen))
        {
            var isSize = identifier == "size";
            if (isSize)
                _sizeArgumentDepth++;
            ValidationCelNode[] arguments;
            try
            {
                arguments = ParseArguments();
            }
            finally
            {
                if (isSize)
                    _sizeArgumentDepth--;
            }
            UsesSize |= isSize;
            return identifier == "timestamp"
                ? ParseTimestamp(arguments)
                : new ValidationCelFunctionNode(identifier, arguments);
        }
        else
        {
            throw Unsupported($"Unsupported CEL identifier '{identifier}'.");
        }

        if (!TryTake(ValidationCelTokenKind.LeftParen))
            return node;

        var separator = identifier.LastIndexOf('.');
        if (separator <= 0)
            throw Unsupported($"Unsupported CEL call '{identifier}'.");
        return new ValidationCelFunctionNode(identifier[(separator + 1)..], ParseArguments(), node);
    }

    private static ValidationCelNode ParseTimestamp(ValidationCelNode[] arguments)
    {
        if (arguments is not [ValidationCelLiteralNode { Value.Kind: ValidationCelValueKind.String } literal] ||
            !TryParseTimestampMilliseconds(literal.Value.Literal, out var timestampMilliseconds))
            throw Unsupported("CEL function 'timestamp' requires one ISO-8601 string literal.");

        return new ValidationCelLiteralNode(
            ValidationCelValue.FromNumber(timestampMilliseconds));
    }

    private static bool TryParseTimestampMilliseconds(string? value, out decimal milliseconds)
    {
        milliseconds = 0;
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp))
        {
            return false;
        }

        var utcTicks = timestamp.UtcDateTime.Ticks;
        var fractionStart = value.IndexOf('.');
        if (fractionStart < 0)
        {
            milliseconds = (utcTicks - DateTime.UnixEpoch.Ticks) / (decimal)TimeSpan.TicksPerMillisecond;
            return true;
        }

        var nanoseconds = 0;
        var digitCount = 0;
        for (var index = fractionStart + 1; index < value.Length; index++)
        {
            var digit = value[index] - '0';
            if ((uint)digit > 9)
                break;
            if (++digitCount > 9)
                return false;
            nanoseconds = nanoseconds * 10 + digit;
        }
        if (digitCount == 0)
            return false;
        while (digitCount++ < 9)
            nanoseconds *= 10;

        var wholeSecondTicks = utcTicks - utcTicks % TimeSpan.TicksPerSecond;
        milliseconds =
            (wholeSecondTicks - DateTime.UnixEpoch.Ticks) / (decimal)TimeSpan.TicksPerMillisecond +
            nanoseconds / 1_000_000m;
        return true;
    }

    private ValidationCelThisNode CreateThisNode(string identifier)
    {
        if (identifier.Length == 4)
        {
            UsesRootValue = true;
            UsesRootSize |= _sizeArgumentDepth != 0;
            return new ValidationCelThisNode(-1);
        }

        var memberPath = identifier[5..];
        var segments = memberPath.Split('.');
        var path = new byte[segments.Length][];
        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index].Length == 0)
                throw Unsupported($"Unsupported CEL identifier '{identifier}'.");
            path[index] = Encoding.UTF8.GetBytes(segments[index]);
        }
        if (!_memberIndexes.TryGetValue(memberPath, out var memberIndex))
        {
            memberIndex = _memberPaths.Count;
            _memberIndexes.Add(memberPath, memberIndex);
            _memberPaths.Add(path);
        }
        _usedMemberIndexes.Add(memberIndex);
        if (_sizeArgumentDepth != 0 && _sizedMemberIndexes?.Add(memberIndex) == true)
            _sizedMemberAdditions!.Add(memberIndex);
        return new ValidationCelThisNode(memberIndex);
    }

    private void RollbackSizedMembers(int count)
    {
        var additions = _sizedMemberAdditions;
        if (additions is null || additions.Count == count)
            return;
        for (var index = additions.Count - 1; index >= count; index--)
            _sizedMemberIndexes!.Remove(additions[index]);
        additions.RemoveRange(count, additions.Count - count);
    }

    private ValidationCelNode ParseParenthesized()
    {
        var value = ParseConditional();
        Expect(ValidationCelTokenKind.RightParen);
        return value;
    }

    private ValidationCelNode[] ParseArguments()
    {
        if (TryTake(ValidationCelTokenKind.RightParen))
            return [];
        var arguments = new List<ValidationCelNode>();
        do
        {
            arguments.Add(ParseConditional());
        }
        while (TryTake(ValidationCelTokenKind.Comma));
        Expect(ValidationCelTokenKind.RightParen);
        return [.. arguments];
    }

    private ValidationCelToken ReadNextToken()
    {
        while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position]))
            _position++;
        if (_position == _expression.Length)
            return new ValidationCelToken(ValidationCelTokenKind.End, string.Empty);

        var character = _expression[_position];
        switch (character)
        {
            case '(':
                _position++;
                return new ValidationCelToken(ValidationCelTokenKind.LeftParen, "(");
            case ')':
                _position++;
                return new ValidationCelToken(ValidationCelTokenKind.RightParen, ")");
            case ',':
                _position++;
                return new ValidationCelToken(ValidationCelTokenKind.Comma, ",");
            case '?':
                _position++;
                return new ValidationCelToken(ValidationCelTokenKind.Question, "?");
            case ':':
                _position++;
                return new ValidationCelToken(ValidationCelTokenKind.Colon, ":");
            case '+':
                _position++;
                return new ValidationCelToken(ValidationCelTokenKind.Plus, "+");
            case '-':
                _position++;
                return new ValidationCelToken(ValidationCelTokenKind.Minus, "-");
            case '!':
                return ReadOperator('=', ValidationCelTokenKind.NotEqual, ValidationCelTokenKind.Not);
            case '=':
                return ReadRequiredOperator('=', ValidationCelTokenKind.Equal);
            case '<':
                return ReadOperator('=', ValidationCelTokenKind.LessOrEqual, ValidationCelTokenKind.Less);
            case '>':
                return ReadOperator('=', ValidationCelTokenKind.GreaterOrEqual, ValidationCelTokenKind.Greater);
            case '&':
                return ReadRequiredOperator('&', ValidationCelTokenKind.And);
            case '|':
                return ReadRequiredOperator('|', ValidationCelTokenKind.Or);
            case '\'':
            case '"':
                return ReadString(character);
            default:
                if (character is 'b' or 'B' &&
                    _position + 1 < _expression.Length &&
                    _expression[_position + 1] is '\'' or '"')
                {
                    _position++;
                    return ReadBytes(_expression[_position]);
                }
                if (char.IsDigit(character))
                    return ReadNumber();
                if (char.IsLetter(character) || character == '_')
                    return ReadIdentifier();
                throw Unsupported($"Unsupported CEL character '{character}'.");
        }
    }

    private ValidationCelToken ReadOperator(
        char second,
        ValidationCelTokenKind combined,
        ValidationCelTokenKind single)
    {
        _position++;
        if (_position < _expression.Length && _expression[_position] == second)
        {
            _position++;
            return new ValidationCelToken(combined, string.Empty);
        }
        return new ValidationCelToken(single, string.Empty);
    }

    private ValidationCelToken ReadRequiredOperator(char second, ValidationCelTokenKind kind)
    {
        _position++;
        if (_position >= _expression.Length || _expression[_position] != second)
            throw Unsupported($"Expected '{second}{second}'.");
        _position++;
        return new ValidationCelToken(kind, string.Empty);
    }

    private ValidationCelToken ReadIdentifier()
    {
        var start = _position++;
        while (_position < _expression.Length &&
               (char.IsLetterOrDigit(_expression[_position]) || _expression[_position] is '_' or '.'))
            _position++;
        var text = _expression[start.._position];
        return text switch
        {
            "true" => new ValidationCelToken(ValidationCelTokenKind.True, text),
            "false" => new ValidationCelToken(ValidationCelTokenKind.False, text),
            "null" => new ValidationCelToken(ValidationCelTokenKind.Null, text),
            _ => new ValidationCelToken(ValidationCelTokenKind.Identifier, text)
        };
    }

    private ValidationCelToken ReadNumber()
    {
        var start = _position++;
        while (_position < _expression.Length && char.IsDigit(_expression[_position]))
            _position++;

        if (_position < _expression.Length && _expression[_position] == '.')
        {
            _position++;
            while (_position < _expression.Length && char.IsDigit(_expression[_position]))
                _position++;
        }

        if (_position < _expression.Length && _expression[_position] is 'e' or 'E')
        {
            _position++;
            if (_position < _expression.Length && _expression[_position] is '+' or '-')
                _position++;
            while (_position < _expression.Length && char.IsDigit(_expression[_position]))
                _position++;
        }

        return new ValidationCelToken(ValidationCelTokenKind.Number, _expression[start.._position]);
    }

    private ValidationCelToken ReadString(char quote)
    {
        _position++;
        var builder = new StringBuilder();
        while (_position < _expression.Length)
        {
            var character = _expression[_position++];
            if (character == quote)
                return new ValidationCelToken(ValidationCelTokenKind.String, builder.ToString());
            if (character != '\\')
            {
                builder.Append(character);
                continue;
            }
            if (_position == _expression.Length)
                throw Unsupported("Unterminated CEL string escape.");
            builder.Append(_expression[_position++] switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '\\' => '\\',
                '\'' => '\'',
                '"' => '"',
                var escaped => throw Unsupported($"Unsupported CEL string escape '\\{escaped}'.")
            });
        }
        throw Unsupported("Unterminated CEL string literal.");
    }

    private ValidationCelToken ReadBytes(char quote)
    {
        _position++;
        var bytes = new List<byte>();
        var text = new StringBuilder();
        while (_position < _expression.Length)
        {
            var character = _expression[_position++];
            if (character == quote)
            {
                AppendUtf8(text, bytes);
                return new ValidationCelToken(
                    ValidationCelTokenKind.Bytes,
                    string.Empty,
                    bytes.ToArray());
            }
            if (character != '\\')
            {
                text.Append(character);
                continue;
            }
            if (_position == _expression.Length)
                throw Unsupported("Unterminated CEL bytes escape.");

            var escaped = _expression[_position++];
            if (escaped is 'x' or 'X')
            {
                AppendUtf8(text, bytes);
                bytes.Add((byte)ReadHexEscape(2));
                continue;
            }
            if (escaped is >= '0' and <= '3')
            {
                AppendUtf8(text, bytes);
                bytes.Add(ReadOctalEscape(escaped));
                continue;
            }
            if (escaped is 'u' or 'U')
            {
                var codePoint = ReadHexEscape(escaped == 'u' ? 4 : 8);
                if ((uint)codePoint > 0x10ffff || codePoint is >= 0xd800 and <= 0xdfff)
                    throw Unsupported("CEL Unicode escape must contain a valid scalar value.");
                if (codePoint <= char.MaxValue)
                {
                    text.Append((char)codePoint);
                }
                else
                {
                    codePoint -= 0x10000;
                    text.Append((char)(0xd800 + (codePoint >> 10)));
                    text.Append((char)(0xdc00 + (codePoint & 0x3ff)));
                }
                continue;
            }

            text.Append(escaped switch
            {
                'a' => '\a',
                'b' => '\b',
                'f' => '\f',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'v' => '\v',
                '\\' => '\\',
                '?' => '?',
                '\'' => '\'',
                '"' => '"',
                '`' => '`',
                var invalid => throw Unsupported($"Unsupported CEL bytes escape '\\{invalid}'.")
            });
        }
        throw Unsupported("Unterminated CEL bytes literal.");
    }

    private int ReadHexEscape(int digits)
    {
        if (_position + digits > _expression.Length)
            throw Unsupported("Incomplete CEL hexadecimal escape.");

        var value = 0;
        for (var index = 0; index < digits; index++)
        {
            var digit = _expression[_position++];
            var nibble = digit switch
            {
                >= '0' and <= '9' => digit - '0',
                >= 'a' and <= 'f' => digit - 'a' + 10,
                >= 'A' and <= 'F' => digit - 'A' + 10,
                _ => throw Unsupported($"Invalid CEL hexadecimal digit '{digit}'.")
            };
            value = (value << 4) | nibble;
        }
        return value;
    }

    private byte ReadOctalEscape(char first)
    {
        if (_position + 2 > _expression.Length)
            throw Unsupported("Incomplete CEL octal escape.");
        var second = _expression[_position++];
        var third = _expression[_position++];
        if (second is not (>= '0' and <= '7') || third is not (>= '0' and <= '7'))
            throw Unsupported("Invalid CEL octal escape.");
        return (byte)(((first - '0') << 6) | ((second - '0') << 3) | third - '0');
    }

    private static void AppendUtf8(StringBuilder text, List<byte> bytes)
    {
        if (text.Length == 0)
            return;
        bytes.AddRange(Encoding.UTF8.GetBytes(text.ToString()));
        text.Clear();
    }

    private bool TryTake(ValidationCelTokenKind kind)
    {
        if (_current.Kind != kind)
            return false;
        _current = ReadNextToken();
        return true;
    }

    private ValidationCelToken Take()
    {
        var result = _current;
        _current = ReadNextToken();
        return result;
    }

    private void Expect(ValidationCelTokenKind kind)
    {
        if (_current.Kind != kind)
            throw Unsupported($"Expected {kind} but found '{_current.Text}'.");
        _current = ReadNextToken();
    }
}

internal static class ValidationCelHelpers
{
    internal static bool RequireBoolean(ValidationCelValue value) =>
        value.Kind == ValidationCelValueKind.Boolean
            ? value.Boolean
            : throw Unsupported("CEL logical operators require boolean operands.");

    internal static decimal RequireNumber(ValidationCelValue value)
    {
        if (value.Kind != ValidationCelValueKind.Number)
            throw Unsupported("CEL arithmetic operators require numeric operands.");
        if (value.Boolean)
            return value.Number;
        throw Unsupported("CEL arithmetic operands must fit the decimal range.");
    }

    internal static SchemaRegistryRuleException Unsupported(string message) =>
        new($"Unsupported CEL expression: {message}");
}
