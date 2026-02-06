namespace Dekaf.Protocol.Messages;

/// <summary>
/// Fetch request (API key 1).
/// Fetches records from topic partitions.
/// </summary>
public sealed class FetchRequest : IKafkaRequest<FetchResponse>
{
    public static ApiKey ApiKey => ApiKey.Fetch;
    public static short LowestSupportedVersion => 0;
    // Note: Fetch v13+ requires TopicId (UUID) which we don't track yet.
    // Limiting to v12 until TopicId support is implemented.
    public static short HighestSupportedVersion => 12;

    /// <summary>
    /// Cluster ID (v12+).
    /// </summary>
    public string? ClusterId { get; init; }

    /// <summary>
    /// Replica ID. -1 for normal consumers.
    /// </summary>
    public int ReplicaId { get; init; } = -1;

    /// <summary>
    /// Replica state (v15+).
    /// </summary>
    public ReplicaState? ReplicaState { get; init; }

    /// <summary>
    /// Maximum wait time in milliseconds.
    /// </summary>
    public required int MaxWaitMs { get; init; }

    /// <summary>
    /// Minimum bytes to return.
    /// </summary>
    public required int MinBytes { get; init; }

    /// <summary>
    /// Maximum bytes to return (v3+).
    /// </summary>
    public int MaxBytes { get; init; } = 0x7FFFFFFF;

    /// <summary>
    /// Isolation level (v4+).
    /// </summary>
    public IsolationLevel IsolationLevel { get; init; } = IsolationLevel.ReadUncommitted;

    /// <summary>
    /// Fetch session ID (v7+).
    /// </summary>
    public int SessionId { get; init; }

    /// <summary>
    /// Fetch session epoch (v7+).
    /// </summary>
    public int SessionEpoch { get; init; } = -1;

    /// <summary>
    /// Topics to fetch.
    /// </summary>
    public required IReadOnlyList<FetchRequestTopic> Topics { get; init; }

    /// <summary>
    /// Topics to forget from the session (v7+).
    /// </summary>
    public IReadOnlyList<ForgottenTopic>? ForgottenTopicsData { get; init; }

    /// <summary>
    /// Rack ID for rack-aware fetching (v11+).
    /// </summary>
    public string? RackId { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 12;
    public static short GetRequestHeaderVersion(short version) => version >= 12 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 12 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 12;

        if (version >= 15)
        {
            ReplicaState?.Write(ref writer, version);
        }
        else
        {
            writer.WriteInt32(ReplicaId);
        }

        writer.WriteInt32(MaxWaitMs);
        writer.WriteInt32(MinBytes);

        if (version >= 3)
        {
            writer.WriteInt32(MaxBytes);
        }

        if (version >= 4)
        {
            writer.WriteInt8((sbyte)IsolationLevel);
        }

        if (version >= 7)
        {
            writer.WriteInt32(SessionId);
            writer.WriteInt32(SessionEpoch);
        }

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Topics,
                (ref KafkaProtocolWriter w, FetchRequestTopic t) => t.Write(ref w, version));
        }
        else
        {
            writer.WriteArray(
                Topics,
                (ref KafkaProtocolWriter w, FetchRequestTopic t) => t.Write(ref w, version));
        }

        if (version >= 7)
        {
            var forgottenTopics = ForgottenTopicsData ?? [];
            if (isFlexible)
            {
                writer.WriteCompactArray(
                    forgottenTopics,
                    (ref KafkaProtocolWriter w, ForgottenTopic t) => t.Write(ref w, version));
            }
            else
            {
                writer.WriteArray(
                    forgottenTopics,
                    (ref KafkaProtocolWriter w, ForgottenTopic t) => t.Write(ref w, version));
            }
        }

        if (version >= 11)
        {
            if (isFlexible)
                writer.WriteCompactString(RackId ?? string.Empty);
            else
                writer.WriteString(RackId ?? string.Empty);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Topic in a fetch request.
/// </summary>
public sealed class FetchRequestTopic
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public string? Topic { get; init; }

    /// <summary>
    /// Topic ID (v13+).
    /// </summary>
    public Guid TopicId { get; init; }

    /// <summary>
    /// Partitions to fetch.
    /// </summary>
    public required IReadOnlyList<FetchRequestPartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 12;

        if (version >= 13)
        {
            writer.WriteUuid(TopicId);
        }
        else
        {
            if (isFlexible)
                writer.WriteCompactString(Topic);
            else
                writer.WriteString(Topic);
        }

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Partitions,
                (ref KafkaProtocolWriter w, FetchRequestPartition p) => p.Write(ref w, version));
        }
        else
        {
            writer.WriteArray(
                Partitions,
                (ref KafkaProtocolWriter w, FetchRequestPartition p) => p.Write(ref w, version));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Partition in a fetch request.
/// </summary>
public sealed class FetchRequestPartition
{
    /// <summary>
    /// Partition index.
    /// </summary>
    public required int Partition { get; init; }

    /// <summary>
    /// Current leader epoch (v9+).
    /// </summary>
    public int CurrentLeaderEpoch { get; init; } = -1;

    /// <summary>
    /// Fetch offset.
    /// </summary>
    public required long FetchOffset { get; set; }

    /// <summary>
    /// Last fetched epoch (v12+).
    /// </summary>
    public int LastFetchedEpoch { get; init; } = -1;

    /// <summary>
    /// Log start offset (v5+).
    /// </summary>
    public long LogStartOffset { get; init; } = -1;

    /// <summary>
    /// Maximum bytes to fetch for this partition.
    /// </summary>
    public required int PartitionMaxBytes { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 12;

        writer.WriteInt32(Partition);

        if (version >= 9)
        {
            writer.WriteInt32(CurrentLeaderEpoch);
        }

        writer.WriteInt64(FetchOffset);

        if (version >= 12)
        {
            writer.WriteInt32(LastFetchedEpoch);
        }

        if (version >= 5)
        {
            writer.WriteInt64(LogStartOffset);
        }

        writer.WriteInt32(PartitionMaxBytes);

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Topic to forget in a fetch session.
/// </summary>
public sealed class ForgottenTopic
{
    /// <summary>
    /// Topic name (v7-v12).
    /// </summary>
    public string? Topic { get; init; }

    /// <summary>
    /// Topic ID (v13+).
    /// </summary>
    public Guid TopicId { get; init; }

    /// <summary>
    /// Partitions to forget.
    /// </summary>
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 12;

        if (version >= 13)
        {
            writer.WriteUuid(TopicId);
        }
        else
        {
            if (isFlexible)
                writer.WriteCompactString(Topic);
            else
                writer.WriteString(Topic);
        }

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Partitions,
                (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
        }
        else
        {
            writer.WriteArray(
                Partitions,
                (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Replica state for follower fetching (v15+).
/// </summary>
public sealed class ReplicaState
{
    public int ReplicaId { get; init; } = -1;
    public long ReplicaEpoch { get; init; } = -1;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(ReplicaId);
        writer.WriteInt64(ReplicaEpoch);
        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Isolation level for reading records.
/// </summary>
public enum IsolationLevel : sbyte
{
    /// <summary>
    /// Read all records including uncommitted transactions.
    /// </summary>
    ReadUncommitted = 0,

    /// <summary>
    /// Only read committed records.
    /// </summary>
    ReadCommitted = 1
}
