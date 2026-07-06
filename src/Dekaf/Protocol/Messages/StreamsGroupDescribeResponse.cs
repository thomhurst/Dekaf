namespace Dekaf.Protocol.Messages;

/// <summary>
/// StreamsGroupDescribe response (API key 89).
/// Contains the descriptions of streams groups (KIP-1071).
/// </summary>
public sealed class StreamsGroupDescribeResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.StreamsGroupDescribe;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation
    /// (zero if not throttled).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The described groups.
    /// </summary>
    public required IReadOnlyList<StreamsGroupDescribeGroup> Groups { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(ThrottleTimeMs);
        writer.WriteCompactArray(
            Groups,
            static (ref KafkaProtocolWriter w, StreamsGroupDescribeGroup group) => group.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();

        var groups = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsGroupDescribeGroup.Read(ref r));

        reader.SkipTaggedFields();

        return new StreamsGroupDescribeResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Groups = groups
        };
    }
}

/// <summary>
/// Described group in a StreamsGroupDescribe response.
/// </summary>
public sealed class StreamsGroupDescribeGroup
{
    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public required string GroupId { get; init; }
    public required string GroupState { get; init; }
    public int GroupEpoch { get; init; }
    public int AssignmentEpoch { get; init; }
    public StreamsGroupDescribeTopology? Topology { get; init; }
    public required IReadOnlyList<StreamsGroupDescribeMember> Members { get; init; }
    public int AuthorizedOperations { get; init; } = int.MinValue;

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt16((short)ErrorCode);
        writer.WriteCompactNullableString(ErrorMessage);
        writer.WriteCompactString(GroupId);
        writer.WriteCompactString(GroupState);
        writer.WriteInt32(GroupEpoch);
        writer.WriteInt32(AssignmentEpoch);
        StreamsGroupDescribeTopology.WriteNullable(ref writer, Topology);
        writer.WriteCompactArray(
            Members,
            static (ref KafkaProtocolWriter w, StreamsGroupDescribeMember member) => member.Write(ref w));
        writer.WriteInt32(AuthorizedOperations);
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsGroupDescribeGroup Read(ref KafkaProtocolReader reader)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();
        var groupId = reader.ReadCompactString() ?? string.Empty;
        var groupState = reader.ReadCompactString() ?? string.Empty;
        var groupEpoch = reader.ReadInt32();
        var assignmentEpoch = reader.ReadInt32();
        var topology = StreamsGroupDescribeTopology.ReadNullable(ref reader);
        var members = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsGroupDescribeMember.Read(ref r));
        var authorizedOperations = reader.ReadInt32();

        reader.SkipTaggedFields();

        return new StreamsGroupDescribeGroup
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            GroupId = groupId,
            GroupState = groupState,
            GroupEpoch = groupEpoch,
            AssignmentEpoch = assignmentEpoch,
            Topology = topology,
            Members = members,
            AuthorizedOperations = authorizedOperations
        };
    }
}

