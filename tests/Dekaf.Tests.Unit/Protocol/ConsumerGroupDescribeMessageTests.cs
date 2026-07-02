using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class ConsumerGroupDescribeMessageTests
{
    [Test]
    public async Task Request_Metadata_MatchesApi69()
    {
        await Assert.That(ConsumerGroupDescribeRequest.ApiKey).IsEqualTo(ApiKey.ConsumerGroupDescribe);
        await Assert.That(ConsumerGroupDescribeRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(ConsumerGroupDescribeRequest.HighestSupportedVersion).IsEqualTo((short)1);
    }

    [Test]
    public async Task Request_Write_V0_EncodesFlexibleSchema()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ConsumerGroupDescribeRequest
        {
            GroupIds = ["group-a", "group-b"],
            IncludeAuthorizedOperations = true
        };

        request.Write(ref writer, version: 0);

        IReadOnlyList<string> groupIds;
        bool includeAuthorizedOperations;
        long remaining;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            groupIds = reader.ReadCompactArray(
                static (ref KafkaProtocolReader r) => r.ReadCompactString() ?? string.Empty);
            includeAuthorizedOperations = reader.ReadBoolean();
            reader.SkipTaggedFields();
            remaining = reader.Remaining;
        }

        await Assert.That(groupIds).IsEquivalentTo(["group-a", "group-b"]);
        await Assert.That(includeAuthorizedOperations).IsTrue();
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task Response_Metadata_MatchesApi69()
    {
        await Assert.That(ConsumerGroupDescribeResponse.ApiKey).IsEqualTo(ApiKey.ConsumerGroupDescribe);
        await Assert.That(ConsumerGroupDescribeResponse.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(ConsumerGroupDescribeResponse.HighestSupportedVersion).IsEqualTo((short)1);
    }

    [Test]
    public async Task Response_Read_V0_ParsesMemberWithoutMemberType()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        WriteResponse(ref writer, version: 0);

        ConsumerGroupDescribeResponse response;
        long remaining;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            response = (ConsumerGroupDescribeResponse)ConsumerGroupDescribeResponse.Read(ref reader, version: 0);
            remaining = reader.Remaining;
        }

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(25);
        await Assert.That(response.Groups.Count).IsEqualTo(1);

        var group = response.Groups[0];
        await Assert.That(group.GroupId).IsEqualTo("consumer-group");
        await Assert.That(group.GroupState).IsEqualTo("Stable");
        await Assert.That(group.GroupEpoch).IsEqualTo(12);
        await Assert.That(group.AssignmentEpoch).IsEqualTo(9);
        await Assert.That(group.AssignorName).IsEqualTo("uniform");
        await Assert.That(group.AuthorizedOperations).IsEqualTo(0x1F);

        var member = group.Members[0];
        await Assert.That(member.MemberId).IsEqualTo("member-1");
        await Assert.That(member.InstanceId).IsEqualTo("instance-1");
        await Assert.That(member.RackId).IsEqualTo("rack-a");
        await Assert.That(member.MemberEpoch).IsEqualTo(7);
        await Assert.That(member.ClientId).IsEqualTo("client-1");
        await Assert.That(member.ClientHost).IsEqualTo("/127.0.0.1");
        await Assert.That(member.SubscribedTopicNames).IsEquivalentTo(["topic-a", "topic-b"]);
        await Assert.That(member.SubscribedTopicRegex).IsEqualTo("orders-.*");
        await Assert.That(member.MemberType).IsNull();

        await Assert.That(member.Assignment.TopicPartitions[0].TopicName).IsEqualTo("topic-a");
        await Assert.That(member.Assignment.TopicPartitions[0].Partitions).IsEquivalentTo([0, 1]);
        await Assert.That(member.TargetAssignment.TopicPartitions[0].TopicName).IsEqualTo("topic-b");
        await Assert.That(member.TargetAssignment.TopicPartitions[0].Partitions).IsEquivalentTo([2]);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task Response_Read_V1_ParsesMemberType()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        WriteResponse(ref writer, version: 1);

        ConsumerGroupDescribeResponse response;
        long remaining;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            response = (ConsumerGroupDescribeResponse)ConsumerGroupDescribeResponse.Read(ref reader, version: 1);
            remaining = reader.Remaining;
        }

        await Assert.That(response.Groups[0].Members[0].MemberType).IsEqualTo((sbyte)1);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task Response_Read_ErrorGroup_ParsesError()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteInt16((short)ErrorCode.GroupIdNotFound);
        writer.WriteCompactNullableString("missing");
        writer.WriteCompactString("missing-group");
        writer.WriteCompactString(string.Empty);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteCompactString(string.Empty);
        writer.WriteUnsignedVarInt(0 + 1);
        writer.WriteInt32(int.MinValue);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();

        ConsumerGroupDescribeResponse response;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            response = (ConsumerGroupDescribeResponse)ConsumerGroupDescribeResponse.Read(ref reader, version: 0);
        }

        await Assert.That(response.Groups[0].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(response.Groups[0].ErrorMessage).IsEqualTo("missing");
        await Assert.That(response.Groups[0].AuthorizedOperations).IsEqualTo(int.MinValue);
    }

    [Test]
    public async Task Member_WriteAndRead_V1_RoundTripsAllFields()
    {
        var topicId = Guid.NewGuid();
        var targetTopicId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ConsumerGroupDescribeMember
        {
            MemberId = "member-1",
            InstanceId = "instance-1",
            RackId = "rack-a",
            MemberEpoch = 7,
            ClientId = "client-1",
            ClientHost = "/127.0.0.1",
            SubscribedTopicNames = ["topic-a", "topic-b"],
            SubscribedTopicRegex = "orders-.*",
            Assignment = Assignment(topicId, "topic-a", [0, 1]),
            TargetAssignment = Assignment(targetTopicId, "topic-b", [2]),
            MemberType = 1
        };

        original.Write(ref writer, version: 1);

        ConsumerGroupDescribeMember deserialized;
        long remaining;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            deserialized = ConsumerGroupDescribeMember.Read(ref reader, version: 1);
            remaining = reader.Remaining;
        }

        await Assert.That(deserialized.MemberId).IsEqualTo(original.MemberId);
        await Assert.That(deserialized.InstanceId).IsEqualTo(original.InstanceId);
        await Assert.That(deserialized.RackId).IsEqualTo(original.RackId);
        await Assert.That(deserialized.MemberEpoch).IsEqualTo(original.MemberEpoch);
        await Assert.That(deserialized.ClientId).IsEqualTo(original.ClientId);
        await Assert.That(deserialized.ClientHost).IsEqualTo(original.ClientHost);
        await Assert.That(deserialized.SubscribedTopicNames).IsEquivalentTo(original.SubscribedTopicNames);
        await Assert.That(deserialized.SubscribedTopicRegex).IsEqualTo(original.SubscribedTopicRegex);
        await Assert.That(deserialized.Assignment.TopicPartitions[0].TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.TargetAssignment.TopicPartitions[0].TopicId).IsEqualTo(targetTopicId);
        await Assert.That(deserialized.MemberType).IsEqualTo((sbyte)1);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task Member_WriteAndRead_V0_DoesNotWriteMemberType()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ConsumerGroupDescribeMember
        {
            MemberId = "member-1",
            MemberEpoch = 1,
            ClientId = "client-1",
            ClientHost = "/127.0.0.1",
            SubscribedTopicNames = [],
            Assignment = EmptyAssignment(),
            TargetAssignment = EmptyAssignment(),
            MemberType = 1
        };

        original.Write(ref writer, version: 0);

        ConsumerGroupDescribeMember deserialized;
        long remaining;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            deserialized = ConsumerGroupDescribeMember.Read(ref reader, version: 0);
            remaining = reader.Remaining;
        }

        await Assert.That(deserialized.MemberType).IsNull();
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task Member_WriteAndRead_V1_NullMemberTypeDefaultsToUnknown()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ConsumerGroupDescribeMember
        {
            MemberId = "member-1",
            MemberEpoch = 1,
            ClientId = "client-1",
            ClientHost = "/127.0.0.1",
            SubscribedTopicNames = [],
            Assignment = EmptyAssignment(),
            TargetAssignment = EmptyAssignment()
        };

        original.Write(ref writer, version: 1);

        ConsumerGroupDescribeMember deserialized;
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            deserialized = ConsumerGroupDescribeMember.Read(ref reader, version: 1);
        }

        await Assert.That(deserialized.MemberType).IsEqualTo((sbyte)-1);
    }

    private static void WriteResponse(ref KafkaProtocolWriter writer, short version)
    {
        var topicId = Guid.NewGuid();
        var targetTopicId = Guid.NewGuid();

        writer.WriteInt32(25);
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteCompactString("consumer-group");
        writer.WriteCompactString("Stable");
        writer.WriteInt32(12);
        writer.WriteInt32(9);
        writer.WriteCompactString("uniform");
        writer.WriteUnsignedVarInt(1 + 1);

        writer.WriteCompactString("member-1");
        writer.WriteCompactNullableString("instance-1");
        writer.WriteCompactNullableString("rack-a");
        writer.WriteInt32(7);
        writer.WriteCompactString("client-1");
        writer.WriteCompactString("/127.0.0.1");
        writer.WriteUnsignedVarInt(2 + 1);
        writer.WriteCompactString("topic-a");
        writer.WriteCompactString("topic-b");
        writer.WriteCompactNullableString("orders-.*");
        WriteAssignment(ref writer, topicId, "topic-a", [0, 1]);
        WriteAssignment(ref writer, targetTopicId, "topic-b", [2]);
        if (version >= 1)
        {
            writer.WriteInt8(1);
        }
        writer.WriteEmptyTaggedFields();

        writer.WriteInt32(0x1F);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();
    }

    private static ConsumerGroupDescribeAssignment Assignment(
        Guid topicId,
        string topicName,
        IReadOnlyList<int> partitions) =>
        new()
        {
            TopicPartitions =
            [
                new ConsumerGroupDescribeTopicPartitions
                {
                    TopicId = topicId,
                    TopicName = topicName,
                    Partitions = partitions
                }
            ]
        };

    private static ConsumerGroupDescribeAssignment EmptyAssignment() =>
        new()
        {
            TopicPartitions = []
        };

    private static void WriteAssignment(
        ref KafkaProtocolWriter writer,
        Guid topicId,
        string topicName,
        IReadOnlyList<int> partitions)
    {
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteUuid(topicId);
        writer.WriteCompactString(topicName);
        writer.WriteCompactArray(
            partitions,
            static (ref KafkaProtocolWriter w, int partition) => w.WriteInt32(partition));
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();
    }
}
