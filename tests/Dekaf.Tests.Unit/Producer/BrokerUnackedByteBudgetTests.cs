using System.Diagnostics;
using System.Reflection;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// State-machine tests for <see cref="BrokerUnackedByteBudget"/>: budget publication from
/// the time-windowed delivery-rate filter, minimum-RTT refresh, floor/cap clamping, probing,
/// and counter accounting.
/// All timestamps are explicit Stopwatch-tick values, so nothing here is timing-dependent.
/// </summary>
public sealed class BrokerUnackedByteBudgetTests
{
    private static readonly long Frequency = Stopwatch.Frequency;

    /// <summary>An arbitrary positive anchor: tick 0 means "no window anchored yet".</summary>
    private static readonly long T0 = Frequency;

    private static long Seconds(double seconds) => (long)(seconds * Frequency);

    private static void SetField<T>(BrokerUnackedByteBudget budget, string fieldName, T value)
        => typeof(BrokerUnackedByteBudget)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(budget, value);

    private static T GetField<T>(BrokerUnackedByteBudget budget, string fieldName)
        => (T)typeof(BrokerUnackedByteBudget)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(budget)!;

    /// <summary>
    /// Acknowledges one request the way the send loop does: the send timestamp is derived
    /// from the request's round trip, and the delivery snapshot defaults to the clock as of
    /// the previous acknowledgement. <paramref name="appLimitedAtSend"/> defaults to true
    /// because sequential test sequences model a request sent after the previous response
    /// arrived — an empty pipe — so the sample uses the request's own sojourn. Pipelining
    /// tests mint explicit earlier snapshots with <c>appLimited: false</c>.
    /// </summary>
    private static void Ack(
        BrokerUnackedByteBudget budget,
        long ackedBytes,
        double rttSeconds,
        long nowTicks,
        BrokerUnackedByteBudget.DeliverySnapshot? snapshotAtSend = null,
        bool appLimitedAtSend = true)
    {
        budget.OnAcked(
            ackedBytes,
            snapshotAtSend ?? budget.SnapshotDelivery(nowTicks - Seconds(rttSeconds), appLimitedAtSend),
            nowTicks);
        CompleteAckedPassWithoutDecay(budget, nowTicks);
    }

    /// <summary>
    /// Most state-machine tests isolate estimator output from decay hysteresis. A standing
    /// queue deliberately invokes the production bypass so those tests can assert the raw
    /// computed budget; dedicated decay tests below exercise smoothing separately.
    /// </summary>
    private static void CompleteAckedPassWithoutDecay(
        BrokerUnackedByteBudget budget,
        long nowTicks)
    {
        SetField(budget, "_lastNormalBudgetBytes", 0L);
        budget.CompleteAckedPass(nowTicks);
    }

    private static void SetCapWithoutDecay(
        BrokerUnackedByteBudget budget,
        long capBytes,
        long nowTicks)
    {
        SetField(budget, "_lastNormalBudgetBytes", 0L);
        budget.SetCap(capBytes, nowTicks);
    }

    /// <summary>
    /// Establishes a per-request drain-rate sample of <paramref name="bytesPerSecond"/>.
    /// </summary>
    private static void EstablishRate(BrokerUnackedByteBudget budget, long bytesPerSecond, double rttSeconds)
    {
        Ack(budget, (long)(bytesPerSecond * rttSeconds), rttSeconds, T0);
    }

