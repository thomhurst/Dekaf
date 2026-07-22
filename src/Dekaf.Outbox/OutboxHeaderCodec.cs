using System.Buffers.Binary;
using System.Text;
using Dekaf.Serialization;

namespace Dekaf.Outbox;

/// <summary>
/// Serializes record headers into a single binary column so any store can persist them
/// without a child table.
/// </summary>
/// <remarks>
/// Format version 1 (all integers little-endian):
/// <c>[byte version=1] [int32 count] { [int32 keyByteLength] [key utf8] [int32 valueLength or -1 for null] [value] }*</c>.
/// Rows outlive deployments, so the leading version byte is what allows the encoding to
/// evolve without misparsing in-flight rows during an upgrade.
/// </remarks>
public static class OutboxHeaderCodec
{
    private const byte FormatVersion = 1;

    /// <summary>
    /// Encodes headers to a binary blob. Returns null for null or empty headers.
    /// </summary>
    public static byte[]? Encode(Headers? headers)
    {
        if (headers is null || headers.Count == 0)
            return null;

        var size = 5;
        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            size += 8 + Encoding.UTF8.GetByteCount(header.Key) + (header.IsValueNull ? 0 : header.Value.Length);
        }

        var buffer = new byte[size];
        var span = buffer.AsSpan();
        span[0] = FormatVersion;
        BinaryPrimitives.WriteInt32LittleEndian(span[1..], headers.Count);
        var offset = 5;

        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            var keyLength = Encoding.UTF8.GetBytes(header.Key, span[(offset + 4)..]);
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], keyLength);
            offset += 4 + keyLength;

            if (header.IsValueNull)
            {
                BinaryPrimitives.WriteInt32LittleEndian(span[offset..], -1);
                offset += 4;
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(span[offset..], header.Value.Length);
                offset += 4;
                header.Value.Span.CopyTo(span[offset..]);
                offset += header.Value.Length;
            }
        }

        return buffer;
    }

    /// <summary>
    /// Decodes a blob produced by <see cref="Encode"/>. Returns null for a null blob.
    /// </summary>
    public static Headers? Decode(byte[]? data)
    {
        if (data is null)
            return null;

        var span = data.AsSpan();
        if (span[0] != FormatVersion)
            throw new FormatException($"Unknown outbox header blob version {span[0]}; expected {FormatVersion}.");

        var count = BinaryPrimitives.ReadInt32LittleEndian(span[1..]);
        if (count < 0)
            throw new FormatException("Outbox header blob has a negative header count.");

        var headers = new Headers(count);
        var offset = 5;

        for (var i = 0; i < count; i++)
        {
            var keyLength = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]);
            offset += 4;
            var key = Encoding.UTF8.GetString(span.Slice(offset, keyLength));
            offset += keyLength;

            var valueLength = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]);
            offset += 4;
            if (valueLength < 0)
            {
                headers.Add(key, (byte[]?)null);
            }
            else
            {
                headers.Add(key, span.Slice(offset, valueLength).ToArray());
                offset += valueLength;
            }
        }

        return headers;
    }
}
