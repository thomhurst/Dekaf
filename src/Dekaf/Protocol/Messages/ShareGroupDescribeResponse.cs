namespace Dekaf.Protocol.Messages;

/// <summary>
/// ShareGroupDescribe response (API key 77).
/// Contains the descriptions of share groups (KIP-932).
/// </summary>
public sealed class ShareGroupDescribeResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ShareGroupDescribe;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation
    /// (zero if not throttled).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The described groups.
    /// </summary>
    public required IReadOnlyList<ShareGroupDescribeGroup> Groups { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();

        var groups = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ShareGroupDescribeGroup.Read(ref r));

        reader.SkipTaggedFields();

        return new ShareGroupDescribeResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Groups = groups
        };
    }
}

/// <summary>
/// Described group in a ShareGroupDescribe response.
/// </summary>
public sealed class ShareGroupDescribeGroup
{
    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The group state.
    /// </summary>
    public required string GroupState { get; init; }

    /// <summary>
    /// The group epoch.
    /// </summary>
    public int GroupEpoch { get; init; }

    /// <summary>
    /// The assignment epoch.
    /// </summary>
    public int AssignmentEpoch { get; init; }

    /// <summary>
    /// The server-side assignor name.
    /// </summary>
    public string? AssignorName { get; init; }

    /// <summary>
    /// The group members.
    /// </summary>
    public required IReadOnlyList<ShareGroupDescribeMember> Members { get; init; }

    /// <summary>
    /// 32-bit bitfield representing authorized operations for this group.
    /// </summary>
    public int AuthorizedOperations { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt16((short)ErrorCode);
        writer.WriteCompactNullableString(ErrorMessage);
        writer.WriteCompactString(GroupId);
        writer.WriteCompactString(GroupState);
        writer.WriteInt32(GroupEpoch);
        writer.WriteInt32(AssignmentEpoch);
        writer.WriteCompactNullableString(AssignorName);

        writer.WriteCompactArray(
            Members,
            static (ref KafkaProtocolWriter w, ShareGroupDescribeMember member) => member.Write(ref w));

        writer.WriteInt32(AuthorizedOperations);

        writer.WriteEmptyTaggedFields();
    }

    public static ShareGroupDescribeGroup Read(ref KafkaProtocolReader reader)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();
        var groupId = reader.ReadCompactString() ?? string.Empty;
        var groupState = reader.ReadCompactString() ?? string.Empty;
        var groupEpoch = reader.ReadInt32();
        var assignmentEpoch = reader.ReadInt32();
        var assignorName = reader.ReadCompactString();

        var members = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ShareGroupDescribeMember.Read(ref r));

        var authorizedOperations = reader.ReadInt32();

        reader.SkipTaggedFields();

        return new ShareGroupDescribeGroup
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            GroupId = groupId,
            GroupState = groupState,
            GroupEpoch = groupEpoch,
            AssignmentEpoch = assignmentEpoch,
            AssignorName = assignorName,
            Members = members,
            AuthorizedOperations = authorizedOperations
        };
    }
}

/// <summary>
/// Member in a ShareGroupDescribe response.
/// </summary>
public sealed class ShareGroupDescribeMember
{
    /// <summary>
    /// The member ID.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// The rack ID of the member.
    /// </summary>
    public string? RackId { get; init; }

    /// <summary>
    /// The current member epoch.
    /// </summary>
    public int MemberEpoch { get; init; }

    /// <summary>
    /// The client ID.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// The client host.
    /// </summary>
    public required string ClientHost { get; init; }

    /// <summary>
    /// The list of topic names the member is subscribed to.
    /// </summary>
    public required IReadOnlyList<string> SubscribedTopicNames { get; init; }

    /// <summary>
    /// The assignment for this member, or null if there is no assignment.
    /// </summary>
    public ShareGroupDescribeAssignment? Assignment { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(MemberId);
        writer.WriteCompactNullableString(RackId);
        writer.WriteInt32(MemberEpoch);
        writer.WriteCompactString(ClientId);
        writer.WriteCompactString(ClientHost);

        writer.WriteCompactArray(
            SubscribedTopicNames,
            static (ref KafkaProtocolWriter w, string topic) => w.WriteCompactString(topic));

        // Nullable non-tagged struct: single signed byte marker (-1 = null, >= 0 = present)
        if (Assignment is not null)
        {
            writer.WriteInt8(0);
            Assignment.Write(ref writer);
        }
        else
        {
            writer.WriteInt8(-1);
        }

        writer.WriteEmptyTaggedFields();
    }

    public static ShareGroupDescribeMember Read(ref KafkaProtocolReader reader)
    {
        var memberId = reader.ReadCompactString() ?? string.Empty;
        var rackId = reader.ReadCompactString();
        var memberEpoch = reader.ReadInt32();
        var clientId = reader.ReadCompactString() ?? string.Empty;
        var clientHost = reader.ReadCompactString() ?? string.Empty;

        var subscribedTopicNames = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadCompactString() ?? string.Empty);

        // Nullable non-tagged struct: single signed byte marker (-1 = null, >= 0 = present)
        var assignmentMarker = reader.ReadInt8();
        ShareGroupDescribeAssignment? assignment = null;
        if (assignmentMarker >= 0)
        {
            assignment = ShareGroupDescribeAssignment.Read(ref reader);
        }

        reader.SkipTaggedFields();

        return new ShareGroupDescribeMember
        {
            MemberId = memberId,
            RackId = rackId,
            MemberEpoch = memberEpoch,
            ClientId = clientId,
            ClientHost = clientHost,
            SubscribedTopicNames = subscribedTopicNames,
            Assignment = assignment
        };
    }
}

/// <summary>
/// Assignment information in a ShareGroupDescribe response.
/// Contains the partitions assigned to a member.
/// </summary>
public sealed class ShareGroupDescribeAssignment
{
    /// <summary>
    /// The topic partitions assigned to this member.
    /// </summary>
    public required IReadOnlyList<ShareGroupDescribeTopicPartitions> TopicPartitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactArray(
            TopicPartitions,
            static (ref KafkaProtocolWriter w, ShareGroupDescribeTopicPartitions tp) => tp.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }

    public static ShareGroupDescribeAssignment Read(ref KafkaProtocolReader reader)
    {
        var topicPartitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ShareGroupDescribeTopicPartitions.Read(ref r));

        reader.SkipTaggedFields();

        return new ShareGroupDescribeAssignment
        {
            TopicPartitions = topicPartitions
        };
    }
}

/// <summary>
/// Topic partitions in a ShareGroupDescribe response.
/// </summary>
public sealed class ShareGroupDescribeTopicPartitions
{
    /// <summary>
    /// The topic ID (UUID).
    /// </summary>
    public required Guid TopicId { get; init; }

    /// <summary>
    /// The topic name.
    /// </summary>
    public string? TopicName { get; init; }

    /// <summary>
    /// The partition indexes.
    /// </summary>
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteUuid(TopicId);
        writer.WriteCompactNullableString(TopicName);
        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, int partition) => w.WriteInt32(partition));
        writer.WriteEmptyTaggedFields();
    }

    public static ShareGroupDescribeTopicPartitions Read(ref KafkaProtocolReader reader)
    {
        var topicId = reader.ReadUuid();
        var topicName = reader.ReadCompactString();
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32());

        reader.SkipTaggedFields();

        return new ShareGroupDescribeTopicPartitions
        {
            TopicId = topicId,
            TopicName = topicName,
            Partitions = partitions
        };
    }
}
