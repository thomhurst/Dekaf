using System.Collections.Concurrent;

namespace Dekaf.Statistics;

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

    public void RecordMessageDelivered(string topic, int partition, int bytes)
    {
        Interlocked.Increment(ref _messagesDelivered);

        var topicCounters = _topicCounters.GetOrAdd(topic, static _ => new TopicCounters());
        topicCounters.IncrementDelivered();

        var partitionKey = (topic, partition);
        var partitionCounters = _partitionCounters.GetOrAdd(partitionKey, static _ => new PartitionCounters());
        partitionCounters.IncrementDelivered();
        partitionCounters.DecrementQueued();
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

    public void RecordMessageFailed(string topic, int partition)
    {
        Interlocked.Increment(ref _messagesFailed);

        var topicCounters = _topicCounters.GetOrAdd(topic, static _ => new TopicCounters());
        topicCounters.IncrementFailed();

        var partitionKey = (topic, partition);
        var partitionCounters = _partitionCounters.GetOrAdd(partitionKey, static _ => new PartitionCounters());
        partitionCounters.IncrementFailed();
        partitionCounters.DecrementQueued();
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

    public (long MessagesProduced, long MessagesDelivered, long MessagesFailed, long BytesProduced,
        long RequestsSent, long ResponsesReceived, long Retries, double AvgLatencyMs) GetGlobalStats()
    {
        var latencyCount = Interlocked.Read(ref _latencyCount);
        var avgLatency = latencyCount > 0
            ? (double)Interlocked.Read(ref _totalLatencyMs) / latencyCount
            : 0;

        return (
            Interlocked.Read(ref _messagesProduced),
            Interlocked.Read(ref _messagesDelivered),
            Interlocked.Read(ref _messagesFailed),
            Interlocked.Read(ref _bytesProduced),
            Interlocked.Read(ref _requestsSent),
            Interlocked.Read(ref _responsesReceived),
            Interlocked.Read(ref _retries),
            avgLatency
        );
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
    private int _rebalanceCount;
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

    public void RecordMessageConsumed(string topic, int partition, int bytes)
    {
        Interlocked.Increment(ref _messagesConsumed);
        Interlocked.Add(ref _bytesConsumed, bytes);

        var topicCounters = _topicCounters.GetOrAdd(topic, static _ => new ConsumerTopicCounters());
        topicCounters.IncrementConsumed(bytes);

        var partitionKey = (topic, partition);
        var partitionCounters = _partitionCounters.GetOrAdd(partitionKey, static _ => new ConsumerPartitionCounters());
        partitionCounters.IncrementConsumed(bytes);

        // Maintain reverse index for O(1) topic->partition lookup
        var partitionSet = _topicPartitionIndex.GetOrAdd(topic, static _ => new ConcurrentDictionary<int, byte>());
        partitionSet.TryAdd(partition, 0);
    }

    /// <summary>
    /// Batch version of RecordMessageConsumed for efficient statistics updates.
    /// Called once per partition-fetch instead of per message.
    /// Inspired by librdkafka's batch-level accounting.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void RecordMessagesConsumedBatch(string topic, int partition, int messageCount, long bytes)
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

    public void RecordRebalance()
    {
        Interlocked.Increment(ref _rebalanceCount);
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

    public (long MessagesConsumed, long BytesConsumed, int RebalanceCount,
        long FetchRequestsSent, long FetchResponsesReceived, double AvgFetchLatencyMs) GetGlobalStats()
    {
        var latencyCount = Interlocked.Read(ref _fetchLatencyCount);
        var avgLatency = latencyCount > 0
            ? (double)Interlocked.Read(ref _totalFetchLatencyMs) / latencyCount
            : 0;

        return (
            Interlocked.Read(ref _messagesConsumed),
            Interlocked.Read(ref _bytesConsumed),
            Volatile.Read(ref _rebalanceCount),
            Interlocked.Read(ref _fetchRequestsSent),
            Interlocked.Read(ref _fetchResponsesReceived),
            avgLatency
        );
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

        public void AddConsumed(int count, long bytes)
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

        public void AddConsumed(int count, long bytes)
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
