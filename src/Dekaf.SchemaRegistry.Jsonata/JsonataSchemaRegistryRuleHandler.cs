using System.Runtime.CompilerServices;
using System.Text;
using Jsonata.Net.Native;
using Jsonata.Net.Native.Json;

namespace Dekaf.SchemaRegistry.Jsonata;

/// <summary>
/// Executes Schema Registry JSONata transform and condition rules over JSON codec payloads.
/// </summary>
/// <remarks>
/// Compiled queries are cached per rule and are safe for concurrent evaluation. Binary Avro and
/// Protobuf payloads require codec-level object conversion and are rejected explicitly.
/// </remarks>
public sealed class JsonataSchemaRegistryRuleHandler : ISchemaRegistryRuleTransformResultHandler
{
    private const int MaxRetainedOutputBufferSize = 1024 * 1024;

    /// <summary>
    /// The Schema Registry rule type handled by this implementation.
    /// </summary>
    public const string RuleType = "JSONATA";

    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    [ThreadStatic]
    private static byte[]? t_outputBuffer;

    private readonly ConditionalWeakTable<SchemaRule, JsonataQuery> _queries = new();

    /// <inheritdoc />
    public string Type => RuleType;

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformSerializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context) => Transform(payload, context);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context) => Transform(payload, context);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context,
        out bool payloadChanged)
    {
        var result = Transform(payload, context);
        payloadChanged = context.Rule.Kind == SchemaRuleKind.Transform && !payload.Equals(result);
        return result;
    }

    private ReadOnlyMemory<byte> Transform(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rule = context.Rule;
        if (context.PayloadContext.PayloadFormat != SchemaRegistryPayloadFormat.Json)
        {
            throw new SchemaRegistryRuleException(
                $"JSONata rule '{rule.Name}' requires a JSON payload; " +
                $"'{context.PayloadContext.PayloadFormat}' is not supported.");
        }

        JToken result;
        try
        {
            var input = JToken.Parse(StrictUtf8.GetString(payload.Span));
            var query = _queries.GetValue(rule, static configuredRule => Compile(configuredRule));
            result = query.Eval(input);
        }
        catch (DecoderFallbackException exception)
        {
            throw Failure(rule, "payload is not valid UTF-8", exception);
        }
        catch (JsonParseException exception)
        {
            throw Failure(rule, "payload is not valid JSON", exception);
        }
        catch (JsonataException exception)
        {
            var position = exception.Position is { } value
                ? $" at position {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : string.Empty;
            throw Failure(rule, $"evaluation failed with {exception.Code}{position}", exception);
        }

        if (rule.Kind == SchemaRuleKind.Condition)
        {
            if (result.Type != JTokenType.Boolean)
            {
                throw new SchemaRegistryRuleException(
                    $"JSONata condition rule '{rule.Name}' must evaluate to a boolean value, " +
                    $"but returned {FormatType(result.Type)}.");
            }

            if (!(bool)result)
                throw new SchemaRegistryRuleException($"JSONata condition rule '{rule.Name}' evaluated to false.");

            return payload;
        }

        if (result.Type == JTokenType.Undefined)
        {
            throw new SchemaRegistryRuleException(
                $"JSONata transform rule '{rule.Name}' evaluated to undefined.");
        }

        if (result.Type == JTokenType.Function)
        {
            throw new SchemaRegistryRuleException(
                $"JSONata transform rule '{rule.Name}' returned a function instead of a JSON value.");
        }

        return Encode(result.ToFlatString());
    }

    private static JsonataQuery Compile(SchemaRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Expr))
            throw new SchemaRegistryRuleException($"JSONata rule '{rule.Name}' has no expression.");

        try
        {
            return new JsonataQuery(rule.Expr);
        }
        catch (JsonataException exception)
        {
            var position = exception.Position is { } value
                ? $" at position {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : string.Empty;
            throw Failure(rule, $"expression is invalid ({exception.Code}{position})", exception);
        }
    }

    private static ReadOnlyMemory<byte> Encode(string json)
    {
        var byteCount = StrictUtf8.GetByteCount(json);
        var buffer = t_outputBuffer;
        if (byteCount > MaxRetainedOutputBufferSize)
        {
            buffer = GC.AllocateUninitializedArray<byte>(byteCount);
        }
        else if (buffer is null || buffer.Length < byteCount)
        {
            buffer = GC.AllocateUninitializedArray<byte>(Math.Max(byteCount, 256));
            t_outputBuffer = buffer;
        }

        var length = StrictUtf8.GetBytes(json, buffer);
        return buffer.AsMemory(0, length);
    }

    private static SchemaRegistryRuleException Failure(
        SchemaRule rule,
        string reason,
        Exception exception) =>
        new($"JSONata rule '{rule.Name}' {reason}.", exception);

    private static string FormatType(JTokenType type) =>
        type.ToString().ToLowerInvariant();
}
