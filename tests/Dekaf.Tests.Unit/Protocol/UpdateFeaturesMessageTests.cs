using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class UpdateFeaturesMessageTests
{
    [Test]
    [Arguments((short)0, FeatureUpdateType.SafeDowngrade, true)]
    [Arguments((short)0, FeatureUpdateType.Upgrade, false)]
    public async Task RequestV0_MapsUpgradeTypeToAllowDowngrade(
        short version,
        FeatureUpdateType upgradeType,
        bool expectedAllowDowngrade)
    {
        var reader = new KafkaProtocolReader(EncodeRequest(version, upgradeType, validateOnly: true));
        var timeoutMs = reader.ReadInt32();
        var arrayLength = reader.ReadUnsignedVarInt();
        var feature = reader.ReadCompactString();
        var maxVersionLevel = reader.ReadInt16();
        var allowDowngrade = reader.ReadBoolean();
        reader.SkipTaggedFields();
        reader.SkipTaggedFields();
        var end = reader.End;

        await Assert.That(timeoutMs).IsEqualTo(1234);
        await Assert.That(arrayLength).IsEqualTo(2);
        await Assert.That(feature).IsEqualTo("metadata.version");
        await Assert.That(maxVersionLevel).IsEqualTo((short)17);
        await Assert.That(allowDowngrade).IsEqualTo(expectedAllowDowngrade);
        await Assert.That(end).IsTrue();
    }

    [Test]
    [Arguments((short)1)]
    [Arguments((short)2)]
    public async Task RequestV1Plus_WritesUpgradeTypeAndValidateOnly(short version)
    {
        var reader = new KafkaProtocolReader(
            EncodeRequest(version, FeatureUpdateType.UnsafeDowngrade, validateOnly: true));

        _ = reader.ReadInt32();
        _ = reader.ReadUnsignedVarInt();
        _ = reader.ReadCompactString();
        _ = reader.ReadInt16();
        var upgradeType = reader.ReadInt8();
        reader.SkipTaggedFields();
        var validateOnly = reader.ReadBoolean();
        reader.SkipTaggedFields();
        var end = reader.End;

        await Assert.That(upgradeType).IsEqualTo((sbyte)FeatureUpdateType.UnsafeDowngrade);
        await Assert.That(validateOnly).IsTrue();
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task ResponseV1_PreservesPerFeatureError()
    {
        var reader = new KafkaProtocolReader(EncodeResponse(version: 1));
        var response = (UpdateFeaturesResponse)UpdateFeaturesResponse.Read(ref reader, version: 1);
        var end = reader.End;

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.Results).HasSingleItem();
        await Assert.That(response.Results[0].Feature).IsEqualTo("metadata.version");
        await Assert.That(response.Results[0].ErrorCode).IsEqualTo(ErrorCode.FeatureUpdateFailed);
        await Assert.That(response.Results[0].ErrorMessage).IsEqualTo("unsupported level");
        await Assert.That(end).IsTrue();
    }

    [Test]
    public async Task ResponseV2_OmitsPerFeatureResults()
    {
        var reader = new KafkaProtocolReader(EncodeResponse(version: 2));
        var response = (UpdateFeaturesResponse)UpdateFeaturesResponse.Read(ref reader, version: 2);
        var end = reader.End;

        await Assert.That(response.Results).IsEmpty();
        await Assert.That(end).IsTrue();
    }

    private static byte[] EncodeRequest(
        short version,
        FeatureUpdateType upgradeType,
        bool validateOnly)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        new UpdateFeaturesRequest
        {
            TimeoutMs = 1234,
            ValidateOnly = validateOnly,
            FeatureUpdates =
            [
                new UpdateFeatureData
                {
                    Feature = "metadata.version",
                    MaxVersionLevel = 17,
                    UpgradeType = upgradeType
                }
            ]
        }.Write(ref writer, version);
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] EncodeResponse(short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(25);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactString(null);
        if (version <= 1)
        {
            writer.WriteUnsignedVarInt(2);
            writer.WriteCompactString("metadata.version");
            writer.WriteInt16((short)ErrorCode.FeatureUpdateFailed);
            writer.WriteCompactString("unsupported level");
            writer.WriteEmptyTaggedFields();
        }

        writer.WriteEmptyTaggedFields();
        return buffer.WrittenSpan.ToArray();
    }
}
