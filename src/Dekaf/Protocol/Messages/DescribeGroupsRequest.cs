namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeGroups request (API key 15).
/// Describes consumer groups.
/// </summary>
public sealed class DescribeGroupsRequest : IKafkaRequest<DescribeGroupsResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeGroups;
    public static short LowestSupportedVersion => 5;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// The group IDs to describe.
    /// </summary>
    public required IReadOnlyList<string> Groups { get; init; }

    /// <summary>
    /// Whether to include authorized operations (v3+).
    /// </summary>
    public bool IncludeAuthorizedOperations { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            Groups,
            (ref KafkaProtocolWriter w, string g) => w.WriteCompactString(g));

        writer.WriteBoolean(IncludeAuthorizedOperations);

        writer.WriteEmptyTaggedFields();
    }
}
