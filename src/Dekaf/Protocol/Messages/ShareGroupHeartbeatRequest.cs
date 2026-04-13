namespace Dekaf.Protocol.Messages;

/// <summary>
/// ShareGroupHeartbeat request (API key 76).
/// Used by the KIP-932 share group protocol for group coordination.
/// The broker handles partition assignment server-side for share groups.
/// </summary>
public sealed class ShareGroupHeartbeatRequest : IKafkaRequest<ShareGroupHeartbeatResponse>
{
    public static ApiKey ApiKey => ApiKey.ShareGroupHeartbeat;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The member ID. Client-generated UUID v4 sent on the first heartbeat.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// The member epoch. 0 to join the group, -1 to leave the group.
    /// </summary>
    public required int MemberEpoch { get; init; }

    /// <summary>
    /// The rack ID of the consumer, used for rack-aware assignment.
    /// </summary>
    public string? RackId { get; init; }

    /// <summary>
    /// The list of topic names the consumer is subscribed to.
    /// Null if not changed since the last heartbeat.
    /// </summary>
    public IReadOnlyList<string>? SubscribedTopicNames { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(GroupId);
        writer.WriteCompactString(MemberId);
        writer.WriteInt32(MemberEpoch);
        writer.WriteCompactNullableString(RackId);

        writer.WriteCompactNullableArray(
            SubscribedTopicNames,
            static (ref KafkaProtocolWriter w, string topic) =>
            {
                w.WriteCompactString(topic);
            });

        writer.WriteEmptyTaggedFields();
    }
}
