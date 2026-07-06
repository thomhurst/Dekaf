namespace Dekaf.Protocol.Messages;

/// <summary>
/// ListClientMetricsResources request (API key 74).
/// Lists client metrics configuration resources available in the cluster.
/// </summary>
public sealed class ListClientMetricsResourcesRequest : IKafkaRequest<ListClientMetricsResourcesResponse>
{
    public static ApiKey ApiKey => ApiKey.ListClientMetricsResources;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// ListClientMetricsResources response (API key 74).
/// </summary>
public sealed class ListClientMetricsResourcesResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ListClientMetricsResources;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public int ThrottleTimeMs { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public IReadOnlyList<ListClientMetricsResource> ClientMetricsResources { get; init; } = [];

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var clientMetricsResources = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ListClientMetricsResource.Read(ref r));

        reader.SkipTaggedFields();

        return new ListClientMetricsResourcesResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ClientMetricsResources = clientMetricsResources
        };
    }
}

/// <summary>
/// A client metrics configuration resource returned by ListClientMetricsResources.
/// </summary>
public sealed class ListClientMetricsResource
{
    public required string Name { get; init; }

    public static ListClientMetricsResource Read(ref KafkaProtocolReader reader)
    {
        var name = reader.ReadCompactNonNullableString();
        reader.SkipTaggedFields();

        return new ListClientMetricsResource
        {
            Name = name
        };
    }
}
