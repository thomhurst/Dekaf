namespace Dekaf.Protocol.Messages;

/// <summary>
/// ShareGroupDescribe request (API key 77).
/// Describes share groups (KIP-932).
/// </summary>
public sealed class ShareGroupDescribeRequest : IKafkaRequest<ShareGroupDescribeResponse>
{
    public static ApiKey ApiKey => ApiKey.ShareGroupDescribe;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The group IDs to describe.
    /// </summary>
    public required IReadOnlyList<string> GroupIds { get; init; }

    /// <summary>
    /// Whether to include authorized operations.
    /// </summary>
    public bool IncludeAuthorizedOperations { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            GroupIds,
            static (ref KafkaProtocolWriter w, string groupId) => w.WriteCompactString(groupId));

        writer.WriteBoolean(IncludeAuthorizedOperations);

        writer.WriteEmptyTaggedFields();
    }
}
