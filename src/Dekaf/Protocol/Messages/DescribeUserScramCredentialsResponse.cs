namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeUserScramCredentials response (API key 50).
/// Contains the SCRAM credential descriptions.
/// </summary>
public sealed class DescribeUserScramCredentialsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeUserScramCredentials;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The message-level error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The message-level error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The results for all users.
    /// </summary>
    public required IReadOnlyList<DescribeUserScramCredentialsResult> Results { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        var results = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => DescribeUserScramCredentialsResult.Read(ref r, version)) ?? [];

        reader.SkipTaggedFields();

        return new DescribeUserScramCredentialsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Results = results
        };
    }
}

/// <summary>
/// Per-user result for DescribeUserScramCredentials.
/// </summary>
public sealed class DescribeUserScramCredentialsResult
{
    /// <summary>
    /// The user name.
    /// </summary>
    public required string User { get; init; }

    /// <summary>
    /// The error code for this user, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message for this user, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The SCRAM credentials for this user.
    /// </summary>
    public required IReadOnlyList<CredentialInfo> CredentialInfos { get; init; }

    public static DescribeUserScramCredentialsResult Read(ref KafkaProtocolReader reader, short version)
    {
        var user = reader.ReadCompactString() ?? string.Empty;
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        var credentialInfos = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => CredentialInfo.Read(ref r, version)) ?? [];

        reader.SkipTaggedFields();

        return new DescribeUserScramCredentialsResult
        {
            User = user,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            CredentialInfos = credentialInfos
        };
    }
}

/// <summary>
/// SCRAM credential information from DescribeUserScramCredentials response.
/// </summary>
public sealed class CredentialInfo
{
    /// <summary>
    /// The SCRAM mechanism (1 = SCRAM-SHA-256, 2 = SCRAM-SHA-512).
    /// </summary>
    public byte Mechanism { get; init; }

    /// <summary>
    /// The number of iterations used in the credential.
    /// </summary>
    public int Iterations { get; init; }

    public static CredentialInfo Read(ref KafkaProtocolReader reader, short version)
    {
        var mechanism = reader.ReadUInt8();
        var iterations = reader.ReadInt32();

        reader.SkipTaggedFields();

        return new CredentialInfo
        {
            Mechanism = mechanism,
            Iterations = iterations
        };
    }
}
