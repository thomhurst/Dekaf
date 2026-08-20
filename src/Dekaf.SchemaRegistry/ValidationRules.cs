using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Dekaf.Errors;
using static Dekaf.SchemaRegistry.ValidationCelHelpers;

namespace Dekaf.SchemaRegistry;

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
    private static ReadOnlyMemory<byte>[]? t_memberValues;

    private readonly ValidationCelNode? _expression;
    private readonly string? _compilationError;

    private CompiledValidationRule(
        ValidationRule rule,
        ValidationCelNode? expression,
        string? compilationError = null)
    {
        Rule = rule;
        _expression = expression;
        _compilationError = compilationError;
    }

    internal ValidationRule Rule { get; }

    internal static CompiledValidationRule Compile(
        ValidationRule rule,
        Dictionary<string, int> memberIndexes,
        List<byte[][]> memberPaths)
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
            var parser = new ValidationCelParser(rule.Expr, memberIndexes, memberPaths);
            var expression = parser.Parse();
            return new CompiledValidationRule(rule, expression);
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
        ReadOnlyMemory<byte>[]? memberValues)
    {
        if (_compilationError is not null)
            throw new SchemaRegistryRuleException(_compilationError);

        try
        {
            var result = _expression!.Evaluate(new ValidationCelContext(value, nowUnixMilliseconds, memberValues));
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

    internal static ReadOnlyMemory<byte>[] GetMemberValues(int count)
    {
        var values = t_memberValues;
        if (values is null || values.Length < count)
            t_memberValues = values = new ReadOnlyMemory<byte>[Math.Max(count, 8)];
        return values;
    }
}

internal readonly record struct ValidationCelContext(
    ReadOnlyMemory<byte> This,
    long NowUnixMilliseconds,
    ReadOnlyMemory<byte>[]? MemberValues);

internal sealed class ValidationCelMemberTable
{
    private readonly MemberNode _root;

    internal ValidationCelMemberTable(byte[][][] paths)
    {
        Count = paths.Length;
        var root = new MemberNodeBuilder();
        for (var index = 0; index < paths.Length; index++)
            root.Add(paths[index], index, depth: 0);
        _root = root.Build();
    }

    internal int Count { get; }

    internal void Resolve(ReadOnlyMemory<byte> json, ReadOnlyMemory<byte>[] values)
    {
        var reader = new Utf8JsonReader(json.Span);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            return;

        _root.Resolve(ref reader, json, values);
    }

    private sealed class MemberNode(
        byte[][] names,
        int[] valueIndexes,
        MemberNode?[] children)
    {
        private readonly byte[][] _names = names;
        private readonly int[] _valueIndexes = valueIndexes;
        private readonly MemberNode?[] _children = children;
        private readonly int[] _buckets = CreateBuckets(names);

        internal void Resolve(
            ref Utf8JsonReader reader,
            ReadOnlyMemory<byte> json,
            ReadOnlyMemory<byte>[] values)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    return;
                var index = Find(ref reader);
                if (!reader.Read())
                    return;
                var start = checked((int)reader.TokenStartIndex);
                if (index >= 0 && _valueIndexes[index] >= 0)
                {
                    var end = reader;
                    end.Skip();
                    values[_valueIndexes[index]] = json.Slice(
                        start,
                        checked((int)end.BytesConsumed) - start);
                }

                if (index >= 0 && _children[index] is { } child &&
                    reader.TokenType == JsonTokenType.StartObject)
                {
                    child.Resolve(ref reader, json, values);
                }
                else
                {
                    reader.Skip();
                }
            }
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

        internal void Add(byte[][] path, int valueIndex, int depth)
        {
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
            return new MemberNode([.. _names], [.. _valueIndexes], children);
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
    Object,
    Array
}

internal readonly record struct ValidationCelValue(
    ValidationCelValueKind Kind,
    ReadOnlyMemory<byte> Json,
    bool Boolean,
    decimal Number,
    string? Literal,
    ReadOnlyMemory<byte> Utf8Literal)
{
    internal static ValidationCelValue Missing { get; } = new(ValidationCelValueKind.Missing, default, false, 0, null, default);
    internal static ValidationCelValue Null { get; } = new(ValidationCelValueKind.Null, default, false, 0, null, default);
    internal static ValidationCelValue True { get; } = new(ValidationCelValueKind.Boolean, default, true, 0, null, default);
    internal static ValidationCelValue False { get; } = new(ValidationCelValueKind.Boolean, default, false, 0, null, default);

    internal static ValidationCelValue FromBoolean(bool value) => value ? True : False;
    internal static ValidationCelValue FromNumber(decimal value) =>
        new(ValidationCelValueKind.Number, default, false, value, null, default);

    internal static ValidationCelValue FromString(string value) =>
        new(ValidationCelValueKind.String, default, false, 0, value, Encoding.UTF8.GetBytes(value));

    internal static ValidationCelValue FromJson(ReadOnlyMemory<byte> json)
    {
        var reader = new Utf8JsonReader(json.Span);
        if (!reader.Read())
            return Missing;
        return reader.TokenType switch
        {
            JsonTokenType.Null => Null,
            JsonTokenType.True => True,
            JsonTokenType.False => False,
            JsonTokenType.Number => FromJsonNumber(ref reader),
            JsonTokenType.String => new ValidationCelValue(ValidationCelValueKind.String, json, false, 0, null, default),
            JsonTokenType.StartObject => new ValidationCelValue(ValidationCelValueKind.Object, json, false, 0, null, default),
            JsonTokenType.StartArray => new ValidationCelValue(ValidationCelValueKind.Array, json, false, 0, null, default),
            _ => Missing
        };
    }

    private static ValidationCelValue FromJsonNumber(ref Utf8JsonReader reader)
    {
        if (reader.TryGetDecimal(out var value))
            return FromNumber(value);
        throw Unsupported("CEL number is outside the supported decimal range.");
    }

    internal string GetString()
    {
        if (Literal is not null)
            return Literal;
        var reader = new Utf8JsonReader(Json.Span);
        _ = reader.Read();
        return reader.GetString() ?? string.Empty;
    }
}

internal abstract class ValidationCelNode
{
    internal abstract ValidationCelValue Evaluate(ValidationCelContext context);
}

internal sealed class ValidationCelLiteralNode(ValidationCelValue value) : ValidationCelNode
{
    internal ValidationCelValue Value => value;

    internal override ValidationCelValue Evaluate(ValidationCelContext context) => value;
}

internal sealed class ValidationCelThisNode(int memberIndex) : ValidationCelNode
{
    internal override ValidationCelValue Evaluate(ValidationCelContext context) =>
        ValidationCelValue.FromJson(memberIndex < 0 ? context.This : context.MemberValues![memberIndex]);
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
            ValidationCelTokenKind.Minus => ValidationCelValue.FromNumber(-RequireNumber(value)),
            _ => throw Unsupported("Unsupported unary operator.")
        };
    }
}

