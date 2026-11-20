using Dekaf.Streams;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    private Dictionary<string, InMemoryStreamsGroup>? _streamsGroups;

    internal InMemoryStreamsGroup GetOrCreateStreamsGroup(string groupId)
    {
        lock (_gate)
        {
            _streamsGroups ??= new Dictionary<string, InMemoryStreamsGroup>(StringComparer.Ordinal);
            if (!_streamsGroups.TryGetValue(groupId, out var group))
            {
                group = new InMemoryStreamsGroup();
                _streamsGroups.Add(groupId, group);
            }

            return group;
        }
    }
}

/// <summary>
/// Membership and topology of one in-memory Streams group. The instance is also the lock shared by every
/// member of the group, so a rebalance can update all of their assignments atomically.
/// </summary>
internal sealed class InMemoryStreamsGroup
{
    /// <summary>Members that hold a place in the group, in join order.</summary>
    public List<InMemoryStreamsGroupMember> Members
    {
        get;
    } = [];

    /// <summary>The most recent topology a member supplied.</summary>
    public StreamsGroupTopology? Topology
    {
        get; set;
    }

    public int NextMemberNumber
    {
        get; set;
    }
}
