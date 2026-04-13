using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for ShareAcknowledge request and response message encoding/decoding (KIP-932).
/// </summary>
public sealed class ShareAcknowledgeMessageTests
{
    #region Request Construction

    [Test]
    public async Task Request_CanBeConstructed_WithRequiredFields()
    {
        var topicId = Guid.NewGuid();
        var request = new ShareAcknowledgeRequest
        {
            GroupId = "my-group",
            MemberId = "member-1",
            Topics =
            [
                new ShareAcknowledgeTopic
                {
                    TopicId = topicId,
                    Partitions =
                    [
                        new ShareAcknowledgePartition
                        {
                            PartitionIndex = 0,
                            AcknowledgementBatches =
                            [
                                new ShareAcknowledgeBatch
                                {
                                    FirstOffset = 0,
                                    LastOffset = 9,
                                    AcknowledgeTypes = [1, 1, 1]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        await Assert.That(request.GroupId).IsEqualTo("my-group");
        await Assert.That(request.MemberId).IsEqualTo("member-1");
        await Assert.That(request.Topics.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Request_CanBeConstructed_WithAllFields()
    {
        var request = new ShareAcknowledgeRequest
        {
            GroupId = "my-group",
            MemberId = "member-1",
            ShareSessionEpoch = 5,
            IsRenewAck = true,
            Topics = []
        };

        await Assert.That(request.ShareSessionEpoch).IsEqualTo(5);
        await Assert.That(request.IsRenewAck).IsTrue();
    }

    [Test]
    public async Task Request_DefaultValues_AreCorrect()
    {
        var request = new ShareAcknowledgeRequest
        {
            GroupId = "g",
            MemberId = "m",
            Topics = []
        };

        await Assert.That(request.ShareSessionEpoch).IsEqualTo(0);
        await Assert.That(request.IsRenewAck).IsFalse();
    }

    #endregion

    #region Request API Metadata

    [Test]
    public async Task Request_ApiKey_IsShareAcknowledge()
    {
        await Assert.That(ShareAcknowledgeRequest.ApiKey).IsEqualTo(ApiKey.ShareAcknowledge);
    }

    [Test]
    public async Task Request_LowestSupportedVersion_Is1()
    {
        await Assert.That(ShareAcknowledgeRequest.LowestSupportedVersion).IsEqualTo((short)1);
    }

    [Test]
    public async Task Request_HighestSupportedVersion_Is2()
    {
        await Assert.That(ShareAcknowledgeRequest.HighestSupportedVersion).IsEqualTo((short)2);
    }

    #endregion

    #region Request Encoding

    [Test]
    public async Task Request_Write_V1_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var topicId = Guid.NewGuid();
        var request = new ShareAcknowledgeRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            ShareSessionEpoch = 1,
            Topics =
            [
                new ShareAcknowledgeTopic
                {
                    TopicId = topicId,
                    Partitions =
                    [
                        new ShareAcknowledgePartition
                        {
                            PartitionIndex = 0,
                            AcknowledgementBatches =
                            [
                                new ShareAcknowledgeBatch
                                {
                                    FirstOffset = 0,
                                    LastOffset = 99,
                                    AcknowledgeTypes = [1, 2, 3]
                                }
                            ]
                        }
                    ]
                }
            ]
        };
        request.Write(ref writer, version: 1);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Request_Write_V2_IncludesIsRenewAck()
    {
        var request = new ShareAcknowledgeRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            IsRenewAck = true,
            Topics = []
        };

        var v1Buffer = new ArrayBufferWriter<byte>();
        var v1Writer = new KafkaProtocolWriter(v1Buffer);
        request.Write(ref v1Writer, version: 1);

        var v2Buffer = new ArrayBufferWriter<byte>();
        var v2Writer = new KafkaProtocolWriter(v2Buffer);
        request.Write(ref v2Writer, version: 2);

        // v2 adds IsRenewAck (1 byte written as Int8)
        await Assert.That(v2Buffer.WrittenCount).IsGreaterThan(v1Buffer.WrittenCount);
    }

    [Test]
    public async Task Request_Write_EmptyTopics_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ShareAcknowledgeRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            Topics = []
        };
        request.Write(ref writer, version: 1);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Request_Write_MultiplePartitions_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var topicId = Guid.NewGuid();
        var request = new ShareAcknowledgeRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            Topics =
            [
                new ShareAcknowledgeTopic
                {
                    TopicId = topicId,
                    Partitions =
                    [
                        new ShareAcknowledgePartition
                        {
                            PartitionIndex = 0,
                            AcknowledgementBatches =
                            [
                                new ShareAcknowledgeBatch
                                {
                                    FirstOffset = 0,
                                    LastOffset = 49,
                                    AcknowledgeTypes = [1]
                                }
                            ]
                        },
                        new ShareAcknowledgePartition
                        {
                            PartitionIndex = 1,
                            AcknowledgementBatches =
                            [
                                new ShareAcknowledgeBatch
                                {
                                    FirstOffset = 0,
                                    LastOffset = 29,
                                    AcknowledgeTypes = [2]
                                }
                            ]
                        }
                    ]
                }
            ]
        };
        request.Write(ref writer, version: 1);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    #endregion

    #region Response Construction

    [Test]
    public async Task Response_CanBeConstructed_WithRequiredFields()
    {
        var response = new ShareAcknowledgeResponse
        {
            Responses = [],
            NodeEndpoints = []
        };

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ErrorMessage).IsNull();
        await Assert.That(response.AcquisitionLockTimeoutMs).IsEqualTo(0);
        await Assert.That(response.Responses.Count).IsEqualTo(0);
        await Assert.That(response.NodeEndpoints.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Response_CanBeConstructed_WithAllFields()
    {
        var topicId = Guid.NewGuid();
        var response = new ShareAcknowledgeResponse
        {
            ThrottleTimeMs = 100,
            ErrorCode = ErrorCode.None,
            AcquisitionLockTimeoutMs = 30000,
            Responses =
            [
                new ShareAcknowledgeResponseTopic
                {
                    TopicId = topicId,
                    Partitions =
                    [
                        new ShareAcknowledgeResponsePartition
                        {
                            PartitionIndex = 0,
                            ErrorCode = ErrorCode.None,
                            CurrentLeader = new ShareAcknowledgeLeaderIdAndEpoch
                            {
                                LeaderId = 1,
                                LeaderEpoch = 5
                            }
                        }
                    ]
                }
            ],
            NodeEndpoints =
            [
                new ShareAcknowledgeNodeEndpoint
                {
                    NodeId = 1,
                    Host = "broker-1",
                    Port = 9092
                }
            ]
        };

        await Assert.That(response.AcquisitionLockTimeoutMs).IsEqualTo(30000);
        await Assert.That(response.Responses.Count).IsEqualTo(1);
        await Assert.That(response.NodeEndpoints.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Response_Partition_CurrentLeader_DefaultsToUnknown()
    {
        var partition = new ShareAcknowledgeResponsePartition
        {
            PartitionIndex = 0,
            ErrorCode = ErrorCode.None,
            CurrentLeader = new ShareAcknowledgeLeaderIdAndEpoch()
        };

        await Assert.That(partition.CurrentLeader.LeaderId).IsEqualTo(-1);
        await Assert.That(partition.CurrentLeader.LeaderEpoch).IsEqualTo(-1);
    }

    [Test]
    public async Task Response_LeaderIdAndEpoch_DefaultsToNegativeOne()
    {
        var leader = new ShareAcknowledgeLeaderIdAndEpoch();

        await Assert.That(leader.LeaderId).IsEqualTo(-1);
        await Assert.That(leader.LeaderEpoch).IsEqualTo(-1);
    }

    #endregion

    #region Response API Metadata

    [Test]
    public async Task Response_ApiKey_IsShareAcknowledge()
    {
        await Assert.That(ShareAcknowledgeResponse.ApiKey).IsEqualTo(ApiKey.ShareAcknowledge);
    }

    [Test]
    public async Task Response_LowestSupportedVersion_Is1()
    {
        await Assert.That(ShareAcknowledgeResponse.LowestSupportedVersion).IsEqualTo((short)1);
    }

    [Test]
    public async Task Response_HighestSupportedVersion_Is2()
    {
        await Assert.That(ShareAcknowledgeResponse.HighestSupportedVersion).IsEqualTo((short)2);
    }

    #endregion

    #region Response Wire Format Parsing

    [Test]
    public async Task Response_Read_V1_EmptyResponse_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteInt16(0);              // ErrorCode = None
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        // No AcquisitionLockTimeoutMs in v1
        writer.WriteUnsignedVarInt(0 + 1); // Responses: empty compact array
        writer.WriteUnsignedVarInt(0 + 1); // NodeEndpoints: empty compact array
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareAcknowledgeResponse)ShareAcknowledgeResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.AcquisitionLockTimeoutMs).IsEqualTo(0);
        await Assert.That(response.Responses.Count).IsEqualTo(0);
        await Assert.That(response.NodeEndpoints.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Response_Read_V2_WithAcquisitionLockTimeout_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteInt16(0);              // ErrorCode = None
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        writer.WriteInt32(20000);          // AcquisitionLockTimeoutMs (v2+)
        writer.WriteUnsignedVarInt(0 + 1); // Responses: empty
        writer.WriteUnsignedVarInt(0 + 1); // NodeEndpoints: empty
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareAcknowledgeResponse)ShareAcknowledgeResponse.Read(ref reader, version: 2);

        await Assert.That(response.AcquisitionLockTimeoutMs).IsEqualTo(20000);
    }

    [Test]
    public async Task Response_Read_WithPerPartitionErrors_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteInt16(0);              // ErrorCode = None
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        // Responses: 1 topic
        writer.WriteUnsignedVarInt(1 + 1);
        // Topic[0]
        writer.WriteUuid(topicId);
        // Partitions: 2 elements
        writer.WriteUnsignedVarInt(2 + 1);
        // Partition[0] - success
        writer.WriteInt32(0);              // PartitionIndex
        writer.WriteInt16(0);              // ErrorCode = None
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        writer.WriteInt32(-1);             // CurrentLeader.LeaderId
        writer.WriteInt32(-1);             // CurrentLeader.LeaderEpoch
        writer.WriteUnsignedVarInt(0);     // CurrentLeader tagged fields
        writer.WriteUnsignedVarInt(0);     // Partition[0] tagged fields
        // Partition[1] - error
        writer.WriteInt32(1);              // PartitionIndex
        writer.WriteInt16(6);              // ErrorCode = NotLeaderOrFollower
        WriteCompactNullableString(ref writer, "Not the leader"); // ErrorMessage
        writer.WriteInt32(-1);             // CurrentLeader.LeaderId
        writer.WriteInt32(-1);             // CurrentLeader.LeaderEpoch
        writer.WriteUnsignedVarInt(0);     // CurrentLeader tagged fields
        writer.WriteUnsignedVarInt(0);     // Partition[1] tagged fields
        writer.WriteUnsignedVarInt(0);     // Topic[0] tagged fields
        // NodeEndpoints: empty
        writer.WriteUnsignedVarInt(0 + 1);
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareAcknowledgeResponse)ShareAcknowledgeResponse.Read(ref reader, version: 1);

        await Assert.That(response.Responses.Count).IsEqualTo(1);
        await Assert.That(response.Responses[0].TopicId).IsEqualTo(topicId);
        await Assert.That(response.Responses[0].Partitions.Count).IsEqualTo(2);

        await Assert.That(response.Responses[0].Partitions[0].PartitionIndex).IsEqualTo(0);
        await Assert.That(response.Responses[0].Partitions[0].ErrorCode).IsEqualTo(ErrorCode.None);

        await Assert.That(response.Responses[0].Partitions[1].PartitionIndex).IsEqualTo(1);
        await Assert.That(response.Responses[0].Partitions[1].ErrorCode).IsEqualTo((ErrorCode)6);
        await Assert.That(response.Responses[0].Partitions[1].ErrorMessage).IsEqualTo("Not the leader");
    }

    [Test]
    public async Task Response_Read_WithCurrentLeader_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteInt16(0);              // ErrorCode
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        // Responses: 1 topic
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteUuid(topicId);
        // Partitions: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteInt32(0);              // PartitionIndex
        writer.WriteInt16(0);              // ErrorCode
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        writer.WriteInt32(2);              // CurrentLeader.LeaderId
        writer.WriteInt32(10);             // CurrentLeader.LeaderEpoch
        writer.WriteUnsignedVarInt(0);     // CurrentLeader tagged fields
        writer.WriteUnsignedVarInt(0);     // Partition tagged fields
        writer.WriteUnsignedVarInt(0);     // Topic tagged fields
        writer.WriteUnsignedVarInt(0 + 1); // NodeEndpoints: empty
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareAcknowledgeResponse)ShareAcknowledgeResponse.Read(ref reader, version: 1);

        var partition = response.Responses[0].Partitions[0];
        await Assert.That(partition.CurrentLeader.LeaderId).IsEqualTo(2);
        await Assert.That(partition.CurrentLeader.LeaderEpoch).IsEqualTo(10);
    }

    [Test]
    public async Task Response_Read_WithDefaultCurrentLeader_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteInt16(0);              // ErrorCode
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        // Responses: 1 topic
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteUuid(topicId);
        // Partitions: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteInt32(0);              // PartitionIndex
        writer.WriteInt16(0);              // ErrorCode
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        writer.WriteInt32(-1);             // CurrentLeader.LeaderId
        writer.WriteInt32(-1);             // CurrentLeader.LeaderEpoch
        writer.WriteUnsignedVarInt(0);     // CurrentLeader tagged fields
        writer.WriteUnsignedVarInt(0);     // Partition tagged fields
        writer.WriteUnsignedVarInt(0);     // Topic tagged fields
        writer.WriteUnsignedVarInt(0 + 1); // NodeEndpoints: empty
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareAcknowledgeResponse)ShareAcknowledgeResponse.Read(ref reader, version: 1);

