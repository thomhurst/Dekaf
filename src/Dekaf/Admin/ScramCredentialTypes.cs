namespace Dekaf.Admin;

/// <summary>
/// SCRAM mechanism types supported by Kafka.
/// </summary>
public enum ScramMechanism : byte
{
    /// <summary>
    /// Unknown mechanism.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// SCRAM-SHA-256 mechanism.
    /// </summary>
    ScramSha256 = 1,

    /// <summary>
    /// SCRAM-SHA-512 mechanism.
    /// </summary>
    ScramSha512 = 2
}

/// <summary>
/// Information about a SCRAM credential for a user.
/// </summary>
public sealed class ScramCredentialInfo
{
    /// <summary>
    /// The SCRAM mechanism.
    /// </summary>
    public required ScramMechanism Mechanism { get; init; }

    /// <summary>
    /// The number of iterations used in the credential.
    /// </summary>
    public required int Iterations { get; init; }
}

/// <summary>
/// Base class for SCRAM credential alterations.
/// </summary>
public abstract class UserScramCredentialAlteration
{
    /// <summary>
    /// The user whose credentials are being altered.
    /// </summary>
    public required string User { get; init; }
}

/// <summary>
/// Represents an upsertion (create or update) of a SCRAM credential.
/// </summary>
public sealed class UserScramCredentialUpsertion : UserScramCredentialAlteration
{
    /// <summary>
    /// The SCRAM mechanism to use.
    /// </summary>
    public required ScramMechanism Mechanism { get; init; }

    /// <summary>
    /// The number of iterations to use (must be at least 4096).
    /// </summary>
    public required int Iterations { get; init; }

    /// <summary>
    /// The password for the credential.
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// Optional salt to use. If not provided, a random salt will be generated.
    /// </summary>
    public byte[]? Salt { get; init; }
}

/// <summary>
/// Represents a deletion of a SCRAM credential.
/// </summary>
public sealed class UserScramCredentialDeletion : UserScramCredentialAlteration
{
    /// <summary>
    /// The SCRAM mechanism to delete.
    /// </summary>
    public required ScramMechanism Mechanism { get; init; }
}

/// <summary>
/// Options for DescribeUserScramCredentials.
/// </summary>
public sealed class DescribeUserScramCredentialsOptions
{
    /// <summary>
    /// How long to wait in milliseconds before timing out the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Options for AlterUserScramCredentials.
/// </summary>
public sealed class AlterUserScramCredentialsOptions
{
    /// <summary>
    /// How long to wait in milliseconds before timing out the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;
}
