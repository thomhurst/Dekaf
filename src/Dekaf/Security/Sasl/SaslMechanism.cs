namespace Dekaf.Security.Sasl;

/// <summary>
/// SASL authentication mechanisms supported by the Kafka client.
/// </summary>
public enum SaslMechanism
{
    /// <summary>
    /// No SASL authentication.
    /// </summary>
    None = 0,

    /// <summary>
    /// SASL PLAIN mechanism. Simple username/password authentication.
    /// Credentials are sent in plain text, so TLS is strongly recommended.
    /// </summary>
    Plain,

    /// <summary>
    /// SASL SCRAM-SHA-256 mechanism. Challenge-response authentication
    /// using SHA-256 hashing. More secure than PLAIN.
    /// </summary>
    ScramSha256,

    /// <summary>
    /// SASL SCRAM-SHA-512 mechanism. Challenge-response authentication
    /// using SHA-512 hashing. More secure than SCRAM-SHA-256.
    /// </summary>
    ScramSha512
}

/// <summary>
/// Extension methods for <see cref="SaslMechanism"/>.
/// </summary>
public static class SaslMechanismExtensions
{
    /// <summary>
    /// Gets the Kafka protocol mechanism name.
    /// </summary>
    public static string ToProtocolName(this SaslMechanism mechanism) => mechanism switch
    {
        SaslMechanism.Plain => "PLAIN",
        SaslMechanism.ScramSha256 => "SCRAM-SHA-256",
        SaslMechanism.ScramSha512 => "SCRAM-SHA-512",
        _ => throw new ArgumentOutOfRangeException(nameof(mechanism), mechanism, "Unsupported SASL mechanism")
    };
}