internal sealed class ValidationCelBinaryNode(
    ValidationCelTokenKind operation,
    ValidationCelNode left,
    ValidationCelNode right) : ValidationCelNode
{
    internal override ValidationCelValue Evaluate(ValidationCelContext context)
    {
        var leftValue = left.Evaluate(context);
        if (operation == ValidationCelTokenKind.And && !RequireBoolean(leftValue))
            return ValidationCelValue.False;
        if (operation == ValidationCelTokenKind.Or && RequireBoolean(leftValue))
            return ValidationCelValue.True;

        var rightValue = right.Evaluate(context);
        return operation switch
        {
            ValidationCelTokenKind.And or ValidationCelTokenKind.Or =>
                ValidationCelValue.FromBoolean(RequireBoolean(rightValue)),
            ValidationCelTokenKind.Equal => ValidationCelValue.FromBoolean(AreEqual(leftValue, rightValue)),
            ValidationCelTokenKind.NotEqual => ValidationCelValue.FromBoolean(!AreEqual(leftValue, rightValue)),
            ValidationCelTokenKind.Less => ValidationCelValue.FromBoolean(Compare(leftValue, rightValue) < 0),
            ValidationCelTokenKind.LessOrEqual => ValidationCelValue.FromBoolean(Compare(leftValue, rightValue) <= 0),
            ValidationCelTokenKind.Greater => ValidationCelValue.FromBoolean(Compare(leftValue, rightValue) > 0),
            ValidationCelTokenKind.GreaterOrEqual => ValidationCelValue.FromBoolean(Compare(leftValue, rightValue) >= 0),
            ValidationCelTokenKind.Plus => ValidationCelValue.FromNumber(RequireNumber(leftValue) + RequireNumber(rightValue)),
            ValidationCelTokenKind.Minus => ValidationCelValue.FromNumber(RequireNumber(leftValue) - RequireNumber(rightValue)),
            _ => throw Unsupported("Unsupported binary operator.")
        };
    }

    private static bool AreEqual(ValidationCelValue left, ValidationCelValue right)
    {
        if (left.Kind != right.Kind)
            return false;
        return left.Kind switch
        {
            ValidationCelValueKind.Missing or ValidationCelValueKind.Null => true,
            ValidationCelValueKind.Boolean => left.Boolean == right.Boolean,
            ValidationCelValueKind.Number => left.Number.Equals(right.Number),
            ValidationCelValueKind.String => ValidationCelStrings.Evaluate(left, right, ValidationCelStringOperation.Equal),
            ValidationCelValueKind.Object or ValidationCelValueKind.Array =>
                ValidationCelJsonEquality.AreEqual(left.Json.Span, right.Json.Span),
            _ => false
        };
    }

    private static int Compare(ValidationCelValue left, ValidationCelValue right)
    {
        if (left.Kind == ValidationCelValueKind.Number && right.Kind == ValidationCelValueKind.Number)
            return left.Number.CompareTo(right.Number);
        if (left.Kind == ValidationCelValueKind.String && right.Kind == ValidationCelValueKind.String)
            return ValidationCelStrings.Compare(left, right);
        throw Unsupported("Comparison operands must have matching numeric or string types.");
    }
}

