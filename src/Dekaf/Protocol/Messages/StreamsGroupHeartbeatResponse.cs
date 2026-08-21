namespace Dekaf.Protocol.Messages;

/// <summary>
/// StreamsGroupHeartbeat response (API key 88), introduced by KIP-1071.
/// </summary>
internal sealed class StreamsGroupHeartbeatResponse : IKafkaResponse
{
    internal const int MaxStatusCount = 100_000;
    internal const int MaxTaskCount = 1_000_000;
    internal const int MaxEndpointCount = 100_000;
    internal const int MaxTopicCount = 1_000_000;
    internal const int MaxPartitionCount = 1_000_000;

    public static ApiKey ApiKey => ApiKey.StreamsGroupHeartbeat;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public int ThrottleTimeMs { get; init; }
    public required ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public required string MemberId { get; init; }
    public int MemberEpoch { get; init; }
    public int HeartbeatIntervalMs { get; init; }
    public int AcceptableRecoveryLag { get; init; }
    public int TaskOffsetIntervalMs { get; init; }
    public IReadOnlyList<StreamsGroupHeartbeatStatus>? Status { get; init; }
    public IReadOnlyList<StreamsGroupHeartbeatTaskIds>? ActiveTasks { get; init; }
    public IReadOnlyList<StreamsGroupHeartbeatTaskIds>? StandbyTasks { get; init; }
    public IReadOnlyList<StreamsGroupHeartbeatTaskIds>? WarmupTasks { get; init; }
    public int EndpointInformationEpoch { get; init; }
    public IReadOnlyList<StreamsGroupHeartbeatEndpointPartitions>? PartitionsByUserEndpoint { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(ThrottleTimeMs);
        writer.WriteInt16((short)ErrorCode);
        writer.WriteCompactNullableString(ErrorMessage);
        writer.WriteCompactString(MemberId);
        writer.WriteInt32(MemberEpoch);
        writer.WriteInt32(HeartbeatIntervalMs);
        writer.WriteInt32(AcceptableRecoveryLag);
        writer.WriteInt32(TaskOffsetIntervalMs);
        writer.WriteCompactNullableArray(
            Status,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatStatus item) => item.Write(ref w));
        WriteNullableTasks(ref writer, ActiveTasks);
        WriteNullableTasks(ref writer, StandbyTasks);
        WriteNullableTasks(ref writer, WarmupTasks);
        writer.WriteInt32(EndpointInformationEpoch);
        writer.WriteCompactNullableArray(
            PartitionsByUserEndpoint,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatEndpointPartitions item) => item.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var response = new StreamsGroupHeartbeatResponse
        {
            ThrottleTimeMs = reader.ReadInt32(),
            ErrorCode = (ErrorCode)reader.ReadInt16(),
            ErrorMessage = reader.ReadCompactString(),
            MemberId = reader.ReadCompactString() ?? string.Empty,
            MemberEpoch = reader.ReadInt32(),
            HeartbeatIntervalMs = reader.ReadInt32(),
            AcceptableRecoveryLag = reader.ReadInt32(),
            TaskOffsetIntervalMs = reader.ReadInt32(),
            Status = reader.ReadCompactNullableArray(
                static (ref KafkaProtocolReader r) => StreamsGroupHeartbeatStatus.Read(ref r),
                minElementSize: 3,
                maxCount: MaxStatusCount),
            ActiveTasks = ReadNullableTasks(ref reader),
            StandbyTasks = ReadNullableTasks(ref reader),
            WarmupTasks = ReadNullableTasks(ref reader),
            EndpointInformationEpoch = reader.ReadInt32(),
            PartitionsByUserEndpoint = reader.ReadCompactNullableArray(
                static (ref KafkaProtocolReader r) => StreamsGroupHeartbeatEndpointPartitions.Read(ref r),
                minElementSize: 7,
                maxCount: MaxEndpointCount)
        };
        reader.SkipTaggedFields();
        return response;
    }

    private static void WriteNullableTasks(
        ref KafkaProtocolWriter writer,
        IReadOnlyList<StreamsGroupHeartbeatTaskIds>? tasks) =>
        writer.WriteCompactNullableArray(
            tasks,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatTaskIds item) => item.Write(ref w));

    private static StreamsGroupHeartbeatTaskIds[]? ReadNullableTasks(ref KafkaProtocolReader reader) =>
        reader.ReadCompactNullableArray(
            static (ref KafkaProtocolReader r) => StreamsGroupHeartbeatTaskIds.Read(ref r),
            minElementSize: 3,
            maxCount: MaxTaskCount);
}

internal sealed class StreamsGroupHeartbeatStatus
{
    public sbyte StatusCode { get; init; }
    public required string StatusDetail { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt8(StatusCode);
        writer.WriteCompactString(StatusDetail);
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsGroupHeartbeatStatus Read(ref KafkaProtocolReader reader)
    {
        var item = new StreamsGroupHeartbeatStatus
        {
            StatusCode = reader.ReadInt8(),
            StatusDetail = reader.ReadCompactString() ?? string.Empty
        };
        reader.SkipTaggedFields();
        return item;
    }
}

internal sealed class StreamsGroupHeartbeatEndpointPartitions
{
    public required StreamsGroupHeartbeatEndpoint UserEndpoint { get; init; }
    public required IReadOnlyList<StreamsGroupHeartbeatTopicPartitions> ActivePartitions { get; init; }
    public required IReadOnlyList<StreamsGroupHeartbeatTopicPartitions> StandbyPartitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        UserEndpoint.Write(ref writer);
        WriteTopicPartitions(ref writer, ActivePartitions);
        WriteTopicPartitions(ref writer, StandbyPartitions);
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsGroupHeartbeatEndpointPartitions Read(ref KafkaProtocolReader reader)
    {
        var item = new StreamsGroupHeartbeatEndpointPartitions
        {
            UserEndpoint = StreamsGroupHeartbeatEndpoint.Read(ref reader),
            ActivePartitions = ReadTopicPartitions(ref reader),
            StandbyPartitions = ReadTopicPartitions(ref reader)
        };
        reader.SkipTaggedFields();
        return item;
    }

    private static void WriteTopicPartitions(
        ref KafkaProtocolWriter writer,
        IReadOnlyList<StreamsGroupHeartbeatTopicPartitions> partitions) =>
        writer.WriteCompactArray(
            partitions,
            static (ref KafkaProtocolWriter w, StreamsGroupHeartbeatTopicPartitions item) => item.Write(ref w));

    private static StreamsGroupHeartbeatTopicPartitions[] ReadTopicPartitions(ref KafkaProtocolReader reader) =>
        reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsGroupHeartbeatTopicPartitions.Read(ref r),
            minElementSize: 3,
            maxCount: StreamsGroupHeartbeatResponse.MaxTopicCount);
}

internal sealed class StreamsGroupHeartbeatTopicPartitions
{
    public required string Topic { get; init; }
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(Topic);
        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, int item) => w.WriteInt32(item));
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsGroupHeartbeatTopicPartitions Read(ref KafkaProtocolReader reader)
    {
        var item = new StreamsGroupHeartbeatTopicPartitions
        {
            Topic = reader.ReadCompactString() ?? string.Empty,
            Partitions = reader.ReadCompactArray(
                static (ref KafkaProtocolReader r) => r.ReadInt32(),
                minElementSize: 4,
                maxCount: StreamsGroupHeartbeatResponse.MaxPartitionCount)
        };
        reader.SkipTaggedFields();
        return item;
    }
}
