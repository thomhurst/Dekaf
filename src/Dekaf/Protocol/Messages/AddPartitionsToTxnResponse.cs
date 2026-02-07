namespace Dekaf.Protocol.Messages;

/// <summary>
/// AddPartitionsToTxn response (API key 24).
/// Contains the results of adding partitions to a transaction.
/// v0-v3: Flat format with topic results.
/// v4+: Wrapped in ResultsByTransaction array (supports batched responses).
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
    /// Top-level error code (v4+ only). Defaults to None.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The results for each topic.
    /// </summary>
    public required IReadOnlyList<AddPartitionsToTxnTopicResult> Results { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();

        if (version >= 4)
        {
            return ReadV4(ref reader, throttleTimeMs);
        }

        return ReadV0ToV3(ref reader, version, throttleTimeMs);
    }

    private static IKafkaResponse ReadV4(ref KafkaProtocolReader reader, int throttleTimeMs)
    {
        // v4+: ErrorCode (top-level, new in v4)
        var errorCode = (ErrorCode)reader.ReadInt16();

        // ResultsByTransaction compact array — unwrap the first (and typically only) transaction result
        var transactionCount = reader.ReadUnsignedVarInt() - 1;
        var results = new List<AddPartitionsToTxnTopicResult>();

        for (var t = 0; t < transactionCount; t++)
        {
            // TransactionalId (we skip it — we only send one transaction at a time)
            _ = reader.ReadCompactNonNullableString();

            // TopicResults compact array
            var topicCount = reader.ReadUnsignedVarInt() - 1;
            for (var i = 0; i < topicCount; i++)
            {
                results.Add(AddPartitionsToTxnTopicResult.Read(ref reader, 4));
            }

            // Transaction-level tagged fields
            reader.SkipTaggedFields();
        }

        // Response-level tagged fields
        reader.SkipTaggedFields();

        return new AddPartitionsToTxnResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            Results = results
        };
    }

    private static IKafkaResponse ReadV0ToV3(ref KafkaProtocolReader reader, short version, int throttleTimeMs)
    {
        var isFlexible = version >= 3;

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
