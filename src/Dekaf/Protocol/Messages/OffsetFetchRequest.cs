namespace Dekaf.Protocol.Messages;

/// <summary>
/// OffsetFetch request (API key 9).
/// Fetches committed offsets for a consumer group.
/// </summary>
public sealed class OffsetFetchRequest : IKafkaRequest<OffsetFetchResponse>
{
    public static ApiKey ApiKey => ApiKey.OffsetFetch;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 9;

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Topics to fetch offsets for. Null means all topics.
    /// </summary>
    public IReadOnlyList<OffsetFetchRequestTopic>? Topics { get; init; }

    /// <summary>
    /// Multiple groups (v8+).
    /// </summary>
    public IReadOnlyList<OffsetFetchRequestGroup>? Groups { get; init; }

    /// <summary>
    /// Whether to require stable offsets (v7+).
    /// </summary>
    public bool RequireStable { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 6;
    public static short GetRequestHeaderVersion(short version) => version >= 6 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 6 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 6;

        if (version < 8)
        {
            if (isFlexible)
                writer.WriteCompactString(GroupId);
            else
                writer.WriteString(GroupId);

            if (Topics is null)
            {
                if (isFlexible)
                    writer.WriteUnsignedVarInt(0);
                else if (version >= 2)
                    writer.WriteInt32(-1);
                else
                    writer.WriteInt32(0);
            }
            else if (isFlexible)
            {
                writer.WriteCompactArray(
                    Topics,
                    (ref KafkaProtocolWriter w, OffsetFetchRequestTopic t) => t.Write(ref w, version));
            }
            else
            {
                writer.WriteArray(
                    Topics,
                    (ref KafkaProtocolWriter w, OffsetFetchRequestTopic t) => t.Write(ref w, version));
            }

            if (version >= 7)
            {
                writer.WriteBoolean(RequireStable);
            }
        }
        else
        {
            // v8+ uses Groups array
            var groups = Groups ?? [new OffsetFetchRequestGroup
            {
                GroupId = GroupId,
                Topics = Topics,
                MemberEpoch = -1,
                MemberId = null
            }];

            writer.WriteCompactArray(
                groups,
                (ref KafkaProtocolWriter w, OffsetFetchRequestGroup g) => g.Write(ref w, version));

            writer.WriteBoolean(RequireStable);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Topic in an OffsetFetch request.
/// </summary>
public sealed class OffsetFetchRequestTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<int> PartitionIndexes { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 6;

        if (isFlexible)
            writer.WriteCompactString(Name);
        else
            writer.WriteString(Name);

        if (isFlexible)
        {
            writer.WriteCompactArray(
                PartitionIndexes,
                (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
        }
        else
        {
            writer.WriteArray(
                PartitionIndexes,
                (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Group in an OffsetFetch request (v8+).
/// </summary>
public sealed class OffsetFetchRequestGroup
{
    public required string GroupId { get; init; }
    public string? MemberId { get; init; }
    public int MemberEpoch { get; init; } = -1;
    public IReadOnlyList<OffsetFetchRequestTopic>? Topics { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(GroupId);

        if (version >= 9)
        {
            writer.WriteCompactNullableString(MemberId);
            writer.WriteInt32(MemberEpoch);
        }

        if (Topics is null)
        {
            writer.WriteUnsignedVarInt(0);
        }
        else
        {
            writer.WriteCompactArray(
                Topics,
                (ref KafkaProtocolWriter w, OffsetFetchRequestTopic t) => t.Write(ref w, version));
        }

        writer.WriteEmptyTaggedFields();
    }
}
