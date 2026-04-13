namespace Dekaf.Protocol.Messages;

/// <summary>
/// ShareFetch response (API key 78).
/// Contains records fetched from share group topic partitions per KIP-932.
/// Includes acquired record ranges, acknowledgement results, and node endpoints.
/// </summary>
public sealed class ShareFetchResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ShareFetch;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The top-level error code, or 0 if there was no error.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The acquisition lock timeout in milliseconds (v1+).
    /// </summary>
    public int AcquisitionLockTimeoutMs { get; init; }

    /// <summary>
    /// Responses per topic.
    /// </summary>
    public required IReadOnlyList<ShareFetchResponseTopic> Responses { get; init; }

    /// <summary>
    /// Node endpoints for preferred leader information.
    /// </summary>
    public required IReadOnlyList<ShareFetchNodeEndpoint> NodeEndpoints { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        var acquisitionLockTimeoutMs = 0;
        if (version >= 1)
        {
            acquisitionLockTimeoutMs = reader.ReadInt32();
        }

        var responses = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => ShareFetchResponseTopic.Read(ref r, v),
            version);

        var nodeEndpoints = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ShareFetchNodeEndpoint.Read(ref r));

        reader.SkipTaggedFields();

        return new ShareFetchResponse
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
/// Topic response in a ShareFetch response.
/// </summary>
public sealed class ShareFetchResponseTopic
{
    /// <summary>
    /// The topic ID (UUID).
    /// </summary>
    public required Guid TopicId { get; init; }

    /// <summary>
    /// Partition responses.
    /// </summary>
    public required IReadOnlyList<ShareFetchResponsePartition> Partitions { get; init; }

    public static ShareFetchResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var topicId = reader.ReadUuid();
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => ShareFetchResponsePartition.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new ShareFetchResponseTopic
        {
            TopicId = topicId,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition response in a ShareFetch response.
/// </summary>
public sealed class ShareFetchResponsePartition
{
    /// <summary>
    /// Partition index.
    /// </summary>
    public int PartitionIndex { get; init; }

    /// <summary>
    /// Error code for this partition.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Error code for the acknowledgement, or 0 if there was no error.
    /// </summary>
    public ErrorCode AcknowledgeErrorCode { get; init; }

    /// <summary>
    /// The acknowledgement error message, or null if there was no error.
    /// </summary>
    public string? AcknowledgeErrorMessage { get; init; }

    /// <summary>
    /// The current leader information. Always present inline in the response.
    /// </summary>
    public required ShareFetchLeaderIdAndEpoch CurrentLeader { get; init; }

    /// <summary>
    /// Raw record batch bytes. Use the protocol Records decoder to parse.
    /// </summary>
    public ReadOnlyMemory<byte> RecordBytes { get; init; }

    /// <summary>
    /// Acquired record ranges for this partition.
    /// </summary>
    public required IReadOnlyList<ShareFetchAcquiredRecords> AcquiredRecords { get; init; }

    public static ShareFetchResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();
        var acknowledgeErrorCode = (ErrorCode)reader.ReadInt16();
        var acknowledgeErrorMessage = reader.ReadCompactString();

        // CurrentLeader is non-nullable — always present inline (no marker byte)
        var leaderId = reader.ReadInt32();
        var leaderEpoch = reader.ReadInt32();
        reader.SkipTaggedFields();

        var currentLeader = new ShareFetchLeaderIdAndEpoch
        {
            LeaderId = leaderId,
            LeaderEpoch = leaderEpoch
        };

        // COMPACT_RECORDS: length+1 encoded as unsigned varint, 0 = null
        var recordsLength = reader.ReadUnsignedVarInt() - 1;
        var recordBytes = ReadOnlyMemory<byte>.Empty;
        if (recordsLength > 0)
        {
            recordBytes = reader.ReadMemorySlice(recordsLength);
        }

        var acquiredRecords = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ShareFetchAcquiredRecords.Read(ref r));

        reader.SkipTaggedFields();

        return new ShareFetchResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            AcknowledgeErrorCode = acknowledgeErrorCode,
            AcknowledgeErrorMessage = acknowledgeErrorMessage,
            CurrentLeader = currentLeader,
            RecordBytes = recordBytes,
            AcquiredRecords = acquiredRecords
        };
    }
}

/// <summary>
/// Leader ID and epoch for a share fetch partition.
/// </summary>
public sealed class ShareFetchLeaderIdAndEpoch
{
    /// <summary>
    /// The leader broker ID, or -1 if unknown.
    /// </summary>
    public int LeaderId { get; init; } = -1;

    /// <summary>
    /// The leader epoch, or -1 if unknown.
    /// </summary>
    public int LeaderEpoch { get; init; } = -1;
}

/// <summary>
/// Acquired record range in a ShareFetch response.
/// Represents a contiguous range of records that have been acquired by this consumer.
/// </summary>
public sealed class ShareFetchAcquiredRecords
{
    /// <summary>
    /// The first offset of the acquired range.
    /// </summary>
    public long FirstOffset { get; init; }

    /// <summary>
    /// The last offset of the acquired range.
    /// </summary>
    public long LastOffset { get; init; }

    /// <summary>
    /// The number of times these records have been delivered.
    /// </summary>
    public short DeliveryCount { get; init; }

    public static ShareFetchAcquiredRecords Read(ref KafkaProtocolReader reader)
    {
        var firstOffset = reader.ReadInt64();
        var lastOffset = reader.ReadInt64();
        var deliveryCount = reader.ReadInt16();

        reader.SkipTaggedFields();

        return new ShareFetchAcquiredRecords
        {
            FirstOffset = firstOffset,
            LastOffset = lastOffset,
            DeliveryCount = deliveryCount
        };
    }
}

/// <summary>
/// Node endpoint in a ShareFetch response.
/// Provides connection information for preferred leaders.
/// </summary>
public sealed class ShareFetchNodeEndpoint
{
    /// <summary>
    /// The node ID.
    /// </summary>
    public int NodeId { get; init; }

    /// <summary>
    /// The hostname.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// The port number.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// The rack ID, or null if not available.
    /// </summary>
    public string? Rack { get; init; }

    public static ShareFetchNodeEndpoint Read(ref KafkaProtocolReader reader)
    {
        var nodeId = reader.ReadInt32();
        var host = reader.ReadCompactNonNullableString();
        var port = reader.ReadInt32();
        var rack = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new ShareFetchNodeEndpoint
        {
            NodeId = nodeId,
            Host = host,
            Port = port,
            Rack = rack
        };
    }
}