/// <summary>
/// Topology metadata in a StreamsGroupDescribe response.
/// </summary>
public sealed class StreamsGroupDescribeTopology
{
    public int Epoch { get; init; }
    public IReadOnlyList<StreamsGroupDescribeSubtopology>? Subtopologies { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(Epoch);
        writer.WriteCompactNullableArray(
            Subtopologies,
            static (ref KafkaProtocolWriter w, StreamsGroupDescribeSubtopology subtopology) => subtopology.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }

    public static void WriteNullable(ref KafkaProtocolWriter writer, StreamsGroupDescribeTopology? topology)
    {
        if (topology is null)
        {
            writer.WriteInt8(-1);
            return;
        }

        writer.WriteInt8(1);
        topology.Write(ref writer);
    }

    public static StreamsGroupDescribeTopology? ReadNullable(ref KafkaProtocolReader reader)
    {
        var marker = reader.ReadInt8();
        if (marker < 0)
        {
            return null;
        }

        var epoch = reader.ReadInt32();
        var subtopologies = reader.ReadCompactNullableArray(
            static (ref KafkaProtocolReader r) => StreamsGroupDescribeSubtopology.Read(ref r));

        reader.SkipTaggedFields();

        return new StreamsGroupDescribeTopology
        {
            Epoch = epoch,
            Subtopologies = subtopologies
        };
    }
}

/// <summary>
/// Subtopology metadata in a StreamsGroupDescribe response.
/// </summary>
public sealed class StreamsGroupDescribeSubtopology
{
    public required string SubtopologyId { get; init; }
    public required IReadOnlyList<string> SourceTopics { get; init; }
    public required IReadOnlyList<string> RepartitionSinkTopics { get; init; }
    public required IReadOnlyList<StreamsGroupDescribeTopicInfo> StateChangelogTopics { get; init; }
    public required IReadOnlyList<StreamsGroupDescribeTopicInfo> RepartitionSourceTopics { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(SubtopologyId);
        writer.WriteCompactArray(
            SourceTopics,
            static (ref KafkaProtocolWriter w, string topic) => w.WriteCompactString(topic));
        writer.WriteCompactArray(
            RepartitionSinkTopics,
            static (ref KafkaProtocolWriter w, string topic) => w.WriteCompactString(topic));
        writer.WriteCompactArray(
            StateChangelogTopics,
            static (ref KafkaProtocolWriter w, StreamsGroupDescribeTopicInfo topic) => topic.Write(ref w));
        writer.WriteCompactArray(
            RepartitionSourceTopics,
            static (ref KafkaProtocolWriter w, StreamsGroupDescribeTopicInfo topic) => topic.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsGroupDescribeSubtopology Read(ref KafkaProtocolReader reader)
    {
        var subtopologyId = reader.ReadCompactString() ?? string.Empty;
        var sourceTopics = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadCompactString() ?? string.Empty);
        var repartitionSinkTopics = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadCompactString() ?? string.Empty);
        var stateChangelogTopics = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsGroupDescribeTopicInfo.Read(ref r));
        var repartitionSourceTopics = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsGroupDescribeTopicInfo.Read(ref r));

        reader.SkipTaggedFields();

        return new StreamsGroupDescribeSubtopology
        {
            SubtopologyId = subtopologyId,
            SourceTopics = sourceTopics,
            RepartitionSinkTopics = repartitionSinkTopics,
            StateChangelogTopics = stateChangelogTopics,
            RepartitionSourceTopics = repartitionSourceTopics
        };
    }
}

