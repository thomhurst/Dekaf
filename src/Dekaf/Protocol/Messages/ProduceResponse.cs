namespace Dekaf.Protocol.Messages;

/// <summary>
/// Produce response (API key 0).
/// Contains acknowledgment for produced records.
/// Pooled to eliminate per-response heap allocation — high batch churn rates (e.g., 25K batches/sec
/// with 16 KB batch size) create mid-lived ProduceResponse objects that survive Gen0 and get
/// promoted to Gen2 before dying, contributing to pathological GC pressure.
/// </summary>
public sealed class ProduceResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.Produce;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 11;

    private static ProduceResponsePool s_pool = new(maxPoolSize: 64);
    private static readonly Lock s_ratchetLock = new();

    /// <summary>
    /// Response for each topic. Reusable array — <see cref="TopicCount"/> indicates valid elements.
    /// When created via <see cref="Read"/>, the array is recycled across pool returns.
    /// When created directly (e.g., tests), <see cref="TopicCount"/> equals the array length.
    /// </summary>
    public ProduceResponseTopicData[] Responses { get; internal set; } = [];

    /// <summary>
    /// Number of valid entries in <see cref="Responses"/>.
    /// When set via init (tests), auto-computed from <see cref="Responses"/> length.
    /// </summary>
    public int TopicCount { get; internal set; }

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; internal set; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 9;

        var topicCount = isFlexible
            ? reader.ReadUnsignedVarInt() - 1
            : reader.ReadInt32();

        var response = Volatile.Read(ref s_pool).Rent();

        if (topicCount > 0)
        {
            if (response.Responses.Length < topicCount)
                response.Responses = new ProduceResponseTopicData[topicCount];

            for (var i = 0; i < topicCount; i++)
                response.Responses[i].ReadInto(ref reader, version);
        }

        response.TopicCount = Math.Max(topicCount, 0);
        response.ThrottleTimeMs = version >= 1 ? reader.ReadInt32() : 0;

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return response;
    }

    /// <summary>
    /// Returns this response to the pool for reuse. Must be called after processing is complete.
    /// </summary>
    internal void Return() => Volatile.Read(ref s_pool).Return(this);

    /// <summary>
    /// Increases the response pool capacity if <paramref name="poolSize"/> exceeds the current size.
    /// Called during producer construction once the broker count is known.
    /// Uses a ratchet (monotonically increasing) — the old pool's items drain naturally.
    /// </summary>
    internal static void RatchetPoolSize(int poolSize)
    {
        if (poolSize <= Volatile.Read(ref s_pool).MaxPoolSize)
            return;

        lock (s_ratchetLock)
        {
            if (poolSize <= s_pool.MaxPoolSize)
                return;

            Volatile.Write(ref s_pool, new ProduceResponsePool(poolSize));
        }
    }

    private sealed class ProduceResponsePool(int maxPoolSize)
        : Producer.ObjectPool<ProduceResponse>(maxPoolSize)
    {
        protected override ProduceResponse Create() => new();

        protected override void Reset(ProduceResponse item)
        {
            item.TopicCount = 0;
            item.ThrottleTimeMs = 0;
        }
    }
}

/// <summary>
/// Topic response in a produce response.
/// Mutable struct to enable reuse of the partition array across responses — the same
/// ProduceResponseTopicData is recycled via the owning ProduceResponse's pooled Responses array.
/// </summary>
public struct ProduceResponseTopicData
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public string Name { get; internal set; }

    /// <summary>
    /// Partition responses. Reusable array — <see cref="PartitionCount"/> indicates valid elements.
    /// </summary>
    public ProduceResponsePartitionData[] PartitionResponses { get; internal set; }

    /// <summary>
    /// Number of valid entries in <see cref="PartitionResponses"/>.
    /// </summary>
    public int PartitionCount { get; internal set; }

    /// <summary>
    /// Non-pooled path: allocates a fresh struct and array. Used by tests and direct construction.
    /// </summary>
    public static ProduceResponseTopicData Read(ref KafkaProtocolReader reader, short version)
    {
        var result = default(ProduceResponseTopicData);
        result.ReadInto(ref reader, version);
        return result;
    }

    /// <summary>
    /// Reads into this existing struct, reusing the PartitionResponses array if large enough.
    /// Called from <see cref="ProduceResponse.Read"/> to avoid per-response array allocations.
    /// Note: this mutates the struct in-place. When called via array indexer (e.g.,
    /// <c>response.Responses[i].ReadInto(...)</c>), the array element is modified directly
    /// because array element access yields a managed reference, not a copy.
    /// </summary>
    internal void ReadInto(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 9;

        Name = isFlexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;

        var partitionCount = isFlexible
            ? reader.ReadUnsignedVarInt() - 1
            : reader.ReadInt32();

        PartitionCount = Math.Max(partitionCount, 0);

        if (PartitionCount > 0)
        {
            if (PartitionResponses is null || PartitionResponses.Length < PartitionCount)
                PartitionResponses = new ProduceResponsePartitionData[PartitionCount];

            for (var i = 0; i < PartitionCount; i++)
                PartitionResponses[i] = ProduceResponsePartitionData.Read(ref reader, version);
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }
    }
}

/// <summary>
/// Partition response in a produce response.
/// Struct to eliminate per-partition heap allocation — these are short-lived parse results
/// consumed immediately by BrokerSender.ProcessResponseBatches and then discarded.
/// </summary>
public readonly struct ProduceResponsePartitionData
{
    public ProduceResponsePartitionData() { }

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
    public BatchIndexAndErrorMessage[]? RecordErrors { get; init; }

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

        BatchIndexAndErrorMessage[]? recordErrors = null;
        string? errorMessage = null;

        if (version >= 8)
        {
            recordErrors = isFlexible
                ? reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => BatchIndexAndErrorMessage.Read(ref r, v), version)
                : reader.ReadArray(static (ref KafkaProtocolReader r, short v) => BatchIndexAndErrorMessage.Read(ref r, v), version);

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
/// Struct to eliminate per-record-error heap allocation — parsed and discarded immediately.
/// </summary>
public readonly struct BatchIndexAndErrorMessage
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
