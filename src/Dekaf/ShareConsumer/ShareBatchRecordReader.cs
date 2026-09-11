using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.ShareConsumer;

/// <summary>
/// Reads borrowed record fields without materializing header strings or renting one
/// header array per record. The enclosing batch owns every returned memory slice.
/// </summary>
internal static class ShareBatchRecordReader
{
    internal static ShareBatchRecordData Read(ref KafkaProtocolReader reader)
    {
        var length = reader.ReadVarInt();
        if (length < 0)
            throw new MalformedProtocolDataException($"Invalid record length {length}");

        // Bound the whole body before reading fields. An incomplete body is a truncated
        // tail; an invalid field inside a complete body is protocol corruption.
        if (length > reader.Remaining)
            throw new InsufficientDataException();
        var body = reader.ReadMemorySlice(length);
        var bodyReader = new KafkaProtocolReader(body);
        try
        {
            _ = bodyReader.ReadInt8();
            var timestampDelta = bodyReader.ReadVarLong();
            var offsetDelta = bodyReader.ReadVarInt();
            var keyLength = bodyReader.ReadVarInt();
            var key = keyLength < 0 ? ReadOnlyMemory<byte>.Empty : bodyReader.ReadMemorySlice(keyLength);
            var valueLength = bodyReader.ReadVarInt();
            var value = valueLength < 0 ? ReadOnlyMemory<byte>.Empty : bodyReader.ReadMemorySlice(valueLength);
            var headerCount = bodyReader.ReadVarInt();
            if (headerCount < 0 || headerCount > Record.MaxReasonableHeaderCount
                || headerCount > bodyReader.Remaining / 2)
                throw new MalformedProtocolDataException($"Invalid record header count {headerCount}");

            var headerBytes = body[(int)bodyReader.Consumed..];
            for (var index = 0; index < headerCount; index++)
            {
                var headerKeyLength = bodyReader.ReadVarInt();
                if (headerKeyLength < 0)
                    throw new MalformedProtocolDataException("Record header keys cannot be null");
                bodyReader.Skip(headerKeyLength);
                var headerValueLength = bodyReader.ReadVarInt();
                if (headerValueLength >= 0)
                    bodyReader.Skip(headerValueLength);
            }

            if (!bodyReader.End)
                throw new MalformedProtocolDataException("Record body has trailing bytes");

            return new ShareBatchRecordData(
                offsetDelta, timestampDelta, key, value, keyLength < 0, valueLength < 0,
                headerBytes, headerCount);
        }
        catch (InsufficientDataException exception)
        {
            throw new MalformedProtocolDataException(
                "Record body cannot be parsed within its declared length", exception);
        }
    }
}

internal readonly record struct ShareBatchRecordData(
    int OffsetDelta,
    long TimestampDelta,
    ReadOnlyMemory<byte> Key,
    ReadOnlyMemory<byte> Value,
    bool IsKeyNull,
    bool IsValueNull,
    ReadOnlyMemory<byte> HeaderBytes,
    int HeaderCount);
