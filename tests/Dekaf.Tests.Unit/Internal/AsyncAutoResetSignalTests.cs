using Dekaf.Internal;

namespace Dekaf.Tests.Unit.Internal;

public class AsyncAutoResetSignalTests
{
    [Test]
    public async Task InlineContinuations_ResumeOnSignalingThreadAndRearm()
    {
        using var signal = new AsyncAutoResetSignal(inlineContinuations: true);
        for (var index = 0; index < 100; index++)
        {
            var awaiter = signal.WaitAsync(Timeout.Infinite).ConfigureAwait(false).GetAwaiter();
            var resumedOn = -1;
            awaiter.UnsafeOnCompleted(() =>
            {
                awaiter.GetResult();
                resumedOn = Environment.CurrentManagedThreadId;
            });
            var signalingThread = Environment.CurrentManagedThreadId;
            signal.Signal();
            await Assert.That(resumedOn).IsEqualTo(signalingThread);
        }
    }

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

    [Test]
    public async Task Signal_WhenTimeoutCompletesObservedWaiterFirst_StoresSignalForNextWait()
    {
        Task<bool>? timedWait = null;
        var observedWaiter = 0;
        using var signalObservedWaiter = new ManualResetEventSlim();
        using var signal = new AsyncAutoResetSignal(
            inlineTimeoutContinuations: false,
            inlineContinuations: false,
            afterSignalObservedWaiterForTesting: () =>
            {
                // Hold Signal between observing the waiter and taking it until the timer
                // has completed that waiter.
                if (Interlocked.Exchange(ref observedWaiter, 1) == 0)
                {
                    signalObservedWaiter.Set();
                    timedWait!.Wait(TimeSpan.FromSeconds(10));
                }
            },
            // Hold the timer callback until Signal has observed the waiter, so the timeout
            // cannot complete the wait before Signal reaches its hook.
            beforeTimeoutCompletionForTesting: () => signalObservedWaiter.Wait(TimeSpan.FromSeconds(10)),
            afterTimeoutCallbackForTesting: null);

        timedWait = signal.WaitAsync(1).AsTask();
        signal.Signal();

        await Assert.That(observedWaiter).IsEqualTo(1);
        await Assert.That(await timedWait).IsFalse();
        await Assert.That(await signal.WaitAsync(0)).IsTrue();
    }

    [Test]
    [Arguments(Timeout.Infinite)]
    [Arguments(30_000)]
    public async Task StaleTimeoutCallback_DoesNotCompleteLaterWait(int laterTimeoutMs)
    {
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var callbackFinished = new ManualResetEventSlim();
        var held = 0;
        using var signal = new AsyncAutoResetSignal(
            inlineTimeoutContinuations: false,
            inlineContinuations: false,
            afterSignalObservedWaiterForTesting: null,
            beforeTimeoutCompletionForTesting: () =>
            {
                // Pause the first wait's timer callback just before it takes the waiter.
                if (Interlocked.Exchange(ref held, 1) == 0)
                {
                    callbackEntered.Set();
                    releaseCallback.Wait(TimeSpan.FromSeconds(10));
                }
            },
            afterTimeoutCallbackForTesting: () =>
            {
                if (releaseCallback.IsSet)
                    callbackFinished.Set();
            });

        var first = signal.WaitAsync(1);
        await Assert.That(callbackEntered.Wait(TimeSpan.FromSeconds(10))).IsTrue();
        signal.Signal();
        await Assert.That(await first).IsTrue();

        var later = signal.WaitAsync(laterTimeoutMs);
        releaseCallback.Set();
        await Assert.That(callbackFinished.Wait(TimeSpan.FromSeconds(10))).IsTrue();

        await Assert.That(later.IsCompleted).IsFalse();
        signal.Signal();
        await Assert.That(await later).IsTrue();
    }

    [Test]
    public async Task StaleTimeoutCallback_LaterFiniteWaitStillTimesOut()
    {
        using var callbackEntered = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        var held = 0;
        using var signal = new AsyncAutoResetSignal(
            inlineTimeoutContinuations: false,
            inlineContinuations: false,
            afterSignalObservedWaiterForTesting: null,
            beforeTimeoutCompletionForTesting: () =>
            {
                if (Interlocked.Exchange(ref held, 1) == 0)
                {
                    callbackEntered.Set();
                    releaseCallback.Wait(TimeSpan.FromSeconds(10));
                }
            },
            afterTimeoutCallbackForTesting: null);

        var first = signal.WaitAsync(1);
        await Assert.That(callbackEntered.Wait(TimeSpan.FromSeconds(10))).IsTrue();
        signal.Signal();
        await Assert.That(await first).IsTrue();

        var later = signal.WaitAsync(50).AsTask();
        releaseCallback.Set();

        await Assert.That(await later.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
    }
}
