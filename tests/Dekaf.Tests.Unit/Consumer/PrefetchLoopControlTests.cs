using System.Net.Sockets;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;

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
    public async Task RecordConsecutiveError_RetriableKafkaFailureDoesNotPoisonLoop()
    {
        var error = new KafkaException(ErrorCode.RequestTimedOut, "offset resolution timed out");

        var consecutiveErrors = PrefetchLoopControl.RecordConsecutiveError(49, error);

        await Assert.That(consecutiveErrors).IsEqualTo(0);
    }

    [Test]
    [Arguments("connection-refused")]
    [Arguments("connection-reset")]
    [Arguments("setup-timeout")]
    [Arguments("dns")]
    [Arguments("metadata-refresh-unreachable")]
    [Arguments("aggregate-transport")]
    [Arguments("rebalance-timeout")]
    public async Task RecordConsecutiveError_TransientOutageDoesNotPoisonLoop(string kind)
    {
        // A broker or coordinator outage can outlast any number of loop iterations; reaching
        // the limit ends the consumer for good.
        var consecutiveErrors = PrefetchLoopControl.RecordConsecutiveError(49, CreateFailure(kind));

        await Assert.That(consecutiveErrors).IsEqualTo(0);
    }

    [Test]
    [Arguments("unexpected")]
    [Arguments("metadata-refresh-authentication")]
    [Arguments("non-retriable-kafka")]
    public async Task RecordConsecutiveError_PersistentFailureIncrementsCount(string kind)
    {
        var consecutiveErrors = PrefetchLoopControl.RecordConsecutiveError(49, CreateFailure(kind));

        await Assert.That(consecutiveErrors).IsEqualTo(50);
    }

    private static Exception CreateFailure(string kind) => kind switch
    {
        "connection-refused" => new SocketException((int)SocketError.ConnectionRefused),
        "connection-reset" => new IOException(
            "connection reset", new SocketException((int)SocketError.ConnectionReset)),
        "setup-timeout" => new TimeoutException("connection setup timed out"),
        "dns" => new DnsResolutionException("broker-1", 9092, new SocketException((int)SocketError.HostNotFound)),
        "metadata-refresh-unreachable" => new InvalidOperationException(
            "Failed to refresh metadata from any broker",
            new SocketException((int)SocketError.ConnectionRefused)),
        "aggregate-transport" => new AggregateException(new IOException("connection reset")),
        "rebalance-timeout" => new KafkaTimeoutException(
            TimeoutKind.Rebalance,
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(60),
            "Group join timed out"),
        "unexpected" => new InvalidOperationException("unexpected"),
        "metadata-refresh-authentication" => new InvalidOperationException(
            "Failed to refresh metadata from any broker",
            new AuthenticationException("SASL authentication failed")),
        "non-retriable-kafka" => new KafkaException(ErrorCode.InvalidRequest, "not retriable"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    [Test]
    public async Task RecordConsecutiveError_UnexpectedFailureIncrementsCount()
    {
        var consecutiveErrors = PrefetchLoopControl.RecordConsecutiveError(
            49,
            new InvalidOperationException("unexpected"));

        await Assert.That(consecutiveErrors).IsEqualTo(50);
    }

    [Test]
    public async Task ShouldResetConsecutiveErrors_OnlyWhenCompletedFetchesDrain()
    {
        await Assert.That(PrefetchLoopControl.ShouldResetConsecutiveErrors(0)).IsFalse();
        await Assert.That(PrefetchLoopControl.ShouldResetConsecutiveErrors(1)).IsTrue();
    }
}
