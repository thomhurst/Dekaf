namespace Dekaf.Protocol.Messages;

/// <summary>
/// ListGroups request (API key 16).
/// Lists the groups on a broker.
/// </summary>
public sealed class ListGroupsRequest : IKafkaRequest<ListGroupsResponse>
{
    public static ApiKey ApiKey => ApiKey.ListGroups;
    public static short LowestSupportedVersion => 3;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// Filter groups by state (v4+). Null means no filter.
    /// </summary>
    public IReadOnlyList<string>? StatesFilter { get; init; }

    /// <summary>
    /// Filter groups by type (v5+). Null means no filter.
    /// </summary>
    public IReadOnlyList<string>? TypesFilter { get; init; }

    public static bool IsFlexibleVersion(short version) => true;
    public static short GetRequestHeaderVersion(short version) => 2;
    public static short GetResponseHeaderVersion(short version) => 1;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 3;

        if (version >= 4)
        {
            var states = StatesFilter ?? [];
            writer.WriteCompactArray(
                states,
                (ref KafkaProtocolWriter w, string s) => w.WriteCompactString(s));
        }

        if (version >= 5)
        {
            var types = TypesFilter ?? [];
            writer.WriteCompactArray(
                types,
                (ref KafkaProtocolWriter w, string t) => w.WriteCompactString(t));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
