namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeConfigs request (API key 32).
/// Describes the configuration of the specified resources.
/// </summary>
public sealed class DescribeConfigsRequest : IKafkaRequest<DescribeConfigsResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeConfigs;
    public static short LowestSupportedVersion => 0;
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

    public static bool IsFlexibleVersion(short version) => version >= 4;
    public static short GetRequestHeaderVersion(short version) => version >= 4 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 4 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 4;

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Resources,
                (ref KafkaProtocolWriter w, DescribeConfigsResource r) => r.Write(ref w, version));
        }
        else
        {
            writer.WriteArray(
                Resources,
                (ref KafkaProtocolWriter w, DescribeConfigsResource r) => r.Write(ref w, version));
        }

        if (version >= 1)
        {
            writer.WriteBoolean(IncludeSynonyms);
        }

        if (version >= 3)
        {
            writer.WriteBoolean(IncludeDocumentation);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
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
        var isFlexible = version >= 4;

        writer.WriteInt8(ResourceType);

        if (isFlexible)
        {
            writer.WriteCompactString(ResourceName);
        }
        else
        {
            writer.WriteString(ResourceName);
        }

        if (isFlexible)
        {
            writer.WriteCompactNullableArray(
                ConfigurationKeys,
                (ref KafkaProtocolWriter w, string s) => w.WriteCompactString(s));
        }
        else
        {
            writer.WriteNullableArray(
                ConfigurationKeys,
                (ref KafkaProtocolWriter w, string s) => w.WriteString(s));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
