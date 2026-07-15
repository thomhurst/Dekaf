using Dekaf.Internal;

namespace Dekaf.Tests.Unit.Internal;

public class AsyncAutoResetSignalTests
{
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
