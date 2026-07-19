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
    [Arguments((short)3)]
    [Arguments((short)4)]
    public async Task ApiVersionsRequest_FlexibleVersions_WriteClientInfo(short version)
    {
        var request = new ApiVersionsRequest
        {
            ClientSoftwareName = "dekaf",
            ClientSoftwareVersion = "1.0"
        };
        var actual = SerializeApiVersionsRequest(request, version);

        var expected = new List<byte>();
        // ClientSoftwareName: COMPACT_STRING "dekaf" (length+1=6)
        expected.Add(0x06);
        expected.AddRange("dekaf"u8.ToArray());
        // ClientSoftwareVersion: COMPACT_STRING "1.0" (length+1=4)
        expected.Add(0x04);
        expected.AddRange("1.0"u8.ToArray());
        // Empty tagged fields
        expected.Add(0x00);

        await Assert.That(actual).IsEquivalentTo(expected.ToArray());
    }

    [Test]
    public async Task ApiVersionsRequest_V5_BootstrapWritesUnspecifiedIdentity()
    {
        var actual = SerializeApiVersionsRequest(new ApiVersionsRequest
        {
            ClientSoftwareName = "dekaf",
            ClientSoftwareVersion = "1.0"
        }, version: 5);

        await Assert.That(actual).IsEquivalentTo(
        new byte[]
        {
            0x06, (byte)'d', (byte)'e', (byte)'k', (byte)'a', (byte)'f',
            0x04, (byte)'1', (byte)'.', (byte)'0',
            0x00,
            0xFF, 0xFF, 0xFF, 0xFF,
            0x00
        });
    }

    [Test]
    public async Task ApiVersionsRequest_V5_WritesExpectedClusterAndNodeIdentity()
    {
        var actual = SerializeApiVersionsRequest(new ApiVersionsRequest
        {
            ClientSoftwareName = "dekaf",
            ClientSoftwareVersion = "1.0",
            ClusterId = "cluster-a",
            NodeId = 7
        }, version: 5);

        var reader = new KafkaProtocolReader(actual);
        var clientSoftwareName = reader.ReadCompactNonNullableString();
        var clientSoftwareVersion = reader.ReadCompactNonNullableString();
        var clusterId = reader.ReadCompactString();
        var nodeId = reader.ReadInt32();
        reader.SkipTaggedFields();
        var remaining = reader.Remaining;

        await Assert.That(clientSoftwareName).IsEqualTo("dekaf");
        await Assert.That(clientSoftwareVersion).IsEqualTo("1.0");
        await Assert.That(clusterId).IsEqualTo("cluster-a");
        await Assert.That(nodeId).IsEqualTo(7);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    [Arguments("cluster-a", -1)]
    [Arguments(null, 7)]
    public async Task ApiVersionsRequest_V5_RejectsPartialIdentity(string? clusterId, int nodeId)
    {
        var request = new ApiVersionsRequest
        {
            ClientSoftwareName = "dekaf",
            ClientSoftwareVersion = "1.0",
            ClusterId = clusterId,
            NodeId = nodeId
        };

        await Assert.That(() => SerializeApiVersionsRequest(request, version: 5))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("must be specified together");
    }

    [Test]
    [Arguments((short)0)]
    [Arguments((short)1)]
    [Arguments((short)2)]
    public async Task ApiVersionsRequest_LegacyVersions_HaveEmptyBody(short version)
    {
        var actual = SerializeApiVersionsRequest(new ApiVersionsRequest(), version);

        await Assert.That(actual).IsEmpty();
    }

    [Test]
    [Arguments((short)3)]
    [Arguments((short)4)]
    [Arguments((short)5)]
    public async Task ApiVersionsRequest_FlexibleVersions_RequireClientInfo(short version)
    {
        await Assert.That(() => SerializeApiVersionsRequest(new ApiVersionsRequest(), version))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("Client software name");
    }

    #endregion

    #region Metadata Request Tests

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
    public async Task MetadataRequest_V9_Flexible_IteratorTopics()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = MetadataRequest.ForTopics(IteratorTopics());
        request.Write(ref writer, version: 9);

        var expected = new List<byte>();
        // COMPACT_ARRAY length+1 = 3
        expected.Add(0x03);
        // Topic: COMPACT_NULLABLE_STRING "alpha" (length+1=6)
        expected.Add(0x06);
        expected.AddRange("alpha"u8.ToArray());
        // Topic tagged fields
        expected.Add(0x00);
        // Topic: COMPACT_NULLABLE_STRING "beta" (length+1=5)
        expected.Add(0x05);
        expected.AddRange("beta"u8.ToArray());
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

    [Test]
    public async Task ApiVersionsResponse_V4_PreservesZeroMinimumSupportedFeature()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteUnsignedVarInt(1); // Empty ApiKeys compact array.
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(1); // One root tagged field.
        writer.WriteUnsignedVarInt(0); // SupportedFeatures.

        var feature = new ArrayBufferWriter<byte>();
        var featureWriter = new KafkaProtocolWriter(feature);
        featureWriter.WriteUnsignedVarInt(2); // One compact-array entry.
        featureWriter.WriteCompactString("kraft.version");
        featureWriter.WriteInt16(0);
        featureWriter.WriteInt16(1);
        featureWriter.WriteEmptyTaggedFields();
        writer.WriteUnsignedVarInt(feature.WrittenCount);
        writer.WriteRawBytes(feature.WrittenSpan);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 4);

        var supportedFeature = response.SupportedFeatures!.Single();
        await Assert.That(supportedFeature.Name).IsEqualTo("kraft.version");
        await Assert.That(supportedFeature.MinVersion).IsEqualTo((short)0);
        await Assert.That(supportedFeature.MaxVersion).IsEqualTo((short)1);
    }

    [Test]
    public async Task ApiVersionsResponse_UnsupportedVersion_UsesV0Schema()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.UnsupportedVersion);
        writer.WriteInt32(1);
        writer.WriteInt16((short)ApiKey.ApiVersions);
        writer.WriteInt16(0);
        writer.WriteInt16(3);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 4);

        await Assert.That(reader.End).IsTrue();
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.UnsupportedVersion);
        await Assert.That(response.ThrottleTimeMs).IsEqualTo(0);
        await Assert.That(response.ApiKeys).IsEquivalentTo([
            new ApiVersion(ApiKey.ApiVersions, 0, 3)
        ]);
    }

    #endregion

    #region FindCoordinator Tests

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

        // v5 writes Key as a single-element compact array on the wire
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);

        // KeyType comes first (v1+)
        var keyType = reader.ReadInt8();
        // Key written as COMPACT_ARRAY with one element
        var keysLength = reader.ReadUnsignedVarInt() - 1;
        var firstKey = reader.ReadCompactString();

        await Assert.That(keyType).IsEqualTo((sbyte)0); // Group
        await Assert.That(keysLength).IsEqualTo(1);
        await Assert.That(firstKey).IsEqualTo("my-group");
    }

    [Test]
    public async Task FindCoordinatorRequest_V6_ShareKey_UsesKafkaUuidText()
    {
        var topicId = new Guid("00112233-4455-6677-8899-aabbccddeeff");
        var request = FindCoordinatorRequest.ForSharePartition("group:with:colons", topicId, 7);
        var key = request.Key;
        await Assert.That(key).IsEqualTo("group:with:colons:ABEiM0RVZneImaq7zN3u_w:7");
        await Assert.That(request.KeyType).IsEqualTo(CoordinatorType.Share);

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        request.Write(ref writer, version: 6);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var keyType = reader.ReadInt8();
        var keyCount = reader.ReadUnsignedVarInt();
        var decodedKey = reader.ReadCompactString();
        reader.SkipTaggedFields();
        var readerEnd = reader.End;

        await Assert.That(keyType).IsEqualTo((sbyte)CoordinatorType.Share);
        await Assert.That(keyCount).IsEqualTo(2);
        await Assert.That(decodedKey).IsEqualTo(key);
        await Assert.That(readerEnd).IsTrue();
    }

    [Test]
    [Arguments("", "00112233-4455-6677-8899-aabbccddeeff", 0)]
    [Arguments("group", "00000000-0000-0000-0000-000000000000", 0)]
    [Arguments("group", "00112233-4455-6677-8899-aabbccddeeff", -1)]
    public void FindCoordinatorRequest_ShareKey_RejectsInvalidParts(
        string groupId,
        string topicId,
        int partition)
    {
        Assert.Throws<ArgumentException>(() =>
            FindCoordinatorRequest.BuildShareCoordinatorKey(groupId, Guid.Parse(topicId), partition));
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

        await Assert.That(response.Coordinators.Count).IsEqualTo(1);
        await Assert.That(response.Coordinators[0].Key).IsEqualTo("my-group");
        await Assert.That(response.Coordinators[0].NodeId).IsEqualTo(1);
        await Assert.That(response.Coordinators[0].Host).IsEqualTo("localhost");
        await Assert.That(response.Coordinators[0].Port).IsEqualTo(9092);
        await Assert.That(response.Coordinators[0].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    #endregion

    #region Header Version Tests

    [Test]
    [Arguments((short)0, (short)0)]
    [Arguments((short)1, (short)0)]
    [Arguments((short)2, (short)0)]
    [Arguments((short)3, (short)0)]
    [Arguments((short)4, (short)0)]
    [Arguments((short)5, (short)0)]
    public async Task ApiVersionsRequest_ResponseHeaderVersion(short apiVersion, short expectedResponseHeader)
    {
        var responseHeaderVersion = ApiVersionsRequest.GetResponseHeaderVersion(apiVersion);

        await Assert.That(responseHeaderVersion).IsEqualTo(expectedResponseHeader);
    }

    #endregion

    private static byte[] SerializeApiVersionsRequest(ApiVersionsRequest request, short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        request.Write(ref writer, version);
        return buffer.WrittenSpan.ToArray();
    }

    private static IEnumerable<string> IteratorTopics()
    {
        yield return "alpha";
        yield return "beta";
    }
}
