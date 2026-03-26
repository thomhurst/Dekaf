using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Protocol.Messages;

/// <summary>
/// Fetch response (API key 1).
/// Contains records fetched from topic partitions.
/// </summary>
public sealed class FetchResponse : IKafkaResponse
{
    // Pool to reuse FetchResponse instances across fetch cycles.
    // One instance per fetch cycle, so a small pool suffices.
    private static readonly FetchResponsePool s_pool = new();

    private int _pooled; // 0 = active, 1 = returned to pool; used with Interlocked for atomic guard
    private IReadOnlyList<FetchResponseTopic> _responses = Array.Empty<FetchResponseTopic>();

    public static ApiKey ApiKey => ApiKey.Fetch;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 16;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; internal set; }

    /// <summary>
    /// Top-level error code.
    /// </summary>
    public ErrorCode ErrorCode { get; internal set; }

    /// <summary>
    /// Fetch session ID.
    /// </summary>
    public int SessionId { get; internal set; }

    /// <summary>
    /// Responses per topic.
    /// </summary>
    public IReadOnlyList<FetchResponseTopic> Responses
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _pooled) != 0, this);
            return _responses;
        }
        internal set => _responses = value;
    }

    /// <summary>
    /// Internal: Pooled memory owner for zero-copy parsing.
    /// Set by KafkaConnection after parsing when records reference the network buffer.
    /// Must be disposed after all records have been consumed.
    /// </summary>
    internal IPooledMemory? PooledMemoryOwner { get; set; }

    /// <summary>
    /// Returns this FetchResponse and all nested topic/partition objects to their pools.
    /// Call after all data has been extracted from the response.
    /// </summary>
    internal void ReturnToPool()
    {
        if (Interlocked.CompareExchange(ref _pooled, 1, 0) != 0)
            return;

        foreach (var topic in _responses)
        {
            topic.ReturnToPool();
        }

        s_pool.Return(this);
    }

    internal static FetchResponse Rent()
    {
        var item = s_pool.Rent();
        Volatile.Write(ref item._pooled, 0);
        return item;
    }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 12;

        var throttleTimeMs = version >= 1 ? reader.ReadInt32() : 0;
        var errorCode = version >= 7 ? (ErrorCode)reader.ReadInt16() : ErrorCode.None;
        var sessionId = version >= 7 ? reader.ReadInt32() : 0;

        var responses = isFlexible
            ? reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => FetchResponseTopic.Read(ref r, v), version)
            : reader.ReadArray(static (ref KafkaProtocolReader r, short v) => FetchResponseTopic.Read(ref r, v), version);

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        var response = Rent();
        response.ThrottleTimeMs = throttleTimeMs;
        response.ErrorCode = errorCode;
        response.SessionId = sessionId;
        response.Responses = responses;
        return response;
    }

    private sealed class FetchResponsePool() : ObjectPool<FetchResponse>(maxPoolSize: 16)
    {
        protected override FetchResponse Create() => new();

        protected override void Reset(FetchResponse item)
        {
            item.ThrottleTimeMs = 0;
            item.ErrorCode = ErrorCode.None;
            item.SessionId = 0;
            item._responses = Array.Empty<FetchResponseTopic>();
            item.PooledMemoryOwner = null;
        }
    }
}

/// <summary>
/// Topic response in a fetch response.
/// </summary>
public sealed class FetchResponseTopic
{
    // Pool to reuse FetchResponseTopic instances. Typically 1-3 per fetch cycle.
    private static readonly FetchResponseTopicPool s_pool = new();

    private int _pooled; // 0 = active, 1 = returned to pool
    private IReadOnlyList<FetchResponsePartition> _partitions = Array.Empty<FetchResponsePartition>();

    /// <summary>
    /// Topic name (v0-v12).
    /// </summary>
    public string? Topic { get; internal set; }

    /// <summary>
    /// Topic ID (v13+).
    /// </summary>
    public Guid TopicId { get; internal set; }

