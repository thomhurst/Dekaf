namespace Dekaf.Protocol.Messages;

/// <summary>
/// EndTxn response (API key 26).
/// </summary>
public sealed class EndTxnResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.EndTxn;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 4;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 3;

        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new EndTxnResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode
        };
    }
}
