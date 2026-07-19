namespace Dekaf.Protocol.Messages;

/// <summary>
/// UpdateFeatures response (API key 57).
/// </summary>
public sealed class UpdateFeaturesResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.UpdateFeatures;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    public int ThrottleTimeMs { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public required IReadOnlyList<UpdateFeatureResult> Results { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();
        var results = version <= 1
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r) => UpdateFeatureResult.Read(ref r)) ?? []
            : [];

        reader.SkipTaggedFields();
        return new UpdateFeaturesResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Results = results
        };
    }
}

/// <summary>
/// Per-feature result returned by UpdateFeatures v0-v1.
/// </summary>
public sealed class UpdateFeatureResult
{
    public required string Feature { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static UpdateFeatureResult Read(ref KafkaProtocolReader reader)
    {
        var result = new UpdateFeatureResult
        {
            Feature = reader.ReadCompactString() ?? string.Empty,
            ErrorCode = (ErrorCode)reader.ReadInt16(),
            ErrorMessage = reader.ReadCompactString()
        };
        reader.SkipTaggedFields();
        return result;
    }
}
