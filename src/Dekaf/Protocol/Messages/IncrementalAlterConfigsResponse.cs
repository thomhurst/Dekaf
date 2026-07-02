namespace Dekaf.Protocol.Messages;

/// <summary>
/// IncrementalAlterConfigs response (API key 44).
/// Contains the results of incrementally altering the configuration for resources.
/// </summary>
public sealed class IncrementalAlterConfigsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.IncrementalAlterConfigs;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The responses for each resource.
    /// </summary>
    public required IReadOnlyList<IncrementalAlterConfigsResourceResponse> Responses { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();

        IReadOnlyList<IncrementalAlterConfigsResourceResponse> responses;
        responses = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => IncrementalAlterConfigsResourceResponse.Read(ref r, version)) ?? [];

        reader.SkipTaggedFields();

        return new IncrementalAlterConfigsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Responses = responses
        };
    }
}

/// <summary>
/// Per-resource response for IncrementalAlterConfigs.
/// </summary>
public sealed class IncrementalAlterConfigsResourceResponse
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

    public static IncrementalAlterConfigsResourceResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();

        string? errorMessage;
        errorMessage = reader.ReadCompactString();

        var resourceType = reader.ReadInt8();

        string resourceName;
        resourceName = reader.ReadCompactString() ?? string.Empty;

        reader.SkipTaggedFields();

        return new IncrementalAlterConfigsResourceResponse
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ResourceType = resourceType,
            ResourceName = resourceName
        };
    }
}