/// <summary>
/// Topic information in a StreamsGroupDescribe response.
/// </summary>
public sealed class StreamsGroupDescribeTopicInfo
{
    public required string Name { get; init; }
    public int Partitions { get; init; }
    public short ReplicationFactor { get; init; }
    public required IReadOnlyList<StreamsGroupDescribeKeyValue> TopicConfigs { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(Name);
        writer.WriteInt32(Partitions);
        writer.WriteInt16(ReplicationFactor);
        writer.WriteCompactArray(
            TopicConfigs,
            static (ref KafkaProtocolWriter w, StreamsGroupDescribeKeyValue config) => config.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsGroupDescribeTopicInfo Read(ref KafkaProtocolReader reader)
    {
        var name = reader.ReadCompactString() ?? string.Empty;
        var partitions = reader.ReadInt32();
        var replicationFactor = reader.ReadInt16();
        var topicConfigs = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsGroupDescribeKeyValue.Read(ref r));

        reader.SkipTaggedFields();

        return new StreamsGroupDescribeTopicInfo
        {
            Name = name,
            Partitions = partitions,
            ReplicationFactor = replicationFactor,
            TopicConfigs = topicConfigs
        };
    }
}

/// <summary>
/// Key/value pair in a StreamsGroupDescribe response.
/// </summary>
public sealed class StreamsGroupDescribeKeyValue
{
    public required string Key { get; init; }
    public required string Value { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(Key);
        writer.WriteCompactString(Value);
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsGroupDescribeKeyValue Read(ref KafkaProtocolReader reader)
    {
        var key = reader.ReadCompactString() ?? string.Empty;
        var value = reader.ReadCompactString() ?? string.Empty;

        reader.SkipTaggedFields();

        return new StreamsGroupDescribeKeyValue
        {
            Key = key,
            Value = value
        };
    }
}

/// <summary>
/// Member in a StreamsGroupDescribe response.
/// </summary>
public sealed class StreamsGroupDescribeMember
{
    public required string MemberId { get; init; }
    public int MemberEpoch { get; init; }
    public string? InstanceId { get; init; }
    public string? RackId { get; init; }
    public required string ClientId { get; init; }
    public required string ClientHost { get; init; }
    public int TopologyEpoch { get; init; }
    public required string ProcessId { get; init; }
    public StreamsGroupDescribeEndpoint? UserEndpoint { get; init; }
    public required IReadOnlyList<StreamsGroupDescribeKeyValue> ClientTags { get; init; }
    public required IReadOnlyList<StreamsGroupDescribeTaskOffset> TaskOffsets { get; init; }
    public required IReadOnlyList<StreamsGroupDescribeTaskOffset> TaskEndOffsets { get; init; }
    public required StreamsGroupDescribeAssignment Assignment { get; init; }
    public required StreamsGroupDescribeAssignment TargetAssignment { get; init; }
    public bool IsClassic { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(MemberId);
        writer.WriteInt32(MemberEpoch);
        writer.WriteCompactNullableString(InstanceId);
        writer.WriteCompactNullableString(RackId);
        writer.WriteCompactString(ClientId);
        writer.WriteCompactString(ClientHost);
        writer.WriteInt32(TopologyEpoch);
        writer.WriteCompactString(ProcessId);
        StreamsGroupDescribeEndpoint.WriteNullable(ref writer, UserEndpoint);
        writer.WriteCompactArray(
            ClientTags,
            static (ref KafkaProtocolWriter w, StreamsGroupDescribeKeyValue tag) => tag.Write(ref w));
        writer.WriteCompactArray(
            TaskOffsets,
            static (ref KafkaProtocolWriter w, StreamsGroupDescribeTaskOffset offset) => offset.Write(ref w));
        writer.WriteCompactArray(
            TaskEndOffsets,
            static (ref KafkaProtocolWriter w, StreamsGroupDescribeTaskOffset offset) => offset.Write(ref w));
        Assignment.Write(ref writer);
        TargetAssignment.Write(ref writer);
        writer.WriteBoolean(IsClassic);
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsGroupDescribeMember Read(ref KafkaProtocolReader reader)
    {
        var memberId = reader.ReadCompactString() ?? string.Empty;
        var memberEpoch = reader.ReadInt32();
        var instanceId = reader.ReadCompactString();
        var rackId = reader.ReadCompactString();
        var clientId = reader.ReadCompactString() ?? string.Empty;
        var clientHost = reader.ReadCompactString() ?? string.Empty;
        var topologyEpoch = reader.ReadInt32();
        var processId = reader.ReadCompactString() ?? string.Empty;
        var userEndpoint = StreamsGroupDescribeEndpoint.ReadNullable(ref reader);
        var clientTags = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsGroupDescribeKeyValue.Read(ref r));
        var taskOffsets = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsGroupDescribeTaskOffset.Read(ref r));
        var taskEndOffsets = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsGroupDescribeTaskOffset.Read(ref r));
        var assignment = StreamsGroupDescribeAssignment.Read(ref reader);
        var targetAssignment = StreamsGroupDescribeAssignment.Read(ref reader);
        var isClassic = reader.ReadBoolean();

        reader.SkipTaggedFields();

        return new StreamsGroupDescribeMember
        {
            MemberId = memberId,
            MemberEpoch = memberEpoch,
            InstanceId = instanceId,
            RackId = rackId,
            ClientId = clientId,
            ClientHost = clientHost,
            TopologyEpoch = topologyEpoch,
            ProcessId = processId,
            UserEndpoint = userEndpoint,
            ClientTags = clientTags,
            TaskOffsets = taskOffsets,
            TaskEndOffsets = taskEndOffsets,
            Assignment = assignment,
            TargetAssignment = targetAssignment,
            IsClassic = isClassic
        };
    }
}

