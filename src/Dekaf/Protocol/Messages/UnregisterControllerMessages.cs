namespace Dekaf.Protocol.Messages;

/// <summary>
/// UnregisterController request (API key 94, KIP-1312).
/// Removes a controller registration from KRaft metadata. It does not change Raft voters.
/// </summary>
public sealed class UnregisterControllerRequest : IKafkaRequest<UnregisterControllerResponse>
{
    public static ApiKey ApiKey => ApiKey.UnregisterController;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public int ControllerId { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(ControllerId);
        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// UnregisterController response (API key 94, KIP-1312).
/// </summary>
public sealed class UnregisterControllerResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.UnregisterController;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public int ThrottleTimeMs { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new UnregisterControllerResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
