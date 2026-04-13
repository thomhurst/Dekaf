using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for ShareFetch request and response message encoding/decoding (KIP-932).
/// </summary>
public sealed class ShareFetchMessageTests
{
    #region Request Construction

    [Test]
    public async Task Request_CanBeConstructed_WithRequiredFields()
    {
        var topicId = Guid.NewGuid();
        var request = new ShareFetchRequest
        {
            GroupId = "my-group",
            MemberId = "member-1",
            Topics =
            [
                new ShareFetchRequestTopic
                {
                    TopicId = topicId,
                    Partitions =
                    [
                        new ShareFetchRequestPartition { PartitionIndex = 0 }
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
        var topicId = Guid.NewGuid();
        var forgottenTopicId = Guid.NewGuid();
        var request = new ShareFetchRequest
        {
            GroupId = "my-group",
            MemberId = "member-1",
            ShareSessionEpoch = 5,
            MaxWaitMs = 500,
            MinBytes = 1,
            MaxBytes = 1048576,
            MaxRecords = 1000,
            BatchSize = 500,
            ShareAcquireMode = 0,
            IsRenewAck = true,
            Topics =
            [
                new ShareFetchRequestTopic
                {
                    TopicId = topicId,
                    Partitions =
                    [
                        new ShareFetchRequestPartition
                        {
                            PartitionIndex = 0,
                            PartitionMaxBytes = 65536,
                            AcknowledgementBatches =
                            [
                                new ShareFetchAcknowledgementBatch
                                {
                                    FirstOffset = 0,
                                    LastOffset = 9,
                                    AcknowledgeTypes = [1, 1, 1]
                                }
                            ]
                        }
                    ]
                }
            ],
            ForgottenTopicsData =
            [
                new ShareFetchForgottenTopic
                {
                    TopicId = forgottenTopicId,
                    Partitions = [0, 1]
                }
            ]
        };

        await Assert.That(request.ShareSessionEpoch).IsEqualTo(5);
        await Assert.That(request.MaxWaitMs).IsEqualTo(500);
        await Assert.That(request.MinBytes).IsEqualTo(1);
        await Assert.That(request.MaxBytes).IsEqualTo(1048576);
        await Assert.That(request.MaxRecords).IsEqualTo(1000);
        await Assert.That(request.BatchSize).IsEqualTo(500);
        await Assert.That(request.ShareAcquireMode).IsEqualTo((sbyte)0);
        await Assert.That(request.IsRenewAck).IsTrue();
        await Assert.That(request.ForgottenTopicsData!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Request_DefaultValues_AreCorrect()
    {
        var request = new ShareFetchRequest
        {
            GroupId = "g",
            MemberId = "m",
            Topics = []
        };

        await Assert.That(request.ShareSessionEpoch).IsEqualTo(0);
        await Assert.That(request.MaxWaitMs).IsEqualTo(0);
        await Assert.That(request.MinBytes).IsEqualTo(0);
        await Assert.That(request.MaxBytes).IsEqualTo(0);
        await Assert.That(request.MaxRecords).IsEqualTo(0);
        await Assert.That(request.BatchSize).IsEqualTo(0);
        await Assert.That(request.ShareAcquireMode).IsEqualTo((sbyte)0);
        await Assert.That(request.IsRenewAck).IsFalse();
        await Assert.That(request.ForgottenTopicsData).IsNull();
    }

    [Test]
    public async Task Request_Partition_AcknowledgementBatches_CanBeNull()
    {
        var partition = new ShareFetchRequestPartition
        {
            PartitionIndex = 0
        };

        await Assert.That(partition.AcknowledgementBatches).IsNull();
    }

    #endregion

    #region Request API Metadata

    [Test]
    public async Task Request_ApiKey_IsShareFetch()
    {
        await Assert.That(ShareFetchRequest.ApiKey).IsEqualTo(ApiKey.ShareFetch);
    }

    [Test]
    public async Task Request_LowestSupportedVersion_Is0()
    {
        await Assert.That(ShareFetchRequest.LowestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task Request_HighestSupportedVersion_Is2()
    {
        await Assert.That(ShareFetchRequest.HighestSupportedVersion).IsEqualTo((short)2);
    }

    #endregion

    #region Request Encoding

    [Test]
    public async Task Request_Write_V0_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var topicId = Guid.NewGuid();
        var request = new ShareFetchRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            ShareSessionEpoch = 0,
            MaxWaitMs = 500,
            MinBytes = 1,
            MaxBytes = 1048576,
            Topics =
            [
                new ShareFetchRequestTopic
                {
                    TopicId = topicId,
                    Partitions =
                    [
                        new ShareFetchRequestPartition
                        {
                            PartitionIndex = 0,
                            PartitionMaxBytes = 65536
                        }
                    ]
                }
            ]
        };
        request.Write(ref writer, version: 0);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Request_Write_V1_IncludesMaxRecordsAndBatchSize()
    {
        var topicId = Guid.NewGuid();
        var request = new ShareFetchRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            MaxRecords = 1000,
            BatchSize = 500,
            Topics =
            [
                new ShareFetchRequestTopic
                {
                    TopicId = topicId,
                    Partitions =
                    [
                        new ShareFetchRequestPartition { PartitionIndex = 0 }
                    ]
                }
            ]
        };

        var v0Buffer = new ArrayBufferWriter<byte>();
        var v0Writer = new KafkaProtocolWriter(v0Buffer);
        request.Write(ref v0Writer, version: 0);

        var v1Buffer = new ArrayBufferWriter<byte>();
        var v1Writer = new KafkaProtocolWriter(v1Buffer);
        request.Write(ref v1Writer, version: 1);

        // v1 should be larger: adds MaxRecords (4B) + BatchSize (4B), removes PartitionMaxBytes (4B) = net +4
        await Assert.That(v1Buffer.WrittenCount).IsGreaterThan(v0Buffer.WrittenCount);
    }

    [Test]
    public async Task Request_Write_V2_IncludesShareAcquireModeAndIsRenewAck()
    {
        var topicId = Guid.NewGuid();
        var request = new ShareFetchRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            ShareAcquireMode = 0,
            IsRenewAck = true,
            Topics =
            [
                new ShareFetchRequestTopic
                {
                    TopicId = topicId,
                    Partitions =
                    [
                        new ShareFetchRequestPartition { PartitionIndex = 0 }
                    ]
                }
            ]
        };

        var v1Buffer = new ArrayBufferWriter<byte>();
        var v1Writer = new KafkaProtocolWriter(v1Buffer);
        request.Write(ref v1Writer, version: 1);

        var v2Buffer = new ArrayBufferWriter<byte>();
        var v2Writer = new KafkaProtocolWriter(v2Buffer);
        request.Write(ref v2Writer, version: 2);

        // v2 adds ShareAcquireMode (1B) + IsRenewAck (1B) = net +2
        await Assert.That(v2Buffer.WrittenCount).IsGreaterThan(v1Buffer.WrittenCount);
    }

    [Test]
    public async Task Request_Write_WithAcknowledgementBatches_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var topicId = Guid.NewGuid();
        var request = new ShareFetchRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            Topics =
            [
                new ShareFetchRequestTopic
                {
                    TopicId = topicId,
                    Partitions =
                    [
                        new ShareFetchRequestPartition
                        {
                            PartitionIndex = 0,
                            AcknowledgementBatches =
                            [
                                new ShareFetchAcknowledgementBatch
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
    public async Task Request_Write_WithForgottenTopics_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var topicId = Guid.NewGuid();
        var forgottenId = Guid.NewGuid();
        var request = new ShareFetchRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            Topics =
            [
                new ShareFetchRequestTopic
                {
                    TopicId = topicId,
                    Partitions = [new ShareFetchRequestPartition { PartitionIndex = 0 }]
                }
            ],
            ForgottenTopicsData =
            [
                new ShareFetchForgottenTopic
                {
                    TopicId = forgottenId,
                    Partitions = [0, 1, 2]
                }
            ]
        };
        request.Write(ref writer, version: 0);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Request_Write_NullForgottenTopics_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ShareFetchRequest
        {
            GroupId = "test",
            MemberId = "member-1",
            Topics = [],
            ForgottenTopicsData = null
        };
        request.Write(ref writer, version: 0);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    #endregion

    #region Response Construction

    [Test]
    public async Task Response_CanBeConstructed_WithRequiredFields()
    {
        var response = new ShareFetchResponse
        {
            ErrorCode = ErrorCode.None,
            Responses = [],
            NodeEndpoints = []
        };

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.ErrorMessage).IsNull();
        await Assert.That(response.AcquisitionLockTimeoutMs).IsEqualTo(0);
        await Assert.That(response.Responses.Count).IsEqualTo(0);
        await Assert.That(response.NodeEndpoints.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Response_CanBeConstructed_WithAllFields()
    {
        var topicId = Guid.NewGuid();
        var response = new ShareFetchResponse
        {
            ThrottleTimeMs = 100,
            ErrorCode = ErrorCode.None,
            ErrorMessage = null,
            AcquisitionLockTimeoutMs = 30000,
            Responses =
            [
                new ShareFetchResponseTopic
                {
                    TopicId = topicId,
                    Partitions =
                    [
                        new ShareFetchResponsePartition
                        {
                            PartitionIndex = 0,
                            ErrorCode = ErrorCode.None,
                            AcknowledgeErrorCode = ErrorCode.None,
                            CurrentLeader = new ShareFetchLeaderIdAndEpoch
                            {
                                LeaderId = 1,
                                LeaderEpoch = 5
                            },
                            AcquiredRecords =
                            [
                                new ShareFetchAcquiredRecords
                                {
                                    FirstOffset = 0,
                                    LastOffset = 99,
                                    DeliveryCount = 1
                                }
                            ]
                        }
                    ]
                }
            ],
            NodeEndpoints =
            [
                new ShareFetchNodeEndpoint
                {
                    NodeId = 1,
                    Host = "broker-1",
                    Port = 9092,
                    Rack = "rack-a"
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
        var partition = new ShareFetchResponsePartition
        {
            PartitionIndex = 0,
            ErrorCode = ErrorCode.None,
            CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
            AcquiredRecords = []
        };

        await Assert.That(partition.CurrentLeader.LeaderId).IsEqualTo(-1);
        await Assert.That(partition.CurrentLeader.LeaderEpoch).IsEqualTo(-1);
    }

    [Test]
    public async Task Response_Partition_RecordBytes_DefaultsToEmpty()
    {
        var partition = new ShareFetchResponsePartition
        {
            PartitionIndex = 0,
            CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
            AcquiredRecords = []
        };

        await Assert.That(partition.RecordBytes.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Response_LeaderIdAndEpoch_DefaultsToNegativeOne()
    {
        var leader = new ShareFetchLeaderIdAndEpoch();

        await Assert.That(leader.LeaderId).IsEqualTo(-1);
        await Assert.That(leader.LeaderEpoch).IsEqualTo(-1);
    }

    [Test]
    public async Task Response_AcquiredRecords_AllFieldsPopulated()
    {
        var ar = new ShareFetchAcquiredRecords
        {
            FirstOffset = 100,
            LastOffset = 199,
            DeliveryCount = 3
        };

        await Assert.That(ar.FirstOffset).IsEqualTo(100L);
        await Assert.That(ar.LastOffset).IsEqualTo(199L);
        await Assert.That(ar.DeliveryCount).IsEqualTo((short)3);
    }

    [Test]
    public async Task Response_NodeEndpoint_AllFieldsPopulated()
    {
        var ep = new ShareFetchNodeEndpoint
        {
            NodeId = 2,
            Host = "broker-2",
            Port = 9093,
            Rack = "rack-b"
        };

        await Assert.That(ep.NodeId).IsEqualTo(2);
        await Assert.That(ep.Host).IsEqualTo("broker-2");
        await Assert.That(ep.Port).IsEqualTo(9093);
        await Assert.That(ep.Rack).IsEqualTo("rack-b");
    }

    #endregion

    #region Response API Metadata

    [Test]
    public async Task Response_ApiKey_IsShareFetch()
    {
        await Assert.That(ShareFetchResponse.ApiKey).IsEqualTo(ApiKey.ShareFetch);
    }

    [Test]
    public async Task Response_LowestSupportedVersion_Is0()
    {
        await Assert.That(ShareFetchResponse.LowestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task Response_HighestSupportedVersion_Is2()
    {
        await Assert.That(ShareFetchResponse.HighestSupportedVersion).IsEqualTo((short)2);
    }

    #endregion

    #region Response Wire Format Parsing

    [Test]
    public async Task Response_Read_V0_EmptyResponse_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteInt16(0);              // ErrorCode = None
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        // No AcquisitionLockTimeoutMs in v0
        writer.WriteUnsignedVarInt(0 + 1); // Responses: empty compact array
        writer.WriteUnsignedVarInt(0 + 1); // NodeEndpoints: empty compact array
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareFetchResponse)ShareFetchResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.AcquisitionLockTimeoutMs).IsEqualTo(0);
        await Assert.That(response.Responses.Count).IsEqualTo(0);
        await Assert.That(response.NodeEndpoints.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Response_Read_V1_WithAcquisitionLockTimeout_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteInt16(0);              // ErrorCode = None
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        writer.WriteInt32(15000);          // AcquisitionLockTimeoutMs (v1+)
        writer.WriteUnsignedVarInt(0 + 1); // Responses: empty
        writer.WriteUnsignedVarInt(0 + 1); // NodeEndpoints: empty
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareFetchResponse)ShareFetchResponse.Read(ref reader, version: 1);

        await Assert.That(response.AcquisitionLockTimeoutMs).IsEqualTo(15000);
    }

    [Test]
    public async Task Response_Read_WithPartitionData_ParsesCorrectly()
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
        // Partitions: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        // Partition[0]
        writer.WriteInt32(0);              // PartitionIndex
        writer.WriteInt16(0);              // ErrorCode = None
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        writer.WriteInt16(0);              // AcknowledgeErrorCode = None
        writer.WriteUnsignedVarInt(0);     // AcknowledgeErrorMessage = null
        writer.WriteInt32(-1);             // CurrentLeader.LeaderId (default)
        writer.WriteInt32(-1);             // CurrentLeader.LeaderEpoch (default)
        writer.WriteUnsignedVarInt(0);     // CurrentLeader tagged fields
        writer.WriteUnsignedVarInt(0);     // RecordBytes = null (length+1=0)
        // AcquiredRecords: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteInt64(0);              // FirstOffset
        writer.WriteInt64(99);             // LastOffset
        writer.WriteInt16(1);              // DeliveryCount
        writer.WriteUnsignedVarInt(0);     // AcquiredRecords[0] tagged fields
        writer.WriteUnsignedVarInt(0);     // Partition[0] tagged fields
        writer.WriteUnsignedVarInt(0);     // Topic[0] tagged fields
        // NodeEndpoints: empty
        writer.WriteUnsignedVarInt(0 + 1);
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareFetchResponse)ShareFetchResponse.Read(ref reader, version: 0);

        await Assert.That(response.Responses.Count).IsEqualTo(1);
        await Assert.That(response.Responses[0].TopicId).IsEqualTo(topicId);
        await Assert.That(response.Responses[0].Partitions.Count).IsEqualTo(1);

        var partition = response.Responses[0].Partitions[0];
        await Assert.That(partition.PartitionIndex).IsEqualTo(0);
        await Assert.That(partition.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(partition.AcknowledgeErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(partition.CurrentLeader).IsNotNull();
        await Assert.That(partition.CurrentLeader!.LeaderId).IsEqualTo(-1);
        await Assert.That(partition.CurrentLeader.LeaderEpoch).IsEqualTo(-1);
        await Assert.That(partition.RecordBytes.Length).IsEqualTo(0);
        await Assert.That(partition.AcquiredRecords.Count).IsEqualTo(1);
        await Assert.That(partition.AcquiredRecords[0].FirstOffset).IsEqualTo(0L);
        await Assert.That(partition.AcquiredRecords[0].LastOffset).IsEqualTo(99L);
        await Assert.That(partition.AcquiredRecords[0].DeliveryCount).IsEqualTo((short)1);
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
        writer.WriteInt16(0);              // AcknowledgeErrorCode
        writer.WriteUnsignedVarInt(0);     // AcknowledgeErrorMessage = null
        writer.WriteInt32(3);              // CurrentLeader.LeaderId
        writer.WriteInt32(7);              // CurrentLeader.LeaderEpoch
        writer.WriteUnsignedVarInt(0);     // CurrentLeader tagged fields
        writer.WriteUnsignedVarInt(0);     // RecordBytes = null
        writer.WriteUnsignedVarInt(0 + 1); // AcquiredRecords: empty
        writer.WriteUnsignedVarInt(0);     // Partition[0] tagged fields
        writer.WriteUnsignedVarInt(0);     // Topic[0] tagged fields
        writer.WriteUnsignedVarInt(0 + 1); // NodeEndpoints: empty
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareFetchResponse)ShareFetchResponse.Read(ref reader, version: 0);

        var partition = response.Responses[0].Partitions[0];
        await Assert.That(partition.CurrentLeader).IsNotNull();
        await Assert.That(partition.CurrentLeader!.LeaderId).IsEqualTo(3);
        await Assert.That(partition.CurrentLeader.LeaderEpoch).IsEqualTo(7);
    }

    [Test]
    public async Task Response_Read_WithRecordBytes_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();
        var recordData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

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
        writer.WriteInt16(0);              // AcknowledgeErrorCode
        writer.WriteUnsignedVarInt(0);     // AcknowledgeErrorMessage = null
        writer.WriteInt32(-1);             // CurrentLeader.LeaderId (default)
        writer.WriteInt32(-1);             // CurrentLeader.LeaderEpoch (default)
        writer.WriteUnsignedVarInt(0);     // CurrentLeader tagged fields
        // RecordBytes: 5 bytes (length+1 = 6)
        writer.WriteUnsignedVarInt(recordData.Length + 1);
        writer.WriteRawBytes(recordData);
        writer.WriteUnsignedVarInt(0 + 1); // AcquiredRecords: empty
        writer.WriteUnsignedVarInt(0);     // Partition[0] tagged fields
        writer.WriteUnsignedVarInt(0);     // Topic[0] tagged fields
        writer.WriteUnsignedVarInt(0 + 1); // NodeEndpoints: empty
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareFetchResponse)ShareFetchResponse.Read(ref reader, version: 0);

        var partition = response.Responses[0].Partitions[0];
        await Assert.That(partition.RecordBytes.Length).IsEqualTo(5);
        await Assert.That(partition.RecordBytes.Span[0]).IsEqualTo((byte)0x01);
        await Assert.That(partition.RecordBytes.Span[4]).IsEqualTo((byte)0x05);
    }

    [Test]
    public async Task Response_Read_WithAcknowledgeError_ParsesCorrectly()
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
        writer.WriteInt16(0);              // ErrorCode = None
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        writer.WriteInt16(75);             // AcknowledgeErrorCode (non-zero)
        WriteCompactNullableString(ref writer, "Invalid acknowledgement"); // AcknowledgeErrorMessage
        writer.WriteInt32(-1);             // CurrentLeader.LeaderId (default)
        writer.WriteInt32(-1);             // CurrentLeader.LeaderEpoch (default)
        writer.WriteUnsignedVarInt(0);     // CurrentLeader tagged fields
        writer.WriteUnsignedVarInt(0);     // RecordBytes = null
        writer.WriteUnsignedVarInt(0 + 1); // AcquiredRecords: empty
        writer.WriteUnsignedVarInt(0);     // Partition tagged fields
        writer.WriteUnsignedVarInt(0);     // Topic tagged fields
        writer.WriteUnsignedVarInt(0 + 1); // NodeEndpoints: empty
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareFetchResponse)ShareFetchResponse.Read(ref reader, version: 0);

        var partition = response.Responses[0].Partitions[0];
        await Assert.That(partition.AcknowledgeErrorCode).IsEqualTo((ErrorCode)75);
        await Assert.That(partition.AcknowledgeErrorMessage).IsEqualTo("Invalid acknowledgement");
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
        // NodeEndpoints: 2 elements
        writer.WriteUnsignedVarInt(2 + 1);
        // NodeEndpoint[0]
        writer.WriteInt32(1);              // NodeId
        writer.WriteCompactString("broker-1"); // Host (non-nullable)
        writer.WriteInt32(9092);           // Port
        writer.WriteCompactString("rack-a");   // Rack
        writer.WriteUnsignedVarInt(0);     // NodeEndpoint[0] tagged fields
        // NodeEndpoint[1]
        writer.WriteInt32(2);              // NodeId
        writer.WriteCompactString("broker-2"); // Host
        writer.WriteInt32(9093);           // Port
        writer.WriteUnsignedVarInt(0);     // Rack = null
        writer.WriteUnsignedVarInt(0);     // NodeEndpoint[1] tagged fields
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareFetchResponse)ShareFetchResponse.Read(ref reader, version: 0);

        await Assert.That(response.NodeEndpoints.Count).IsEqualTo(2);
        await Assert.That(response.NodeEndpoints[0].NodeId).IsEqualTo(1);
        await Assert.That(response.NodeEndpoints[0].Host).IsEqualTo("broker-1");
        await Assert.That(response.NodeEndpoints[0].Port).IsEqualTo(9092);
        await Assert.That(response.NodeEndpoints[0].Rack).IsEqualTo("rack-a");
        await Assert.That(response.NodeEndpoints[1].NodeId).IsEqualTo(2);
        await Assert.That(response.NodeEndpoints[1].Host).IsEqualTo("broker-2");
        await Assert.That(response.NodeEndpoints[1].Port).IsEqualTo(9093);
        await Assert.That(response.NodeEndpoints[1].Rack).IsNull();
    }

    [Test]
    public async Task Response_Read_WithMultipleAcquiredRecords_ParsesCorrectly()
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
        writer.WriteInt16(0);              // AcknowledgeErrorCode
        writer.WriteUnsignedVarInt(0);     // AcknowledgeErrorMessage = null
        writer.WriteInt32(-1);             // CurrentLeader.LeaderId (default)
        writer.WriteInt32(-1);             // CurrentLeader.LeaderEpoch (default)
        writer.WriteUnsignedVarInt(0);     // CurrentLeader tagged fields
        writer.WriteUnsignedVarInt(0);     // RecordBytes = null
        // AcquiredRecords: 3 elements
        writer.WriteUnsignedVarInt(3 + 1);
        // AcquiredRecords[0]
        writer.WriteInt64(0);
        writer.WriteInt64(49);
        writer.WriteInt16(1);
        writer.WriteUnsignedVarInt(0);
        // AcquiredRecords[1]
        writer.WriteInt64(50);
        writer.WriteInt64(99);
        writer.WriteInt16(2);
        writer.WriteUnsignedVarInt(0);
        // AcquiredRecords[2]
        writer.WriteInt64(100);
        writer.WriteInt64(149);
        writer.WriteInt16(5);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(0);     // Partition tagged fields
        writer.WriteUnsignedVarInt(0);     // Topic tagged fields
        writer.WriteUnsignedVarInt(0 + 1); // NodeEndpoints: empty
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareFetchResponse)ShareFetchResponse.Read(ref reader, version: 0);

        var records = response.Responses[0].Partitions[0].AcquiredRecords;
        await Assert.That(records.Count).IsEqualTo(3);
        await Assert.That(records[0].FirstOffset).IsEqualTo(0L);
        await Assert.That(records[0].LastOffset).IsEqualTo(49L);
        await Assert.That(records[0].DeliveryCount).IsEqualTo((short)1);
        await Assert.That(records[1].FirstOffset).IsEqualTo(50L);
        await Assert.That(records[1].LastOffset).IsEqualTo(99L);
        await Assert.That(records[1].DeliveryCount).IsEqualTo((short)2);
        await Assert.That(records[2].FirstOffset).IsEqualTo(100L);
        await Assert.That(records[2].DeliveryCount).IsEqualTo((short)5);
    }

    [Test]
    public async Task Response_Read_WithPartitionError_ParsesCorrectly()
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
        writer.WriteInt16(6);              // ErrorCode = NotLeaderOrFollower
        WriteCompactNullableString(ref writer, "Not the leader"); // ErrorMessage
        writer.WriteInt16(0);              // AcknowledgeErrorCode
        writer.WriteUnsignedVarInt(0);     // AcknowledgeErrorMessage = null
        writer.WriteInt32(-1);             // CurrentLeader.LeaderId (default)
        writer.WriteInt32(-1);             // CurrentLeader.LeaderEpoch (default)
        writer.WriteUnsignedVarInt(0);     // CurrentLeader tagged fields
        writer.WriteUnsignedVarInt(0);     // RecordBytes = null
        writer.WriteUnsignedVarInt(0 + 1); // AcquiredRecords: empty
        writer.WriteUnsignedVarInt(0);     // Partition tagged fields
        writer.WriteUnsignedVarInt(0);     // Topic tagged fields
        writer.WriteUnsignedVarInt(0 + 1); // NodeEndpoints: empty
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ShareFetchResponse)ShareFetchResponse.Read(ref reader, version: 0);

        var partition = response.Responses[0].Partitions[0];
        await Assert.That(partition.ErrorCode).IsEqualTo((ErrorCode)6);
        await Assert.That(partition.ErrorMessage).IsEqualTo("Not the leader");
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
    public async Task AcknowledgementBatch_WriteAndRead_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareFetchAcknowledgementBatch
        {
            FirstOffset = 100,
            LastOffset = 199,
            AcknowledgeTypes = [1, 2, 3, 1]
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareFetchAcknowledgementBatch.Read(ref reader);

        await Assert.That(deserialized.FirstOffset).IsEqualTo(100L);
        await Assert.That(deserialized.LastOffset).IsEqualTo(199L);
        await Assert.That(deserialized.AcknowledgeTypes.Count).IsEqualTo(4);
        await Assert.That(deserialized.AcknowledgeTypes[0]).IsEqualTo((byte)1);
        await Assert.That(deserialized.AcknowledgeTypes[1]).IsEqualTo((byte)2);
        await Assert.That(deserialized.AcknowledgeTypes[2]).IsEqualTo((byte)3);
        await Assert.That(deserialized.AcknowledgeTypes[3]).IsEqualTo((byte)1);
    }

    [Test]
    public async Task ForgottenTopic_WriteAndRead_RoundTrips()
    {
        var topicId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareFetchForgottenTopic
        {
            TopicId = topicId,
            Partitions = [0, 1, 2]
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareFetchForgottenTopic.Read(ref reader);

        await Assert.That(deserialized.TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.Partitions.Count).IsEqualTo(3);
        await Assert.That(deserialized.Partitions[0]).IsEqualTo(0);
        await Assert.That(deserialized.Partitions[1]).IsEqualTo(1);
        await Assert.That(deserialized.Partitions[2]).IsEqualTo(2);
    }

    [Test]
    public async Task RequestTopic_WriteAndRead_RoundTrips()
    {
        var topicId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareFetchRequestTopic
        {
            TopicId = topicId,
            Partitions =
            [
                new ShareFetchRequestPartition
                {
                    PartitionIndex = 0,
                    PartitionMaxBytes = 65536
                }
            ]
        };
        original.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareFetchRequestTopic.Read(ref reader, version: 0);

        await Assert.That(deserialized.TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.Partitions.Count).IsEqualTo(1);
        await Assert.That(deserialized.Partitions[0].PartitionIndex).IsEqualTo(0);
        await Assert.That(deserialized.Partitions[0].PartitionMaxBytes).IsEqualTo(65536);
    }

    [Test]
    public async Task RequestPartition_V0_WithAckBatches_WriteAndRead_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareFetchRequestPartition
        {
            PartitionIndex = 3,
            PartitionMaxBytes = 32768,
            AcknowledgementBatches =
            [
                new ShareFetchAcknowledgementBatch
                {
                    FirstOffset = 10,
                    LastOffset = 19,
                    AcknowledgeTypes = [1, 1]
                }
            ]
        };
        original.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareFetchRequestPartition.Read(ref reader, version: 0);

        await Assert.That(deserialized.PartitionIndex).IsEqualTo(3);
        await Assert.That(deserialized.PartitionMaxBytes).IsEqualTo(32768);
        await Assert.That(deserialized.AcknowledgementBatches).IsNotNull();
        await Assert.That(deserialized.AcknowledgementBatches!.Count).IsEqualTo(1);
        await Assert.That(deserialized.AcknowledgementBatches[0].FirstOffset).IsEqualTo(10L);
    }

    [Test]
    public async Task RequestPartition_V1_DoesNotIncludePartitionMaxBytes()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new ShareFetchRequestPartition
        {
            PartitionIndex = 0,
            PartitionMaxBytes = 65536 // Should be ignored in v1
        };
        original.Write(ref writer, version: 1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = ShareFetchRequestPartition.Read(ref reader, version: 1);

        await Assert.That(deserialized.PartitionIndex).IsEqualTo(0);
        await Assert.That(deserialized.PartitionMaxBytes).IsEqualTo(0); // Not written/read in v1
    }

    #endregion
}
