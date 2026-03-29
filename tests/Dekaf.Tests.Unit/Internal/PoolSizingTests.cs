using Dekaf.Internal;

namespace Dekaf.Tests.Unit.Internal;

public class PoolSizingTests
{
    // --- ForProducer tests ---

    [Test]
    public async Task ForProducer_DefaultConfig_ReturnsReasonableSizes()
    {
        // Default: 256MB buffer, 1MB batch
        var sizes = PoolSizing.ForProducer(bufferMemory: 256UL * 1024 * 1024, batchSize: 1024 * 1024);

        await Assert.That(sizes.CancellationTokenSources).IsGreaterThanOrEqualTo(256)
            .And.IsLessThanOrEqualTo(8192);
        await Assert.That(sizes.MaxRetainedBufferSize).IsEqualTo(1024 * 1024);
    }

    [Test]
    public async Task ForProducer_SmallBuffer_ClampsToMinimum()
    {
        var sizes = PoolSizing.ForProducer(bufferMemory: 1UL * 1024 * 1024, batchSize: 1024 * 1024);

        await Assert.That(sizes.CancellationTokenSources).IsGreaterThanOrEqualTo(256);
        await Assert.That(sizes.MaxRetainedBufferSize).IsEqualTo(1024 * 1024);
    }

    [Test]
    public async Task ForProducer_SmallBatch_ScalesRetainedBufferDown()
    {
        var sizes = PoolSizing.ForProducer(bufferMemory: 256UL * 1024 * 1024, batchSize: 256 * 1024);

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
}
