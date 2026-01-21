namespace Dekaf.Protocol.Messages;

/// <summary>
/// SaslAuthenticate request (API key 36).
/// Used to perform SASL authentication exchanges.
/// </summary>
public sealed class SaslAuthenticateRequest : IKafkaRequest<SaslAuthenticateResponse>
{
    public static ApiKey ApiKey => ApiKey.SaslAuthenticate;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The SASL authentication bytes from the client.
    /// </summary>
    public required byte[] AuthBytes { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 2;
    public static short GetRequestHeaderVersion(short version) => version >= 2 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 2 ? (short)1 : (short)0;

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
