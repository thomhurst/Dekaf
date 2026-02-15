using Dekaf.Compression;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Statistics;
using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Unit tests for the BrokerSender per-broker sender thread.
/// Since BrokerSender requires many dependencies for a full integration test,
/// these tests focus on the construction, enqueue, and disposal lifecycle.
/// Full end-to-end behavior is covered by integration tests.
/// </summary>
public sealed class BrokerSenderTests
{
    private static ProducerOptions CreateDefaultOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        MaxInFlightRequestsPerConnection = 5,
        Acks = Acks.All,
        DeliveryTimeoutMs = 5000,
        RetryBackoffMs = 100,
        RetryBackoffMaxMs = 1000
    };

    [Test]
    public async Task Disposal_Graceful_CompletesWithoutError()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var options = CreateDefaultOptions();
        var accumulator = new RecordAccumulator(options);
        var metadataManager = new MetadataManager(connectionPool, options.BootstrapServers);
        var statisticsCollector = new ProducerStatisticsCollector();

        var sender = new BrokerSender(
            brokerId: 1,
            connectionPool,
            metadataManager,
            accumulator,
            options,
            new CompressionCodecRegistry(),
            inflightTracker: new PartitionInflightTracker(),
            statisticsCollector,
            getProduceApiVersion: () => -1,
            setProduceApiVersion: _ => { },
            isTransactional: () => false,
            ensurePartitionInTransaction: null,
            bumpEpoch: null,
            getCurrentEpoch: null,
            rerouteBatch: null,
            onAcknowledgement: null,
            logger: null);

        // Should dispose gracefully without throwing
        await sender.DisposeAsync();

        // Dispose idempotent
        await sender.DisposeAsync();

        await accumulator.DisposeAsync();
        await metadataManager.DisposeAsync();
    }

    [Test]
    public async Task Disposal_AfterChannelComplete_NoDeadlock()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var options = CreateDefaultOptions();
        var accumulator = new RecordAccumulator(options);
        var metadataManager = new MetadataManager(connectionPool, options.BootstrapServers);
        var statisticsCollector = new ProducerStatisticsCollector();

        var sender = new BrokerSender(
            brokerId: 1,
            connectionPool,
            metadataManager,
            accumulator,
            options,
            new CompressionCodecRegistry(),
            inflightTracker: new PartitionInflightTracker(),
            statisticsCollector,
            getProduceApiVersion: () => -1,
            setProduceApiVersion: _ => { },
            isTransactional: () => false,
            ensurePartitionInTransaction: null,
            bumpEpoch: null,
            getCurrentEpoch: null,
            rerouteBatch: null,
            onAcknowledgement: null,
            logger: null);

        // Give the send loop time to start
        await Task.Delay(10);

        // Dispose should complete within timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var disposeTask = sender.DisposeAsync().AsTask();
        await disposeTask.WaitAsync(cts.Token);

        await accumulator.DisposeAsync();
        await metadataManager.DisposeAsync();
    }

    [Test]
    public async Task BrokerSender_ConstructsWithIdempotentTracker()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var options = CreateDefaultOptions();
        var accumulator = new RecordAccumulator(options);
        var metadataManager = new MetadataManager(connectionPool, options.BootstrapServers);
        var statisticsCollector = new ProducerStatisticsCollector();
        var tracker = new PartitionInflightTracker();

        var sender = new BrokerSender(
            brokerId: 1,
            connectionPool,
            metadataManager,
            accumulator,
            options,
            new CompressionCodecRegistry(),
            inflightTracker: tracker,
            statisticsCollector,
            getProduceApiVersion: () => 9,
            setProduceApiVersion: _ => { },
            isTransactional: () => false,
            ensurePartitionInTransaction: null,
            bumpEpoch: null,
            getCurrentEpoch: null,
            rerouteBatch: null,
            onAcknowledgement: null,
            logger: null);

        await sender.DisposeAsync();
        await accumulator.DisposeAsync();
        await metadataManager.DisposeAsync();
    }

    [Test]
    public async Task BrokerSender_ConstructsWithTransactionalCallbacks()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var options = CreateDefaultOptions();
        var accumulator = new RecordAccumulator(options);
        var metadataManager = new MetadataManager(connectionPool, options.BootstrapServers);
        var statisticsCollector = new ProducerStatisticsCollector();

        var transactionCalled = false;
        var sender = new BrokerSender(
            brokerId: 1,
            connectionPool,
            metadataManager,
            accumulator,
            options,
            new CompressionCodecRegistry(),
            inflightTracker: new PartitionInflightTracker(),
            statisticsCollector,
            getProduceApiVersion: () => 9,
            setProduceApiVersion: _ => { },
            isTransactional: () => true,
            ensurePartitionInTransaction: (_, _) => { transactionCalled = true; return ValueTask.CompletedTask; },
            bumpEpoch: null,
            getCurrentEpoch: null,
            rerouteBatch: null,
            onAcknowledgement: null,
            logger: null);

        await sender.DisposeAsync();
        await accumulator.DisposeAsync();
        await metadataManager.DisposeAsync();

        // Transaction callback should not have been called (no batches enqueued)
        await Assert.That(transactionCalled).IsFalse();
    }
}
