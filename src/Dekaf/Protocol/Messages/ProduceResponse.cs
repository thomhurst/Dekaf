using Dekaf.Internal;

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
    public static short LowestSupportedVersion => 9;
    public static short HighestSupportedVersion => 13;

    private static ProduceResponsePool s_pool = new(maxPoolSize: 64);
    private static readonly Lock s_ratchetLock = new();

    // Pool hygiene bounds: legitimate produce responses stay far below these (topics per
    // request and partitions per topic in a single response), while a hostile frame can
    // inflate the reusable arrays toward the frame cap before parsing fails. Pooling such
    // an instance would pin that memory for the pool's lifetime.
    private const int MaxPooledTopicCapacity = 1024;
    private const int MaxPooledPartitionCapacity = 4096;

    // Absolute parse caps: a produce response echoes the topics/partitions of the produce
    // request that elicited it, so the client's own max request size bounds how many
    // entries a valid response can carry. The wire-minimum bound alone is insufficient
    // because the in-memory element can be an order of magnitude larger than its minimum
    // encoding (e.g. a 3-byte pre-v13 topic entry expands to a ~48-byte struct, letting a
    // 16 MiB frame demand hundreds of MiB before parsing fails). Defaults derive from the
    // 1 MiB default max.request.size; producers configured with larger requests ratchet
    // them up at construction via RatchetMaxEntryCaps. Node endpoints are cluster-bound,
    // not request-bound, so that cap stays fixed.
    private const int DefaultMaxRequestSize = 1024 * 1024;

    // Conservative minimum request-side encodings, deliberately below anything the
    // producer can actually emit so the derived caps always exceed a legitimate
    // response's counts: a v13 request topic entry carries a 16-byte topic id plus at
    // least one partition holding a 61-byte record batch header; a request partition
    // entry carries a 4-byte index plus that same batch header; a record is never
    // smaller than a few bytes.
    private const int MinRequestBytesPerTopic = 24;
    private const int MinRequestBytesPerPartition = 64;
    private const int MinRequestBytesPerRecord = 4;

    // Absolute ceilings for the ratchet, chosen independently of any single connection's
    // actual frame size (standalone producers read responses through the 16 MiB
    // ResponseBufferPool.Default frame; producers sharing a KafkaClient read through a
    // pool sized by the client's SharedResponseBufferFetchMaxBytes constant, a ~201 MiB
    // tier). The ceiling budget is what a 16 MiB frame can encode at each entry's minimum
    // legitimate response encoding — deliberately conservative so one producer's huge
    // MaxRequestSize cannot inflate the process-global cap into hundreds-of-MiB hostile
    // allocations (a pre-v13 topic entry encodes in 3 bytes but expands to a ~48-byte
    // struct). Deliberate tradeoff: a single produce response carrying more than ~466k
    // topics would be rejected even where a larger shared-pool frame could deliver it —
    // far beyond any practical workload — while the per-frame wire-minimum check in
    // ValidateReadableLength remains the proportional defense at every frame size.
    private const int RatchetCeilingBudgetBytes = 16 * 1024 * 1024;
    // A legitimate response topic entry carries a name/id plus at least one 33-byte
    // partition entry and two varints.
    internal const int MaxRatchetTopicCount = RatchetCeilingBudgetBytes / 36;
    internal const int MaxRatchetPartitionCount = RatchetCeilingBudgetBytes / 33;
    internal const int MaxRatchetRecordErrorCount = RatchetCeilingBudgetBytes / 6;

    private static int s_maxTopicCount = DefaultMaxRequestSize / MinRequestBytesPerTopic;
    private static int s_maxPartitionCount = DefaultMaxRequestSize / MinRequestBytesPerPartition;
    private static int s_maxRecordErrorCount = DefaultMaxRequestSize / MinRequestBytesPerRecord;

    internal const int MaxNodeEndpointCount = 10_000;

    internal static int MaxTopicCount => Volatile.Read(ref s_maxTopicCount);
    internal static int MaxPartitionCount => Volatile.Read(ref s_maxPartitionCount);
    internal static int MaxRecordErrorCount => Volatile.Read(ref s_maxRecordErrorCount);

    /// <summary>
    /// Raises the response parse caps for producers configured with a max request size
    /// larger than the default, clamped to conservative absolute ceilings. Monotonic
    /// — caps never shrink — mirroring <see cref="RatchetPoolSize"/>, since responses can
    /// only echo requests this client was configured to send.
    /// </summary>
    internal static void RatchetMaxEntryCaps(int maxRequestSize)
    {
        InterlockedHelper.RatchetUp(
            ref s_maxTopicCount,
            Math.Min(maxRequestSize / MinRequestBytesPerTopic, MaxRatchetTopicCount));
        InterlockedHelper.RatchetUp(
            ref s_maxPartitionCount,
            Math.Min(maxRequestSize / MinRequestBytesPerPartition, MaxRatchetPartitionCount));
        InterlockedHelper.RatchetUp(
            ref s_maxRecordErrorCount,
            Math.Min(maxRequestSize / MinRequestBytesPerRecord, MaxRatchetRecordErrorCount));
    }

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

    /// <summary>
    /// Endpoints for current leaders reported in KIP-951 response tagged fields.
    /// </summary>
    public NodeEndpoint[] NodeEndpoints { get; internal set; } = [];

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var topicCount = reader.ReadUnsignedVarInt() - 1;

        // Each topic entry needs at least a topic id (16 bytes, v13+) or a compact name
        // length prefix (1 byte), plus partition-count and tagged-fields varints. A count
        // that cannot fit in the remaining payload at that minimum size, or that exceeds
        // the absolute cap, is malformed and must fail before the array allocation.
        if (topicCount > 0)
            reader.ValidateReadableLength(topicCount, version >= ProduceRequest.TopicIdVersion ? 18 : 3, MaxTopicCount);

        var response = Volatile.Read(ref s_pool).Rent();
        var parsedTopics = 0;
        try
        {
            if (topicCount > 0)
            {
                if (response.Responses.Length < topicCount)
                    response.Responses = new ProduceResponseTopicData[topicCount];

                for (; parsedTopics < topicCount; parsedTopics++)
                    response.Responses[parsedTopics].ReadInto(ref reader, version);
            }

            response.TopicCount = Math.Max(topicCount, 0);
            response.ThrottleTimeMs = reader.ReadInt32();
            response.NodeEndpoints = ReadResponseTaggedFields(ref reader, version);

            return response;
        }
        catch
        {
            // Hostile or truncated frames must not defeat the response pool: the caller
            // never sees the instance on failure, so return it before propagating. Mark
            // the entries this parse touched (including the partially-read one) so
            // Return's pool-hygiene scan covers them.
            response.TopicCount = Math.Max(Math.Min(parsedTopics + 1, topicCount), 0);
            response.Return();
            throw;
        }
    }

    private static NodeEndpoint[] ReadResponseTaggedFields(ref KafkaProtocolReader reader, short version)
    {
        NodeEndpoint[]? nodeEndpoints = null;
        var numFields = reader.ReadUnsignedVarInt();
        for (var i = 0; i < numFields; i++)
        {
            var tag = reader.ReadUnsignedVarInt();
            var size = reader.ReadUnsignedVarInt();
            var start = reader.Consumed;

            if (tag == 0 && version >= 10)
            {
                // Each endpoint needs at least node id (4), host prefix (1), port (4),
                // rack prefix (1), and tagged-fields varint (1) on the wire.
                nodeEndpoints = reader.ReadCompactArray(
                    static (ref KafkaProtocolReader r) => NodeEndpoint.Read(ref r),
                    minElementSize: 11,
                    maxCount: MaxNodeEndpointCount);
            }
            else
            {
                reader.Skip(size);
            }

            LeaderDiscoveryFields.SkipRemaining(ref reader, start, size);
        }

        return nodeEndpoints ?? [];
    }

    /// <summary>
    /// Returns this response to the pool for reuse. Must be called after processing is complete.
    /// Oversized instances are dropped instead of pooled (the pool self-heals via Create on the
    /// next miss), and oversized nested partition arrays are trimmed, so hostile frames cannot
    /// pin large allocations in the pool.
    /// </summary>
    internal void Return()
    {
        if (Responses.Length > MaxPooledTopicCapacity)
            return;

        // Only entries this response touched can have grown or hold references; earlier
        // returns already vetted the rest, so the scan is O(this response's content) —
        // which the caller has just iterated anyway — not O(retained capacity).
        for (var i = 0; i < TopicCount; i++)
        {
            ref var topic = ref Responses[i];
            topic.Name = string.Empty;

            if (topic.PartitionResponses is not { } partitions)
                continue;

            if (partitions.Length > MaxPooledPartitionCapacity)
            {
                topic.PartitionResponses = null!;
                continue;
            }

            // Clear consumed entries so a pooled instance never pins error-message
            // strings or record-error arrays from prior responses — hostile frames can
            // carry multi-MB error strings that would otherwise stay live for the
            // pool's lifetime. (PartitionCount can exceed the array length when a
            // hostile count failed validation before allocation, hence the Min.)
            Array.Clear(partitions, 0, Math.Min(topic.PartitionCount, partitions.Length));
        }

        Volatile.Read(ref s_pool).Return(this);
    }

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
            item.NodeEndpoints = [];
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
    /// Topic ID returned instead of <see cref="Name"/> in Produce v13+.
    /// </summary>
    public Guid TopicId { get; internal set; }

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
        if (version >= ProduceRequest.TopicIdVersion)
        {
            TopicId = reader.ReadUuid();
            Name = string.Empty;
        }
        else
        {
            Name = TopicNameInternCache.Intern(reader.ReadCompactNonNullableString());
            TopicId = Guid.Empty;
        }

        var partitionCount = reader.ReadUnsignedVarInt() - 1;

        PartitionCount = Math.Max(partitionCount, 0);

        if (PartitionCount > 0)
        {
            // Each partition entry carries 30 bytes of fixed fields (index, error code,
            // base offset, log append time, log start offset) plus three varints for the
            // record-errors array, error message, and tagged fields. A count that cannot
            // fit in the remaining payload at that minimum size, or that exceeds the
            // absolute cap, is malformed and must fail before the array allocation.
            reader.ValidateReadableLength(PartitionCount, 33, ProduceResponse.MaxPartitionCount);

            if (PartitionResponses is null || PartitionResponses.Length < PartitionCount)
                PartitionResponses = new ProduceResponsePartitionData[PartitionCount];

            for (var i = 0; i < PartitionCount; i++)
                PartitionResponses[i] = ProduceResponsePartitionData.Read(ref reader, version);
        }

        reader.SkipTaggedFields();
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

    /// <summary>
    /// Current leader reported by KIP-951 for retriable leader errors.
    /// </summary>
    public LeaderIdAndEpoch? CurrentLeader { get; init; }

    public static ProduceResponsePartitionData Read(ref KafkaProtocolReader reader, short version)
    {
        var index = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var baseOffset = reader.ReadInt64();
        var logAppendTimeMs = reader.ReadInt64();
        var logStartOffset = reader.ReadInt64();

        // Each record error needs at least batch index (4), error message prefix (1),
        // and tagged-fields varint (1) on the wire.
        var recordErrors = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => BatchIndexAndErrorMessage.Read(ref r, v),
            version,
            minElementSize: 6,
            maxCount: ProduceResponse.MaxRecordErrorCount);
        var errorMessage = reader.ReadCompactString();
        var currentLeader = ReadPartitionTaggedFields(ref reader, version);

        return new ProduceResponsePartitionData
        {
            Index = index,
            ErrorCode = errorCode,
            BaseOffset = baseOffset,
            LogAppendTimeMs = logAppendTimeMs,
            LogStartOffset = logStartOffset,
            RecordErrors = recordErrors,
            ErrorMessage = errorMessage,
            CurrentLeader = currentLeader
        };
    }

    private static LeaderIdAndEpoch? ReadPartitionTaggedFields(ref KafkaProtocolReader reader, short version)
    {
        LeaderIdAndEpoch? currentLeader = null;
        var numFields = reader.ReadUnsignedVarInt();
        for (var i = 0; i < numFields; i++)
        {
            var tag = reader.ReadUnsignedVarInt();
            var size = reader.ReadUnsignedVarInt();
            var start = reader.Consumed;

            if (tag == 0 && version >= 10)
            {
                currentLeader = LeaderDiscoveryFields.ReadLeaderIdAndEpoch(ref reader, size);
            }
            else
            {
                reader.Skip(size);
            }

            LeaderDiscoveryFields.SkipRemaining(ref reader, start, size);
        }

        return currentLeader;
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
        var batchIndex = reader.ReadInt32();
        var errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new BatchIndexAndErrorMessage
        {
            BatchIndex = batchIndex,
            BatchIndexErrorMessage = errorMessage
        };
    }
}
