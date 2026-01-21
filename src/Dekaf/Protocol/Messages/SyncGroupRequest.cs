namespace Dekaf.Protocol.Messages;

/// <summary>
/// SyncGroup request (API key 14).
/// Synchronizes group state after a rebalance.
/// </summary>
public sealed class SyncGroupRequest : IKafkaRequest<SyncGroupResponse>
{
    public static ApiKey ApiKey => ApiKey.SyncGroup;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Generation ID.
    /// </summary>
    public required int GenerationId { get; init; }

    /// <summary>
    /// Member ID.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// Group instance ID for static membership (v3+).
    /// </summary>
    public string? GroupInstanceId { get; init; }

    /// <summary>
    /// Protocol type (v5+).
    /// </summary>
    public string? ProtocolType { get; init; }

    /// <summary>
    /// Protocol name (v5+).
    /// </summary>
    public string? ProtocolName { get; init; }

    /// <summary>
    /// Assignments from the leader.
    /// </summary>
    public required IReadOnlyList<SyncGroupRequestAssignment> Assignments { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 4;
    public static short GetRequestHeaderVersion(short version) => version >= 4 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 4 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 4;

        if (isFlexible)
            writer.WriteCompactString(GroupId);
        else
            writer.WriteString(GroupId);

        writer.WriteInt32(GenerationId);

        if (isFlexible)
            writer.WriteCompactString(MemberId);
        else
            writer.WriteString(MemberId);

        if (version >= 3)
        {
            if (isFlexible)
                writer.WriteCompactNullableString(GroupInstanceId);
            else
                writer.WriteString(GroupInstanceId);
        }

        if (version >= 5)
        {
            writer.WriteCompactNullableString(ProtocolType);
            writer.WriteCompactNullableString(ProtocolName);
        }

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Assignments.ToArray().AsSpan(),
                (ref KafkaProtocolWriter w, SyncGroupRequestAssignment a) => a.Write(ref w, version));
        }
        else
        {
            writer.WriteArray(
                Assignments.ToArray().AsSpan(),
                (ref KafkaProtocolWriter w, SyncGroupRequestAssignment a) => a.Write(ref w, version));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Assignment in a SyncGroup request.
/// </summary>
public sealed class SyncGroupRequestAssignment
{
    /// <summary>
    /// Member ID.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// Assignment data.
    /// </summary>
    public required byte[] Assignment { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 4;

        if (isFlexible)
        {
            writer.WriteCompactString(MemberId);
            writer.WriteCompactBytes(Assignment);
            writer.WriteEmptyTaggedFields();
        }
        else
        {
            writer.WriteString(MemberId);
            writer.WriteBytes(Assignment);
        }
    }
}
