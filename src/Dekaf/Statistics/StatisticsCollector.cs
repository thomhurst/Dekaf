using System.Collections.Concurrent;
using System.Diagnostics;

namespace Dekaf.Statistics;

/// <summary>
/// Snapshot of global producer statistics counters.
/// </summary>
internal readonly record struct ProducerGlobalStats
{
    public long MessagesProduced { get; init; }
    public long MessagesDelivered { get; init; }
    public long MessagesFailed { get; init; }
    public long BytesProduced { get; init; }
    public long RequestsSent { get; init; }
    public long ResponsesReceived { get; init; }
    public long Retries { get; init; }
    public double AvgLatencyMs { get; init; }
}

/// <summary>
/// Snapshot of global consumer statistics counters.
/// </summary>
internal readonly record struct ConsumerGlobalStats
{
    public long MessagesConsumed { get; init; }
    public long BytesConsumed { get; init; }
    public long TotalRebalances { get; init; }
    public long? LastRebalanceDurationMs { get; init; }
    public long TotalRebalanceDurationMs { get; init; }
    public long FetchRequestsSent { get; init; }
    public long FetchResponsesReceived { get; init; }
    public double AvgFetchLatencyMs { get; init; }
}

/// <summary>
/// Internal collector for producer statistics counters.
/// Thread-safe for concurrent access from producer workers and sender.
/// </summary>
internal sealed class ProducerStatisticsCollector
{
    // Global counters
    private long _messagesProduced;
    private long _messagesDelivered;
    private long _messagesFailed;
    private long _bytesProduced;
    private long _requestsSent;
    private long _responsesReceived;
    private long _retries;
    private long _totalLatencyMs;
    private long _latencyCount;

    // Per-topic counters
    private readonly ConcurrentDictionary<string, TopicCounters> _topicCounters = new();

    // Per-partition counters
    private readonly ConcurrentDictionary<(string Topic, int Partition), PartitionCounters> _partitionCounters = new();

    // Reverse index: topic -> set of partitions for O(1) lookup in GetTopicStatistics()
    // Avoids O(n²) scan of all partitions for each topic
    // Uses ConcurrentDictionary<int, byte> as a concurrent HashSet (value is unused)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> _topicPartitionIndex = new();

    public void RecordMessageProduced(string topic, int partition, int bytes)
    {
        Interlocked.Increment(ref _messagesProduced);
        Interlocked.Add(ref _bytesProduced, bytes);

        var topicCounters = _topicCounters.GetOrAdd(topic, static _ => new TopicCounters());
        topicCounters.IncrementProduced(bytes);

        var partitionKey = (topic, partition);
        var partitionCounters = _partitionCounters.GetOrAdd(partitionKey, static _ => new PartitionCounters());
        partitionCounters.IncrementProduced(bytes);
        partitionCounters.IncrementQueued();

        // Maintain reverse index for O(1) topic->partition lookup
        var partitionSet = _topicPartitionIndex.GetOrAdd(topic, static _ => new ConcurrentDictionary<int, byte>());
        partitionSet.TryAdd(partition, 0);
    }

    /// <summary>
    /// Fast path for fire-and-forget: only updates global atomic counters.
    /// Skips per-topic/partition dictionary lookups for maximum throughput.
    /// Inspired by librdkafka's atomic-only statistics in fire-and-forget mode.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void RecordMessageProducedFast(int bytes)
    {
        Interlocked.Increment(ref _messagesProduced);
        Interlocked.Add(ref _bytesProduced, bytes);
    }

    public void RecordBatchDelivered(string topic, int partition, int messageCount)
    {
        Interlocked.Add(ref _messagesDelivered, messageCount);

        var topicCounters = _topicCounters.GetOrAdd(topic, static _ => new TopicCounters());
        topicCounters.AddDelivered(messageCount);

        var partitionKey = (topic, partition);
        var partitionCounters = _partitionCounters.GetOrAdd(partitionKey, static _ => new PartitionCounters());
        partitionCounters.AddDelivered(messageCount);
        partitionCounters.AddQueued(-messageCount);
    }

    public void RecordBatchFailed(string topic, int partition, int messageCount)
    {
        Interlocked.Add(ref _messagesFailed, messageCount);

        var topicCounters = _topicCounters.GetOrAdd(topic, static _ => new TopicCounters());
        topicCounters.AddFailed(messageCount);

        var partitionKey = (topic, partition);
        var partitionCounters = _partitionCounters.GetOrAdd(partitionKey, static _ => new PartitionCounters());
        partitionCounters.AddFailed(messageCount);
        partitionCounters.AddQueued(-messageCount);
    }

