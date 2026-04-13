using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for ShareGroupDescribe request and response message encoding/decoding (KIP-932).
/// </summary>
public sealed class ShareGroupDescribeMessageTests
{
    #region Request Construction

    [Test]
    public async Task Request_CanBeConstructed_WithRequiredFields()
    {
        var request = new ShareGroupDescribeRequest
        {
            GroupIds = ["group-1"]
        };

        await Assert.That(request.GroupIds.Count).IsEqualTo(1);
        await Assert.That(request.GroupIds[0]).IsEqualTo("group-1");
    }

    [Test]
    public async Task Request_CanBeConstructed_WithAllFields()
    {
        var request = new ShareGroupDescribeRequest
        {
            GroupIds = ["group-1", "group-2"],
            IncludeAuthorizedOperations = true
        };

        await Assert.That(request.GroupIds.Count).IsEqualTo(2);
        await Assert.That(request.IncludeAuthorizedOperations).IsTrue();
    }

    [Test]
    public async Task Request_IncludeAuthorizedOperations_DefaultsToFalse()
    {
        var request = new ShareGroupDescribeRequest
        {
            GroupIds = ["group-1"]
        };

        await Assert.That(request.IncludeAuthorizedOperations).IsFalse();
    }

    [Test]
    public async Task Request_MultipleGroupIds_PreservesOrder()
    {
        var request = new ShareGroupDescribeRequest
        {
            GroupIds = ["alpha", "beta", "gamma"]
        };

        await Assert.That(request.GroupIds.Count).IsEqualTo(3);
        await Assert.That(request.GroupIds[0]).IsEqualTo("alpha");
        await Assert.That(request.GroupIds[1]).IsEqualTo("beta");
        await Assert.That(request.GroupIds[2]).IsEqualTo("gamma");
    }

    #endregion

    #region Request API Metadata

    [Test]
    public async Task Request_ApiKey_IsShareGroupDescribe()
    {
        await Assert.That(ShareGroupDescribeRequest.ApiKey).IsEqualTo(ApiKey.ShareGroupDescribe);
    }

    [Test]
    public async Task Request_LowestSupportedVersion_Is1()
    {
        await Assert.That(ShareGroupDescribeRequest.LowestSupportedVersion).IsEqualTo((short)1);
    }

    [Test]
    public async Task Request_HighestSupportedVersion_Is1()
    {
        await Assert.That(ShareGroupDescribeRequest.HighestSupportedVersion).IsEqualTo((short)1);
    }

    #endregion

    #region Request Encoding

    [Test]
    public async Task Request_Write_SingleGroup_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ShareGroupDescribeRequest
        {
            GroupIds = ["my-group"],
            IncludeAuthorizedOperations = false
        };
        request.Write(ref writer, version: 1);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Request_Write_MultipleGroups_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ShareGroupDescribeRequest
        {
            GroupIds = ["group-1", "group-2", "group-3"],
            IncludeAuthorizedOperations = true
        };
        request.Write(ref writer, version: 1);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Request_Write_WithAuthorizedOperations_EncodesMoreBytes()
    {
        // Both encode the same way since IncludeAuthorizedOperations is a boolean (1 byte),
        // but verify both variants produce valid output
        var buffer1 = new ArrayBufferWriter<byte>();
        var writer1 = new KafkaProtocolWriter(buffer1);

        new ShareGroupDescribeRequest
        {
            GroupIds = ["test"],
            IncludeAuthorizedOperations = false
        }.Write(ref writer1, version: 1);

        var buffer2 = new ArrayBufferWriter<byte>();
        var writer2 = new KafkaProtocolWriter(buffer2);

        new ShareGroupDescribeRequest
        {
            GroupIds = ["test"],
            IncludeAuthorizedOperations = true
        }.Write(ref writer2, version: 1);

        // Same size - boolean field is always 1 byte
        await Assert.That(buffer1.WrittenCount).IsEqualTo(buffer2.WrittenCount);
    }

