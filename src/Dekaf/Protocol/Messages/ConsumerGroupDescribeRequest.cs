namespace Dekaf.Protocol.Messages;

/// <summary>
/// ConsumerGroupDescribe request (API key 69).
/// Describes KIP-848 consumer groups.
/// </summary>
public sealed class ConsumerGroupDescribeRequest : IKafkaRequest<ConsumerGroupDescribeResponse>
{
    public static ApiKey ApiKey => ApiKey.ConsumerGroupDescribe;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The consumer group IDs to describe.
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
