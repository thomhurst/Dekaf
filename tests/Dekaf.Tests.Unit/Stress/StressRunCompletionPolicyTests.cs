using Dekaf.StressTests;

namespace Dekaf.Tests.Unit.Stress;

public sealed class StressRunCompletionPolicyTests
{
    [Test]
    public async Task EndedEarly_SelfBoundedRun_DoesNotRequireConfiguredDuration()
    {
        // Self-bounded scenarios (e.g. the steady round-trip produce window plus its
        // variable-length consume phase) define their own completion instead of --duration.
        var endedEarly = StressRunCompletionPolicy.EndedEarly(
            elapsedSeconds: 30,
            configuredDurationMinutes: 15,
            isSelfBounded: true);

        await Assert.That(endedEarly).IsFalse();
    }

    [Test]
    public async Task EndedEarly_DurationBoundedRun_RequiresNinetyPercentOfConfiguredDuration()
    {
        var endedEarly = StressRunCompletionPolicy.EndedEarly(
            elapsedSeconds: 809,
            configuredDurationMinutes: 15,
            isSelfBounded: false);

        await Assert.That(endedEarly).IsTrue();
    }

    [Test]
    public async Task EndedEarly_DurationBoundedRun_AfterThreshold_IsFalse()
    {
        var endedEarly = StressRunCompletionPolicy.EndedEarly(
            elapsedSeconds: 900,
            configuredDurationMinutes: 15,
            isSelfBounded: false);

        await Assert.That(endedEarly).IsFalse();
    }
}
