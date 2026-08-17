using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Handles Schema Registry CEL condition rules and the supported subset of CEL transform rules.
/// </summary>
/// <remarks>
/// This handler intentionally implements a safe, bounded CEL subset over Schema Registry payload
/// context and schema metadata. Unsupported CEL constructs fail closed with
/// <see cref="SchemaRegistryRuleException" /> instead of being ignored.
/// </remarks>
public sealed class CelSchemaRegistryRuleHandler : ISchemaRegistryRuleHandler
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly ConditionalWeakTable<SchemaRule, ParsedCelExpression> ParsedExpressions = new();
    private static readonly ConditionalWeakTable<string, byte[]> TransformUtf8Values = new();

    [ThreadStatic]
    private static Utf8TextCache? t_utf8Values;

    /// <inheritdoc />
    public string Type => "CEL";

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformSerializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context)
        => ApplyRule(payload, context);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context)
        => ApplyRule(payload, context);

    private static ReadOnlyMemory<byte> ApplyRule(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context)
    {
        var value = ParsedExpressions
            .GetValue(context.Rule, static rule => ParsedCelExpression.Create(rule))
            .Evaluate(payload, context);

        if (context.Rule.Kind == SchemaRuleKind.Condition)
        {
            if (value.Kind != CelValueKind.Boolean)
            {
                throw new SchemaRegistryRuleException(
                    $"CEL condition rule '{context.Rule.Name}' must evaluate to a boolean value.");
            }

            if (!value.Boolean)
                throw new SchemaRegistryRuleException($"CEL condition rule '{context.Rule.Name}' evaluated to false.");

            return payload;
        }

        if (value.Kind is CelValueKind.String or CelValueKind.Utf8String)
            return GetUtf8Memory(value);

        throw new SchemaRegistryRuleException(
            $"CEL transform rule '{context.Rule.Name}' returned {value.Kind.ToString().ToLowerInvariant()}; only string transform results are supported.");
    }

    private static CelValue PayloadAsUtf8(ReadOnlyMemory<byte> payload)
    {
        try
        {
            _ = StrictUtf8.GetCharCount(payload.Span);
            return CelValue.FromUtf8(payload);
        }
        catch (DecoderFallbackException ex)
        {
            throw new SchemaRegistryRuleException("CEL variable 'message' requires a valid UTF-8 payload.", ex);
        }
    }

    private enum CelTokenKind
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
        Not,
        And,
        Or,
        Equal,
        NotEqual
    }

    private readonly record struct CelToken(CelTokenKind Kind, string Text);

    private enum CelValueKind
    {
        Null,
        Boolean,
        String,
        Utf8String,
        Number
    }

    private readonly record struct CelValue(
        CelValueKind Kind,
        bool Boolean,
        string String,
        ReadOnlyMemory<byte> Utf8,
        long Number)
    {
        public static CelValue Null { get; } = new(CelValueKind.Null, false, string.Empty, default, 0);
        public static CelValue True { get; } = new(CelValueKind.Boolean, true, string.Empty, default, 0);
        public static CelValue False { get; } = new(CelValueKind.Boolean, false, string.Empty, default, 0);
        public static CelValue FromBoolean(bool value) => value ? True : False;
        public static CelValue FromString(string? value) =>
            value is null ? Null : new CelValue(CelValueKind.String, false, value, default, 0);
        public static CelValue FromUtf8(ReadOnlyMemory<byte> value) =>
            new(CelValueKind.Utf8String, false, string.Empty, value, 0);
        public static CelValue FromNumber(long value) =>
            new(CelValueKind.Number, false, string.Empty, default, value);
    }

    private readonly record struct CelEvaluationContext(
        ReadOnlyMemory<byte> Payload,
        SchemaRegistryRuleHandlerContext RuleContext);

    private sealed class ParsedCelExpression
    {
        private readonly CelNode _root;

        private ParsedCelExpression(CelNode root)
        {
            _root = root;
        }

        public static ParsedCelExpression Create(SchemaRule rule)
        {
            var expression = rule.Expr;
            if (string.IsNullOrWhiteSpace(expression))
                throw new SchemaRegistryRuleException($"CEL rule '{rule.Name}' has no expression.");

            return new ParsedCelExpression(new CelExpressionParser(expression).Parse());
        }

        public CelValue Evaluate(ReadOnlyMemory<byte> payload, SchemaRegistryRuleHandlerContext context)
            => _root.Evaluate(new CelEvaluationContext(payload, context));
    }

    private abstract class CelNode
    {
        public abstract CelValue Evaluate(CelEvaluationContext context);
    }

    private sealed class LiteralNode(CelValue value) : CelNode
    {
        public override CelValue Evaluate(CelEvaluationContext context) => value;
    }

    private sealed class IdentifierNode(string identifier) : CelNode
    {
        public override CelValue Evaluate(CelEvaluationContext context)
        {
            var payloadContext = context.RuleContext.PayloadContext;
            return identifier switch
            {
                "message" or "payload" => PayloadAsUtf8(context.Payload),
                "topic" => CelValue.FromString(payloadContext.Topic),
                "subject" => CelValue.FromString(payloadContext.Subject),
                "schemaId" => CelValue.FromNumber(payloadContext.SchemaId),
                "component" => CelValue.FromString(FormatComponent(payloadContext.Component)),
                "format" or "payloadFormat" => CelValue.FromString(FormatPayloadFormat(payloadContext.PayloadFormat)),
                "rule.name" => CelValue.FromString(context.RuleContext.Rule.Name),
                "rule.type" => CelValue.FromString(context.RuleContext.Rule.Type),
                "rule.mode" => CelValue.FromString(FormatRuleMode(context.RuleContext.Rule.Mode)),
                "rule.kind" => CelValue.FromString(FormatRuleKind(context.RuleContext.Rule.Kind)),
                _ when identifier.StartsWith("metadata.", StringComparison.Ordinal) =>
                    CelValue.FromString(GetMetadataProperty(context, identifier["metadata.".Length..])),
                _ => throw Unsupported($"Unsupported CEL identifier '{identifier}'.")
            };
        }
    }

    private sealed class FunctionNode(string name, CelNode[] arguments) : CelNode
    {
        public override CelValue Evaluate(CelEvaluationContext context)
            => name switch
            {
                "metadata" => CelValue.FromString(GetMetadataProperty(
                    context,
                    ExpectUtf16Argument(name, arguments, 0, 1, context))),
                "hasMetadata" => CelValue.FromBoolean(GetMetadataProperty(
                    context,
                    ExpectUtf16Argument(name, arguments, 0, 1, context)) is not null),
                "hasTag" or "tagged" => CelValue.FromBoolean(HasTag(
                    context,
                    ExpectUtf16Argument(name, arguments, 0, 2, context),
                    ExpectUtf16Argument(name, arguments, 1, 2, context))),
                "contains" => CelValue.FromBoolean(Contains(
                    ExpectStringArgument(name, arguments, 0, 2, context),
                    ExpectStringArgument(name, arguments, 1, 2, context))),
                "startsWith" => CelValue.FromBoolean(StartsWith(
                    ExpectStringArgument(name, arguments, 0, 2, context),
                    ExpectStringArgument(name, arguments, 1, 2, context))),
                "endsWith" => CelValue.FromBoolean(EndsWith(
                    ExpectStringArgument(name, arguments, 0, 2, context),
                    ExpectStringArgument(name, arguments, 1, 2, context))),
                _ => throw Unsupported($"Unsupported CEL function '{name}'.")
            };
    }

    private sealed class NotNode(CelNode operand) : CelNode
    {
        public override CelValue Evaluate(CelEvaluationContext context)
            => CelValue.FromBoolean(!AsBoolean(operand.Evaluate(context)));
    }

    private sealed class AndNode(CelNode left, CelNode right) : CelNode
    {
        public override CelValue Evaluate(CelEvaluationContext context)
            => !AsBoolean(left.Evaluate(context))
                ? CelValue.False
                : CelValue.FromBoolean(AsBoolean(right.Evaluate(context)));
    }

    private sealed class OrNode(CelNode left, CelNode right) : CelNode
    {
        public override CelValue Evaluate(CelEvaluationContext context)
            => AsBoolean(left.Evaluate(context))
                ? CelValue.True
                : CelValue.FromBoolean(AsBoolean(right.Evaluate(context)));
    }

    private sealed class EqualityNode(CelNode left, CelNode right, bool negate) : CelNode
    {
        public override CelValue Evaluate(CelEvaluationContext context)
        {
            var equal = AreEqual(left.Evaluate(context), right.Evaluate(context));
            return CelValue.FromBoolean(negate ? !equal : equal);
        }
    }

    private static string? GetMetadataProperty(CelEvaluationContext context, string name)
    {
        if (string.IsNullOrEmpty(name))
            throw Unsupported("Metadata property name cannot be empty.");

        return context.RuleContext.PayloadContext.Schema?.Metadata?.Properties?.TryGetValue(name, out var value) == true
            ? value
            : null;
    }

    private static bool HasTag(CelEvaluationContext context, string path, string tag)
    {
        if (context.RuleContext.PayloadContext.Schema?.Metadata?.Tags is not { } tags)
            return false;

        return tags.TryGetValue(path, out var pathTags) && pathTags.Contains(tag);
    }

    private static CelValue ExpectStringArgument(
        string functionName,
        IReadOnlyList<CelNode> arguments,
        int index,
        int expectedCount,
        CelEvaluationContext context)
    {
        if (arguments.Count != expectedCount)
        {
            throw Unsupported(
                $"CEL function '{functionName}' expects {expectedCount.ToString(CultureInfo.InvariantCulture)} argument(s).");
        }

        var value = arguments[index].Evaluate(context);
        if (value.Kind is not (CelValueKind.String or CelValueKind.Utf8String))
            throw Unsupported($"CEL function '{functionName}' argument {index + 1} must be a string.");

        return value;
    }

    private static string ExpectUtf16Argument(
        string functionName,
        IReadOnlyList<CelNode> arguments,
        int index,
        int expectedCount,
        CelEvaluationContext context)
    {
        var value = ExpectStringArgument(functionName, arguments, index, expectedCount, context);
        if (value.Kind != CelValueKind.String)
        {
            throw Unsupported(
                $"CEL function '{functionName}' argument {index + 1} must be a context or literal string.");
        }

        return value.String;
    }

    private sealed class CelExpressionParser
    {
        private readonly string _expression;
        private int _position;
        private CelToken _current;

        public CelExpressionParser(string expression)
        {
            _expression = expression;
            _current = ReadNextToken();
        }

        public CelNode Parse()
        {
            var value = ParseOr();
            Expect(CelTokenKind.End);
            return value;
        }

        private CelNode ParseOr()
        {
            var left = ParseAnd();
            while (TryTake(CelTokenKind.Or))
            {
                left = new OrNode(left, ParseAnd());
            }

            return left;
        }

        private CelNode ParseAnd()
        {
            var left = ParseEquality();
            while (TryTake(CelTokenKind.And))
            {
                left = new AndNode(left, ParseEquality());
            }

            return left;
        }

        private CelNode ParseEquality()
        {
            var left = ParseUnary();
            while (_current.Kind is CelTokenKind.Equal or CelTokenKind.NotEqual)
            {
                var negate = _current.Kind == CelTokenKind.NotEqual;
                Take();
                left = new EqualityNode(left, ParseUnary(), negate);
            }

            return left;
        }

        private CelNode ParseUnary()
        {
            if (!TryTake(CelTokenKind.Not))
                return ParsePrimary();

            return new NotNode(ParseUnary());
        }

        private CelNode ParsePrimary()
        {
            var token = _current;
            switch (token.Kind)
            {
                case CelTokenKind.True:
                    Take();
                    return new LiteralNode(CelValue.True);
                case CelTokenKind.False:
                    Take();
                    return new LiteralNode(CelValue.False);
                case CelTokenKind.Null:
                    Take();
                    return new LiteralNode(CelValue.Null);
                case CelTokenKind.String:
                    Take();
                    return new LiteralNode(CelValue.FromString(token.Text));
                case CelTokenKind.Number:
                    Take();
                    return new LiteralNode(CelValue.FromNumber(long.Parse(token.Text, CultureInfo.InvariantCulture)));
                case CelTokenKind.Identifier:
                    return ParseIdentifierOrCall(token.Text);
                case CelTokenKind.LeftParen:
                    Take();
                    var value = ParseOr();
                    Expect(CelTokenKind.RightParen);
                    return value;
                default:
                    throw Unsupported($"Unexpected token '{token.Text}'.");
            }
        }

        private CelNode ParseIdentifierOrCall(string identifier)
        {
            Take();
            if (!TryTake(CelTokenKind.LeftParen))
                return new IdentifierNode(identifier);

            var arguments = new List<CelNode>();
            if (!TryTake(CelTokenKind.RightParen))
            {
                do
                {
                    arguments.Add(ParseOr());
                }
                while (TryTake(CelTokenKind.Comma));

                Expect(CelTokenKind.RightParen);
            }

            return new FunctionNode(identifier, [.. arguments]);
        }

        private CelToken ReadNextToken()
        {
            SkipWhitespace();
            if (_position >= _expression.Length)
                return new CelToken(CelTokenKind.End, string.Empty);

            var ch = _expression[_position];
            switch (ch)
            {
                case '(':
                    _position++;
                    return new CelToken(CelTokenKind.LeftParen, "(");
                case ')':
                    _position++;
                    return new CelToken(CelTokenKind.RightParen, ")");
                case ',':
                    _position++;
                    return new CelToken(CelTokenKind.Comma, ",");
                case '!':
                    if (PeekNext('='))
                    {
                        _position += 2;
                        return new CelToken(CelTokenKind.NotEqual, "!=");
                    }

                    _position++;
                    return new CelToken(CelTokenKind.Not, "!");
                case '=' when PeekNext('='):
                    _position += 2;
                    return new CelToken(CelTokenKind.Equal, "==");
                case '&' when PeekNext('&'):
                    _position += 2;
                    return new CelToken(CelTokenKind.And, "&&");
                case '|' when PeekNext('|'):
                    _position += 2;
                    return new CelToken(CelTokenKind.Or, "||");
                case '"':
                case '\'':
                    return ReadString(ch);
                default:
                    if (char.IsDigit(ch))
                        return ReadNumber();

                    if (IsIdentifierStart(ch))
                        return ReadIdentifier();

                    throw Unsupported($"Unsupported CEL character '{ch}'.");
            }
        }

        private CelToken ReadIdentifier()
        {
            var start = _position;
            _position++;
            while (_position < _expression.Length && IsIdentifierPart(_expression[_position]))
            {
                _position++;
            }

            var text = _expression[start.._position];
            return text switch
            {
                "true" => new CelToken(CelTokenKind.True, text),
                "false" => new CelToken(CelTokenKind.False, text),
                "null" => new CelToken(CelTokenKind.Null, text),
                _ => new CelToken(CelTokenKind.Identifier, text)
            };
        }

        private CelToken ReadNumber()
        {
            var start = _position;
            while (_position < _expression.Length && char.IsDigit(_expression[_position]))
            {
                _position++;
            }

            return new CelToken(CelTokenKind.Number, _expression[start.._position]);
        }

        private CelToken ReadString(char quote)
        {
            _position++;
            var builder = new StringBuilder();
            while (_position < _expression.Length)
            {
                var ch = _expression[_position++];
                if (ch == quote)
                    return new CelToken(CelTokenKind.String, builder.ToString());

                if (ch != '\\')
                {
                    builder.Append(ch);
                    continue;
                }

                if (_position >= _expression.Length)
                    throw Unsupported("Unterminated CEL string escape.");

                var escaped = _expression[_position++];
                builder.Append(escaped switch
                {
                    '"' => '"',
                    '\'' => '\'',
                    '\\' => '\\',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => throw Unsupported($"Unsupported CEL string escape '\\{escaped}'.")
                });
            }

            throw Unsupported("Unterminated CEL string literal.");
        }

        private void SkipWhitespace()
        {
            while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position]))
            {
                _position++;
            }
        }

        private bool PeekNext(char expected) =>
            _position + 1 < _expression.Length && _expression[_position + 1] == expected;

        private bool TryTake(CelTokenKind kind)
        {
            if (_current.Kind != kind)
                return false;

            Take();
            return true;
        }

        private void Expect(CelTokenKind kind)
        {
            if (_current.Kind != kind)
                throw Unsupported($"Expected {kind} but found '{_current.Text}'.");

            Take();
        }

        private void Take() => _current = ReadNextToken();
    }

    private static bool AreEqual(CelValue left, CelValue right)
    {
        if (IsString(left) && IsString(right))
            return StringEquals(left, right);

        if (left.Kind != right.Kind)
            return false;

        return left.Kind switch
        {
            CelValueKind.Null => true,
            CelValueKind.Boolean => left.Boolean == right.Boolean,
            CelValueKind.Number => left.Number == right.Number,
            _ => false
        };
    }

    private static bool StringEquals(CelValue left, CelValue right)
    {
        if (left.Kind == CelValueKind.String && right.Kind == CelValueKind.String)
            return string.Equals(left.String, right.String, StringComparison.Ordinal);

        return GetUtf8Span(left).SequenceEqual(GetUtf8Span(right));
    }

    private static bool Contains(CelValue value, CelValue candidate)
    {
        if (value.Kind == CelValueKind.String && candidate.Kind == CelValueKind.String)
            return value.String.Contains(candidate.String, StringComparison.Ordinal);

        return GetUtf8Span(value).IndexOf(GetUtf8Span(candidate)) >= 0;
    }

    private static bool StartsWith(CelValue value, CelValue candidate)
    {
        if (value.Kind == CelValueKind.String && candidate.Kind == CelValueKind.String)
            return value.String.StartsWith(candidate.String, StringComparison.Ordinal);

        return GetUtf8Span(value).StartsWith(GetUtf8Span(candidate));
    }

    private static bool EndsWith(CelValue value, CelValue candidate)
    {
        if (value.Kind == CelValueKind.String && candidate.Kind == CelValueKind.String)
            return value.String.EndsWith(candidate.String, StringComparison.Ordinal);

        return GetUtf8Span(value).EndsWith(GetUtf8Span(candidate));
    }

    private static bool IsString(CelValue value) =>
        value.Kind is CelValueKind.String or CelValueKind.Utf8String;

    private static ReadOnlyMemory<byte> GetUtf8Memory(CelValue value) =>
        value.Kind == CelValueKind.Utf8String
            ? value.Utf8
            : TransformUtf8Values.GetValue(value.String, static text => StrictUtf8.GetBytes(text));

    private static ReadOnlySpan<byte> GetUtf8Span(CelValue value)
    {
        if (value.Kind == CelValueKind.Utf8String)
            return value.Utf8.Span;

        return (t_utf8Values ??= new Utf8TextCache()).Get(value.String);
    }

    private sealed class Utf8TextCache
    {
        private const int Capacity = 8;
        private readonly string?[] _texts = new string?[Capacity];
        private readonly byte[]?[] _buffers = new byte[Capacity][];
        private readonly int[] _lengths = new int[Capacity];
        private int _count;
        private int _next;

        public ReadOnlySpan<byte> Get(string text)
        {
            for (var index = 0; index < _count; index++)
            {
                var cached = _texts[index];
                if (ReferenceEquals(cached, text) || string.Equals(cached, text, StringComparison.Ordinal))
                {
                    _texts[index] = text;
                    return _buffers[index].AsSpan(0, _lengths[index]);
                }
            }

            var slot = _count < Capacity ? _count++ : _next++ & (Capacity - 1);
            var byteCount = StrictUtf8.GetByteCount(text);
            var buffer = _buffers[slot];
            if (buffer is null || buffer.Length < byteCount)
            {
                buffer = GC.AllocateUninitializedArray<byte>(Math.Max(byteCount, 256));
                _buffers[slot] = buffer;
            }

            _lengths[slot] = StrictUtf8.GetBytes(text, buffer);
            _texts[slot] = text;
            return buffer.AsSpan(0, _lengths[slot]);
        }
    }

    private static bool AsBoolean(CelValue value)
    {
        if (value.Kind == CelValueKind.Boolean)
            return value.Boolean;

        throw Unsupported("CEL logical operators require boolean operands.");
    }

    private static string FormatComponent(SerializationComponent component) =>
        component == SerializationComponent.Key ? "key" : "value";

    private static string FormatPayloadFormat(SchemaRegistryPayloadFormat format) =>
        format switch
        {
            SchemaRegistryPayloadFormat.Custom => "Custom",
            SchemaRegistryPayloadFormat.Json => "Json",
            SchemaRegistryPayloadFormat.Avro => "Avro",
            SchemaRegistryPayloadFormat.Protobuf => "Protobuf",
            _ => throw Unsupported($"Unsupported payload format '{format}'.")
        };

    private static string FormatRuleMode(SchemaRuleMode mode) =>
        mode switch
        {
            SchemaRuleMode.Upgrade => "Upgrade",
            SchemaRuleMode.Downgrade => "Downgrade",
            SchemaRuleMode.UpDown => "UpDown",
            SchemaRuleMode.Read => "Read",
            SchemaRuleMode.Write => "Write",
            SchemaRuleMode.WriteRead => "WriteRead",
            _ => throw Unsupported($"Unsupported rule mode '{mode}'.")
        };

    private static string FormatRuleKind(SchemaRuleKind kind) =>
        kind switch
        {
            SchemaRuleKind.Transform => "Transform",
            SchemaRuleKind.Condition => "Condition",
            _ => throw Unsupported($"Unsupported rule kind '{kind}'.")
        };

    private static bool IsIdentifierStart(char ch) => char.IsLetter(ch) || ch == '_';

    private static bool IsIdentifierPart(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '_' or '.';

    private static SchemaRegistryRuleException Unsupported(string message) =>
        new($"Unsupported CEL expression: {message}");
}
