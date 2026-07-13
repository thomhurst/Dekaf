using System.Diagnostics;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public sealed class ProducerDeliveryDiagnosticsTests
{
    private static ProducerOptions CreateOptions(
        bool enableDeliveryDiagnostics = false,
        int deliveryLatencyTargetMs = 0) => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "diagnostics-test",
        BufferMemory = ulong.MaxValue,
        BatchSize = 60,
        LingerMs = 10_000,
        EnableDeliveryDiagnostics = enableDeliveryDiagnostics,
        DeliveryLatencyTargetMs = deliveryLatencyTargetMs
    };

    [Test]
    public async Task SnapshotDeliveryDiagnostics_DefaultDisabled_ReturnsDisabledSnapshot()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions());
        var batches = await AppendAndDrainBatchesAsync(accumulator);

        var snapshot = accumulator.GetDeliveryDiagnosticsSnapshot();

        await Assert.That(snapshot.DiagnosticsEnabled).IsFalse();
        await Assert.That(snapshot.InFlightBatchCount).IsEqualTo(0);
        await Assert.That(snapshot.Batches.Count).IsEqualTo(0);
        await Assert.That(batches[0].PipelineGeneration).IsEqualTo(0);

        CompleteAndReturn(accumulator, batches);
    }

    [Test]
    public async Task ConnectionScaleDiagnostics_Disabled_DoesNotRecordEvents()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions());

        accumulator.RecordConnectionScaleEvent(
            brokerId: 1,
            oldConnectionCount: 1,
            newConnectionCount: 2,
            bufferUtilization: 0.75,
            bufferPressureDelta: 120,
            sendLoopPressureDelta: 150);

        var snapshot = accumulator.GetDeliveryDiagnosticsSnapshot();

        await Assert.That(snapshot.ConnectionScaleEvents).IsEmpty();
    }

    [Test]
    public async Task ConnectionScaleDiagnostics_Enabled_CapturesOrderedEvents()
    {
        await using var accumulator = new RecordAccumulator(
            CreateOptions(enableDeliveryDiagnostics: true));

        accumulator.RecordConnectionScaleEvent(1, 1, 2, 0.75, 120, 150);
        accumulator.RecordConnectionScaleEvent(1, 2, 5, 0.82, 0, 340);

        var events = accumulator.GetDeliveryDiagnosticsSnapshot().ConnectionScaleEvents;

        await Assert.That(events.Count).IsEqualTo(2);
        await Assert.That(events[0].BrokerId).IsEqualTo(1);
        await Assert.That(events[0].OldConnectionCount).IsEqualTo(1);
        await Assert.That(events[0].NewConnectionCount).IsEqualTo(2);
        await Assert.That(events[0].Direction).IsEqualTo("up");
        await Assert.That(events[0].BufferUtilization).IsEqualTo(0.75);
        await Assert.That(events[0].BufferPressureDelta).IsEqualTo(120);
        await Assert.That(events[0].SendLoopPressureDelta).IsEqualTo(150);
        await Assert.That(events[1].OccurredAtUtc).IsGreaterThanOrEqualTo(events[0].OccurredAtUtc);
    }

    [Test]
    public async Task ConnectionScaleDiagnostics_PartitionLimit_RecordsCappedDirection()
    {
        await using var accumulator = new RecordAccumulator(
            CreateOptions(enableDeliveryDiagnostics: true));

        accumulator.RecordConnectionScaleEvent(1, 2, 2, 0.5, 0, 200, partitionLimited: true);

        var diagnostic = accumulator.GetDeliveryDiagnosticsSnapshot().ConnectionScaleEvents.Single();

        await Assert.That(diagnostic.Direction).IsEqualTo("capped");
        await Assert.That(diagnostic.PartitionLimited).IsTrue();
        await Assert.That(diagnostic.SendLoopPressureDelta).IsEqualTo(200);
    }

    [Test]
    public async Task ProduceRequestDiagnostics_CapturesCountRateAndCoalesceWidths()
    {
        await using var accumulator = new RecordAccumulator(
            CreateOptions(enableDeliveryDiagnostics: true));

        accumulator.RecordProduceRequest(coalesceWidth: 1);
        accumulator.RecordProduceRequest(coalesceWidth: 2);
        accumulator.RecordProduceRequest(coalesceWidth: 2);
        accumulator.RecordProduceRequest(coalesceWidth: 65);
        accumulator.RecordProduceRequest(coalesceWidth: 100);

        var snapshot = accumulator.GetDeliveryDiagnosticsSnapshot();

        await Assert.That(snapshot.ProduceRequestCount).IsEqualTo(5);
        await Assert.That(snapshot.ProduceRequestElapsedSeconds).IsGreaterThanOrEqualTo(0);
        await Assert.That(snapshot.ProduceRequestsPerSecond).IsGreaterThanOrEqualTo(0);
        await Assert.That(snapshot.CoalesceWidthHistogram.Single(bucket => bucket.MinimumWidth == 1).RequestCount)
            .IsEqualTo(1);
        await Assert.That(snapshot.CoalesceWidthHistogram.Single(bucket => bucket.MinimumWidth == 2).RequestCount)
            .IsEqualTo(2);
        var overflow = snapshot.CoalesceWidthHistogram.Single(bucket => bucket.MinimumWidth == 65);
        await Assert.That(overflow.MaximumWidth).IsNull();
        await Assert.That(overflow.RequestCount).IsEqualTo(2);
    }

    [Test]
    public async Task ProduceRequestDiagnostics_ResetStartsEmptyWindow()
    {
        await using var accumulator = new RecordAccumulator(
            CreateOptions(enableDeliveryDiagnostics: true));
        accumulator.RecordProduceRequest(coalesceWidth: 4);

        accumulator.ResetProduceRequestDiagnostics();
        var reset = accumulator.GetDeliveryDiagnosticsSnapshot();
        accumulator.RecordProduceRequest(coalesceWidth: 3);
        var afterRequest = accumulator.GetDeliveryDiagnosticsSnapshot();

        await Assert.That(reset.ProduceRequestCount).IsEqualTo(0);
        await Assert.That(reset.CoalesceWidthHistogram).IsEmpty();
        await Assert.That(afterRequest.ProduceRequestCount).IsEqualTo(1);
        await Assert.That(afterRequest.CoalesceWidthHistogram.Single().MinimumWidth).IsEqualTo(3);
    }

    [Test]
    public async Task ProduceRequestDiagnostics_Disabled_DoesNotRecord()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions());

        accumulator.RecordProduceRequest(coalesceWidth: 2);
        var snapshot = accumulator.GetDeliveryDiagnosticsSnapshot();

        await Assert.That(snapshot.ProduceRequestCount).IsEqualTo(0);
        await Assert.That(snapshot.CoalesceWidthHistogram).IsEmpty();
    }

    [Test]
    public async Task BrokerBudgetDiagnostics_CapturesCurrentStateAndPeriodicSample()
    {
        var options = CreateOptions(
            enableDeliveryDiagnostics: true,
            deliveryLatencyTargetMs: 10);
        await using var accumulator = new RecordAccumulator(
            options,
            resolveLeaderId: static (_, _) => 7);
        var budget = accumulator.GetBrokerUnackedBudget(7)!;
        budget.Charge(600);
        budget.RecordAdmissionBlock();
        var ackTimestamp = Stopwatch.GetTimestamp();
        budget.OnAcked(
            1_000,
            budget.SnapshotDelivery(ackTimestamp - Stopwatch.Frequency / 1_000, appLimited: true),
            ackTimestamp);
        budget.CompleteAckedPass(ackTimestamp);

        await accumulator.ExpireLingerAsync(CancellationToken.None);
        var snapshot = accumulator.GetDeliveryDiagnosticsSnapshot();
        var current = snapshot.BrokerBudgets.Single();
        var sample = snapshot.BrokerBudgetSamples.Single();

        await Assert.That(current.BrokerId).IsEqualTo(7);
        await Assert.That(current.BudgetBytes).IsGreaterThan(0);
        await Assert.That(current.UnackedBytes).IsEqualTo(600);
        await Assert.That(current.MinRttMicros).IsGreaterThan(0);
        await Assert.That(current.MaxRateBytesPerSec).IsGreaterThan(0);
        await Assert.That(current.AdmissionBlockCount).IsEqualTo(1);
        await Assert.That(current.CapacityProbeSuccessCount).IsEqualTo(0);
        await Assert.That(current.CapacityProbeFailureCount).IsEqualTo(0);
        await Assert.That(current.RequestSizeLog2Histogram).IsNotNull();
        await Assert.That(current.RequestSizeLog2Histogram!.Sum()).IsEqualTo(1);
        await Assert.That(current.RequestRttMicrosLog2Histogram).IsNotNull();
        await Assert.That(current.RequestRttMicrosLog2Histogram!.Sum()).IsEqualTo(1);
        await Assert.That(sample.BrokerId).IsEqualTo(7);
        await Assert.That(sample.CapacityProbeSuccessCount).IsEqualTo(0);
        await Assert.That(sample.CapacityProbeFailureCount).IsEqualTo(0);
        await Assert.That(sample.CapturedAtUtc).IsLessThanOrEqualTo(snapshot.CapturedAtUtc);
        await Assert.That(sample.RequestSizeLog2Histogram).IsNull()
            .Because("periodic samples omit histograms to keep the 4096-entry ring compact");
    }

    [Test]
    public async Task BrokerBudgetFloor_UsesMeasuredTimeBudget_NotFixedBatchBytes()
    {
        await using var accumulator = new RecordAccumulator(
            CreateOptions(deliveryLatencyTargetMs: 10),
            resolveLeaderId: static (_, _) => 7);
        var budget = accumulator.GetBrokerUnackedBudget(7)!;
        var now = Stopwatch.GetTimestamp();

        budget.OnAcked(
            100,
            budget.SnapshotDelivery(now - Stopwatch.Frequency / 1_000, appLimited: true),
            now);
        budget.CompleteAckedPass(now);

        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000)
            .Because("100 kB/s at a 10 ms target needs a 1 kB time-derived budget");
    }

    [Test]
    public async Task BrokerBudgetDiagnostics_RollingWindowKeepsNewest4096Samples()
    {
        var options = CreateOptions(
            enableDeliveryDiagnostics: true,
            deliveryLatencyTargetMs: 10);
        await using var accumulator = new RecordAccumulator(
            options,
            resolveLeaderId: static (_, _) => 7);
        var budget = accumulator.GetBrokerUnackedBudget(7)!;

        for (var i = 0; i < 4_097; i++)
        {
            budget.RecordAdmissionBlock();
            accumulator.RecordBrokerBudgetDiagnosticSample();
        }

        var samples = accumulator.GetDeliveryDiagnosticsSnapshot().BrokerBudgetSamples;

        await Assert.That(samples.Count).IsEqualTo(4_096);
        await Assert.That(samples[0].AdmissionBlockCount).IsEqualTo(2);
        await Assert.That(samples[^1].AdmissionBlockCount).IsEqualTo(4_097);
    }

    [Test]
    public async Task DeliveryDiagnostics_Disabled_DoesNotTrackTrace()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions());
        var batches = await AppendAndDrainBatchesAsync(accumulator);
        var batch = batches[0];

        await Assert.That(batch.PipelineGeneration).IsEqualTo(0);
        await Assert.That(batch.DiagTrace).IsEqualTo("");

        batch.CompleteSend(baseOffset: 42, DateTimeOffset.UtcNow);
        accumulator.OnBatchExitsPipeline(batch);

        await Assert.That(batch.DiagTrace).IsEqualTo("");

        accumulator.ReturnReadyBatch(batch);
        CompleteAndReturn(accumulator, batches, startIndex: 1);
    }

    [Test]
    public async Task SnapshotDeliveryDiagnostics_CapturesLiveInFlightBatch()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions(enableDeliveryDiagnostics: true));
        var batches = await AppendAndDrainBatchesAsync(accumulator);

        var snapshot = accumulator.GetDeliveryDiagnosticsSnapshot();
        var diagnostic = snapshot.Batches.First();

        await Assert.That(snapshot.DiagnosticsEnabled).IsTrue();
        await Assert.That(snapshot.InFlightBatchCount).IsEqualTo(snapshot.Batches.Count);
        await Assert.That(diagnostic.Topic).IsEqualTo("diagnostics-topic");
        await Assert.That(diagnostic.Partition).IsEqualTo(2);
        await Assert.That(diagnostic.RecordCount).IsGreaterThan(0);
        await Assert.That(diagnostic.DataSize).IsGreaterThan(0);
        await Assert.That(diagnostic.EncodedSize).IsGreaterThan(0);
        await Assert.That(diagnostic.PipelineGeneration).IsEqualTo(diagnostic.CurrentGeneration);
        await Assert.That(diagnostic.ReadyBatchId).IsNotEqualTo(0);
        await Assert.That(diagnostic.RecordBatchId).IsNotNull();
        await Assert.That(diagnostic.ArenaId).IsNotNull();
        await Assert.That(diagnostic.Trace).Contains("E");
        await Assert.That(diagnostic.LastTouchedBy).IsNotEqualTo("");
        await Assert.That(diagnostic.LifecycleState).IsNotEqualTo("");
        await Assert.That(diagnostic.IsStale).IsFalse();

        CompleteAndReturn(accumulator, batches);
    }

    [Test]
    public async Task SnapshotDeliveryDiagnostics_AfterBatchExitsPipeline_IsEmpty()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions(enableDeliveryDiagnostics: true));
        var batches = await AppendAndDrainBatchesAsync(accumulator);

        CompleteAndReturn(accumulator, batches);

        var snapshot = accumulator.GetDeliveryDiagnosticsSnapshot();

        await Assert.That(snapshot.DiagnosticsEnabled).IsTrue();
        await Assert.That(snapshot.InFlightBatchCount).IsEqualTo(0);
        await Assert.That(snapshot.Batches.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SnapshotDeliveryDiagnostics_ThroughProducerDiagnosticsInterface_UsesProducerAccumulator()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithDeliveryDiagnostics()
            .Build();

        var diagnostics = (IProducerDiagnostics)producer;

        var snapshot = diagnostics.GetDeliveryDiagnosticsSnapshot();

        await Assert.That(snapshot.DiagnosticsEnabled).IsTrue();
        await Assert.That(snapshot.InFlightBatchCount).IsEqualTo(0);
        await Assert.That(snapshot.Batches.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CreateDeliveryDiagnostic_WhenBatchCannotBePinned_ReturnsStaleDiagnostic()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions(enableDeliveryDiagnostics: true));
        var batches = await AppendAndDrainBatchesAsync(accumulator);
        var batch = batches[0];
        var generation = batch.Generation;
        var topicPartition = batch.TopicPartition;

        batch.CompleteSend(baseOffset: 42, DateTimeOffset.UtcNow);

        var diagnostic = batch.CreateDeliveryDiagnostic(generation, topicPartition);

        await Assert.That(diagnostic.IsStale).IsTrue();
        await Assert.That(diagnostic.LifecycleState).IsEqualTo("stale");
        await Assert.That(diagnostic.Topic).IsEqualTo(topicPartition.Topic);
        await Assert.That(diagnostic.Partition).IsEqualTo(topicPartition.Partition);

        accumulator.OnBatchExitsPipeline(batch);
        accumulator.ReturnReadyBatch(batch);
        CompleteAndReturn(accumulator, batches, startIndex: 1);
    }

    [Test]
    public async Task CreateDeliveryDiagnostic_LoopExitRedelivery_DescribesRecoverySource()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions(enableDeliveryDiagnostics: true));
        var batches = await AppendAndDrainBatchesAsync(accumulator);
        var batch = batches[0];
        batch.AppendDiag('Y');

        var diagnostic = batch.CreateDeliveryDiagnostic(batch.Generation, batch.TopicPartition);

        await Assert.That(diagnostic.LastTouchedBy).IsEqualTo("BrokerSender.LoopExitRedelivery");

        CompleteAndReturn(accumulator, batches);
    }

    [Test]
    public async Task DiagTrace_KeepsMostRecentTransitionsWhenCapacityIsExceeded()
    {
        var batch = new ReadyBatch();
        for (var i = 0; i < 39; i++)
            batch.AppendDiag('D');
        batch.AppendDiag('X');

        var trace = batch.DiagTrace;

        await Assert.That(trace.Length).IsEqualTo(32);
        await Assert.That(trace[^1]).IsEqualTo('X');
    }

    private static async Task<List<ReadyBatch>> AppendAndDrainBatchesAsync(RecordAccumulator accumulator)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < 3; i++)
        {
            var value = new byte[30];
            value[0] = (byte)i;

            var appended = await accumulator.AppendFromSpansAsync(
                "diagnostics-topic",
                partition: 2,
                timestamp + i,
                ReadOnlySpan<byte>.Empty,
                keyIsNull: true,
                value,
                valueIsNull: false,
                headers: null,
                headerCount: 0,
                callback: null,
                CancellationToken.None,
                partitionCount: 3);

            await Assert.That(appended).IsTrue();
        }

        var batches = new List<ReadyBatch>();
        while (accumulator.TryDrainBatch(out var batch))
            batches.Add(batch);

        await Assert.That(batches.Count).IsGreaterThan(0);
        return batches;
    }

    private static void CompleteAndReturn(RecordAccumulator accumulator, List<ReadyBatch> batches, int startIndex = 0)
    {
        for (var i = startIndex; i < batches.Count; i++)
        {
            var batch = batches[i];
            batch.CompleteSend(baseOffset: 42 + i, DateTimeOffset.UtcNow);
            accumulator.OnBatchExitsPipeline(batch);
            accumulator.ReturnReadyBatch(batch);
        }
    }
}