    public void RecordRequestSent()
    {
        Interlocked.Increment(ref _requestsSent);
    }

    public void RecordResponseReceived(long latencyMs)
    {
        Interlocked.Increment(ref _responsesReceived);
        Interlocked.Add(ref _totalLatencyMs, latencyMs);
        Interlocked.Increment(ref _latencyCount);
    }

    public void RecordRetry()
    {
        Interlocked.Increment(ref _retries);
    }

    public ProducerGlobalStats GetGlobalStats()
    {
        var latencyCount = Interlocked.Read(ref _latencyCount);
        var avgLatency = latencyCount > 0
            ? (double)Interlocked.Read(ref _totalLatencyMs) / latencyCount
            : 0;

        return new ProducerGlobalStats
        {
            MessagesProduced = Interlocked.Read(ref _messagesProduced),
            MessagesDelivered = Interlocked.Read(ref _messagesDelivered),
            MessagesFailed = Interlocked.Read(ref _messagesFailed),
            BytesProduced = Interlocked.Read(ref _bytesProduced),
            RequestsSent = Interlocked.Read(ref _requestsSent),
            ResponsesReceived = Interlocked.Read(ref _responsesReceived),
            Retries = Interlocked.Read(ref _retries),
            AvgLatencyMs = avgLatency
        };
    }

    public IReadOnlyDictionary<string, TopicStatistics> GetTopicStatistics()
    {
        var result = new Dictionary<string, TopicStatistics>();

        foreach (var (topic, counters) in _topicCounters)
        {
            var (produced, delivered, failed, bytes) = counters.GetStats();

            // Get partition stats for this topic using reverse index (O(1) per partition vs O(n) scan)
            var partitionStats = new Dictionary<int, PartitionStatistics>();
            if (_topicPartitionIndex.TryGetValue(topic, out var partitionSet))
            {
                foreach (var partition in partitionSet.Keys)
                {
                    if (_partitionCounters.TryGetValue((topic, partition), out var pCounters))
                    {
                        var (pProduced, pDelivered, pFailed, pBytes, pQueued) = pCounters.GetStats();
                        partitionStats[partition] = new PartitionStatistics
                        {
                            Partition = partition,
                            MessagesProduced = pProduced,
                            MessagesDelivered = pDelivered,
                            MessagesFailed = pFailed,
                            BytesProduced = pBytes,
                            QueuedMessages = pQueued
                        };
                    }
                }
            }

            result[topic] = new TopicStatistics
            {
                Topic = topic,
                MessagesProduced = produced,
                MessagesDelivered = delivered,
                MessagesFailed = failed,
                BytesProduced = bytes,
                Partitions = partitionStats
            };
        }

        return result;
    }

    private sealed class TopicCounters
    {
        private long _produced;
        private long _delivered;
        private long _failed;
        private long _bytes;

        public void IncrementProduced(int bytes)
        {
            Interlocked.Increment(ref _produced);
            Interlocked.Add(ref _bytes, bytes);
        }

        public void IncrementDelivered() => Interlocked.Increment(ref _delivered);
        public void AddDelivered(int count) => Interlocked.Add(ref _delivered, count);
        public void IncrementFailed() => Interlocked.Increment(ref _failed);
        public void AddFailed(int count) => Interlocked.Add(ref _failed, count);

        public (long Produced, long Delivered, long Failed, long Bytes) GetStats() =>
            (Interlocked.Read(ref _produced),
             Interlocked.Read(ref _delivered),
             Interlocked.Read(ref _failed),
             Interlocked.Read(ref _bytes));
    }

    private sealed class PartitionCounters
    {
        private long _produced;
        private long _delivered;
        private long _failed;
        private long _bytes;
        private int _queued;

        public void IncrementProduced(int bytes)
        {
            Interlocked.Increment(ref _produced);
            Interlocked.Add(ref _bytes, bytes);
        }

        public void IncrementDelivered() => Interlocked.Increment(ref _delivered);
        public void AddDelivered(int count) => Interlocked.Add(ref _delivered, count);
        public void IncrementFailed() => Interlocked.Increment(ref _failed);
        public void AddFailed(int count) => Interlocked.Add(ref _failed, count);
        public void IncrementQueued() => Interlocked.Increment(ref _queued);
        public void DecrementQueued() => Interlocked.Decrement(ref _queued);
        public void AddQueued(int count) => Interlocked.Add(ref _queued, count);

