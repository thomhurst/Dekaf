using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
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
    internal const int TraceparentLength = Header.TraceparentLength;

    static TraceContextPropagator()
    {
        Header.ConfigureDeferredTraceparentWriter(
            static (value, destination) => WriteTraceparentUnchecked((Activity)value, destination));
    }

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

        headers.AddDeferredTraceContext(activity, activity.TraceStateString);

        return headers;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteTraceparent(Activity activity, Span<byte> destination)
    {
        if (destination.Length < TraceparentLength)
            throw new ArgumentException("The traceparent destination must be at least 55 bytes.", nameof(destination));

        WriteTraceparentUnchecked(activity, destination);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteTraceparentUnchecked(Activity activity, Span<byte> destination)
    {
        destination[0] = (byte)'0';
        destination[1] = (byte)'0';
        destination[2] = (byte)'-';

        Span<byte> traceId = stackalloc byte[16];
        activity.TraceId.CopyTo(traceId);
        WriteLowerHex(traceId, destination.Slice(3, 32));
        destination[35] = (byte)'-';

        Span<byte> spanId = stackalloc byte[8];
        activity.SpanId.CopyTo(spanId);
        WriteLowerHex(spanId, destination.Slice(36, 16));
        destination[52] = (byte)'-';
        destination[53] = (byte)'0';
        destination[54] = activity.Recorded ? (byte)'1' : (byte)'0';
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteLowerHex(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        const string HexDigits = "0123456789abcdef";
        for (var i = 0; i < source.Length; i++)
        {
            var value = source[i];
            destination[i * 2] = (byte)HexDigits[value >> 4];
            destination[(i * 2) + 1] = (byte)HexDigits[value & 0x0f];
        }
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

#if NET10_0_OR_GREATER
    [SkipLocalsInit] // Every byte/character in the consumed stack slices is written first.
#endif
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe ActivityContext? ExtractTraceContextSlow(IReadOnlyList<Header> headers)
    {
        Header? traceparent = null;
        Header? tracestate = null;

        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            if (header.Key == TraceparentHeader)
            {
                traceparent = header;
            }
            else if (header.Key == TracestateHeader)
            {
                tracestate = header;
            }
        }

        if (traceparent is not { IsValueNull: false } parent)
            return null;

        if (parent.DeferredValue is string text)
            return ParseTraceparent(text.AsSpan(), tracestate);

        // Only the fixed prefix and its extension delimiter are understood. Avoid decoding
        // the whole UTF-8 header, including arbitrarily large unknown future-version fields.
        Span<byte> deferred = stackalloc byte[TraceparentLength];
        scoped ReadOnlySpan<byte> bytes = parent.RawValue.Span;
        if (parent.DeferredValue is Activity activity)
        {
            WriteTraceparentUnchecked(activity, deferred);
            bytes = deferred;
        }

        Span<char> prefix = stackalloc char[TraceparentLength + 1];
        var length = Math.Min(bytes.Length, prefix.Length);
        if (length < TraceparentLength)
            return null;

        // The pointer overload also supports netstandard2.0 without an intermediate array.
        fixed (byte* source = bytes)
        fixed (char* destination = prefix)
            Encoding.ASCII.GetChars(source, length, destination, length);

        return ParseTraceparent(prefix[..length], tracestate);
    }

    /// <summary>
    /// Parses a W3C traceparent header value into an <see cref="ActivityContext"/>.
    /// Format: {version}-{traceId}-{spanId}-{traceFlags}
    /// </summary>
#if NET10_0_OR_GREATER
    [SkipLocalsInit] // IDs are consumed only after both decoders fill their entire destination.
#endif
    private static ActivityContext? ParseTraceparent(ReadOnlySpan<char> span, Header? tracestate)
    {
        // Minimum length: "00-" + 32 (traceId) + "-" + 16 (spanId) + "-" + 2 (flags) = 55
        if (span.Length < TraceparentLength)
            return null;

        if (span[2] != '-' || span[35] != '-' || span[52] != '-')
            return null;

        if (!TryParseHexByte(span[..2], out var version) || version == 0xff)
            return null;

        // Version 00 has exactly 55 characters. Higher versions may append opaque fields
        // after a dash; W3C requires readers not to interpret those unknown fields.
        if (span.Length > TraceparentLength && (version == 0 || span[TraceparentLength] != '-'))
            return null;

        if (!TryParseHexByte(span.Slice(53, 2), out var flags))
            return null;

        Span<byte> traceId = stackalloc byte[16];
        Span<byte> spanId = stackalloc byte[8];
        if (!TryDecodeIdentifier(span.Slice(3, 32), traceId) ||
            !TryDecodeIdentifier(span.Slice(36, 16), spanId))
            return null;

        // Validate and decode once before the platform APIs materialize their owned strings.
        return new ActivityContext(
            ActivityTraceId.CreateFromBytes(traceId),
            ActivitySpanId.CreateFromBytes(spanId),
            (ActivityTraceFlags)flags,
            tracestate?.GetValueAsString(),
            isRemote: true);
    }

    private static bool TryDecodeIdentifier(ReadOnlySpan<char> chars, Span<byte> bytes)
    {
        var nonzero = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            var hi = HexCharToNibble(chars[i * 2]);
            var lo = HexCharToNibble(chars[(i * 2) + 1]);
            if ((hi | lo) < 0)
                return false;

            var value = (hi << 4) | lo;
            bytes[i] = (byte)value;
            nonzero |= value;
        }
        return nonzero != 0;
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
        _ => -1
    };
}