    /// <summary>
    /// Partition responses.
    /// </summary>
    public IReadOnlyList<FetchResponsePartition> Partitions
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _pooled) != 0, this);
            return _partitions;
        }
        internal set => _partitions = value;
    }

    /// <summary>
    /// Returns this FetchResponseTopic and all nested partition objects to their pools.
    /// </summary>
    internal void ReturnToPool()
    {
        if (Interlocked.CompareExchange(ref _pooled, 1, 0) != 0)
            return;

        foreach (var partition in _partitions)
        {
            partition.ReturnToPool();
        }

        s_pool.Return(this);
    }

    internal static FetchResponseTopic Rent()
    {
        var item = s_pool.Rent();
        Volatile.Write(ref item._pooled, 0);
        return item;
    }

    public static FetchResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 12;

        Guid topicId = Guid.Empty;
        string? topic = null;

        if (version >= 13)
        {
            topicId = reader.ReadUuid();
        }
        else
        {
            topic = isFlexible ? reader.ReadCompactString() : reader.ReadString();
        }

        var partitions = isFlexible
            ? reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => FetchResponsePartition.Read(ref r, v), version)
            : reader.ReadArray(static (ref KafkaProtocolReader r, short v) => FetchResponsePartition.Read(ref r, v), version);

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        var result = Rent();
        result.Topic = topic;
        result.TopicId = topicId;
        result.Partitions = partitions;
        return result;
    }

    private sealed class FetchResponseTopicPool() : ObjectPool<FetchResponseTopic>(maxPoolSize: 32)
    {
        protected override FetchResponseTopic Create() => new();

        protected override void Reset(FetchResponseTopic item)
        {
            item.Topic = null;
            item.TopicId = Guid.Empty;
            item._partitions = Array.Empty<FetchResponsePartition>();
        }
    }
}

/// <summary>
/// Partition response in a fetch response.
/// </summary>
public sealed class FetchResponsePartition
{
    // Pool for reusing List<RecordBatch> instances to reduce GC pressure.
    private static readonly RecordBatchListPool s_recordBatchListPool = new();

    // Pool to reuse FetchResponsePartition instances. Typically 1-6+ per fetch cycle.
    private static readonly FetchResponsePartitionPool s_pool = new();

    private int _pooled; // 0 = active, 1 = returned to pool
    private IReadOnlyList<RecordBatch>? _records;
    private IReadOnlyList<AbortedTransaction>? _abortedTransactions;

    internal static List<RecordBatch> RentRecordBatchList() => s_recordBatchListPool.Rent();

    internal static void ReturnRecordBatchList(List<RecordBatch> list) => s_recordBatchListPool.Return(list);

    /// <summary>
    /// Partition index.
    /// </summary>
    public int PartitionIndex { get; internal set; }

    /// <summary>
    /// Error code.
    /// </summary>
    public ErrorCode ErrorCode { get; internal set; }

    /// <summary>
    /// High watermark.
    /// </summary>
    public long HighWatermark { get; internal set; }

    /// <summary>
    /// Last stable offset (for transactions).
    /// </summary>
    public long LastStableOffset { get; internal set; } = -1;

    /// <summary>
    /// Log start offset.
    /// </summary>
    public long LogStartOffset { get; internal set; } = -1;

    /// <summary>
    /// Diverging epoch (v12+).
    /// </summary>
    public EpochEndOffset? DivergingEpoch { get; internal set; }

    /// <summary>
    /// Current leader (v12+).
    /// </summary>
    public LeaderIdAndEpoch? CurrentLeader { get; internal set; }

    /// <summary>
    /// Snapshot ID (v12+).
    /// </summary>
    public SnapshotId? SnapshotId { get; internal set; }

