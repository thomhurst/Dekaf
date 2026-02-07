namespace Dekaf.Protocol.Messages;

/// <summary>
/// AddPartitionsToTxn response (API key 24).
/// Contains the results of adding partitions to a transaction.
/// </summary>
public sealed class AddPartitionsToTxnResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.AddPartitionsToTxn;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 4;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The results for each topic.
    /// </summary>
    public required IReadOnlyList<AddPartitionsToTxnTopicResult> Results { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 3;

        var throttleTimeMs = reader.ReadInt32();

        var results = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => AddPartitionsToTxnTopicResult.Read(ref r, version))
            : reader.ReadArray((ref KafkaProtocolReader r) => AddPartitionsToTxnTopicResult.Read(ref r, version));

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new AddPartitionsToTxnResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Results = results
        };
    }
}

/// <summary>
/// Per-topic result in an AddPartitionsToTxn response.
/// </summary>
public sealed class AddPartitionsToTxnTopicResult
{
    public required string Name { get; init; }
    public required IReadOnlyList<AddPartitionsToTxnPartitionResult> Partitions { get; init; }

    public static AddPartitionsToTxnTopicResult Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 3;

        var name = isFlexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;

        var partitions = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => AddPartitionsToTxnPartitionResult.Read(ref r, version))
            : reader.ReadArray((ref KafkaProtocolReader r) => AddPartitionsToTxnPartitionResult.Read(ref r, version));

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new AddPartitionsToTxnTopicResult
        {
            Name = name,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Per-partition result in an AddPartitionsToTxn response.
/// </summary>
public sealed class AddPartitionsToTxnPartitionResult
{
    public required int PartitionIndex { get; init; }
    public required ErrorCode ErrorCode { get; init; }

    public static AddPartitionsToTxnPartitionResult Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 3;

        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new AddPartitionsToTxnPartitionResult
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode
        };
    }
}
