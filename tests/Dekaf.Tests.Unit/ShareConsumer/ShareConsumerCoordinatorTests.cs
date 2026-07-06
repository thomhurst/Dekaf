using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumerCoordinatorTests
{
    [Test]
    public async Task WaitForAssignmentDelay_UsesHeartbeatIntervalWhenLonger()
    {
        var delayMs = ShareConsumerCoordinator.GetWaitForAssignmentDelayMs(
            retryDelayMs: 200,
            heartbeatIntervalMs: 3000);

        await Assert.That(delayMs).IsEqualTo(3000);
    }

    [Test]
    public async Task WaitForAssignmentDelay_UsesRetryDelayWhenLonger()
    {
        var delayMs = ShareConsumerCoordinator.GetWaitForAssignmentDelayMs(
            retryDelayMs: 1000,
            heartbeatIntervalMs: 500);

        await Assert.That(delayMs).IsEqualTo(1000);
    }
}
