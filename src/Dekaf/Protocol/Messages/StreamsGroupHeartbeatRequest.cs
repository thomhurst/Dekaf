namespace Dekaf.Protocol.Messages;

/// <summary>
/// StreamsGroupHeartbeat request (API key 88), introduced by KIP-1071.
/// </summary>
internal sealed class StreamsGroupHeartbeatRequest : IKafkaRequest<StreamsGroupHeartbeatResponse>
{
    public static ApiKey ApiKey => ApiKey.StreamsGroupHeartbeat;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public required string GroupId { get; init; }
    public required string MemberId { get; init; }
    public required int MemberEpoch { get; init; }
    public int EndpointInformationEpoch { get; init; }
    public string? InstanceId { get; init; }
    public string? RackId { get; init; }
    public int RebalanceTimeoutMs { get; init; } = -1;
    public StreamsGroupHeartbeatTopology? Topology { get; init; }
    public IReadOnlyList<StreamsGroupHeartbeatTaskIds>? ActiveTasks { get; init; }
    public IReadOnlyList<StreamsGroupHeartbeatTaskIds>? StandbyTasks { get; init; }
    public IReadOnlyList<StreamsGroupHeartbeatTaskIds>? WarmupTasks { get; init; }
    public string? ProcessId { get; init; }
    public StreamsGroupHeartbeatEndpoint? UserEndpoint { get; init; }
    public IReadOnlyList<StreamsGroupHeartbeatKeyValue>? ClientTags { get; init; }
    public IReadOnlyList<StreamsGroupHeartbeatTaskOffset>? TaskOffsets { get; init; }
    public IReadOnlyList<StreamsGroupHeartbeatTaskOffset>? TaskEndOffsets { get; init; }
    public bool ShutdownApplication { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(GroupId);
        writer.WriteCompactString(MemberId);
        writer.WriteInt32(MemberEpoch);
        writer.WriteInt32(EndpointInformationEpoch);
        writer.WriteCompactNullableString(InstanceId);
        writer.WriteCompactNullableString(RackId);
        writer.WriteInt32(RebalanceTimeoutMs);
        StreamsGroupHeartbeatTopology.WriteNullable(ref writer, Topology);
        WriteNullableTasks(ref writer, ActiveTasks);
        WriteNullableTasks(ref writer, StandbyTasks);
        WriteNullableTasks(ref writer, WarmupTasks);
        writer.WriteCompactNullableString(ProcessId);
        StreamsGroupHeartbeatEndpoint.WriteNullable(ref writer, UserEndpoint);
        writer.WriteCompactNullableArray(
            ClientTags,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatKeyValue item) => item.Write(ref w));
        writer.WriteCompactNullableArray(
            TaskOffsets,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatTaskOffset item) => item.Write(ref w));
        writer.WriteCompactNullableArray(
            TaskEndOffsets,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatTaskOffset item) => item.Write(ref w));
        writer.WriteBoolean(ShutdownApplication);
        writer.WriteEmptyTaggedFields();
    }

    private static void WriteNullableTasks(
        ref KafkaProtocolWriter writer,
        IReadOnlyList<StreamsGroupHeartbeatTaskIds>? tasks) =>
        writer.WriteCompactNullableArray(
            tasks,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatTaskIds item) => item.Write(ref w));
}

internal sealed class StreamsGroupHeartbeatTopology
{
    public int Epoch { get; init; }
    public required IReadOnlyList<StreamsGroupHeartbeatSubtopology> Subtopologies { get; init; }

    public static void WriteNullable(ref KafkaProtocolWriter writer, StreamsGroupHeartbeatTopology? topology)
    {
        if (topology is null)
        {
            writer.WriteInt8(-1);
            return;
        }

        writer.WriteInt8(1);
        writer.WriteInt32(topology.Epoch);
        writer.WriteCompactArray(
            topology.Subtopologies,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatSubtopology item) => item.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }
}