/// <summary>
/// Interactive Query endpoint in a StreamsGroupDescribe response.
/// </summary>
public sealed class StreamsGroupDescribeEndpoint
{
    public required string Host { get; init; }
    public ushort Port { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(Host);
        writer.WriteUInt16(Port);
        writer.WriteEmptyTaggedFields();
    }

    public static void WriteNullable(ref KafkaProtocolWriter writer, StreamsGroupDescribeEndpoint? endpoint)
    {
        if (endpoint is null)
        {
            writer.WriteInt8(-1);
            return;
        }

        writer.WriteInt8(1);
        endpoint.Write(ref writer);
    }

    public static StreamsGroupDescribeEndpoint? ReadNullable(ref KafkaProtocolReader reader)
    {
        var marker = reader.ReadInt8();
        if (marker < 0)
        {
            return null;
        }

        var host = reader.ReadCompactString() ?? string.Empty;
        var port = reader.ReadUInt16();

        reader.SkipTaggedFields();

        return new StreamsGroupDescribeEndpoint
        {
            Host = host,
            Port = port
        };
    }
}

/// <summary>
/// Cumulative task changelog offset in a StreamsGroupDescribe response.
/// </summary>
public sealed class StreamsGroupDescribeTaskOffset
{
    public required string SubtopologyId { get; init; }
    public int Partition { get; init; }
    public long Offset { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(SubtopologyId);
        writer.WriteInt32(Partition);
        writer.WriteInt64(Offset);
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsGroupDescribeTaskOffset Read(ref KafkaProtocolReader reader)
    {
        var subtopologyId = reader.ReadCompactString() ?? string.Empty;
        var partition = reader.ReadInt32();
        var offset = reader.ReadInt64();

        reader.SkipTaggedFields();

        return new StreamsGroupDescribeTaskOffset
        {
            SubtopologyId = subtopologyId,
            Partition = partition,
            Offset = offset
        };
    }
}

/// <summary>
/// Task assignment in a StreamsGroupDescribe response.
/// </summary>
public sealed class StreamsGroupDescribeAssignment
{
    public required IReadOnlyList<StreamsGroupDescribeTaskIds> ActiveTasks { get; init; }
    public required IReadOnlyList<StreamsGroupDescribeTaskIds> StandbyTasks { get; init; }
    public required IReadOnlyList<StreamsGroupDescribeTaskIds> WarmupTasks { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactArray(
            ActiveTasks,
            static (ref KafkaProtocolWriter w, StreamsGroupDescribeTaskIds tasks) => tasks.Write(ref w));
        writer.WriteCompactArray(
            StandbyTasks,
            static (ref KafkaProtocolWriter w, StreamsGroupDescribeTaskIds tasks) => tasks.Write(ref w));
        writer.WriteCompactArray(
            WarmupTasks,
            static (ref KafkaProtocolWriter w, StreamsGroupDescribeTaskIds tasks) => tasks.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsGroupDescribeAssignment Read(ref KafkaProtocolReader reader)
    {
        var activeTasks = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsGroupDescribeTaskIds.Read(ref r));
        var standbyTasks = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsGroupDescribeTaskIds.Read(ref r));
        var warmupTasks = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsGroupDescribeTaskIds.Read(ref r));

        reader.SkipTaggedFields();

        return new StreamsGroupDescribeAssignment
        {
            ActiveTasks = activeTasks,
            StandbyTasks = standbyTasks,
            WarmupTasks = warmupTasks
        };
    }
}

/// <summary>
/// Task IDs by subtopology in a StreamsGroupDescribe response.
/// </summary>
public sealed class StreamsGroupDescribeTaskIds
{
    public required string SubtopologyId { get; init; }
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(SubtopologyId);
        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, int partition) => w.WriteInt32(partition));
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsGroupDescribeTaskIds Read(ref KafkaProtocolReader reader)
    {
        var subtopologyId = reader.ReadCompactString() ?? string.Empty;
        var partitions = reader.ReadCompactArray(static (ref KafkaProtocolReader r) => r.ReadInt32());

        reader.SkipTaggedFields();

        return new StreamsGroupDescribeTaskIds
        {
            SubtopologyId = subtopologyId,
            Partitions = partitions
        };
    }
}
