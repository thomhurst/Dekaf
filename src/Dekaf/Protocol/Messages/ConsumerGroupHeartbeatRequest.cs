namespace Dekaf.Protocol.Messages;

/// <summary>
/// ConsumerGroupHeartbeat request (API key 68).
/// Used by the KIP-848 consumer group protocol for group coordination.
/// The broker handles partition assignment server-side instead of the client.
/// </summary>
public sealed class ConsumerGroupHeartbeatRequest : IKafkaRequest<ConsumerGroupHeartbeatResponse>
{
    public static ApiKey ApiKey => ApiKey.ConsumerGroupHeartbeat;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The member ID. Empty string for new members joining the group.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// The member epoch. 0 for new members, -1 to leave the group, -2 for a static member rejoining.
    /// </summary>
    public required int MemberEpoch { get; init; }

    /// <summary>
    /// The group instance ID for static membership.
    /// </summary>
    public string? InstanceId { get; init; }

    /// <summary>
    /// The rack ID of the consumer, used for rack-aware assignment.
    /// </summary>
    public string? RackId { get; init; }

    /// <summary>
    /// The maximum time in milliseconds that the coordinator will wait for the member to revoke
    /// partitions. Only provided when joining or rejoining the group.
    /// </summary>
    public int RebalanceTimeoutMs { get; init; } = -1;

    /// <summary>
    /// The list of topic names the consumer is subscribed to.
    /// Null if not changed since the last heartbeat.
    /// </summary>
    public IReadOnlyList<string>? SubscribedTopicNames { get; init; }

    /// <summary>
    /// The server-side assignor to use. Null to use the broker default.
    /// </summary>
    public string? ServerAssignor { get; init; }

    /// <summary>
    /// The topic partitions currently owned by this member.
    /// Null if not changed since the last heartbeat.
    /// </summary>
    public IReadOnlyList<ConsumerGroupHeartbeatTopicPartitions>? TopicPartitions { get; init; }

    // ConsumerGroupHeartbeat is only v0, which is always flexible
    public static bool IsFlexibleVersion(short version) => true;
    public static short GetRequestHeaderVersion(short version) => 2;
    public static short GetResponseHeaderVersion(short version) => 1;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(GroupId);
        writer.WriteCompactString(MemberId);
        writer.WriteInt32(MemberEpoch);
        writer.WriteCompactNullableString(InstanceId);
        writer.WriteCompactNullableString(RackId);
        writer.WriteInt32(RebalanceTimeoutMs);

        writer.WriteCompactNullableArray(
            SubscribedTopicNames,
            static (ref KafkaProtocolWriter w, string topic) =>
            {
                w.WriteCompactString(topic);
            });

        writer.WriteCompactNullableString(ServerAssignor);

        writer.WriteCompactNullableArray(
            TopicPartitions,
            static (ref KafkaProtocolWriter w, ConsumerGroupHeartbeatTopicPartitions tp) => tp.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic partitions in a ConsumerGroupHeartbeat request/response.
/// </summary>
public sealed class ConsumerGroupHeartbeatTopicPartitions
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

    public static ConsumerGroupHeartbeatTopicPartitions Read(ref KafkaProtocolReader reader)
    {
        var topicId = reader.ReadUuid();
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32());

        reader.SkipTaggedFields();

        return new ConsumerGroupHeartbeatTopicPartitions
        {
            TopicId = topicId,
            Partitions = partitions
        };
    }
}
