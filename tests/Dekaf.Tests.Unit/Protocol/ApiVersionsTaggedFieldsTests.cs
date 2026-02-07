using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

/// <summary>
/// Tests for ApiVersions response tagged field parsing (v3+).
/// </summary>
public sealed class ApiVersionsTaggedFieldsTests
{
    /// <summary>
    /// Builds a minimal ApiVersions v3 response with the given tagged fields appended.
    /// </summary>
    private static byte[] BuildApiVersionsV3Response(Action<KafkaProtocolWriter>? writeTaggedFields = null)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        // ErrorCode
        writer.WriteInt16(0);

        // ApiKeys compact array (1 entry)
        writer.WriteUnsignedVarInt(2); // length + 1 = 2 (1 element)
        // ApiKey = ApiVersions (18)
        writer.WriteInt16(18);
        writer.WriteInt16(0); // MinVersion
        writer.WriteInt16(3); // MaxVersion
        writer.WriteEmptyTaggedFields(); // Per-element tagged fields

        // ThrottleTimeMs
        writer.WriteInt32(0);

        // Response-level tagged fields
        if (writeTaggedFields is not null)
        {
            writeTaggedFields(writer);
        }
        else
        {
            writer.WriteEmptyTaggedFields();
        }

        return buffer.WrittenSpan.ToArray();
    }

    [Test]
    public async Task V3_EmptyTaggedFields_ParsesCorrectly()
    {
        var data = BuildApiVersionsV3Response();
        var reader = new KafkaProtocolReader(data);
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 3);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ApiKeys).Count().IsEqualTo(1);
        await Assert.That(response.SupportedFeatures).IsNull();
        await Assert.That(response.FinalizedFeaturesEpoch).IsEqualTo(-1L);
        await Assert.That(response.FinalizedFeatures).IsNull();
        await Assert.That(response.ZkMigrationReady).IsFalse();
    }

    [Test]
    public async Task V3_SupportedFeatures_Tag0_ParsedCorrectly()
    {
        var data = BuildApiVersionsV3Response(writer =>
        {
            // 1 tagged field
            writer.WriteUnsignedVarInt(1);
            // Tag 0 = SupportedFeatures
            writer.WriteUnsignedVarInt(0);

            // Build the SupportedFeatures array content in a temp buffer to know the size
            var innerBuffer = new ArrayBufferWriter<byte>();
            var innerWriter = new KafkaProtocolWriter(innerBuffer);
            // CompactArray with 1 element (length+1=2)
            innerWriter.WriteUnsignedVarInt(2);
            // Feature: name="metadata.version", min=1, max=20
            innerWriter.WriteCompactString("metadata.version");
            innerWriter.WriteInt16(1);
            innerWriter.WriteInt16(20);
            innerWriter.WriteEmptyTaggedFields();

            writer.WriteUnsignedVarInt(innerBuffer.WrittenCount);
            writer.WriteRawBytes(innerBuffer.WrittenSpan);
        });

        var reader = new KafkaProtocolReader(data);
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 3);

        await Assert.That(response.SupportedFeatures).IsNotNull();
        await Assert.That(response.SupportedFeatures!).Count().IsEqualTo(1);
        await Assert.That(response.SupportedFeatures![0].Name).IsEqualTo("metadata.version");
        await Assert.That(response.SupportedFeatures![0].MinVersion).IsEqualTo((short)1);
        await Assert.That(response.SupportedFeatures![0].MaxVersion).IsEqualTo((short)20);
    }

    [Test]
    public async Task V3_FinalizedFeaturesEpoch_Tag1_ParsedCorrectly()
    {
        var data = BuildApiVersionsV3Response(writer =>
        {
            writer.WriteUnsignedVarInt(1); // 1 tagged field
            writer.WriteUnsignedVarInt(1); // Tag 1 = FinalizedFeaturesEpoch
            writer.WriteUnsignedVarInt(8); // Size = 8 bytes (INT64)
            writer.WriteInt64(42);
        });

        var reader = new KafkaProtocolReader(data);
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 3);

        await Assert.That(response.FinalizedFeaturesEpoch).IsEqualTo(42L);
    }

    [Test]
    public async Task V3_ZkMigrationReady_Tag3_ParsedCorrectly()
    {
        var data = BuildApiVersionsV3Response(writer =>
        {
            writer.WriteUnsignedVarInt(1); // 1 tagged field
            writer.WriteUnsignedVarInt(3); // Tag 3 = ZkMigrationReady
            writer.WriteUnsignedVarInt(1); // Size = 1 byte
            writer.WriteUInt8(1); // true
        });

        var reader = new KafkaProtocolReader(data);
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 3);

        await Assert.That(response.ZkMigrationReady).IsTrue();
    }

    [Test]
    public async Task V3_ZkMigrationReady_False()
    {
        var data = BuildApiVersionsV3Response(writer =>
        {
            writer.WriteUnsignedVarInt(1); // 1 tagged field
            writer.WriteUnsignedVarInt(3); // Tag 3 = ZkMigrationReady
            writer.WriteUnsignedVarInt(1); // Size = 1 byte
            writer.WriteUInt8(0); // false
        });

        var reader = new KafkaProtocolReader(data);
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 3);

        await Assert.That(response.ZkMigrationReady).IsFalse();
    }

    [Test]
    public async Task V3_UnknownTag_SkippedCorrectly()
    {
        var data = BuildApiVersionsV3Response(writer =>
        {
            writer.WriteUnsignedVarInt(1); // 1 tagged field
            writer.WriteUnsignedVarInt(99); // Unknown tag
            writer.WriteUnsignedVarInt(3); // Size = 3 bytes
            writer.WriteRawBytes(new byte[] { 0xAA, 0xBB, 0xCC }); // Unknown data
        });

        var reader = new KafkaProtocolReader(data);
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 3);

        // Should parse without error, ignoring unknown tag
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.SupportedFeatures).IsNull();
    }

    [Test]
    public async Task V3_AllTags_ParsedCorrectly()
    {
        var data = BuildApiVersionsV3Response(writer =>
        {
            // 4 tagged fields
            writer.WriteUnsignedVarInt(4);

            // Tag 0: SupportedFeatures (empty array)
            writer.WriteUnsignedVarInt(0);
            var emptyArray = new ArrayBufferWriter<byte>();
            var emptyWriter = new KafkaProtocolWriter(emptyArray);
            emptyWriter.WriteUnsignedVarInt(1); // length+1=1 (0 elements)
            writer.WriteUnsignedVarInt(emptyArray.WrittenCount);
            writer.WriteRawBytes(emptyArray.WrittenSpan);

            // Tag 1: FinalizedFeaturesEpoch
            writer.WriteUnsignedVarInt(1);
            writer.WriteUnsignedVarInt(8);
            writer.WriteInt64(100);

            // Tag 2: FinalizedFeatures (empty array)
            writer.WriteUnsignedVarInt(2);
            var emptyArray2 = new ArrayBufferWriter<byte>();
            var emptyWriter2 = new KafkaProtocolWriter(emptyArray2);
            emptyWriter2.WriteUnsignedVarInt(1); // length+1=1 (0 elements)
            writer.WriteUnsignedVarInt(emptyArray2.WrittenCount);
            writer.WriteRawBytes(emptyArray2.WrittenSpan);

            // Tag 3: ZkMigrationReady
            writer.WriteUnsignedVarInt(3);
            writer.WriteUnsignedVarInt(1);
            writer.WriteUInt8(1);
        });

        var reader = new KafkaProtocolReader(data);
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 3);

        await Assert.That(response.SupportedFeatures).IsNotNull();
        await Assert.That(response.SupportedFeatures!).Count().IsEqualTo(0);
        await Assert.That(response.FinalizedFeaturesEpoch).IsEqualTo(100L);
        await Assert.That(response.FinalizedFeatures).IsNotNull();
        await Assert.That(response.FinalizedFeatures!).Count().IsEqualTo(0);
        await Assert.That(response.ZkMigrationReady).IsTrue();
    }

    [Test]
    public async Task V3_PartialTags_OnlySomePresent()
    {
        var data = BuildApiVersionsV3Response(writer =>
        {
            // Only tag 1 and tag 3
            writer.WriteUnsignedVarInt(2);

            // Tag 1: FinalizedFeaturesEpoch
            writer.WriteUnsignedVarInt(1);
            writer.WriteUnsignedVarInt(8);
            writer.WriteInt64(55);

            // Tag 3: ZkMigrationReady
            writer.WriteUnsignedVarInt(3);
            writer.WriteUnsignedVarInt(1);
            writer.WriteUInt8(0);
        });

        var reader = new KafkaProtocolReader(data);
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 3);

        await Assert.That(response.SupportedFeatures).IsNull();
        await Assert.That(response.FinalizedFeaturesEpoch).IsEqualTo(55L);
        await Assert.That(response.FinalizedFeatures).IsNull();
        await Assert.That(response.ZkMigrationReady).IsFalse();
    }

    [Test]
    public async Task V0_NoTaggedFields()
    {
        // v0 has no tagged fields at all
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        // ErrorCode
        writer.WriteInt16(0);
        // ApiKeys array (empty)
        writer.WriteInt32(0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (ApiVersionsResponse)ApiVersionsResponse.Read(ref reader, version: 0);

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.SupportedFeatures).IsNull();
        await Assert.That(response.FinalizedFeaturesEpoch).IsEqualTo(-1L);
    }
}