        var partition = response.Responses[0].Partitions[0];
        await Assert.That(partition.CurrentLeader.LeaderId).IsEqualTo(-1);
        await Assert.That(partition.CurrentLeader.LeaderEpoch).IsEqualTo(-1);
    }

    [Test]
    public async Task Response_Read_WithNodeEndpoints_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteInt16(0);              // ErrorCode
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        writer.WriteUnsignedVarInt(0 + 1); // Responses: empty
        // NodeEndpoints: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteInt32(1);              // NodeId
        writer.WriteCompactString("broker-1"); // Host (non-nullable)
        writer.WriteInt32(9092);           // Port
        writer.WriteCompactString("rack-a");   // Rack
        writer.WriteUnsignedVarInt(0);     // NodeEndpoint tagged fields
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareAcknowledgeResponse)ShareAcknowledgeResponse.Read(ref reader, version: 1);

        await Assert.That(response.NodeEndpoints.Count).IsEqualTo(1);
        await Assert.That(response.NodeEndpoints[0].NodeId).IsEqualTo(1);
        await Assert.That(response.NodeEndpoints[0].Host).IsEqualTo("broker-1");
        await Assert.That(response.NodeEndpoints[0].Port).IsEqualTo(9092);
        await Assert.That(response.NodeEndpoints[0].Rack).IsEqualTo("rack-a");
    }

    [Test]
    public async Task Response_Read_TopLevelError_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(50);             // ThrottleTimeMs
        writer.WriteInt16(29);             // ErrorCode = GroupAuthorizationFailed
        WriteCompactNullableString(ref writer, "Not authorized"); // ErrorMessage
        writer.WriteUnsignedVarInt(0 + 1); // Responses: empty
        writer.WriteUnsignedVarInt(0 + 1); // NodeEndpoints: empty
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareAcknowledgeResponse)ShareAcknowledgeResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(50);
        await Assert.That(response.ErrorCode).IsEqualTo((ErrorCode)29);
        await Assert.That(response.ErrorMessage).IsEqualTo("Not authorized");
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

    #region Nested Type Round-Trips

    [Test]
    public async Task AcknowledgeTopic_WriteAndRead_RoundTrips()
    {
        var topicId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareAcknowledgeTopic
        {
            TopicId = topicId,
            Partitions =
            [
                new ShareAcknowledgePartition
                {
                    PartitionIndex = 0,
                    AcknowledgementBatches =
                    [
                        new ShareAcknowledgeBatch
                        {
                            FirstOffset = 0,
                            LastOffset = 49,
                            AcknowledgeTypes = [1, 2]
                        }
                    ]
                }
            ]
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareAcknowledgeTopic.Read(ref reader);

        await Assert.That(deserialized.TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.Partitions.Count).IsEqualTo(1);
        await Assert.That(deserialized.Partitions[0].PartitionIndex).IsEqualTo(0);
    }

    [Test]
    public async Task AcknowledgePartition_WriteAndRead_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareAcknowledgePartition
        {
            PartitionIndex = 3,
            AcknowledgementBatches =
            [
                new ShareAcknowledgeBatch
                {
                    FirstOffset = 100,
                    LastOffset = 199,
                    AcknowledgeTypes = [1, 1, 2, 3]
                },
                new ShareAcknowledgeBatch
                {
                    FirstOffset = 200,
                    LastOffset = 299,
                    AcknowledgeTypes = [1]
                }
            ]
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareAcknowledgePartition.Read(ref reader);

        await Assert.That(deserialized.PartitionIndex).IsEqualTo(3);
        await Assert.That(deserialized.AcknowledgementBatches.Count).IsEqualTo(2);
        await Assert.That(deserialized.AcknowledgementBatches[0].FirstOffset).IsEqualTo(100L);
        await Assert.That(deserialized.AcknowledgementBatches[0].LastOffset).IsEqualTo(199L);
        await Assert.That(deserialized.AcknowledgementBatches[0].AcknowledgeTypes.Count).IsEqualTo(4);
        await Assert.That(deserialized.AcknowledgementBatches[1].FirstOffset).IsEqualTo(200L);
    }

    [Test]
    public async Task AcknowledgeBatch_WriteAndRead_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareAcknowledgeBatch
        {
            FirstOffset = 50,
            LastOffset = 149,
            AcknowledgeTypes = [1, 2, 3]
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareAcknowledgeBatch.Read(ref reader);

        await Assert.That(deserialized.FirstOffset).IsEqualTo(50L);
        await Assert.That(deserialized.LastOffset).IsEqualTo(149L);
        await Assert.That(deserialized.AcknowledgeTypes.Count).IsEqualTo(3);
        await Assert.That(deserialized.AcknowledgeTypes[0]).IsEqualTo((byte)1);
        await Assert.That(deserialized.AcknowledgeTypes[1]).IsEqualTo((byte)2);
        await Assert.That(deserialized.AcknowledgeTypes[2]).IsEqualTo((byte)3);
    }

    #endregion
}
