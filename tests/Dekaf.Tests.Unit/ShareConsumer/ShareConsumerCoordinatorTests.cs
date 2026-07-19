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
}
