using Dekaf.Resilience;

namespace Dekaf.Tests.Unit.Resilience;

public class CircuitBreakerConcurrencyTests
{
    [Test]
    public async Task CircuitBreaker_ConcurrentFailures_TripsCircuit()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 100
        });

        // Record 200 failures concurrently - circuit should trip at 100
        var tasks = new Task[200];
        for (var i = 0; i < 200; i++)
        {
            tasks[i] = Task.Run(() => cb.RecordFailure());
        }
        await Task.WhenAll(tasks);

        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Open);
    }

    [Test]
    public async Task CircuitBreaker_ConcurrentSuccesses_DoNotCorruptState()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 5
        });

        // Mix successes and failures concurrently
        var tasks = new Task[100];
        for (var i = 0; i < 100; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                if (index % 2 == 0)
                    cb.RecordSuccess();
                else
                    cb.RecordFailure();
            });
        }
        await Task.WhenAll(tasks);

        // State should be valid (either Closed or Open)
        var state = cb.State;
        await Assert.That(state is CircuitBreakerState.Closed or CircuitBreakerState.Open).IsTrue();
    }

    [Test]
    public async Task CircuitBreaker_ConcurrentResetAndFailure_DoesNotThrow()
    {
        var cb = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            BreakDuration = TimeSpan.FromMilliseconds(10)
        });

        // Run concurrent resets and failures for a short duration
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var tasks = new[]
        {
            Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    cb.RecordFailure();
                    await Task.Yield();
                }
            }),
            Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    cb.Reset();
                    await Task.Yield();
                }
            }),
            Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    _ = cb.State;
                    _ = cb.IsAllowingRequests;
                    _ = cb.ConsecutiveFailures;
                    await Task.Yield();
                }
            })
        };

        // Should complete without throwing
        await Task.WhenAll(tasks);

        // State should be valid
        var state = cb.State;
        await Assert.That(state is CircuitBreakerState.Closed or CircuitBreakerState.Open or CircuitBreakerState.HalfOpen).IsTrue();
    }
}
