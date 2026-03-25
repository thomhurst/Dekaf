using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Dekaf.Protocol.Records;

namespace Dekaf.Protocol.Messages;

/// <summary>
/// Fetch response (API key 1).
/// Contains records fetched from topic partitions.
/// </summary>
public sealed class FetchResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.Fetch;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 16;

    // ── Pool for FetchResponse reuse ──
    // Eliminates per-fetch-cycle allocation. Typical consumer has 1-2 concurrent fetches.
    private static readonly ConcurrentStack<FetchResponse> s_pool = new();
    private static int s_poolCount;
    private const int MaxPoolSize = 64;

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
    public IReadOnlyList<FetchResponseTopic> Responses { get; internal set; } = [];

    /// <summary>
    /// Internal: Pooled memory owner for zero-copy parsing.
    /// Set by KafkaConnection after parsing when records reference the network buffer.
    /// Must be disposed after all records have been consumed.
    /// </summary>
    internal IPooledMemory? PooledMemoryOwner { get; set; }

    /// <summary>
    /// Rents a FetchResponse from the pool or creates a new one.
    /// Caller must call <see cref="ReturnToPool"/> after the response is fully processed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FetchResponse RentFromPool()
    {
        if (s_pool.TryPop(out var response))
        {
            Interlocked.Decrement(ref s_poolCount);
            return response;
        }
        return new FetchResponse();
    }

    /// <summary>
    /// Returns this FetchResponse and all nested topic/partition objects to their pools.
    /// Clears all fields to avoid leaking references.
    /// </summary>
    internal void ReturnToPool()
    {
        // Return nested topics (and their partitions) first
        foreach (var topic in Responses)
        {
            topic.ReturnToPool();
        }

        // Clear all references
        Responses = [];
        PooledMemoryOwner = null;

        // Reset value types to defaults
        ThrottleTimeMs = 0;
        ErrorCode = ErrorCode.None;
        SessionId = 0;

        // Soft limit: intentionally non-atomic check-then-act to keep return path lock-free
        if (Volatile.Read(ref s_poolCount) < MaxPoolSize)
        {
            s_pool.Push(this);
            Interlocked.Increment(ref s_poolCount);
        }
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

        var response = RentFromPool();
        response.ThrottleTimeMs = throttleTimeMs;
        response.ErrorCode = errorCode;
        response.SessionId = sessionId;
        response.Responses = responses;
        return response;
    }
}

/// <summary>
/// Topic response in a fetch response.
/// </summary>
public sealed class FetchResponseTopic
{
    // ── Pool for FetchResponseTopic reuse ──
    // One topic per subscribed topic per fetch cycle; typically 1-10.
    private static readonly ConcurrentStack<FetchResponseTopic> s_pool = new();
    private static int s_poolCount;
    private const int MaxPoolSize = 256;

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
    public IReadOnlyList<FetchResponsePartition> Partitions { get; internal set; } = [];

    /// <summary>
    /// Rents a FetchResponseTopic from the pool or creates a new one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FetchResponseTopic RentFromPool()
    {
        if (s_pool.TryPop(out var topic))
        {
            Interlocked.Decrement(ref s_poolCount);
            return topic;
        }
        return new FetchResponseTopic();
    }

    /// <summary>
    /// Returns this FetchResponseTopic and all nested partition objects to their pools.
    /// Clears all fields to avoid leaking references.
    /// </summary>
    internal void ReturnToPool()
    {
        // Return nested partitions first
        foreach (var partition in Partitions)
        {
            partition.ReturnToPool();
        }

        // Clear references
        Topic = null;
        TopicId = Guid.Empty;
        Partitions = [];

        if (Volatile.Read(ref s_poolCount) < MaxPoolSize)
        {
            s_pool.Push(this);
            Interlocked.Increment(ref s_poolCount);
        }
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

        var result = RentFromPool();
        result.Topic = topic;
        result.TopicId = topicId;
        result.Partitions = partitions;
        return result;
    }
}

/// <summary>
/// Partition response in a fetch response.
/// </summary>
public sealed class FetchResponsePartition
{
    // Pool for reusing List<RecordBatch> instances to reduce GC pressure.
    // Soft limit of 64, matching LazyRecordList pattern in RecordBatch.cs.
    private static readonly ConcurrentBag<List<RecordBatch>> s_recordBatchListPool = new();
    private const int MaxPooledLists = 64;

    internal static List<RecordBatch> RentRecordBatchList()
    {
        if (s_recordBatchListPool.TryTake(out var list))
            return list;
        return [];
    }

    internal static void ReturnRecordBatchList(List<RecordBatch> list)
    {
        if (s_recordBatchListPool.Count < MaxPooledLists)
        {
            list.Clear();
            s_recordBatchListPool.Add(list);
        }
    }

    // ── Pool for FetchResponsePartition reuse ──
    // One partition per assigned partition per fetch cycle; typically 1-50.
    private static readonly ConcurrentStack<FetchResponsePartition> s_pool = new();
    private static int s_poolCount;
    private const int MaxPoolSize = 1024;

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
    public IReadOnlyList<AbortedTransaction>? AbortedTransactions { get; internal set; }

    /// <summary>
    /// Preferred read replica.
    /// </summary>
    public int PreferredReadReplica { get; internal set; } = -1;

    /// <summary>
    /// Record batches.
    /// </summary>
    public IReadOnlyList<RecordBatch>? Records { get; internal set; }

    /// <summary>
    /// Rents a FetchResponsePartition from the pool or creates a new one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FetchResponsePartition RentFromPool()
    {
        if (s_pool.TryPop(out var partition))
        {
            Interlocked.Decrement(ref s_poolCount);
            return partition;
        }
        return new FetchResponsePartition();
    }

    /// <summary>
    /// Returns this FetchResponsePartition to the pool.
    /// Clears all fields to avoid leaking references.
    /// Does NOT dispose Records — ownership is transferred to PendingFetchData.
    /// </summary>
    internal void ReturnToPool()
    {
        // Clear references to avoid holding onto GC-tracked objects
        DivergingEpoch = null;
        CurrentLeader = null;
        SnapshotId = null;
        AbortedTransactions = null;
        Records = null;

        // Reset value types to defaults
        PartitionIndex = 0;
        ErrorCode = ErrorCode.None;
        HighWatermark = 0;
        LastStableOffset = -1;
        LogStartOffset = -1;
        PreferredReadReplica = -1;

        if (Volatile.Read(ref s_poolCount) < MaxPoolSize)
        {
            s_pool.Push(this);
            Interlocked.Increment(ref s_poolCount);
        }
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

        var result = RentFromPool();
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
