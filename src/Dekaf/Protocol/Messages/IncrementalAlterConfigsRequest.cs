namespace Dekaf.Protocol.Messages;

/// <summary>
/// IncrementalAlterConfigs request (API key 44).
/// Incrementally alters the configuration of the specified resources.
/// </summary>
public sealed class IncrementalAlterConfigsRequest : IKafkaRequest<IncrementalAlterConfigsResponse>
{
    public static ApiKey ApiKey => ApiKey.IncrementalAlterConfigs;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The incremental updates for each resource.
    /// </summary>
    public required IReadOnlyList<IncrementalAlterConfigsResource> Resources { get; init; }

    /// <summary>
    /// True if we should validate the request, but not change the configuration.
    /// </summary>
    public bool ValidateOnly { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 1;
    public static short GetRequestHeaderVersion(short version) => version >= 1 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 1 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 1;

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Resources,
                (ref KafkaProtocolWriter w, IncrementalAlterConfigsResource r) => r.Write(ref w, version));
        }
        else
        {
            writer.WriteArray(
                Resources,
                (ref KafkaProtocolWriter w, IncrementalAlterConfigsResource r) => r.Write(ref w, version));
        }

        writer.WriteBoolean(ValidateOnly);

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// A resource for incremental config alteration.
/// </summary>
public sealed class IncrementalAlterConfigsResource
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
    /// The configurations to alter.
    /// </summary>
    public required IReadOnlyList<IncrementalAlterableConfig> Configs { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 1;

        writer.WriteInt8(ResourceType);

        if (isFlexible)
        {
            writer.WriteCompactString(ResourceName);
            writer.WriteCompactArray(
                Configs,
                (ref KafkaProtocolWriter w, IncrementalAlterableConfig c) => c.Write(ref w, version));
        }
        else
        {
            writer.WriteString(ResourceName);
            writer.WriteArray(
                Configs,
                (ref KafkaProtocolWriter w, IncrementalAlterableConfig c) => c.Write(ref w, version));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// A configuration to incrementally alter.
/// </summary>
public sealed class IncrementalAlterableConfig
{
    /// <summary>
    /// The configuration key name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The type (Set, Delete, Append, Subtract) of operation.
    /// </summary>
    public sbyte ConfigOperation { get; init; }

    /// <summary>
    /// The value to set for the configuration key.
    /// </summary>
    public string? Value { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 1;

        if (isFlexible)
        {
            writer.WriteCompactString(Name);
        }
        else
        {
            writer.WriteString(Name);
        }

        writer.WriteInt8(ConfigOperation);

        if (isFlexible)
        {
            writer.WriteCompactNullableString(Value);
        }
        else
        {
            writer.WriteString(Value);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
