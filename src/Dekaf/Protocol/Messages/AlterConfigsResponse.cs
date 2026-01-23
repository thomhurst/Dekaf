namespace Dekaf.Protocol.Messages;

/// <summary>
/// AlterConfigs response (API key 33).
/// Contains the results of altering the configuration for resources.
/// </summary>
public sealed class AlterConfigsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.AlterConfigs;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The responses for each resource.
    /// </summary>
    public required IReadOnlyList<AlterConfigsResourceResponse> Responses { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var throttleTimeMs = reader.ReadInt32();

        IReadOnlyList<AlterConfigsResourceResponse> responses;
        if (isFlexible)
        {
            responses = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => AlterConfigsResourceResponse.Read(ref r, version)) ?? [];
        }
        else
        {
            responses = reader.ReadArray(
                (ref KafkaProtocolReader r) => AlterConfigsResourceResponse.Read(ref r, version)) ?? [];
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new AlterConfigsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Responses = responses
        };
    }
}

/// <summary>
/// Per-resource response for AlterConfigs.
/// </summary>
public sealed class AlterConfigsResourceResponse
{
    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The resource type.
    /// </summary>
    public sbyte ResourceType { get; init; }

    /// <summary>
    /// The resource name.
    /// </summary>
    public required string ResourceName { get; init; }

    public static AlterConfigsResourceResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var errorCode = (ErrorCode)reader.ReadInt16();

        string? errorMessage;
        if (isFlexible)
        {
            errorMessage = reader.ReadCompactString();
        }
        else
        {
            errorMessage = reader.ReadString();
        }

        var resourceType = reader.ReadInt8();

        string resourceName;
        if (isFlexible)
        {
            resourceName = reader.ReadCompactString() ?? string.Empty;
        }
        else
        {
            resourceName = reader.ReadString() ?? string.Empty;
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new AlterConfigsResourceResponse
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ResourceType = resourceType,
            ResourceName = resourceName
        };
    }
}
