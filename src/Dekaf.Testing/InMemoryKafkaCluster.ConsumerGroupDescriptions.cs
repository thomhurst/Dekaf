using Dekaf.Admin;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    internal GroupDescription DescribeConsumerGroup(string groupId)
    {
        lock (_gate)
        {
            if (_classicGroupDescriptions?.TryGetValue(groupId, out var snapshot) == true)
            {
                var classicMembers = new MemberDescription[snapshot.Members.Count];
                for (var i = 0; i < classicMembers.Length; i++)
                {
                    var member = snapshot.Members[i];
                    classicMembers[i] = new MemberDescription
                    {
                        MemberId = member.MemberId,
                        GroupInstanceId = member.GroupInstanceId,
                        ClientId = member.ClientId,
                        ClientHost = member.ClientHost,
                        Assignment = member.Assignment?.ToArray()
                    };
                }
                return new GroupDescription
                {
                    GroupId = groupId,
                    ProtocolType = snapshot.ProtocolType,
                    ProtocolData = snapshot.ProtocolData,
                    State = snapshot.State,
                    CoordinatorId = snapshot.CoordinatorId,
                    AuthorizedOperations = snapshot.AuthorizedOperations ?? int.MinValue,
                    Members = classicMembers
                };
            }

            var hasGeneration = _consumerGroupGenerations.TryGetValue(groupId, out var generation);
            var exists = hasGeneration || _consumerGroupOffsets.ContainsKey(groupId);
            var state = exists ? "Empty" : "Dead";
            var descriptions = new List<MemberDescription>();
            if (_consumerGroupMembers.TryGetValue(groupId, out var members))
            {
                if (members.Count > 0)
                    state = "Stable";
                var assignments = BuildConsumerGroupAssignments(groupId);
                foreach (var (memberId, member) in members)
                {
                    descriptions.Add(new MemberDescription
                    {
                        MemberId = memberId,
                        GroupInstanceId = member.GroupInstanceId,
                        MemberEpoch = generation,
                        Assignment = assignments[memberId].ToArray()
                    });
                }
            }

            return new GroupDescription
            {
                GroupId = groupId,
                ProtocolType = hasGeneration ? "consumer" : "",
                State = state,
                CoordinatorId = 0,
                GroupEpoch = hasGeneration ? generation : null,
                AssignmentEpoch = hasGeneration ? generation : null,
                Members = descriptions
            };
        }
    }
}
