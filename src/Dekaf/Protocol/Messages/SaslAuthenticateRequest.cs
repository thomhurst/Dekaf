namespace Dekaf.Protocol.Messages;

/// <summary>
/// SaslAuthenticate request (API key 36).
/// Used to perform SASL authentication exchanges.
/// </summary>
public sealed class SaslAuthenticateRequest : IKafkaRequest<SaslAuthenticateResponse>
{
    public static ApiKey ApiKey => ApiKey.SaslAuthenticate;
    public static short LowestSupportedVersion => 2;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The SASL authentication bytes from the client.
    /// </summary>
    public required byte[] AuthBytes { get; init; }

    public static bool IsFlexibleVersion(short version) => true;
    public static short GetRequestHeaderVersion(short version) => 2;
    public static short GetResponseHeaderVersion(short version) => 1;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (version >= 2)
        {
            writer.WriteCompactBytes(AuthBytes);
            writer.WriteEmptyTaggedFields();
        }
        else
        {
            writer.WriteBytes(AuthBytes);
        }
    }
}
