using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for Kafka protocol message encoding/decoding.
/// These tests verify that messages are encoded correctly according to the Kafka spec.
/// </summary>
public class MessageEncodingTests
{
    #region ApiVersions Request Tests

    [Test]
    public async Task ApiVersionsRequest_V0_EmptyBody()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ApiVersionsRequest();
        request.Write(ref writer, version: 0);

        // v0 has no body
        await Assert.That(buffer.WrittenCount).IsEqualTo(0);
    }

    [Test]
    public async Task ApiVersionsRequest_V3_WithClientInfo()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new ApiVersionsRequest
        {
            ClientSoftwareName = "dekaf",
            ClientSoftwareVersion = "1.0"
        };
        request.Write(ref writer, version: 3);

        var expected = new List<byte>();
        // ClientSoftwareName: COMPACT_STRING "dekaf" (length+1=6)
        expected.Add(0x06);
        expected.AddRange("dekaf"u8.ToArray());
        // ClientSoftwareVersion: COMPACT_STRING "1.0" (length+1=4)
        expected.Add(0x04);
        expected.AddRange("1.0"u8.ToArray());
        // Empty tagged fields
        expected.Add(0x00);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    #endregion

    #region Metadata Request Tests

    [Test]
    public async Task MetadataRequest_V0_SingleTopic()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = MetadataRequest.ForTopics("test-topic");
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // Array length (INT32)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        // Topic name (STRING with INT16 length prefix)
        expected.AddRange(new byte[] { 0x00, 0x0A }); // length = 10
        expected.AddRange("test-topic"u8.ToArray());

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task MetadataRequest_V9_Flexible_SingleTopic()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = MetadataRequest.ForTopics("test");
        request.Write(ref writer, version: 9);

        var expected = new List<byte>();
        // COMPACT_ARRAY length+1 = 2
        expected.Add(0x02);
        // Topic: COMPACT_NULLABLE_STRING "test" (length+1=5)
        expected.Add(0x05);
        expected.AddRange("test"u8.ToArray());
        // Topic tagged fields
        expected.Add(0x00);
        // AllowAutoTopicCreation (v4+)
        expected.Add(0x01); // true
        // IncludeClusterAuthorizedOperations (v8+)
        expected.Add(0x00); // false
        // IncludeTopicAuthorizedOperations (v8+)
        expected.Add(0x00); // false
        // Request tagged fields
        expected.Add(0x00);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task MetadataRequest_V1_NullTopics_FetchAll()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = MetadataRequest.ForAllTopics();
        request.Write(ref writer, version: 1);

        // Null array = -1 (INT32)
        await Assert.That(buffer.WrittenSpan.ToArray())
            .IsEquivalentTo(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
    }

    [Test]
    public async Task MetadataRequest_V9_NullTopics_FetchAll()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = MetadataRequest.ForAllTopics();
        request.Write(ref writer, version: 9);

        var expected = new List<byte>();
        // COMPACT_NULLABLE_ARRAY: 0 = null
        expected.Add(0x00);
        // AllowAutoTopicCreation
        expected.Add(0x01);
        // IncludeClusterAuthorizedOperations
        expected.Add(0x00);
        // IncludeTopicAuthorizedOperations
        expected.Add(0x00);
        // Tagged fields
        expected.Add(0x00);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    #endregion

    #region ApiVersions Response Tests

    [Test]
    public async Task ApiVersionsResponse_V0_CanBeParsed()
    {
        // Construct a valid v0 response
        var data = new List<byte>();
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ApiKeys array (INT32 length + entries)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x02 }); // 2 entries
        // Entry 1: Produce (0)
        data.AddRange(new byte[] { 0x00, 0x00 }); // ApiKey = 0
        data.AddRange(new byte[] { 0x00, 0x00 }); // MinVersion = 0
        data.AddRange(new byte[] { 0x00, 0x09 }); // MaxVersion = 9
        // Entry 2: Fetch (1)
        data.AddRange(new byte[] { 0x00, 0x01 }); // ApiKey = 1
        data.AddRange(new byte[] { 0x00, 0x00 }); // MinVersion = 0
        data.AddRange(new byte[] { 0x00, 0x0C }); // MaxVersion = 12

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ApiKeys.Count).IsEqualTo(2);
        await Assert.That(response.ApiKeys[0].ApiKey).IsEqualTo(ApiKey.Produce);
        await Assert.That(response.ApiKeys[0].MaxVersion).IsEqualTo((short)9);
        await Assert.That(response.ApiKeys[1].ApiKey).IsEqualTo(ApiKey.Fetch);
        await Assert.That(response.ApiKeys[1].MaxVersion).IsEqualTo((short)12);
    }

    [Test]
    public async Task ApiVersionsResponse_V3_Flexible_CanBeParsed()
    {
        // Construct a valid v3 (flexible) response
        var data = new List<byte>();
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ApiKeys COMPACT_ARRAY (length+1 = 2, so 1 entry)
        data.Add(0x02);
        // Entry: Produce (0)
        data.AddRange(new byte[] { 0x00, 0x00 }); // ApiKey = 0
        data.AddRange(new byte[] { 0x00, 0x00 }); // MinVersion = 0
        data.AddRange(new byte[] { 0x00, 0x0B }); // MaxVersion = 11
        data.Add(0x00); // Entry tagged fields
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // 0ms
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 3);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ApiKeys.Count).IsEqualTo(1);
        await Assert.That(response.ApiKeys[0].ApiKey).IsEqualTo(ApiKey.Produce);
        await Assert.That(response.ApiKeys[0].MaxVersion).IsEqualTo((short)11);
        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
    }

    #endregion

    #region FindCoordinator Tests

    [Test]
    public async Task FindCoordinatorRequest_V0_EncodedCorrectly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new FindCoordinatorRequest
        {
            Key = "my-group",
            KeyType = CoordinatorType.Group
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // Key (STRING with INT16 length prefix)
        expected.AddRange(new byte[] { 0x00, 0x08 }); // length = 8
        expected.AddRange("my-group"u8.ToArray());

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task FindCoordinatorRequest_V1_WithKeyType()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new FindCoordinatorRequest
        {
            Key = "tx-id",
            KeyType = CoordinatorType.Transaction
        };
        request.Write(ref writer, version: 1);

        var expected = new List<byte>();
        // Key (STRING)
        expected.AddRange(new byte[] { 0x00, 0x05 }); // length = 5
        expected.AddRange("tx-id"u8.ToArray());
        // KeyType (INT8)
        expected.Add(0x01); // Transaction

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task FindCoordinatorRequest_V5_BatchFormat()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new FindCoordinatorRequest
        {
            Key = "my-group",
            KeyType = CoordinatorType.Group
        };
        request.Write(ref writer, version: 5);

        // v5 uses CoordinatorKeys array, not Key field
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // KeyType comes first (v1+)
        var keyType = reader.ReadInt8();
        // CoordinatorKeys COMPACT_ARRAY
        var keysLength = reader.ReadUnsignedVarInt() - 1;
        var firstKey = reader.ReadCompactString();

        await Assert.That(keyType).IsEqualTo((sbyte)0); // Group
        await Assert.That(keysLength).IsEqualTo(1);
        await Assert.That(firstKey).IsEqualTo("my-group");
    }

    [Test]
    public async Task FindCoordinatorResponse_V4_BatchFormat_CanBeParsed()
    {
        // Construct a v4+ response with Coordinators array
        var data = new List<byte>();
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        // Coordinators COMPACT_ARRAY (length+1 = 2, so 1 entry)
        data.Add(0x02);
        // Coordinator entry:
        // Key (COMPACT_STRING)
        data.Add(0x09); // length+1 = 9
        data.AddRange("my-group"u8.ToArray());
        // NodeId (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1
        // Host (COMPACT_STRING)
        data.Add(0x0A); // length+1 = 10
        data.AddRange("localhost"u8.ToArray());
        // Port (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x23, 0x84 }); // 9092
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ErrorMessage (COMPACT_NULLABLE_STRING)
        data.Add(0x00); // null
        // Entry tagged fields
        data.Add(0x00);
        // Response tagged fields
        data.Add(0x00);

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (FindCoordinatorResponse)FindCoordinatorResponse.Read(ref reader, version: 4);

        await Assert.That(response.Coordinators).IsNotNull();
        await Assert.That(response.Coordinators!.Count).IsEqualTo(1);
        await Assert.That(response.Coordinators[0].Key).IsEqualTo("my-group");
        await Assert.That(response.Coordinators[0].NodeId).IsEqualTo(1);
        await Assert.That(response.Coordinators[0].Host).IsEqualTo("localhost");
        await Assert.That(response.Coordinators[0].Port).IsEqualTo(9092);
        await Assert.That(response.Coordinators[0].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    #endregion

    #region Version Flexibility Tests

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)1, false)]
    [Arguments((short)2, false)]
    [Arguments((short)3, true)]
    public async Task ApiVersionsRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = ApiVersionsRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)8, false)]
    [Arguments((short)9, true)]
    [Arguments((short)12, true)]
    public async Task MetadataRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = MetadataRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    [Test]
    [Arguments((short)0, false)]
    [Arguments((short)2, false)]
    [Arguments((short)3, true)]
    [Arguments((short)5, true)]
    public async Task FindCoordinatorRequest_FlexibilityDetection(short version, bool expectedFlexible)
    {
        var isFlexible = FindCoordinatorRequest.IsFlexibleVersion(version);
        await Assert.That(isFlexible).IsEqualTo(expectedFlexible);
    }

    #endregion

    #region OffsetDelete Tests

    [Test]
    public async Task OffsetDeleteRequest_V0_SingleTopicPartition()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new OffsetDeleteRequest
        {
            GroupId = "my-group",
            Topics =
            [
                new OffsetDeleteRequestTopic
                {
                    Name = "test-topic",
                    Partitions =
                    [
                        new OffsetDeleteRequestPartition { PartitionIndex = 0 }
                    ]
                }
            ]
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // GroupId (STRING with INT16 length prefix)
        expected.AddRange(new byte[] { 0x00, 0x08 }); // length = 8
        expected.AddRange("my-group"u8.ToArray());
        // Topics array (INT32 length)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 topic
        // Topic name (STRING)
        expected.AddRange(new byte[] { 0x00, 0x0A }); // length = 10
        expected.AddRange("test-topic"u8.ToArray());
        // Partitions array (INT32 length)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 partition
        // PartitionIndex (INT32)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // partition 0

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task OffsetDeleteRequest_V0_MultipleTopicsAndPartitions()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new OffsetDeleteRequest
        {
            GroupId = "grp",
            Topics =
            [
                new OffsetDeleteRequestTopic
                {
                    Name = "t1",
                    Partitions =
                    [
                        new OffsetDeleteRequestPartition { PartitionIndex = 0 },
                        new OffsetDeleteRequestPartition { PartitionIndex = 1 }
                    ]
                },
                new OffsetDeleteRequestTopic
                {
                    Name = "t2",
                    Partitions =
                    [
                        new OffsetDeleteRequestPartition { PartitionIndex = 2 }
                    ]
                }
            ]
        };
        request.Write(ref writer, version: 0);

        var expected = new List<byte>();
        // GroupId (STRING)
        expected.AddRange(new byte[] { 0x00, 0x03 }); // length = 3
        expected.AddRange("grp"u8.ToArray());
        // Topics array (2 topics)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x02 });
        // Topic 1: "t1"
        expected.AddRange(new byte[] { 0x00, 0x02 }); // length = 2
        expected.AddRange("t1"u8.ToArray());
        // Partitions array (2 partitions)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x02 });
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // partition 0
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // partition 1
        // Topic 2: "t2"
        expected.AddRange(new byte[] { 0x00, 0x02 }); // length = 2
        expected.AddRange("t2"u8.ToArray());
        // Partitions array (1 partition)
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        expected.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x02 }); // partition 2

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task OffsetDeleteResponse_V0_Success_CanBeParsed()
    {
        var data = new List<byte>();
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x64 }); // 100ms
        // Topics array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 topic
        // Topic name (STRING)
        data.AddRange(new byte[] { 0x00, 0x04 }); // length = 4
        data.AddRange("test"u8.ToArray());
        // Partitions array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 partition
        // PartitionIndex (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // partition 0
        // Partition ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (OffsetDeleteResponse)OffsetDeleteResponse.Read(ref reader, version: 0);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ThrottleTimeMs).IsEqualTo(100);
        await Assert.That(response.Topics.Count).IsEqualTo(1);
        await Assert.That(response.Topics[0].Name).IsEqualTo("test");
        await Assert.That(response.Topics[0].Partitions.Count).IsEqualTo(1);
        await Assert.That(response.Topics[0].Partitions[0].PartitionIndex).IsEqualTo(0);
        await Assert.That(response.Topics[0].Partitions[0].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    [Test]
    public async Task OffsetDeleteResponse_V0_WithPartitionError_CanBeParsed()
    {
        var data = new List<byte>();
        // ErrorCode (INT16)
        data.AddRange(new byte[] { 0x00, 0x00 }); // None (top-level)
        // ThrottleTimeMs (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // 0ms
        // Topics array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 topic
        // Topic name (STRING)
        data.AddRange(new byte[] { 0x00, 0x05 }); // length = 5
        data.AddRange("topic"u8.ToArray());
        // Partitions array (INT32 length)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01 }); // 1 partition
        // PartitionIndex (INT32)
        data.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x05 }); // partition 5
        // Partition ErrorCode (INT16) - GROUP_SUBSCRIBED_TO_TOPIC (86)
        data.AddRange(new byte[] { 0x00, 0x56 }); // 86

        var reader = new KafkaProtocolReader(data.ToArray());
        var response = (OffsetDeleteResponse)OffsetDeleteResponse.Read(ref reader, version: 0);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Topics[0].Partitions[0].PartitionIndex).IsEqualTo(5);
        await Assert.That((int)response.Topics[0].Partitions[0].ErrorCode).IsEqualTo(86);
    }

    [Test]
    public async Task OffsetDeleteRequest_ApiKey_IsCorrect()
    {
        await Assert.That(OffsetDeleteRequest.ApiKey).IsEqualTo(ApiKey.OffsetDelete);
    }

    [Test]
    public async Task OffsetDeleteRequest_VersionRange_IsCorrect()
    {
        await Assert.That(OffsetDeleteRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(OffsetDeleteRequest.HighestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task OffsetDeleteRequest_IsNotFlexible()
    {
        // OffsetDelete v0 is not flexible (no tagged fields)
        await Assert.That(OffsetDeleteRequest.IsFlexibleVersion(0)).IsEqualTo(false);
    }

    [Test]
    public async Task OffsetDeleteRequest_HeaderVersions()
    {
        await Assert.That(OffsetDeleteRequest.GetRequestHeaderVersion(0)).IsEqualTo((short)1);
        await Assert.That(OffsetDeleteRequest.GetResponseHeaderVersion(0)).IsEqualTo((short)0);
    }

    #endregion

    #region Header Version Tests

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)2, (short)1, (short)0)]
    [Arguments((short)3, (short)2, (short)1)]
    public async Task ApiVersionsRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = ApiVersionsRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = ApiVersionsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    [Test]
    [Arguments((short)0, (short)1, (short)0)]
    [Arguments((short)8, (short)1, (short)0)]
    [Arguments((short)9, (short)2, (short)1)]
    public async Task MetadataRequest_HeaderVersions(short apiVersion, short expectedRequestHeader, short expectedResponseHeader)
    {
        var requestHeaderVersion = MetadataRequest.GetRequestHeaderVersion(apiVersion);
        var responseHeaderVersion = MetadataRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(requestHeaderVersion).IsEqualTo(expectedRequestHeader);
        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    #endregion
}
