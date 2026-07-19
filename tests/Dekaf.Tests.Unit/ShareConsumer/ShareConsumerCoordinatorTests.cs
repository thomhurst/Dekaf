using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumerCoordinatorTests
{
    [Test]
    public async Task WaitForAssignmentDelay_UsesHeartbeatInterval()
    {
        var delayMs = ShareConsumerCoordinator.GetWaitForAssignmentDelayMs(heartbeatIntervalMs: 3000);

        await Assert.That(delayMs).IsEqualTo(3000);
    }

    [Test]
    public async Task WaitForAssignmentDelay_NormalizesNonPositiveInterval()
    {
        var delayMs = ShareConsumerCoordinator.GetWaitForAssignmentDelayMs(heartbeatIntervalMs: 0);

        await Assert.That(delayMs).IsEqualTo(1);
    }

    [Test]
    public async Task JoinRetryDelay_UsesCalculatedDelayWhenDeadlineIsFartherAway()
    {
        var delay = ShareConsumerCoordinator.GetJoinRetryDelay(
            retryDelayMs: 500,
            elapsed: TimeSpan.FromSeconds(1),
            joinTimeout: TimeSpan.FromSeconds(5));

        await Assert.That(delay).IsEqualTo(TimeSpan.FromMilliseconds(500));
    }

    [Test]
    public async Task JoinRetryDelay_IsCappedToRemainingDeadline()
    {
        var delay = ShareConsumerCoordinator.GetJoinRetryDelay(
            retryDelayMs: 5_000,
            elapsed: TimeSpan.FromMilliseconds(4_750),
            joinTimeout: TimeSpan.FromSeconds(5));

        await Assert.That(delay).IsEqualTo(TimeSpan.FromMilliseconds(250));
    }

    [Test]
    public async Task JoinRetryDelay_IsZeroAfterDeadline()
    {
        var delay = ShareConsumerCoordinator.GetJoinRetryDelay(
            retryDelayMs: 500,
            elapsed: TimeSpan.FromSeconds(6),
            joinTimeout: TimeSpan.FromSeconds(5));

        await Assert.That(delay).IsEqualTo(TimeSpan.Zero);
    }
}
