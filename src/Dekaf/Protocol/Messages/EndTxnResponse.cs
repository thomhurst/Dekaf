namespace Dekaf.Protocol.Messages;

/// <summary>
/// EndTxn response (API key 26).
/// </summary>
public sealed class EndTxnResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.EndTxn;
    public static short LowestSupportedVersion => 3;
    public static short HighestSupportedVersion => 4;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The bumped producer ID (v4+, KIP-890). -1 if not present.
    /// </summary>
    public long ProducerId { get; init; } = -1;

    /// <summary>
    /// The bumped producer epoch (v4+, KIP-890). -1 if not present.
    /// </summary>
    public short ProducerEpoch { get; init; } = -1;

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 3;

        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        var producerId = -1L;
        var producerEpoch = (short)-1;

        if (isFlexible)
        {
            var numTaggedFields = reader.ReadUnsignedVarInt();
            for (var i = 0; i < numTaggedFields; i++)
            {
                var tag = reader.ReadUnsignedVarInt();
                var size = reader.ReadUnsignedVarInt();
                switch (tag)
                {
                    case 0:
                        producerId = reader.ReadInt64();
                        break;
                    case 1:
                        producerEpoch = reader.ReadInt16();
                        break;
                    default:
                        reader.Skip(size);
                        break;
                }
            }
        }

        return new EndTxnResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ProducerId = producerId,
            ProducerEpoch = producerEpoch
        };
    }
}
