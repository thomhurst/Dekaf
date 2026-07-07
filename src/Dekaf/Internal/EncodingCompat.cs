using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dekaf.Internal;

/// <summary>
/// UTF-8 span helpers that behave identically across all target frameworks.
/// </summary>
/// <remarks>
/// On <c>netstandard2.0</c> the span-based <see cref="Encoding.GetBytes(ReadOnlySpan{char}, Span{byte})"/>
/// and <c>Encoding.GetByteCount(ReadOnlySpan&lt;char&gt;)</c> overloads are supplied by the Polyfill package,
/// whose implementation pins the source span with <c>fixed</c>. Pinning an <b>empty</b> span yields a null
/// pointer, which <see cref="UTF8Encoding"/> rejects with an <see cref="ArgumentNullException"/>
/// ("Array cannot be null. (Parameter 'chars')"). These helpers short-circuit empty input so the
/// netstandard2.0 build matches the net10.0 behaviour (zero bytes). On net8.0+/netstandard2.1 the native,
/// empty-safe overloads are used and the guard is compiled out, so those builds gain no extra branch.
/// </remarks>
internal static class EncodingCompat
{
    /// <summary>UTF-8 encodes <paramref name="chars"/> into <paramref name="bytes"/>; empty-safe on all TFMs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBytes(ReadOnlySpan<char> chars, Span<byte> bytes)
    {
#if NETSTANDARD2_0
        if (chars.IsEmpty)
            return 0;
#endif
        return Encoding.UTF8.GetBytes(chars, bytes);
    }

    /// <summary>Counts the UTF-8 bytes for <paramref name="chars"/>; empty-safe on all TFMs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetByteCount(ReadOnlySpan<char> chars)
    {
#if NETSTANDARD2_0
        if (chars.IsEmpty)
            return 0;
#endif
        return Encoding.UTF8.GetByteCount(chars);
    }

    /// <summary>UTF-8 decodes <paramref name="bytes"/> into a string; empty-safe on all TFMs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetString(ReadOnlySpan<byte> bytes)
    {
#if NETSTANDARD2_0
        if (bytes.IsEmpty)
            return string.Empty;
#endif
        return Encoding.UTF8.GetString(bytes);
    }
}