    private static void SeedCapacityProbeEvaluation(
        BrokerUnackedByteBudget budget,
        double baselineRate,
        double baselineSealToAckSeconds,
        double accumulatedRate)
    {
        SetField(budget, "_capacityProbeBaselineRate", baselineRate);
        SetField(budget, "_capacityProbePreProbeSealToAckSeconds", baselineSealToAckSeconds);
        SetField(budget, "_capacityProbeStartTimestamp", T0);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.0));
        SetField(budget, "_capacityProbeEvaluationDeadlineTimestamp", T0 + Seconds(0.200));
        SetField(budget, "_capacityProbeRateSum", accumulatedRate);
        SetField(budget, "_capacityProbeRateSampleCount", 2);
        SetField(budget, "_capacityProbeActive", true);
    }

    [Test]
    public async Task Budget_BeforeFirstRateSample_EqualsCap()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 3_200);

        await Assert.That(budget.BudgetBytes).IsEqualTo(3_200);
    }

    [Test]
    public async Task Budget_AfterRateSample_TracksRttSafetyTimesRate()
    {
        // 300,000 B/s × 1.5 × 1ms RTT = 450 bytes, inside [200, 3,200].
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 3_200);

        EstablishRate(budget, bytesPerSecond: 300_000, rttSeconds: 0.001);
        Ack(budget, 300, 0.001, T0 + Seconds(0.051));

        await Assert.That(budget.BudgetBytes).IsEqualTo(450);
    }

    [Test]
    public async Task LowRttBudget_UsesMinimumNormalHorizon()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 3_200);

        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.0001);
        Ack(budget, 100, 0.0001, T0 + Seconds(0.051));

        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000)
            .Because("an empty-pipe RTT must not make the admission horizon too shallow to discover capacity");
    }

    [Test]
    public async Task DeliveryLatencyAboveTarget_ReducesRateBudgetAndProof()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.001);
        SetField(budget, "_provenPipelineRequestQuanta", 32.0);

        var now = T0;
        for (var i = 0; i < 6; i++)
        {
            now += Seconds(0.110);
            // Sender-backlog shrink requires the wire to demonstrably carry the budget.
            budget.ObserveWrittenUnackedBytes(8_000);
            var snapshot = budget.SnapshotDelivery(
                now - Seconds(0.001),
                appLimited: true,
                oldestBatchTimestamp: now - Seconds(0.020));
            Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);
        }

        await Assert.That(budget.DeliveryLatencyEwmaMicros).IsEqualTo(19_000).Within(10);
        await Assert.That(budget.LatencyBudgetScale).IsLessThan(0.25);
        await Assert.That(budget.ProvenPipelineRequestQuanta).IsLessThan(8.0)
            .Because("actionable over-target queueing must revoke stale probe proof, not only derate it transiently");
        await Assert.That(budget.BudgetBytes).IsLessThan(2_500);
    }

    [Test]
    public async Task DeliveryLatencyBelowTarget_DoesNotGrowBudgetWithoutAdmissionPressure()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.001);

        var now = T0;
        for (var i = 0; i < 6; i++)
        {
            now += Seconds(0.110);
            var snapshot = budget.SnapshotDelivery(
                now - Seconds(0.001),
                appLimited: true,
                oldestBatchTimestamp: now - Seconds(0.020));
            Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);
        }
        for (var i = 0; i < 30; i++)
        {
            now += Seconds(0.110);
            var snapshot = budget.SnapshotDelivery(
                now - Seconds(0.001),
                appLimited: true,
                oldestBatchTimestamp: now - Seconds(0.005));
            Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);
        }
        var scaleAfterLatencySettledBelowTarget = budget.LatencyBudgetScale;

        for (var i = 0; i < 20; i++)
        {
            now += Seconds(0.110);
            var snapshot = budget.SnapshotDelivery(
                now - Seconds(0.001),
                appLimited: true,
                oldestBatchTimestamp: now - Seconds(0.005));
            Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);
        }

        await Assert.That(budget.DeliveryLatencyEwmaMicros).IsLessThan(6_000);
        await Assert.That(budget.LatencyBudgetScale)
            .IsEqualTo(scaleAfterLatencySettledBelowTarget).Within(0.000_001);
    }

    [Test]
    public async Task DeliveryLatencyBelowTarget_RecoversReducedBudgetWhenAdmissionBlocked()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.001);

        var now = T0;
        for (var i = 0; i < 6; i++)
        {
            now += Seconds(0.110);
            budget.ObserveWrittenUnackedBytes(8_000);
            var snapshot = budget.SnapshotDelivery(
                now - Seconds(0.001),
                appLimited: true,
                oldestBatchTimestamp: now - Seconds(0.020));
            Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);
        }
        var reducedScale = budget.LatencyBudgetScale;

        for (var i = 0; i < 30; i++)
        {
            now += Seconds(0.110);
            budget.RecordAdmissionBlock();
            var snapshot = budget.SnapshotDelivery(
                now - Seconds(0.001),
                appLimited: true,
                oldestBatchTimestamp: now - Seconds(0.005));
            Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);
        }

        await Assert.That(budget.DeliveryLatencyEwmaMicros).IsLessThan(6_000);
        await Assert.That(budget.LatencyBudgetScale).IsGreaterThan(reducedScale);
        await Assert.That(budget.BudgetBytes).IsEqualTo(2_225)
            .Because("blocked demand restores a request pipeline only within the remaining end-to-end latency cap");
    }

    [Test]
    public async Task DeliveryLatencyBelowTarget_RestoresReducedScaleToNeutralWithoutAdmissionPressure()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.001);
        SetField(budget, "_latencyBudgetScale", 0.25);

        var now = T0;
        for (var i = 0; i < 40; i++)
        {
            now += Seconds(0.110);
            var snapshot = budget.SnapshotDelivery(
                now - Seconds(0.001),
                appLimited: true,
                oldestBatchTimestamp: now - Seconds(0.003));
            Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);
        }

        await Assert.That(budget.LatencyBudgetScale).IsEqualTo(1.0).Within(0.000_001)
            .Because("transient queue control must not leave the normal budget permanently derated");
    }

    [Test]
    public async Task DeliveryLatencySample_UsesSealToSendQueueDelay_NotBrokerRoundTrip()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        var now = T0;
        var snapshot = budget.SnapshotDelivery(
            sendTimestamp: now - Seconds(0.015),
            appLimited: true,
            oldestBatchTimestamp: now - Seconds(0.020));

        Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);

        await Assert.That(budget.DeliveryLatencyEwmaMicros).IsEqualTo(5_000).Within(10);
    }

    [Test]
    public async Task Budget_ClampsToFloor_WhenRateCollapses()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 3_200);

        // 1,000 B/s × 10ms = 10 bytes — far below the floor.
        EstablishRate(budget, bytesPerSecond: 1_000, rttSeconds: 0.001);

        await Assert.That(budget.BudgetBytes).IsEqualTo(200);
    }

    [Test]
    public async Task Budget_ClampsToCap_WhenRateExplodes()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 3_200);

        // One BDP at 1,000,000 B/s × 100ms is already far above the cap.
        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.100);

        await Assert.That(budget.BudgetBytes).IsEqualTo(3_200);
    }

    [Test]
    public async Task Budget_HorizonNeverDropsBelowOneBdp()
    {
        // Target 10ms but measured RTT is 100ms: an impossible target still retains one BDP.
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        EstablishRate(budget, bytesPerSecond: 100_000, rttSeconds: 0.100);
        Ack(budget, 10_000, 0.100, T0 + Seconds(0.101));

        // 100,000 B/s × 100ms = 10,000 bytes (±tick-quantization of the RTT).
        await Assert.That(budget.BudgetBytes).IsGreaterThanOrEqualTo(9_990);
        await Assert.That(budget.BudgetBytes).IsLessThanOrEqualTo(10_010);
    }

    [Test]
    public async Task Budget_RttGuard_UsesServingRttEwmaWithinClampUnderLoad()
    {
        // Base RTT 10ms, loaded serving RTT 15ms — the 10ms target caps the preferred
        // 1.5 × serving-RTT safety margin at one base-RTT BDP.
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_hasMinRttSample", true);
        SetField(budget, "_minRttSeconds", 0.010);
        SetField(budget, "_servingRttEwmaSeconds", 0.015);
        SetField(budget, "_windowMaxRate", 1_000_000.0);
        SetField(budget, "_retainedLoadedMaxRate", 1_000_000.0);
        SetField(budget, "_hasLoadedServingSample", true);

        SetCapWithoutDecay(budget, 1_000_000, T0);

        await Assert.That(budget.BudgetBytes).IsEqualTo(10_000)
            .Because("the latency cap wins without dropping below one base-RTT BDP");
    }

    [Test]
    public async Task Budget_RttGuard_ClampsQueueInflatedServingRtt()
    {
        // Base RTT 1ms but the loaded serving RTT reads 20ms — that excess is queueing the
        // budget itself admitted. Unclamped, the floor would be 1.5 × 20ms = 30,000 bytes,
        // re-inflating the RTT it is derived from (the #2009 ratchet). The clamp bounds the
        // safety horizon at 1.5 × 2 × base RTT = 3ms, below the 10ms latency cap.
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_hasMinRttSample", true);
        SetField(budget, "_minRttSeconds", 0.001);
        SetField(budget, "_servingRttEwmaSeconds", 0.020);
        SetField(budget, "_windowMaxRate", 1_000_000.0);
        SetField(budget, "_retainedLoadedMaxRate", 1_000_000.0);
        SetField(budget, "_hasLoadedServingSample", true);

        SetCapWithoutDecay(budget, 1_000_000, T0);

        await Assert.That(budget.BudgetBytes).IsEqualTo(3_000)
            .Because("queue-inflated serving RTT must not force the horizon to the target");
    }

    [Test]
    public async Task RttFloorCapacityProbe_UsesUniformHeadroom()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_hasMinRttSample", true);
        SetField(budget, "_minRttSeconds", 0.001);
        SetField(budget, "_servingRttEwmaSeconds", 0.020);
        SetField(budget, "_windowMaxRate", 1_000_000.0);
        SetField(budget, "_retainedLoadedMaxRate", 1_000_000.0);
        SetField(budget, "_hasLoadedServingSample", true);
        SetField(budget, "_capacityProbeActive", true);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.0));

        SetCapWithoutDecay(budget, 1_000_000, T0);

        await Assert.That(budget.BudgetBytes).IsEqualTo(3_750)
            .Because("capacity probes apply uniform headroom to the target-bounded RTT horizon");
    }

    [Test]
    public async Task ServingRtt_LoadedSamplesRaiseSafetyFloorWithoutReplacingMinimum()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 500, 0.005, T0);
        Ack(budget, 500, 0.005, T0 + Seconds(0.051));
        Ack(budget, 10_000, 0.100, T0 + Seconds(1.0));

        // Both samples measure 100,000 B/s. The later back-to-back 100ms service sample
        // raises the preferred safety horizon above the 10ms end-to-end cap while the 5ms
        // base RTT remains unchanged, leaving one base-RTT BDP as standing flight.
        await Assert.That(budget.MinimumRttMicros).IsEqualTo(5_000);
        await Assert.That(budget.BudgetBytes).IsEqualTo(500);
    }

    [Test]
    public async Task MinimumRttProbe_RetainsOneBdpWhileDrainingQueue()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);

        Ack(budget, 10_000, 0.100, T0, appLimitedAtSend: false);

        await Assert.That(budget.BudgetBytes).IsEqualTo(10_000)
            .Because("base-RTT sampling must drain queueing without emptying the serving pipe");
    }

    [Test]
    public async Task MinimumRtt_ExpiredWindowDrainsQueueBeforeRefreshing()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 500, 0.005, T0);
        Ack(budget, 500, 0.005, T0 + Seconds(0.051));

        // Expiring the minimum with a loaded sample starts a brief one-BDP drain,
        // rather than immediately accepting the 100ms queueing delay as base RTT.
        Ack(budget, 10_000, 0.100, T0 + Seconds(10.1));
        await Assert.That(budget.BudgetBytes).IsEqualTo(500);

        Ack(budget, 5_000, 0.050, T0 + Seconds(10.2));
        Ack(budget, 500, 0.005, T0 + Seconds(10.31));
        Ack(budget, 10_000, 0.100, T0 + Seconds(10.4));

        // The final 10,000-byte request measures its own 100ms sojourn (100,000 B/s);
        // pipelined delivery credit flows through the delivered-counter delta rather than
        // a shrunken inter-ack interval. Normal operation retains one base-RTT BDP after
        // reserving serving time inside the end-to-end target.
        await Assert.That(budget.MinimumRttMicros).IsEqualTo(5_000);
        await Assert.That(budget.BudgetBytes).IsEqualTo(500);
    }

    [Test]
    public async Task MinimumRtt_ProbeBoundsSingleWindowGrowth()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_hasMinRttSample", true);
        SetField(budget, "_minRttSeconds", 0.001);
        SetField(budget, "_minRttProbeUntilTimestamp", T0);
        SetField(budget, "_minRttProbeMinimumSeconds", 0.010);

        Ack(budget, 10_000, 0.010, T0, appLimitedAtSend: false);

        await Assert.That(budget.MinimumRttMicros).IsEqualTo(1_250)
            .Because("one partially queued refresh must not multiply the BDP floor");
    }

    [Test]
    public async Task MinimumRtt_RecentNaturalSampleSkipsDrainProbe()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);

        Ack(budget, 500, 0.005, T0, appLimitedAtSend: false);
        Ack(budget, 500, 0.005, T0 + Seconds(0.051), appLimitedAtSend: true);
        for (var second = 1; second <= 9; second++)
            Ack(budget, 500, 0.005, T0 + Seconds(second), appLimitedAtSend: true);
        Ack(budget, 10_000, 0.100, T0 + Seconds(10.2), appLimitedAtSend: false);

        await Assert.That(GetField<long>(budget, "_minRttProbeUntilTimestamp")).IsEqualTo(0)
            .Because("a recent empty-pipe RTT already refreshed the base RTT without narrowing admission");
    }

    [Test]
    public async Task MinimumRtt_NaturalSampleCompletesProbeWithBestObservedRtt()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_hasMinRttSample", true);
        SetField(budget, "_minRttSeconds", 0.010);
        SetField(budget, "_minRttProbeUntilTimestamp", T0 + Seconds(1.0));
        SetField(budget, "_minRttProbeMinimumSeconds", 0.004);

        Ack(budget, 600, 0.006, T0, appLimitedAtSend: true);

        await Assert.That(budget.MinimumRttMicros).IsEqualTo(4_000)
            .Because("ending a drain on a natural sample must retain the probe's best earlier RTT");
        await Assert.That(GetField<long>(budget, "_minRttProbeUntilTimestamp")).IsEqualTo(0);
    }

    [Test]
    public async Task MinimumRtt_ExpiredWindowProbeUsesCurrentRttDuration()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 500, 0.005, T0);
        Ack(budget, 500, 0.005, T0 + Seconds(0.051));

        Ack(budget, 10_000, 0.100, T0 + Seconds(10.1));
        Ack(budget, 5_000, 0.050, T0 + Seconds(10.16));

        // The 100ms sample that starts the refresh keeps the one-BDP drain active.
        // Sizing from the stale 5ms minimum would have ended after the 50ms floor and
        // accepted the still-loaded 50ms sample as base RTT, inflating this to 7,500.
        await Assert.That(budget.BudgetBytes).IsEqualTo(500);
    }

    [Test]
    public async Task MinimumRtt_OutlierProbeDurationIsBounded()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 10_000, 0.100, T0);
        Ack(budget, 10_000, 0.100, T0 + Seconds(0.101));
        Ack(budget, 200_000, 2.000, T0 + Seconds(10.2));
        budget.Charge(11_000);

        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.3))).IsTrue();
        await Assert.That(GetField<long>(budget, "_minRttProbeUntilTimestamp"))
            .IsLessThanOrEqualTo(T0 + Seconds(10.45));
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.452))).IsTrue();
    }

    [Test]
    public async Task SetCap_ExpiredMinRttProbeRestoresSafetyFloorWithoutAck()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 10_000, 0.100, T0);
        Ack(budget, 10_000, 0.100, T0 + Seconds(0.101));
        await Assert.That(budget.BudgetBytes).IsEqualTo(10_000);

        Ack(budget, 20_000, 0.200, T0 + Seconds(10.2));
        await Assert.That(budget.BudgetBytes).IsEqualTo(10_000);

        SetCapWithoutDecay(budget, 1_000_000, T0 + Seconds(10.401));

        await Assert.That(budget.BudgetBytes).IsEqualTo(10_000);
    }

    [Test]
    public async Task Admission_ExpiredIdleMinRttProbeUsesSafetyFloorWithoutAck()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 10_000, 0.100, T0);
        Ack(budget, 10_000, 0.100, T0 + Seconds(0.101));
        Ack(budget, 20_000, 0.200, T0 + Seconds(10.2));
        budget.Charge(11_000);

        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.3))).IsTrue();
        await Assert.That(budget.IsOverBudget()).IsTrue();
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.402))).IsTrue();
        await Assert.That(GetField<long>(budget, "_minRttProbeUntilTimestamp"))
            .IsLessThanOrEqualTo(T0 + Seconds(10.4));
    }

    [Test]
    public async Task Admission_ExpiredProbeUsesMinimumObservedDuringProbe()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 10_000, 0.100, T0);
        Ack(budget, 10_000, 0.100, T0 + Seconds(0.101));

        // Refresh the old 100ms minimum, then observe 5ms while the drain probe is open.
        Ack(budget, 20_000, 0.200, T0 + Seconds(10.2));
        Ack(budget, 500, 0.005, T0 + Seconds(10.25));
        SetField(budget, "_retainedLoadedMaxRate", 0.0);
        SetCapWithoutDecay(budget, 1_000_000, T0 + Seconds(10.25));
        budget.Charge(2_000);

        // Once the probe expires without another ack, admission must use the refreshed
        // 5ms minimum (1,000-byte budget), not the stale 100ms minimum (15,000 bytes).
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.401))).IsTrue();
    }

    [Test]
    public async Task MinimumRtt_LateAckAfterEmptyProbeRetainsPriorMinimum()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 500, 0.005, T0);
        Ack(budget, 500, 0.005, T0 + Seconds(0.051));

        Ack(budget, 10_000, 0.100, T0 + Seconds(10.1));
        Ack(budget, 10_000, 0.100, T0 + Seconds(10.25));

        // No ack landed inside the 100ms drain. The late loaded sample must not replace
        // the retained 5ms base RTT. Serving time consumes the end-to-end target, so normal
        // operation retains one base-RTT BDP rather than adding another target-sized flight.
        await Assert.That(budget.MinimumRttMicros).IsEqualTo(5_000);
        await Assert.That(budget.BudgetBytes).IsEqualTo(500);
    }

    [Test]
    public async Task WindowedMaximum_LongGap_ExpiresStaleRate()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        EstablishRate(budget, bytesPerSecond: 3_000, rttSeconds: 0.001);
        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(3_000);

        Ack(budget, 1, 0.001, T0 + Seconds(11.0));

        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(1_000);
    }

    [Test]
    public async Task IdleGap_IsNotCountedAsRequestDrainTime()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        EstablishRate(budget, bytesPerSecond: 100_000, rttSeconds: 0.001);
        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(100_000);

        Ack(budget, 100, 0.001, T0 + Seconds(11.0));
        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(100_000);
    }

    [Test]
    public async Task SubMillisecondRequest_ProducesRateSampleImmediately()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 10, 0.0001, T0);

        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(100_000);
    }

    [Test]
    public async Task AppLimitedShortSample_CannotReplaceEstablishedRate()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.100, T0);
        Ack(budget, 1_000_000, 0.001, T0 + Seconds(0.100));

        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(10_000)
            .Because("a short idle-gap response may seed cold start but cannot ratchet a populated estimator");
    }

    [Test]
    public async Task Budget_RecoversWhenRateRises()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        EstablishRate(budget, bytesPerSecond: 3_000, rttSeconds: 0.001);
        var shrunken = budget.BudgetBytes;

        // Faster drain in the next window must grow the budget back (no ratchet-down).
        Ack(budget, 12_000, 0.100, T0 + Seconds(2.0));

        await Assert.That(budget.BudgetBytes).IsGreaterThan(shrunken);
    }

    [Test]
    public async Task RateSample_UsesRequestBusyTime_NotGapBetweenAckPasses()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.100, T0);

        // 1,000 bytes / 100ms request RTT = 10,000 B/s. The first request is enough to
        // establish rate; no preceding wall-clock ack timestamp is required.
        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(10_000);
    }

    [Test]
    public async Task RateSample_UsesDeliveredDelta_ForPipelinedRequests()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        // Both requests were sent before either delivery.
        var firstSend = budget.SnapshotDelivery(T0 - Seconds(0.100), appLimited: false);
        var secondSend = budget.SnapshotDelivery(T0 - Seconds(0.099), appLimited: false);
        budget.OnAcked(1_000, firstSend, T0);
        budget.OnAcked(1_000, secondSend, T0 + Seconds(0.001));
        CompleteAckedPassWithoutDecay(budget, T0 + Seconds(0.001));

        // The second sample covers the full delivery epoch: 2,000 bytes over 101ms from the
        // first send to the second acknowledgement = 19,801 B/s aggregate drain. The
        // 100ms measured RTT horizon therefore publishes a 1,980-byte budget.
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_980);
    }

    [Test]
    public async Task WindowedMaximum_LowerSamples_DoNotRatchetBudgetDown()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.100, T0);
        Ack(budget, 100, 0.100, T0 + Seconds(0.100));

        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(10_000);
    }

    [Test]
    public async Task WindowedMaximum_PeakExpires_AfterTwoSeconds()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);
        SetField(budget, "_nextProbeTimestamp", T0 + Seconds(100));

        Ack(budget, 1_000, 0.100, T0);
        for (var i = 1; i <= 23; i++)
            Ack(budget, 100, 0.100, T0 + Seconds(i * 0.100));

        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(1_000);
    }

    [Test]
    public async Task LoadedRatePeak_DecaysOverMinutes_NotTwoSeconds()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.100, T0, appLimitedAtSend: false);
        for (var i = 1; i <= 21; i++)
            Ack(budget, 100, 0.100, T0 + Seconds(i * 0.100), appLimitedAtSend: false);

        await Assert.That(budget.MaxRateBytesPerSecond).IsGreaterThan(9_000)
            .Because("a two-second broker stall must not collapse the estimated pipe capacity");
    }

    [Test]
    public async Task LoadedRatePeak_OneMinuteDipDoesNotBecomeNewAdmissionCeiling()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_nextProbeTimestamp", T0 + Seconds(100));

        Ack(budget, 1_000, 0.100, T0, appLimitedAtSend: false);
        for (var second = 2; second <= 60; second += 2)
            Ack(budget, 100, 0.100, T0 + Seconds(second), appLimitedAtSend: false);

        await Assert.That(budget.MaxRateBytesPerSecond).IsGreaterThan(9_000)
            .Because("a transient one-minute rate dip must not self-ratchet a saturated producer");
    }

    [Test]
    public async Task AppLimitedSend_AfterBriefDrainRetainsLoadedRate()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.100, T0, appLimitedAtSend: false);
        Ack(budget, 100, 0.100, T0 + Seconds(2.1), appLimitedAtSend: false);
        await Assert.That(budget.MaxRateBytesPerSecond).IsGreaterThan(9_000);

        Ack(budget, 100, 0.100, T0 + Seconds(3.0), appLimitedAtSend: true);

        await Assert.That(GetField<double>(budget, "_retainedLoadedMaxRate")).IsGreaterThan(0)
            .Because("an instantaneous empty-pipe snapshot is common between loaded bursts");
    }

    [Test]
    public async Task BackToBackAppLimitedSends_EstablishLoadedServiceEstimate()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.100, T0, appLimitedAtSend: true);
        Ack(budget, 1_000, 0.100, T0 + Seconds(0.100), appLimitedAtSend: true);

        await Assert.That(GetField<double>(budget, "_retainedLoadedMaxRate")).IsEqualTo(0)
            .Because("an empty-pipe own-RTT sample must not persist as loaded capacity");
        await Assert.That(GetField<double>(budget, "_windowMaxRate")).IsGreaterThan(0)
            .Because("depth-one traffic still needs immediate windowed budget sizing");
        await Assert.That(GetField<bool>(budget, "_hasLoadedServingSample")).IsTrue()
            .Because("recent delivery still establishes a loaded serving-RTT horizon");
        await Assert.That(GetField<double>(budget, "_servingRttEwmaSeconds")).IsGreaterThan(0);
    }

    [Test]
    public async Task AppLimitedSend_AfterSustainedIdleResetsRetainedLoadedRate()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.100, T0, appLimitedAtSend: false);
        Ack(budget, 100, 0.100, T0 + Seconds(2.1), appLimitedAtSend: false);
        await Assert.That(budget.MaxRateBytesPerSecond).IsGreaterThan(9_000);

        Ack(budget, 100, 0.100, T0 + Seconds(4.3), appLimitedAtSend: true);

        await Assert.That(GetField<double>(budget, "_retainedLoadedMaxRate")).IsEqualTo(0)
            .Because("a full rate-window of true idle must clear stale loaded capacity");
    }

    [Test]
    public async Task MinimumRttProbe_DoesNotLowerServingRttEwma()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_hasMinRttSample", true);
        SetField(budget, "_minRttSeconds", 0.005);
        SetField(budget, "_servingRttEwmaSeconds", 0.100);
        SetField(budget, "_minRttProbeUntilTimestamp", T0 + Seconds(1.0));
        SetField(budget, "_minRttProbeMinimumSeconds", double.MaxValue);

        budget.OnAcked(
            1_000,
            budget.SnapshotDelivery(T0 - Seconds(0.005), appLimited: false),
            T0);

        await Assert.That(GetField<double>(budget, "_servingRttEwmaSeconds")).IsEqualTo(0.100)
            .Because("queue-draining probe RTTs must not erase the loaded serving-RTT floor");
    }

    [Test]
    public async Task MinimumRttProbe_CompletionAck_DoesNotLowerServingRttEwma()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_hasMinRttSample", true);
        SetField(budget, "_minRttSeconds", 0.005);
        SetField(budget, "_servingRttEwmaSeconds", 0.100);
        SetField(budget, "_minRttProbeUntilTimestamp", T0);
        SetField(budget, "_minRttProbeMinimumSeconds", 0.005);

        budget.OnAcked(
            1_000,
            budget.SnapshotDelivery(T0 - Seconds(0.005), appLimited: false),
            T0);

        await Assert.That(GetField<long>(budget, "_minRttProbeUntilTimestamp")).IsEqualTo(0);
        await Assert.That(GetField<double>(budget, "_servingRttEwmaSeconds")).IsEqualTo(0.100)
            .Because("the ack that completes a queue-draining probe is still a probe RTT sample");
    }

    [Test]
    public async Task SetCap_AndAckPass_BothRateLimitBudgetDecay()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_windowMaxRate", 1_000.0);
        SetField(budget, "_lastNormalBudgetBytes", 100_000L);
        SetField(budget, "_lastBudgetUpdateTimestamp", T0);
        budget.SetCap(1_000_000, T0 + Seconds(1.0));
        await Assert.That(budget.BudgetBytes).IsEqualTo(90_000);

        budget.CompleteAckedPass(T0 + Seconds(2.0));
        await Assert.That(budget.BudgetBytes).IsEqualTo(81_000);
    }

    [Test]
    public async Task WindowedMaximum_UsesElapsedTime_NotResponsePassCount()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.100, T0);
        for (var i = 1; i <= 20; i++)
            Ack(budget, 10, 0.025, T0 + Seconds(i * 0.025));

        // Twenty fast acknowledgements cover only 500ms, so the 2-second maximum window
        // must retain the original 10,000 B/s observation.
        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(10_000);

        Ack(budget, 10, 0.025, T0 + Seconds(2.6));

        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(400);
    }

    [Test]
    public async Task PeriodicProbe_CollectsThreeRtts_ThenRevertsWithoutRateGain()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000,
            enableDiagnostics: true);
        budget.Charge(5_000);

        for (var i = 0; i <= 8; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        await Assert.That(budget.BudgetBytes).IsEqualTo(375);
        await Assert.That(GetField<bool>(budget, "_capacityProbeBaselineActive")).IsTrue();

        for (var i = 9; i <= 14; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsTrue();
        await Assert.That(budget.BudgetBytes).IsEqualTo(468);

        for (var i = 15; i <= 20; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        await Assert.That(budget.BudgetBytes).IsEqualTo(375);
        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(0);
        await Assert.That(budget.CapacityProbeFailureCount).IsEqualTo(1);
        await Assert.That(GetField<long>(budget, "_nextProbeTimestamp") - (T0 + Seconds(2.000)))
            .IsGreaterThanOrEqualTo(Seconds(1.0));
        var capacityEvents = budget.CopyProbeEvents()
            .Where(probeEvent => probeEvent.ProbeType == BrokerBudgetProbeType.Capacity)
            .ToArray();
        await Assert.That(capacityEvents.Select(probeEvent => probeEvent.Outcome)).IsEquivalentTo(new[]
        {
            BrokerBudgetProbeOutcome.Started,
            BrokerBudgetProbeOutcome.Failed
        });
    }

    [Test]
    public async Task PeriodicProbe_CapsHighRttWallClockInterval()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 1.0, T0);

        var nextProbeTimestamp = GetField<long>(budget, "_nextProbeTimestamp");
        await Assert.That(nextProbeTimestamp - T0).IsLessThanOrEqualTo(Seconds(1.0));
    }

    [Test]
    public async Task PeriodicProbe_BaselineTimeoutDoesNotReportCandidateFailure()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000,
            enableDiagnostics: true);
        SetField(budget, "_capacityProbeStartTimestamp", T0);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(0.200));
        SetField(budget, "_capacityProbeBaselineActive", true);

        Ack(budget, 1_000, 0.100, T0 + Seconds(0.300));

        await Assert.That(GetField<bool>(budget, "_capacityProbeBaselineActive")).IsFalse();
        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsFalse();
        await Assert.That(budget.CapacityProbeFailureCount).IsEqualTo(0)
            .Because("the enlarged candidate budget never opened");
        await Assert.That(budget.CopyProbeEvents()
                .Where(probeEvent => probeEvent.ProbeType == BrokerBudgetProbeType.Capacity))
            .IsEmpty();
    }

    [Test]
    public async Task PeriodicProbe_MeasuresSettledBaselineBeforeOpeningCandidateBudget()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        Ack(budget, 1_000, 0.008, T0);
        budget.Charge(20_000);

        var probeStart = T0 + Seconds(0.064);
        SetField(budget, "_nextProbeTimestamp", probeStart);
        Ack(budget, 1_000, 0.008, probeStart);

        var normalBudget = GetField<long>(budget, "_budgetAfterMinRttProbeBytes");
        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsFalse()
            .Because("the first phase must observe the current budget before changing the treatment");
        await Assert.That(GetField<bool>(budget, "_capacityProbeBaselineActive")).IsTrue();
        await Assert.That(budget.BudgetBytes).IsEqualTo(normalBudget);

        Ack(budget, 1_000, 0.008, T0 + Seconds(0.073));
        await Assert.That(GetField<int>(budget, "_capacityProbeRateSampleCount")).IsEqualTo(1);
        Ack(budget, 1_000, 0.008, T0 + Seconds(0.123));
        Ack(budget, 1_000, 0.008, T0 + Seconds(0.174));

        await Assert.That(GetField<bool>(budget, "_capacityProbeBaselineActive")).IsFalse();
        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsTrue();
        await Assert.That(GetField<double>(budget, "_capacityProbeBaselineRate"))
            .IsEqualTo(125_000.0).Within(0.001)
            .Because("baseline and candidate must use the same per-request delivery-rate domain");
        await Assert.That(budget.BudgetBytes).IsGreaterThan(normalBudget)
            .Because("only the candidate phase may publish the enlarged budget");
    }

    [Test]
    public async Task PeriodicProbe_UsesMultiRttAverage_NotSinglePeak()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_windowMaxRate", 10_000.0);
        SetField(budget, "_capacityProbeBaselineRate", 10_000.0);
        SetField(budget, "_capacityProbeStartTimestamp", T0);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.0));
        SetField(budget, "_capacityProbeActive", true);

        Ack(budget, 2_000, 0.100, T0 + Seconds(0.100), appLimitedAtSend: false);
        Ack(budget, 500, 0.100, T0 + Seconds(0.200), appLimitedAtSend: false);
        Ack(budget, 500, 0.100, T0 + Seconds(0.300), appLimitedAtSend: false);
        Ack(budget, 500, 0.100, T0 + Seconds(0.400), appLimitedAtSend: false);
        Ack(budget, 500, 0.100, T0 + Seconds(0.500), appLimitedAtSend: false);
        Ack(budget, 500, 0.100, T0 + Seconds(0.600), appLimitedAtSend: false);

        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsFalse()
            .Because("one noisy high sample must not ratchet a three-RTT low-rate probe");
    }

    [Test]
    public async Task PeriodicProbe_SuccessClosesSessionBeforeAnotherRung()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_capacityProbeBaselineRate", 10_000.0);
        SetField(budget, "_capacityProbeStartTimestamp", T0);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.0));
        SetField(budget, "_capacityProbeEvaluationDeadlineTimestamp", T0 + Seconds(0.200));
        SetField(budget, "_capacityProbeRateSum", 42_000.0);
        SetField(budget, "_capacityProbeRateSampleCount", 2);
        SetField(budget, "_capacityProbeActive", true);

        Ack(budget, 1_200, 0.100, T0 + Seconds(0.300), appLimitedAtSend: false);

        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(1);
        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsFalse()
            .Because("a successful sample must not chain another +25% treatment without a settled baseline");
        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(5.0).Within(0.000_001);
        await Assert.That(GetField<long>(budget, "_nextProbeTimestamp") - (T0 + Seconds(0.300)))
            .IsGreaterThanOrEqualTo(Seconds(1.0))
            .Because("scheduler bursts must not become thousands of independent probe votes");
    }

    [Test]
    public async Task PeriodicProbe_RejectsRateGainThatCostsSealToAckEfficiency()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SeedCapacityProbeEvaluation(budget, 10_000, 0.100, 42_000);

        var now = T0 + Seconds(0.300);
        var snapshot = budget.SnapshotDelivery(
            sendTimestamp: now - Seconds(0.100),
            appLimited: false,
            oldestBatchTimestamp: now - Seconds(0.250));
        Ack(budget, 1_200, 0.100, now, snapshot);

        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(0);
        await Assert.That(budget.CapacityProbeFailureCount).IsEqualTo(1);
        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(4.0).Within(0.000_001)
            .Because("a throughput gain that buys proportionally more seal-to-ack latency is queue, not capacity");
    }

    [Test]
    public async Task PeriodicProbe_RejectsSmallRateGainWithLargerMarginalLatencyCost()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SeedCapacityProbeEvaluation(budget, 10_000, 0.100, 20_400);

        var now = T0 + Seconds(0.300);
        var snapshot = budget.SnapshotDelivery(
            sendTimestamp: now - Seconds(0.100),
            appLimited: false,
            oldestBatchTimestamp: now - Seconds(0.108));
        Ack(budget, 1_020, 0.100, now, snapshot);

        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(0);
        await Assert.That(budget.CapacityProbeFailureCount).IsEqualTo(1);
        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(4.0).Within(0.000_001)
            .Because("2% more rate cannot justify 8% more latency");
    }

    [Test]
    public async Task PeriodicProbe_RejectsLowElasticityRateGainEvenWhenLatencyIsFlat()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SeedCapacityProbeEvaluation(budget, 10_000, 0, 20_800);

        Ack(budget, 1_040, 0.100, T0 + Seconds(0.300), appLimitedAtSend: false);

        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(0);
        await Assert.That(budget.CapacityProbeFailureCount).IsEqualTo(1);
        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(4.0).Within(0.000_001)
            .Because("4% more rate does not justify 25% more standing flight");
    }

    [Test]
    public async Task PeriodicProbe_PersistsRateGainWhenSealToAckEfficiencyImproves()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SeedCapacityProbeEvaluation(budget, 10_000, 0.100, 42_000);

        var now = T0 + Seconds(0.300);
        var snapshot = budget.SnapshotDelivery(
            sendTimestamp: now - Seconds(0.100),
            appLimited: false,
            oldestBatchTimestamp: now - Seconds(0.150));
        Ack(budget, 1_200, 0.100, now, snapshot);

        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(1);
        await Assert.That(budget.CapacityProbeFailureCount).IsEqualTo(0);
        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(5.0).Within(0.000_001)
            .Because("higher delivery rate with better rate-to-latency efficiency is usable capacity");
    }

    [Test]
    public async Task PeriodicProbe_RejectsDeliveryLatencyAboveConfiguredTarget()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.200,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SeedCapacityProbeEvaluation(budget, 10_000, 0, 78_000);

        var now = T0 + Seconds(0.300);
        var snapshot = budget.SnapshotDelivery(
            sendTimestamp: now - Seconds(0.250),
            appLimited: false,
            oldestBatchTimestamp: now - Seconds(0.250));
        Ack(budget, 600, 0.250, now, snapshot);

        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(0);
        await Assert.That(budget.CapacityProbeFailureCount).IsEqualTo(1)
            .Because("rate growth cannot persist a rung whose complete delivery latency exceeds the target");
    }

    [Test]
    public async Task PeriodicProbe_SuccessPersistsProvenRequestDepthAfterProbeEnds()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_capacityProbeBaselineRate", 10_000.0);
        SetField(budget, "_capacityProbeStartTimestamp", T0);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.0));
        SetField(budget, "_capacityProbeEvaluationDeadlineTimestamp", T0 + Seconds(0.200));
        SetField(budget, "_capacityProbeRateSum", 42_000.0);
        SetField(budget, "_capacityProbeRateSampleCount", 2);
        SetField(budget, "_capacityProbeActive", true);

        Ack(budget, 1_200, 0.100, T0 + Seconds(0.300), appLimitedAtSend: false);
        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(1);
        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(5.0).Within(0.000_001);

        SetField(budget, "_capacityProbeActive", false);
        SetField(budget, "_capacityProbeDeadlineTimestamp", 0L);
        SetField(budget, "_minRttProbeUntilTimestamp", 0L);
        SetField(budget, "_hasMinRttSample", true);
        SetField(budget, "_minRttSeconds", 0.0001);
        SetField(budget, "_servingRttEwmaSeconds", 0.0001);
        SetField(budget, "_hasLoadedServingSample", true);
        SetField(budget, "_windowMaxRate", 1_000_000.0);
        SetField(budget, "_retainedLoadedMaxRate", 1_000_000.0);
        SetField(budget, "_requestSizeEwmaBytes", 1_000.0);
        SetField(budget, "_admissionPressureActive", false);
        SetField(budget, "_latencyBudgetScale", 1.0);

        SetCapWithoutDecay(budget, 1_000_000, T0 + Seconds(0.301));

        await Assert.That(budget.BudgetBytes).IsEqualTo(5_000)
            .Because("a successful five-request rung must become the normal budget, not a transient probe only");

        SetField(budget, "_latencyBudgetScale", 0.25);
        SetCapWithoutDecay(budget, 1_000_000, T0 + Seconds(0.302));

        await Assert.That(budget.BudgetBytes).IsEqualTo(2_400)
            .Because("latency pressure and serving RTT must still bound previously proven request depth");
    }

    [Test]
    public async Task ProvenRequestDepth_ReservesServingRttWithinLatencyCap()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000,
            initialConnectionCount: 3);
        SetField(budget, "_hasMinRttSample", true);
        SetField(budget, "_minRttSeconds", 0.001);
        SetField(budget, "_servingRttEwmaSeconds", 0.002);
        SetField(budget, "_hasLoadedServingSample", true);
        SetField(budget, "_windowMaxRate", 1_000_000.0);
        SetField(budget, "_retainedLoadedMaxRate", 1_000_000.0);
        SetField(budget, "_requestSizeEwmaBytes", 1_000.0);
        SetField(budget, "_provenPipelineRequestQuanta", 32.0);
        SetField(budget, "_latencyBudgetScale", 1.0);

        SetCapWithoutDecay(budget, 1_000_000, T0);

        await Assert.That(budget.BudgetBytes).IsEqualTo(8_000)
            .Because("the configured horizon includes serving RTT; only the remainder may become standing flight");
    }

    [Test]
    public async Task ProvenRequestDepth_IsSeededByConnectionWidthAndBoundedByWireCap()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 32_000_000);

        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(4.0);

        SetField(budget, "_provenPipelineRequestQuanta", 96.0);
        SetField(budget, "_requestSizeEwmaBytes", 1_000_000.0);
        budget.SetCap(32_000_000, T0, connectionCount: 1);

        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(32.0)
            .Because("marginal delivery efficiency, not a fixed width heuristic, bounds persistence below the wire cap");

        SetField(budget, "_provenPipelineRequestQuanta", 4.0);
        budget.SetCap(96_000_000, T0, connectionCount: 3);

        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(12.0)
            .Because("scale-up must seed four acknowledgement-clock requests per connection");

        SetField(budget, "_provenPipelineRequestQuanta", 96.0);
        budget.SetCap(96_000_000, T0, connectionCount: 3);

        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(96.0)
            .Because("three connections retain at most their full aggregate wire depth");
    }

    [Test]
    public async Task PeriodicProbe_ProofIsNotStoppedByWidthHeuristic()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 32_000_000);
        SetField(budget, "_provenPipelineRequestQuanta", 6.0);
        SetField(budget, "_requestSizeEwmaBytes", 1_000_000.0);
        SetField(budget, "_capacityProbeBaselineRate", 10_000.0);
        SetField(budget, "_capacityProbeStartTimestamp", T0);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.0));
        SetField(budget, "_capacityProbeEvaluationDeadlineTimestamp", T0 + Seconds(0.200));
        SetField(budget, "_capacityProbeRateSum", 42_000.0);
        SetField(budget, "_capacityProbeRateSampleCount", 2);
        SetField(budget, "_capacityProbeActive", true);

        Ack(budget, 1_200, 0.100, T0 + Seconds(0.300), appLimitedAtSend: false);

        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(7.5)
            .Because("the measured delivery signal, not a fixed single-connection ceiling, must stop proof growth");
    }

    [Test]
    public async Task PeriodicProbe_ThreeConnectionProofGrowsPastSingleWidthLimit()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 96_000_000,
            initialConnectionCount: 3);
        SetField(budget, "_provenPipelineRequestQuanta", 24.0);
        SetField(budget, "_requestSizeEwmaBytes", 1_000_000.0);
        SetField(budget, "_capacityProbeBaselineRate", 10_000.0);
        SetField(budget, "_capacityProbeStartTimestamp", T0);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.0));
        SetField(budget, "_capacityProbeEvaluationDeadlineTimestamp", T0 + Seconds(0.200));
        SetField(budget, "_capacityProbeRateSum", 42_000.0);
        SetField(budget, "_capacityProbeRateSampleCount", 2);
        SetField(budget, "_capacityProbeActive", true);

        Ack(budget, 1_200, 0.100, T0 + Seconds(0.300), appLimitedAtSend: false);

        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(30.0)
            .Because("successful three-connection probing must escape the 24-request throughput ceiling");
    }

    [Test]
    public async Task ProvenRequestDepth_ResetsAfterSustainedIdle()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        Ack(budget, 1_000, 0.001, T0);
        SetField(budget, "_provenPipelineRequestQuanta", 10.0);
        SetField(budget, "_nextProbeTimestamp", T0 + Seconds(100));

        var sendTimestamp = T0 + Seconds(3.0);
        var snapshot = budget.SnapshotDelivery(sendTimestamp, appLimited: true);
        Ack(budget, 1_000, 0.001, sendTimestamp + Seconds(0.001), snapshot);

        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(4.0)
            .Because("capacity proven before a sustained idle gap is stale path evidence");
    }

    [Test]
    public async Task BudgetDecay_IsRateLimitedWithoutAdmissionBlocks()
    {
        var budget = BrokerUnackedByteBudget.BoundBudgetDecay(
            previousBudget: 100_000,
            computedBudget: 10_000,
            elapsedSeconds: 1.0);

        await Assert.That(budget).IsEqualTo(90_000);
    }

    [Test]
    public async Task BudgetDecay_FirstRecomputePreservesColdStartBudget()
    {
        var budget = BrokerUnackedByteBudget.BoundBudgetDecay(
            previousBudget: 100_000,
            computedBudget: 10_000,
            elapsedSeconds: 0);

        await Assert.That(budget).IsEqualTo(100_000)
            .Because("the first estimator sample has no elapsed decay interval");
    }

    [Test]
    public async Task BudgetDecay_StandingQueueCannotBypassTimeBound()
    {
        var budget = BrokerUnackedByteBudget.BoundBudgetDecay(
            previousBudget: 100_000,
            computedBudget: 10_000,
            elapsedSeconds: 1.0);

        await Assert.That(budget).IsEqualTo(90_000)
            .Because("standing occupancy must not collapse the budget faster than elapsed time permits");
    }

    [Test]
    public async Task BudgetDecay_SustainedIdleRestoresColdStartBudget()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        var firstSend = budget.SnapshotDelivery(T0 - Seconds(0.001), appLimited: true);
        budget.OnAcked(1, firstSend, T0);
        SetField(budget, "_lastNormalBudgetBytes", 10_000L);
        SetField(budget, "_budgetBytes", 10_000L);

        var idleSend = budget.SnapshotDelivery(T0 + Seconds(3.0), appLimited: true);
        budget.OnAcked(1, idleSend, T0 + Seconds(3.001));
        budget.CompleteAckedPass(T0 + Seconds(3.001));

        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000_000);
    }

    [Test]
    public async Task PeriodicProbe_RejectsRateGainWhenRttInflates()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        SetField(budget, "_windowMaxRate", 10_000.0);
        SetField(budget, "_capacityProbeBaselineRate", 10_000.0);
        SetField(budget, "_capacityProbeStartTimestamp", T0);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(10.0));
        SetField(budget, "_capacityProbeActive", true);
        SetField(budget, "_capacityProbePreProbeRttSeconds", 0.100);

        // Delivered rate rises 25%, but every round trip now takes 1.5x the pre-probe RTT:
        // through a saturated broker the "gain" is standing queue, not headroom.
        Ack(budget, 1_875, 0.150, T0 + Seconds(0.150), appLimitedAtSend: false);
        Ack(budget, 1_875, 0.150, T0 + Seconds(0.300), appLimitedAtSend: false);
        Ack(budget, 1_875, 0.150, T0 + Seconds(0.450), appLimitedAtSend: false);
        Ack(budget, 1_875, 0.150, T0 + Seconds(0.600), appLimitedAtSend: false);

        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsFalse()
            .Because("rate-up with RTT-up is added queueing, not capacity");
    }

    [Test]
    public async Task ServingRttFeedback_DoesNotRatchetBudgetTowardCap()
    {
        // Saturated-broker model (#2009): drain rate is fixed at 1 MB/s, the admitted queue
        // always sits at the published budget, and every loaded round trip measures base RTT
        // (5ms) plus the standing queue's drain time. Uncorrected, budget = 1.5 x rate x
        // (base + budget/rate) has loop gain 1.5 and rides the 32 MB cap; with queue-corrected
        // RTT sensing the 10ms target governs and the budget stays near rate x target.
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 32_000_000);

        var now = T0;
        Ack(budget, 5_000, 0.005, now);
        for (var i = 0; i < 100; i++)
        {
            var standingBytes = Math.Max(0, budget.BudgetBytes - 5_000);
            var rtt = 0.005 + standingBytes / 1_000_000.0;
            now += Seconds(rtt);
            var requestBytes = (long)(1_000_000 * rtt);
            budget.Charge(standingBytes + requestBytes);
            budget.RecordAdmissionBlock();
            Ack(budget, requestBytes, rtt, now, appLimitedAtSend: false);
            budget.Release(standingBytes + requestBytes);
        }

        await Assert.That(budget.BudgetBytes).IsLessThanOrEqualTo(20_000)
            .Because("self-admitted queueing must not masquerade as service time in the RTT floor");
    }

    [Test]
    public async Task BrokerQueueDelayAboveTarget_ShrinksLatencyScale()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 32_000_000);
        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.001);

        // Batches leave the sender 2ms after sealing but sit 50ms at the broker: latency the
        // seal-to-send sample cannot see. No wire-occupancy evidence is required — broker
        // queueing always responds to admission.
        var now = T0;
        for (var i = 0; i < 6; i++)
        {
            now += Seconds(0.110);
            var snapshot = budget.SnapshotDelivery(
                now - Seconds(0.050),
                appLimited: true,
                oldestBatchTimestamp: now - Seconds(0.052));
            Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);
        }

        await Assert.That(budget.LatencyBudgetScale).IsLessThan(0.5)
            .Because("seal-to-ack beyond the base round trip is queueing the budget controls");
    }

    [Test]
    public async Task SendLoopBacklog_WithoutWireOccupancy_DoesNotShrinkScaleOrProof()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.001);
        SetField(budget, "_provenPipelineRequestQuanta", 8.0);

        // 19ms of seal-to-send backlog with an empty wire: the send loop, not broker
        // admission, is the bottleneck. Shrinking would starve the sender below capacity.
        var now = T0;
        for (var i = 0; i < 6; i++)
        {
            now += Seconds(0.110);
            var snapshot = budget.SnapshotDelivery(
                now - Seconds(0.001),
                appLimited: true,
                oldestBatchTimestamp: now - Seconds(0.020));
            Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);
        }

        await Assert.That(budget.LatencyBudgetScale).IsEqualTo(1.0).Within(0.000_001)
            .Because("sender backlog with an underfilled wire is not actionable by admission");
        await Assert.That(budget.ProvenPipelineRequestQuanta).IsEqualTo(8.0).Within(0.000_001)
            .Because("non-actionable sender backlog must not revoke proven broker capacity");
    }

    [Test]
    public async Task AdmissionPressureWithLatencyHeadroom_ProvidesMinimumRequestPipeline()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 32_000_000);
        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.001);
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);

        // The drain estimate only ever measures traffic the gate admitted, so a low-RTT
        // budget smaller than a few request quanta is self-confirming at low throughput.
        // Blocked demand provides a minimum request pipeline without filling the latency
        // target with queueing delay.
        var now = T0;
        for (var i = 0; i < 30; i++)
        {
            now += Seconds(0.110);
            budget.RecordAdmissionBlock();
            var snapshot = budget.SnapshotDelivery(
                now - Seconds(0.001),
                appLimited: true,
                oldestBatchTimestamp: now - Seconds(0.002));
            Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);
        }

        await Assert.That(budget.LatencyBudgetScale).IsEqualTo(1.0).Within(0.000_001);
        await Assert.That(budget.BudgetBytes).IsEqualTo(4_000)
            .Because("four full request quanta keep the acknowledgement clock running without a target-sized queue");
    }

    [Test]
    public async Task MinimumRequestPipeline_IsBoundedByLatencyTarget()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.002,
            floorBytes: 200,
            initialCapBytes: 32_000_000);
        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.001);

        var now = T0;
        for (var i = 0; i < 80; i++)
        {
            now += Seconds(0.110);
            budget.RecordAdmissionBlock();
            var snapshot = budget.SnapshotDelivery(
                now - Seconds(0.001),
                appLimited: true,
                oldestBatchTimestamp: now - Seconds(0.002));
            Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);
        }

        await Assert.That(budget.LatencyBudgetScale).IsEqualTo(1.0).Within(0.000_001);
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000)
            .Because("request granularity must not add standing flight on top of serving RTT");
    }

    [Test]
    public async Task MinimumRequestPipeline_ExpiresWithoutRecentAdmissionPressure()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 32_000_000);
        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.001);

        var now = T0 + Seconds(0.110);
        budget.RecordAdmissionBlock();
        var snapshot = budget.SnapshotDelivery(
            now - Seconds(0.001),
            appLimited: true,
            oldestBatchTimestamp: now - Seconds(0.002));
        Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);
        await Assert.That(budget.BudgetBytes).IsEqualTo(4_000);

        now += Seconds(0.110);
        snapshot = budget.SnapshotDelivery(
            now - Seconds(0.001),
            appLimited: true,
            oldestBatchTimestamp: now - Seconds(0.002));
        Ack(budget, 1_000, rttSeconds: 0, now, snapshotAtSend: snapshot);

        await Assert.That(budget.BudgetBytes).IsEqualTo(1_500)
            .Because("one historical admission block must not retain the request floor forever");
    }

    [Test]
    public async Task PeriodicProbe_RttDominantTarget_ExploresCapacityAboveTargetCap()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.008);
        await Assert.That(GetField<long>(budget, "_budgetAfterMinRttProbeBytes")).IsEqualTo(8_000)
            .Because("one base-RTT BDP remains when serving time consumes most of the end-to-end target");
        budget.Charge(20_000);

        for (var i = 1; i <= 22; i++)
            Ack(budget, 8_000, 0.008, T0 + Seconds(i * 0.008));

        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsTrue();
        var normalBudget = GetField<long>(budget, "_budgetAfterMinRttProbeBytes");
        await Assert.That(budget.BudgetBytes).IsEqualTo((long)(normalBudget * 1.25))
            .Because("the periodic probe must explore 25% beyond an RTT-dominant target cap");

        for (var i = 23; i <= 36; i++)
            Ack(budget, 10_000, 0.008, T0 + Seconds(i * 0.008));

        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(1)
            .Because("flat-RTT rate gain above the target cap must remain discoverable");
        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsFalse()
            .Because("the next rung requires a newly settled baseline");
    }

    [Test]
    public async Task PeriodicProbe_CollectsFullRttBeforeJudgingRateGain()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);
        budget.Charge(5_000);

        for (var i = 0; i <= 7; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        SetField(budget, "_capacityProbeBaselineRate", 10_000.0);
        SetField(budget, "_capacityProbePreProbeRttSeconds", 0.100);
        SetField(budget, "_capacityProbeStartTimestamp", T0 + Seconds(0.800));
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.600));
        SetField(budget, "_capacityProbeActive", true);
        CompleteAckedPassWithoutDecay(budget, T0 + Seconds(0.800));

        // One small response can arrive first after the larger budget is published.
        // It must not cancel the probe before other requests from the same RTT land.
        Ack(budget, 500, 0.100, T0 + Seconds(0.900));
        await Assert.That(budget.BudgetBytes).IsEqualTo(468);

        Ack(budget, 1_250, 0.100, T0 + Seconds(0.950));
        await Assert.That(budget.BudgetBytes).IsEqualTo(585);
    }

    [Test]
    public async Task PeriodicProbe_FastGateFallsThroughForDeadlineCheck()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        budget.Charge(5_500);

        for (var i = 0; i <= 7; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        SetField(budget, "_capacityProbeStartTimestamp", T0 + Seconds(0.800));
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.600));
        SetField(budget, "_capacityProbeActive", true);
        CompleteAckedPassWithoutDecay(budget, T0 + Seconds(0.800));

        await Assert.That(budget.BudgetBytes).IsEqualTo(468);
        budget.Release(5_100);
        await Assert.That(budget.IsOverBudget()).IsTrue()
            .Because("occupancy above the normal budget must reach the deadline-aware gate");
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(0.900))).IsFalse()
            .Because("the active probe still admits against its larger budget");
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(1.601))).IsTrue()
            .Because("the expired probe must fall back without requiring another ack");
    }

    [Test]
    public async Task ExpiredCapacityProbe_DoesNotOverrideActiveMinimumRttProbe()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000);
        budget.Charge(1_000);
        SetField(budget, "_budgetBytes", 500L);
        SetField(budget, "_budgetAfterMinRttProbeBytes", 5_000L);
        SetField(budget, "_capacityProbeActive", true);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(0.010));
        SetField(budget, "_minRttProbeUntilTimestamp", T0 + Seconds(0.050));

        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(0.020))).IsTrue()
            .Because("an active minimum-RTT probe must retain its one-BDP drain budget");
    }

    [Test]
    public async Task PeriodicProbe_WaitsForSampleWhollyAdmittedUnderProbe()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);
        budget.Charge(5_000);

        for (var i = 0; i <= 7; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        SetField(budget, "_capacityProbeBaselineRate", 10_000.0);
        SetField(budget, "_capacityProbeStartTimestamp", T0 + Seconds(0.800));
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(2.000));
        SetField(budget, "_capacityProbeActive", true);
        CompleteAckedPassWithoutDecay(budget, T0 + Seconds(0.800));

        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsTrue();
        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(0);

        // This request began before the probe opened at 800ms, so it cannot confirm
        // capacity under the larger admission budget even though it completes later.
        Ack(budget, 1_000, 0.200, T0 + Seconds(0.900));
        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsTrue();

        // First request wholly admitted under the probe starts three RTTs of measurement.
        Ack(budget, 1_000, 0.100, T0 + Seconds(1.000));
        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsTrue();

        Ack(budget, 1_000, 0.100, T0 + Seconds(1.100));
        Ack(budget, 1_000, 0.100, T0 + Seconds(1.200));
        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsTrue();

        // Target horizon is longer than three RTTs, so measurement continues until 500ms
        // after the first wholly admitted response.
        Ack(budget, 1_000, 0.100, T0 + Seconds(1.300));
        Ack(budget, 1_000, 0.100, T0 + Seconds(1.400));
        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsTrue();

        Ack(budget, 1_000, 0.100, T0 + Seconds(1.500));
        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsFalse();
        await Assert.That(budget.CapacityProbeFailureCount).IsEqualTo(1);
    }

    [Test]
    public async Task PeriodicProbe_RejectsBatchAdmittedBeforeProbeEvenWhenSentAfterProbe()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);
        SetField(budget, "_capacityProbeBaselineRate", 10_000.0);
        SetField(budget, "_capacityProbeStartTimestamp", T0);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.0));
        SetField(budget, "_capacityProbeActive", true);

        var snapshot = budget.SnapshotDelivery(
            sendTimestamp: T0 + Seconds(0.010),
            appLimited: true,
            oldestBatchTimestamp: T0 - Seconds(0.010));
        Ack(budget, 200, 0.001, T0 + Seconds(0.011), snapshot);

        await Assert.That(GetField<int>(budget, "_capacityProbeRateSampleCount")).IsEqualTo(0)
            .Because("a send after probe start can still contain work admitted under the old budget");
    }

    [Test]
    public async Task PeriodicProbe_BaselineRejectsBatchAdmittedBeforeControlWindow()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);
        SetField(budget, "_capacityProbeStartTimestamp", T0);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.0));
        SetField(budget, "_capacityProbeBaselineActive", true);

        var snapshot = budget.SnapshotDelivery(
            sendTimestamp: T0 + Seconds(0.010),
            appLimited: true,
            oldestBatchTimestamp: T0 - Seconds(0.010));
        Ack(budget, 200, 0.001, T0 + Seconds(0.011), snapshot);

        await Assert.That(GetField<int>(budget, "_capacityProbeRateSampleCount")).IsEqualTo(0)
            .Because("the control window must exclude the same pre-phase admissions as the treatment window");
    }

    [Test]
    public async Task PeriodicProbe_EvaluationWindowCoversTargetHorizonAndLinger()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000,
            lingerSeconds: 0.005);
        SetField(budget, "_capacityProbeBaselineRate", 10_000.0);
        SetField(budget, "_capacityProbeStartTimestamp", T0);
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.0));
        SetField(budget, "_capacityProbeActive", true);

        var now = T0 + Seconds(0.020);
        var snapshot = budget.SnapshotDelivery(
            sendTimestamp: now - Seconds(0.001),
            appLimited: true,
            oldestBatchTimestamp: T0 + Seconds(0.001));
        Ack(budget, 20, 0.001, now, snapshot);

        var evaluationDeadline = GetField<long>(budget, "_capacityProbeEvaluationDeadlineTimestamp");
        await Assert.That(evaluationDeadline - now).IsGreaterThanOrEqualTo(Seconds(0.100))
            .Because("probe-added admissions need a scheduler-noise-resistant observation horizon");
    }

    [Test]
    public async Task PeriodicProbe_BaselineIgnoresDifferentDomainRollingRateWindow()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);
        Ack(budget, 1_000, 0.008, T0);
        budget.Charge(20_000);
        SetField(budget, "_windowMaxRate", 10_000_000.0);
        SetField(budget, "_retainedLoadedMaxRate", 10_000_000.0);
        SetField(budget, "_nextProbeTimestamp", T0 + Seconds(0.064));

        Ack(budget, 1_000, 0.008, T0 + Seconds(0.064));
        Ack(budget, 1_000, 0.008, T0 + Seconds(0.073));
        Ack(budget, 1_000, 0.008, T0 + Seconds(0.123));
        Ack(budget, 1_000, 0.008, T0 + Seconds(0.174));

        await Assert.That(GetField<double>(budget, "_capacityProbeBaselineRate"))
            .IsEqualTo(125_000.0).Within(0.001)
            .Because("the control and treatment windows must use identical measurements");
    }

    [Test]
    public async Task PeriodicProbe_DoesNotArmWhileAdmissionGateHasNoDemand()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);
        Ack(budget, 10, 0.001, T0);
        SetField(budget, "_nextProbeTimestamp", T0 + Seconds(0.008));

        Ack(budget, 10, 0.001, T0 + Seconds(0.008));

        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsFalse()
            .Because("raising an underfilled gate cannot reveal delivery headroom");
        await Assert.That(budget.CapacityProbeFailureCount).IsEqualTo(0)
            .Because("lack of demand is not evidence that a capacity probe failed");
    }

    [Test]
    public async Task PeriodicProbe_NextRungRequiresFreshSettledBaseline()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);
        budget.Charge(5_000);
        SeedCapacityProbeEvaluation(budget, 10_000, 0, 42_000);

        Ack(budget, 1_200, 0.100, T0 + Seconds(0.300), appLimitedAtSend: false);
        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(1);
        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(5.0).Within(0.000_001);

        Ack(budget, 1_250, 0.100, T0 + Seconds(1.300));
        await Assert.That(GetField<bool>(budget, "_capacityProbeBaselineActive")).IsTrue();
        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsFalse();

        for (var i = 14; i <= 19; i++)
            Ack(budget, 1_250, 0.100, T0 + Seconds(i * 0.100));

        await Assert.That(GetField<bool>(budget, "_capacityProbeActive")).IsTrue();
        await Assert.That(GetField<double>(budget, "_capacityProbeBaselineRate"))
            .IsEqualTo(12_500.0).Within(0.001);

        for (var i = 20; i <= 25; i++)
            Ack(budget, 1_563, 0.100, T0 + Seconds(i * 0.100));

        await Assert.That(budget.CapacityProbeSuccessCount).IsEqualTo(2);
        await Assert.That(budget.CapacityProbeFailureCount).IsEqualTo(0);
        await Assert.That(GetField<double>(budget, "_provenPipelineRequestQuanta"))
            .IsEqualTo(6.25).Within(0.000_001)
            .Because("two independently baselined 25% gains remain discoverable");
    }

    [Test]
    public async Task OccupancyFloor_RetainsHeadroomAboveDemonstratedWireDemand()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.ObserveWrittenUnackedBytes(20_000);
        Ack(budget, 100, 0.100, T0);
        Ack(budget, 100, 0.100, T0 + Seconds(0.101));

        // Rate budget = 1,000 B/s × max(10ms, 1.5 × 100ms) = 150 bytes. The demonstrated
        // Occupancy is admission-bounded by the budget itself. It must never inflate the
        // latency-governed rate budget; the configured 200-byte minimum wins here.
        await Assert.That(budget.BudgetBytes).IsEqualTo(200);
    }

    [Test]
    public async Task MinimumRttProbe_IgnoresRecentOccupancyUntilQueueDrains()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.ObserveWrittenUnackedBytes(20_000);
        Ack(budget, 100, 0.100, T0);

        await Assert.That(budget.BudgetBytes).IsEqualTo(200)
            .Because("minimum-RTT probes must publish a one-BDP budget that drains standing queueing");

        budget.ObserveWrittenUnackedBytes(20_000);
        Ack(budget, 100, 0.100, T0 + Seconds(0.101));

        await Assert.That(budget.BudgetBytes).IsEqualTo(200);
    }

    [Test]
    public async Task OccupancyFloor_ExpiresAfterTwoSeconds()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.ObserveWrittenUnackedBytes(20_000);
        Ack(budget, 100, 0.100, T0);
        Ack(budget, 100, 0.100, T0 + Seconds(2.1));

        await Assert.That(budget.BudgetBytes).IsEqualTo(200);
    }

    [Test]
    public async Task OccupancyFloor_IsBoundedByRateBudget_NoFeedbackRatchet()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        // 100,000 B/s drain at negligible RTT => the configured 200-byte floor. Feed the
        // published budget back as observed occupancy each round — the exact feedback
        // shape that previously ratcheted the budget to its cap.
        Ack(budget, 100, 0.001, T0);
        for (var i = 1; i <= 5; i++)
        {
            budget.ObserveWrittenUnackedBytes(budget.BudgetBytes);
            Ack(budget, 100, 0.001, T0 + Seconds(i * 0.100));
        }

        // Fixed point is the floor, not a multiple of it or the 1,000,000-byte cap.
        await Assert.That(budget.BudgetBytes).IsEqualTo(200);
    }

    [Test]
    public async Task HotPollPasses_BackToBackAcks_MeasureAggregateRate_NotInterPassGap()
    {
        // The stress-run failure shape (#1941): five pipelined 1 MB requests all sent
        // before any delivery, whose responses drain in back-to-back hot-poll passes
        // microseconds apart. Interval math based on inter-pass gaps read TB/s here.
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 200_000_000);

        var sendSnapshots = new BrokerUnackedByteBudget.DeliverySnapshot[5];
        for (var i = 0; i < 5; i++)
            sendSnapshots[i] = budget.SnapshotDelivery(T0 + Seconds(i * 0.00001), appLimited: false);
        for (var i = 0; i < 5; i++)
            budget.OnAcked(1_000_000, sendSnapshots[i], T0 + Seconds(0.100 + i * 0.00001));
        CompleteAckedPassWithoutDecay(budget, T0 + Seconds(0.100 + 4 * 0.00001));

        // Each sample measures the cumulative delivered delta over its own ~100ms sojourn;
        // the last one reads the true aggregate drain of ~50 MB/s.
        await Assert.That(budget.MaxRateBytesPerSecond).IsGreaterThanOrEqualTo(49_000_000);
        await Assert.That(budget.MaxRateBytesPerSecond).IsLessThanOrEqualTo(51_000_000);

        // The initial minimum-RTT probe now retains one BDP instead of draining the pipe;
        // this remains far below the cap the broken estimator pegged.
        await Assert.That(budget.BudgetBytes).IsEqualTo(4_998_000);
    }

    [Test]
    public async Task TailOfBurstAck_UsesSendAxisAcrossDeliveryEpoch()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 200_000_000);
        var sendSnapshots = new BrokerUnackedByteBudget.DeliverySnapshot[5];

        // All requests enter the same delivery epoch before any response arrives. The tail
        // request has only a 0.5 ms own RTT, so using own RTT alone reports 10 GB/s for the
        // short burst. The mature sample must average the full epoch instead.
        for (var i = 0; i < sendSnapshots.Length; i++)
        {
            sendSnapshots[i] = budget.SnapshotDelivery(
                T0 + Seconds(i * 0.001),
                appLimited: false);
        }

        for (var i = 0; i < sendSnapshots.Length; i++)
        {
            budget.OnAcked(
                1_000_000,
                sendSnapshots[i],
                T0 + Seconds(0.0041 + i * 0.0001));
        }
        var matureEpochSend = budget.SnapshotDelivery(T0 + Seconds(0.005), appLimited: false);
        budget.OnAcked(1_000_000, matureEpochSend, T0 + Seconds(0.100));
        budget.CompleteAckedPass(T0 + Seconds(0.100));

        await Assert.That(budget.MaxRateBytesPerSecond).IsGreaterThanOrEqualTo(59_000_000);
        await Assert.That(budget.MaxRateBytesPerSecond).IsLessThanOrEqualTo(61_000_000)
            .Because("a cumulative delivery delta must include its delivery epoch's full elapsed time");
    }

    [Test]
    public async Task LoadedDeliveryEpoch_SpansAcknowledgementsUntilPipeBecomesAppLimited()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 200_000_000);

        var firstSend = budget.SnapshotDelivery(T0, appLimited: true);
        budget.OnAcked(1_000, firstSend, T0 + Seconds(0.005));

        var loadedSend = budget.SnapshotDelivery(T0 + Seconds(0.006), appLimited: false);
        await Assert.That(loadedSend.DeliveredBytes).IsEqualTo(1_000);
        await Assert.That(loadedSend.DeliveryEpochDeliveredBytes).IsEqualTo(0)
            .Because("delivery progress must not reset an epoch while the pipe remains loaded");
        await Assert.That(loadedSend.DeliveryEpochFirstSendTimestamp).IsEqualTo(T0);

        var appLimitedSend = budget.SnapshotDelivery(T0 + Seconds(0.007), appLimited: true);
        await Assert.That(appLimitedSend.DeliveryEpochDeliveredBytes).IsEqualTo(1_000);
        await Assert.That(appLimitedSend.DeliveryEpochFirstSendTimestamp)
            .IsEqualTo(T0 + Seconds(0.007))
            .Because("an empty-pipe send starts a new delivery epoch");
    }

    [Test]
    public async Task LoadedDeliveryEpoch_RollsAfterTwoHundredFiftyMilliseconds()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 200_000_000);

        var firstSend = budget.SnapshotDelivery(T0, appLimited: true);
        budget.OnAcked(1_000, firstSend, T0 + Seconds(0.005));

        var nextEpochTimestamp = T0 + Seconds(0.251);
        var loadedSend = budget.SnapshotDelivery(nextEpochTimestamp, appLimited: false);

        await Assert.That(loadedSend.DeliveryEpochDeliveredBytes).IsEqualTo(1_000);
        await Assert.That(loadedSend.DeliveryEpochFirstSendTimestamp).IsEqualTo(nextEpochTimestamp)
            .Because("a continuously loaded producer must still adapt to capacity changes");
    }

    [Test]
    public async Task BackToBackAppLimitedSend_ContinuesRollingRateEpoch()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 200_000_000);

        var firstSend = budget.SnapshotDelivery(T0, appLimited: true);
        budget.OnAcked(1_000, firstSend, T0 + Seconds(0.0005));

        var hotSend = budget.SnapshotDelivery(T0 + Seconds(0.0006), appLimited: true);
        await Assert.That(hotSend.AppLimited).IsTrue();
        await Assert.That(hotSend.RateAppLimited).IsFalse()
            .Because("an immediate post-ack send is a serialized loaded rate cycle");
        await Assert.That(hotSend.DeliveryEpochDeliveredBytes).IsEqualTo(0);
        await Assert.That(hotSend.DeliveryEpochFirstSendTimestamp).IsEqualTo(T0);
        budget.OnAcked(1_000, hotSend, T0 + Seconds(0.0011));

        var idleSend = budget.SnapshotDelivery(T0 + Seconds(0.004), appLimited: true);
        await Assert.That(idleSend.RateAppLimited).IsTrue();
        await Assert.That(idleSend.DeliveryEpochDeliveredBytes).IsEqualTo(2_000);
        await Assert.That(idleSend.DeliveryEpochFirstSendTimestamp)
            .IsEqualTo(T0 + Seconds(0.004))
            .Because("a real empty-pipe gap starts a fresh rate epoch");
    }

    [Test]
    public async Task LoadedDeliveryEpoch_RejectsShortBurstThenMeasuresCumulativeRate()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 200_000_000);

        var firstSend = budget.SnapshotDelivery(T0, appLimited: false);
        budget.OnAcked(1_000_000, firstSend, T0 + Seconds(0.0005));
        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(0)
            .Because("a short loaded sample is dominated by scheduler and burst quantization");

        var secondSend = budget.SnapshotDelivery(T0 + Seconds(0.0006), appLimited: false);
        budget.OnAcked(1_000_000, secondSend, T0 + Seconds(0.0025));
        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(0);

        var thirdSend = budget.SnapshotDelivery(T0 + Seconds(0.003), appLimited: false);
        budget.OnAcked(1_000_000, thirdSend, T0 + Seconds(0.020));
        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(0);

        var fourthSend = budget.SnapshotDelivery(T0 + Seconds(0.021), appLimited: false);
        budget.OnAcked(1_000_000, fourthSend, T0 + Seconds(0.100));

        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(40_000_000)
            .Because("the accepted sample covers 4MB over the full 100ms loaded epoch");
    }

    [Test]
    public async Task AckAxis_LoadedGapSpreadsDeliveryOverArrivalTime()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 10, 0.005, T0);

        // The pipe was loaded at send (not app-limited) but nothing was delivered for
        // 500ms; the 100,000-byte delivery must average over the wall time it actually
        // took to arrive, not its own 5ms sojourn (which would read 20 MB/s).
        Ack(budget, 100_000, 0.005, T0 + Seconds(0.5), appLimitedAtSend: false);

        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(200_000);
    }

    [Test]
    public async Task AppLimitedSend_AfterIdle_UsesSojournAxis_NotIdleGap()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 10, 0.005, T0);

        // The pipe was empty at send: the 5-second idle gap is not drain time, so the
        // sample uses the request's own 5ms sojourn (20 MB/s, budget clamps to cap).
        Ack(budget, 100_000, 0.005, T0 + Seconds(5.0));

        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(20_000_000);
    }

    [Test]
    public async Task RequestHistograms_RecordSizeAndRttBuckets()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        // 100,000 bytes => log2 = 16, shifted by 8 => size bucket 8.
        // 1ms RTT = 1,000 µs => log2 = 9 => RTT bucket 9.
        Ack(budget, 100_000, 0.001, T0);

        var sizeHistogram = budget.CopyRequestSizeHistogram();
        var rttHistogram = budget.CopyRequestRttMicrosHistogram();

        await Assert.That(sizeHistogram[8]).IsEqualTo(1);
        await Assert.That(sizeHistogram.Sum()).IsEqualTo(1);
        await Assert.That(rttHistogram[9]).IsEqualTo(1);
        await Assert.That(rttHistogram.Sum()).IsEqualTo(1);
    }

    [Test]
    public async Task PeriodicProbe_WithoutFurtherAck_RevertsAfterEightRtts()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);
        budget.Charge(5_500);

        for (var i = 0; i <= 7; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        SetField(budget, "_capacityProbeStartTimestamp", T0 + Seconds(0.800));
        SetField(budget, "_capacityProbeDeadlineTimestamp", T0 + Seconds(1.600));
        SetField(budget, "_capacityProbeActive", true);
        CompleteAckedPassWithoutDecay(budget, T0 + Seconds(0.800));

        budget.Release(5_100);
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(0.900))).IsFalse();
        await Assert.That(budget.GetAdmissionRecheckDelayMilliseconds(T0 + Seconds(0.900)))
            .IsEqualTo(700);
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(1.601))).IsTrue();

        budget.Release(400);
    }

    [Test]
    public async Task AckPassGap_DoesNotChangeRequestRateSample()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.010, T0);
        Ack(budget, 1_000, 0.010, T0 + Seconds(1.0));

        await Assert.That(budget.MaxRateBytesPerSecond).IsEqualTo(100_000);
    }

    [Test]
    public async Task SetCap_RecomputesAndRepublishesBudget()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 3_200);

        // Cold start follows the cap in both directions.
        budget.SetCap(6_400, T0);
        await Assert.That(budget.BudgetBytes).IsEqualTo(6_400);

        // With a rate established, a cap below the computed budget clamps it...
        EstablishRate(budget, bytesPerSecond: 3_000, rttSeconds: 0.5); // budget 1,500
        budget.SetCap(1_000, T0);
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);

        // ...and raising the cap re-releases the computed value.
        budget.SetCap(6_400, T0);
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_500);
    }

    [Test]
    public async Task ChargeRelease_IsExactlyBalanced_UnderConcurrency()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 3_200);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 10_000; i++)
            {
                budget.Charge(17);
                budget.Release(17);
            }
        })));

        await Assert.That(budget.UnackedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task DeliverySnapshots_NeverRegressUnderConcurrentAcknowledgements()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 3_200);
        using var start = new Barrier(participantCount: 9);
        var regressionCount = 0;

        var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            start.SignalAndWait();
            var previousDeliveredBytes = 0L;
            for (var i = 0; i < 100_000; i++)
            {
                var snapshot = budget.SnapshotDelivery(i + 1, appLimited: false);
                if (snapshot.DeliveredBytes < previousDeliveredBytes)
                    Interlocked.Increment(ref regressionCount);
                previousDeliveredBytes = snapshot.DeliveredBytes;
            }
        })).ToArray();

        start.SignalAndWait();
        for (var i = 0; i < 50_000; i++)
        {
            var now = T0 + i + 1;
            budget.OnAcked(
                1,
                new BrokerUnackedByteBudget.DeliverySnapshot(
                    i, 0, i, 0, now - 1, true, true, 0, 0),
                now);
        }

        await Task.WhenAll(readers);
        await Assert.That(regressionCount).IsEqualTo(0)
            .Because("a completed send snapshot cannot predate an earlier snapshot on the same connection");
    }

    [Test]
    [Timeout(30_000)]
    public async Task DeliveryEpochSnapshots_NeverMixConcurrentPublisherGenerations(
        CancellationToken ct)
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 3_200);
        const int publisherIterations = 100_000;
        const long deliveryEpochUpdating = -2;
        using var start = new Barrier(participantCount: 5);
        var publisherComplete = 0;
        var mixedGenerationCount = 0;

        SetField(budget, "_totalDeliveredBytes", 0L);
        SetField(budget, "_sendEpochDeliveredBytes", 0L);
        SetField(budget, "_sendEpochFirstTimestamp", 0L);

        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            start.SignalAndWait(ct);
            while (!ct.IsCancellationRequested && Volatile.Read(ref publisherComplete) == 0)
            {
                var snapshot = budget.SnapshotDelivery(sendTimestamp: 1, appLimited: false);
                if (snapshot.DeliveryEpochFirstSendTimestamp
                    != snapshot.DeliveryEpochDeliveredBytes * 10)
                {
                    Interlocked.Increment(ref mixedGenerationCount);
                }
            }
        }, ct)).ToArray();

        start.SignalAndWait(ct);
        for (var generation = 1; generation <= publisherIterations; generation++)
        {
            SetField(budget, "_totalDeliveredBytes", (long)generation);
            SetField(budget, "_sendEpochDeliveredBytes", deliveryEpochUpdating);
            SetField(budget, "_sendEpochFirstTimestamp", generation * 10L);
            Thread.Yield();
            SetField(budget, "_sendEpochDeliveredBytes", (long)generation);
        }
        Volatile.Write(ref publisherComplete, 1);

        await Task.WhenAll(readers).WaitAsync(ct);
        await Assert.That(mixedGenerationCount).IsEqualTo(0)
            .Because("the delivered-byte and first-send anchors belong to one publication");
    }

    [Test]
    public async Task DeliveryEpoch_RedundantPublisherPreservesFirstSendAnchor()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 3_200);
        const long deliveredBytes = 100;
        const long firstSendTimestamp = 6_000;
        const long stalePublisherTimestamp = 7_000;
        const long deliveryEpochUpdating = -2;

        SetField(budget, "_totalDeliveredBytes", deliveredBytes);
        SetField(budget, "_sendEpochDeliveredBytes", deliveryEpochUpdating);
        SetField(budget, "_sendEpochFirstTimestamp", firstSendTimestamp);

        var arguments = new object?[] { stalePublisherTimestamp, deliveredBytes, null, null };
        var anchor = (long)typeof(BrokerUnackedByteBudget)
            .GetMethod("PublishDeliveryEpoch", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(budget, arguments)!;

        await Assert.That(anchor).IsEqualTo(firstSendTimestamp);
        await Assert.That(arguments[2]).IsEqualTo(deliveredBytes);
        await Assert.That(arguments[3]).IsEqualTo(deliveredBytes);
        await Assert.That(GetField<long>(budget, "_sendEpochFirstTimestamp")).IsEqualTo(firstSendTimestamp);
        await Assert.That(GetField<long>(budget, "_sendEpochDeliveredBytes")).IsEqualTo(deliveredBytes);
    }

    [Test]
    public async Task IsOverBudget_TransitionsWithChargesAndReleases()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 100, initialCapBytes: 100);

        await Assert.That(budget.IsOverBudget()).IsFalse();

        budget.Charge(100);
        await Assert.That(budget.IsOverBudget()).IsTrue();

        budget.Release(1);
        await Assert.That(budget.IsOverBudget()).IsFalse();
        budget.Release(99);
    }

    [Test]
    public async Task RecordAdmissionBlock_CountsEvents()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 3_200);

        await Assert.That(budget.AdmissionBlockEvents).IsEqualTo(0);

        budget.RecordAdmissionBlock();
        budget.RecordAdmissionBlock();

        await Assert.That(budget.AdmissionBlockEvents).IsEqualTo(2);
    }

    [Test]
    public async Task AdmissionBlockDiagnostics_RecordContiguousBlockedDuration()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 3_200,
            enableDiagnostics: true);

        budget.RecordAdmissionBlock(T0);
        budget.RecordAdmissionBlock(T0 + Seconds(0.002));
        budget.CompleteAckedPass(T0 + Seconds(0.008));

        var histogram = budget.CopyAdmissionBlockMicrosHistogram();

        await Assert.That(histogram.Sum()).IsEqualTo(1);
        await Assert.That(histogram[12]).IsEqualTo(1)
            .Because("8,000 microseconds belongs to log2 bucket 12");
        await Assert.That(budget.GetCurrentAdmissionBlockDurationMicros(T0 + Seconds(1))).IsEqualTo(0);
    }

    [Test]
    public async Task AdmissionBlockDiagnostics_DisabledDoesNotTouchTrackingState()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 3_200);
        SetField(budget, "_admissionBlockedSinceTimestamp", T0);

        budget.CompleteAckedPass(T0 + Seconds(0.008));

        await Assert.That(budget.CopyAdmissionBlockMicrosHistogram()).IsEmpty();
        await Assert.That(GetField<long>(budget, "_admissionBlockedSinceTimestamp")).IsEqualTo(T0);
    }

    [Test]
    public async Task AdmissionBlockDiagnostics_ResetPreservesOpenEpisodeFromBoundary()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 3_200,
            enableDiagnostics: true);

        budget.RecordAdmissionBlock(T0);
        budget.ResetDiagnostics(T0 + Seconds(0.005));

        await Assert.That(budget.GetCurrentAdmissionBlockDurationMicros(T0 + Seconds(0.013)))
            .IsEqualTo(8_000).Within(1);

        budget.CompleteAckedPass(T0 + Seconds(0.013));
        var histogram = budget.CopyAdmissionBlockMicrosHistogram();

        await Assert.That(histogram.Sum()).IsEqualTo(1);
        await Assert.That(histogram[12]).IsEqualTo(1);
    }

    [Test]
    public async Task MinimumRttProbeDiagnostics_RecordWindowBoundaries()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.010,
            floorBytes: 200,
            initialCapBytes: 1_000_000,
            enableDiagnostics: true);

        Ack(budget, 500, 0.005, T0);
        Ack(budget, 500, 0.005, T0 + Seconds(0.051));

        var events = budget.CopyProbeEvents();

        await Assert.That(events).Count().IsEqualTo(2);
        await Assert.That(events[0].ProbeType).IsEqualTo(BrokerBudgetProbeType.MinimumRtt);
        await Assert.That(events[0].Outcome).IsEqualTo(BrokerBudgetProbeOutcome.Started);
        await Assert.That(events[1].Outcome).IsEqualTo(BrokerBudgetProbeOutcome.Succeeded);
        await Assert.That(events[1].DurationMilliseconds).IsBetween(50, 51);
    }
}
