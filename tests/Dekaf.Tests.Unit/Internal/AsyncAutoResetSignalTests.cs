using Dekaf.Internal;

namespace Dekaf.Tests.Unit.Internal;

public class AsyncAutoResetSignalTests
{
    [Test]
    public async Task InlineTimeoutContinuations_RearmAcrossTimeoutsAndSignals()
    {
        using var signal = new AsyncAutoResetSignal(inlineTimeoutContinuations: true);

        for (var i = 0; i < 100; i++)
            await Assert.That(await signal.WaitAsync(1)).IsFalse();

        for (var i = 0; i < 100; i++)
        {
            var wait = signal.WaitAsync(1_000);
            signal.Signal();
            await Assert.That(await wait).IsTrue();
        }
    }

    [Test]
    public async Task InlineTimeoutContinuations_ShutdownCompletesWaiter()
    {
        using var signal = new AsyncAutoResetSignal(inlineTimeoutContinuations: true);
        using var cancellation = new CancellationTokenSource();
        signal.RegisterShutdownToken(cancellation.Token);

        var wait = signal.WaitAsync(Timeout.Infinite).AsTask();
        cancellation.Cancel();

        await Assert.That(async () => await wait.WaitAsync(TimeSpan.FromSeconds(1)))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task WaitAsync_AfterIdleShutdown_ThrowsCancellation()
    {
        using var signal = new AsyncAutoResetSignal();
        using var cancellation = new CancellationTokenSource();

        signal.RegisterShutdownToken(cancellation.Token);
        cancellation.Cancel();

        await Assert.That(async () =>
                await signal.WaitAsync(Timeout.Infinite).AsTask().WaitAsync(TimeSpan.FromSeconds(1)))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task WaitAsync_AfterPreCancelledRegistration_ThrowsCancellation()
    {
        using var signal = new AsyncAutoResetSignal();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        signal.RegisterShutdownToken(cancellation.Token);

        await Assert.That(async () =>
                await signal.WaitAsync(Timeout.Infinite).AsTask().WaitAsync(TimeSpan.FromSeconds(1)))
            .Throws<OperationCanceledException>();
    }
}
