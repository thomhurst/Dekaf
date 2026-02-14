using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dekaf.Serialization;

namespace Dekaf.Diagnostics;

/// <summary>
/// W3C Trace Context propagation for Kafka message headers.
/// Injects/extracts <c>traceparent</c> and <c>tracestate</c> headers
/// following the W3C specification.
/// </summary>
internal static class TraceContextPropagator
{
    private const string TraceparentHeader = "traceparent";
    private const string TracestateHeader = "tracestate";

    /// <summary>
    /// Injects the current trace context into message headers.
    /// Returns immediately when <paramref name="activity"/> is null (zero cost).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Headers? InjectTraceContext(Headers? headers, Activity? activity)
    {
        if (activity is null)
            return headers;

        return InjectTraceContextSlow(headers, activity);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Headers InjectTraceContextSlow(Headers? headers, Activity activity)
    {
        headers ??= new Headers(2);

        // W3C traceparent: 00-{traceId}-{spanId}-{traceFlags}
        var traceparent = $"00-{activity.TraceId}-{activity.SpanId}-{(activity.Recorded ? "01" : "00")}";
        headers.Add(TraceparentHeader, traceparent);

        if (!string.IsNullOrEmpty(activity.TraceStateString))
        {
            headers.Add(TracestateHeader, activity.TraceStateString);
        }

        return headers;
    }

    /// <summary>
    /// Extracts trace context from consumed message headers.
    /// Returns null if no valid <c>traceparent</c> header is found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ActivityContext? ExtractTraceContext(IReadOnlyList<Header>? headers)
    {
        if (headers is null || headers.Count == 0)
            return null;

        return ExtractTraceContextSlow(headers);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ActivityContext? ExtractTraceContextSlow(IReadOnlyList<Header> headers)
    {
        string? traceparent = null;
        string? tracestate = null;

        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            if (header.Key == TraceparentHeader)
            {
                traceparent = header.GetValueAsString();
            }
            else if (header.Key == TracestateHeader)
            {
                tracestate = header.GetValueAsString();
            }
        }

        if (traceparent is null)
            return null;

        return ParseTraceparent(traceparent, tracestate);
    }

    /// <summary>
    /// Parses a W3C traceparent header value into an <see cref="ActivityContext"/>.
    /// Format: {version}-{traceId}-{spanId}-{traceFlags}
    /// </summary>
    private static ActivityContext? ParseTraceparent(string traceparent, string? tracestate)
    {
        // Minimum length: "00-" + 32 (traceId) + "-" + 16 (spanId) + "-" + 2 (flags) = 55
        if (traceparent.Length < 55)
            return null;

        var span = traceparent.AsSpan();

        // Skip version prefix "00-"
        if (span[2] != '-')
            return null;

        var traceIdSpan = span.Slice(3, 32);
        if (span[35] != '-')
            return null;

        var spanIdSpan = span.Slice(36, 16);
        if (span[52] != '-')
            return null;

        var flagsSpan = span.Slice(53, 2);

        if (!TryParseTraceId(traceIdSpan, out var traceId))
            return null;

        if (!TryParseSpanId(spanIdSpan, out var spanId))
            return null;

        if (!TryParseHexByte(flagsSpan, out var flags))
            return null;

        return new ActivityContext(traceId, spanId, (ActivityTraceFlags)flags, tracestate, isRemote: true);
    }

    private static bool TryParseTraceId(ReadOnlySpan<char> chars, out ActivityTraceId result)
    {
        // Validate all characters are hex before calling CreateFromString
        for (var i = 0; i < chars.Length; i++)
        {
            if (HexCharToNibble(chars[i]) < 0)
            {
                result = default;
                return false;
            }
        }

        result = ActivityTraceId.CreateFromString(chars);
        return true;
    }

    private static bool TryParseSpanId(ReadOnlySpan<char> chars, out ActivitySpanId result)
    {
        // Validate all characters are hex before calling CreateFromString
        for (var i = 0; i < chars.Length; i++)
        {
            if (HexCharToNibble(chars[i]) < 0)
            {
                result = default;
                return false;
            }
        }

        result = ActivitySpanId.CreateFromString(chars);
        return true;
    }

    private static bool TryParseHexByte(ReadOnlySpan<char> hex, out byte result)
    {
        result = 0;
        if (hex.Length != 2)
            return false;

        var hi = HexCharToNibble(hex[0]);
        var lo = HexCharToNibble(hex[1]);

        if (hi < 0 || lo < 0)
            return false;

        result = (byte)((hi << 4) | lo);
        return true;
    }

    private static int HexCharToNibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };
}
