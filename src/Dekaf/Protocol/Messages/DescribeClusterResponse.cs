namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeCluster response (API key 60).
/// </summary>
public sealed class DescribeClusterResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeCluster;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    public int ThrottleTimeMs { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DescribeClusterEndpointType EndpointType { get; init; } = DescribeClusterEndpointType.Broker;
    public required string ClusterId { get; init; }
    public int ControllerId { get; init; } = -1;
    public required IReadOnlyList<DescribeClusterNode> Nodes { get; init; }
    public int ClusterAuthorizedOperations { get; init; } = int.MinValue;

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();
        var endpointType = version >= 1
            ? (DescribeClusterEndpointType)reader.ReadInt8()
            : DescribeClusterEndpointType.Broker;
        var clusterId = reader.ReadCompactString() ?? string.Empty;
        var controllerId = reader.ReadInt32();
        var nodes = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => DescribeClusterNode.Read(ref r, v),
            version);
        var clusterAuthorizedOperations = reader.ReadInt32();
        reader.SkipTaggedFields();

        return new DescribeClusterResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            EndpointType = endpointType,
            ClusterId = clusterId,
            ControllerId = controllerId,
            Nodes = nodes,
            ClusterAuthorizedOperations = clusterAuthorizedOperations
        };
    }
}

public sealed class DescribeClusterNode
{
    public int NodeId { get; init; }
    public required string Host { get; init; }
    public int Port { get; init; }
    public string? Rack { get; init; }
    public bool IsFenced { get; init; }

    public static DescribeClusterNode Read(ref KafkaProtocolReader reader, short version)
    {
        var nodeId = reader.ReadInt32();
        var host = reader.ReadCompactString() ?? string.Empty;
        var port = reader.ReadInt32();
        var rack = reader.ReadCompactString();
        var isFenced = version >= 2 && reader.ReadBoolean();
        reader.SkipTaggedFields();

        return new DescribeClusterNode
        {
            NodeId = nodeId,
            Host = host,
            Port = port,
            Rack = rack,
            IsFenced = isFenced
        };
    }
}
