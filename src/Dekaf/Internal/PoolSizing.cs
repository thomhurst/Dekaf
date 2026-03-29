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
    /// </summary>
    private const int InflightEntriesPerBatch = 32;

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
}
