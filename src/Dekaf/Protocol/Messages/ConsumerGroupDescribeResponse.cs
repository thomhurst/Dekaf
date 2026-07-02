namespace Dekaf.Protocol.Messages;

/// <summary>
/// ConsumerGroupDescribe response (API key 69).
/// Contains KIP-848 consumer group descriptions.
/// </summary>
public sealed class ConsumerGroupDescribeResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ConsumerGroupDescribe;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The described consumer groups.
    /// </summary>
    public required IReadOnlyList<ConsumerGroupDescribeGroup> Groups { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();

        var groups = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => ConsumerGroupDescribeGroup.Read(ref r, version));

        reader.SkipTaggedFields();

        return new ConsumerGroupDescribeResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Groups = groups
        };
    }
}

/// <summary>
/// Described KIP-848 consumer group.
/// </summary>
public sealed class ConsumerGroupDescribeGroup
{
    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public required string GroupId { get; init; }
    public required string GroupState { get; init; }
    public int GroupEpoch { get; init; }
    public int AssignmentEpoch { get; init; }
    public required string AssignorName { get; init; }
    public required IReadOnlyList<ConsumerGroupDescribeMember> Members { get; init; }
    public int AuthorizedOperations { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt16((short)ErrorCode);
        writer.WriteCompactNullableString(ErrorMessage);
        writer.WriteCompactString(GroupId);
        writer.WriteCompactString(GroupState);
        writer.WriteInt32(GroupEpoch);
        writer.WriteInt32(AssignmentEpoch);
        writer.WriteCompactString(AssignorName);

        writer.WriteCompactArray(
            Members,
            (ref KafkaProtocolWriter w, ConsumerGroupDescribeMember member) => member.Write(ref w, version));

        writer.WriteInt32(AuthorizedOperations);
        writer.WriteEmptyTaggedFields();
    }

    public static ConsumerGroupDescribeGroup Read(ref KafkaProtocolReader reader, short version)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();
        var groupId = reader.ReadCompactString() ?? string.Empty;
        var groupState = reader.ReadCompactString() ?? string.Empty;
        var groupEpoch = reader.ReadInt32();
        var assignmentEpoch = reader.ReadInt32();
        var assignorName = reader.ReadCompactString() ?? string.Empty;

        var members = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => ConsumerGroupDescribeMember.Read(ref r, version));

        var authorizedOperations = reader.ReadInt32();
        reader.SkipTaggedFields();

        return new ConsumerGroupDescribeGroup
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
/// Member in a KIP-848 consumer group description.
/// </summary>
public sealed class ConsumerGroupDescribeMember
{
    public required string MemberId { get; init; }
    public string? InstanceId { get; init; }
    public string? RackId { get; init; }
    public int MemberEpoch { get; init; }
    public required string ClientId { get; init; }
    public required string ClientHost { get; init; }
    public required IReadOnlyList<string> SubscribedTopicNames { get; init; }
    public string? SubscribedTopicRegex { get; init; }
    public required ConsumerGroupDescribeAssignment Assignment { get; init; }
    public required ConsumerGroupDescribeAssignment TargetAssignment { get; init; }
    public sbyte? MemberType { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(MemberId);
        writer.WriteCompactNullableString(InstanceId);
        writer.WriteCompactNullableString(RackId);
        writer.WriteInt32(MemberEpoch);
        writer.WriteCompactString(ClientId);
        writer.WriteCompactString(ClientHost);

        writer.WriteCompactArray(
            SubscribedTopicNames,
            static (ref KafkaProtocolWriter w, string topicName) => w.WriteCompactString(topicName));

        writer.WriteCompactNullableString(SubscribedTopicRegex);
        Assignment.Write(ref writer);
        TargetAssignment.Write(ref writer);

        if (version >= 1)
        {
            writer.WriteInt8(MemberType ?? (sbyte)-1);
        }

        writer.WriteEmptyTaggedFields();
    }

    public static ConsumerGroupDescribeMember Read(ref KafkaProtocolReader reader, short version)
    {
        var memberId = reader.ReadCompactString() ?? string.Empty;
        var instanceId = reader.ReadCompactString();
        var rackId = reader.ReadCompactString();
        var memberEpoch = reader.ReadInt32();
        var clientId = reader.ReadCompactString() ?? string.Empty;
        var clientHost = reader.ReadCompactString() ?? string.Empty;

        var subscribedTopicNames = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadCompactString() ?? string.Empty);

        var subscribedTopicRegex = reader.ReadCompactString();
        var assignment = ConsumerGroupDescribeAssignment.Read(ref reader);
        var targetAssignment = ConsumerGroupDescribeAssignment.Read(ref reader);
        sbyte? memberType = version >= 1 ? reader.ReadInt8() : null;

        reader.SkipTaggedFields();

        return new ConsumerGroupDescribeMember
        {
            MemberId = memberId,
            InstanceId = instanceId,
            RackId = rackId,
            MemberEpoch = memberEpoch,
            ClientId = clientId,
            ClientHost = clientHost,
            SubscribedTopicNames = subscribedTopicNames,
            SubscribedTopicRegex = subscribedTopicRegex,
            Assignment = assignment,
            TargetAssignment = targetAssignment,
            MemberType = memberType
        };
    }
}

/// <summary>
/// Assignment information in a ConsumerGroupDescribe response.
/// </summary>
public sealed class ConsumerGroupDescribeAssignment
{
    public required IReadOnlyList<ConsumerGroupDescribeTopicPartitions> TopicPartitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactArray(
            TopicPartitions,
            static (ref KafkaProtocolWriter w, ConsumerGroupDescribeTopicPartitions topicPartitions) => topicPartitions.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }

    public static ConsumerGroupDescribeAssignment Read(ref KafkaProtocolReader reader)
    {
        var topicPartitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ConsumerGroupDescribeTopicPartitions.Read(ref r));

        reader.SkipTaggedFields();

        return new ConsumerGroupDescribeAssignment
        {
            TopicPartitions = topicPartitions
        };
    }
}

/// <summary>
/// Topic partitions in a ConsumerGroupDescribe assignment.
/// </summary>
public sealed class ConsumerGroupDescribeTopicPartitions
{
    public required Guid TopicId { get; init; }
    public required string TopicName { get; init; }
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteUuid(TopicId);
        writer.WriteCompactString(TopicName);
        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, int partition) => w.WriteInt32(partition));
        writer.WriteEmptyTaggedFields();
    }

    public static ConsumerGroupDescribeTopicPartitions Read(ref KafkaProtocolReader reader)
    {
        var topicId = reader.ReadUuid();
        var topicName = reader.ReadCompactString() ?? string.Empty;
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32());

        reader.SkipTaggedFields();

        return new ConsumerGroupDescribeTopicPartitions
        {
            TopicId = topicId,
            TopicName = topicName,
            Partitions = partitions
        };
    }
}
