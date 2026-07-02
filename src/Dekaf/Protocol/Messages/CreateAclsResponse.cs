namespace Dekaf.Protocol.Messages;

/// <summary>
/// CreateAcls response (API key 30).
/// Contains the results of ACL creation requests.
/// </summary>
public sealed class CreateAclsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.CreateAcls;
    public static short LowestSupportedVersion => 2;
    public static short HighestSupportedVersion => 3;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The results for each ACL creation.
    /// </summary>
    public required IReadOnlyList<AclCreationResult> Results { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();

        IReadOnlyList<AclCreationResult> results;
        results = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => AclCreationResult.Read(ref r, version)) ?? [];

        reader.SkipTaggedFields();

        return new CreateAclsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Results = results
        };
    }
}

/// <summary>
/// The result of an ACL creation.
/// </summary>
public sealed class AclCreationResult
{
    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static AclCreationResult Read(ref KafkaProtocolReader reader, short version)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();

        string? errorMessage;
        errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new AclCreationResult
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