internal static class ValidationCelJsonEquality
{
    internal static bool AreEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var leftReader = new Utf8JsonReader(left);
        var rightReader = new Utf8JsonReader(right);
        return leftReader.Read() && rightReader.Read() && AreCurrentEqual(ref leftReader, left, ref rightReader, right);
    }

    private static bool AreCurrentEqual(
        ref Utf8JsonReader leftReader,
        ReadOnlySpan<byte> left,
        ref Utf8JsonReader rightReader,
        ReadOnlySpan<byte> right)
    {
        if (leftReader.TokenType != rightReader.TokenType)
        {
            if (leftReader.TokenType == JsonTokenType.Number && rightReader.TokenType == JsonTokenType.Number)
                return NumbersEqual(ref leftReader, ref rightReader);
            return false;
        }

        return leftReader.TokenType switch
        {
            JsonTokenType.Null => true,
            JsonTokenType.True or JsonTokenType.False => true,
            JsonTokenType.Number => NumbersEqual(ref leftReader, ref rightReader),
            JsonTokenType.String => StringsEqual(ref leftReader, ref rightReader),
            JsonTokenType.StartArray => ArraysEqual(ref leftReader, left, ref rightReader, right),
            JsonTokenType.StartObject => ObjectsEqual(left, right),
            _ => false
        };
    }

    private static bool NumbersEqual(ref Utf8JsonReader left, ref Utf8JsonReader right) =>
        left.TryGetDecimal(out var leftValue) &&
        right.TryGetDecimal(out var rightValue) &&
        leftValue == rightValue;

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

    private static bool ArraysEqual(
        ref Utf8JsonReader leftReader,
        ReadOnlySpan<byte> left,
        ref Utf8JsonReader rightReader,
        ReadOnlySpan<byte> right)
    {
        while (leftReader.Read() && rightReader.Read())
        {
            if (leftReader.TokenType == JsonTokenType.EndArray || rightReader.TokenType == JsonTokenType.EndArray)
                return leftReader.TokenType == rightReader.TokenType;
            var leftValue = SliceCurrent(ref leftReader, left);
            var rightValue = SliceCurrent(ref rightReader, right);
            if (!AreEqual(leftValue, rightValue))
                return false;
        }
        return false;
    }

    private static bool ObjectsEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var count = CountProperties(left);
        var capacity = 4;
        while (capacity < count * 2)
            capacity <<= 1;

        PropertyEntry[]? rented = null;
        Span<PropertyEntry> table = capacity <= 64
            ? stackalloc PropertyEntry[capacity]
            : (rented = ArrayPool<PropertyEntry>.Shared.Rent(capacity));
        table = table[..capacity];
        table.Clear();
        try
        {
            FillTable(left, table);
            var reader = new Utf8JsonReader(right);
            if (!reader.Read())
                return false;
            var matched = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    return false;
                var hash = HashName(ref reader);
                var bucket = (int)(hash & (uint)(capacity - 1));
                while (table[bucket].Occupied &&
                       (table[bucket].Hash != hash || !NameEquals(left, table[bucket], ref reader)))
                    bucket = (bucket + 1) & (capacity - 1);
                if (!table[bucket].Occupied || table[bucket].Matched || !reader.Read())
                    return false;
                var rightValue = SliceCurrent(ref reader, right);
                var entry = table[bucket];
                if (!AreEqual(left.Slice(entry.ValueStart, entry.ValueLength), rightValue))
                    return false;
                entry.Matched = true;
                table[bucket] = entry;
                matched++;
            }
            return matched == count;
        }
        finally
        {
            if (rented is not null)
                ArrayPool<PropertyEntry>.Shared.Return(rented);
        }
    }

    private static int CountProperties(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json);
        _ = reader.Read();
        var count = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            count++;
            if (!reader.Read())
                break;
            reader.Skip();
        }
        return count;
    }

    private static void FillTable(ReadOnlySpan<byte> json, Span<PropertyEntry> table)
    {
        var reader = new Utf8JsonReader(json);
        _ = reader.Read();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var nameStart = checked((int)reader.TokenStartIndex + 1);
            var nameLength = reader.ValueSpan.Length;
            var escaped = reader.ValueIsEscaped;
            var hash = HashName(ref reader);
            if (!reader.Read())
                return;
            var valueStart = checked((int)reader.TokenStartIndex);
            var end = reader;
            end.Skip();
            var bucket = (int)(hash & (uint)(table.Length - 1));
            while (table[bucket].Occupied)
                bucket = (bucket + 1) & (table.Length - 1);
            table[bucket] = new PropertyEntry(
                hash,
                nameStart,
                nameLength,
                valueStart,
                checked((int)end.BytesConsumed) - valueStart,
                escaped);
            reader.Skip();
        }
    }

    private static bool NameEquals(ReadOnlySpan<byte> source, PropertyEntry entry, ref Utf8JsonReader right)
    {
        var name = source.Slice(entry.NameStart, entry.NameLength);
        if (!entry.NameEscaped)
            return right.ValueTextEquals(name);
        var reader = new Utf8JsonReader(source.Slice(entry.NameStart - 1, entry.NameLength + 2));
        _ = reader.Read();
        return StringsEqual(ref reader, ref right);
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

    private static ReadOnlySpan<byte> SliceCurrent(ref Utf8JsonReader reader, ReadOnlySpan<byte> source)
    {
        var start = checked((int)reader.TokenStartIndex);
        var end = reader;
        end.Skip();
        reader.Skip();
        return source.Slice(start, checked((int)end.BytesConsumed) - start);
    }

    private struct PropertyEntry(
        uint hash,
        int nameStart,
        int nameLength,
        int valueStart,
        int valueLength,
        bool nameEscaped)
    {
        internal readonly uint Hash = hash;
        internal readonly int NameStart = nameStart;
        internal readonly int NameLength = nameLength;
        internal readonly int ValueStart = valueStart;
        internal readonly int ValueLength = valueLength;
        internal readonly bool NameEscaped = nameEscaped;
        internal readonly bool Occupied = true;
        internal bool Matched;
    }
}

