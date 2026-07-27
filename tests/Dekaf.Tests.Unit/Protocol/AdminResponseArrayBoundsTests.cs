using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class AdminResponseArrayBoundsTests
{
    private const int HostileElementCount = 40;
    private const int HostilePayloadLength = 60;
    private const int HostileOneByteElementCount = 1_000_001;

    [Test]
    [Arguments(ArrayTarget.AlterClientEntriesLegacy)]
    [Arguments(ArrayTarget.AlterClientEntriesFlexible)]
    [Arguments(ArrayTarget.AlterClientEntityLegacy)]
    [Arguments(ArrayTarget.AlterClientEntityFlexible)]
    [Arguments(ArrayTarget.AlterConfigsResources)]
    [Arguments(ArrayTarget.IncrementalAlterConfigsResources)]
    [Arguments(ArrayTarget.CreateAclResults)]
    [Arguments(ArrayTarget.DeleteAclFilterResults)]
    [Arguments(ArrayTarget.DeleteAclMatchingAcls)]
    [Arguments(ArrayTarget.DescribeAclResources)]
    [Arguments(ArrayTarget.DescribeAclEntries)]
    [Arguments(ArrayTarget.CreatePartitionsResults)]
    [Arguments(ArrayTarget.CreateTopicsV5)]
    [Arguments(ArrayTarget.CreateTopicsV7)]
    [Arguments(ArrayTarget.CreateTopicConfigs)]
    [Arguments(ArrayTarget.DeleteTopicsV4)]
    [Arguments(ArrayTarget.DeleteTopicsV5)]
    [Arguments(ArrayTarget.DeleteTopicsV6)]
    [Arguments(ArrayTarget.AlterScramResults)]
    [Arguments(ArrayTarget.DescribeScramResults)]
    [Arguments(ArrayTarget.DescribeScramCredentials)]
    [Arguments(ArrayTarget.DelegationTokensLegacy)]
    [Arguments(ArrayTarget.DelegationTokensFlexible)]
    [Arguments(ArrayTarget.DelegationRenewersLegacy)]
    [Arguments(ArrayTarget.DelegationRenewersFlexible)]
    [Arguments(ArrayTarget.SaslMechanisms)]
    [Arguments(ArrayTarget.QuotaEntriesLegacy)]
    [Arguments(ArrayTarget.QuotaEntriesFlexible)]
    [Arguments(ArrayTarget.QuotaEntityLegacy)]
    [Arguments(ArrayTarget.QuotaEntityFlexible)]
    [Arguments(ArrayTarget.QuotaValuesLegacy)]
    [Arguments(ArrayTarget.QuotaValuesFlexible)]
    [Arguments(ArrayTarget.ClusterNodesV0)]
    [Arguments(ArrayTarget.ClusterNodesV2)]
    [Arguments(ArrayTarget.DescribeConfigsResults)]
    [Arguments(ArrayTarget.DescribeConfigsEntries)]
    [Arguments(ArrayTarget.DescribeConfigsSynonyms)]
    [Arguments(ArrayTarget.TelemetryCompressionTypes)]
    [Arguments(ArrayTarget.TelemetryRequestedMetrics)]
    [Arguments(ArrayTarget.ClientMetricsResources)]
    [Arguments(ArrayTarget.ConfigResources)]
    [Arguments(ArrayTarget.UpdateFeatureResults)]
    public async Task Read_HostileArrayCount_RejectsBeforeAllocation(ArrayTarget target)
    {
        var payload = CreatePayload(target);

        await Assert.That(() => ReadContiguous(payload, target))
            .ThrowsExactly<MalformedProtocolDataException>();
        await Assert.That(() => ReadSegmented(payload, target))
            .ThrowsExactly<MalformedProtocolDataException>();
    }

    [Test]
    public async Task DescribeDelegationTokenResponse_Read_V2MinimumToken_AcceptsExactWireSize()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteUnsignedVarInt(2);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactString(string.Empty);
        writer.WriteInt64(0);
        writer.WriteInt64(0);
        writer.WriteInt64(0);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactBytes([]);
        writer.WriteUnsignedVarInt(1);
        writer.WriteEmptyTaggedFields();
        writer.WriteInt32(0);
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (DescribeDelegationTokenResponse)DescribeDelegationTokenResponse.Read(
            ref reader,
            version: 2);
        var remaining = reader.Remaining;

        await Assert.That(response.Tokens).Count().IsEqualTo(1);
        await Assert.That(remaining).IsEqualTo(0);
    }

    [Test]
    public async Task DeleteAclsFilterResult_Read_MinimumMatchingAcl_AcceptsExactWireSize()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteUnsignedVarInt(2);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteInt8(0);
        writer.WriteCompactString(string.Empty);
        writer.WriteInt8(0);
        writer.WriteCompactString(string.Empty);
        writer.WriteCompactString(string.Empty);
        writer.WriteInt8(0);
        writer.WriteInt8(0);
        writer.WriteEmptyTaggedFields();
        writer.WriteEmptyTaggedFields();

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var result = DeleteAclsFilterResult.Read(ref reader, version: 3);
        var remaining = reader.Remaining;

        await Assert.That(result.MatchingAcls).Count().IsEqualTo(1);
        await Assert.That(remaining).IsEqualTo(0);
    }

    private static byte[] CreatePayload(ArrayTarget target)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        switch (target)
        {
            case ArrayTarget.AlterClientEntriesLegacy:
            case ArrayTarget.AlterClientEntriesFlexible:
            case ArrayTarget.AlterConfigsResources:
            case ArrayTarget.IncrementalAlterConfigsResources:
            case ArrayTarget.CreateAclResults:
            case ArrayTarget.DeleteAclFilterResults:
            case ArrayTarget.CreatePartitionsResults:
            case ArrayTarget.CreateTopicsV5:
            case ArrayTarget.CreateTopicsV7:
            case ArrayTarget.DeleteTopicsV4:
            case ArrayTarget.DeleteTopicsV5:
            case ArrayTarget.DeleteTopicsV6:
            case ArrayTarget.AlterScramResults:
            case ArrayTarget.DescribeConfigsResults:
                writer.WriteInt32(0);
                break;
            case ArrayTarget.AlterClientEntityLegacy:
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteString(null);
                break;
            case ArrayTarget.AlterClientEntityFlexible:
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                break;
            case ArrayTarget.DeleteAclMatchingAcls:
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                break;
            case ArrayTarget.DescribeAclResources:
                writer.WriteInt32(0);
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                break;
            case ArrayTarget.DescribeAclEntries:
                writer.WriteInt8(0);
                writer.WriteCompactString(string.Empty);
                writer.WriteInt8(0);
                break;
            case ArrayTarget.CreateTopicConfigs:
                writer.WriteCompactString(string.Empty);
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                writer.WriteInt32(0);
                writer.WriteInt16(0);
                break;
            case ArrayTarget.DescribeScramCredentials:
                writer.WriteCompactString(string.Empty);
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                break;
            case ArrayTarget.DescribeScramResults:
                writer.WriteInt32(0);
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                break;
            case ArrayTarget.DelegationTokensLegacy:
            case ArrayTarget.DelegationTokensFlexible:
                writer.WriteInt16((short)ErrorCode.None);
                break;
            case ArrayTarget.DelegationRenewersLegacy:
                WriteDelegationTokenPreamble(ref writer, flexible: false);
                break;
            case ArrayTarget.DelegationRenewersFlexible:
                WriteDelegationTokenPreamble(ref writer, flexible: true);
                break;
            case ArrayTarget.SaslMechanisms:
                writer.WriteInt16((short)ErrorCode.None);
                break;
            case ArrayTarget.QuotaEntriesLegacy:
                writer.WriteInt32(0);
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteString(null);
                break;
            case ArrayTarget.QuotaEntriesFlexible:
                writer.WriteInt32(0);
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                break;
            case ArrayTarget.QuotaValuesLegacy:
                writer.WriteInt32(0);
                break;
            case ArrayTarget.QuotaValuesFlexible:
                writer.WriteUnsignedVarInt(1);
                break;
            case ArrayTarget.ClusterNodesV0:
            case ArrayTarget.ClusterNodesV2:
                writer.WriteInt32(0);
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                if (target is ArrayTarget.ClusterNodesV2)
                    writer.WriteInt8(0);
                writer.WriteCompactString(string.Empty);
                writer.WriteInt32(0);
                break;
            case ArrayTarget.DescribeConfigsEntries:
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                writer.WriteInt8(0);
                writer.WriteCompactString(string.Empty);
                break;
            case ArrayTarget.DescribeConfigsSynonyms:
                writer.WriteCompactString(string.Empty);
                writer.WriteCompactNullableString(null);
                writer.WriteBoolean(false);
                writer.WriteInt8(0);
                writer.WriteBoolean(false);
                break;
            case ArrayTarget.TelemetryCompressionTypes:
            case ArrayTarget.TelemetryRequestedMetrics:
                writer.WriteInt32(0);
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteUuid(Guid.Empty);
                writer.WriteInt32(0);
                if (target is ArrayTarget.TelemetryRequestedMetrics)
                {
                    writer.WriteUnsignedVarInt(1);
                    writer.WriteInt32(0);
                    writer.WriteInt32(0);
                    writer.WriteBoolean(false);
                }
                break;
            case ArrayTarget.ClientMetricsResources:
            case ArrayTarget.ConfigResources:
                writer.WriteInt32(0);
                writer.WriteInt16((short)ErrorCode.None);
                break;
            case ArrayTarget.UpdateFeatureResults:
                writer.WriteInt32(0);
                writer.WriteInt16((short)ErrorCode.None);
                writer.WriteCompactNullableString(null);
                break;
            case ArrayTarget.QuotaEntityLegacy:
            case ArrayTarget.QuotaEntityFlexible:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }

        WriteHostileArray(ref writer, target);
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteDelegationTokenPreamble(ref KafkaProtocolWriter writer, bool flexible)
    {
        WriteString(ref writer, string.Empty, flexible);
        WriteString(ref writer, string.Empty, flexible);
        if (flexible)
        {
            WriteString(ref writer, string.Empty, true);
            WriteString(ref writer, string.Empty, true);
        }
        writer.WriteInt64(0);
        writer.WriteInt64(0);
        writer.WriteInt64(0);
        WriteString(ref writer, string.Empty, flexible);
        if (flexible)
            writer.WriteCompactBytes([]);
        else
            writer.WriteBytes([]);
    }

    private static void WriteString(ref KafkaProtocolWriter writer, string value, bool flexible)
    {
        if (flexible)
            writer.WriteCompactString(value);
        else
            writer.WriteString(value);
    }

    private static void WriteHostileArray(ref KafkaProtocolWriter writer, ArrayTarget target)
    {
        var count = target is ArrayTarget.TelemetryCompressionTypes
            ? 257
            : target is ArrayTarget.TelemetryRequestedMetrics
                ? HostileOneByteElementCount
                : HostileElementCount;

        if (UsesLegacyEncoding(target))
            writer.WriteInt32(count);
        else
            writer.WriteUnsignedVarInt(count + 1);

        var payloadLength = target is ArrayTarget.TelemetryCompressionTypes
            or ArrayTarget.TelemetryRequestedMetrics
                ? count
                : HostilePayloadLength;
        writer.WriteRawBytes(new byte[payloadLength]);
    }

    private static bool UsesLegacyEncoding(ArrayTarget target) =>
        target is ArrayTarget.AlterClientEntriesLegacy
            or ArrayTarget.AlterClientEntityLegacy
            or ArrayTarget.DelegationTokensLegacy
            or ArrayTarget.DelegationRenewersLegacy
            or ArrayTarget.SaslMechanisms
            or ArrayTarget.QuotaEntriesLegacy
            or ArrayTarget.QuotaEntityLegacy
            or ArrayTarget.QuotaValuesLegacy;

    private static void ReadContiguous(byte[] payload, ArrayTarget target)
    {
        var reader = new KafkaProtocolReader(payload);
        ReadTarget(ref reader, target);
    }

    private static void ReadSegmented(byte[] payload, ArrayTarget target)
    {
        var sequence = SequenceTestHelpers.CreateMultiSegmentSequence(payload, payload.Length / 2);
        var reader = new KafkaProtocolReader(sequence);
        ReadTarget(ref reader, target);
    }

    private static void ReadTarget(ref KafkaProtocolReader reader, ArrayTarget target)
    {
        switch (target)
        {
            case ArrayTarget.AlterClientEntriesLegacy:
                _ = AlterClientQuotasResponse.Read(ref reader, version: 0);
                break;
            case ArrayTarget.AlterClientEntriesFlexible:
                _ = AlterClientQuotasResponse.Read(ref reader, version: 1);
                break;
            case ArrayTarget.AlterClientEntityLegacy:
                _ = AlterClientQuotasResponseEntry.Read(ref reader, version: 0);
                break;
            case ArrayTarget.AlterClientEntityFlexible:
                _ = AlterClientQuotasResponseEntry.Read(ref reader, version: 1);
                break;
            case ArrayTarget.AlterConfigsResources:
                _ = AlterConfigsResponse.Read(ref reader, version: 2);
                break;
            case ArrayTarget.IncrementalAlterConfigsResources:
                _ = IncrementalAlterConfigsResponse.Read(ref reader, version: 1);
                break;
            case ArrayTarget.CreateAclResults:
                _ = CreateAclsResponse.Read(ref reader, version: 3);
                break;
            case ArrayTarget.DeleteAclFilterResults:
                _ = DeleteAclsResponse.Read(ref reader, version: 3);
                break;
            case ArrayTarget.DeleteAclMatchingAcls:
                _ = DeleteAclsFilterResult.Read(ref reader, version: 3);
                break;
            case ArrayTarget.DescribeAclResources:
                _ = DescribeAclsResponse.Read(ref reader, version: 3);
                break;
            case ArrayTarget.DescribeAclEntries:
                _ = DescribeAclsResource.Read(ref reader, version: 3);
                break;
            case ArrayTarget.CreatePartitionsResults:
                _ = CreatePartitionsResponse.Read(ref reader, version: 3);
                break;
            case ArrayTarget.CreateTopicsV5:
                _ = CreateTopicsResponse.Read(ref reader, version: 5);
                break;
            case ArrayTarget.CreateTopicsV7:
                _ = CreateTopicsResponse.Read(ref reader, version: 7);
                break;
            case ArrayTarget.CreateTopicConfigs:
                _ = CreateTopicsResponseTopic.Read(ref reader, version: 5);
                break;
            case ArrayTarget.DeleteTopicsV4:
                _ = DeleteTopicsResponse.Read(ref reader, version: 4);
                break;
            case ArrayTarget.DeleteTopicsV5:
                _ = DeleteTopicsResponse.Read(ref reader, version: 5);
                break;
            case ArrayTarget.DeleteTopicsV6:
                _ = DeleteTopicsResponse.Read(ref reader, version: 6);
                break;
            case ArrayTarget.AlterScramResults:
                _ = AlterUserScramCredentialsResponse.Read(ref reader, version: 0);
                break;
            case ArrayTarget.DescribeScramResults:
                _ = DescribeUserScramCredentialsResponse.Read(ref reader, version: 0);
                break;
            case ArrayTarget.DescribeScramCredentials:
                _ = DescribeUserScramCredentialsResult.Read(ref reader, version: 0);
                break;
            case ArrayTarget.DelegationTokensLegacy:
                _ = DescribeDelegationTokenResponse.Read(ref reader, version: 1);
                break;
            case ArrayTarget.DelegationTokensFlexible:
                _ = DescribeDelegationTokenResponse.Read(ref reader, version: 3);
                break;
            case ArrayTarget.DelegationRenewersLegacy:
                _ = DescribedDelegationTokenData.Read(ref reader, version: 1);
                break;
            case ArrayTarget.DelegationRenewersFlexible:
                _ = DescribedDelegationTokenData.Read(ref reader, version: 3);
                break;
            case ArrayTarget.SaslMechanisms:
                _ = SaslHandshakeResponse.Read(ref reader, version: 1);
                break;
            case ArrayTarget.QuotaEntriesLegacy:
                _ = DescribeClientQuotasResponse.Read(ref reader, version: 0);
                break;
            case ArrayTarget.QuotaEntriesFlexible:
                _ = DescribeClientQuotasResponse.Read(ref reader, version: 1);
                break;
            case ArrayTarget.QuotaEntityLegacy:
            case ArrayTarget.QuotaValuesLegacy:
                _ = DescribeClientQuotasResponseEntry.Read(ref reader, version: 0);
                break;
            case ArrayTarget.QuotaEntityFlexible:
            case ArrayTarget.QuotaValuesFlexible:
                _ = DescribeClientQuotasResponseEntry.Read(ref reader, version: 1);
                break;
            case ArrayTarget.ClusterNodesV0:
                _ = DescribeClusterResponse.Read(ref reader, version: 0);
                break;
            case ArrayTarget.ClusterNodesV2:
                _ = DescribeClusterResponse.Read(ref reader, version: 2);
                break;
            case ArrayTarget.DescribeConfigsResults:
                _ = DescribeConfigsResponse.Read(ref reader, version: 4);
                break;
            case ArrayTarget.DescribeConfigsEntries:
                _ = DescribeConfigsResult.Read(ref reader, version: 4);
                break;
            case ArrayTarget.DescribeConfigsSynonyms:
                _ = DescribeConfigsResourceResult.Read(ref reader, version: 4);
                break;
            case ArrayTarget.TelemetryCompressionTypes:
            case ArrayTarget.TelemetryRequestedMetrics:
                _ = GetTelemetrySubscriptionsResponse.Read(ref reader, version: 0);
                break;
            case ArrayTarget.ClientMetricsResources:
                _ = ListClientMetricsResourcesResponse.Read(ref reader, version: 0);
                break;
            case ArrayTarget.ConfigResources:
                _ = ListConfigResourcesResponse.Read(ref reader, version: 0);
                break;
            case ArrayTarget.UpdateFeatureResults:
                _ = UpdateFeaturesResponse.Read(ref reader, version: 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }

    public enum ArrayTarget
    {
        AlterClientEntriesLegacy,
        AlterClientEntriesFlexible,
        AlterClientEntityLegacy,
        AlterClientEntityFlexible,
        AlterConfigsResources,
        IncrementalAlterConfigsResources,
        CreateAclResults,
        DeleteAclFilterResults,
        DeleteAclMatchingAcls,
        DescribeAclResources,
        DescribeAclEntries,
        CreatePartitionsResults,
        CreateTopicsV5,
        CreateTopicsV7,
        CreateTopicConfigs,
        DeleteTopicsV4,
        DeleteTopicsV5,
        DeleteTopicsV6,
        AlterScramResults,
        DescribeScramResults,
        DescribeScramCredentials,
        DelegationTokensLegacy,
        DelegationTokensFlexible,
        DelegationRenewersLegacy,
        DelegationRenewersFlexible,
        SaslMechanisms,
        QuotaEntriesLegacy,
        QuotaEntriesFlexible,
        QuotaEntityLegacy,
        QuotaEntityFlexible,
        QuotaValuesLegacy,
        QuotaValuesFlexible,
        ClusterNodesV0,
        ClusterNodesV2,
        DescribeConfigsResults,
        DescribeConfigsEntries,
        DescribeConfigsSynonyms,
        TelemetryCompressionTypes,
        TelemetryRequestedMetrics,
        ClientMetricsResources,
        ConfigResources,
        UpdateFeatureResults
    }
}
