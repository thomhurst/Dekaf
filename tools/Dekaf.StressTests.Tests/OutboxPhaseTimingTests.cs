using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;

namespace Dekaf.StressTests.Tests;

public class OutboxPhaseTimingTests
{
    [Test]
    [Arguments(15.0, 45.0, true)]
    [Arguments(20.0, 40.0, false)]
    [Arguments(5.0, 55.0, false)]
    [Arguments(double.NaN, 45.0, false)]
    [Arguments(15.0, double.PositiveInfinity, false)]
    public async Task EachPhaseMustCompleteItsOwnDeclaredDuration(double idleElapsed, double activeElapsed, bool expected)
    {
        var operations = new OutboxOperationCounts(0, 0, 1, 0, 0, 0, 0, 0);
        var idle = new OutboxIdleSnapshot(15, idleElapsed, new RuntimeObservation(), new RuntimeObservation(), operations, []);
        var snapshot = new OutboxWorkloadSnapshot(idle, idle, 45, activeElapsed, 1, 1, 0, operations);
        await Assert.That(snapshot.HasCompleteDuration(1, activeElapsed)).IsEqualTo(expected);
        await Assert.That(snapshot.HasCompleteDuration(2, activeElapsed)).IsFalse();
        await Assert.That((snapshot with { ActiveWorkloadSeconds = 1 }).HasCompleteDuration(1, 60)).IsFalse();
    }
}