        public (long Produced, long Delivered, long Failed, long Bytes, int Queued) GetStats() =>
            (Interlocked.Read(ref _produced),
             Interlocked.Read(ref _delivered),
             Interlocked.Read(ref _failed),
             Interlocked.Read(ref _bytes),
             Volatile.Read(ref _queued));
    }
}

/// <summary>
/// Internal collector for consumer statistics counters.
/// Thread-safe for concurrent access from consumer and prefetch tasks.
/// </summary>
internal sealed class ConsumerStatisticsCollector
{
    // Global counters
    private long _messagesConsumed;
    private long _bytesConsumed;
    private long _totalRebalances;
    private long _lastRebalanceDurationMs = -1;
    private long _totalRebalanceDurationMs;
    private long _rebalanceStartTimestamp = -1;
    private long _fetchRequestsSent;
    private long _fetchResponsesReceived;
    private long _totalFetchLatencyMs;
    private long _fetchLatencyCount;

    // Per-topic counters
    private readonly ConcurrentDictionary<string, ConsumerTopicCounters> _topicCounters = new();

    // Per-partition counters
    private readonly ConcurrentDictionary<(string Topic, int Partition), ConsumerPartitionCounters> _partitionCounters = new();

    // Reverse index: topic -> set of partitions for O(1) lookup in GetTopicStatistics()
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> _topicPartitionIndex = new();

    /// <summary>
    /// Records consumed messages at the batch level for efficient statistics updates.
    /// Called once per partition-fetch instead of per message.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void RecordMessagesConsumedBatch(string topic, int partition, long messageCount, long bytes)
    {
        Interlocked.Add(ref _messagesConsumed, messageCount);
        Interlocked.Add(ref _bytesConsumed, bytes);

        var topicCounters = _topicCounters.GetOrAdd(topic, static _ => new ConsumerTopicCounters());
        topicCounters.AddConsumed(messageCount, bytes);

        var partitionKey = (topic, partition);
        var partitionCounters = _partitionCounters.GetOrAdd(partitionKey, static _ => new ConsumerPartitionCounters());
        partitionCounters.AddConsumed(messageCount, bytes);

        // Maintain reverse index for O(1) topic->partition lookup
        var partitionSet = _topicPartitionIndex.GetOrAdd(topic, static _ => new ConcurrentDictionary<int, byte>());
        partitionSet.TryAdd(partition, 0);
    }

