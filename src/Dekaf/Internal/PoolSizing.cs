namespace Dekaf.Internal;

/// <summary>
/// Centralizes derivation of internal pool sizes from user-facing configuration.
/// All pool sizing is computed once at construction from <c>BufferMemory</c>,
/// <c>BatchSize</c>, <c>MaxInFlightRequestsPerConnection</c>, etc.
/// No new public config knobs — users express workload intent through existing settings.
/// </summary>
internal static class PoolSizing
{
    /// <summary>
    /// Serialization buffer arrays per connection. Each RentedBufferWriter growth step
    /// (4KB → doubling → final size) rents/returns through the pool; 8 covers the
    /// typical growth chain with headroom for concurrent rent/return overlap.
    /// </summary>
    internal const int SerializationArraysPerConnection = 8;

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
        public required int PendingAppends { get; init; }
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

    internal static ProducerPoolSizes ForProducer(
        ulong bufferMemory,
        int batchSize,
        int maxInFlightRequestsPerConnection = 5,
        int maxConnectionsPerBroker = 10)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");

        const ulong maxUsefulBatches = MaxValueTaskSources / EstimatedMessagesPerBatch; // 64
        var maxBatches = Math.Min(bufferMemory / (ulong)batchSize, maxUsefulBatches);
        var estimatedMessages = (int)(maxBatches * EstimatedMessagesPerBatch);

        // InflightEntries must cover the worst-case concurrent in-flight working set.
        // The pool is shared across all BrokerSenders in the producer, but in single-broker
        // topologies all traffic concentrates on one sender — so the cap cannot be reduced
        // by broker count. Worst case ≈ maxConnections × maxInFlight × InflightEntriesPerBatch
        // (one entry per partition coalesced into each in-flight produce request).
        // If the pool is too small, Rent() falls through to `new InflightEntry()` and Return()
        // drops on full, producing a steady stream of mid-lived (Gen0→Gen2 promoted) garbage
        // — which is the failure mode previously observed in single-broker idempotent stress.
        var clampedMaxConns = Math.Max(1, maxConnectionsPerBroker);
        var clampedMaxInFlight = Math.Max(1, maxInFlightRequestsPerConnection);
        // Compute in long to make the final Math.Clamp ceiling unconditional — pathological
        // inputs (e.g. misconfigured maxConnectionsPerBroker) would otherwise overflow int
        // and produce a negative intermediate that clamps to the floor instead of the ceiling.
        var peakInflightEntries = (long)clampedMaxConns * clampedMaxInFlight * InflightEntriesPerBatch;
        var bufferDerivedEntries = (long)maxBatches * InflightEntriesPerBatch;
        var inflightEntries = Math.Max(peakInflightEntries, bufferDerivedEntries);

        var pendingAppends = Math.Clamp(clampedMaxConns * clampedMaxInFlight * 4, 64, 1024);

        return new ProducerPoolSizes
        {
            ValueTaskSources = Math.Clamp(estimatedMessages, MinValueTaskSources, MaxValueTaskSources),
            // Floor at 256KB to avoid ArrayPool thrash from frequent grow/shrink on modest workloads.
            MaxRetainedBufferSize = Math.Max(batchSize, 256 * 1024),
            InflightEntries = (int)Math.Clamp(inflightEntries, 128L, 16384L),
            PendingAppends = pendingAppends,
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
        /// Depth covers in-flight PooledMemory arrays on the cold path (buffer-full scenario).
        /// Scales with batch size, max connections (adaptive scaling), and in-flight depth.
        /// </summary>
        public required int ProducerDataArraysPerBucket { get; init; }

        /// <summary>
        /// <c>maxArraysPerBucket</c> for the shared <c>PipeMemoryPool</c> in ConnectionPool.
        /// Scales by total connections (brokers x connections-per-broker) since all
        /// connections share the same pool instance.
        /// </summary>
        public required int PipeMemoryArraysPerBucket { get; init; }

        /// <summary>
        /// <c>maxArraysPerBucket</c> for <see cref="DekafPools.SerializationBuffers"/>.
        /// Covers concurrent request serialization across all connections. Each
        /// RentedBufferWriter growth step rents/returns through this pool.
        /// </summary>
        public required int SerializationArraysPerBucket { get; init; }

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
    /// <param name="batchSize">Producer batch size in bytes. Smaller batches create higher batch churn
    /// and more concurrent in-flight arrays, requiring deeper pool buckets.</param>
    /// <param name="maxConnectionsPerBroker">Maximum connections per broker when adaptive scaling is enabled.
    /// Pool depth must cover peak concurrency, not just initial connections.</param>
    internal static SharedPoolSizes ForSharedPools(
        int brokerCount,
        int connectionsPerBroker = 1,
        int maxInFlightRequestsPerConnection = 5,
        int batchSize = 1048576,
        int maxConnectionsPerBroker = 10)
    {
        var clampedBrokers = Math.Clamp(brokerCount, 1, 16);
        var clampedConnections = Math.Max(1, connectionsPerBroker);
        var clampedMaxConnections = Math.Max(clampedConnections, maxConnectionsPerBroker);
        var totalConnections = clampedBrokers * clampedConnections;
        var totalMaxConnections = clampedBrokers * clampedMaxConnections;

        // ProducerDataPool: depth must cover in-flight PooledMemory arrays on the cold path.
        // When buffer pressure forces messages through AppendFromSpansAsync's cold path,
        // each message rents key+value arrays that are held until batch cleanup.
        // Arrays in-flight ≈ messagesPerBatch × inFlightBatches × connections.
        // Use maxConnectionsPerBroker (not current) since adaptive scaling can ramp up.
        var clampedBatchSize = Math.Clamp(batchSize, 1024, 4 * 1024 * 1024);
        var estimatedMessagesPerBatch = Math.Clamp(clampedBatchSize / 256, 8, 512);
        var peakInFlightBatches = totalMaxConnections * maxInFlightRequestsPerConnection;
        var producerDataArrays = Math.Clamp(estimatedMessagesPerBatch * peakInFlightBatches, 64, 4096);

        // PipeMemoryPool: 32 arrays per connection (covers pipelined segments),
        // scaled by total connections, capped at 256.
        var pipeMemoryArrays = Math.Clamp(totalConnections * 32, 32, 256);

        // SerializationBuffers: covers concurrent request serialization across all connections.
        // Each RentedBufferWriter growth step rents/returns arrays; with multiple connections
        // serializing concurrently, the pool needs depth proportional to peak connections.
        var serializationArrays = Math.Clamp(
            totalMaxConnections * SerializationArraysPerConnection, 16, 256);

        // ProduceResponsePool: one response per in-flight request per connection,
        // with 2x headroom for concurrent rent/return overlap.
        var responsePoolSize = Math.Clamp(
            totalConnections * maxInFlightRequestsPerConnection * 2,
            64, 512);

        return new SharedPoolSizes
        {
            ProducerDataArraysPerBucket = producerDataArrays,
            PipeMemoryArraysPerBucket = pipeMemoryArrays,
            SerializationArraysPerBucket = serializationArrays,
            ProduceResponsePoolSize = responsePoolSize,
        };
    }
}
