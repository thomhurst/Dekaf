namespace Dekaf.Protocol.Messages;

/// <summary>
/// UpdateFeatures request (API key 57).
/// Updates the cluster's finalized feature levels.
/// </summary>
public sealed class UpdateFeaturesRequest : IKafkaRequest<UpdateFeaturesResponse>
{
    public static ApiKey ApiKey => ApiKey.UpdateFeatures;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    public int TimeoutMs { get; init; } = 60000;
    public required IReadOnlyList<UpdateFeatureData> FeatureUpdates { get; init; }
    public bool ValidateOnly { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(TimeoutMs);
        writer.WriteCompactArray(
            FeatureUpdates,
            static (ref KafkaProtocolWriter w, UpdateFeatureData update, short v) => update.Write(ref w, v),
            version);

        if (version >= 1)
            writer.WriteBoolean(ValidateOnly);

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// One feature-level update.
/// </summary>
public sealed class UpdateFeatureData
{
    public required string Feature { get; init; }
    public short MaxVersionLevel { get; init; }
    public FeatureUpdateType UpgradeType { get; init; } = FeatureUpdateType.Upgrade;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(Feature);
        writer.WriteInt16(MaxVersionLevel);

        if (version == 0)
            writer.WriteBoolean(UpgradeType is not FeatureUpdateType.Upgrade);
        else
            writer.WriteInt8((sbyte)UpgradeType);

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// KIP-584 feature update operation type.
/// </summary>
public enum FeatureUpdateType : sbyte
{
    Upgrade = 1,
    SafeDowngrade = 2,
    UnsafeDowngrade = 3
}
