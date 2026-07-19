using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class LeaveGroupMessageTests
{
    [Test]
    public async Task Request_Write_V3_EncodesStaticMembers()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = CreateRequest();

        request.Write(ref writer, version: 3);

        string? groupId;
        IReadOnlyList<(string? MemberId, string? GroupInstanceId)> members;
        long remaining;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            groupId = reader.ReadString();
            members = reader.ReadArray(static (ref KafkaProtocolReader r) =>
                (r.ReadString(), r.ReadString()));
            remaining = reader.Remaining;
        }

        await Assert.That(groupId).IsEqualTo("orders");
        await Assert.That(members.Count).IsEqualTo(2);
        await Assert.That(members[0]).IsEqualTo(("", "worker-a"));
        await Assert.That(members[1]).IsEqualTo(("member-b", "worker-b"));
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task Request_Write_V5_EncodesReasonAndTaggedFields()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        var request = CreateRequest();

        request.Write(ref writer, version: 5);

        string? groupId;
        IReadOnlyList<(string? MemberId, string? GroupInstanceId, string? Reason)> members;
        long remaining;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            groupId = reader.ReadCompactString();
            members = reader.ReadCompactArray(static (ref KafkaProtocolReader r) =>
            {
                var member = (r.ReadCompactString(), r.ReadCompactString(), r.ReadCompactString());
                r.SkipTaggedFields();
                return member;
            });
            reader.SkipTaggedFields();
            remaining = reader.Remaining;
        }

        await Assert.That(groupId).IsEqualTo("orders");
        await Assert.That(members[0]).IsEqualTo(("", "worker-a", "stale deployment"));
        await Assert.That(members[1]).IsEqualTo(("member-b", "worker-b", "stale deployment"));
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    [Arguments((short)3)]
    [Arguments((short)5)]
    public async Task Response_Read_ParsesPerMemberErrors(short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        WriteResponse(ref writer, version);

        LeaveGroupResponse response;
        long remaining;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            response = (LeaveGroupResponse)LeaveGroupResponse.Read(ref reader, version);
            remaining = reader.Remaining;
        }

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(12);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Members.Count).IsEqualTo(2);
        await Assert.That(response.Members[0].GroupInstanceId).IsEqualTo("worker-a");
        await Assert.That(response.Members[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Members[1].GroupInstanceId).IsEqualTo("worker-b");
        await Assert.That(response.Members[1].ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
        await Assert.That(remaining).IsEqualTo(0);
    }

    private static LeaveGroupRequest CreateRequest() => new()
    {
        GroupId = "orders",
        Members =
        [
            new LeaveGroupRequestMember
            {
                GroupInstanceId = "worker-a",
                Reason = "stale deployment"
            },
            new LeaveGroupRequestMember
            {
                MemberId = "member-b",
                GroupInstanceId = "worker-b",
                Reason = "stale deployment"
            }
        ]
    };

    private static void WriteResponse(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(12);
        writer.WriteInt16((short)ErrorCode.None);
        var members = new[]
        {
            (MemberId: "", GroupInstanceId: "worker-a", ErrorCode: ErrorCode.None),
            (MemberId: "member-b", GroupInstanceId: "worker-b", ErrorCode: ErrorCode.UnknownMemberId)
        };

        if (LeaveGroupRequest.IsFlexibleVersion(version))
        {
            writer.WriteCompactArray(members, static (ref KafkaProtocolWriter w, (string MemberId, string GroupInstanceId, ErrorCode ErrorCode) member) =>
            {
                w.WriteCompactString(member.MemberId);
                w.WriteCompactNullableString(member.GroupInstanceId);
                w.WriteInt16((short)member.ErrorCode);
                w.WriteEmptyTaggedFields();
            });
            writer.WriteEmptyTaggedFields();
            return;
        }

        writer.WriteArray(members, static (ref KafkaProtocolWriter w, (string MemberId, string GroupInstanceId, ErrorCode ErrorCode) member) =>
        {
            w.WriteString(member.MemberId);
            w.WriteString(member.GroupInstanceId);
            w.WriteInt16((short)member.ErrorCode);
        });
    }
}
