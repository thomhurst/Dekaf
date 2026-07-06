using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PrefetchLoopControlTests
{
    [Test]
    public async Task ShouldWaitForMemory_WhenPrefetchMemoryLimitReached_ReturnsTrue()
    {
        await Assert.That(PrefetchLoopControl.ShouldWaitForMemory(currentPrefetchedBytes: 1024, maxBytes: 1024)).IsTrue();
        await Assert.That(PrefetchLoopControl.ShouldWaitForMemory(currentPrefetchedBytes: 1023, maxBytes: 1024)).IsFalse();
    }

    [Test]
    public async Task DecideAfterDispatch_WhenNoDispatchProgressAndTargetsRemain_ReportsBacklogAndWaitsForAny()
    {
        var decision = PrefetchLoopControl.DecideAfterDispatch(
            started: 0,
            targetCount: 2,
            hasInFlight: true);

        await Assert.That(decision.Action).IsEqualTo(PrefetchLoopAction.WaitForAny);
        await Assert.That(decision.ReportBacklog).IsTrue();
        await Assert.That(decision.RecordFetchWait).IsTrue();
    }

    [Test]
    public async Task DecideAfterDispatch_WhenNoDispatchProgressButNoTargets_WaitsWithoutBacklog()
    {
        var decision = PrefetchLoopControl.DecideAfterDispatch(
            started: 0,
            targetCount: 0,
            hasInFlight: true);

        await Assert.That(decision.Action).IsEqualTo(PrefetchLoopAction.WaitForAny);
        await Assert.That(decision.ReportBacklog).IsFalse();
        await Assert.That(decision.RecordFetchWait).IsTrue();
    }

    [Test]
    public async Task DecideAfterDispatch_WhenNoDispatchProgressAndNoInFlightWork_DelaysWithoutBacklog()
    {
        var decision = PrefetchLoopControl.DecideAfterDispatch(
            started: 0,
            targetCount: 0,
            hasInFlight: false);

        await Assert.That(decision.Action).IsEqualTo(PrefetchLoopAction.DelayNoWork);
        await Assert.That(decision.ReportBacklog).IsFalse();
        await Assert.That(decision.RecordFetchWait).IsFalse();
    }

    [Test]
    public async Task DecideAfterDispatch_WhenDispatchStartsWork_ContinuesWithoutBacklog()
    {
        var decision = PrefetchLoopControl.DecideAfterDispatch(
            started: 1,
            targetCount: 2,
            hasInFlight: true);

        await Assert.That(decision.Action).IsEqualTo(PrefetchLoopAction.Continue);
        await Assert.That(decision.ReportBacklog).IsFalse();
        await Assert.That(decision.RecordFetchWait).IsFalse();
    }

    [Test]
    public async Task ShouldBreakOnConsecutiveError_TripsAtThresholdOnly()
    {
        await Assert.That(PrefetchLoopControl.ShouldBreakOnConsecutiveError(49, 50)).IsFalse();
        await Assert.That(PrefetchLoopControl.ShouldBreakOnConsecutiveError(50, 50)).IsTrue();
    }

    [Test]
    public async Task ShouldResetConsecutiveErrors_OnlyWhenCompletedFetchesDrain()
    {
        await Assert.That(PrefetchLoopControl.ShouldResetConsecutiveErrors(0)).IsFalse();
        await Assert.That(PrefetchLoopControl.ShouldResetConsecutiveErrors(1)).IsTrue();
    }
}
