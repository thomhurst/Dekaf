namespace Dekaf.Protocol.Messages;

/// <summary>
/// PushTelemetry response (API key 72).
/// Contains the broker result for a telemetry upload.
/// </summary>
public sealed class PushTelemetryResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.PushTelemetry;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The top-level error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        reader.SkipTaggedFields();

        return new PushTelemetryResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode
        };
    }
}
