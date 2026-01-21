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
        var errorCode = (ErrorCode)reader.ReadInt16();

        var apiKeys = version >= 3
            ? reader.ReadCompactArray(ReadApiVersion)
            : reader.ReadArray(ReadApiVersion);

        var throttleTimeMs = version >= 1 ? reader.ReadInt32() : 0;

        IReadOnlyList<SupportedFeature>? supportedFeatures = null;
        var finalizedFeaturesEpoch = -1L;
        IReadOnlyList<FinalizedFeature>? finalizedFeatures = null;
        var zkMigrationReady = false;

        if (version >= 3)
        {
            supportedFeatures = reader.ReadCompactArray(ReadSupportedFeature);
            finalizedFeaturesEpoch = reader.ReadInt64();
            finalizedFeatures = reader.ReadCompactArray(ReadFinalizedFeature);

            if (version >= 3)
            {
                zkMigrationReady = reader.ReadBoolean();
            }

            reader.SkipTaggedFields();
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

    private static ApiVersion ReadApiVersion(ref KafkaProtocolReader reader)
    {
        var apiKey = (ApiKey)reader.ReadInt16();
        var minVersion = reader.ReadInt16();
        var maxVersion = reader.ReadInt16();
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
