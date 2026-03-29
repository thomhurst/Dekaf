namespace Dekaf.Internal;

/// <summary>
/// Centralizes derivation of internal pool sizes from user-facing configuration.
/// All pool sizing is computed once at construction from <c>BufferMemory</c>,
/// <c>BatchSize</c>, <c>MaxInFlightRequestsPerConnection</c>, etc.
/// No new public config knobs — users express workload intent through existing settings.
/// </summary>
internal static class PoolSizing
{
    private const int EstimatedMessagesPerBatch = 1024;
    private const int MinValueTaskSources = 256;
    private const int MaxValueTaskSources = 65536;

    /// <summary>
    /// Approximate number of coalesced partitions per batch — used to scale
    /// the inflight-entry pool relative to the number of estimated batches.
    /// Also used by the producer to calculate pre-warm counts.
    /// </summary>
    internal const int InflightEntriesPerBatch = 32;

    internal readonly record struct ProducerPoolSizes
    {
        public required int ValueTaskSources { get; init; }
        public required int MaxRetainedBufferSize { get; init; }
        public required int InflightEntries { get; init; }
    }

    internal readonly record struct ConnectionPoolSizes
    {
        public required int PendingRequests { get; init; }
        public required int CancellationTokenSources { get; init; }
    }

    internal readonly record struct ConsumerPoolSizes
    {
        public required int FetchDataPool { get; init; }
        public required int CancellationTokenSources { get; init; }
    }

    internal static ProducerPoolSizes ForProducer(ulong bufferMemory, int batchSize)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");

        const ulong maxUsefulBatches = MaxValueTaskSources / EstimatedMessagesPerBatch; // 64
        var maxBatches = Math.Min(bufferMemory / (ulong)batchSize, maxUsefulBatches);
        var estimatedMessages = (int)(maxBatches * EstimatedMessagesPerBatch);

        return new ProducerPoolSizes
        {
            ValueTaskSources = Math.Clamp(estimatedMessages, MinValueTaskSources, MaxValueTaskSources),
            // Floor at 256KB to avoid ArrayPool thrash from frequent grow/shrink on modest workloads.
            MaxRetainedBufferSize = Math.Max(batchSize, 256 * 1024),
            InflightEntries = Math.Clamp((int)maxBatches * InflightEntriesPerBatch, 128, 2048),
        };
    }

    internal static ConnectionPoolSizes ForConnection(int maxInFlightRequestsPerConnection)
    {
        var pendingRequests = Math.Clamp(maxInFlightRequestsPerConnection * 4, 64, 1024);

        return new ConnectionPoolSizes
        {
            PendingRequests = pendingRequests,
            CancellationTokenSources = Math.Clamp(maxInFlightRequestsPerConnection * 8, 64, 2048),
        };
    }

    internal static ConsumerPoolSizes ForConsumer(int maxPartitionCount)
    {
        return new ConsumerPoolSizes
        {
            FetchDataPool = Math.Clamp(maxPartitionCount * 2, 32, 512),
            CancellationTokenSources = Math.Clamp(maxPartitionCount * 4, 64, 2048),
        };
    }

    /// <summary>
    /// Pool sizes that scale with the number of brokers sharing a global/shared pool.
    /// Called once during producer construction when the bootstrap server count is known.
    /// </summary>
    internal readonly record struct SharedPoolSizes
    {
        /// <summary>
        /// <c>maxArraysPerBucket</c> for <see cref="Producer.ProducerDataPool"/>.
        /// With N brokers, N sender threads concurrently rent/return arrays; the bucket
        /// must be deep enough to absorb cross-thread churn without pool misses.
        /// </summary>
        public required int ProducerDataArraysPerBucket { get; init; }

        /// <summary>
        /// <c>maxArraysPerBucket</c> for the shared <c>PipeMemoryPool</c> in ConnectionPool.
        /// Scales by total connections (brokers x connections-per-broker) since all
        /// connections share the same pool instance.
        /// </summary>
        public required int PipeMemoryArraysPerBucket { get; init; }

        /// <summary>
        /// Pool size for <c>ProduceResponsePool</c>. With N brokers each having up to
        /// <c>MaxInFlightRequestsPerConnection</c> pipelined requests, the pool must
        /// hold enough responses to avoid allocation under concurrent load.
        /// </summary>
        public required int ProduceResponsePoolSize { get; init; }
    }

    /// <summary>
    /// Computes pool sizes for resources shared across all brokers.
    /// </summary>
    /// <param name="brokerCount">Number of known brokers (from bootstrap servers or metadata).
    /// Clamped to [1, 16] to avoid pathological over-allocation.</param>
    /// <param name="connectionsPerBroker">Connections per broker (default 1).</param>
    /// <param name="maxInFlightRequestsPerConnection">Max pipelined requests per connection (default 5).</param>
    internal static SharedPoolSizes ForSharedPools(
        int brokerCount,
        int connectionsPerBroker = 1,
        int maxInFlightRequestsPerConnection = 5)
    {
        var clampedBrokers = Math.Clamp(brokerCount, 1, 16);
        var clampedConnections = Math.Max(1, connectionsPerBroker);
        var totalConnections = clampedBrokers * clampedConnections;

        // ProducerDataPool: 16 arrays per bucket per broker, capped at 128.
        // Each broker's sender thread rents/returns concurrently; insufficient depth
        // causes pool misses → new allocations → GC pressure.
        var producerDataArrays = Math.Clamp(clampedBrokers * 16, 16, 128);

        // PipeMemoryPool: 32 arrays per connection (covers pipelined segments),
        // scaled by total connections, capped at 256.
        var pipeMemoryArrays = Math.Clamp(totalConnections * 32, 32, 256);

        // ProduceResponsePool: one response per in-flight request per connection,
        // with 2x headroom for concurrent rent/return overlap.
        var responsePoolSize = Math.Clamp(
            totalConnections * maxInFlightRequestsPerConnection * 2,
            64, 512);

        return new SharedPoolSizes
        {
            ProducerDataArraysPerBucket = producerDataArrays,
            PipeMemoryArraysPerBucket = pipeMemoryArrays,
            ProduceResponsePoolSize = responsePoolSize,
        };
    }
}
