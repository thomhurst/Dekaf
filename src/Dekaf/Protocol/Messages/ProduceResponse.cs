namespace Dekaf.Protocol.Messages;

/// <summary>
/// Produce response (API key 0).
/// Contains acknowledgment for produced records.
/// </summary>
public sealed class ProduceResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.Produce;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 11;

    /// <summary>
    /// Response for each topic.
    /// </summary>
    public required IReadOnlyList<ProduceResponseTopicData> Responses { get; init; }

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 9;

        var responses = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => ProduceResponseTopicData.Read(ref r, version))
            : reader.ReadArray((ref KafkaProtocolReader r) => ProduceResponseTopicData.Read(ref r, version));

        var throttleTimeMs = version >= 1 ? reader.ReadInt32() : 0;

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new ProduceResponse
        {
            Responses = responses,
            ThrottleTimeMs = throttleTimeMs
        };
    }
}

/// <summary>
/// Topic response in a produce response.
/// </summary>
public sealed class ProduceResponseTopicData
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Partition responses.
    /// </summary>
    public required IReadOnlyList<ProduceResponsePartitionData> PartitionResponses { get; init; }

    public static ProduceResponseTopicData Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 9;

        var name = isFlexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;

        var partitionResponses = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => ProduceResponsePartitionData.Read(ref r, version))
            : reader.ReadArray((ref KafkaProtocolReader r) => ProduceResponsePartitionData.Read(ref r, version));

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new ProduceResponseTopicData
        {
            Name = name,
            PartitionResponses = partitionResponses
        };
    }
}

/// <summary>
/// Partition response in a produce response.
/// </summary>
public sealed class ProduceResponsePartitionData
{
    /// <summary>
    /// Partition index.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Error code.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Base offset assigned by the broker.
    /// </summary>
    public required long BaseOffset { get; init; }

    /// <summary>
    /// Log append time (if using log append time).
    /// </summary>
    public long LogAppendTimeMs { get; init; } = -1;

    /// <summary>
    /// Log start offset.
    /// </summary>
    public long LogStartOffset { get; init; } = -1;

    /// <summary>
    /// Record-level errors (v8+).
    /// </summary>
    public IReadOnlyList<BatchIndexAndErrorMessage>? RecordErrors { get; init; }

    /// <summary>
    /// Error message for the entire batch.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static ProduceResponsePartitionData Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 9;

        var index = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var baseOffset = reader.ReadInt64();

        var logAppendTimeMs = version >= 2 ? reader.ReadInt64() : -1;
        var logStartOffset = version >= 5 ? reader.ReadInt64() : -1;

        IReadOnlyList<BatchIndexAndErrorMessage>? recordErrors = null;
        string? errorMessage = null;

        if (version >= 8)
        {
            recordErrors = isFlexible
                ? reader.ReadCompactArray((ref KafkaProtocolReader r) => BatchIndexAndErrorMessage.Read(ref r, version))
                : reader.ReadArray((ref KafkaProtocolReader r) => BatchIndexAndErrorMessage.Read(ref r, version));

            errorMessage = isFlexible ? reader.ReadCompactString() : reader.ReadString();
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new ProduceResponsePartitionData
        {
            Index = index,
            ErrorCode = errorCode,
            BaseOffset = baseOffset,
            LogAppendTimeMs = logAppendTimeMs,
            LogStartOffset = logStartOffset,
            RecordErrors = recordErrors,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Record-level error in a produce response.
/// </summary>
public sealed class BatchIndexAndErrorMessage
{
    /// <summary>
    /// Index of the record in the batch.
    /// </summary>
    public required int BatchIndex { get; init; }

    /// <summary>
    /// Error message for this record.
    /// </summary>
    public string? BatchIndexErrorMessage { get; init; }

    public static BatchIndexAndErrorMessage Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 9;

        var batchIndex = reader.ReadInt32();
        var errorMessage = isFlexible ? reader.ReadCompactString() : reader.ReadString();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new BatchIndexAndErrorMessage
        {
            BatchIndex = batchIndex,
            BatchIndexErrorMessage = errorMessage
        };
    }
}
