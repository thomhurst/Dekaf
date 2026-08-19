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
        string? instanceId) =>
        _request ??= new StreamsGroupHeartbeatRequest
        {
            GroupId = groupId,
            MemberId = memberId,
            MemberEpoch = memberEpoch,
            EndpointInformationEpoch = endpointInformationEpoch,
            InstanceId = instanceId
        };

    internal void Invalidate() => _request = null;
}
