namespace Dekaf.Protocol.Messages;

/// <summary>
/// CreateAcls response (API key 30).
/// Contains the results of ACL creation requests.
/// </summary>
public sealed class CreateAclsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.CreateAcls;
    public static short LowestSupportedVersion => 0;
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
        var isFlexible = version >= 2;

        var throttleTimeMs = reader.ReadInt32();

        IReadOnlyList<AclCreationResult> results;
        if (isFlexible)
        {
            results = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => AclCreationResult.Read(ref r, version)) ?? [];
        }
        else
        {
            results = reader.ReadArray(
                (ref KafkaProtocolReader r) => AclCreationResult.Read(ref r, version)) ?? [];
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

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
        var isFlexible = version >= 2;

        var errorCode = (ErrorCode)reader.ReadInt16();

        string? errorMessage;
        if (isFlexible)
            errorMessage = reader.ReadCompactString();
        else
            errorMessage = reader.ReadString();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new AclCreationResult
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
