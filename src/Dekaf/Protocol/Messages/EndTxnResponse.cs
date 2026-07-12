namespace Dekaf.Protocol.Messages;

/// <summary>
/// EndTxn response (API key 26).
/// </summary>
public sealed class EndTxnResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.EndTxn;
    public static short LowestSupportedVersion => 3;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The bumped producer ID (v5+, KIP-890). -1 if not present.
    /// </summary>
    public long ProducerId { get; init; } = -1;

    /// <summary>
    /// The bumped producer epoch (v5+, KIP-890). -1 if not present.
    /// </summary>
    public short ProducerEpoch { get; init; } = -1;

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        var producerId = version >= 5 ? reader.ReadInt64() : -1L;
        var producerEpoch = version >= 5 ? reader.ReadInt16() : (short)-1;
        reader.SkipTaggedFields();

        return new EndTxnResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ProducerId = producerId,
            ProducerEpoch = producerEpoch
        };
    }
}
