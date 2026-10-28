namespace Dekaf.Protocol.Messages;

/// <summary>
/// StreamsGroupDescribe request (API key 89).
/// Describes streams groups (KIP-1071). Version 1 can request the full topology description (KIP-1331).
/// </summary>
public sealed class StreamsGroupDescribeRequest : IKafkaRequest<StreamsGroupDescribeResponse>
{
    public static ApiKey ApiKey => ApiKey.StreamsGroupDescribe;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The group IDs to describe.
    /// </summary>
    public required IReadOnlyList<string> GroupIds { get; init; }

    /// <summary>
    /// Whether to include authorized operations.
    /// </summary>
    public bool IncludeAuthorizedOperations { get; init; }

    /// <summary>
    /// Whether to include the full topology description from the broker's topology-description plugin (v1+).
    /// </summary>
    public bool IncludeTopologyDescription { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            GroupIds,
            static (ref KafkaProtocolWriter w, string groupId) => w.WriteCompactString(groupId));

        writer.WriteBoolean(IncludeAuthorizedOperations);

        if (version >= 1)
        {
            writer.WriteBoolean(IncludeTopologyDescription);
        }

        writer.WriteEmptyTaggedFields();
    }
}
