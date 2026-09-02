using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Dekaf.Internal;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Serialization;

internal static class HeaderProtocol
{
    private const int MaxCachedKeys = 128;
    private const int MaxCachedKeyBytes = 256;
    private static readonly Utf8StringInternCache s_keyCache = new(MaxCachedKeys, MaxCachedKeyBytes);

    [SkipLocalsInit]
    internal static void Write(in Header header, ref KafkaProtocolWriter writer)
    {
        WriteKey(ref writer, header.Key);

        if (header.IsValueNull)
        {
            writer.WriteVarInt(-1);
            return;
        }

        switch (header.DeferredValue)
        {
            case null:
                writer.WriteVarInt(header.RawValue.Length);
                writer.WriteRawBytes(header.RawValue.Span);
                break;
            case Activity activity:
                writer.WriteVarInt(Header.TraceparentLength);
                var traceparentDestination = writer.BufferWriter.GetSpan(Header.TraceparentLength);
                Diagnostics.TraceContextPropagator.WriteTraceparentUnchecked(activity, traceparentDestination);
                writer.BufferWriter.Advance(Header.TraceparentLength);
                writer.AddBytesWritten(Header.TraceparentLength);
                break;
            case string traceState:
                var traceStateLength = Encoding.UTF8.GetByteCount(traceState);
                writer.WriteVarInt(traceStateLength);
                var traceStateDestination = writer.BufferWriter.GetSpan(traceStateLength);
                Encoding.UTF8.GetBytes(traceState, traceStateDestination);
                writer.BufferWriter.Advance(traceStateLength);
                writer.AddBytesWritten(traceStateLength);
                break;
            default:
                throw new InvalidOperationException("Unsupported deferred header value.");
        }
    }

    private static void WriteKey(ref KafkaProtocolWriter writer, string key)
    {
        if (key.Length <= 128)
        {
            Span<byte> buffer = stackalloc byte[512];
            var actualBytes = Encoding.UTF8.GetBytes(key, buffer);
            writer.WriteVarInt(actualBytes);
            if (actualBytes > 0)
            {
                var outputSpan = writer.BufferWriter.GetSpan(actualBytes);
                buffer[..actualBytes].CopyTo(outputSpan);
                writer.BufferWriter.Advance(actualBytes);
                writer.AddBytesWritten(actualBytes);
            }

            return;
        }

        var keyByteCount = Encoding.UTF8.GetByteCount(key);
        writer.WriteVarInt(keyByteCount);
        if (keyByteCount == 0)
            return;

        var span = writer.BufferWriter.GetSpan(keyByteCount);
        Encoding.UTF8.GetBytes(key, span);
        writer.BufferWriter.Advance(keyByteCount);
        writer.AddBytesWritten(keyByteCount);
    }

    /// <summary>
    /// Reads a header whose key and value must lie within the enclosing record body ending at
    /// <paramref name="bodyEnd"/> (a reader offset). Bounding the slices here keeps a corrupt
    /// header length from interning or exposing bytes that belong to later records.
    /// </summary>
    internal static Header Read(ref KafkaProtocolReader reader, long bodyEnd)
    {
        var keyLength = reader.ReadVarInt();
        var key = s_keyCache.Intern(reader.ReadMemorySlice(keyLength, bodyEnd));

        var valueLength = reader.ReadVarInt();
        var isValueNull = valueLength < 0;
        var value = isValueNull ? ReadOnlyMemory<byte>.Empty : reader.ReadMemorySlice(valueLength, bodyEnd);

        return new Header(key, value, isNull: isValueNull);
    }

    [SkipLocalsInit]
    internal static void Encode(in Header header, Span<byte> destination, ref int offset)
    {
        var key = header.Key;
        if (key.Length <= 128)
        {
            Span<byte> buffer = stackalloc byte[512];
            var keyByteCount = Encoding.UTF8.GetBytes(key, buffer);
            Record.WriteVarInt(destination, ref offset, keyByteCount);
            buffer[..keyByteCount].CopyTo(destination[offset..]);
            offset += keyByteCount;
        }
        else
        {
            var keyByteCount = Encoding.UTF8.GetByteCount(key);
            Record.WriteVarInt(destination, ref offset, keyByteCount);
            Encoding.UTF8.GetBytes(key, destination[offset..]);
            offset += keyByteCount;
        }

        if (header.IsValueNull)
        {
            Record.WriteVarInt(destination, ref offset, -1);
            return;
        }

        var valueLength = header.DeferredValue switch
        {
            null => header.RawValue.Length,
            Activity => Header.TraceparentLength,
            string traceState => Encoding.UTF8.GetByteCount(traceState),
            _ => throw new InvalidOperationException("Unsupported deferred header value.")
        };
        Record.WriteVarInt(destination, ref offset, valueLength);
        switch (header.DeferredValue)
        {
            case null:
                header.RawValue.Span.CopyTo(destination[offset..]);
                break;
            case Activity activity:
                Diagnostics.TraceContextPropagator.WriteTraceparentUnchecked(
                    activity,
                    destination.Slice(offset, valueLength));
                break;
            case string traceState:
                Encoding.UTF8.GetBytes(traceState, destination[offset..]);
                break;
        }

        offset += valueLength;
    }

    internal static int CalculateSize(in Header header)
    {
        var keyBytes = Ascii.IsValid(header.Key)
            ? header.Key.Length
            : Encoding.UTF8.GetByteCount(header.Key);
        var size = Record.VarIntSize(keyBytes) + keyBytes;

        if (header.IsValueNull)
            return size + Record.VarIntSize(-1);

        var valueLength = header.DeferredValue switch
        {
            null => header.RawValue.Length,
            Activity => Header.TraceparentLength,
            string traceState => Encoding.UTF8.GetByteCount(traceState),
            _ => throw new InvalidOperationException("Unsupported deferred header value.")
        };
        return size + Record.VarIntSize(valueLength) + valueLength;
    }
}
