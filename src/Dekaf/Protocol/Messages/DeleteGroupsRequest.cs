namespace Dekaf.Protocol.Messages;

/// <summary>
/// DeleteGroups request (API key 42).
/// Deletes consumer groups.
/// </summary>
public sealed class DeleteGroupsRequest : IKafkaRequest<DeleteGroupsResponse>
{
    public static ApiKey ApiKey => ApiKey.DeleteGroups;
    public static short LowestSupportedVersion => 2;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The group IDs to delete.
    /// </summary>
    public required IReadOnlyList<string> GroupsNames { get; init; }

    public static bool IsFlexibleVersion(short version) => true;
    public static short GetRequestHeaderVersion(short version) => 2;
    public static short GetResponseHeaderVersion(short version) => 1;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        if (isFlexible)
        {
            writer.WriteCompactArray(
                GroupsNames,
                (ref KafkaProtocolWriter w, string g) => w.WriteCompactString(g));
        }
        else
        {
            writer.WriteArray(
                GroupsNames,
                (ref KafkaProtocolWriter w, string g) => w.WriteString(g));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
