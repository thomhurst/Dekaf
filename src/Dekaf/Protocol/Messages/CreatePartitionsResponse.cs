namespace Dekaf.Protocol.Messages;

/// <summary>
/// CreatePartitions response (API key 37).
/// Contains the results of partition creation requests.
/// </summary>
public sealed class CreatePartitionsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.CreatePartitions;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 3;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation
    /// (zero if not throttled).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Results for each topic.
    /// </summary>
    public required IReadOnlyList<CreatePartitionsResponseResult> Results { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var throttleTimeMs = reader.ReadInt32();

        IReadOnlyList<CreatePartitionsResponseResult> results;
        if (isFlexible)
        {
            results = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => CreatePartitionsResponseResult.Read(ref r, version)) ?? [];
        }
        else
        {
            results = reader.ReadArray(
                (ref KafkaProtocolReader r) => CreatePartitionsResponseResult.Read(ref r, version)) ?? [];
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new CreatePartitionsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Results = results
        };
    }
}

/// <summary>
/// Per-topic result for CreatePartitions.
/// </summary>
public sealed class CreatePartitionsResponseResult
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static CreatePartitionsResponseResult Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var name = isFlexible
            ? reader.ReadCompactString() ?? string.Empty
            : reader.ReadString() ?? string.Empty;

        var errorCode = (ErrorCode)reader.ReadInt16();

        var errorMessage = isFlexible
            ? reader.ReadCompactString()
            : reader.ReadString();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new CreatePartitionsResponseResult
        {
            Name = name,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
