using Dekaf.Protocol.Messages;

namespace Dekaf.Metadata;

internal enum FinalizedFeatureStatus
{
    Unavailable,
    Absent,
    Present
}

/// <summary>
/// Immutable epoch-qualified cluster finalized-feature state.
/// </summary>
internal sealed class FinalizedFeatureSnapshot
{
    private readonly Dictionary<string, FeatureLevel> _features;

    private FinalizedFeatureSnapshot(long epoch, Dictionary<string, FeatureLevel> features)
    {
        Epoch = epoch;
        _features = features;
    }

    public long Epoch { get; }

    public static FinalizedFeatureSnapshot? Create(
        long epoch,
        IReadOnlyList<FinalizedFeature>? features)
    {
        if (epoch < 0)
            return null;

        var featureMap = new Dictionary<string, FeatureLevel>(
            features?.Count ?? 0,
            StringComparer.Ordinal);
        if (features is not null)
        {
            foreach (var feature in features)
            {
                var level = new FeatureLevel(feature.MinVersionLevel, feature.MaxVersionLevel);
                if (!featureMap.TryAdd(feature.Name, level)
                    && featureMap[feature.Name] != level)
                {
                    throw new KafkaException(
                        Protocol.ErrorCode.FeatureUpdateFailed,
                        $"Finalized feature '{feature.Name}' appeared more than once with conflicting levels");
                }
            }
        }

        return new FinalizedFeatureSnapshot(epoch, featureMap);
    }

    public FinalizedFeatureStatus GetFeatureStatus(string featureName, out short maxVersionLevel)
    {
        if (_features.TryGetValue(featureName, out var level))
        {
            maxVersionLevel = level.MaxVersionLevel;
            return FinalizedFeatureStatus.Present;
        }

        maxVersionLevel = default;
        return FinalizedFeatureStatus.Absent;
    }

    public bool HasSameContent(FinalizedFeatureSnapshot other)
    {
        if (_features.Count != other._features.Count)
            return false;

        foreach (var feature in _features)
        {
            if (!other._features.TryGetValue(feature.Key, out var level)
                || level != feature.Value)
            {
                return false;
            }
        }

        return true;
    }

    public IReadOnlyDictionary<string, (short MinVersion, short MaxVersion)> CopyFeatures()
    {
        var copy = new Dictionary<string, (short MinVersion, short MaxVersion)>(
            _features.Count,
            StringComparer.Ordinal);
        foreach (var feature in _features)
            copy.Add(feature.Key, (feature.Value.MinVersionLevel, feature.Value.MaxVersionLevel));

        return copy;
    }

    private readonly record struct FeatureLevel(short MinVersionLevel, short MaxVersionLevel);
}
