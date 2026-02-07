namespace Dekaf.Protocol.Messages;

/// <summary>
/// InitProducerId response (API key 22).
/// Contains the allocated producer ID and epoch.
/// </summary>
public sealed class InitProducerIdResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.InitProducerId;
    public static short LowestSupportedVersion => 0;
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
    /// The producer ID.
    /// </summary>
    public long ProducerId { get; init; } = -1;

    /// <summary>
    /// The producer epoch.
    /// </summary>
    public short ProducerEpoch { get; init; } = -1;

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new InitProducerIdResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ProducerId = producerId,
            ProducerEpoch = producerEpoch
        };
    }
}
