using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for share group admin message encoding/decoding (KIP-932):
/// DescribeShareGroupOffsets, AlterShareGroupOffsets, DeleteShareGroupOffsets.
/// </summary>
public sealed class ShareGroupAdminMessageTests
{
    #region DescribeShareGroupOffsets Request

    [Test]
    public async Task DescribeOffsets_Request_CanBeConstructed_WithRequiredFields()
    {
        var request = new DescribeShareGroupOffsetsRequest
        {
            Groups =
            [
                new DescribeShareGroupOffsetsRequestGroup
                {
                    GroupId = "my-group"
                }
            ]
        };

        await Assert.That(request.Groups.Count).IsEqualTo(1);
        await Assert.That(request.Groups[0].GroupId).IsEqualTo("my-group");
    }

    [Test]
    public async Task DescribeOffsets_Request_CanBeConstructed_WithTopics()
    {
        var request = new DescribeShareGroupOffsetsRequest
        {
            Groups =
            [
                new DescribeShareGroupOffsetsRequestGroup
                {
                    GroupId = "my-group",
                    Topics =
                    [
                        new DescribeShareGroupOffsetsRequestTopic
                        {
                            TopicName = "topic-1",
                            Partitions = [0, 1, 2]
                        }
                    ]
                }
            ]
        };

        await Assert.That(request.Groups[0].Topics).IsNotNull();
        await Assert.That(request.Groups[0].Topics!.Count).IsEqualTo(1);
        await Assert.That(request.Groups[0].Topics![0].TopicName).IsEqualTo("topic-1");
        await Assert.That(request.Groups[0].Topics![0].Partitions.Count).IsEqualTo(3);
    }

    [Test]
    public async Task DescribeOffsets_Request_Topics_CanBeNull()
    {
        var group = new DescribeShareGroupOffsetsRequestGroup
        {
            GroupId = "my-group",
            Topics = null
        };

        await Assert.That(group.Topics).IsNull();
    }

    [Test]
    public async Task DescribeOffsets_Request_MultipleGroups()
    {
        var request = new DescribeShareGroupOffsetsRequest
        {
            Groups =
            [
                new DescribeShareGroupOffsetsRequestGroup { GroupId = "group-1" },
                new DescribeShareGroupOffsetsRequestGroup { GroupId = "group-2" }
            ]
        };

        await Assert.That(request.Groups.Count).IsEqualTo(2);
    }

    #endregion

    #region DescribeShareGroupOffsets Request API Metadata

    [Test]
    public async Task DescribeOffsets_Request_ApiKey_IsDescribeShareGroupOffsets()
    {
        await Assert.That(DescribeShareGroupOffsetsRequest.ApiKey).IsEqualTo(ApiKey.DescribeShareGroupOffsets);
    }

