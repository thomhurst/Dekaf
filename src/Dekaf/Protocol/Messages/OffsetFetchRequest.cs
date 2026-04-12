namespace Dekaf.Protocol.Messages;

/// <summary>
/// OffsetFetch request (API key 9).
/// Fetches committed offsets for a consumer group.
/// </summary>
public sealed class OffsetFetchRequest : IKafkaRequest<OffsetFetchResponse>
{
    public static ApiKey ApiKey => ApiKey.OffsetFetch;
    public static short LowestSupportedVersion => 6;
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

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (version < 8)
        {
            writer.WriteCompactString(GroupId);

            if (Topics is null)
            {
                writer.WriteUnsignedVarInt(0);
            }
            else
            {
                writer.WriteCompactArray(
                    Topics,
                    static (ref KafkaProtocolWriter w, OffsetFetchRequestTopic t, short v) => t.Write(ref w, v),
                    version);
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
                static (ref KafkaProtocolWriter w, OffsetFetchRequestGroup g, short v) => g.Write(ref w, v),
                version);

            writer.WriteBoolean(RequireStable);
        }

        writer.WriteEmptyTaggedFields();
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
        writer.WriteCompactString(Name);

        writer.WriteCompactArray(
            PartitionIndexes,
            (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));

        writer.WriteEmptyTaggedFields();
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
                static (ref KafkaProtocolWriter w, OffsetFetchRequestTopic t, short v) => t.Write(ref w, v),
                version);
        }

        writer.WriteEmptyTaggedFields();
    }
}
