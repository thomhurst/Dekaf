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

    public void RecordMessageProduced(string topic, int partition, int bytes)
    {
        Interlocked.Increment(ref _messagesProduced);
        Interlocked.Add(ref _bytesProduced, bytes);

        if (!_topicCounters.TryGetValue(topic, out var topicCounters))
        {
            topicCounters = new TopicCounters();
            _topicCounters.TryAdd(topic, topicCounters);
        }
        topicCounters.IncrementProduced(bytes);

        var partitionKey = (topic, partition);
        if (!_partitionCounters.TryGetValue(partitionKey, out var partitionCounters))
        {
            partitionCounters = new PartitionCounters();
            _partitionCounters.TryAdd(partitionKey, partitionCounters);
        }
        partitionCounters.IncrementProduced(bytes);
        partitionCounters.IncrementQueued();
    }

    public void RecordMessageDelivered(string topic, int partition, int bytes)
    {
        Interlocked.Increment(ref _messagesDelivered);

        if (!_topicCounters.TryGetValue(topic, out var topicCounters))
        {
            topicCounters = new TopicCounters();
            _topicCounters.TryAdd(topic, topicCounters);
        }
        topicCounters.IncrementDelivered();

        var partitionKey = (topic, partition);
        if (!_partitionCounters.TryGetValue(partitionKey, out var partitionCounters))
        {
            partitionCounters = new PartitionCounters();
            _partitionCounters.TryAdd(partitionKey, partitionCounters);
        }
        partitionCounters.IncrementDelivered();
        partitionCounters.DecrementQueued();
    }

    public void RecordBatchDelivered(string topic, int partition, int messageCount)
    {
        Interlocked.Add(ref _messagesDelivered, messageCount);

        if (!_topicCounters.TryGetValue(topic, out var topicCounters))
        {
            topicCounters = new TopicCounters();
            _topicCounters.TryAdd(topic, topicCounters);
        }
        topicCounters.AddDelivered(messageCount);

        var partitionKey = (topic, partition);
        if (!_partitionCounters.TryGetValue(partitionKey, out var partitionCounters))
        {
            partitionCounters = new PartitionCounters();
            _partitionCounters.TryAdd(partitionKey, partitionCounters);
        }
        partitionCounters.AddDelivered(messageCount);
        partitionCounters.AddQueued(-messageCount);
    }

    public void RecordMessageFailed(string topic, int partition)
    {
        Interlocked.Increment(ref _messagesFailed);

        if (!_topicCounters.TryGetValue(topic, out var topicCounters))
        {
            topicCounters = new TopicCounters();
            _topicCounters.TryAdd(topic, topicCounters);
        }
        topicCounters.IncrementFailed();

        var partitionKey = (topic, partition);
        if (!_partitionCounters.TryGetValue(partitionKey, out var partitionCounters))
        {
            partitionCounters = new PartitionCounters();
            _partitionCounters.TryAdd(partitionKey, partitionCounters);
        }
        partitionCounters.IncrementFailed();
        partitionCounters.DecrementQueued();
    }

    public void RecordBatchFailed(string topic, int partition, int messageCount)
    {
        Interlocked.Add(ref _messagesFailed, messageCount);

        if (!_topicCounters.TryGetValue(topic, out var topicCounters))
        {
            topicCounters = new TopicCounters();
            _topicCounters.TryAdd(topic, topicCounters);
        }
        topicCounters.AddFailed(messageCount);

        var partitionKey = (topic, partition);
        if (!_partitionCounters.TryGetValue(partitionKey, out var partitionCounters))
        {
            partitionCounters = new PartitionCounters();
            _partitionCounters.TryAdd(partitionKey, partitionCounters);
        }
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

            // Get partition stats for this topic
            var partitionStats = new Dictionary<int, PartitionStatistics>();
            foreach (var ((t, partition), pCounters) in _partitionCounters)
            {
                if (t != topic) continue;

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

    public void RecordMessageConsumed(string topic, int partition, int bytes)
    {
        Interlocked.Increment(ref _messagesConsumed);
        Interlocked.Add(ref _bytesConsumed, bytes);

        if (!_topicCounters.TryGetValue(topic, out var topicCounters))
        {
            topicCounters = new ConsumerTopicCounters();
            _topicCounters.TryAdd(topic, topicCounters);
        }
        topicCounters.IncrementConsumed(bytes);

        var partitionKey = (topic, partition);
        if (!_partitionCounters.TryGetValue(partitionKey, out var partitionCounters))
        {
            partitionCounters = new ConsumerPartitionCounters();
            _partitionCounters.TryAdd(partitionKey, partitionCounters);
        }
        partitionCounters.IncrementConsumed(bytes);
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
        if (!_partitionCounters.TryGetValue(partitionKey, out var counters))
        {
            counters = new ConsumerPartitionCounters();
            _partitionCounters.TryAdd(partitionKey, counters);
        }
        counters.SetHighWatermark(highWatermark);
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

            // Get partition stats for this topic
            var partitionStats = new Dictionary<int, ConsumerPartitionStatistics>();
            foreach (var ((t, partition), pCounters) in _partitionCounters)
            {
                if (t != topic) continue;

                var (pConsumed, pBytes, pHighWatermark) = pCounters.GetStats();
                var (position, committedOffset, isPaused) = getPartitionInfo((t, partition));

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