    [Test]
    public async Task DescribeOffsets_Request_LowestSupportedVersion_Is0()
    {
        await Assert.That(DescribeShareGroupOffsetsRequest.LowestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task DescribeOffsets_Request_HighestSupportedVersion_Is1()
    {
        await Assert.That(DescribeShareGroupOffsetsRequest.HighestSupportedVersion).IsEqualTo((short)1);
    }

    #endregion

    #region DescribeShareGroupOffsets Request Encoding

    [Test]
    public async Task DescribeOffsets_Request_Write_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeShareGroupOffsetsRequest
        {
            Groups =
            [
                new DescribeShareGroupOffsetsRequestGroup
                {
                    GroupId = "my-group",
                    Topics =
                    [
                        new DescribeShareGroupOffsetsRequestTopic
                        {
                            TopicName = "topic-1",
                            Partitions = [0, 1]
                        }
                    ]
                }
            ]
        };
        request.Write(ref writer, version: 0);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task DescribeOffsets_Request_Write_NullTopics_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DescribeShareGroupOffsetsRequest
        {
            Groups =
            [
                new DescribeShareGroupOffsetsRequestGroup
                {
                    GroupId = "my-group",
                    Topics = null
                }
            ]
        };
        request.Write(ref writer, version: 0);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    #endregion

    #region DescribeShareGroupOffsets Response

    [Test]
    public async Task DescribeOffsets_Response_CanBeConstructed_WithRequiredFields()
    {
        var response = new DescribeShareGroupOffsetsResponse
        {
            Groups = []
        };

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.Groups.Count).IsEqualTo(0);
    }

    #endregion

    #region DescribeShareGroupOffsets Response API Metadata

    [Test]
    public async Task DescribeOffsets_Response_ApiKey_IsDescribeShareGroupOffsets()
    {
        await Assert.That(DescribeShareGroupOffsetsResponse.ApiKey).IsEqualTo(ApiKey.DescribeShareGroupOffsets);
    }

    [Test]
    public async Task DescribeOffsets_Response_LowestSupportedVersion_Is0()
    {
        await Assert.That(DescribeShareGroupOffsetsResponse.LowestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task DescribeOffsets_Response_HighestSupportedVersion_Is1()
    {
        await Assert.That(DescribeShareGroupOffsetsResponse.HighestSupportedVersion).IsEqualTo((short)1);
    }

    #endregion

    #region DescribeShareGroupOffsets Response Wire Format

    [Test]
    public async Task DescribeOffsets_Response_Read_V0_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        // Groups: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        // Group[0]
        writer.WriteCompactString("my-group"); // GroupId
        // Topics: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        // Topic[0]
        writer.WriteCompactString("topic-1");  // TopicName
        writer.WriteUuid(topicId);             // TopicId
        // Partitions: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        // Partition[0]
        writer.WriteInt32(0);                  // PartitionIndex
        writer.WriteInt64(100);                // StartOffset
        writer.WriteInt32(5);                  // LeaderEpoch
        // No Lag field in v0
        writer.WriteInt16(0);                  // ErrorCode = None
        writer.WriteUnsignedVarInt(0);         // ErrorMessage = null
        writer.WriteUnsignedVarInt(0);         // Partition tagged fields
        writer.WriteUnsignedVarInt(0);         // Topic tagged fields
        writer.WriteInt16(0);                  // Group ErrorCode
        writer.WriteUnsignedVarInt(0);         // Group ErrorMessage = null
        writer.WriteUnsignedVarInt(0);         // Group tagged fields
        writer.WriteUnsignedVarInt(0);         // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DescribeShareGroupOffsetsResponse)DescribeShareGroupOffsetsResponse.Read(ref reader, version: 0);

        await Assert.That(response.Groups.Count).IsEqualTo(1);
        await Assert.That(response.Groups[0].GroupId).IsEqualTo("my-group");
        await Assert.That(response.Groups[0].Topics.Count).IsEqualTo(1);
        await Assert.That(response.Groups[0].Topics[0].TopicName).IsEqualTo("topic-1");
        await Assert.That(response.Groups[0].Topics[0].TopicId).IsEqualTo(topicId);
        await Assert.That(response.Groups[0].Topics[0].Partitions.Count).IsEqualTo(1);

        var partition = response.Groups[0].Topics[0].Partitions[0];
        await Assert.That(partition.PartitionIndex).IsEqualTo(0);
        await Assert.That(partition.StartOffset).IsEqualTo(100L);
        await Assert.That(partition.LeaderEpoch).IsEqualTo(5);
        await Assert.That(partition.Lag).IsEqualTo(-1L); // Default, not read in v0
        await Assert.That(partition.ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task DescribeOffsets_Response_Read_V1_WithLag_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(25);             // ThrottleTimeMs
        // Groups: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        // Group[0]
        writer.WriteCompactString("my-group");
        // Topics: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteCompactString("topic-1");
        writer.WriteUuid(topicId);
        // Partitions: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteInt32(0);                  // PartitionIndex
        writer.WriteInt64(100);                // StartOffset
        writer.WriteInt32(5);                  // LeaderEpoch
        writer.WriteInt64(42);                 // Lag (v1+)
        writer.WriteInt16(0);                  // ErrorCode = None
        writer.WriteUnsignedVarInt(0);         // ErrorMessage = null
        writer.WriteUnsignedVarInt(0);         // Partition tagged fields
        writer.WriteUnsignedVarInt(0);         // Topic tagged fields
        writer.WriteInt16(0);                  // Group ErrorCode
        writer.WriteUnsignedVarInt(0);         // Group ErrorMessage = null
        writer.WriteUnsignedVarInt(0);         // Group tagged fields
        writer.WriteUnsignedVarInt(0);         // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DescribeShareGroupOffsetsResponse)DescribeShareGroupOffsetsResponse.Read(ref reader, version: 1);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(25);
        var partition = response.Groups[0].Topics[0].Partitions[0];
        await Assert.That(partition.StartOffset).IsEqualTo(100L);
        await Assert.That(partition.Lag).IsEqualTo(42L);
    }

    [Test]
    public async Task DescribeOffsets_Response_Read_WithGroupError_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        // Groups: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteCompactString("my-group");
        writer.WriteUnsignedVarInt(0 + 1);     // Topics: empty
        writer.WriteInt16(29);                 // Group ErrorCode = GroupAuthorizationFailed
        WriteCompactNullableString(ref writer, "Not authorized"); // Group ErrorMessage
        writer.WriteUnsignedVarInt(0);         // Group tagged fields
        writer.WriteUnsignedVarInt(0);         // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DescribeShareGroupOffsetsResponse)DescribeShareGroupOffsetsResponse.Read(ref reader, version: 0);

        await Assert.That(response.Groups[0].ErrorCode).IsEqualTo((ErrorCode)29);
        await Assert.That(response.Groups[0].ErrorMessage).IsEqualTo("Not authorized");
    }

    [Test]
    public async Task DescribeOffsets_Response_Read_WithPartitionError_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(1 + 1); // Groups: 1
        writer.WriteCompactString("my-group");
        writer.WriteUnsignedVarInt(1 + 1); // Topics: 1
        writer.WriteCompactString("topic-1");
        writer.WriteUuid(topicId);
        writer.WriteUnsignedVarInt(1 + 1); // Partitions: 1
        writer.WriteInt32(0);              // PartitionIndex
        writer.WriteInt64(-1);             // StartOffset = unknown
        writer.WriteInt32(-1);             // LeaderEpoch = unknown
        writer.WriteInt16(3);              // ErrorCode = UnknownTopicOrPartition
        WriteCompactNullableString(ref writer, "Unknown partition");
        writer.WriteUnsignedVarInt(0);     // Partition tagged fields
        writer.WriteUnsignedVarInt(0);     // Topic tagged fields
        writer.WriteInt16(0);              // Group ErrorCode
        writer.WriteUnsignedVarInt(0);     // Group ErrorMessage = null
        writer.WriteUnsignedVarInt(0);     // Group tagged fields
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DescribeShareGroupOffsetsResponse)DescribeShareGroupOffsetsResponse.Read(ref reader, version: 0);

        var partition = response.Groups[0].Topics[0].Partitions[0];
        await Assert.That(partition.ErrorCode).IsEqualTo((ErrorCode)3);
        await Assert.That(partition.ErrorMessage).IsEqualTo("Unknown partition");
        await Assert.That(partition.StartOffset).IsEqualTo(-1L);
    }

