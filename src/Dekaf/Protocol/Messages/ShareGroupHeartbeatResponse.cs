namespace Dekaf.Protocol.Messages;

/// <summary>
/// ShareGroupHeartbeat response (API key 76).
/// Contains the group coordination state from the broker, including
/// the assigned partitions (server-side assignment per KIP-932).
/// </summary>
public sealed class ShareGroupHeartbeatResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ShareGroupHeartbeat;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 1;

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
    /// The member ID. Echoes the client-generated UUID v4 back.
    /// </summary>
    public string? MemberId { get; init; }

    /// <summary>
    /// The current member epoch. The member must use this epoch for subsequent heartbeats.
    /// </summary>
    public int MemberEpoch { get; init; }

    /// <summary>
    /// The heartbeat interval in milliseconds.
    /// The member should send the next heartbeat before this interval expires.
    /// </summary>
    public int HeartbeatIntervalMs { get; init; }

    /// <summary>
    /// The assignment for this member, or null if the assignment has not changed.
    /// Contains the partitions assigned to this member by the server.
    /// </summary>
    public ShareGroupHeartbeatAssignment? Assignment { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();
        var memberId = reader.ReadCompactString();
        var memberEpoch = reader.ReadInt32();
        var heartbeatIntervalMs = reader.ReadInt32();

        // Nullable non-tagged struct: single signed byte marker (-1 = null, >= 0 = present)
        var assignmentMarker = reader.ReadInt8();
        ShareGroupHeartbeatAssignment? assignment = null;
        if (assignmentMarker >= 0)
        {
            assignment = ShareGroupHeartbeatAssignment.Read(ref reader);
        }

        // Response tagged fields
        reader.SkipTaggedFields();

        return new ShareGroupHeartbeatResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            MemberId = memberId,
            MemberEpoch = memberEpoch,
            HeartbeatIntervalMs = heartbeatIntervalMs,
            Assignment = assignment
        };
    }
}

/// <summary>
/// Assignment information in a ShareGroupHeartbeat response.
/// Contains the partitions assigned to this member by the server.
/// </summary>
public sealed class ShareGroupHeartbeatAssignment
{
    /// <summary>
    /// The partitions assigned to this member.
    /// </summary>
    public required IReadOnlyList<ShareGroupHeartbeatTopicPartitions> TopicPartitions { get; init; }

    public static ShareGroupHeartbeatAssignment Read(ref KafkaProtocolReader reader)
    {
        var topicPartitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ShareGroupHeartbeatTopicPartitions.Read(ref r));

        reader.SkipTaggedFields();

        return new ShareGroupHeartbeatAssignment
        {
            TopicPartitions = topicPartitions
        };
    }
}

/// <summary>
/// Topic partitions in a ShareGroupHeartbeat response.
/// </summary>
public sealed class ShareGroupHeartbeatTopicPartitions
{
    /// <summary>
    /// The topic ID (UUID).
    /// </summary>
    public required Guid TopicId { get; init; }

    /// <summary>
    /// The partition indexes.
    /// </summary>
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteUuid(TopicId);
        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, int partition) => w.WriteInt32(partition));
        writer.WriteEmptyTaggedFields();
    }

    public static ShareGroupHeartbeatTopicPartitions Read(ref KafkaProtocolReader reader)
    {
        var topicId = reader.ReadUuid();
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32());

        reader.SkipTaggedFields();

        return new ShareGroupHeartbeatTopicPartitions
        {
            TopicId = topicId,
            Partitions = partitions
        };
    }
}
