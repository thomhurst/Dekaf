namespace Dekaf.Protocol.Messages;

/// <summary>
/// ApiVersions response (API key 18).
/// Contains the API versions supported by the broker.
/// </summary>
public sealed class ApiVersionsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ApiVersions;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 3;

    public required ErrorCode ErrorCode { get; init; }
    public required IReadOnlyList<ApiVersion> ApiKeys { get; init; }
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Features supported by the broker (v3+).
    /// </summary>
    public IReadOnlyList<SupportedFeature>? SupportedFeatures { get; init; }

    /// <summary>
    /// The monotonically increasing epoch for finalized features (v3+).
    /// </summary>
    public long FinalizedFeaturesEpoch { get; init; } = -1;

    /// <summary>
    /// Finalized features (v3+).
    /// </summary>
    public IReadOnlyList<FinalizedFeature>? FinalizedFeatures { get; init; }

    /// <summary>
    /// Whether ZK migration is ready (v3+).
    /// </summary>
    public bool ZkMigrationReady { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 3;
        var errorCode = (ErrorCode)reader.ReadInt16();

        var apiKeys = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => ReadApiVersion(ref r, isFlexible))
            : reader.ReadArray((ref KafkaProtocolReader r) => ReadApiVersion(ref r, isFlexible));

        var throttleTimeMs = version >= 1 ? reader.ReadInt32() : 0;

        // In v3+, SupportedFeatures, FinalizedFeaturesEpoch, FinalizedFeatures, and ZkMigrationReady
        // are in the tagged fields section (tags 0-3), not as regular inline fields
        IReadOnlyList<SupportedFeature>? supportedFeatures = null;
        var finalizedFeaturesEpoch = -1L;
        IReadOnlyList<FinalizedFeature>? finalizedFeatures = null;
        var zkMigrationReady = false;

        if (isFlexible)
        {
            var numTaggedFields = reader.ReadUnsignedVarInt();
            for (var i = 0; i < numTaggedFields; i++)
            {
                var tag = reader.ReadUnsignedVarInt();
                var size = reader.ReadUnsignedVarInt();
                switch (tag)
                {
                    case 0:
                        supportedFeatures = reader.ReadCompactArray(
                            (ref KafkaProtocolReader r) => ReadSupportedFeature(ref r));
                        break;
                    case 1:
                        finalizedFeaturesEpoch = reader.ReadInt64();
                        break;
                    case 2:
                        finalizedFeatures = reader.ReadCompactArray(
                            (ref KafkaProtocolReader r) => ReadFinalizedFeature(ref r));
                        break;
                    case 3:
                        zkMigrationReady = reader.ReadUInt8() != 0;
                        break;
                    default:
                        reader.Skip(size);
                        break;
                }
            }
        }

        return new ApiVersionsResponse
        {
            ErrorCode = errorCode,
            ApiKeys = apiKeys,
            ThrottleTimeMs = throttleTimeMs,
            SupportedFeatures = supportedFeatures,
            FinalizedFeaturesEpoch = finalizedFeaturesEpoch,
            FinalizedFeatures = finalizedFeatures,
            ZkMigrationReady = zkMigrationReady
        };
    }

    private static ApiVersion ReadApiVersion(ref KafkaProtocolReader reader, bool isFlexible)
    {
        var apiKey = (ApiKey)reader.ReadInt16();
        var minVersion = reader.ReadInt16();
        var maxVersion = reader.ReadInt16();

        // In flexible versions, every struct ends with tagged fields (even if empty)
        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new ApiVersion(apiKey, minVersion, maxVersion);
    }

    private static SupportedFeature ReadSupportedFeature(ref KafkaProtocolReader reader)
    {
        var name = reader.ReadCompactNonNullableString();
        var minVersion = reader.ReadInt16();
        var maxVersion = reader.ReadInt16();
        reader.SkipTaggedFields();
        return new SupportedFeature(name, minVersion, maxVersion);
    }

    private static FinalizedFeature ReadFinalizedFeature(ref KafkaProtocolReader reader)
    {
        var name = reader.ReadCompactNonNullableString();
        var maxVersionLevel = reader.ReadInt16();
        var minVersionLevel = reader.ReadInt16();
        reader.SkipTaggedFields();
        return new FinalizedFeature(name, maxVersionLevel, minVersionLevel);
    }
}

/// <summary>
/// Represents an API version range supported by the broker.
/// </summary>
public readonly record struct ApiVersion(ApiKey ApiKey, short MinVersion, short MaxVersion);

/// <summary>
/// Represents a feature supported by the broker.
/// </summary>
public readonly record struct SupportedFeature(string Name, short MinVersion, short MaxVersion);

/// <summary>
/// Represents a finalized feature.
/// </summary>
public readonly record struct FinalizedFeature(string Name, short MaxVersionLevel, short MinVersionLevel);
