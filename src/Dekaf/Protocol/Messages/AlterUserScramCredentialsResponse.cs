namespace Dekaf.Protocol.Messages;

/// <summary>
/// AlterUserScramCredentials response (API key 51).
/// Contains the results of SCRAM credential alterations.
/// </summary>
public sealed class AlterUserScramCredentialsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.AlterUserScramCredentials;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The results for each alteration.
    /// </summary>
    public required IReadOnlyList<AlterUserScramCredentialsResult> Results { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();

        var results = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => AlterUserScramCredentialsResult.Read(ref r, version)) ?? [];

        reader.SkipTaggedFields();

        return new AlterUserScramCredentialsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Results = results
        };
    }
}

/// <summary>
/// Per-user result for AlterUserScramCredentials.
/// </summary>
public sealed class AlterUserScramCredentialsResult
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

    public static AlterUserScramCredentialsResult Read(ref KafkaProtocolReader reader, short version)
    {
        var user = reader.ReadCompactString() ?? string.Empty;
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new AlterUserScramCredentialsResult
        {
            User = user,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
