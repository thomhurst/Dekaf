namespace Dekaf.Protocol.Messages;

/// <summary>
/// ConsumerGroupHeartbeat response (API key 68).
/// Contains the group coordination state from the broker, including
/// the assigned partitions (server-side assignment per KIP-848).
/// </summary>
public sealed class ConsumerGroupHeartbeatResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ConsumerGroupHeartbeat;
    public static short LowestSupportedVersion => 0;
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
    /// The member ID. For v0: assigned by the coordinator on first join.
    /// For v1+ (KIP-1082): echoes the client-generated UUID v4 back.
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
    /// Contains both immediately usable partitions and pending partitions
    /// that are still being revoked from other members.
    /// </summary>
    public ConsumerGroupHeartbeatAssignment? Assignment { get; init; }

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
        ConsumerGroupHeartbeatAssignment? assignment = null;
        if (assignmentMarker >= 0)
        {
            assignment = ConsumerGroupHeartbeatAssignment.Read(ref reader, version);
        }

        // Response tagged fields
        reader.SkipTaggedFields();

        return new ConsumerGroupHeartbeatResponse
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
/// Assignment information in a ConsumerGroupHeartbeat response.
/// Contains the partitions assigned to this member by the server.
/// </summary>
public sealed class ConsumerGroupHeartbeatAssignment
{
    /// <summary>
    /// The partitions assigned to this member that are immediately usable.
    /// </summary>
    public required IReadOnlyList<ConsumerGroupHeartbeatTopicPartitions> AssignedTopicPartitions { get; init; }

    /// <summary>
    /// The partitions assigned to this member that are not yet released by their former owners.
    /// The member should not start consuming from these partitions until they appear in
    /// <see cref="AssignedTopicPartitions"/> in a subsequent heartbeat response.
    /// </summary>
    public required IReadOnlyList<ConsumerGroupHeartbeatTopicPartitions> PendingTopicPartitions { get; init; }

    public static ConsumerGroupHeartbeatAssignment Read(ref KafkaProtocolReader reader, short version)
    {
        // The wire format for Assignment is identical across v0 and v1 (KIP-1082 is a
        // semantic-only change for the response). Only AssignedTopicPartitions is a positional field.
        // PendingTopicPartitions is not present as a positional field in any version.
        var assignedTopicPartitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ConsumerGroupHeartbeatTopicPartitions.Read(ref r));

        reader.SkipTaggedFields();

        return new ConsumerGroupHeartbeatAssignment
        {
            AssignedTopicPartitions = assignedTopicPartitions,
            PendingTopicPartitions = []
        };
    }
}
