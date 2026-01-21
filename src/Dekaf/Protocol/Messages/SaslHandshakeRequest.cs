namespace Dekaf.Protocol.Messages;

/// <summary>
/// SaslHandshake request (API key 17).
/// Used to initiate SASL authentication and negotiate the mechanism.
/// </summary>
public sealed class SaslHandshakeRequest : IKafkaRequest<SaslHandshakeResponse>
{
    public static ApiKey ApiKey => ApiKey.SaslHandshake;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The SASL mechanism to use (e.g., "PLAIN", "SCRAM-SHA-256", "SCRAM-SHA-512").
    /// </summary>
    public required string Mechanism { get; init; }

    public static bool IsFlexibleVersion(short version) => false;
    public static short GetRequestHeaderVersion(short version) => 1;
    public static short GetResponseHeaderVersion(short version) => 0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteString(Mechanism);
    }
}
