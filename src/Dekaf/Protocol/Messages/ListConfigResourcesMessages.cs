namespace Dekaf.Protocol.Messages;

/// <summary>
/// ListConfigResources request (API key 74, version 1).
/// </summary>
public sealed class ListConfigResourcesRequest : IKafkaRequest<ListConfigResourcesResponse>
{
    public static ApiKey ApiKey => ApiKey.ListClientMetricsResources;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// Configuration resource type filters. An empty list requests every supported type.
    /// </summary>
    public IReadOnlyList<sbyte> ResourceTypes { get; init; } = [];

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            ResourceTypes,
            static (ref KafkaProtocolWriter w, sbyte resourceType) => w.WriteInt8(resourceType));
        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// ListConfigResources response (API key 74, version 1).
/// </summary>
public sealed class ListConfigResourcesResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ListClientMetricsResources;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 1;

    public int ThrottleTimeMs { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public IReadOnlyList<ListConfigResource> ConfigResources { get; init; } = [];

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var configResources = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ListConfigResource.Read(ref r));

        reader.SkipTaggedFields();

        return new ListConfigResourcesResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ConfigResources = configResources
        };
    }
}

/// <summary>
/// A typed configuration resource returned by ListConfigResources.
/// </summary>
public sealed class ListConfigResource
{
    public required string Name { get; init; }
    public sbyte ResourceType { get; init; }

    public static ListConfigResource Read(ref KafkaProtocolReader reader)
    {
        var name = reader.ReadCompactNonNullableString();
        var resourceType = reader.ReadInt8();
        reader.SkipTaggedFields();

        return new ListConfigResource
        {
            Name = name,
            ResourceType = resourceType
        };
    }
}
