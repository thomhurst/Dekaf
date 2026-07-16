using System.Diagnostics;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Deterministic tests for the atomic admission gate and knee-seeking broker window.
/// All controller time is explicit; no test sleeps or depends on wall-clock scheduling.
/// </summary>
public sealed class BrokerUnackedByteBudgetTests
{
    private static readonly long T0 = Stopwatch.Frequency;

    private static long Seconds(double seconds) => (long)(seconds * Stopwatch.Frequency);

    [Test]
    public async Task InitialWindow_IsFourRequestQuanta()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);

        await Assert.That(budget.BudgetBytes).IsEqualTo(400);
        await Assert.That(budget.Phase).IsEqualTo(BrokerWindowPhase.Startup);
    }

    [Test]
    public async Task TryReserve_AtomicallyEnforcesWindow()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);

        await Assert.That(budget.TryReserve(250, out var firstGeneration)).IsTrue();
        await Assert.That(budget.TryReserve(150, out var secondGeneration)).IsTrue();
        await Assert.That(budget.TryReserve(1, out _)).IsFalse();
        await Assert.That(budget.UnackedBytes).IsEqualTo(400);
        await Assert.That(firstGeneration).IsEqualTo(budget.CurrentGeneration);
        await Assert.That(secondGeneration).IsEqualTo(budget.CurrentGeneration);

        budget.Release(400);
        await Assert.That(budget.UnackedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task TryReserve_AllowsOneOversizedRequestOnlyWhenEmpty()
    {
        var budget = CreateBudget(capBytes: 400, initialRequestBytes: 100);

        await Assert.That(budget.TryReserve(500, out _)).IsTrue();
        await Assert.That(budget.TryReserve(1, out _)).IsFalse();

        budget.Release(500);
        await Assert.That(budget.TryReserve(500, out _)).IsTrue();
        budget.Release(500);
    }

    [Test]
    public async Task ConcurrentReservations_CannotOvershootWindow()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        var successes = 0;

        Parallel.For(0, 256, _ =>
        {
            if (budget.TryReserve(10, out _))
                Interlocked.Increment(ref successes);
        });

        await Assert.That(successes).IsEqualTo(40);
        await Assert.That(budget.UnackedBytes).IsEqualTo(budget.BudgetBytes);
        budget.Release(successes * 10L);
    }

    [Test]
    public async Task SetCap_ClampsWindowAndAdvancesGeneration()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        var generation = budget.CurrentGeneration;

        budget.SetCap(250, T0);

        await Assert.That(budget.BudgetBytes).IsEqualTo(250);
        await Assert.That(budget.CurrentGeneration).IsGreaterThan(generation);
    }

    [Test]
    public async Task Startup_GrowsWindowWhenDemandAndGoodputExist()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        budget.CompleteAckedPass(T0);

        DriveBudgetEpoch(budget, T0 + Seconds(0.110), logicalBytes: 100, rttSeconds: 0.001);

        await Assert.That(budget.BudgetBytes).IsEqualTo(800);
        await Assert.That(budget.MaxRateBytesPerSecond).IsGreaterThan(0);
    }

    [Test]
    public async Task StaleGenerationFeedback_DoesNotMoveWindow()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        var initialGeneration = budget.CurrentGeneration;
        budget.CompleteAckedPass(T0);
        DriveBudgetEpoch(budget, T0 + Seconds(0.110), 100, 0.001);
        var grownWindow = budget.BudgetBytes;

        DriveBudgetEpoch(
            budget,
            T0 + Seconds(0.220),
            100,
            0.001,
            admissionGeneration: initialGeneration);

        await Assert.That(budget.BudgetBytes).IsEqualTo(grownWindow);
    }

    [Test]
    public async Task PersistentQueueDelay_EntersRecoveryAndCutsWindow()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        budget.CompleteAckedPass(T0);
        DriveBudgetEpoch(budget, T0 + Seconds(0.110), 100, 0.001);
        var grownWindow = budget.BudgetBytes;

        DriveBudgetEpoch(budget, T0 + Seconds(0.220), 100, 0.030);

        await Assert.That(budget.Phase).IsEqualTo(BrokerWindowPhase.Recovery);
        await Assert.That(budget.BudgetBytes).IsLessThan(grownWindow);
        await Assert.That(budget.DeliveryLatencyEwmaMicros).IsGreaterThan(0);
    }

    [Test]
    public async Task DownProbe_KeepsSmallerWindowWhenGoodputIsPreserved()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);
        ReachSteady(controller, ref now, ref admissionBlocks);

        var baselineWindow = controller.WindowBytes;
        BrokerWindowDecision decision = default;
        for (var i = 0; i < 20 && controller.Phase != BrokerWindowPhase.ProbeDown; i++)
            decision = DriveControllerEpoch(controller, ref now, ref admissionBlocks);

        await Assert.That(controller.Phase).IsEqualTo(BrokerWindowPhase.ProbeDown);
        await Assert.That(controller.WindowBytes).IsLessThan(baselineWindow);
        var probedWindow = controller.WindowBytes;

        decision = DriveControllerEpoch(controller, ref now, ref admissionBlocks);

        await Assert.That(decision.ProbeOutcome).IsEqualTo(BrokerBudgetProbeOutcome.Succeeded);
        await Assert.That(controller.WindowBytes).IsEqualTo(probedWindow);
        await Assert.That(controller.CapacityProbeSuccessCount).IsEqualTo(1);
    }

    [Test]
    public async Task UpProbe_RejectsWindowGrowthWithoutGoodputGrowth()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);
        ReachSteady(controller, ref now, ref admissionBlocks);

        while (controller.Phase != BrokerWindowPhase.ProbeDown)
            _ = DriveControllerEpoch(controller, ref now, ref admissionBlocks);
        _ = DriveControllerEpoch(controller, ref now, ref admissionBlocks);

        while (controller.Phase != BrokerWindowPhase.ProbeUp)
            _ = DriveControllerEpoch(controller, ref now, ref admissionBlocks);
        var baselineWindow = (long)Math.Floor(controller.WindowBytes / 1.125);

        var decision = DriveControllerEpoch(controller, ref now, ref admissionBlocks);

        await Assert.That(decision.ProbeOutcome).IsEqualTo(BrokerBudgetProbeOutcome.Failed);
        await Assert.That(controller.WindowBytes).IsLessThanOrEqualTo(baselineWindow + 1);
        await Assert.That(controller.CapacityProbeFailureCount).IsEqualTo(1);
    }

    [Test]
    public async Task RttProbe_DrainsThenRestoresSteadyWindow()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);
        ReachSteady(controller, ref now, ref admissionBlocks);
        var steadyWindow = controller.WindowBytes;

        now = T0 + Seconds(10.1);
        admissionBlocks++;
        controller.RecordAcknowledgement(
            logicalBytes: 100,
            rttTicks: Seconds(0.001),
            sealToSendTicks: 0,
            appLimited: false,
            controller.Generation,
            now);
        var started = controller.CompleteInterval(
            admissionBlocks,
            controller.WindowBytes,
            now);

        await Assert.That(started.ProbeType).IsEqualTo(BrokerBudgetProbeType.MinimumRtt);
        await Assert.That(controller.Phase).IsEqualTo(BrokerWindowPhase.ProbeRtt);
        await Assert.That(controller.WindowBytes).IsLessThanOrEqualTo(steadyWindow);

        var completed = DriveControllerEpoch(controller, ref now, ref admissionBlocks);
        await Assert.That(completed.ProbeOutcome).IsEqualTo(BrokerBudgetProbeOutcome.Succeeded);
        await Assert.That(controller.Phase).IsEqualTo(BrokerWindowPhase.Steady);
        await Assert.That(controller.WindowBytes).IsEqualTo(steadyWindow);
    }

    [Test]
    public async Task PartitionBatch_RefundsEstimateAndTransfersReservation()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1_024
        };
        var batch = new PartitionBatch(new TopicPartition("topic", 0), options);
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        const int reservedBytes = 200;
        await Assert.That(budget.TryReserve(reservedBytes, out var generation)).IsTrue();

        var append = batch.TryAppendFromSpans(
            timestamp: 1,
            keyData: ReadOnlySpan<byte>.Empty,
            keyIsNull: true,
            valueData: new byte[10],
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            completionSource: null,
            callback: null,
            estimatedSize: reservedBytes);
        batch.AddAdmissionReservation(budget, generation, reservedBytes);
        var ready = batch.Complete()!;

        await Assert.That(append.Success).IsTrue();
        await Assert.That(ready.UnackedReservedBytes).IsEqualTo(ready.DataSize);
        await Assert.That(budget.UnackedBytes).IsEqualTo(ready.DataSize);

        ready.Fail(new InvalidOperationException("test cleanup"));
        ready.Reset();
        await Assert.That(budget.UnackedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task PartitionBatch_LeaderChangeTransfersExistingReservation()
    {
        var batch = new PartitionBatch(
            new TopicPartition("topic", 0),
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                BatchSize = 1_024
            });
        var oldBudget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        var newBudget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        _ = oldBudget.TryReserve(100, out var oldGeneration);
        _ = newBudget.TryReserve(100, out var newGeneration);

        batch.AddAdmissionReservation(oldBudget, oldGeneration, 100);
        batch.AddAdmissionReservation(newBudget, newGeneration, 100);

        await Assert.That(oldBudget.UnackedBytes).IsEqualTo(0);
        await Assert.That(newBudget.UnackedBytes).IsEqualTo(200);
        await Assert.That(batch.Complete()).IsNull();
        await Assert.That(newBudget.UnackedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task CommonAdmissionGeneration_RejectsMixedRequests()
    {
        var first = new ReadyBatch { AdmissionGeneration = 7 };
        var second = new ReadyBatch { AdmissionGeneration = 7 };
        ReadyBatch[] batches = [first, second];

        await Assert.That(BrokerSender.GetCommonAdmissionGeneration(batches, 2)).IsEqualTo(7);

        second.AdmissionGeneration = 8;
        await Assert.That(BrokerSender.GetCommonAdmissionGeneration(batches, 2)).IsEqualTo(0);
    }

    private static BrokerUnackedByteBudget CreateBudget(long capBytes, long initialRequestBytes) =>
        new(
            targetSeconds: 0.010,
            floorBytes: 1,
            initialCapBytes: capBytes,
            initialRequestBytes: initialRequestBytes);

    private static BrokerWindowController CreateController() =>
        new(targetSeconds: 0.010, floorBytes: 1, capBytes: 10_000, initialRequestBytes: 100);

    private static void DriveBudgetEpoch(
        BrokerUnackedByteBudget budget,
        long now,
        long logicalBytes,
        double rttSeconds,
        long? admissionGeneration = null)
    {
        var rttTicks = Seconds(rttSeconds);
        budget.RecordAdmissionBlock(now);
        budget.ObserveWrittenUnackedBytes(budget.BudgetBytes);
        budget.OnAcked(
            logicalBytes,
            budget.SnapshotDelivery(
                now - rttTicks,
                appLimited: false,
                oldestBatchTimestamp: now - rttTicks,
                admissionGeneration ?? budget.CurrentGeneration),
            now);
        budget.CompleteAckedPass(now);
    }

    private static void ReachSteady(
        BrokerWindowController controller,
        ref long now,
        ref long admissionBlocks)
    {
        for (var i = 0; i < 10 && controller.Phase != BrokerWindowPhase.Steady; i++)
            _ = DriveControllerEpoch(controller, ref now, ref admissionBlocks);

        if (controller.Phase != BrokerWindowPhase.Steady)
            throw new InvalidOperationException("Controller did not leave startup.");
    }

    private static BrokerWindowDecision DriveControllerEpoch(
        BrokerWindowController controller,
        ref long now,
        ref long admissionBlocks,
        double rttSeconds = 0.001)
    {
        now += Seconds(0.110);
        admissionBlocks++;
        controller.RecordAcknowledgement(
            logicalBytes: 100,
            rttTicks: Seconds(rttSeconds),
            sealToSendTicks: 0,
            appLimited: false,
            controller.Generation,
            now);
        return controller.CompleteInterval(
            admissionBlocks,
            controller.WindowBytes,
            now);
    }
}
