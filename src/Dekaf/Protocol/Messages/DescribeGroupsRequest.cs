namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeGroups request (API key 15).
/// Describes consumer groups.
/// </summary>
public sealed class DescribeGroupsRequest : IKafkaRequest<DescribeGroupsResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeGroups;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// The group IDs to describe.
    /// </summary>
    public required IReadOnlyList<string> Groups { get; init; }

    /// <summary>
    /// Whether to include authorized operations (v3+).
    /// </summary>
    public bool IncludeAuthorizedOperations { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 5;
    public static short GetRequestHeaderVersion(short version) => version >= 5 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 5 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 5;

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Groups,
                (ref KafkaProtocolWriter w, string g) => w.WriteCompactString(g));
        }
        else
        {
            writer.WriteArray(
                Groups,
                (ref KafkaProtocolWriter w, string g) => w.WriteString(g));
        }

        if (version >= 3)
        {
            writer.WriteBoolean(IncludeAuthorizedOperations);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
