namespace Dekaf.Protocol.Messages;

/// <summary>
/// SaslAuthenticate response (API key 36).
/// Contains the result of a SASL authentication exchange.
/// </summary>
public sealed class SaslAuthenticateResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.SaslAuthenticate;
    public static short LowestSupportedVersion => 2;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The SASL authentication bytes from the server.
    /// </summary>
    public required byte[] AuthBytes { get; init; }

    /// <summary>
    /// The session lifetime in milliseconds (v1+).
    /// A value of 0 or greater indicates the session will not expire.
    /// </summary>
    public long SessionLifetimeMs { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();

        var errorMessage = reader.ReadCompactString();

        var authBytes = reader.ReadCompactBytes();

        var sessionLifetimeMs = reader.ReadInt64();

        reader.SkipTaggedFields();

        return new SaslAuthenticateResponse
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            AuthBytes = authBytes ?? [],
            SessionLifetimeMs = sessionLifetimeMs
        };
    }
}
