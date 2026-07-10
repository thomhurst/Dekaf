using Dekaf.StressTests;

namespace Dekaf.Tests.Unit.Stress;

public sealed class StressRunCompletionPolicyTests
{
    [Test]
    public async Task EndedEarly_MessageBoundedRun_DoesNotRequireConfiguredDuration()
    {
        var endedEarly = StressRunCompletionPolicy.EndedEarly(
            elapsedSeconds: 30,
            configuredDurationMinutes: 15,
            isMessageBounded: true);

        await Assert.That(endedEarly).IsFalse();
    }

    [Test]
    public async Task EndedEarly_DurationBoundedRun_RequiresNinetyPercentOfConfiguredDuration()
    {
        var endedEarly = StressRunCompletionPolicy.EndedEarly(
            elapsedSeconds: 809,
            configuredDurationMinutes: 15,
            isMessageBounded: false);

        await Assert.That(endedEarly).IsTrue();
    }

    [Test]
    public async Task EndedEarly_DurationBoundedRun_AfterThreshold_IsFalse()
    {
        var endedEarly = StressRunCompletionPolicy.EndedEarly(
            elapsedSeconds: 900,
            configuredDurationMinutes: 15,
            isMessageBounded: false);

        await Assert.That(endedEarly).IsFalse();
    }
}
