namespace Dekaf.Protocol.Messages;

/// <summary>
/// AlterConfigs request (API key 33).
/// Alters the configuration of the specified resources.
/// This is a deprecated API - use IncrementalAlterConfigs instead.
/// </summary>
public sealed class AlterConfigsRequest : IKafkaRequest<AlterConfigsResponse>
{
    public static ApiKey ApiKey => ApiKey.AlterConfigs;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The updates for each resource.
    /// </summary>
    public required IReadOnlyList<AlterConfigsResource> Resources { get; init; }

    /// <summary>
    /// True if we should validate the request, but not change the configuration.
    /// </summary>
    public bool ValidateOnly { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 2;
    public static short GetRequestHeaderVersion(short version) => version >= 2 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 2 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Resources,
                static (ref KafkaProtocolWriter w, AlterConfigsResource r, short v) => r.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                Resources,
                static (ref KafkaProtocolWriter w, AlterConfigsResource r, short v) => r.Write(ref w, v),
                version);
        }

        writer.WriteBoolean(ValidateOnly);

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// A resource to alter configuration for.
/// </summary>
public sealed class AlterConfigsResource
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
    /// The configurations to set.
    /// </summary>
    public required IReadOnlyList<AlterableConfig> Configs { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        writer.WriteInt8(ResourceType);

        if (isFlexible)
        {
            writer.WriteCompactString(ResourceName);
            writer.WriteCompactArray(
                Configs,
                static (ref KafkaProtocolWriter w, AlterableConfig c, short v) => c.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteString(ResourceName);
            writer.WriteArray(
                Configs,
                static (ref KafkaProtocolWriter w, AlterableConfig c, short v) => c.Write(ref w, v),
                version);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// A configuration to alter.
/// </summary>
public sealed class AlterableConfig
{
    /// <summary>
    /// The configuration key name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The value to set for the configuration key.
    /// </summary>
    public string? Value { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        if (isFlexible)
        {
            writer.WriteCompactString(Name);
            writer.WriteCompactNullableString(Value);
        }
        else
        {
            writer.WriteString(Name);
            writer.WriteString(Value);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
