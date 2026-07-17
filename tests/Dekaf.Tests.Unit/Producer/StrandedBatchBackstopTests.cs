using System.Collections.Concurrent;
using System.Diagnostics;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for the Ready() lost-notification backstop: a sealed batch whose
/// _readyPartitions publish was lost must be re-discovered by rescanning the partition
/// deques after a sustained idle window, instead of stranding until close
/// (release-gate run 29612262949: a first-wave batch sat invisible for the full
/// MaxBlockMs against a healthy broker).
/// </summary>
public class StrandedBatchBackstopTests
{
    private const string Topic = "backstop-topic";
    private static readonly TopicPartition Partition = new(Topic, 0);

    private static ProducerOptions CreateTestOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "test-producer",
        BufferMemory = ulong.MaxValue, // Disable buffer limit for unit tests (no producer to drain)
        BatchSize = 1000,
        LingerMs = 10
    };

    private static async Task AppendOneRecordAsync(RecordAccumulator accumulator)
    {
        var appended = await AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, Topic);
        await Assert.That(appended).IsTrue();
    }

    /// <summary>
    /// Simulates a lost publish: seals the current batch (which enqueues it and publishes
    /// its ready notification) and then discards every pending notification.
    /// </summary>
    private static async Task SealAndDropNotificationsAsync(RecordAccumulator accumulator)
    {
        await AccumulatorTestHelpers.SealAllAsync(accumulator);

        var readyPartitions = AccumulatorTestHelpers.GetPrivateField<ConcurrentQueue<TopicPartition>>(
            accumulator, "_readyPartitions");
        while (readyPartitions.TryDequeue(out _))
        {
        }
    }

    private static HashSet<int> RunReadyPass(RecordAccumulator accumulator, MetadataManager metadataManager)
    {
        var readyNodes = new HashSet<int>();
        _ = accumulator.Ready(metadataManager, readyNodes);
        return readyNodes;
    }

    private static long DispatchQueuedCount(RecordAccumulator accumulator)
        => accumulator.DispatchQueuedBatchCount;

    [Test]
    public async Task Ready_LostNotification_BackstopRepublishesAfterIdleWindow()
    {
        var capturingLogger = new CapturingLogger();
        await using var accumulator = new RecordAccumulator(CreateTestOptions(), logger: capturingLogger);
        await using var metadataManager = AccumulatorTestHelpers.CreateMetadataManager(Topic, partitionCount: 1);

        await AppendOneRecordAsync(accumulator);
        await SealAndDropNotificationsAsync(accumulator);
        await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(1);
        await Assert.That(DispatchQueuedCount(accumulator)).IsEqualTo(1);

        // Pass 1: idle — arms the idle window; no recovery yet.
        await Assert.That(RunReadyPass(accumulator, metadataManager)).IsEmpty();
        await Assert.That(capturingLogger.Messages.Any(m => m.Level == LogLevel.Warning)).IsFalse();

        // Pass 2 after the idle window has elapsed: rescan re-publishes the stranded
        // partition and emits the tripwire Warning.
        AccumulatorTestHelpers.SetPrivateField(
            accumulator, "_dispatchIdleSinceTimestamp", Stopwatch.GetTimestamp() - Stopwatch.Frequency);
        await Assert.That(RunReadyPass(accumulator, metadataManager)).IsEmpty();
        await Assert.That(capturingLogger.Messages.Any(m =>
                m.Level == LogLevel.Warning && m.Message.Contains("Recovered stranded sealed batch")))
            .IsTrue();

        // Pass 3: the re-published notification makes the partition ready.
        await Assert.That(RunReadyPass(accumulator, metadataManager)).Contains(1);
    }

    [Test]
    public async Task Ready_NotificationIntact_FirstPassIsReady_NoTripwire()
    {
        var capturingLogger = new CapturingLogger();
        await using var accumulator = new RecordAccumulator(CreateTestOptions(), logger: capturingLogger);
        await using var metadataManager = AccumulatorTestHelpers.CreateMetadataManager(Topic, partitionCount: 1);

        await AppendOneRecordAsync(accumulator);
        await AccumulatorTestHelpers.SealAllAsync(accumulator);

        await Assert.That(RunReadyPass(accumulator, metadataManager)).Contains(1);
        await Assert.That(capturingLogger.Messages.Any(m => m.Level == LogLevel.Warning)).IsFalse();
    }

    [Test]
    public async Task Ready_MutedPartition_BackstopDefersToUnmute()
    {
        var capturingLogger = new CapturingLogger();
        await using var accumulator = new RecordAccumulator(CreateTestOptions(), logger: capturingLogger);
        await using var metadataManager = AccumulatorTestHelpers.CreateMetadataManager(Topic, partitionCount: 1);

        await AppendOneRecordAsync(accumulator);
        await SealAndDropNotificationsAsync(accumulator);
        accumulator.MutePartition(Partition);

        // Muted heads cannot be dispatched; the backstop must neither re-publish nor log,
        // no matter how long the idle window lasts.
        for (var pass = 0; pass < 4; pass++)
        {
            await Assert.That(RunReadyPass(accumulator, metadataManager)).IsEmpty();
            AccumulatorTestHelpers.SetPrivateField(
                accumulator, "_dispatchIdleSinceTimestamp", Stopwatch.GetTimestamp() - Stopwatch.Frequency);
        }
        await Assert.That(capturingLogger.Messages.Any(m => m.Level == LogLevel.Warning)).IsFalse();

        // Unmute re-publishes the partition itself (existing contract).
        accumulator.UnmutePartition(Partition);
        await Assert.That(RunReadyPass(accumulator, metadataManager)).Contains(1);
    }

    [Test]
    public async Task Ready_EmptyPipeline_IdlePassesNeverScanOrLog()
    {
        var capturingLogger = new CapturingLogger();
        await using var accumulator = new RecordAccumulator(CreateTestOptions(), logger: capturingLogger);
        await using var metadataManager = AccumulatorTestHelpers.CreateMetadataManager(Topic, partitionCount: 1);

        for (var pass = 0; pass < 5; pass++)
            await Assert.That(RunReadyPass(accumulator, metadataManager)).IsEmpty();

        await Assert.That(capturingLogger.Messages).IsEmpty();
        await Assert.That(DispatchQueuedCount(accumulator)).IsEqualTo(0);
    }

    [Test]
    public async Task DispatchQueuedCount_ReturnsToZero_AfterDisposeDrainsDeques()
    {
        var accumulator = new RecordAccumulator(CreateTestOptions());

        await AppendOneRecordAsync(accumulator);
        await AccumulatorTestHelpers.SealAllAsync(accumulator);
        await Assert.That(DispatchQueuedCount(accumulator)).IsEqualTo(1);

        await accumulator.DisposeAsync();
        await Assert.That(DispatchQueuedCount(accumulator)).IsEqualTo(0);
    }
}
