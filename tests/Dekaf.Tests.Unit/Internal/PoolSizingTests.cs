using Dekaf.Internal;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Internal;

public class PoolSizingTests
{
    // --- ForProducer tests ---

    [Test]
    public async Task ForProducer_DefaultConfig_ReturnsReasonableSizes()
    {
        // Default: 256MB buffer, 1MB batch
        var sizes = PoolSizing.ForProducer(bufferMemory: 256UL * 1024 * 1024, batchSize: 1024 * 1024);

        await Assert.That(sizes.MaxRetainedBufferSize).IsEqualTo(1024 * 1024);
        await Assert.That(sizes.InflightEntries).IsGreaterThanOrEqualTo(128);
    }

    [Test]
    public async Task ForProducer_SmallBuffer_ClampsToMinimum()
    {
        var sizes = PoolSizing.ForProducer(bufferMemory: 1UL * 1024 * 1024, batchSize: 1024 * 1024);

        await Assert.That(sizes.MaxRetainedBufferSize).IsEqualTo(1024 * 1024);
        // bufferDerived = 1*32 = 32, peak (default maxConns=10, maxInFlight=5) = 1600.
        // max(32, 1600) = 1600, well above the 128 floor.
        await Assert.That(sizes.InflightEntries).IsEqualTo(1600);
    }

    [Test]
    public async Task ForProducer_SmallBufferTinyConcurrency_ClampsToMinimum()
    {
        var sizes = PoolSizing.ForProducer(
            bufferMemory: 1UL * 1024 * 1024,
            batchSize: 1024 * 1024,
            maxInFlightRequestsPerConnection: 1,
            maxConnectionsPerBroker: 1);

        // peak = 1*1*32 = 32, bufferDerived = 32, max = 32, clamped up to floor 128.
        await Assert.That(sizes.InflightEntries).IsEqualTo(128);
    }

    [Test]
    public async Task ForProducer_SmallBatch_ClampsToMinRetainedBufferSize()
    {
        // batchSize (128KB) < floor (256KB), so MaxRetainedBufferSize should be clamped to 256KB
        var sizes = PoolSizing.ForProducer(bufferMemory: 256UL * 1024 * 1024, batchSize: 128 * 1024);

        await Assert.That(sizes.MaxRetainedBufferSize).IsEqualTo(256 * 1024);
    }

    [Test]
    public async Task ForProducer_LargeBatch_ScalesRetainedBufferUp()
    {
        var sizes = PoolSizing.ForProducer(bufferMemory: 256UL * 1024 * 1024, batchSize: 4 * 1024 * 1024);

        await Assert.That(sizes.MaxRetainedBufferSize).IsEqualTo(4 * 1024 * 1024);
    }