internal sealed class ValidationCelConditionalNode(
    ValidationCelNode condition,
    ValidationCelNode whenTrue,
    ValidationCelNode whenFalse) : ValidationCelNode
{
    internal override ValidationCelValue Evaluate(ValidationCelContext context) =>
        RequireBoolean(condition.Evaluate(context))
            ? whenTrue.Evaluate(context)
            : whenFalse.Evaluate(context);
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
                arguments[0].Evaluate(context).Kind != ValidationCelValueKind.Missing);
        }

        if (name == "size")
        {
            RequireArgumentCount(1);
            return ValidationCelValue.FromNumber(GetSize(arguments[0].Evaluate(context)));
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

    private static int GetSize(ValidationCelValue value)
    {
        if (value.Kind == ValidationCelValueKind.String)
            return ValidationCelStrings.GetLength(value);
        if (value.Kind is not (ValidationCelValueKind.Array or ValidationCelValueKind.Object))
            throw Unsupported("CEL function 'size' requires a string, list, or map.");

        var reader = new Utf8JsonReader(value.Json.Span);
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

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName || !reader.Read())
                throw Unsupported("CEL map value contains invalid JSON.");
            count++;
            reader.Skip();
        }
        return count;
    }
}

internal enum ValidationCelStringOperation : byte
{
    Equal,
    StartsWith,
    EndsWith,
    Contains
}