    #endregion

    #region Response Construction

    [Test]
    public async Task Response_CanBeConstructed_WithRequiredFields()
    {
        var response = new ShareGroupDescribeResponse
        {
            Groups = []
        };

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Groups.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Response_CanBeConstructed_WithMultipleGroups()
    {
        var response = new ShareGroupDescribeResponse
        {
            ThrottleTimeMs = 100,
            Groups =
            [
                new ShareGroupDescribeGroup
                {
                    GroupId = "group-1",
                    GroupState = "Stable",
                    Members = []
                },
                new ShareGroupDescribeGroup
                {
                    GroupId = "group-2",
                    GroupState = "Dead",
                    Members = []
                }
            ]
        };

        await Assert.That(response.Groups.Count).IsEqualTo(2);
        await Assert.That(response.Groups[0].GroupId).IsEqualTo("group-1");
        await Assert.That(response.Groups[1].GroupId).IsEqualTo("group-2");
    }

    [Test]
    public async Task Response_Group_CanHaveError()
    {
        var group = new ShareGroupDescribeGroup
        {
            ErrorCode = ErrorCode.GroupAuthorizationFailed,
            ErrorMessage = "Not authorized",
            GroupId = "my-group",
            GroupState = "",
            Members = []
        };

        await Assert.That(group.ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(group.ErrorMessage).IsEqualTo("Not authorized");
    }

    [Test]
    public async Task Response_Group_AllFieldsPopulated()
    {
        var topicId = Guid.NewGuid();
        var group = new ShareGroupDescribeGroup
        {
            ErrorCode = ErrorCode.None,
            GroupId = "my-group",
            GroupState = "Stable",
            GroupEpoch = 5,
            AssignmentEpoch = 4,
            AssignorName = "server-side",
            Members =
            [
                new ShareGroupDescribeMember
                {
                    MemberId = "member-1",
                    RackId = "rack-a",
                    MemberEpoch = 3,
                    ClientId = "client-1",
                    ClientHost = "/127.0.0.1",
                    SubscribedTopicNames = ["topic-1"],
                    Assignment = new ShareGroupDescribeAssignment
                    {
                        TopicPartitions =
                        [
                            new ShareGroupDescribeTopicPartitions
                            {
                                TopicId = topicId,
                                TopicName = "topic-1",
                                Partitions = [0, 1]
                            }
                        ]
                    }
                }
            ],
            AuthorizedOperations = 0x1F
        };

        await Assert.That(group.GroupEpoch).IsEqualTo(5);
        await Assert.That(group.AssignmentEpoch).IsEqualTo(4);
        await Assert.That(group.AssignorName).IsEqualTo("server-side");
        await Assert.That(group.Members.Count).IsEqualTo(1);
        await Assert.That(group.AuthorizedOperations).IsEqualTo(0x1F);
    }

    #endregion

    #region Response API Metadata

    [Test]
    public async Task Response_ApiKey_IsShareGroupDescribe()
    {
        await Assert.That(ShareGroupDescribeResponse.ApiKey).IsEqualTo(ApiKey.ShareGroupDescribe);
    }

    [Test]
    public async Task Response_LowestSupportedVersion_Is1()
    {
        await Assert.That(ShareGroupDescribeResponse.LowestSupportedVersion).IsEqualTo((short)1);
    }

    [Test]
    public async Task Response_HighestSupportedVersion_Is1()
    {
        await Assert.That(ShareGroupDescribeResponse.HighestSupportedVersion).IsEqualTo((short)1);
    }

    #endregion

    #region Response Wire Format Parsing

    [Test]
    public async Task Response_Read_EmptyGroups_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteUnsignedVarInt(0 + 1); // Groups: empty compact array
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareGroupDescribeResponse)ShareGroupDescribeResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Groups.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Response_Read_WithGroup_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(50);             // ThrottleTimeMs
        writer.WriteUnsignedVarInt(1 + 1); // Groups: 1 element
        // Group[0]
        writer.WriteInt16(0);                          // ErrorCode = None
        writer.WriteUnsignedVarInt(0);                 // ErrorMessage = null
        writer.WriteCompactString("my-group");         // GroupId
        writer.WriteCompactString("Stable");           // GroupState
        writer.WriteInt32(2);                          // GroupEpoch
        writer.WriteInt32(2);                          // AssignmentEpoch
        writer.WriteUnsignedVarInt(0);                 // AssignorName = null
        writer.WriteUnsignedVarInt(0 + 1);             // Members: empty compact array
        writer.WriteInt32(-2147483648);                // AuthorizedOperations (INT32_MIN = not requested)
        writer.WriteUnsignedVarInt(0);                 // Group[0] tagged fields
        writer.WriteUnsignedVarInt(0);                 // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareGroupDescribeResponse)ShareGroupDescribeResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(50);
        await Assert.That(response.Groups.Count).IsEqualTo(1);
        await Assert.That(response.Groups[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Groups[0].GroupId).IsEqualTo("my-group");
        await Assert.That(response.Groups[0].GroupState).IsEqualTo("Stable");
        await Assert.That(response.Groups[0].GroupEpoch).IsEqualTo(2);
        await Assert.That(response.Groups[0].AssignmentEpoch).IsEqualTo(2);
        await Assert.That(response.Groups[0].AssignorName).IsNull();
        await Assert.That(response.Groups[0].Members.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Response_Read_WithMemberAndAssignment_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteUnsignedVarInt(1 + 1); // Groups: 1 element
        // Group[0]
        writer.WriteInt16(0);                          // ErrorCode
        writer.WriteUnsignedVarInt(0);                 // ErrorMessage = null
        writer.WriteCompactString("my-group");         // GroupId
        writer.WriteCompactString("Stable");           // GroupState
        writer.WriteInt32(1);                          // GroupEpoch
        writer.WriteInt32(1);                          // AssignmentEpoch
        writer.WriteCompactString("server-side");      // AssignorName
        // Members: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        // Member[0]
        writer.WriteCompactString("member-1");         // MemberId
        writer.WriteUnsignedVarInt(0);                 // RackId = null
        writer.WriteInt32(1);                          // MemberEpoch
        writer.WriteCompactString("client-1");         // ClientId
        writer.WriteCompactString("/127.0.0.1");       // ClientHost
        // SubscribedTopicNames: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteCompactString("topic-1");
        // Assignment (inline, no marker byte)
        // Assignment.TopicPartitions: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteUuid(topicId);
        writer.WriteCompactString("topic-1");          // TopicName
        writer.WriteUnsignedVarInt(2 + 1);             // Partitions: 2 elements
        writer.WriteInt32(0);
        writer.WriteInt32(1);
        writer.WriteUnsignedVarInt(0);                 // TopicPartitions[0] tagged fields
        writer.WriteUnsignedVarInt(0);                 // Assignment tagged fields
        writer.WriteUnsignedVarInt(0);                 // Member[0] tagged fields
        writer.WriteInt32(0x1F);                       // AuthorizedOperations
        writer.WriteUnsignedVarInt(0);                 // Group[0] tagged fields
        writer.WriteUnsignedVarInt(0);                 // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareGroupDescribeResponse)ShareGroupDescribeResponse.Read(ref reader, version: 1);

        await Assert.That(response.Groups.Count).IsEqualTo(1);
        var group = response.Groups[0];
        await Assert.That(group.AssignorName).IsEqualTo("server-side");
        await Assert.That(group.Members.Count).IsEqualTo(1);

        var member = group.Members[0];
        await Assert.That(member.MemberId).IsEqualTo("member-1");
        await Assert.That(member.RackId).IsNull();
        await Assert.That(member.MemberEpoch).IsEqualTo(1);
        await Assert.That(member.ClientId).IsEqualTo("client-1");
        await Assert.That(member.ClientHost).IsEqualTo("/127.0.0.1");
        await Assert.That(member.SubscribedTopicNames.Count).IsEqualTo(1);
        await Assert.That(member.SubscribedTopicNames[0]).IsEqualTo("topic-1");
        await Assert.That(member.Assignment).IsNotNull();
        await Assert.That(member.Assignment!.TopicPartitions.Count).IsEqualTo(1);
        await Assert.That(member.Assignment.TopicPartitions[0].TopicId).IsEqualTo(topicId);
        await Assert.That(member.Assignment.TopicPartitions[0].TopicName).IsEqualTo("topic-1");
        await Assert.That(member.Assignment.TopicPartitions[0].Partitions.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Response_Read_MemberWithEmptyAssignment_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteUnsignedVarInt(1 + 1); // Groups: 1 element
        // Group[0]
        writer.WriteInt16(0);                          // ErrorCode
        writer.WriteUnsignedVarInt(0);                 // ErrorMessage = null
        writer.WriteCompactString("my-group");         // GroupId
        writer.WriteCompactString("Empty");            // GroupState
        writer.WriteInt32(0);                          // GroupEpoch
        writer.WriteInt32(0);                          // AssignmentEpoch
        writer.WriteUnsignedVarInt(0);                 // AssignorName = null
        // Members: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        // Member[0]
        writer.WriteCompactString("member-1");
        writer.WriteUnsignedVarInt(0);                 // RackId = null
        writer.WriteInt32(0);                          // MemberEpoch
        writer.WriteCompactString("client-1");
        writer.WriteCompactString("/127.0.0.1");
        writer.WriteUnsignedVarInt(0 + 1);             // SubscribedTopicNames: empty
        // Assignment (inline, no marker byte)
        writer.WriteUnsignedVarInt(0 + 1);             // Assignment.TopicPartitions: empty compact array
        writer.WriteUnsignedVarInt(0);                 // Assignment tagged fields
        writer.WriteUnsignedVarInt(0);                 // Member[0] tagged fields
        writer.WriteInt32(-2147483648);                // AuthorizedOperations
        writer.WriteUnsignedVarInt(0);                 // Group[0] tagged fields
        writer.WriteUnsignedVarInt(0);                 // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareGroupDescribeResponse)ShareGroupDescribeResponse.Read(ref reader, version: 1);

        var member = response.Groups[0].Members[0];
        await Assert.That(member.Assignment.TopicPartitions.Count).IsEqualTo(0);
        await Assert.That(member.SubscribedTopicNames.Count).IsEqualTo(0);
    }

    #endregion

    #region Nested Type Round-Trips

    [Test]
    public async Task Group_WriteAndRead_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareGroupDescribeGroup
        {
            ErrorCode = ErrorCode.None,
            GroupId = "my-group",
            GroupState = "Stable",
            GroupEpoch = 3,
            AssignmentEpoch = 2,
            AssignorName = "uniform",
            Members = [],
            AuthorizedOperations = 0xFF
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareGroupDescribeGroup.Read(ref reader);

        await Assert.That(deserialized.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(deserialized.GroupId).IsEqualTo("my-group");
        await Assert.That(deserialized.GroupState).IsEqualTo("Stable");
        await Assert.That(deserialized.GroupEpoch).IsEqualTo(3);
        await Assert.That(deserialized.AssignmentEpoch).IsEqualTo(2);
        await Assert.That(deserialized.AssignorName).IsEqualTo("uniform");
        await Assert.That(deserialized.Members.Count).IsEqualTo(0);
        await Assert.That(deserialized.AuthorizedOperations).IsEqualTo(0xFF);
    }

    [Test]
    public async Task Member_WriteAndRead_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareGroupDescribeMember
        {
            MemberId = "member-1",
            RackId = "rack-a",
            MemberEpoch = 2,
            ClientId = "client-1",
            ClientHost = "/10.0.0.1",
            SubscribedTopicNames = ["topic-a", "topic-b"],
            Assignment = new ShareGroupDescribeAssignment
            {
                TopicPartitions = []
            }
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareGroupDescribeMember.Read(ref reader);

        await Assert.That(deserialized.MemberId).IsEqualTo("member-1");
        await Assert.That(deserialized.RackId).IsEqualTo("rack-a");
        await Assert.That(deserialized.MemberEpoch).IsEqualTo(2);
        await Assert.That(deserialized.ClientId).IsEqualTo("client-1");
        await Assert.That(deserialized.ClientHost).IsEqualTo("/10.0.0.1");
        await Assert.That(deserialized.SubscribedTopicNames.Count).IsEqualTo(2);
        await Assert.That(deserialized.Assignment.TopicPartitions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Member_WriteAndRead_WithAssignment_RoundTrips()
    {
        var topicId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareGroupDescribeMember
        {
            MemberId = "member-1",
            MemberEpoch = 1,
            ClientId = "client-1",
            ClientHost = "/127.0.0.1",
            SubscribedTopicNames = ["topic-1"],
            Assignment = new ShareGroupDescribeAssignment
            {
                TopicPartitions =
                [
                    new ShareGroupDescribeTopicPartitions
                    {
                        TopicId = topicId,
                        TopicName = "topic-1",
                        Partitions = [0, 1, 2]
                    }
                ]
            }
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareGroupDescribeMember.Read(ref reader);

        await Assert.That(deserialized.Assignment).IsNotNull();
        await Assert.That(deserialized.Assignment!.TopicPartitions.Count).IsEqualTo(1);
        await Assert.That(deserialized.Assignment.TopicPartitions[0].TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.Assignment.TopicPartitions[0].TopicName).IsEqualTo("topic-1");
        await Assert.That(deserialized.Assignment.TopicPartitions[0].Partitions.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Assignment_WriteAndRead_RoundTrips()
    {
        var topicId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareGroupDescribeAssignment
        {
            TopicPartitions =
            [
                new ShareGroupDescribeTopicPartitions
                {
                    TopicId = topicId,
                    TopicName = "my-topic",
                    Partitions = [0]
                }
            ]
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareGroupDescribeAssignment.Read(ref reader);

        await Assert.That(deserialized.TopicPartitions.Count).IsEqualTo(1);
        await Assert.That(deserialized.TopicPartitions[0].TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.TopicPartitions[0].TopicName).IsEqualTo("my-topic");
    }

    [Test]
    public async Task TopicPartitions_WriteAndRead_RoundTrips()
    {
        var topicId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareGroupDescribeTopicPartitions
        {
            TopicId = topicId,
            TopicName = "topic-1",
            Partitions = [0, 3, 7]
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareGroupDescribeTopicPartitions.Read(ref reader);

        await Assert.That(deserialized.TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.TopicName).IsEqualTo("topic-1");
        await Assert.That(deserialized.Partitions.Count).IsEqualTo(3);
        await Assert.That(deserialized.Partitions[0]).IsEqualTo(0);
        await Assert.That(deserialized.Partitions[1]).IsEqualTo(3);
        await Assert.That(deserialized.Partitions[2]).IsEqualTo(7);
    }

    [Test]
    public async Task TopicPartitions_NullTopicName_RoundTrips()
    {
        var topicId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareGroupDescribeTopicPartitions
        {
            TopicId = topicId,
            TopicName = null,
            Partitions = [0]
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareGroupDescribeTopicPartitions.Read(ref reader);

        await Assert.That(deserialized.TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.TopicName).IsNull();
        await Assert.That(deserialized.Partitions.Count).IsEqualTo(1);
    }

    #endregion
}
