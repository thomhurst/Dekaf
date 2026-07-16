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
    public async Task InitialWindow_IsSixteenFixedRequestQuanta()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);

        await Assert.That(budget.BudgetBytes).IsEqualTo(1_600);
        await Assert.That(budget.Phase).IsEqualTo(BrokerWindowPhase.Steady);
    }

    [Test]
    public async Task ColdStartAdmission_UsesOneRequestWaveUntilFirstAcknowledgement()
    {
        var budget = CreateColdStartBudget();

        await Assert.That(budget.BudgetBytes).IsEqualTo(100);
        await Assert.That(budget.TryReserve(100, out _)).IsTrue();
        await Assert.That(budget.TryReserve(1, out _)).IsFalse();

        // First ack: 100 bytes drained in 1ms = 100 KB/s; the 10ms target admits ten
        // milliseconds of that drain (1,000 bytes), not the full optimistic initial window.
        var now = T0 + Seconds(0.001);
        budget.OnAcked(
            ackedBytes: 100,
            budget.SnapshotDelivery(now - Seconds(0.001), appLimited: false),
            now);

        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);
        await Assert.That(budget.TryReserve(900, out _)).IsTrue();
        await Assert.That(budget.TryReserve(1, out _)).IsFalse();
        budget.Release(1_000);

        // Second ack raises the cumulative measured rate; the ramp opens the window further
        // until the optimistic sixteen-quantum start stops being the binding bound.
        var later = now + Seconds(0.001);
        budget.OnAcked(
            ackedBytes: 1_000,
            budget.SnapshotDelivery(later - Seconds(0.001), appLimited: false),
            later);
        budget.CompleteAckedPass(later);

        await Assert.That(budget.BudgetBytes).IsEqualTo(1_600);
    }

    [Test]
    public async Task ColdStartAdmission_TracksCurrentConnectionWidthBeforeFirstAck()
    {
        var budget = CreateColdStartBudget();

        budget.SetCap(capBytes: 30_000, coldStartBudgetBytes: 300, nowTicks: T0);
        await Assert.That(budget.BudgetBytes).IsEqualTo(300);

        budget.SetCap(capBytes: 10_000, coldStartBudgetBytes: 100, nowTicks: T0);
        await Assert.That(budget.BudgetBytes).IsEqualTo(100);
    }

    [Test]
    public async Task ColdStartBudget_UsesSharedBatchAndConnectionFormula()
    {
        await Assert.That(BrokerUnackedByteBudget.ComputeColdStartBudget(1_000, 3))
            .IsEqualTo(3_000);
        await Assert.That(BrokerUnackedByteBudget.ComputeCap(1_000, 3))
            .IsEqualTo(96_000);
    }

    [Test]
    public async Task NotePartialDeliverySuccess_EndsColdStartClampWithoutRateSample()
    {
        var budget = CreateColdStartBudget();
        await Assert.That(budget.BudgetBytes).IsEqualTo(100);

        budget.NotePartialDeliverySuccess();

        // No rate was measured, so the window opens only to the pessimistic two-quantum
        // start — but admission is no longer serialized on one request wave per round trip.
        await Assert.That(budget.BudgetBytes).IsEqualTo(200);

        budget.NotePartialDeliverySuccess();
        await Assert.That(budget.BudgetBytes).IsEqualTo(200);

        // The ramp still owns subsequent growth once a clean acknowledgement is measured.
        var now = T0 + Seconds(0.001);
        budget.OnAcked(
            ackedBytes: 100,
            budget.SnapshotDelivery(now - Seconds(0.001), appLimited: false),
            now);
        budget.CompleteAckedPass(now);

        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);
    }

    [Test]
    public async Task LatencyCeiling_ClampsWindowWhenMeasuredGoodputFallsBelowTarget()
    {
        var budget = CreateColdStartBudget();
        var now = FlipColdStartGateWithFastAck(budget);
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_600);
        var generation = budget.CurrentGeneration;

        // One completed control epoch at ~5.9 KB/s: ten milliseconds of that drain rounds
        // far below the two-quantum floor, so the window clamps to the floor.
        now += Seconds(0.510);
        DriveBudgetEpoch(budget, now, logicalBytes: 1_000, rttSeconds: 0.001);

        await Assert.That(budget.BudgetBytes).IsEqualTo(200);
        await Assert.That(budget.CurrentGeneration).IsGreaterThan(generation);
    }

    [Test]
    public async Task LatencyCeiling_RestoresCeilingBoundWindowWhenGoodputRecovers()
    {
        var budget = CreateColdStartBudget();
        var now = FlipColdStartGateWithFastAck(budget);

        now += Seconds(0.510);
        DriveBudgetEpoch(budget, now, logicalBytes: 1_000, rttSeconds: 0.001);
        await Assert.That(budget.BudgetBytes).IsEqualTo(200);

        // A recovered epoch (~980 KB/s) re-raises the ceiling; the ceiling-bound window
        // follows it without waiting for the probe schedule.
        now += Seconds(0.510);
        DriveBudgetEpoch(budget, now, logicalBytes: 500_000, rttSeconds: 0.001);

        await Assert.That(budget.BudgetBytes).IsEqualTo(9_800);
    }

    [Test]
    public async Task LatencyCeiling_SuppressesProbesWhileBindingAtTheFloor()
    {
        var budget = CreateColdStartBudget();
        var now = FlipColdStartGateWithFastAck(budget);

        // Sustained ~200 B/s goodput pins the ceiling at the floor: no up-probe candidate
        // can differ from the baseline, so the schedule never launches an experiment.
        for (var i = 0; i < 100; i++)
        {
            now += Seconds(0.510);
            DriveBudgetEpoch(budget, now, logicalBytes: 100, rttSeconds: 0.001);
        }

        await Assert.That(budget.BudgetBytes).IsEqualTo(200);
        await Assert.That(budget.Phase).IsEqualTo(BrokerWindowPhase.Steady);
        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(0);
        await Assert.That(budget.CapacityProbeFailureCount).IsEqualTo(0);
    }

    [Test]
    public async Task LatencyCeiling_FloorsHorizonAtRequestServingTimeWhenTargetUnreachable()
    {
        var budget = CreateColdStartBudget();

        // The first full acknowledgement takes 50ms — five times the 10ms target. The
        // horizon floors at two serving round trips (100ms), so the ceiling admits 100ms of
        // the measured 40 KB/s drain (4,000 bytes) and the optimistic 1,600-byte window
        // stands, instead of a target-only horizon (400 bytes) strangling admission at the
        // floor and re-measuring goodput through its own clamp.
        var now = T0 + Seconds(0.050);
        budget.OnAcked(
            ackedBytes: 2_000,
            budget.SnapshotDelivery(now - Seconds(0.050), appLimited: false),
            now);
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_600);

        // The completed-epoch path honors the same serving-time floor.
        now += Seconds(0.510);
        DriveBudgetEpoch(budget, now, logicalBytes: 100_000, rttSeconds: 0.050);
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_600);
    }

    private static BrokerUnackedByteBudget CreateColdStartBudget() =>
        new(
            targetSeconds: 0.010,
            floorBytes: 1,
            initialCapBytes: 10_000,
            initialRequestBytes: 100,
            coldStartBudgetBytes: 100);

    /// <summary>Ends the cold-start phase with a 2 MB/s first acknowledgement, whose
    /// target-scaled ceiling (20 KB, capped to 10 KB) leaves the sixteen-quantum optimistic
    /// window as the binding bound — the pre-ceiling handoff behavior.</summary>
    private static long FlipColdStartGateWithFastAck(BrokerUnackedByteBudget budget)
    {
        var now = T0 + Seconds(0.001);
        budget.OnAcked(
            ackedBytes: 2_000,
            budget.SnapshotDelivery(now - Seconds(0.001), appLimited: false),
            now);
        return now;
    }

    [Test]
    public async Task TryReserve_AtomicallyEnforcesWindow()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);

        await Assert.That(budget.TryReserve(1_000, out var firstGeneration)).IsTrue();
        await Assert.That(budget.TryReserve(600, out var secondGeneration)).IsTrue();
        await Assert.That(budget.TryReserve(1, out _)).IsFalse();
        await Assert.That(budget.UnackedBytes).IsEqualTo(1_600);
        await Assert.That(firstGeneration).IsEqualTo(budget.CurrentGeneration);
        await Assert.That(secondGeneration).IsEqualTo(budget.CurrentGeneration);

        budget.Release(1_600);
        await Assert.That(budget.UnackedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task TryReserve_OversizedRequestCannotStarveBehindWindowOccupancy()
    {
        var budget = CreateBudget(capBytes: 400, initialRequestBytes: 100);

        await Assert.That(budget.TryReserve(400, out _)).IsTrue();
        await Assert.That(budget.TryReserve(500, out _)).IsTrue();
        await Assert.That(budget.TryReserve(1, out _)).IsFalse();
        await Assert.That(budget.TryReserve(500, out _)).IsFalse();
        await Assert.That(budget.UnackedBytes).IsEqualTo(900);

        budget.Release(500);
        await Assert.That(budget.UnackedBytes).IsEqualTo(400);
        budget.Release(400);
    }

    [Test]
    public async Task Release_UnderflowClampsAccountingAndRecordsDiagnostic()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);

        await Assert.That(budget.TryReserve(100, out _)).IsTrue();
        budget.Release(101);

        await Assert.That(budget.UnackedBytes).IsEqualTo(0);
        await Assert.That(budget.AccountingUnderflowCount).IsEqualTo(1);
        await Assert.That(budget.TryReserve(budget.BudgetBytes, out _)).IsTrue();
        budget.Release(budget.BudgetBytes);
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

        await Assert.That(successes).IsEqualTo(160);
        await Assert.That(budget.UnackedBytes).IsEqualTo(budget.BudgetBytes);
        budget.Release(successes * 10L);
    }

    [Test]
    public async Task AdmissionFlushClaim_RequiresBrokerWithoutPipelineProgress()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);

        await Assert.That(budget.TryClaimAdmissionFlush(out var firstClaim)).IsTrue();
        await Assert.That(budget.TryClaimAdmissionFlush(out _)).IsFalse();
        await Assert.That(budget.IsAdmissionFlushClaimActive(firstClaim)).IsTrue();

        budget.RecordPipelineBatchEntered();

        await Assert.That(budget.PipelineBatchCount).IsEqualTo(1);
        await Assert.That(budget.IsAdmissionFlushClaimActive(firstClaim)).IsFalse();
        await Assert.That(budget.TryClaimAdmissionFlush(out _)).IsFalse();

        budget.RecordPipelineBatchExited();

        await Assert.That(budget.PipelineBatchCount).IsEqualTo(0);
        await Assert.That(budget.TryClaimAdmissionFlush(out var nextClaim)).IsTrue();
        await Assert.That(nextClaim).IsNotEqualTo(firstClaim);
        budget.ReleaseAdmissionFlushClaim(nextClaim);
    }

    [Test]
    public async Task AdmissionFlushClaim_StaleReleaseCannotCancelNewOwner()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);

        await Assert.That(budget.TryClaimAdmissionFlush(out var staleClaim)).IsTrue();
        budget.RecordPipelineBatchEntered();
        budget.RecordPipelineBatchExited();
        await Assert.That(budget.TryClaimAdmissionFlush(out var activeClaim)).IsTrue();

        budget.ReleaseAdmissionFlushClaim(staleClaim);

        await Assert.That(budget.IsAdmissionFlushClaimActive(activeClaim)).IsTrue();
        budget.ReleaseAdmissionFlushClaim(activeClaim);
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
    public async Task FragmentedAcknowledgements_DoNotRedefineWindowQuantum()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        budget.CompleteAckedPass(T0);

        var now = T0;
        for (var i = 0; i < 4; i++)
        {
            now += Seconds(0.510);
            DriveBudgetEpoch(budget, now, logicalBytes: 10, rttSeconds: 0.001);
        }

        await Assert.That(budget.BudgetBytes).IsEqualTo(1_600);
        await Assert.That(budget.MaxRateBytesPerSecond).IsGreaterThan(0);
    }

    [Test]
    public async Task StaleGenerationFeedback_DoesNotMoveWindow()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        var initialGeneration = budget.CurrentGeneration;
        budget.CompleteAckedPass(T0);
        var initialWindow = budget.BudgetBytes;

        DriveBudgetEpoch(
            budget,
            T0 + Seconds(0.510),
            100,
            0.001,
            admissionGeneration: initialGeneration - 1);

        await Assert.That(budget.BudgetBytes).IsEqualTo(initialWindow);
    }

    [Test]
    public async Task IdleAppLimitedTraffic_DoesNotStartCapacityProbe()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        var initialWindow = controller.WindowBytes;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);

        for (var i = 0; i < 100; i++)
        {
            _ = DriveControllerEpoch(
                controller,
                ref now,
                ref admissionBlocks,
                appLimited: true,
                recordAdmissionBlock: false,
                observedOutstandingBytes: 0);
        }

        await Assert.That(controller.Phase).IsEqualTo(BrokerWindowPhase.Steady);
        await Assert.That(controller.WindowBytes).IsEqualTo(initialWindow);
    }

    [Test]
    public async Task OccupancyPressure_AloneStartsCapacityProbe()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);

        for (var i = 0; i < 100 && controller.Phase == BrokerWindowPhase.Steady; i++)
        {
            _ = DriveControllerEpoch(
                controller,
                ref now,
                ref admissionBlocks,
                appLimited: true,
                recordAdmissionBlock: false,
                observedOutstandingBytes: controller.WindowBytes);
        }

        await Assert.That(controller.Phase).IsEqualTo(BrokerWindowPhase.ProbeDown);
    }

    [Test]
    public async Task AdmissionPressure_AloneStartsCapacityProbe()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);

        for (var i = 0; i < 100 && controller.Phase == BrokerWindowPhase.Steady; i++)
        {
            _ = DriveControllerEpoch(
                controller,
                ref now,
                ref admissionBlocks,
                appLimited: true,
                observedOutstandingBytes: 0);
        }

        await Assert.That(controller.Phase).IsEqualTo(BrokerWindowPhase.ProbeDown);
    }

    [Test]
    public async Task LoadedEpoch_AloneStartsCapacityProbe()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);

        for (var i = 0; i < 100 && controller.Phase == BrokerWindowPhase.Steady; i++)
        {
            _ = DriveControllerEpoch(
                controller,
                ref now,
                ref admissionBlocks,
                recordAdmissionBlock: false,
                observedOutstandingBytes: 0);
        }

        await Assert.That(controller.Phase).IsEqualTo(BrokerWindowPhase.ProbeDown);
    }

    [Test]
    public async Task PersistentQueueDelay_DoesNotBypassSettledProbeSchedule()
    {
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        budget.CompleteAckedPass(T0);
        DriveBudgetEpoch(budget, T0 + Seconds(0.510), 100, 0.001);
        var initialWindow = budget.BudgetBytes;

        DriveBudgetEpoch(budget, T0 + Seconds(1.020), 100, 0.030);
        DriveBudgetEpoch(budget, T0 + Seconds(1.530), 100, 0.030);
        DriveBudgetEpoch(budget, T0 + Seconds(2.040), 100, 0.030);

        await Assert.That(budget.Phase).IsEqualTo(BrokerWindowPhase.Steady);
        await Assert.That(budget.BudgetBytes).IsEqualTo(initialWindow);
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
        StartNextProbe(
            controller,
            BrokerWindowPhase.ProbeDown,
            ref now,
            ref admissionBlocks);

        await Assert.That(controller.Phase).IsEqualTo(BrokerWindowPhase.ProbeDown);
        await Assert.That(controller.WindowBytes).IsLessThan(baselineWindow);
        var probedWindow = controller.WindowBytes;

        var decision = CompleteActiveProbe(
            controller,
            baselineWindow,
            ref now,
            ref admissionBlocks);

        await Assert.That(decision.ProbeOutcome).IsEqualTo(BrokerBudgetProbeOutcome.Succeeded);
        await Assert.That(controller.WindowBytes).IsEqualTo(probedWindow);
        await Assert.That(controller.CapacityProbeSuccessCount).IsEqualTo(1);
    }

    [Test]
    public async Task DownProbe_RevertsSmallerWindowWhenGoodputFalls()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);
        var baselineWindow = controller.WindowBytes;

        StartNextProbe(
            controller,
            BrokerWindowPhase.ProbeDown,
            ref now,
            ref admissionBlocks);

        var decision = CompleteActiveProbe(
            controller,
            baselineWindow,
            ref now,
            ref admissionBlocks,
            candidateLogicalBytes: 50);

        await Assert.That(decision.ProbeOutcome).IsEqualTo(BrokerBudgetProbeOutcome.Failed);
        await Assert.That(controller.WindowBytes).IsEqualTo(baselineWindow);
        await Assert.That(controller.CapacityProbeFailureCount).IsEqualTo(1);
    }

    [Test]
    public async Task DownProbe_DoesNotUseIncompletePostSealDelayAsVeto()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);
        var baselineWindow = controller.WindowBytes;

        StartNextProbe(
            controller,
            BrokerWindowPhase.ProbeDown,
            ref now,
            ref admissionBlocks);

        var decision = CompleteActiveProbe(
            controller,
            baselineWindow,
            ref now,
            ref admissionBlocks,
            candidateSealToSendSeconds: 0.050);

        await Assert.That(decision.ProbeOutcome).IsEqualTo(BrokerBudgetProbeOutcome.Succeeded);
        await Assert.That(controller.WindowBytes).IsLessThan(baselineWindow);
    }

    [Test]
    public async Task ActiveProbe_MixedGenerationsAbortAndRestoreBaseline()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);
        var baselineWindow = controller.WindowBytes;

        StartNextProbe(
            controller,
            BrokerWindowPhase.ProbeDown,
            ref now,
            ref admissionBlocks);

        BrokerWindowDecision decision = default;
        for (var i = 0; i < 4; i++)
        {
            decision = DriveControllerEpoch(
                controller,
                ref now,
                ref admissionBlocks,
                admissionGeneration: 0);
        }

        await Assert.That(decision.ProbeOutcome).IsEqualTo(BrokerBudgetProbeOutcome.Failed);
        await Assert.That(controller.Phase).IsEqualTo(BrokerWindowPhase.Steady);
        await Assert.That(controller.WindowBytes).IsEqualTo(baselineWindow);
        await Assert.That(controller.CapacityProbeFailureCount).IsEqualTo(1);
    }

    [Test]
    public async Task UpProbe_RejectsWindowGrowthWithoutGoodputGrowth()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);
        ReachSteady(controller, ref now, ref admissionBlocks);

        FailNextDownProbe(controller, ref now, ref admissionBlocks);
        var baselineWindow = controller.WindowBytes;
        StartNextProbe(
            controller,
            BrokerWindowPhase.ProbeUp,
            ref now,
            ref admissionBlocks);

        var decision = CompleteActiveProbe(
            controller,
            baselineWindow,
            ref now,
            ref admissionBlocks);

        await Assert.That(decision.ProbeOutcome).IsEqualTo(BrokerBudgetProbeOutcome.Failed);
        await Assert.That(controller.WindowBytes).IsEqualTo(baselineWindow);
        await Assert.That(controller.CapacityProbeFailureCount).IsEqualTo(2);
    }

    [Test]
    public async Task UpProbe_KeepsLargerWindowWhenGoodputGrows()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);

        FailNextDownProbe(controller, ref now, ref admissionBlocks);
        var baselineWindow = controller.WindowBytes;
        StartNextProbe(
            controller,
            BrokerWindowPhase.ProbeUp,
            ref now,
            ref admissionBlocks);
        var probedWindow = controller.WindowBytes;

        var decision = CompleteActiveProbe(
            controller,
            baselineWindow,
            ref now,
            ref admissionBlocks,
            candidateLogicalBytes: 120);

        await Assert.That(decision.ProbeOutcome).IsEqualTo(BrokerBudgetProbeOutcome.Succeeded);
        await Assert.That(controller.WindowBytes).IsEqualTo(probedWindow);
        await Assert.That(controller.CapacityProbeSuccessCount).IsEqualTo(1);
    }

    [Test]
    public async Task UpProbe_RejectsGoodputGrowthWithControlledDelayInflation()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);

        FailNextDownProbe(controller, ref now, ref admissionBlocks);
        var baselineWindow = controller.WindowBytes;
        StartNextProbe(
            controller,
            BrokerWindowPhase.ProbeUp,
            ref now,
            ref admissionBlocks);

        var decision = CompleteActiveProbe(
            controller,
            baselineWindow,
            ref now,
            ref admissionBlocks,
            candidateLogicalBytes: 120,
            candidateSealToSendSeconds: 0.050);

        await Assert.That(decision.ProbeOutcome).IsEqualTo(BrokerBudgetProbeOutcome.Failed);
        await Assert.That(controller.WindowBytes).IsEqualTo(baselineWindow);
    }

    [Test]
    public async Task UpProbe_SandwichCancelsLinearRunnerTrend()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);

        FailNextDownProbe(controller, ref now, ref admissionBlocks);
        var baselineWindow = controller.WindowBytes;
        StartNextProbe(
            controller,
            BrokerWindowPhase.ProbeUp,
            ref now,
            ref admissionBlocks);

        BrokerWindowDecision decision = default;
        var epoch = 0;
        while (controller.Phase == BrokerWindowPhase.ProbeUp && epoch < 100)
        {
            decision = DriveControllerEpoch(
                controller,
                ref now,
                ref admissionBlocks,
                logicalBytes: 100 + epoch++);
        }

        await Assert.That(decision.ProbeOutcome).IsEqualTo(BrokerBudgetProbeOutcome.Failed);
        await Assert.That(controller.WindowBytes).IsEqualTo(baselineWindow);
    }

    [Test]
    public async Task CapacityProbes_NeverShrinkBelowFixedPipelineFloor()
    {
        var controller = CreateController();
        var now = T0;
        var admissionBlocks = 0L;
        _ = controller.CompleteInterval(admissionBlocks, 0, now);
        _ = DriveControllerEpoch(controller, ref now, ref admissionBlocks, rttSeconds: 0.001);
        for (var i = 0; i < 1_000; i++)
            _ = DriveControllerEpoch(controller, ref now, ref admissionBlocks, rttSeconds: 0.050);

        await Assert.That(controller.RequestQuantumBytes).IsEqualTo(100);
        await Assert.That(controller.WindowBytes).IsGreaterThanOrEqualTo(800);
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
        const int leaseBytes = 1_024;
        await Assert.That(budget.TryReserve(leaseBytes, out var generation)).IsTrue();

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
            estimatedSize: 200);
        batch.AddAdmissionLease(budget, generation, leaseBytes);
        var ready = batch.Complete()!;

        await Assert.That(append.Success).IsTrue();
        await Assert.That(ready.UnackedReservedBytes).IsEqualTo(ready.DataSize);
        await Assert.That(budget.UnackedBytes).IsEqualTo(ready.DataSize);

        ready.Fail(new InvalidOperationException("test cleanup"));
        ready.Reset();
        await Assert.That(budget.UnackedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task PartitionBatch_MultipleRecordsReuseOneBrokerLease()
    {
        var batch = new PartitionBatch(
            new TopicPartition("topic", 0),
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                BatchSize = 1_024
            });
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        await Assert.That(budget.TryReserve(1_024, out var generation)).IsTrue();
        batch.AddAdmissionLease(budget, generation, 1_024);

        for (var i = 0; i < 2; i++)
        {
            var append = batch.TryAppendFromSpans(
                timestamp: i + 1,
                keyData: ReadOnlySpan<byte>.Empty,
                keyIsNull: true,
                valueData: new byte[10],
                valueIsNull: false,
                headers: null,
                headerCount: 0,
                completionSource: null,
                callback: null,
                estimatedSize: 64);
            await Assert.That(append.Success).IsTrue();
            await Assert.That(budget.UnackedBytes).IsEqualTo(1_024);
        }

        var ready = batch.Complete()!;
        await Assert.That(budget.UnackedBytes).IsEqualTo(ready.DataSize);
        ready.Fail(new InvalidOperationException("test cleanup"));
        ready.Reset();
        await Assert.That(budget.UnackedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task PartitionBatch_MixedLeaseGenerationsClearAdmissionGeneration()
    {
        var batch = new PartitionBatch(
            new TopicPartition("topic", 0),
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                BatchSize = 1_024
            });
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        const int leaseBytes = 1_024;
        await Assert.That(budget.TryReserve(leaseBytes, out var generation)).IsTrue();

        batch.AddAdmissionLease(budget, generation, leaseBytes / 2);
        batch.AddAdmissionLease(budget, generation + 1, leaseBytes / 2);
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
            estimatedSize: 200);
        var ready = batch.Complete()!;

        await Assert.That(append.Success).IsTrue();
        await Assert.That(ready.AdmissionGeneration).IsEqualTo(0);
        await Assert.That(ready.UnackedReservedBytes).IsEqualTo(ready.DataSize);
        await Assert.That(budget.UnackedBytes).IsEqualTo(ready.DataSize);

        ready.Fail(new InvalidOperationException("test cleanup"));
        ready.Reset();
        await Assert.That(budget.UnackedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task PartitionBatch_UnderReservedTopUpClearsAdmissionGeneration()
    {
        var batch = new PartitionBatch(
            new TopicPartition("topic", 0),
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                BatchSize = 1_024
            });
        var budget = CreateBudget(capBytes: 10_000, initialRequestBytes: 100);
        const int leaseBytes = 64;
        await Assert.That(budget.TryReserve(leaseBytes, out var generation)).IsTrue();
        batch.AddAdmissionLease(budget, generation, leaseBytes);

        var append = batch.TryAppendFromSpans(
            timestamp: 1,
            keyData: ReadOnlySpan<byte>.Empty,
            keyIsNull: true,
            valueData: new byte[128],
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            completionSource: null,
            callback: null,
            estimatedSize: 200);
        var ready = batch.Complete()!;

        await Assert.That(append.Success).IsTrue();
        await Assert.That(ready.DataSize).IsGreaterThan(leaseBytes);
        await Assert.That(ready.AdmissionGeneration).IsEqualTo(0);
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
        _ = oldBudget.TryReserve(1_024, out var oldGeneration);
        _ = newBudget.TryReserve(1_024, out var newGeneration);

        batch.AddAdmissionLease(oldBudget, oldGeneration, 1_024);
        batch.AddAdmissionLease(newBudget, newGeneration, 1_024);

        await Assert.That(oldBudget.UnackedBytes).IsEqualTo(0);
        await Assert.That(newBudget.UnackedBytes).IsEqualTo(2_048);
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
        if (controller.Phase != BrokerWindowPhase.Steady)
            throw new InvalidOperationException("Controller did not leave startup.");
    }

    private static void StartNextProbe(
        BrokerWindowController controller,
        BrokerWindowPhase expectedPhase,
        ref long now,
        ref long admissionBlocks)
    {
        for (var i = 0; i < 100 && controller.Phase == BrokerWindowPhase.Steady; i++)
            _ = DriveControllerEpoch(controller, ref now, ref admissionBlocks);

        if (controller.Phase != expectedPhase)
        {
            throw new InvalidOperationException(
                $"Expected {expectedPhase}, reached {controller.Phase}.");
        }
    }

    private static BrokerWindowDecision CompleteActiveProbe(
        BrokerWindowController controller,
        long baselineWindow,
        ref long now,
        ref long admissionBlocks,
        long candidateLogicalBytes = 100,
        long baselineLogicalBytes = 100,
        double candidateSealToSendSeconds = 0,
        double baselineSealToSendSeconds = 0)
    {
        BrokerWindowDecision decision = default;
        for (var i = 0; i < 100 && controller.Phase != BrokerWindowPhase.Steady; i++)
        {
            var candidateActive = controller.WindowBytes != baselineWindow;
            decision = DriveControllerEpoch(
                controller,
                ref now,
                ref admissionBlocks,
                logicalBytes: candidateActive ? candidateLogicalBytes : baselineLogicalBytes,
                sealToSendSeconds: candidateActive
                    ? candidateSealToSendSeconds
                    : baselineSealToSendSeconds);
        }

        if (controller.Phase != BrokerWindowPhase.Steady)
            throw new InvalidOperationException("Capacity probe did not complete.");

        return decision;
    }

    private static void FailNextDownProbe(
        BrokerWindowController controller,
        ref long now,
        ref long admissionBlocks)
    {
        var baselineWindow = controller.WindowBytes;
        StartNextProbe(
            controller,
            BrokerWindowPhase.ProbeDown,
            ref now,
            ref admissionBlocks);
        _ = CompleteActiveProbe(
            controller,
            baselineWindow,
            ref now,
            ref admissionBlocks,
            candidateLogicalBytes: 50);
    }

    private static BrokerWindowDecision DriveControllerEpoch(
        BrokerWindowController controller,
        ref long now,
        ref long admissionBlocks,
        double rttSeconds = 0.001,
        long logicalBytes = 100,
        long? admissionGeneration = null,
        double sealToSendSeconds = 0,
        bool appLimited = false,
        bool recordAdmissionBlock = true,
        long? observedOutstandingBytes = null)
    {
        now += Seconds(0.510);
        if (recordAdmissionBlock)
            admissionBlocks++;
        controller.RecordAcknowledgement(
            logicalBytes,
            rttTicks: Seconds(rttSeconds),
            sealToSendTicks: Seconds(sealToSendSeconds),
            appLimited,
            admissionGeneration ?? controller.Generation,
            now);
        return controller.CompleteInterval(
            admissionBlocks,
            observedOutstandingBytes ?? controller.WindowBytes,
            now);
    }
}