internal static class ValidationCelStrings
{
    private const int StackBufferLength = 256;

    internal static bool Evaluate(
        ValidationCelValue left,
        ValidationCelValue right,
        ValidationCelStringOperation operation)
    {
        byte[]? leftRented = null;
        byte[]? rightRented = null;
        var leftMaximum = GetMaximumLength(left);
        var rightMaximum = GetMaximumLength(right);
        Span<byte> leftBuffer = leftMaximum <= StackBufferLength
            ? stackalloc byte[leftMaximum]
            : (leftRented = ArrayPool<byte>.Shared.Rent(leftMaximum));
        Span<byte> rightBuffer = rightMaximum <= StackBufferLength
            ? stackalloc byte[rightMaximum]
            : (rightRented = ArrayPool<byte>.Shared.Rent(rightMaximum));
        try
        {
            var leftText = Decode(left, leftBuffer);
            var rightText = Decode(right, rightBuffer);
            return operation switch
            {
                ValidationCelStringOperation.Equal => leftText.SequenceEqual(rightText),
                ValidationCelStringOperation.StartsWith => leftText.StartsWith(rightText),
                ValidationCelStringOperation.EndsWith => leftText.EndsWith(rightText),
                ValidationCelStringOperation.Contains => leftText.IndexOf(rightText) >= 0,
                _ => false
            };
        }
        finally
        {
            if (leftRented is not null)
                ArrayPool<byte>.Shared.Return(leftRented);
            if (rightRented is not null)
                ArrayPool<byte>.Shared.Return(rightRented);
        }
    }

    internal static int Compare(ValidationCelValue left, ValidationCelValue right)
    {
        byte[]? leftRented = null;
        byte[]? rightRented = null;
        var leftMaximum = GetMaximumLength(left);
        var rightMaximum = GetMaximumLength(right);
        Span<byte> leftBuffer = leftMaximum <= StackBufferLength
            ? stackalloc byte[leftMaximum]
            : (leftRented = ArrayPool<byte>.Shared.Rent(leftMaximum));
        Span<byte> rightBuffer = rightMaximum <= StackBufferLength
            ? stackalloc byte[rightMaximum]
            : (rightRented = ArrayPool<byte>.Shared.Rent(rightMaximum));
        try
        {
            return Decode(left, leftBuffer).SequenceCompareTo(Decode(right, rightBuffer));
        }
        finally
        {
            if (leftRented is not null)
                ArrayPool<byte>.Shared.Return(leftRented);
            if (rightRented is not null)
                ArrayPool<byte>.Shared.Return(rightRented);
        }
    }