    [Test]
    public async Task ForProducer_ZeroBatchSize_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Task.FromResult(PoolSizing.ForProducer(bufferMemory: 256UL * 1024 * 1024, batchSize: 0)));
    }

    [Test]
    public async Task ForProducer_ValueTaskSources_MatchesExistingCalculation()
    {
        var sizes = PoolSizing.ForProducer(bufferMemory: 256UL * 1024 * 1024, batchSize: 1024 * 1024);

        // Original: (256MB / 1MB) * 1024 = 262144, clamped to [256, 65536] = 65536
        await Assert.That(sizes.ValueTaskSources).IsEqualTo(65536);
    }

    [Test]
    public async Task ForProducer_InflightEntries_ScalesWithBatches()
    {
        // Default maxConns=10, maxInFlight=5 → peak = 10*5*32 = 1600.
        // bufferDerived (256MB / 16KB clamped at 64 batches) * 32 = 2048. max(1600,2048) = 2048.
        var smallBatch = PoolSizing.ForProducer(bufferMemory: 256UL * 1024 * 1024, batchSize: 16 * 1024);
        await Assert.That(smallBatch.InflightEntries).IsEqualTo(2048);

        var largeBatch = PoolSizing.ForProducer(bufferMemory: 256UL * 1024 * 1024, batchSize: 1024 * 1024);
        await Assert.That(largeBatch.InflightEntries).IsGreaterThanOrEqualTo(128);
    }

    [Test]
    public async Task ForProducer_InflightEntries_ScalesWithMaxConnections()
    {
        // High maxConnectionsPerBroker → larger peak working set must be reflected in pool size.
        // Single-broker idempotent stress concentrates all in-flight on one BrokerSender,
        // so the cap must cover maxConns * maxInFlight * partitionsPerBatch even at high values.
        var sizes = PoolSizing.ForProducer(
            bufferMemory: 256UL * 1024 * 1024,
            batchSize: 1024 * 1024,
            maxInFlightRequestsPerConnection: 5,
            maxConnectionsPerBroker: 32);

        // peak = 32 * 5 * 32 = 5120, well above the legacy 2048 cap.
        await Assert.That(sizes.InflightEntries).IsGreaterThanOrEqualTo(5120);
    }

    [Test]
    public async Task ForProducer_InflightEntries_RespectsHardCeiling()
    {
        var sizes = PoolSizing.ForProducer(
            bufferMemory: 256UL * 1024 * 1024,
            batchSize: 1024 * 1024,
            maxInFlightRequestsPerConnection: 1000,
            maxConnectionsPerBroker: 1000);

        // Even pathological inputs should clamp to the hard ceiling (16384).
        await Assert.That(sizes.InflightEntries).IsLessThanOrEqualTo(16384);
    }

    // --- ForConnection tests ---

    [Test]
    public async Task ForConnection_DefaultMaxInFlight_ReturnsReasonableSize()
    {
        var sizes = PoolSizing.ForConnection(maxInFlightRequestsPerConnection: 5);

        await Assert.That(sizes.PendingRequests).IsGreaterThanOrEqualTo(64);
    }

    [Test]
    public async Task ForConnection_HighMaxInFlight_ScalesUp()
    {
        var sizes = PoolSizing.ForConnection(maxInFlightRequestsPerConnection: 100);

        await Assert.That(sizes.PendingRequests).IsEqualTo(400);
    }

    [Test]
    public async Task ForConnection_VeryHighMaxInFlight_ClampsToMax()
    {
        var sizes = PoolSizing.ForConnection(maxInFlightRequestsPerConnection: 500);

        await Assert.That(sizes.PendingRequests).IsLessThanOrEqualTo(1024);
    }

    [Test]
    public async Task ForConnection_CtsPool_ScalesWithInFlight()
    {
        var small = PoolSizing.ForConnection(maxInFlightRequestsPerConnection: 5);
        var large = PoolSizing.ForConnection(maxInFlightRequestsPerConnection: 100);

        await Assert.That(large.CancellationTokenSources)
            .IsGreaterThanOrEqualTo(small.CancellationTokenSources);
    }

    // --- ForConsumer tests ---

    [Test]
    public async Task ForConsumer_SmallPartitionCount_ClampsToMinimum()
    {
        var sizes = PoolSizing.ForConsumer(maxPartitionCount: 4);

        await Assert.That(sizes.FetchDataPool).IsGreaterThanOrEqualTo(32);
    }

    [Test]
    public async Task ForConsumer_LargePartitionCount_ScalesUp()
    {
        var sizes = PoolSizing.ForConsumer(maxPartitionCount: 200);

        await Assert.That(sizes.FetchDataPool).IsGreaterThan(128);
    }

    [Test]
    public async Task ForConsumer_VeryLargePartitionCount_ClampsToMax()
    {
        var sizes = PoolSizing.ForConsumer(maxPartitionCount: 10000);

        await Assert.That(sizes.FetchDataPool).IsLessThanOrEqualTo(512);
    }

    // --- ForSharedPools tests ---

    [Test]
    public async Task ForSharedPools_SingleBroker_DefaultBatch_ReturnsDepthForAdaptiveScaling()
    {
        var sizes = PoolSizing.ForSharedPools(brokerCount: 1);

        // Default: 1MB batch, maxConnections=10
        // estimatedMessagesPerBatch = clamp(1048576/256, 8, 512) = 512
        // peakInFlightBatches = 1 * 10 * 5 = 50
        // producerDataArrays = clamp(512 * 50, 64, 4096) = 4096 (capped)
        await Assert.That(sizes.ProducerDataArraysPerBucket).IsGreaterThanOrEqualTo(64);
        await Assert.That(sizes.ProducerDataArraysPerBucket).IsLessThanOrEqualTo(4096);
        // 1 broker * 1 conn * 32 = 32
        await Assert.That(sizes.PipeMemoryArraysPerBucket).IsEqualTo(32);
        // SerializationBuffers: 1 * 10 * 8 = 80
        await Assert.That(sizes.SerializationArraysPerBucket).IsGreaterThanOrEqualTo(16);
        // 1 * 1 * 5 * 2 = 10, clamped to min 64
        await Assert.That(sizes.ProduceResponsePoolSize).IsEqualTo(64);
    }

    [Test]
    public async Task ForSharedPools_SingleBroker_SmallBatch_ScalesForHighBatchChurn()
    {
        // 16KB batch with adaptive scaling to 10 connections
        var sizes = PoolSizing.ForSharedPools(brokerCount: 1, batchSize: 16384, maxConnectionsPerBroker: 10);

        // estimatedMessagesPerBatch = clamp(16384/256, 8, 512) = 64
        // peakInFlightBatches = 1 * 10 * 5 = 50
        // producerDataArrays = clamp(64 * 50, 64, 4096) = 3200
        await Assert.That(sizes.ProducerDataArraysPerBucket).IsGreaterThanOrEqualTo(64);
        await Assert.That(sizes.ProducerDataArraysPerBucket).IsLessThanOrEqualTo(4096);
    }

    [Test]
    public async Task ForSharedPools_SingleBroker_NoAdaptiveScaling_SmallerPool()
    {
        // Single connection, no adaptive scaling
        var sizes = PoolSizing.ForSharedPools(brokerCount: 1, batchSize: 16384, maxConnectionsPerBroker: 1);

        // estimatedMessagesPerBatch = 64, peakInFlightBatches = 1 * 1 * 5 = 5
        // producerDataArrays = clamp(64 * 5, 64, 4096) = 320
        await Assert.That(sizes.ProducerDataArraysPerBucket).IsGreaterThanOrEqualTo(64);
        await Assert.That(sizes.ProducerDataArraysPerBucket).IsLessThanOrEqualTo(512);
    }

    [Test]
    public async Task ForSharedPools_ThreeBrokers_ScalesUp()
    {
        var sizes = PoolSizing.ForSharedPools(brokerCount: 3);

        // More brokers = deeper pool
        var singleBroker = PoolSizing.ForSharedPools(brokerCount: 1);
        await Assert.That(sizes.ProducerDataArraysPerBucket)
            .IsGreaterThanOrEqualTo(singleBroker.ProducerDataArraysPerBucket);
        // 3 * 1 * 32 = 96
        await Assert.That(sizes.PipeMemoryArraysPerBucket).IsEqualTo(96);
        // 3 * 1 * 5 * 2 = 30, clamped to min 64
        await Assert.That(sizes.ProduceResponsePoolSize).IsEqualTo(64);
    }

    [Test]
    public async Task ForSharedPools_ThreeBrokers_MultipleConnections_ScalesFurther()
    {
        var sizes = PoolSizing.ForSharedPools(brokerCount: 3, connectionsPerBroker: 3);

        // 3 * 3 * 32 = 288, capped at 256
        await Assert.That(sizes.PipeMemoryArraysPerBucket).IsEqualTo(256);
        // 3 * 3 * 5 * 2 = 90
        await Assert.That(sizes.ProduceResponsePoolSize).IsEqualTo(90);
    }

    [Test]
    public async Task ForSharedPools_ManyBrokers_ClampsToMax()
    {
        var sizes = PoolSizing.ForSharedPools(brokerCount: 20, connectionsPerBroker: 10);

        await Assert.That(sizes.ProducerDataArraysPerBucket).IsLessThanOrEqualTo(4096);
        // 16 * 10 * 32 = 5120, capped at 256
        await Assert.That(sizes.PipeMemoryArraysPerBucket).IsLessThanOrEqualTo(256);
        // SerializationBuffers capped at 256
        await Assert.That(sizes.SerializationArraysPerBucket).IsLessThanOrEqualTo(256);
        // 16 * 10 * 5 * 2 = 1600, capped at 512
        await Assert.That(sizes.ProduceResponsePoolSize).IsLessThanOrEqualTo(512);
    }

    [Test]
    public async Task ForSharedPools_ZeroBrokerCount_ClampsToOne()
    {
        var sizes = PoolSizing.ForSharedPools(brokerCount: 0);

        await Assert.That(sizes.ProducerDataArraysPerBucket).IsGreaterThanOrEqualTo(64);
        await Assert.That(sizes.PipeMemoryArraysPerBucket).IsEqualTo(32);
    }

    [Test]
    public async Task ForSharedPools_SerializationBuffers_ScalesWithMaxConnections()
    {
        var smallConns = PoolSizing.ForSharedPools(brokerCount: 1, maxConnectionsPerBroker: 1);
        var largeConns = PoolSizing.ForSharedPools(brokerCount: 1, maxConnectionsPerBroker: 10);

        await Assert.That(largeConns.SerializationArraysPerBucket)
            .IsGreaterThan(smallConns.SerializationArraysPerBucket);
    }

    [Test]
    [NotInParallel("ProducerDataPool")]
    public async Task RatchetBucketCapacity_SameOrSmallerSize_DoesNotReplacePool()
    {
        // Ratchet up to 48 (3 brokers)
        ProducerDataPool.RatchetBucketCapacity(48);
        var poolAfterFirst = ProducerDataPool.BytePool;

        // Same size — should be a no-op
        ProducerDataPool.RatchetBucketCapacity(48);
        var poolAfterSame = ProducerDataPool.BytePool;

        await Assert.That(poolAfterSame).IsSameReferenceAs(poolAfterFirst);

        // Smaller size — should also be a no-op (ratchet only goes up)
        ProducerDataPool.RatchetBucketCapacity(16);
        var poolAfterSmaller = ProducerDataPool.BytePool;

        await Assert.That(poolAfterSmaller).IsSameReferenceAs(poolAfterFirst);
    }

    [Test]
    [NotInParallel("DekafPools")]
    public async Task SerializationBuffers_RatchetUp_ReplacesPool()
    {
        // This test must use the HIGHEST values across all DekafPools tests
        // to guarantee replacement regardless of execution order (ratchet is monotonic).
        DekafPools.RatchetSerializationBucketCapacity(8192);
        var poolBefore = DekafPools.SerializationBuffers;

        DekafPools.RatchetSerializationBucketCapacity(16384);
        var poolAfter = DekafPools.SerializationBuffers;
        await Assert.That(poolAfter).IsNotSameReferenceAs(poolBefore);

        // Same value — should be a no-op (pool reference unchanged)
        DekafPools.RatchetSerializationBucketCapacity(16384);
        var afterSame = DekafPools.SerializationBuffers;
        await Assert.That(afterSame).IsSameReferenceAs(poolAfter);
    }

    [Test]
    [NotInParallel("DekafPools")]
    public async Task SerializationBuffers_RatchetDown_DoesNotShrink()
    {
        // Uses values BELOW RatchetUp's range so this test works in any order.
        // If RatchetUp already ran (pool at 16384), ratchet(4096) is a no-op — fine,
        // we capture the current pool and verify ratchet(16) doesn't change it.
        DekafPools.RatchetSerializationBucketCapacity(4096);
        var afterHigh = DekafPools.SerializationBuffers;

        DekafPools.RatchetSerializationBucketCapacity(16);
        var afterSmaller = DekafPools.SerializationBuffers;

        await Assert.That(afterSmaller).IsSameReferenceAs(afterHigh);
    }
}
