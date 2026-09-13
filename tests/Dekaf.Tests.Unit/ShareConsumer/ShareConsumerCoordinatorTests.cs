using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.ShareConsumer;
using NSubstitute;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumerCoordinatorTests
{
    [Test]
    public async Task TelemetryMemberId_OmitsUnjoinedFencedAndDisposedIdentities()
    {
        var pool = Substitute.For<IConnectionPool>();
        await using var metadata = new MetadataManager(pool, ["localhost:9092"]);
        await using var coordinator = new ShareConsumerCoordinator(
            new ShareConsumerOptions { BootstrapServers = ["localhost:9092"], GroupId = "group" }, pool, metadata);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(ShareConsumerCoordinator);
        type.GetField("_memberId", flags)!.SetValue(coordinator, "member-a");
        type.GetField("_memberEpoch", flags)!.SetValue(coordinator, 1);
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsNull();
        type.GetField("_state", flags)!.SetValue(coordinator, CoordinatorState.Stable);
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsEqualTo("member-a");
        type.GetField("_memberEpoch", flags)!.SetValue(coordinator, 0);
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsNull();
        type.GetField("_memberId", flags)!.SetValue(coordinator, "member-b");
        type.GetField("_memberEpoch", flags)!.SetValue(coordinator, 2);
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsEqualTo("member-b");
        await coordinator.DisposeAsync();
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsNull();
    }

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
