namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeCluster request (API key 60).
/// Version 1 supports selecting broker or controller endpoints (KIP-919).
/// </summary>
public sealed class DescribeClusterRequest : IKafkaRequest<DescribeClusterResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeCluster;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    public bool IncludeClusterAuthorizedOperations { get; init; }
    public DescribeClusterEndpointType EndpointType { get; init; } = DescribeClusterEndpointType.Broker;
    public bool IncludeFencedBrokers { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteBoolean(IncludeClusterAuthorizedOperations);
        if (version >= 1)
            writer.WriteInt8((sbyte)EndpointType);
        if (version >= 2)
            writer.WriteBoolean(IncludeFencedBrokers);
        writer.WriteEmptyTaggedFields();
    }
}

public enum DescribeClusterEndpointType : sbyte
{
    Broker = 1,
    Controller = 2
}
