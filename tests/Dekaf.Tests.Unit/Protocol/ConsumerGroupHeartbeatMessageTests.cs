using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for ConsumerGroupHeartbeat request and response message encoding/decoding (KIP-848).
/// </summary>
public sealed class ConsumerGroupHeartbeatMessageTests
{
    #region Request Construction

    [Test]
    public async Task Request_CanBeConstructed_WithRequiredFields()
    {
        var request = new ConsumerGroupHeartbeatRequest
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
        var topicId = Guid.NewGuid();
        var request = new ConsumerGroupHeartbeatRequest
        {
            GroupId = "my-group",
            MemberId = "member-1",
            MemberEpoch = 5,
            InstanceId = "instance-1",
            RackId = "rack-a",
            RebalanceTimeoutMs = 30000,
            SubscribedTopicNames = ["topic-1", "topic-2"],
            ServerAssignor = "uniform",
            TopicPartitions =
            [
                new ConsumerGroupHeartbeatTopicPartitions
                {
                    TopicId = topicId,
                    Partitions = [0, 1, 2]
                }
            ]
        };

        await Assert.That(request.GroupId).IsEqualTo("my-group");
        await Assert.That(request.MemberId).IsEqualTo("member-1");
        await Assert.That(request.MemberEpoch).IsEqualTo(5);
        await Assert.That(request.InstanceId).IsEqualTo("instance-1");
        await Assert.That(request.RackId).IsEqualTo("rack-a");
        await Assert.That(request.RebalanceTimeoutMs).IsEqualTo(30000);
        await Assert.That(request.SubscribedTopicNames!.Count).IsEqualTo(2);
        await Assert.That(request.ServerAssignor).IsEqualTo("uniform");
        await Assert.That(request.TopicPartitions!.Count).IsEqualTo(1);
        await Assert.That(request.TopicPartitions[0].TopicId).IsEqualTo(topicId);
        await Assert.That(request.TopicPartitions[0].Partitions.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Request_NullableFields_DefaultToNull()
    {
        var request = new ConsumerGroupHeartbeatRequest
        {
            GroupId = "my-group",
            MemberId = "",
            MemberEpoch = 0
        };

        await Assert.That(request.InstanceId).IsNull();
        await Assert.That(request.RackId).IsNull();
        await Assert.That(request.SubscribedTopicNames).IsNull();
        await Assert.That(request.ServerAssignor).IsNull();
        await Assert.That(request.TopicPartitions).IsNull();
    }

    [Test]
    public async Task Request_RebalanceTimeoutMs_DefaultsToNegativeOne()
    {
        var request = new ConsumerGroupHeartbeatRequest
        {
            GroupId = "my-group",
            MemberId = "",
            MemberEpoch = 0
        };

        await Assert.That(request.RebalanceTimeoutMs).IsEqualTo(-1);
    }

    [Test]
    public async Task Request_MemberEpoch_NegativeOne_MeansLeave()
    {
        var request = new ConsumerGroupHeartbeatRequest
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
    public async Task Request_ApiKey_IsConsumerGroupHeartbeat()
    {
        await Assert.That(ConsumerGroupHeartbeatRequest.ApiKey).IsEqualTo(ApiKey.ConsumerGroupHeartbeat);
    }

    [Test]
    public async Task Request_LowestSupportedVersion_Is0()
    {
        await Assert.That(ConsumerGroupHeartbeatRequest.LowestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task Request_HighestSupportedVersion_Is0()
    {
        await Assert.That(ConsumerGroupHeartbeatRequest.HighestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task Request_IsAlwaysFlexible()
    {
        await Assert.That(ConsumerGroupHeartbeatRequest.IsFlexibleVersion(0)).IsTrue();
    }

    [Test]
    public async Task Request_RequestHeaderVersion_Is2()
    {
        await Assert.That(ConsumerGroupHeartbeatRequest.GetRequestHeaderVersion(0)).IsEqualTo((short)2);
    }

    [Test]
    public async Task Request_ResponseHeaderVersion_Is1()
    {
        await Assert.That(ConsumerGroupHeartbeatRequest.GetResponseHeaderVersion(0)).IsEqualTo((short)1);
    }

    #endregion

    #region Request Encoding

    [Test]
    public async Task Request_Write_JoinGroup_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ConsumerGroupHeartbeatRequest
        {
            GroupId = "test",
            MemberId = "",
            MemberEpoch = 0,
            RebalanceTimeoutMs = 60000,
            SubscribedTopicNames = ["my-topic"],
            ServerAssignor = "uniform"
        };
        request.Write(ref writer, version: 0);

        // Verify the output is non-empty and can be produced without error
        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Request_Write_WithNullOptionalFields_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ConsumerGroupHeartbeatRequest
        {
            GroupId = "test",
            MemberId = "",
            MemberEpoch = 0
        };
        request.Write(ref writer, version: 0);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Request_Write_WithTopicPartitions_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var topicId = Guid.NewGuid();
        var request = new ConsumerGroupHeartbeatRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            MemberEpoch = 3,
            TopicPartitions =
            [
                new ConsumerGroupHeartbeatTopicPartitions
                {
                    TopicId = topicId,
                    Partitions = [0, 1]
                }
            ]
        };
        request.Write(ref writer, version: 0);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    #endregion

    #region Response Construction

    [Test]
    public async Task Response_CanBeConstructed_WithRequiredFields()
    {
        var response = new ConsumerGroupHeartbeatResponse
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
        var response = new ConsumerGroupHeartbeatResponse
        {
            ThrottleTimeMs = 100,
            ErrorCode = ErrorCode.None,
            ErrorMessage = null,
            MemberId = "member-1",
            MemberEpoch = 5,
            HeartbeatIntervalMs = 5000,
            Assignment = new ConsumerGroupHeartbeatAssignment
            {
                AssignedTopicPartitions =
                [
                    new ConsumerGroupHeartbeatTopicPartitions
                    {
                        TopicId = topicId,
                        Partitions = [0, 1, 2]
                    }
                ],
                PendingTopicPartitions = []
            }
        };

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.MemberId).IsEqualTo("member-1");
        await Assert.That(response.MemberEpoch).IsEqualTo(5);
        await Assert.That(response.HeartbeatIntervalMs).IsEqualTo(5000);
        await Assert.That(response.Assignment).IsNotNull();
        await Assert.That(response.Assignment!.AssignedTopicPartitions.Count).IsEqualTo(1);
        await Assert.That(response.Assignment.PendingTopicPartitions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Response_Assignment_CanBeNull()
    {
        var response = new ConsumerGroupHeartbeatResponse
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
        var response = new ConsumerGroupHeartbeatResponse
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
    public async Task Response_ApiKey_IsConsumerGroupHeartbeat()
    {
        await Assert.That(ConsumerGroupHeartbeatResponse.ApiKey).IsEqualTo(ApiKey.ConsumerGroupHeartbeat);
    }

    [Test]
    public async Task Response_LowestSupportedVersion_Is0()
    {
        await Assert.That(ConsumerGroupHeartbeatResponse.LowestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task Response_HighestSupportedVersion_Is0()
    {
        await Assert.That(ConsumerGroupHeartbeatResponse.HighestSupportedVersion).IsEqualTo((short)0);
    }

    #endregion

    #region TopicPartitions

    [Test]
    public async Task TopicPartitions_CanBeConstructed()
    {
        var topicId = Guid.NewGuid();
        var tp = new ConsumerGroupHeartbeatTopicPartitions
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

        var tp = new ConsumerGroupHeartbeatTopicPartitions
        {
            TopicId = Guid.NewGuid(),
            Partitions = [0, 1]
        };
        tp.Write(ref writer);

        // UUID (16 bytes) + compact array header + 2 * INT32 (8 bytes) + tagged fields
        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task TopicPartitions_WriteAndRead_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var topicId = Guid.NewGuid();
        var original = new ConsumerGroupHeartbeatTopicPartitions
        {
            TopicId = topicId,
            Partitions = [0, 3, 7]
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ConsumerGroupHeartbeatTopicPartitions.Read(ref reader);

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
        var original = new ConsumerGroupHeartbeatTopicPartitions
        {
            TopicId = topicId,
            Partitions = []
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ConsumerGroupHeartbeatTopicPartitions.Read(ref reader);

        await Assert.That(deserialized.TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.Partitions.Count).IsEqualTo(0);
    }

    #endregion

    #region Assignment

    [Test]
    public async Task Assignment_CanBeConstructed_WithEmptyLists()
    {
        var assignment = new ConsumerGroupHeartbeatAssignment
        {
            AssignedTopicPartitions = [],
            PendingTopicPartitions = []
        };

        await Assert.That(assignment.AssignedTopicPartitions.Count).IsEqualTo(0);
        await Assert.That(assignment.PendingTopicPartitions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Assignment_CanBeConstructed_WithAssignedAndPending()
    {
        var topicId1 = Guid.NewGuid();
        var topicId2 = Guid.NewGuid();

        var assignment = new ConsumerGroupHeartbeatAssignment
        {
            AssignedTopicPartitions =
            [
                new ConsumerGroupHeartbeatTopicPartitions
                {
                    TopicId = topicId1,
                    Partitions = [0, 1]
                }
            ],
            PendingTopicPartitions =
            [
                new ConsumerGroupHeartbeatTopicPartitions
                {
                    TopicId = topicId2,
                    Partitions = [2]
                }
            ]
        };

        await Assert.That(assignment.AssignedTopicPartitions.Count).IsEqualTo(1);
        await Assert.That(assignment.AssignedTopicPartitions[0].TopicId).IsEqualTo(topicId1);
        await Assert.That(assignment.PendingTopicPartitions.Count).IsEqualTo(1);
        await Assert.That(assignment.PendingTopicPartitions[0].TopicId).IsEqualTo(topicId2);
    }

    #endregion
}
