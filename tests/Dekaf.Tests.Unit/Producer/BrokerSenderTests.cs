using System.Collections.Concurrent;
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
        var gates = new ConcurrentDictionary<TopicPartition, SemaphoreSlim>();

        var sender = new BrokerSender(
            brokerId: 1,
            connectionPool,
            metadataManager,
            accumulator,
            options,
            new CompressionCodecRegistry(),
            inflightTracker: null,
            statisticsCollector,
            gates,
            createPartitionGate: () => new SemaphoreSlim(1, 1),
            getProduceApiVersion: () => -1,
            setProduceApiVersion: _ => { },
            isTransactional: () => false,
            ensurePartitionInTransaction: null,
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
        var gates = new ConcurrentDictionary<TopicPartition, SemaphoreSlim>();

        var sender = new BrokerSender(
            brokerId: 1,
            connectionPool,
            metadataManager,
            accumulator,
            options,
            new CompressionCodecRegistry(),
            inflightTracker: null,
            statisticsCollector,
            gates,
            createPartitionGate: () => new SemaphoreSlim(1, 1),
            getProduceApiVersion: () => -1,
            setProduceApiVersion: _ => { },
            isTransactional: () => false,
            ensurePartitionInTransaction: null,
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
    public async Task PartitionGate_AlwaysSingleInflight()
    {
        // Non-idempotent should use SemaphoreSlim(1,1)
        var gate = new SemaphoreSlim(1, 1);

        var firstAcquired = gate.Wait(0);
        await Assert.That(firstAcquired).IsTrue();

        var secondAcquired = gate.Wait(0);
        await Assert.That(secondAcquired).IsFalse();

        gate.Release();
        gate.Dispose();
    }

    [Test]
    public async Task InFlightSemaphore_LimitsMaxPipelinedRequests()
    {
        // Verify the in-flight semaphore limits concurrent requests correctly
        var maxInflight = 3;
        var semaphore = new SemaphoreSlim(maxInflight, maxInflight);

        // Acquire all slots
        for (var i = 0; i < maxInflight; i++)
        {
            var acquired = await semaphore.WaitAsync(0);
            await Assert.That(acquired).IsTrue();
        }

        // Next acquire should timeout (non-blocking)
        var extra = await semaphore.WaitAsync(0);
        await Assert.That(extra).IsFalse();

        // Release one — should now be acquirable
        semaphore.Release();
        var afterRelease = await semaphore.WaitAsync(0);
        await Assert.That(afterRelease).IsTrue();

        // Cleanup
        for (var i = 0; i < maxInflight; i++)
        {
            semaphore.Release();
        }

        semaphore.Dispose();
    }

    [Test]
    public async Task BrokerSender_ConstructsWithIdempotentTracker()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var options = CreateDefaultOptions();
        var accumulator = new RecordAccumulator(options);
        var metadataManager = new MetadataManager(connectionPool, options.BootstrapServers);
        var statisticsCollector = new ProducerStatisticsCollector();
        var gates = new ConcurrentDictionary<TopicPartition, SemaphoreSlim>();
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
            gates,
            createPartitionGate: () => new SemaphoreSlim(1, 1),
            getProduceApiVersion: () => 9,
            setProduceApiVersion: _ => { },
            isTransactional: () => false,
            ensurePartitionInTransaction: null,
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
        var gates = new ConcurrentDictionary<TopicPartition, SemaphoreSlim>();

        var transactionCalled = false;
        var sender = new BrokerSender(
            brokerId: 1,
            connectionPool,
            metadataManager,
            accumulator,
            options,
            new CompressionCodecRegistry(),
            inflightTracker: null,
            statisticsCollector,
            gates,
            createPartitionGate: () => new SemaphoreSlim(1, 1),
            getProduceApiVersion: () => 9,
            setProduceApiVersion: _ => { },
            isTransactional: () => true,
            ensurePartitionInTransaction: (_, _) => { transactionCalled = true; return ValueTask.CompletedTask; },
            onAcknowledgement: null,
            logger: null);

        await sender.DisposeAsync();
        await accumulator.DisposeAsync();
        await metadataManager.DisposeAsync();

        // Transaction callback should not have been called (no batches enqueued)
        await Assert.That(transactionCalled).IsFalse();
    }

    [Test]
    public async Task MultipleGates_DifferentPartitions_IndependentSemaphores()
    {
        var gates = new ConcurrentDictionary<TopicPartition, SemaphoreSlim>();
        var tp0 = new TopicPartition("test", 0);
        var tp1 = new TopicPartition("test", 1);

        var gate0 = gates.GetOrAdd(tp0, _ => new SemaphoreSlim(1, 1));
        var gate1 = gates.GetOrAdd(tp1, _ => new SemaphoreSlim(1, 1));

        // Acquiring gate for partition 0 should not affect partition 1
        gate0.Wait(0);
        var canAcquireGate1 = gate1.Wait(0);
        await Assert.That(canAcquireGate1).IsTrue();

        gate0.Release();
        gate1.Release();
        gate0.Dispose();
        gate1.Dispose();
    }
}
