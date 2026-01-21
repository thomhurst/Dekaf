namespace Dekaf.Protocol.Messages;

/// <summary>
/// SaslHandshake response (API key 17).
/// Contains the list of SASL mechanisms supported by the broker.
/// </summary>
public sealed class SaslHandshakeResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.SaslHandshake;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The SASL mechanisms supported by the broker.
    /// </summary>
    public required IReadOnlyList<string> Mechanisms { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();
        var mechanisms = reader.ReadArray((ref KafkaProtocolReader r) => r.ReadString()!);

        return new SaslHandshakeResponse
        {
            ErrorCode = errorCode,
            Mechanisms = mechanisms
        };
    }
}
