using Dekaf.Admin;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    // Only administrative Streams offset creation allocates this set. Consumer delivery
    // does not need to maintain a second per-message or per-membership registry.
    private HashSet<string>? _streamsGroupIds;

    internal IReadOnlyList<GroupListing> ListGroupInventory()
    {
        lock (_gate)
        {
            var ids = new HashSet<string>(_consumerGroupOffsets.Keys, StringComparer.Ordinal);
            ids.UnionWith(_consumerGroupGenerations.Keys);
            if (_streamsGroupIds is not null)
                ids.UnionWith(_streamsGroupIds);
            if (_classicGroupDescriptions is not null)
                ids.UnionWith(_classicGroupDescriptions.Keys);
            var result = new List<GroupListing>(ids.Count);
            foreach (var id in ids)
            {
                if (_classicGroupDescriptions?.TryGetValue(id, out var classic) == true)
                {
                    result.Add(new GroupListing
                    {
                        GroupId = id, GroupType = "classic", ProtocolType = classic.ProtocolType, State = classic.State
                    });
                    continue;
                }
                var streams = _streamsGroupIds?.Contains(id) == true;
                var consumer = _consumerGroupGenerations.ContainsKey(id);
                var active = _consumerGroupMembers.TryGetValue(id, out var members) && members.Count > 0;
                var (type, protocol) = (streams, consumer) switch
                {
                    (true, _) => ("streams", "streams"),
                    (_, true) => ("consumer", "consumer"),
                    _ => ("classic", "")
                };
                result.Add(new GroupListing
                {
                    GroupId = id,
                    GroupType = type,
                    ProtocolType = protocol,
                    State = active ? "Stable" : "Empty"
                });
            }
            foreach (var share in ListShareGroups())
            {
                if (!ids.Add(share.GroupId))
                    continue;
                result.Add(new GroupListing
                {
                    GroupId = share.GroupId, GroupType = "share", ProtocolType = "share",
                    State = share.HasActiveMembers ? "Stable" : "Empty"
                });
            }
            result.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.GroupId, right.GroupId));
            return result;
        }
    }
}
