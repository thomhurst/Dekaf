namespace Dekaf.Protocol.Messages;

/// <summary>
/// ShareAcknowledge response (API key 79).
/// Contains the results of acknowledging records fetched from a share group (KIP-932).
/// </summary>
public sealed class ShareAcknowledgeResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ShareAcknowledge;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The top-level error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The top-level error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The acquisition lock timeout in milliseconds (v2+ only).
    /// </summary>
    public int AcquisitionLockTimeoutMs { get; init; }

    /// <summary>
    /// The per-topic acknowledgement responses.
    /// </summary>
    public required IReadOnlyList<ShareAcknowledgeResponseTopic> Responses { get; init; }

    /// <summary>
    /// Endpoints for all current leaders enumerated in the response.
    /// </summary>
    public required IReadOnlyList<ShareAcknowledgeNodeEndpoint> NodeEndpoints { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        var acquisitionLockTimeoutMs = 0;
        if (version >= 2)
        {
            acquisitionLockTimeoutMs = reader.ReadInt32();
        }

        var responses = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ShareAcknowledgeResponseTopic.Read(ref r));

        var nodeEndpoints = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ShareAcknowledgeNodeEndpoint.Read(ref r));

        reader.SkipTaggedFields();

        return new ShareAcknowledgeResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            AcquisitionLockTimeoutMs = acquisitionLockTimeoutMs,
            Responses = responses,
            NodeEndpoints = nodeEndpoints
        };
    }
}

/// <summary>
/// Per-topic acknowledgement response in a ShareAcknowledge response.
/// </summary>
public sealed class ShareAcknowledgeResponseTopic
{
    /// <summary>
    /// The topic ID (UUID).
    /// </summary>
    public Guid TopicId { get; init; }

    /// <summary>
    /// The per-partition acknowledgement responses.
    /// </summary>
    public required IReadOnlyList<ShareAcknowledgeResponsePartition> Partitions { get; init; }

    public static ShareAcknowledgeResponseTopic Read(ref KafkaProtocolReader reader)
    {
        var topicId = reader.ReadUuid();
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ShareAcknowledgeResponsePartition.Read(ref r));

        reader.SkipTaggedFields();

        return new ShareAcknowledgeResponseTopic
        {
            TopicId = topicId,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Per-partition acknowledgement response in a ShareAcknowledge response.
/// </summary>
public sealed class ShareAcknowledgeResponsePartition
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public int PartitionIndex { get; init; }

    /// <summary>
    /// The error code for this partition, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message for this partition, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The current leader for this partition, or null if unknown.
    /// </summary>
    public ShareAcknowledgeLeaderIdAndEpoch? CurrentLeader { get; init; }

    public static ShareAcknowledgeResponsePartition Read(ref KafkaProtocolReader reader)
    {
        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        var leaderMarker = reader.ReadInt8();
        ShareAcknowledgeLeaderIdAndEpoch? currentLeader = null;
        if (leaderMarker >= 0)
        {
            currentLeader = ShareAcknowledgeLeaderIdAndEpoch.Read(ref reader);
        }

        reader.SkipTaggedFields();

        return new ShareAcknowledgeResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            CurrentLeader = currentLeader
        };
    }
}

/// <summary>
/// Leader ID and epoch information in a ShareAcknowledge response.
/// </summary>
public sealed class ShareAcknowledgeLeaderIdAndEpoch
{
    /// <summary>
    /// The ID of the current leader, or -1 if the leader is unknown.
    /// </summary>
    public int LeaderId { get; init; } = -1;

    /// <summary>
    /// The latest known leader epoch.
    /// </summary>
    public int LeaderEpoch { get; init; } = -1;

    public static ShareAcknowledgeLeaderIdAndEpoch Read(ref KafkaProtocolReader reader)
    {
        var leaderId = reader.ReadInt32();
        var leaderEpoch = reader.ReadInt32();

        reader.SkipTaggedFields();

        return new ShareAcknowledgeLeaderIdAndEpoch
        {
            LeaderId = leaderId,
            LeaderEpoch = leaderEpoch
        };
    }
}

/// <summary>
/// Node endpoint information in a ShareAcknowledge response.
/// </summary>
public sealed class ShareAcknowledgeNodeEndpoint
{
    /// <summary>
    /// The ID of the associated node.
    /// </summary>
    public int NodeId { get; init; }

    /// <summary>
    /// The node's hostname.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// The node's port.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// The rack of the node, or null if it has not been assigned to a rack.
    /// </summary>
    public string? Rack { get; init; }

    public static ShareAcknowledgeNodeEndpoint Read(ref KafkaProtocolReader reader)
    {
        var nodeId = reader.ReadInt32();
        var host = reader.ReadCompactNonNullableString();
        var port = reader.ReadInt32();
        var rack = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new ShareAcknowledgeNodeEndpoint
        {
            NodeId = nodeId,
            Host = host,
            Port = port,
            Rack = rack
        };
    }
}