    internal static int GetLength(ValidationCelValue value)
    {
        byte[]? rented = null;
        var maximum = GetMaximumLength(value);
        Span<byte> buffer = maximum <= StackBufferLength
            ? stackalloc byte[maximum]
            : (rented = ArrayPool<byte>.Shared.Rent(maximum));
        try
        {
            var text = Decode(value, buffer);
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
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static int GetMaximumLength(ValidationCelValue value) =>
        value.Literal is null ? value.Json.Length : value.Utf8Literal.Length;

    private static ReadOnlySpan<byte> Decode(ValidationCelValue value, Span<byte> destination)
    {
        if (value.Literal is not null)
        {
            value.Utf8Literal.Span.CopyTo(destination);
            return destination[..value.Utf8Literal.Length];
        }

        var reader = new Utf8JsonReader(value.Json.Span);
        _ = reader.Read();
        var written = reader.CopyString(destination);
        return destination[..written];
    }
}

internal enum ValidationCelTokenKind : byte
{
    End,
    Identifier,
    String,
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

internal readonly record struct ValidationCelToken(ValidationCelTokenKind Kind, string Text);

internal sealed class ValidationCelParser
{
    private readonly string _expression;
    private readonly Dictionary<string, int> _memberIndexes;
    private readonly List<byte[][]> _memberPaths;
    private int _position;
    private ValidationCelToken _current;

    internal ValidationCelParser(
        string expression,
        Dictionary<string, int> memberIndexes,
        List<byte[][]> memberPaths)
    {
        _expression = expression;
        _memberIndexes = memberIndexes;
        _memberPaths = memberPaths;
        _current = ReadNextToken();
    }

    internal ValidationCelNode Parse()
    {
        var result = ParseConditional();
        Expect(ValidationCelTokenKind.End);
        return result;
    }

    private ValidationCelNode ParseConditional()
    {
        var condition = ParseOr();
        if (!TryTake(ValidationCelTokenKind.Question))
            return condition;
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
            left = new ValidationCelBinaryNode(operation, left, ParseComparison());
        }
        return left;
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
            ValidationCelTokenKind.Number => new ValidationCelLiteralNode(
                ValidationCelValue.FromNumber(ParseNumber(token.Text))),
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
            var arguments = ParseArguments();
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
            !DateTimeOffset.TryParse(
                literal.Value.Literal,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp))
            throw Unsupported("CEL function 'timestamp' requires one ISO-8601 string literal.");

        return new ValidationCelLiteralNode(
            ValidationCelValue.FromNumber(timestamp.ToUnixTimeMilliseconds()));
    }

    private ValidationCelThisNode CreateThisNode(string identifier)
    {
        if (identifier.Length == 4)
            return new ValidationCelThisNode(-1);

        var memberPath = identifier[5..];
        var segments = memberPath.Split('.');
        var path = new byte[segments.Length][];
        for (var index = 0; index < segments.Length; index++)
            path[index] = Encoding.UTF8.GetBytes(segments[index]);
        if (!_memberIndexes.TryGetValue(memberPath, out var memberIndex))
        {
            memberIndex = _memberPaths.Count;
            _memberIndexes.Add(memberPath, memberIndex);
            _memberPaths.Add(path);
        }
        return new ValidationCelThisNode(memberIndex);
    }

    private static decimal ParseNumber(string text)
    {
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;
        throw Unsupported($"Invalid CEL number '{text}'.");
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

    internal static decimal RequireNumber(ValidationCelValue value) =>
        value.Kind == ValidationCelValueKind.Number
            ? value.Number
            : throw Unsupported("CEL arithmetic operators require numeric operands.");

    internal static SchemaRegistryRuleException Unsupported(string message) =>
        new($"Unsupported CEL expression: {message}");
}
