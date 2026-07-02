namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeConfigs request (API key 32).
/// Describes the configuration of the specified resources.
/// </summary>
public sealed class DescribeConfigsRequest : IKafkaRequest<DescribeConfigsResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeConfigs;
    public static short LowestSupportedVersion => 4;
    public static short HighestSupportedVersion => 4;

    /// <summary>
    /// The resources whose configurations we want to describe.
    /// </summary>
    public required IReadOnlyList<DescribeConfigsResource> Resources { get; init; }

    /// <summary>
    /// True if we should include all synonyms (v1+).
    /// </summary>
    public bool IncludeSynonyms { get; init; }

    /// <summary>
    /// True if we should include configuration documentation (v3+).
    /// </summary>
    public bool IncludeDocumentation { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            Resources,
            static (ref KafkaProtocolWriter w, DescribeConfigsResource r, short v) => r.Write(ref w, v),
            version);

        writer.WriteBoolean(IncludeSynonyms);

        writer.WriteBoolean(IncludeDocumentation);

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// A resource to describe configuration for.
/// </summary>
public sealed class DescribeConfigsResource
{
    /// <summary>
    /// The resource type (2=TOPIC, 4=BROKER, 8=BROKER_LOGGER).
    /// </summary>
    public required sbyte ResourceType { get; init; }

    /// <summary>
    /// The resource name.
    /// </summary>
    public required string ResourceName { get; init; }

    /// <summary>
    /// The configuration keys to list, or null to list all.
    /// </summary>
    public IReadOnlyList<string>? ConfigurationKeys { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt8(ResourceType);

        writer.WriteCompactString(ResourceName);

        writer.WriteCompactNullableArray(
            ConfigurationKeys,
            (ref KafkaProtocolWriter w, string s) => w.WriteCompactString(s));

        writer.WriteEmptyTaggedFields();
    }
}