internal sealed class StreamsGroupHeartbeatSubtopology
{
    public required string SubtopologyId { get; init; }
    public required IReadOnlyList<string> SourceTopics { get; init; }
    public required IReadOnlyList<string> SourceTopicRegex { get; init; }
    public required IReadOnlyList<StreamsGroupHeartbeatTopicInfo> StateChangelogTopics { get; init; }
    public required IReadOnlyList<string> RepartitionSinkTopics { get; init; }
    public required IReadOnlyList<StreamsGroupHeartbeatTopicInfo> RepartitionSourceTopics { get; init; }
    public required IReadOnlyList<StreamsGroupHeartbeatCopartitionGroup> CopartitionGroups { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(SubtopologyId);
        WriteStrings(ref writer, SourceTopics);
        WriteStrings(ref writer, SourceTopicRegex);
        writer.WriteCompactArray(
            StateChangelogTopics,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatTopicInfo item) => item.Write(ref w));
        WriteStrings(ref writer, RepartitionSinkTopics);
        writer.WriteCompactArray(
            RepartitionSourceTopics,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatTopicInfo item) => item.Write(ref w));
        writer.WriteCompactArray(
            CopartitionGroups,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatCopartitionGroup item) => item.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }

    private static void WriteStrings(ref KafkaProtocolWriter writer, IReadOnlyList<string> items) =>
        writer.WriteCompactArray(
            items,
            static (ref KafkaProtocolWriter w, string item) => w.WriteCompactString(item));
}

internal sealed class StreamsGroupHeartbeatTopicInfo
{
    public required string Name { get; init; }
    public int Partitions { get; init; }
    public short ReplicationFactor { get; init; }
    public required IReadOnlyList<StreamsGroupHeartbeatKeyValue> TopicConfigs { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(Name);
        writer.WriteInt32(Partitions);
        writer.WriteInt16(ReplicationFactor);
        writer.WriteCompactArray(
            TopicConfigs,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatKeyValue item) => item.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }
}

internal sealed class StreamsGroupHeartbeatCopartitionGroup
{
    public required IReadOnlyList<short> SourceTopics { get; init; }
    public required IReadOnlyList<short> SourceTopicRegex { get; init; }
    public required IReadOnlyList<short> RepartitionSourceTopics { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        WriteIndexes(ref writer, SourceTopics);
        WriteIndexes(ref writer, SourceTopicRegex);
        WriteIndexes(ref writer, RepartitionSourceTopics);
        writer.WriteEmptyTaggedFields();
    }

    private static void WriteIndexes(ref KafkaProtocolWriter writer, IReadOnlyList<short> indexes) =>
        writer.WriteCompactArray(
            indexes,
            static (ref KafkaProtocolWriter w, short item) => w.WriteInt16(item));
}

internal sealed class StreamsGroupHeartbeatKeyValue
{
    public required string Key { get; init; }
    public required string Value { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(Key);
        writer.WriteCompactString(Value);
        writer.WriteEmptyTaggedFields();
    }
}

internal sealed class StreamsGroupHeartbeatEndpoint
{
    public required string Host { get; init; }
    public ushort Port { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(Host);
        writer.WriteUInt16(Port);
        writer.WriteEmptyTaggedFields();
    }

    public static void WriteNullable(ref KafkaProtocolWriter writer, StreamsGroupHeartbeatEndpoint? endpoint)
    {
        if (endpoint is null)
        {
            writer.WriteInt8(-1);
            return;
        }

        writer.WriteInt8(1);
        endpoint.Write(ref writer);
    }

    public static StreamsGroupHeartbeatEndpoint Read(ref KafkaProtocolReader reader)
    {
        var endpoint = new StreamsGroupHeartbeatEndpoint
        {
            Host = reader.ReadCompactString() ?? string.Empty,
            Port = reader.ReadUInt16()
        };
        reader.SkipTaggedFields();
        return endpoint;
    }
}

internal sealed class StreamsGroupHeartbeatTaskOffset
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
}

internal sealed class StreamsGroupHeartbeatTaskIds
{
    public required string SubtopologyId { get; init; }
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(SubtopologyId);
        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, int item) => w.WriteInt32(item));
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsGroupHeartbeatTaskIds Read(ref KafkaProtocolReader reader)
    {
        var item = new StreamsGroupHeartbeatTaskIds
        {
            SubtopologyId = reader.ReadCompactString() ?? string.Empty,
            Partitions = reader.ReadCompactArray(
                static (ref KafkaProtocolReader r) => r.ReadInt32(),
                minElementSize: 4,
                maxCount: StreamsGroupHeartbeatResponse.MaxPartitionCount)
        };
        reader.SkipTaggedFields();
        return item;
    }
}
