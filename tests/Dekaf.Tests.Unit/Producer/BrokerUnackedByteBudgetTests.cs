using System.Diagnostics;
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

    /// <summary>
    /// Establishes a per-request drain-rate sample of <paramref name="bytesPerSecond"/>.
    /// </summary>
    private static void EstablishRate(BrokerUnackedByteBudget budget, long bytesPerSecond, double rttSeconds)
    {
        budget.OnAcked((long)(bytesPerSecond * rttSeconds), Seconds(rttSeconds), T0);
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
        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(0.101));

        // 100,000 B/s × max(0.010, 1.5 × 0.100) = 15,000 bytes (±tick-quantization of the RTT).
        await Assert.That(budget.BudgetBytes).IsGreaterThanOrEqualTo(14_990);
        await Assert.That(budget.BudgetBytes).IsLessThanOrEqualTo(15_010);
    }

    [Test]
    public async Task MinimumRtt_LoadedSamplesDoNotInflateBudget()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 500, rttTicks: Seconds(0.005), nowTicks: T0);
        budget.OnAcked(ackedBytes: 500, rttTicks: Seconds(0.005), nowTicks: T0 + Seconds(0.051));
        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(1.0));

        // Both samples measure 100,000 B/s. The 5ms base RTT leaves the 10ms target
        // in control; the later queue-loaded 100ms sample must not grow the horizon.
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);
    }

    [Test]
    public async Task MinimumRtt_ExpiredWindowDrainsQueueBeforeRefreshing()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 500, rttTicks: Seconds(0.005), nowTicks: T0);
        budget.OnAcked(ackedBytes: 500, rttTicks: Seconds(0.005), nowTicks: T0 + Seconds(0.051));

        // Expiring the minimum with a loaded sample starts a brief target-only drain,
        // rather than immediately accepting the 100ms queueing delay as base RTT.
        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(10.1));
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);

        budget.OnAcked(ackedBytes: 5_000, rttTicks: Seconds(0.050), nowTicks: T0 + Seconds(10.2));
        budget.OnAcked(ackedBytes: 500, rttTicks: Seconds(0.005), nowTicks: T0 + Seconds(10.31));
        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(10.4));

        // Final 10,000-byte pass lands 90ms after the preceding ack, measuring
        // 111,111 B/s from inter-ack delivery time rather than 100ms request sojourn.
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_111);
    }

    [Test]
    public async Task MinimumRtt_ExpiredWindowProbeUsesCurrentRttDuration()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 500, rttTicks: Seconds(0.005), nowTicks: T0);
        budget.OnAcked(ackedBytes: 500, rttTicks: Seconds(0.005), nowTicks: T0 + Seconds(0.051));

        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(10.1));
        budget.OnAcked(ackedBytes: 5_000, rttTicks: Seconds(0.050), nowTicks: T0 + Seconds(10.16));

        // The 100ms sample that starts the refresh keeps the target-only drain active.
        // Sizing from the stale 5ms minimum would have ended after the 50ms floor and
        // accepted the still-loaded 50ms sample as base RTT, inflating this to 7,500.
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);
    }

    [Test]
    public async Task MinimumRtt_OutlierProbeDurationIsBounded()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0);
        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(0.101));
        budget.OnAcked(ackedBytes: 200_000, rttTicks: Seconds(2.000), nowTicks: T0 + Seconds(10.2));
        budget.Charge(2_000);

        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.3))).IsTrue();
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.451))).IsFalse();
    }

    [Test]
    public async Task SetCap_ExpiredMinRttProbeRestoresSafetyFloorWithoutAck()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0);
        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(0.101));
        await Assert.That(budget.BudgetBytes).IsEqualTo(15_000);

        budget.OnAcked(ackedBytes: 20_000, rttTicks: Seconds(0.200), nowTicks: T0 + Seconds(10.2));
        await Assert.That(budget.BudgetBytes).IsEqualTo(1_000);

        budget.SetCap(1_000_000, T0 + Seconds(10.401));

        await Assert.That(budget.BudgetBytes).IsEqualTo(15_000);
    }

    [Test]
    public async Task Admission_ExpiredIdleMinRttProbeUsesSafetyFloorWithoutAck()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0);
        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(0.101));
        budget.OnAcked(ackedBytes: 20_000, rttTicks: Seconds(0.200), nowTicks: T0 + Seconds(10.2));
        budget.Charge(2_000);

        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.3))).IsTrue();
        await Assert.That(budget.IsOverBudget()).IsTrue();
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.401))).IsFalse();
    }

    [Test]
    public async Task Admission_ExpiredProbeUsesMinimumObservedDuringProbe()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0);
        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(0.101));

        // Refresh the old 100ms minimum, then observe 5ms while the drain probe is open.
        budget.OnAcked(ackedBytes: 20_000, rttTicks: Seconds(0.200), nowTicks: T0 + Seconds(10.2));
        budget.OnAcked(ackedBytes: 500, rttTicks: Seconds(0.005), nowTicks: T0 + Seconds(10.25));
        budget.Charge(2_000);

        // Once the probe expires without another ack, admission must use the refreshed
        // 5ms minimum (1,000-byte budget), not the stale 100ms minimum (15,000 bytes).
        await Assert.That(budget.IsOverBudgetAt(T0 + Seconds(10.401))).IsTrue();
    }

    [Test]
    public async Task MinimumRtt_LateAckAfterEmptyProbeRetainsPriorMinimum()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 500, rttTicks: Seconds(0.005), nowTicks: T0);
        budget.OnAcked(ackedBytes: 500, rttTicks: Seconds(0.005), nowTicks: T0 + Seconds(0.051));

        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(10.1));
        budget.OnAcked(ackedBytes: 10_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(10.25));

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

        budget.OnAcked(ackedBytes: 1, rttTicks: Seconds(0.001), nowTicks: T0 + Seconds(11.0));

        await Assert.That(budget.BudgetBytes).IsEqualTo(500);
    }

    [Test]
    public async Task IdleGap_IsNotCountedAsRequestDrainTime()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        EstablishRate(budget, bytesPerSecond: 100_000, rttSeconds: 0.001);
        await Assert.That(budget.BudgetBytes).IsEqualTo(50_000);

        budget.OnAcked(ackedBytes: 100, rttTicks: Seconds(0.001), nowTicks: T0 + Seconds(11.0));
        await Assert.That(budget.BudgetBytes).IsEqualTo(50_000);
    }

    [Test]
    public async Task SubMillisecondRequest_ProducesRateSampleImmediately()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 10, rttTicks: Seconds(0.0001), nowTicks: T0);

        await Assert.That(budget.BudgetBytes).IsEqualTo(50_000);
    }

    [Test]
    public async Task Budget_RecoversWhenRateRises()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        EstablishRate(budget, bytesPerSecond: 3_000, rttSeconds: 0.001);
        var shrunken = budget.BudgetBytes;

        // Faster drain in the next window must grow the budget back (no ratchet-down).
        budget.OnAcked(ackedBytes: 12_000, rttTicks: Seconds(0.001), nowTicks: T0 + Seconds(2.0));

        await Assert.That(budget.BudgetBytes).IsGreaterThan(shrunken);
    }

    [Test]
    public async Task RateSample_UsesRequestBusyTime_NotGapBetweenAckPasses()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.100), nowTicks: T0);

        // 1,000 bytes / 100ms request RTT = 10,000 B/s. The first request is enough to
        // establish rate; no preceding wall-clock ack timestamp is required.
        await Assert.That(budget.BudgetBytes).IsEqualTo(5_000);
    }

    [Test]
    public async Task RateSample_UsesInterAckDeliveryTime_ForPipelinedRequests()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.005), nowTicks: T0);
        budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.005), nowTicks: T0 + Seconds(0.001));

        // Both requests overlapped on the wire. The second 1,000-byte delivery took 1ms
        // since the preceding acknowledgement, independent of its 5ms request sojourn.
        await Assert.That(budget.BudgetBytes).IsEqualTo(10_000);
    }

    [Test]
    public async Task WindowedMaximum_LowerSamples_DoNotRatchetBudgetDown()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.100), nowTicks: T0);
        budget.OnAcked(ackedBytes: 100, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(0.100));

        await Assert.That(budget.BudgetBytes).IsEqualTo(5_000);
    }

    [Test]
    public async Task WindowedMaximum_PeakExpires_AfterTwoSeconds()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.100), nowTicks: T0);
        for (var i = 1; i <= 20; i++)
            budget.OnAcked(ackedBytes: 100, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(i * 0.100));

        await Assert.That(budget.BudgetBytes).IsEqualTo(500);
    }

    [Test]
    public async Task WindowedMaximum_UsesElapsedTime_NotResponsePassCount()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.100), nowTicks: T0);
        for (var i = 1; i <= 20; i++)
            budget.OnAcked(ackedBytes: 10, rttTicks: Seconds(0.025), nowTicks: T0 + Seconds(i * 0.025));

        // Twenty fast response passes cover only 500ms, so the 2-second maximum window
        // must retain the original 10,000 B/s observation.
        await Assert.That(budget.BudgetBytes).IsEqualTo(5_000);

        budget.OnAcked(ackedBytes: 10, rttTicks: Seconds(0.025), nowTicks: T0 + Seconds(2.1));

        await Assert.That(budget.BudgetBytes).IsEqualTo(200);
    }

    [Test]
    public async Task PeriodicProbe_RaisesBudgetForOneRtt_ThenRevertsWithoutRateGain()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        for (var i = 0; i <= 8; i++)
            budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(i * 0.100));

        await Assert.That(budget.BudgetBytes).IsEqualTo(6_250);

        budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(0.900));

        await Assert.That(budget.BudgetBytes).IsEqualTo(5_000);
    }

    [Test]
    public async Task PeriodicProbe_FastGateFallsThroughForDeadlineCheck()
    {
        var budget = new BrokerUnackedByteBudget(
            targetSeconds: 0.5,
            floorBytes: 200,
            initialCapBytes: 1_000_000);

        for (var i = 0; i <= 8; i++)
            budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(i * 0.100));

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
    public async Task PeriodicProbe_WaitsForSampleWhollyAdmittedUnderProbe()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        for (var i = 0; i <= 8; i++)
            budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(i * 0.100));

        await Assert.That(budget.BudgetBytes).IsEqualTo(6_250);

        // This request began before the probe opened at 800ms, so it cannot confirm
        // capacity under the larger admission budget even though it completes later.
        budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.200), nowTicks: T0 + Seconds(0.900));
        await Assert.That(budget.BudgetBytes).IsEqualTo(6_250);

        // First request wholly admitted under the probe confirms no rate gain and ends it.
        budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(1.000));
        await Assert.That(budget.BudgetBytes).IsEqualTo(5_000);
    }

    [Test]
    public async Task PeriodicProbe_RatchetsWhileWhollyProbedRateRises()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        for (var i = 0; i <= 8; i++)
            budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(i * 0.100));

        budget.OnAcked(ackedBytes: 1_250, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(0.900));
        await Assert.That(budget.BudgetBytes).IsEqualTo(7_812);

        budget.OnAcked(ackedBytes: 1_562, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(1.000));
        await Assert.That(budget.BudgetBytes).IsEqualTo(9_762);

        // A second wholly-probed sample at the same rate confirms capacity and publishes
        // the newly discovered base budget without the temporary 1.25x multiplier.
        budget.OnAcked(ackedBytes: 1_562, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(1.100));
        await Assert.That(budget.BudgetBytes).IsEqualTo(7_810);
    }

    [Test]
    public async Task OccupancyFloor_RetainsHeadroomAboveDemonstratedWireDemand()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.ObserveWrittenUnackedBytes(20_000);
        budget.OnAcked(ackedBytes: 100, rttTicks: Seconds(0.100), nowTicks: T0);
        budget.OnAcked(ackedBytes: 100, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(0.101));

        await Assert.That(budget.BudgetBytes).IsEqualTo(30_000);
    }

    [Test]
    public async Task MinimumRttProbe_IgnoresRecentOccupancyUntilQueueDrains()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.ObserveWrittenUnackedBytes(20_000);
        budget.OnAcked(ackedBytes: 100, rttTicks: Seconds(0.100), nowTicks: T0);

        await Assert.That(budget.BudgetBytes).IsEqualTo(200)
            .Because("minimum-RTT probes must publish a target-only budget that drains standing queueing");

        budget.ObserveWrittenUnackedBytes(20_000);
        budget.OnAcked(ackedBytes: 100, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(0.101));

        await Assert.That(budget.BudgetBytes).IsEqualTo(30_000);
    }

    [Test]
    public async Task OccupancyFloor_ExpiresAfterTwoSeconds()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.010, floorBytes: 200, initialCapBytes: 1_000_000);

        budget.ObserveWrittenUnackedBytes(20_000);
        budget.OnAcked(ackedBytes: 100, rttTicks: Seconds(0.100), nowTicks: T0);
        budget.OnAcked(ackedBytes: 100, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(2.1));

        await Assert.That(budget.BudgetBytes).IsEqualTo(200);
    }

    [Test]
    public async Task PeriodicProbe_WithoutFurtherAck_RevertsAfterEightRtts()
    {
        var budget = new BrokerUnackedByteBudget(targetSeconds: 0.5, floorBytes: 200, initialCapBytes: 1_000_000);

        for (var i = 0; i <= 8; i++)
            budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.100), nowTicks: T0 + Seconds(i * 0.100));

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

        budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.010), nowTicks: T0);
        budget.OnAcked(ackedBytes: 1_000, rttTicks: Seconds(0.010), nowTicks: T0 + Seconds(1.0));

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
