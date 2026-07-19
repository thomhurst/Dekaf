using System.Runtime.CompilerServices;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Networking;

internal interface IKafkaCapabilityProvider
{
    KafkaConnectionCapabilities Capabilities { get; }
}

/// <summary>
/// Immutable API and feature snapshot owned by one physical Kafka connection generation.
/// </summary>
internal sealed class KafkaConnectionCapabilities
{
    private const int ApiKeyCount = (int)ApiKey.DeleteShareGroupOffsets + 1;
    private const int MissingRange = -1;

    private readonly int[] _apiRanges;
    private readonly SupportedFeature[] _supportedFeatures;
    private readonly FinalizedFeature[] _finalizedFeatures;

    private KafkaConnectionCapabilities(
        int[] apiRanges,
        SupportedFeature[] supportedFeatures,
        long finalizedFeaturesEpoch,
        FinalizedFeature[] finalizedFeatures,
        bool zkMigrationReady)
    {
        _apiRanges = apiRanges;
        _supportedFeatures = supportedFeatures;
        FinalizedFeaturesEpoch = finalizedFeaturesEpoch;
        _finalizedFeatures = finalizedFeatures;
        ZkMigrationReady = zkMigrationReady;
    }

    public long FinalizedFeaturesEpoch { get; }
    public bool ZkMigrationReady { get; }

    public static KafkaConnectionCapabilities Create(ApiVersionsResponse response)
    {
        if (response.ErrorCode != ErrorCode.None)
            throw new InvalidOperationException($"ApiVersions failed: {response.ErrorCode}");

        var ranges = new int[ApiKeyCount];
        Array.Fill(ranges, MissingRange);

        foreach (var version in response.ApiKeys)
        {
            var key = (int)version.ApiKey;
            if ((uint)key >= (uint)ranges.Length || version.MinVersion > version.MaxVersion)
                continue;

            ranges[key] = Pack(version.MinVersion, version.MaxVersion);
        }

        var supportedFeatures = CopyFeatures(response.SupportedFeatures);
        var finalizedFeatures = CopyFeatures(response.FinalizedFeatures);

        return new KafkaConnectionCapabilities(
            ranges,
            supportedFeatures,
            response.FinalizedFeaturesEpoch,
            finalizedFeatures,
            response.ZkMigrationReady);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasApi(ApiKey apiKey)
    {
        var key = (int)apiKey;
        return (uint)key < (uint)_apiRanges.Length && _apiRanges[key] != MissingRange;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SupportsVersion(ApiKey apiKey, short version)
    {
        var key = (int)apiKey;
        if ((uint)key >= (uint)_apiRanges.Length)
            return false;

        var range = _apiRanges[key];
        return range != MissingRange && version >= UnpackMin(range) && version <= UnpackMax(range);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short NegotiateVersion(ApiKey apiKey, short clientMinVersion, short clientMaxVersion)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(clientMinVersion, clientMaxVersion);

        var key = (int)apiKey;
        if ((uint)key >= (uint)_apiRanges.Length || _apiRanges[key] == MissingRange)
            ApiVersionNegotiator.ThrowApiAbsent(apiKey, clientMinVersion, clientMaxVersion);

        var range = _apiRanges[key];
        var brokerMinVersion = UnpackMin(range);
        var brokerMaxVersion = UnpackMax(range);
        if (!ApiVersionNegotiator.TryNegotiate(
                brokerMinVersion,
                brokerMaxVersion,
                clientMinVersion,
                clientMaxVersion,
                out var negotiatedVersion))
        {
            ApiVersionNegotiator.ThrowDisjointRange(
                apiKey,
                brokerMinVersion,
                brokerMaxVersion,
                clientMinVersion,
                clientMaxVersion);
        }

        return negotiatedVersion;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryNegotiateVersion(
        ApiKey apiKey,
        short clientMinVersion,
        short clientMaxVersion,
        out short negotiatedVersion)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(clientMinVersion, clientMaxVersion);

        var key = (int)apiKey;
        if ((uint)key >= (uint)_apiRanges.Length || _apiRanges[key] == MissingRange)
        {
            negotiatedVersion = default;
            return false;
        }

        var range = _apiRanges[key];
        return ApiVersionNegotiator.TryNegotiate(
            UnpackMin(range),
            UnpackMax(range),
            clientMinVersion,
            clientMaxVersion,
            out negotiatedVersion);
    }

    public short GetFinalizedFeatureVersion(string featureName)
    {
        foreach (var feature in _finalizedFeatures)
        {
            if (string.Equals(feature.Name, featureName, StringComparison.Ordinal))
                return feature.MaxVersionLevel;
        }

        return 0;
    }

    public bool TryGetSupportedFeatureRange(
        string featureName,
        out short minVersion,
        out short maxVersion)
    {
        foreach (var feature in _supportedFeatures)
        {
            if (string.Equals(feature.Name, featureName, StringComparison.Ordinal))
            {
                minVersion = feature.MinVersion;
                maxVersion = feature.MaxVersion;
                return true;
            }
        }

        minVersion = default;
        maxVersion = default;
        return false;
    }

    public bool TryGetApiRange(
        ApiKey apiKey,
        out short minVersion,
        out short maxVersion)
    {
        var key = (int)apiKey;
        if ((uint)key >= (uint)_apiRanges.Length || _apiRanges[key] == MissingRange)
        {
            minVersion = default;
            maxVersion = default;
            return false;
        }

        var range = _apiRanges[key];
        minVersion = UnpackMin(range);
        maxVersion = UnpackMax(range);
        return true;
    }

    internal IReadOnlyList<FinalizedFeature> FinalizedFeatures => _finalizedFeatures;
    internal int ApiRangeCount => _apiRanges.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Pack(short minVersion, short maxVersion)
        // Kafka API versions are non-negative; -1 is reserved as MissingRange.
        => (minVersion << 16) | (ushort)maxVersion;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short UnpackMin(int range) => (short)(range >> 16);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short UnpackMax(int range) => (short)range;

    private static T[] CopyFeatures<T>(IReadOnlyList<T>? source)
    {
        if (source is null || source.Count == 0)
            return [];

        var copy = new T[source.Count];
        for (var i = 0; i < copy.Length; i++)
            copy[i] = source[i];

        return copy;
    }
}