    #endregion

    #region DescribeShareGroupOffsets Nested Type Round-Trips

    [Test]
    public async Task DescribeOffsets_ResponseGroup_WriteAndRead_V0_RoundTrips()
    {
        var topicId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new DescribeShareGroupOffsetsResponseGroup
        {
            GroupId = "my-group",
            Topics =
            [
                new DescribeShareGroupOffsetsResponseTopic
                {
                    TopicName = "topic-1",
                    TopicId = topicId,
                    Partitions =
                    [
                        new DescribeShareGroupOffsetsResponsePartition
                        {
                            PartitionIndex = 0,
                            StartOffset = 500,
                            LeaderEpoch = 3,
                            ErrorCode = ErrorCode.None
                        }
                    ]
                }
            ],
            ErrorCode = ErrorCode.None
        };
        original.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = DescribeShareGroupOffsetsResponseGroup.Read(ref reader, version: 0);

        await Assert.That(deserialized.GroupId).IsEqualTo("my-group");
        await Assert.That(deserialized.Topics.Count).IsEqualTo(1);
        await Assert.That(deserialized.Topics[0].Partitions[0].StartOffset).IsEqualTo(500L);
        await Assert.That(deserialized.ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task DescribeOffsets_ResponsePartition_WriteAndRead_V1_WithLag_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new DescribeShareGroupOffsetsResponsePartition
        {
            PartitionIndex = 2,
            StartOffset = 1000,
            LeaderEpoch = 7,
            Lag = 50,
            ErrorCode = ErrorCode.None
        };
        original.Write(ref writer, version: 1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = DescribeShareGroupOffsetsResponsePartition.Read(ref reader, version: 1);

        await Assert.That(deserialized.PartitionIndex).IsEqualTo(2);
        await Assert.That(deserialized.StartOffset).IsEqualTo(1000L);
        await Assert.That(deserialized.LeaderEpoch).IsEqualTo(7);
        await Assert.That(deserialized.Lag).IsEqualTo(50L);
        await Assert.That(deserialized.ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task DescribeOffsets_ResponsePartition_Lag_DefaultsToNegativeOne()
    {
        var partition = new DescribeShareGroupOffsetsResponsePartition
        {
            PartitionIndex = 0,
            StartOffset = 0,
            LeaderEpoch = 0
        };

        await Assert.That(partition.Lag).IsEqualTo(-1L);
    }

    #endregion

    #region AlterShareGroupOffsets Request

    [Test]
    public async Task AlterOffsets_Request_CanBeConstructed_WithRequiredFields()
    {
        var request = new AlterShareGroupOffsetsRequest
        {
            GroupId = "my-group",
            Topics =
            [
                new AlterShareGroupOffsetsRequestTopic
                {
                    TopicName = "topic-1",
                    Partitions =
                    [
                        new AlterShareGroupOffsetsRequestPartition
                        {
                            PartitionIndex = 0,
                            StartOffset = 100
                        }
                    ]
                }
            ]
        };

        await Assert.That(request.GroupId).IsEqualTo("my-group");
        await Assert.That(request.Topics.Count).IsEqualTo(1);
        await Assert.That(request.Topics[0].TopicName).IsEqualTo("topic-1");
        await Assert.That(request.Topics[0].Partitions[0].StartOffset).IsEqualTo(100L);
    }

    [Test]
    public async Task AlterOffsets_Request_MultiplePartitions()
    {
        var request = new AlterShareGroupOffsetsRequest
        {
            GroupId = "my-group",
            Topics =
            [
                new AlterShareGroupOffsetsRequestTopic
                {
                    TopicName = "topic-1",
                    Partitions =
                    [
                        new AlterShareGroupOffsetsRequestPartition { PartitionIndex = 0, StartOffset = 100 },
                        new AlterShareGroupOffsetsRequestPartition { PartitionIndex = 1, StartOffset = 200 },
                        new AlterShareGroupOffsetsRequestPartition { PartitionIndex = 2, StartOffset = 300 }
                    ]
                }
            ]
        };

        await Assert.That(request.Topics[0].Partitions.Count).IsEqualTo(3);
        await Assert.That(request.Topics[0].Partitions[2].StartOffset).IsEqualTo(300L);
    }

    #endregion

    #region AlterShareGroupOffsets Request API Metadata

    [Test]
    public async Task AlterOffsets_Request_ApiKey_IsAlterShareGroupOffsets()
    {
        await Assert.That(AlterShareGroupOffsetsRequest.ApiKey).IsEqualTo(ApiKey.AlterShareGroupOffsets);
    }

    [Test]
    public async Task AlterOffsets_Request_LowestSupportedVersion_Is0()
    {
        await Assert.That(AlterShareGroupOffsetsRequest.LowestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task AlterOffsets_Request_HighestSupportedVersion_Is0()
    {
        await Assert.That(AlterShareGroupOffsetsRequest.HighestSupportedVersion).IsEqualTo((short)0);
    }

    #endregion

    #region AlterShareGroupOffsets Request Encoding

    [Test]
    public async Task AlterOffsets_Request_Write_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new AlterShareGroupOffsetsRequest
        {
            GroupId = "my-group",
            Topics =
            [
                new AlterShareGroupOffsetsRequestTopic
                {
                    TopicName = "topic-1",
                    Partitions =
                    [
                        new AlterShareGroupOffsetsRequestPartition
                        {
                            PartitionIndex = 0,
                            StartOffset = 500
                        }
                    ]
                }
            ]
        };
        request.Write(ref writer, version: 0);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    #endregion

    #region AlterShareGroupOffsets Response

    [Test]
    public async Task AlterOffsets_Response_CanBeConstructed_WithRequiredFields()
    {
        var response = new AlterShareGroupOffsetsResponse
        {
            Responses = []
        };

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ErrorMessage).IsNull();
        await Assert.That(response.Responses.Count).IsEqualTo(0);
    }

    #endregion

    #region AlterShareGroupOffsets Response API Metadata

    [Test]
    public async Task AlterOffsets_Response_ApiKey_IsAlterShareGroupOffsets()
    {
        await Assert.That(AlterShareGroupOffsetsResponse.ApiKey).IsEqualTo(ApiKey.AlterShareGroupOffsets);
    }

    [Test]
    public async Task AlterOffsets_Response_LowestSupportedVersion_Is0()
    {
        await Assert.That(AlterShareGroupOffsetsResponse.LowestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task AlterOffsets_Response_HighestSupportedVersion_Is0()
    {
        await Assert.That(AlterShareGroupOffsetsResponse.HighestSupportedVersion).IsEqualTo((short)0);
    }

    #endregion

    #region AlterShareGroupOffsets Response Wire Format

    [Test]
    public async Task AlterOffsets_Response_Read_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteInt16(0);              // ErrorCode = None
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        // Responses: 1 topic
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteCompactString("topic-1");  // TopicName
        writer.WriteUuid(topicId);             // TopicId
        // Partitions: 1 element
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteInt32(0);              // PartitionIndex
        writer.WriteInt16(0);              // ErrorCode = None
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        writer.WriteUnsignedVarInt(0);     // Partition tagged fields
        writer.WriteUnsignedVarInt(0);     // Topic tagged fields
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (AlterShareGroupOffsetsResponse)AlterShareGroupOffsetsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Responses.Count).IsEqualTo(1);
        await Assert.That(response.Responses[0].TopicName).IsEqualTo("topic-1");
        await Assert.That(response.Responses[0].TopicId).IsEqualTo(topicId);
        await Assert.That(response.Responses[0].Partitions.Count).IsEqualTo(1);
        await Assert.That(response.Responses[0].Partitions[0].PartitionIndex).IsEqualTo(0);
        await Assert.That(response.Responses[0].Partitions[0].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task AlterOffsets_Response_Read_WithErrors_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteInt16(0);              // ErrorCode
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        // Responses: 1 topic
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteCompactString("topic-1");
        writer.WriteUuid(topicId);
        // Partitions: 2 elements
        writer.WriteUnsignedVarInt(2 + 1);
        // Partition[0] - success
        writer.WriteInt32(0);
        writer.WriteInt16(0);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(0);
        // Partition[1] - error
        writer.WriteInt32(1);
        writer.WriteInt16(3);              // UnknownTopicOrPartition
        WriteCompactNullableString(ref writer, "Unknown partition");
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(0);     // Topic tagged fields
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (AlterShareGroupOffsetsResponse)AlterShareGroupOffsetsResponse.Read(ref reader, version: 0);

        await Assert.That(response.Responses[0].Partitions.Count).IsEqualTo(2);
        await Assert.That(response.Responses[0].Partitions[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Responses[0].Partitions[1].ErrorCode).IsEqualTo((ErrorCode)3);
        await Assert.That(response.Responses[0].Partitions[1].ErrorMessage).IsEqualTo("Unknown partition");
    }

    [Test]
    public async Task AlterOffsets_Response_Read_TopLevelError_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(50);             // ThrottleTimeMs
        writer.WriteInt16(29);             // ErrorCode = GroupAuthorizationFailed
        WriteCompactNullableString(ref writer, "Not authorized");
        writer.WriteUnsignedVarInt(0 + 1); // Responses: empty
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (AlterShareGroupOffsetsResponse)AlterShareGroupOffsetsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(50);
        await Assert.That(response.ErrorCode).IsEqualTo((ErrorCode)29);
        await Assert.That(response.ErrorMessage).IsEqualTo("Not authorized");
    }

    #endregion

    #region AlterShareGroupOffsets Nested Type Round-Trips

    [Test]
    public async Task AlterOffsets_RequestTopic_WriteAndRead_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new AlterShareGroupOffsetsRequestTopic
        {
            TopicName = "topic-1",
            Partitions =
            [
                new AlterShareGroupOffsetsRequestPartition
                {
                    PartitionIndex = 0,
                    StartOffset = 500
                },
                new AlterShareGroupOffsetsRequestPartition
                {
                    PartitionIndex = 1,
                    StartOffset = 1000
                }
            ]
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = AlterShareGroupOffsetsRequestTopic.Read(ref reader);

        await Assert.That(deserialized.TopicName).IsEqualTo("topic-1");
        await Assert.That(deserialized.Partitions.Count).IsEqualTo(2);
        await Assert.That(deserialized.Partitions[0].StartOffset).IsEqualTo(500L);
        await Assert.That(deserialized.Partitions[1].StartOffset).IsEqualTo(1000L);
    }

    [Test]
    public async Task AlterOffsets_RequestPartition_WriteAndRead_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new AlterShareGroupOffsetsRequestPartition
        {
            PartitionIndex = 5,
            StartOffset = 9999
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = AlterShareGroupOffsetsRequestPartition.Read(ref reader);

        await Assert.That(deserialized.PartitionIndex).IsEqualTo(5);
        await Assert.That(deserialized.StartOffset).IsEqualTo(9999L);
    }

    [Test]
    public async Task AlterOffsets_ResponseTopic_WriteAndRead_RoundTrips()
    {
        var topicId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new AlterShareGroupOffsetsResponseTopic
        {
            TopicName = "topic-1",
            TopicId = topicId,
            Partitions =
            [
                new AlterShareGroupOffsetsResponsePartition
                {
                    PartitionIndex = 0,
                    ErrorCode = ErrorCode.None
                }
            ]
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = AlterShareGroupOffsetsResponseTopic.Read(ref reader);

        await Assert.That(deserialized.TopicName).IsEqualTo("topic-1");
        await Assert.That(deserialized.TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.Partitions.Count).IsEqualTo(1);
    }

    #endregion

    #region DeleteShareGroupOffsets Request

    [Test]
    public async Task DeleteOffsets_Request_CanBeConstructed_WithRequiredFields()
    {
        var request = new DeleteShareGroupOffsetsRequest
        {
            GroupId = "my-group",
            Topics =
            [
                new DeleteShareGroupOffsetsRequestTopic
                {
                    TopicName = "topic-1"
                }
            ]
        };

        await Assert.That(request.GroupId).IsEqualTo("my-group");
        await Assert.That(request.Topics.Count).IsEqualTo(1);
        await Assert.That(request.Topics[0].TopicName).IsEqualTo("topic-1");
    }

    [Test]
    public async Task DeleteOffsets_Request_MultipleTopics()
    {
        var request = new DeleteShareGroupOffsetsRequest
        {
            GroupId = "my-group",
            Topics =
            [
                new DeleteShareGroupOffsetsRequestTopic { TopicName = "topic-1" },
                new DeleteShareGroupOffsetsRequestTopic { TopicName = "topic-2" },
                new DeleteShareGroupOffsetsRequestTopic { TopicName = "topic-3" }
            ]
        };

        await Assert.That(request.Topics.Count).IsEqualTo(3);
        await Assert.That(request.Topics[0].TopicName).IsEqualTo("topic-1");
        await Assert.That(request.Topics[2].TopicName).IsEqualTo("topic-3");
    }

    #endregion

    #region DeleteShareGroupOffsets Request API Metadata

    [Test]
    public async Task DeleteOffsets_Request_ApiKey_IsDeleteShareGroupOffsets()
    {
        await Assert.That(DeleteShareGroupOffsetsRequest.ApiKey).IsEqualTo(ApiKey.DeleteShareGroupOffsets);
    }

    [Test]
    public async Task DeleteOffsets_Request_LowestSupportedVersion_Is0()
    {
        await Assert.That(DeleteShareGroupOffsetsRequest.LowestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task DeleteOffsets_Request_HighestSupportedVersion_Is0()
    {
        await Assert.That(DeleteShareGroupOffsetsRequest.HighestSupportedVersion).IsEqualTo((short)0);
    }

    #endregion

    #region DeleteShareGroupOffsets Request Encoding

    [Test]
    public async Task DeleteOffsets_Request_Write_EncodesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new DeleteShareGroupOffsetsRequest
        {
            GroupId = "my-group",
            Topics =
            [
                new DeleteShareGroupOffsetsRequestTopic { TopicName = "topic-1" },
                new DeleteShareGroupOffsetsRequestTopic { TopicName = "topic-2" }
            ]
        };
        request.Write(ref writer, version: 0);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
    }

    [Test]
    public async Task DeleteOffsets_Request_TopicHasNoPartitions()
    {
        // DeleteShareGroupOffsetsRequestTopic has only TopicName, no partition list
        var topic = new DeleteShareGroupOffsetsRequestTopic
        {
            TopicName = "topic-1"
        };

        await Assert.That(topic.TopicName).IsEqualTo("topic-1");
    }

    #endregion

    #region DeleteShareGroupOffsets Response

    [Test]
    public async Task DeleteOffsets_Response_CanBeConstructed_WithRequiredFields()
    {
        var response = new DeleteShareGroupOffsetsResponse
        {
            Responses = []
        };

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ErrorMessage).IsNull();
        await Assert.That(response.Responses.Count).IsEqualTo(0);
    }

    #endregion

    #region DeleteShareGroupOffsets Response API Metadata

    [Test]
    public async Task DeleteOffsets_Response_ApiKey_IsDeleteShareGroupOffsets()
    {
        await Assert.That(DeleteShareGroupOffsetsResponse.ApiKey).IsEqualTo(ApiKey.DeleteShareGroupOffsets);
    }

    [Test]
    public async Task DeleteOffsets_Response_LowestSupportedVersion_Is0()
    {
        await Assert.That(DeleteShareGroupOffsetsResponse.LowestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task DeleteOffsets_Response_HighestSupportedVersion_Is0()
    {
        await Assert.That(DeleteShareGroupOffsetsResponse.HighestSupportedVersion).IsEqualTo((short)0);
    }

    #endregion

    #region DeleteShareGroupOffsets Response Wire Format

    [Test]
    public async Task DeleteOffsets_Response_Read_ParsesCorrectly()
    {
        var topicId = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteInt16(0);              // ErrorCode = None
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        // Responses: 1 topic
        writer.WriteUnsignedVarInt(1 + 1);
        writer.WriteCompactString("topic-1");  // TopicName
        writer.WriteUuid(topicId);             // TopicId
        writer.WriteInt16(0);                  // ErrorCode = None
        writer.WriteUnsignedVarInt(0);         // ErrorMessage = null
        writer.WriteUnsignedVarInt(0);         // Topic tagged fields
        writer.WriteUnsignedVarInt(0);         // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DeleteShareGroupOffsetsResponse)DeleteShareGroupOffsetsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Responses.Count).IsEqualTo(1);
        await Assert.That(response.Responses[0].TopicName).IsEqualTo("topic-1");
        await Assert.That(response.Responses[0].TopicId).IsEqualTo(topicId);
        await Assert.That(response.Responses[0].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task DeleteOffsets_Response_Read_WithTopicErrors_ParsesCorrectly()
    {
        var topicId1 = Guid.NewGuid();
        var topicId2 = Guid.NewGuid();

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(0);              // ThrottleTimeMs
        writer.WriteInt16(0);              // ErrorCode
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        // Responses: 2 topics
        writer.WriteUnsignedVarInt(2 + 1);
        // Topic[0] - success
        writer.WriteCompactString("topic-1");
        writer.WriteUuid(topicId1);
        writer.WriteInt16(0);              // ErrorCode = None
        writer.WriteUnsignedVarInt(0);     // ErrorMessage = null
        writer.WriteUnsignedVarInt(0);     // Topic tagged fields
        // Topic[1] - error
        writer.WriteCompactString("topic-2");
        writer.WriteUuid(topicId2);
        writer.WriteInt16(29);             // ErrorCode = GroupAuthorizationFailed
        WriteCompactNullableString(ref writer, "Not authorized");
        writer.WriteUnsignedVarInt(0);     // Topic tagged fields
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DeleteShareGroupOffsetsResponse)DeleteShareGroupOffsetsResponse.Read(ref reader, version: 0);

        await Assert.That(response.Responses.Count).IsEqualTo(2);
        await Assert.That(response.Responses[0].TopicName).IsEqualTo("topic-1");
        await Assert.That(response.Responses[0].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Responses[1].TopicName).IsEqualTo("topic-2");
        await Assert.That(response.Responses[1].ErrorCode).IsEqualTo((ErrorCode)29);
        await Assert.That(response.Responses[1].ErrorMessage).IsEqualTo("Not authorized");
    }

    [Test]
    public async Task DeleteOffsets_Response_Read_TopLevelError_ParsesCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        writer.WriteInt32(100);            // ThrottleTimeMs
        writer.WriteInt16(29);             // ErrorCode = GroupAuthorizationFailed
        WriteCompactNullableString(ref writer, "Not authorized");
        writer.WriteUnsignedVarInt(0 + 1); // Responses: empty
        writer.WriteUnsignedVarInt(0);     // Response tagged fields

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DeleteShareGroupOffsetsResponse)DeleteShareGroupOffsetsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
        await Assert.That(response.ErrorCode).IsEqualTo((ErrorCode)29);
        await Assert.That(response.ErrorMessage).IsEqualTo("Not authorized");
    }

    #endregion

    #region DeleteShareGroupOffsets Nested Type Round-Trips

    [Test]
    public async Task DeleteOffsets_RequestTopic_WriteAndRead_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new DeleteShareGroupOffsetsRequestTopic
        {
            TopicName = "my-topic"
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = DeleteShareGroupOffsetsRequestTopic.Read(ref reader);

        await Assert.That(deserialized.TopicName).IsEqualTo("my-topic");
    }

    [Test]
    public async Task DeleteOffsets_ResponseTopic_WriteAndRead_RoundTrips()
    {
        var topicId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new DeleteShareGroupOffsetsResponseTopic
        {
            TopicName = "topic-1",
            TopicId = topicId,
            ErrorCode = ErrorCode.None
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = DeleteShareGroupOffsetsResponseTopic.Read(ref reader);

        await Assert.That(deserialized.TopicName).IsEqualTo("topic-1");
        await Assert.That(deserialized.TopicId).IsEqualTo(topicId);
        await Assert.That(deserialized.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(deserialized.ErrorMessage).IsNull();
    }

    [Test]
    public async Task DeleteOffsets_ResponseTopic_WriteAndRead_WithError_RoundTrips()
    {
        var topicId = Guid.NewGuid();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new DeleteShareGroupOffsetsResponseTopic
        {
            TopicName = "topic-1",
            TopicId = topicId,
            ErrorCode = (ErrorCode)3,
            ErrorMessage = "Unknown topic"
        };
        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = DeleteShareGroupOffsetsResponseTopic.Read(ref reader);

        await Assert.That(deserialized.ErrorCode).IsEqualTo((ErrorCode)3);
        await Assert.That(deserialized.ErrorMessage).IsEqualTo("Unknown topic");
    }

    #endregion

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
}
