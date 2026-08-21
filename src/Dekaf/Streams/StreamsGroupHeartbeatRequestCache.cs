using Dekaf.Protocol.Messages;

namespace Dekaf.Streams;

internal sealed class StreamsGroupHeartbeatRequestCache
{
    private StreamsGroupHeartbeatRequest? _request;

    internal StreamsGroupHeartbeatRequest Get(
        string groupId,
        string memberId,
        int memberEpoch,
        int endpointInformationEpoch,
        string? instanceId)
    {
        var request = _request;
        if (request is not null
            && request.MemberEpoch == memberEpoch
            && request.EndpointInformationEpoch == endpointInformationEpoch
            && string.Equals(request.GroupId, groupId, StringComparison.Ordinal)
            && string.Equals(request.MemberId, memberId, StringComparison.Ordinal)
            && string.Equals(request.InstanceId, instanceId, StringComparison.Ordinal))
        {
            return request;
        }

        return _request = new StreamsGroupHeartbeatRequest
        {
            GroupId = groupId,
            MemberId = memberId,
            MemberEpoch = memberEpoch,
            EndpointInformationEpoch = endpointInformationEpoch,
            InstanceId = instanceId
        };
    }

    internal void Invalidate() => _request = null;
}