    /// <summary>
    /// Records the start of a rebalance. Captures a high-resolution timestamp.
    /// </summary>
    public void RecordRebalanceStarted()
    {
        Volatile.Write(ref _rebalanceStartTimestamp, Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Cancels a previously started rebalance timer without recording a completed rebalance.
    /// Used when the group coordination did not result in an actual assignment change.
    /// </summary>
    public void CancelRebalanceStarted()
    {
        Volatile.Write(ref _rebalanceStartTimestamp, -1);
    }

    /// <summary>
    /// Records the completion of a rebalance. Computes duration from the start timestamp,
    /// increments the total rebalance count, and accumulates total rebalance time.
    /// </summary>
    public void RecordRebalanceCompleted()
    {
        var startTimestamp = Interlocked.Exchange(ref _rebalanceStartTimestamp, -1);
        if (startTimestamp < 0)
            return;

        var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        var durationMs = (long)elapsed.TotalMilliseconds;

        Interlocked.Exchange(ref _lastRebalanceDurationMs, durationMs);
        Interlocked.Add(ref _totalRebalanceDurationMs, durationMs);
        Interlocked.Increment(ref _totalRebalances);
    }

    public void RecordFetchRequestSent()
    {
        Interlocked.Increment(ref _fetchRequestsSent);
    }

    public void RecordFetchResponseReceived(long latencyMs)
    {
        Interlocked.Increment(ref _fetchResponsesReceived);
        Interlocked.Add(ref _totalFetchLatencyMs, latencyMs);
        Interlocked.Increment(ref _fetchLatencyCount);
    }

    public void UpdatePartitionHighWatermark(string topic, int partition, long highWatermark)
    {
        var partitionKey = (topic, partition);
        var counters = _partitionCounters.GetOrAdd(partitionKey, static _ => new ConsumerPartitionCounters());
        counters.SetHighWatermark(highWatermark);

        // Maintain reverse index for O(1) topic->partition lookup
        var partitionSet = _topicPartitionIndex.GetOrAdd(topic, static _ => new ConcurrentDictionary<int, byte>());
        partitionSet.TryAdd(partition, 0);
    }

    public ConsumerGlobalStats GetGlobalStats()
    {
        var latencyCount = Interlocked.Read(ref _fetchLatencyCount);
        var avgLatency = latencyCount > 0
            ? (double)Interlocked.Read(ref _totalFetchLatencyMs) / latencyCount
            : 0;

        var lastRebalanceMs = Interlocked.Read(ref _lastRebalanceDurationMs);

        return new ConsumerGlobalStats
        {
            MessagesConsumed = Interlocked.Read(ref _messagesConsumed),
            BytesConsumed = Interlocked.Read(ref _bytesConsumed),
            TotalRebalances = Interlocked.Read(ref _totalRebalances),
            LastRebalanceDurationMs = lastRebalanceMs >= 0 ? lastRebalanceMs : null,
            TotalRebalanceDurationMs = Interlocked.Read(ref _totalRebalanceDurationMs),
            FetchRequestsSent = Interlocked.Read(ref _fetchRequestsSent),
            FetchResponsesReceived = Interlocked.Read(ref _fetchResponsesReceived),
            AvgFetchLatencyMs = avgLatency
        };
    }

    public IReadOnlyDictionary<string, ConsumerTopicStatistics> GetTopicStatistics(
        Func<(string Topic, int Partition), (long? Position, long? CommittedOffset, bool IsPaused)> getPartitionInfo)
    {
        var result = new Dictionary<string, ConsumerTopicStatistics>();

        foreach (var (topic, counters) in _topicCounters)
        {
            var (consumed, bytes) = counters.GetStats();

            // Get partition stats for this topic using reverse index (O(1) per partition vs O(n) scan)
            var partitionStats = new Dictionary<int, ConsumerPartitionStatistics>();
            if (_topicPartitionIndex.TryGetValue(topic, out var partitionSet))
            {
                foreach (var partition in partitionSet.Keys)
                {
                    if (_partitionCounters.TryGetValue((topic, partition), out var pCounters))
                    {
                        var (pConsumed, pBytes, pHighWatermark) = pCounters.GetStats();
                        var (position, committedOffset, isPaused) = getPartitionInfo((topic, partition));

                        partitionStats[partition] = new ConsumerPartitionStatistics
                        {
                            Partition = partition,
                            MessagesConsumed = pConsumed,
                            BytesConsumed = pBytes,
                            Position = position,
                            CommittedOffset = committedOffset,
                            HighWatermark = pHighWatermark,
                            IsPaused = isPaused
                        };
                    }
                }
            }

            result[topic] = new ConsumerTopicStatistics
            {
                Topic = topic,
                MessagesConsumed = consumed,
                BytesConsumed = bytes,
                Partitions = partitionStats
            };
        }

        return result;
    }

    private sealed class ConsumerTopicCounters
    {
        private long _consumed;
        private long _bytes;

        public void IncrementConsumed(int bytes)
        {
            Interlocked.Increment(ref _consumed);
            Interlocked.Add(ref _bytes, bytes);
        }

        public void AddConsumed(long count, long bytes)
        {
            Interlocked.Add(ref _consumed, count);
            Interlocked.Add(ref _bytes, bytes);
        }

        public (long Consumed, long Bytes) GetStats() =>
            (Interlocked.Read(ref _consumed), Interlocked.Read(ref _bytes));
    }

    private sealed class ConsumerPartitionCounters
    {
        private long _consumed;
        private long _bytes;
        private long _highWatermark = -1;

        public void IncrementConsumed(int bytes)
        {
            Interlocked.Increment(ref _consumed);
            Interlocked.Add(ref _bytes, bytes);
        }

        public void AddConsumed(long count, long bytes)
        {
            Interlocked.Add(ref _consumed, count);
            Interlocked.Add(ref _bytes, bytes);
        }

        public void SetHighWatermark(long highWatermark)
        {
            Interlocked.Exchange(ref _highWatermark, highWatermark);
        }

        public (long Consumed, long Bytes, long? HighWatermark) GetStats()
        {
            var hw = Interlocked.Read(ref _highWatermark);
            return (
                Interlocked.Read(ref _consumed),
                Interlocked.Read(ref _bytes),
                hw >= 0 ? hw : null
            );
        }
    }
}
