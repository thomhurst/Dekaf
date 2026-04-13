using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for ShareGroupHeartbeat request and response message encoding/decoding (KIP-932).
/// </summary>
public sealed class ShareGroupHeartbeatMessageTests
{
    #region Request Construction

    [Test]
    public async Task Request_CanBeConstructed_WithRequiredFields()
    {
        var request = new ShareGroupHeartbeatRequest
        {
            GroupId = "my-group",
            MemberId = "",
            MemberEpoch = 0
        };

        await Assert.That(request.GroupId).IsEqualTo("my-group");
        await Assert.That(request.MemberId).IsEqualTo("");
        await Assert.That(request.MemberEpoch).IsEqualTo(0);
    }

    [Test]
    public async Task Request_CanBeConstructed_WithAllFields()
    {
        var request = new ShareGroupHeartbeatRequest
        {
            GroupId = "my-group",
            MemberId = "member-1",
            MemberEpoch = 5,
            RackId = "rack-a",
            SubscribedTopicNames = ["topic-1", "topic-2"]
        };

        await Assert.That(request.GroupId).IsEqualTo("my-group");
        await Assert.That(request.MemberId).IsEqualTo("member-1");
        await Assert.That(request.MemberEpoch).IsEqualTo(5);
        await Assert.That(request.RackId).IsEqualTo("rack-a");
        await Assert.That(request.SubscribedTopicNames!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Request_NullableFields_DefaultToNull()
    {
        var request = new ShareGroupHeartbeatRequest
        {
            GroupId = "my-group",
            MemberId = "",
            MemberEpoch = 0
        };

        await Assert.That(request.RackId).IsNull();
        await Assert.That(request.SubscribedTopicNames).IsNull();
    }

    [Test]
    public async Task Request_MemberEpoch_Zero_MeansJoin()
    {
        var request = new ShareGroupHeartbeatRequest
        {
            GroupId = "my-group",
            MemberId = "",
            MemberEpoch = 0
        };

        await Assert.That(request.MemberEpoch).IsEqualTo(0);
    }

    [Test]
    public async Task Request_MemberEpoch_NegativeOne_MeansLeave()
    {
        var request = new ShareGroupHeartbeatRequest
        {
            GroupId = "my-group",
            MemberId = "member-1",
            MemberEpoch = -1
        };

        await Assert.That(request.MemberEpoch).IsEqualTo(-1);
    }

    #endregion

    #region Request API Metadata

    [Test]
    public async Task Request_ApiKey_IsShareGroupHeartbeat()
    {
        await Assert.That(ShareGroupHeartbeatRequest.ApiKey).IsEqualTo(ApiKey.ShareGroupHeartbeat);
    }

    [Test]
    public async Task Request_LowestSupportedVersion_Is1()
    {
        await Assert.That(ShareGroupHeartbeatRequest.LowestSupportedVersion).IsEqualTo((short)1);
    }

    [Test]
    public async Task Request_HighestSupportedVersion_Is1()
    {
        await Assert.That(ShareGroupHeartbeatRequest.HighestSupportedVersion).IsEqualTo((short)1);
    }

    #endregion

    #region Request Encoding

    [Test]
    public async Task Request_Write_JoinGroup_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ShareGroupHeartbeatRequest
        {
            GroupId = "test",
            MemberId = "",
            MemberEpoch = 0,
            SubscribedTopicNames = ["my-topic"]
        };
        request.Write(ref writer, version: 1);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Request_Write_WithNullOptionalFields_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ShareGroupHeartbeatRequest
        {
            GroupId = "test",
            MemberId = "",
            MemberEpoch = 0
        };
        request.Write(ref writer, version: 1);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Request_Write_WithRackId_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ShareGroupHeartbeatRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            MemberEpoch = 3,
            RackId = "rack-a"
        };
        request.Write(ref writer, version: 1);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Request_Write_WithSubscribedTopics_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ShareGroupHeartbeatRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            MemberEpoch = 2,
            SubscribedTopicNames = ["topic-a", "topic-b", "topic-c"]
        };
        request.Write(ref writer, version: 1);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Request_Write_LeaveGroup_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ShareGroupHeartbeatRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            MemberEpoch = -1
        };
        request.Write(ref writer, version: 1);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    #endregion

    #region Response Construction

    [Test]
    public async Task Response_CanBeConstructed_WithRequiredFields()
    {
        var response = new ShareGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.None
        };

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.MemberEpoch).IsEqualTo(0);
        await Assert.That(response.HeartbeatIntervalMs).IsEqualTo(0);
    }

    [Test]
    public async Task Response_CanBeConstructed_WithAllFields()
    {
        var topicId = Guid.NewGuid();
        var response = new ShareGroupHeartbeatResponse
        {
            ThrottleTimeMs = 100,
            ErrorCode = ErrorCode.None,
            ErrorMessage = null,
            MemberId = "member-1",
            MemberEpoch = 5,
            HeartbeatIntervalMs = 5000,
            Assignment = new ShareGroupHeartbeatAssignment
            {
                TopicPartitions =
                [
                    new ShareGroupHeartbeatTopicPartitions
                    {
                        TopicId = topicId,
                        Partitions = [0, 1, 2]
                    }
                ]
            }
        };

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.MemberId).IsEqualTo("member-1");
        await Assert.That(response.MemberEpoch).IsEqualTo(5);
        await Assert.That(response.HeartbeatIntervalMs).IsEqualTo(5000);
        await Assert.That(response.Assignment).IsNotNull();
        await Assert.That(response.Assignment!.TopicPartitions.Count).IsEqualTo(1);
        await Assert.That(response.Assignment.TopicPartitions[0].TopicId).IsEqualTo(topicId);
        await Assert.That(response.Assignment.TopicPartitions[0].Partitions.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Response_Assignment_CanBeNull()
    {
        var response = new ShareGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.None,
            MemberId = "member-1",
            MemberEpoch = 3,
            HeartbeatIntervalMs = 5000,
            Assignment = null
        };

        await Assert.That(response.Assignment).IsNull();
    }

    [Test]
    public async Task Response_WithError_HasErrorMessage()
    {
        var response = new ShareGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.GroupAuthorizationFailed,
            ErrorMessage = "Not authorized"
        };

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(response.ErrorMessage).IsEqualTo("Not authorized");
    }

    #endregion

    #region Response API Metadata

    [Test]
    public async Task Response_ApiKey_IsShareGroupHeartbeat()
    {
        await Assert.That(ShareGroupHeartbeatResponse.ApiKey).IsEqualTo(ApiKey.ShareGroupHeartbeat);
    }

    [Test]
    public async Task Response_LowestSupportedVersion_Is1()
    {
        await Assert.That(ShareGroupHeartbeatResponse.LowestSupportedVersion).IsEqualTo((short)1);
    }

    [Test]
    public async Task Response_HighestSupportedVersion_Is1()
    {
        await Assert.That(ShareGroupHeartbeatResponse.HighestSupportedVersion).IsEqualTo((short)1);
    }

    #endregion

    #region Response Wire Format Parsing

    [Test]
    public async Task Response_Read_NullAssignment_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);                     // ThrottleTimeMs
        writer.WriteInt16(0);                     // ErrorCode = None
        writer.WriteUnsignedVarInt(0);            // ErrorMessage = null (compact nullable string)
        WriteCompactNullableString(ref writer, "member-1"); // MemberId
        writer.WriteInt32(1);                     // MemberEpoch
        writer.WriteInt32(5000);                  // HeartbeatIntervalMs
        writer.WriteInt8(-1);                     // Assignment = null (signed byte marker)
        writer.WriteUnsignedVarInt(0);            // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareGroupHeartbeatResponse)ShareGroupHeartbeatResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ErrorMessage).IsNull();
        await Assert.That(response.MemberId).IsEqualTo("member-1");
        await Assert.That(response.MemberEpoch).IsEqualTo(1);
        await Assert.That(response.HeartbeatIntervalMs).IsEqualTo(5000);
        await Assert.That(response.Assignment).IsNull();
    }

    [Test]
    public async Task Response_Read_WithAssignment_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);                     // ThrottleTimeMs
        writer.WriteInt16(0);                     // ErrorCode = None
        writer.WriteUnsignedVarInt(0);            // ErrorMessage = null
        WriteCompactNullableString(ref writer, "member-1"); // MemberId
        writer.WriteInt32(1);                     // MemberEpoch
        writer.WriteInt32(5000);                  // HeartbeatIntervalMs
        writer.WriteInt8(1);                      // Assignment = present (signed byte marker)
        // TopicPartitions compact array: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteUuid(topicId);
        writer.WriteUnsignedVarInt(1 + 1);        // Partitions: 1 element
        writer.WriteInt32(0);                     // partition 0
        writer.WriteUnsignedVarInt(0);            // TopicPartitions[0] tagged fields
        writer.WriteUnsignedVarInt(0);            // Assignment tagged fields
        writer.WriteUnsignedVarInt(0);            // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareGroupHeartbeatResponse)ShareGroupHeartbeatResponse.Read(ref reader, version: 1);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.MemberId).IsEqualTo("member-1");
        await Assert.That(response.MemberEpoch).IsEqualTo(1);
        await Assert.That(response.HeartbeatIntervalMs).IsEqualTo(5000);
        await Assert.That(response.Assignment).IsNotNull();
        await Assert.That(response.Assignment!.TopicPartitions.Count).IsEqualTo(1);
        await Assert.That(response.Assignment.TopicPartitions[0].TopicId).IsEqualTo(topicId);
        await Assert.That(response.Assignment.TopicPartitions[0].Partitions.Count).IsEqualTo(1);
        await Assert.That(response.Assignment.TopicPartitions[0].Partitions[0]).IsEqualTo(0);
    }

    [Test]
    public async Task Response_Read_EmptyAssignment_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);                     // ThrottleTimeMs
        writer.WriteInt16(0);                     // ErrorCode = None
        writer.WriteUnsignedVarInt(0);            // ErrorMessage = null
        WriteCompactNullableString(ref writer, "member-1"); // MemberId
        writer.WriteInt32(1);                     // MemberEpoch
        writer.WriteInt32(5000);                  // HeartbeatIntervalMs
        writer.WriteInt8(1);                      // Assignment = present (signed byte marker)
        writer.WriteUnsignedVarInt(0 + 1);        // TopicPartitions: empty compact array
        writer.WriteUnsignedVarInt(0);            // Assignment tagged fields
        writer.WriteUnsignedVarInt(0);            // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareGroupHeartbeatResponse)ShareGroupHeartbeatResponse.Read(ref reader, version: 1);

        await Assert.That(response.Assignment).IsNotNull();
        await Assert.That(response.Assignment!.TopicPartitions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Response_Read_WithMultiplePartitions_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(50);                    // ThrottleTimeMs
        writer.WriteInt16(0);                     // ErrorCode = None
        writer.WriteUnsignedVarInt(0);            // ErrorMessage = null
        WriteCompactNullableString(ref writer, "member-uuid"); // MemberId
        writer.WriteInt32(3);                     // MemberEpoch
        writer.WriteInt32(3000);                  // HeartbeatIntervalMs
        writer.WriteInt8(1);                      // Assignment = present
        // TopicPartitions compact array: 1 topic
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteUuid(topicId);
        writer.WriteUnsignedVarInt(3 + 1);        // Partitions: 3 elements
        writer.WriteInt32(0);
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        writer.WriteUnsignedVarInt(0);            // TopicPartitions[0] tagged fields
        writer.WriteUnsignedVarInt(0);            // Assignment tagged fields
        writer.WriteUnsignedVarInt(0);            // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareGroupHeartbeatResponse)ShareGroupHeartbeatResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(50);
        await Assert.That(response.MemberId).IsEqualTo("member-uuid");
        await Assert.That(response.MemberEpoch).IsEqualTo(3);
        await Assert.That(response.HeartbeatIntervalMs).IsEqualTo(3000);
        await Assert.That(response.Assignment).IsNotNull();
        await Assert.That(response.Assignment!.TopicPartitions[0].Partitions.Count).IsEqualTo(3);
        await Assert.That(response.Assignment.TopicPartitions[0].Partitions[0]).IsEqualTo(0);
        await Assert.That(response.Assignment.TopicPartitions[0].Partitions[1]).IsEqualTo(1);
        await Assert.That(response.Assignment.TopicPartitions[0].Partitions[2]).IsEqualTo(2);
    }

    /// <summary>
    /// Helper to write a compact nullable string (varint length+1, then UTF-8 bytes; 0 for null).
    /// </summary>
    private static void WriteCompactNullableString(ref KafkaProtocolWriter writer, string? value)
    {
        if (value is null)
        {
            writer.WriteUnsignedVarInt(0);
            return;
        }
        writer.WriteCompactString(value);
    }

    #endregion

    #region TopicPartitions

    [Test]
    public async Task TopicPartitions_CanBeConstructed()
    {
        var topicId = Guid.NewGuid();
        var tp = new ShareGroupHeartbeatTopicPartitions
        {
            TopicId = topicId,
            Partitions = [0, 1, 2]
        };

        await Assert.That(tp.TopicId).IsEqualTo(topicId);
        await Assert.That(tp.Partitions.Count).IsEqualTo(3);
        await Assert.That(tp.Partitions[0]).IsEqualTo(0);
        await Assert.That(tp.Partitions[1]).IsEqualTo(1);
        await Assert.That(tp.Partitions[2]).IsEqualTo(2);
    }

    [Test]
    public async Task TopicPartitions_Write_ProducesOutput()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var tp = new ShareGroupHeartbeatTopicPartitions
        {
            TopicId = Guid.NewGuid(),
            Partitions = [0, 1]
        };
        tp.Write(ref writer);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task TopicPartitions_WriteAndRead_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var topicId = Guid.NewGuid();
        var original = new ShareGroupHeartbeatTopicPartitions
        {
            TopicId = topicId,
            Partitions = [0, 3, 7]
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareGroupHeartbeatTopicPartitions.Read(ref reader);

        await Assert.That(deserialized.TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.Partitions.Count).IsEqualTo(3);
        await Assert.That(deserialized.Partitions[0]).IsEqualTo(0);
        await Assert.That(deserialized.Partitions[1]).IsEqualTo(3);
        await Assert.That(deserialized.Partitions[2]).IsEqualTo(7);
    }

    [Test]
    public async Task TopicPartitions_EmptyPartitions_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var topicId = Guid.NewGuid();
        var original = new ShareGroupHeartbeatTopicPartitions
        {
            TopicId = topicId,
            Partitions = []
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareGroupHeartbeatTopicPartitions.Read(ref reader);

        await Assert.That(deserialized.TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.Partitions.Count).IsEqualTo(0);
    }

    #endregion

    #region Assignment

    [Test]
    public async Task Assignment_CanBeConstructed_WithEmptyList()
    {
        var assignment = new ShareGroupHeartbeatAssignment
        {
            TopicPartitions = []
        };

        await Assert.That(assignment.TopicPartitions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Assignment_CanBeConstructed_WithTopicPartitions()
    {
        var topicId = Guid.NewGuid();

        var assignment = new ShareGroupHeartbeatAssignment
        {
            TopicPartitions =
            [
                new ShareGroupHeartbeatTopicPartitions
                {
                    TopicId = topicId,
                    Partitions = [0, 1]
                }
            ]
        };

        await Assert.That(assignment.TopicPartitions.Count).IsEqualTo(1);
        await Assert.That(assignment.TopicPartitions[0].TopicId).IsEqualTo(topicId);
        await Assert.That(assignment.TopicPartitions[0].Partitions.Count).IsEqualTo(2);
    }

    #endregion
}
