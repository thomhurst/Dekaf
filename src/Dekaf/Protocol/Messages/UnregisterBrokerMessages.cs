namespace Dekaf.Protocol.Messages;

/// <summary>
/// UnregisterBroker request (API key 64).
/// Unregisters a broker from KRaft metadata.
/// </summary>
public sealed class UnregisterBrokerRequest : IKafkaRequest<UnregisterBrokerResponse>
{
    public static ApiKey ApiKey => ApiKey.UnregisterBroker;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public int BrokerId { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(BrokerId);
        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// UnregisterBroker response (API key 64).
/// </summary>
public sealed class UnregisterBrokerResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.UnregisterBroker;
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

        return new UnregisterBrokerResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
