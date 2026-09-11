using Dekaf.Admin;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    private Dictionary<string, ClassicGroupDescription>? _classicGroupDescriptions;

    /// <summary>
    /// Seeds an administrative classic-group snapshot, including Connect/custom protocols.
    /// Raw AssignmentData determines decoded consumer assignments. This does not simulate membership coordination.
    /// </summary>
    public void SetClassicGroupDescription(ClassicGroupDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(description.GroupId);
        ArgumentNullException.ThrowIfNull(description.State);
        ArgumentNullException.ThrowIfNull(description.Members);
        var snapshot = CopyClassicGroupDescription(description, true);
        lock (_gate)
        {
            _classicGroupDescriptions ??= new(StringComparer.Ordinal);
            _classicGroupDescriptions[description.GroupId] = snapshot.Description!;
        }
    }

    internal ClassicGroupDescriptionResult DescribeClassicGroup(string groupId, bool includeAuthorizedOperations)
    {
        lock (_gate)
        {
            if (_classicGroupDescriptions?.TryGetValue(groupId, out var snapshot) == true)
                return CopyClassicGroupDescription(snapshot, includeAuthorizedOperations);
            if (!_consumerGroupOffsets.ContainsKey(groupId) || _consumerGroupGenerations.ContainsKey(groupId) ||
                _streamsGroupIds?.Contains(groupId) == true)
                return AdminClient.ClassicGroupError(groupId, ErrorCode.GroupIdNotFound);
            return new ClassicGroupDescriptionResult
            {
                GroupId = groupId,
                Description = new ClassicGroupDescription
                {
                    GroupId = groupId, ProtocolType = "", ProtocolData = "", State = "Empty",
                    CoordinatorId = 0, Members = []
                }
            };
        }
    }

    private static ClassicGroupDescriptionResult CopyClassicGroupDescription(ClassicGroupDescription source,
        bool includeAuthorizedOperations)
    {
        var members = new DescribeGroupsResponseMember[source.Members.Count];
        for (var i = 0; i < members.Length; i++)
        {
            var member = source.Members[i];
            ArgumentNullException.ThrowIfNull(member);
            ArgumentNullException.ThrowIfNull(member.MemberId);
            members[i] = new DescribeGroupsResponseMember
            {
                MemberId = member.MemberId, GroupInstanceId = member.GroupInstanceId,
                ClientId = member.ClientId, ClientHost = member.ClientHost,
                MemberMetadata = member.Metadata.ToArray(), MemberAssignment = member.AssignmentData.ToArray()
            };
        }
        return AdminClient.MapClassicGroup(new DescribeGroupsResponseGroup
        {
            GroupId = source.GroupId, GroupState = source.State, ProtocolType = source.ProtocolType,
            ProtocolData = source.ProtocolData, Members = members,
            AuthorizedOperations = source.AuthorizedOperations ?? int.MinValue
        }, source.CoordinatorId, includeAuthorizedOperations);
    }
}