    /// <summary>
    /// Aborted transactions (for read committed isolation).
    /// </summary>
    public IReadOnlyList<AbortedTransaction>? AbortedTransactions
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _pooled) != 0, this);
            return _abortedTransactions;
        }
        internal set => _abortedTransactions = value;
    }

    /// <summary>
    /// Preferred read replica.
    /// </summary>
    public int PreferredReadReplica { get; internal set; } = -1;

    /// <summary>
    /// Record batches.
    /// </summary>
    public IReadOnlyList<RecordBatch>? Records
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _pooled) != 0, this);
            return _records;
        }
        internal set => _records = value;
    }

    /// <summary>
    /// Returns this FetchResponsePartition to the pool. Does NOT return Records or AbortedTransactions
    /// since those are transferred to PendingFetchData and have separate lifecycles.
    /// </summary>
    internal void ReturnToPool()
    {
        if (Interlocked.CompareExchange(ref _pooled, 1, 0) != 0)
            return;

        s_pool.Return(this);
    }

    internal static FetchResponsePartition Rent()
    {
        var item = s_pool.Rent();
        Volatile.Write(ref item._pooled, 0);
        return item;
    }

    public static FetchResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 12;

        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var highWatermark = reader.ReadInt64();

        var lastStableOffset = version >= 4 ? reader.ReadInt64() : -1;
        var logStartOffset = version >= 5 ? reader.ReadInt64() : -1;

        EpochEndOffset? divergingEpoch = null;
        LeaderIdAndEpoch? currentLeader = null;
        SnapshotId? snapshotId = null;

        if (version >= 12)
        {
            // These are in tagged fields for flexible versions
        }

        IReadOnlyList<AbortedTransaction>? abortedTransactions = null;
        if (version >= 4)
        {
            abortedTransactions = isFlexible
                ? reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => AbortedTransaction.Read(ref r, v), version)
                : reader.ReadArray(static (ref KafkaProtocolReader r, short v) => AbortedTransaction.Read(ref r, v), version);
        }

        var preferredReadReplica = version >= 11 ? reader.ReadInt32() : -1;

        // Read record batches
        // COMPACT_RECORDS uses COMPACT_NULLABLE_BYTES encoding (length+1, 0 = null)
        // RECORDS uses NULLABLE_BYTES encoding (INT32 length, -1 = null)
        var recordsLength = isFlexible
            ? reader.ReadUnsignedVarInt() - 1
            : reader.ReadInt32();

        List<RecordBatch>? records = null;
        if (recordsLength > 0)
        {
            records = RentRecordBatchList();
            var recordsEndPosition = reader.Consumed + recordsLength;

            while (reader.Consumed < recordsEndPosition && !reader.End)
            {
                try
                {
                    records.Add(RecordBatch.Read(ref reader));
                }
                catch
                {
                    // Partial batch at end, skip remaining
                    break;
                }
            }

            // Skip any remaining bytes
            var remaining = (int)(recordsEndPosition - reader.Consumed);
            if (remaining > 0)
            {
                reader.Skip(remaining);
            }
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        var result = Rent();
        result.PartitionIndex = partitionIndex;
        result.ErrorCode = errorCode;
        result.HighWatermark = highWatermark;
        result.LastStableOffset = lastStableOffset;
        result.LogStartOffset = logStartOffset;
        result.DivergingEpoch = divergingEpoch;
        result.CurrentLeader = currentLeader;
        result.SnapshotId = snapshotId;
        result.AbortedTransactions = abortedTransactions;
        result.PreferredReadReplica = preferredReadReplica;
        result.Records = records;
        return result;
    }

    private sealed class FetchResponsePartitionPool() : ObjectPool<FetchResponsePartition>(maxPoolSize: 64)
    {
        protected override FetchResponsePartition Create() => new();

        protected override void Reset(FetchResponsePartition item)
        {
            item.PartitionIndex = 0;
            item.ErrorCode = ErrorCode.None;
            item.HighWatermark = 0;
            item.LastStableOffset = -1;
            item.LogStartOffset = -1;
            item.DivergingEpoch = null;
            item.CurrentLeader = null;
            item.SnapshotId = null;
            item._abortedTransactions = null;
            item.PreferredReadReplica = -1;
            item._records = null;
        }
    }

    private sealed class RecordBatchListPool() : ObjectPool<List<RecordBatch>>(maxPoolSize: 64)
    {
        protected override List<RecordBatch> Create() => [];

        protected override void Reset(List<RecordBatch> item) => item.Clear();
    }
}

/// <summary>
/// Aborted transaction information.
/// </summary>
public sealed class AbortedTransaction
{
    public required long ProducerId { get; init; }
    public required long FirstOffset { get; init; }

    public static AbortedTransaction Read(ref KafkaProtocolReader reader, short version)
    {
        var producerId = reader.ReadInt64();
        var firstOffset = reader.ReadInt64();

        if (version >= 12)
        {
            reader.SkipTaggedFields();
        }

        return new AbortedTransaction
        {
            ProducerId = producerId,
            FirstOffset = firstOffset
        };
    }
}

/// <summary>
/// Epoch end offset for diverging epoch.
/// </summary>
public sealed class EpochEndOffset
{
    public int Epoch { get; init; } = -1;
    public long EndOffset { get; init; } = -1;
}

/// <summary>
/// Leader ID and epoch.
/// </summary>
public sealed class LeaderIdAndEpoch
{
    public int LeaderId { get; init; } = -1;
    public int LeaderEpoch { get; init; } = -1;
}

/// <summary>
/// Snapshot ID.
/// </summary>
public sealed class SnapshotId
{
    public long EndOffset { get; init; } = -1;
    public int Epoch { get; init; } = -1;
}
