using Dekaf.Admin;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    internal GroupDescription DescribeConsumerGroup(string groupId)
    {
        lock (_gate)
        {
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
