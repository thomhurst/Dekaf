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
        => budget.OnAcked(
            ackedBytes,
            snapshotAtSend ?? budget.SnapshotDelivery(nowTicks - Seconds(rttSeconds), appLimitedAtSend),
            nowTicks);

    /// <summary>
    /// Establishes a per-request drain-rate sample of <paramref name="bytesPerSecond"/>.
    /// </summary>
    private static void EstablishRate(BrokerUnackedByteBudget budget, long bytesPerSecond, double rttSeconds)
    {
        Ack(budget, (long)(bytesPerSecond * rttSeconds), rttSeconds, T0);
    }

    [Test]
    public async Task Budget_BeforeFirstRateSample_EqualsCap()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 3_200);

        await Assert.That(budget.BudgetBytes).IsEqualTo(3_200);
    }

    [Test]
    public async Task Budget_AfterRateSample_TracksTargetTimesRate()
    {
        // 3,000 B/s at negligible RTT with a 0.5s target => budget 1,500, inside [200, 3,200].
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 3_200);

        EstablishRate(budget, bytesPerSecond: 3_000, rttSeconds: 0.001);

        await Assert.That(budget.BudgetBytes).IsEqualTo(1_500);
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

        // 1,000,000 B/s × 0.5s = 500,000 bytes — far above the cap.
        EstablishRate(budget, bytesPerSecond: 1_000_000, rttSeconds: 0.001);

        await Assert.That(budget.BudgetBytes).IsEqualTo(3_200);
    }

    [Test]
    public async Task Budget_RttGuard_DominatesWhenRttExceedsTarget()
    {
        // Target 10ms but the measured round-trip is 100ms: the guard keeps the budget at
        // 1.5 × BDP (rate × RTT) so throughput is never window-limited.
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        EstablishRate(budget, bytesPerSecond: 100_000, rttSeconds: 0.100);
        Ack(budget, 10_000, 0.100, T0 + Seconds(0.101));

        // 100,000 B/s × max(0.010, 1.5 × 0.100) = 15,000 bytes (±tick-quantization of the RTT).
        await Assert.That(budget.BudgetBytes).IsGreaterThanOrEqualTo(14_990);
        await Assert.That(budget.BudgetBytes).IsLessThanOrEqualTo(15_010);
    }

    [Test]
    public async Task MinimumRtt_LoadedSamplesDoNotInflateBudget()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 500, 0.005, T0);
        Ack(budget, 500, 0.005, T0 + Seconds(0.051));
        Ack(budget, 10_000, 0.100, T0 + Seconds(1.0));

        // Both samples measure 100,000 B/s. The 5ms base RTT leaves the 10ms target
        // in control; the later queue-loaded 100ms sample must not grow the horizon.
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);
    }

    [Test]
    public async Task MinimumRtt_ExpiredWindowDrainsQueueBeforeRefreshing()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 500, 0.005, T0);
        Ack(budget, 500, 0.005, T0 + Seconds(0.051));

        // Expiring the minimum with a loaded sample starts a brief target-only drain,
        // rather than immediately accepting the 100ms queueing delay as base RTT.
        Ack(budget, 10_000, 0.100, T0 + Seconds(10.1));
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);

        Ack(budget, 5_000, 0.050, T0 + Seconds(10.2));
        Ack(budget, 500, 0.005, T0 + Seconds(10.31));
        Ack(budget, 10_000, 0.100, T0 + Seconds(10.4));

        // The final 10,000-byte request measures its own 100ms sojourn (100,000 B/s);
        // pipelined delivery credit flows through the delivered-counter delta rather than
        // a shrunken inter-ack interval.
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);
    }

    [Test]
    public async Task MinimumRtt_ExpiredWindowProbeUsesCurrentRttDuration()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 500, 0.005, T0);
        Ack(budget, 500, 0.005, T0 + Seconds(0.051));

        Ack(budget, 10_000, 0.100, T0 + Seconds(10.1));
        Ack(budget, 5_000, 0.050, T0 + Seconds(10.16));

        // The 100ms sample that starts the refresh keeps the target-only drain active.
        // Sizing from the stale 5ms minimum would have ended after the 50ms floor and
        // accepted the still-loaded 50ms sample as base RTT, inflating this to 7,500.
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);
    }

    [Test]
    public async Task MinimumRtt_OutlierProbeDurationIsBounded()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 10_000, 0.100, T0);
        Ack(budget, 10_000, 0.100, T0 + Seconds(0.101));
        Ack(budget, 200_000, 2.000, T0 + Seconds(10.2));
        budget.Charge(2_000);

        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.3))).IsTrue();
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.451))).IsFalse();
    }

    [Test]
    public async Task SetCap_ExpiredMinRttProbeRestoresSafetyFloorWithoutAck()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 10_000, 0.100, T0);
        Ack(budget, 10_000, 0.100, T0 + Seconds(0.101));
        await Assert.That(budget.BudgetBytes).IsEqualTo(15_000);

        Ack(budget, 20_000, 0.200, T0 + Seconds(10.2));
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);

        budget.SetCap(1_000_000, T0 + Seconds(10.401));

        await Assert.That(budget.BudgetBytes).IsEqualTo(15_000);
    }

    [Test]
    public async Task Admission_ExpiredIdleMinRttProbeUsesSafetyFloorWithoutAck()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 10_000, 0.100, T0);
        Ack(budget, 10_000, 0.100, T0 + Seconds(0.101));
        Ack(budget, 20_000, 0.200, T0 + Seconds(10.2));
        budget.Charge(2_000);

        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.3))).IsTrue();
        await Assert.That(budget.IsOverBudget()).IsTrue();
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.401))).IsFalse();
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

        // No ack landed inside the 100ms target-only drain. The late loaded sample must
        // not replace the retained 5ms base RTT and inflate the horizon to 150ms.
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);
    }

    [Test]
    public async Task WindowedMaximum_LongGap_ExpiresStaleRate()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        EstablishRate(budget, bytesPerSecond: 3_000, rttSeconds: 0.001);
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_500);

        Ack(budget, 1, 0.001, T0 + Seconds(11.0));

        await Assert.That(budget.BudgetBytes).IsEqualTo(500);
    }

    [Test]
    public async Task IdleGap_IsNotCountedAsRequestDrainTime()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        EstablishRate(budget, bytesPerSecond: 100_000, rttSeconds: 0.001);
        await Assert.That(budget.BudgetBytes).IsEqualTo(50_000);

        Ack(budget, 100, 0.001, T0 + Seconds(11.0));
        await Assert.That(budget.BudgetBytes).IsEqualTo(50_000);
    }

    [Test]
    public async Task SubMillisecondRequest_ProducesRateSampleImmediately()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 10, 0.0001, T0);

        await Assert.That(budget.BudgetBytes).IsEqualTo(50_000);
    }

    [Test]
    public async Task Budget_RecoversWhenRateRises()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        EstablishRate(budget, bytesPerSecond: 3_000, rttSeconds: 0.001);
        var shrunken = budget.BudgetBytes;

        // Faster drain in the next window must grow the budget back (no ratchet-down).
        Ack(budget, 12_000, 0.001, T0 + Seconds(2.0));

        await Assert.That(budget.BudgetBytes).IsGreaterThan(shrunken);
    }

    [Test]
    public async Task RateSample_UsesRequestBusyTime_NotGapBetweenAckPasses()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.100, T0);

        // 1,000 bytes / 100ms request RTT = 10,000 B/s. The first request is enough to
        // establish rate; no preceding wall-clock ack timestamp is required.
        await Assert.That(budget.BudgetBytes).IsEqualTo(5_000);
    }

    [Test]
    public async Task RateSample_UsesDeliveredDelta_ForPipelinedRequests()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        // Both requests were sent before either delivery.
        var firstSend = budget.SnapshotDelivery(T0 - Seconds(0.005), appLimited: false);
        var secondSend = budget.SnapshotDelivery(T0 + Seconds(0.001) - Seconds(0.005), appLimited: false);
        budget.OnAcked(1_000, firstSend, T0);
        budget.OnAcked(1_000, secondSend, T0 + Seconds(0.001));

        // The second sample's delivered-counter delta covers both deliveries: 2,000 bytes
        // over its own 5ms sojourn = 400,000 B/s aggregate drain, independent of the 1ms
        // gap between the two acknowledgements.
        await Assert.That(budget.BudgetBytes).IsEqualTo(4_000);
    }

    [Test]
    public async Task WindowedMaximum_LowerSamples_DoNotRatchetBudgetDown()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.100, T0);
        Ack(budget, 100, 0.100, T0 + Seconds(0.100));

        await Assert.That(budget.BudgetBytes).IsEqualTo(5_000);
    }

    [Test]
    public async Task WindowedMaximum_PeakExpires_AfterTwoSeconds()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.100, T0);
        for (var i = 1; i <= 20; i++)
            Ack(budget, 100, 0.100, T0 + Seconds(i * 0.100));

        await Assert.That(budget.BudgetBytes).IsEqualTo(500);
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
        await Assert.That(budget.BudgetBytes).IsEqualTo(5_000);

        Ack(budget, 10, 0.025, T0 + Seconds(2.1));

        await Assert.That(budget.BudgetBytes).IsEqualTo(200);
    }

    [Test]
    public async Task PeriodicProbe_CollectsOneRtt_ThenRevertsWithoutRateGain()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        for (var i = 0; i <= 8; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        await Assert.That(budget.BudgetBytes).IsEqualTo(6_250);

        Ack(budget, 1_000, 0.100, T0 + Seconds(0.900));
        await Assert.That(budget.BudgetBytes).IsEqualTo(6_250);

        Ack(budget, 1_000, 0.100, T0 + Seconds(1.000));

        await Assert.That(budget.BudgetBytes).IsEqualTo(5_000);
    }

    [Test]
    public async Task PeriodicProbe_CollectsFullRttBeforeJudgingRateGain()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        for (var i = 0; i <= 8; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        // One small response can arrive first after the larger budget is published.
        // It must not cancel the probe before other requests from the same RTT land.
        Ack(budget, 500, 0.100, T0 + Seconds(0.900));
        await Assert.That(budget.BudgetBytes).IsEqualTo(6_250);

        Ack(budget, 1_250, 0.100, T0 + Seconds(0.950));
        await Assert.That(budget.BudgetBytes).IsEqualTo(7_812);
    }

    [Test]
    public async Task PeriodicProbe_FastGateFallsThroughForDeadlineCheck()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);

        for (var i = 0; i <= 8; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        await Assert.That(budget.BudgetBytes).IsEqualTo(6_250);
        budget.Charge(5_500);

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
            .Because("an active minimum-RTT probe must retain its target-only drain budget");
    }

    [Test]
    public async Task PeriodicProbe_WaitsForSampleWhollyAdmittedUnderProbe()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        for (var i = 0; i <= 8; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        await Assert.That(budget.BudgetBytes).IsEqualTo(6_250);

        // This request began before the probe opened at 800ms, so it cannot confirm
        // capacity under the larger admission budget even though it completes later.
        Ack(budget, 1_000, 0.200, T0 + Seconds(0.900));
        await Assert.That(budget.BudgetBytes).IsEqualTo(6_250);

        // First request wholly admitted under the probe starts one RTT of measurement.
        Ack(budget, 1_000, 0.100, T0 + Seconds(1.000));
        await Assert.That(budget.BudgetBytes).IsEqualTo(6_250);

        // A full RTT of wholly-probed responses confirms no gain and ends the probe.
        Ack(budget, 1_000, 0.100, T0 + Seconds(1.100));
        await Assert.That(budget.BudgetBytes).IsEqualTo(5_000);
    }

    [Test]
    public async Task PeriodicProbe_RatchetsWhileWhollyProbedRateRises()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        for (var i = 0; i <= 8; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        Ack(budget, 1_250, 0.100, T0 + Seconds(0.900));
        await Assert.That(budget.BudgetBytes).IsEqualTo(7_812);

        Ack(budget, 1_562, 0.100, T0 + Seconds(1.000));
        await Assert.That(budget.BudgetBytes).IsEqualTo(9_762);

        // The first sample under the higher level starts another full-RTT measurement.
        Ack(budget, 1_562, 0.100, T0 + Seconds(1.100));
        await Assert.That(budget.BudgetBytes).IsEqualTo(9_762);

        // One RTT without further growth publishes the newly discovered base budget
        // without the temporary 1.25x multiplier.
        Ack(budget, 1_562, 0.100, T0 + Seconds(1.200));
        await Assert.That(budget.BudgetBytes).IsEqualTo(7_810);
    }

    [Test]
    public async Task OccupancyFloor_RetainsHeadroomAboveDemonstratedWireDemand()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.ObserveWrittenUnackedBytes(20_000);
        Ack(budget, 100, 0.100, T0);
        Ack(budget, 100, 0.100, T0 + Seconds(0.101));

        // Rate budget = 1,000 B/s × max(10ms, 1.5 × 100ms) = 150 bytes. The demonstrated
        // 20,000-byte occupancy grants headroom above that, but only up to 1.5 × the rate
        // budget — occupancy is admission-bounded by the budget itself, so an uncapped
        // floor would ratchet the budget to its cap (#1941).
        await Assert.That(budget.BudgetBytes).IsEqualTo(225);
    }

    [Test]
    public async Task MinimumRttProbe_IgnoresRecentOccupancyUntilQueueDrains()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.ObserveWrittenUnackedBytes(20_000);
        Ack(budget, 100, 0.100, T0);

        await Assert.That(budget.BudgetBytes).IsEqualTo(200)
            .Because("minimum-RTT probes must publish a target-only budget that drains standing queueing");

        budget.ObserveWrittenUnackedBytes(20_000);
        Ack(budget, 100, 0.100, T0 + Seconds(0.101));

        await Assert.That(budget.BudgetBytes).IsEqualTo(225);
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

        // 100,000 B/s drain at negligible RTT => rate budget 1,000 bytes. Feed the
        // published budget back as observed occupancy each round — the exact feedback
        // shape that previously ratcheted the budget to its cap.
        Ack(budget, 100, 0.001, T0);
        for (var i = 1; i <= 5; i++)
        {
            budget.ObserveWrittenUnackedBytes(budget.BudgetBytes);
            Ack(budget, 100, 0.001, T0 + Seconds(i * 0.100));
        }

        // Fixed point is 1.5 × the rate budget, not the 1,000,000-byte cap.
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_500);
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

        // Each sample measures the cumulative delivered delta over its own ~100ms sojourn;
        // the last one reads the true aggregate drain of ~50 MB/s.
        await Assert.That(budget.MaxRateBytesPerSecond).IsGreaterThanOrEqualTo(49_000_000);
        await Assert.That(budget.MaxRateBytesPerSecond).IsLessThanOrEqualTo(51_000_000);

        // Budget ≈ target × rate — three orders of magnitude below the cap the broken
        // estimator pegged.
        await Assert.That(budget.BudgetBytes).IsLessThanOrEqualTo(600_000);
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

        await Assert.That(budget.BudgetBytes).IsEqualTo(100_000);
    }

    [Test]
    public async Task AppLimitedSend_AfterIdle_UsesSojournAxis_NotIdleGap()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 10, 0.005, T0);

        // The pipe was empty at send: the 5-second idle gap is not drain time, so the
        // sample uses the request's own 5ms sojourn (20 MB/s, budget clamps to cap).
        Ack(budget, 100_000, 0.005, T0 + Seconds(5.0));

        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000_000);
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

        for (var i = 0; i <= 8; i++)
            Ack(budget, 1_000, 0.100, T0 + Seconds(i * 0.100));

        budget.Charge(5_500);

        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(0.900))).IsFalse();
        await Assert.That(budget.GetAdmissionRecheckDelayMilliseconds(T0 + Seconds(0.900)))
            .IsEqualTo(700);
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(1.601))).IsTrue();

        budget.Release(5_500);
    }

    [Test]
    public async Task AckPassGap_DoesNotChangeRequestRateSample()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        Ack(budget, 1_000, 0.010, T0);
        Ack(budget, 1_000, 0.010, T0 + Seconds(1.0));

        await Assert.That(budget.BudgetBytes).IsEqualTo(50_000);
    }

    [Test]
    public async Task SetCap_RecomputesAndRepublishesBudget()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 3_200);

        // Cold start follows the cap in both directions.
        budget.SetCap(6_400, T0);
        await Assert.That(budget.BudgetBytes).IsEqualTo(6_400);

        // With a rate established, a cap below the computed budget clamps it...
        EstablishRate(budget, bytesPerSecond: 3_000, rttSeconds: 0.001); // budget 1,500
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
}
