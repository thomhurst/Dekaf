namespace Dekaf.Protocol.Messages;

/// <summary>
/// Heartbeat response (API key 12).
/// </summary>
public sealed class HeartbeatResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.Heartbeat;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 4;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Error code.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 4;

        var throttleTimeMs = version >= 1 ? reader.ReadInt32() : 0;
        var errorCode = (ErrorCode)reader.ReadInt16();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new HeartbeatResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode
        };
    }
}
