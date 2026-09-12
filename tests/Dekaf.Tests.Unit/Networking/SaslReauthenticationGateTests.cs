using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Networking;

public class SaslReauthenticationGateTests
{
    [Test]
    public async Task Pause_DrainsExistingRequestsAndBlocksNewRequests(CancellationToken cancellationToken)
    {
        var gate = new SaslReauthenticationGate();
        await gate.EnterAsync(cancellationToken);
        await gate.EnterAsync(cancellationToken);
        var paused = gate.PauseAsync(cancellationToken);
        var waiting = gate.EnterAsync(cancellationToken).AsTask();
        await Assert.That(paused.IsCompleted).IsFalse();
        await Assert.That(waiting.IsCompleted).IsFalse();
        gate.Exit();
        await Assert.That(paused.IsCompleted).IsFalse();
        gate.Exit();
        await paused.WaitAsync(cancellationToken);
        await Assert.That(waiting.IsCompleted).IsFalse();
        gate.Resume();
        await waiting.WaitAsync(cancellationToken);
        gate.Exit();
    }

    [Test]
    public async Task CancelledWaiter_DoesNotPreventNextRenewal(CancellationToken cancellationToken)
    {
        var gate = new SaslReauthenticationGate();
        await gate.PauseAsync(cancellationToken);
        using var cancelled = new CancellationTokenSource();
        var waiting = gate.EnterAsync(cancelled.Token).AsTask();
        cancelled.Cancel();
        await Assert.That(async () => await waiting).Throws<OperationCanceledException>();
        gate.Resume();
        await gate.EnterAsync(cancellationToken);
        gate.Exit();
        await gate.PauseAsync(cancellationToken).WaitAsync(cancellationToken);
        gate.Resume();
    }

    [Test]
    public async Task CancelledPause_PreservesOutstandingRequestCount(CancellationToken cancellationToken)
    {
        var gate = new SaslReauthenticationGate();
        await gate.EnterAsync(cancellationToken);
        using var cancelled = new CancellationTokenSource();
        var pause = gate.PauseAsync(cancelled.Token);
        cancelled.Cancel();
        await Assert.That(async () => await pause).Throws<OperationCanceledException>();
        gate.Resume();
        var nextPause = gate.PauseAsync(cancellationToken);
        await Assert.That(nextPause.IsCompleted).IsFalse();
        gate.Exit();
        await nextPause.WaitAsync(cancellationToken);
        gate.Resume();
    }

    [Test]
    public async Task Close_ReleasesBlockedRequestsAndRenewal(CancellationToken cancellationToken)
    {
        var gate = new SaslReauthenticationGate();
        await gate.EnterAsync(cancellationToken);
        var pause = gate.PauseAsync(cancellationToken);
        var waiting = gate.EnterAsync(cancellationToken).AsTask();
        gate.Close();
        await pause.WaitAsync(cancellationToken);
        await Assert.That(async () => await waiting).Throws<ObjectDisposedException>();
        gate.Resume();
        await Assert.That(async () => await gate.EnterAsync(cancellationToken)).Throws<ObjectDisposedException>();
        gate.Exit();
    }
}
