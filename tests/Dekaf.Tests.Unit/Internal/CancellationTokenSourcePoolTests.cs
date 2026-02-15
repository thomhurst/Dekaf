using Dekaf.Internal;

namespace Dekaf.Tests.Unit.Internal;

public class CancellationTokenSourcePoolTests
{
    [Test]
    public async Task Rent_ReturnsNewCts_WhenPoolEmpty()
    {
        var pool = new CancellationTokenSourcePool();

        var cts = pool.Rent();

        await Assert.That(cts).IsNotNull();
        await Assert.That(cts.IsCancellationRequested).IsFalse();

        cts.Dispose();
    }

    [Test]
    public async Task Dispose_ReturnsToPool_WhenNotCancelled()
    {
        var pool = new CancellationTokenSourcePool();
        var cts = pool.Rent();

        cts.Dispose();

        var cts2 = pool.Rent();

        await Assert.That(cts2).IsSameReferenceAs(cts);

        cts2.Dispose();
    }

    [Test]
    public async Task Dispose_DoesNotReturnToPool_WhenCancelled()
    {
        var pool = new CancellationTokenSourcePool();
        var cts = pool.Rent();
        cts.Cancel();

        cts.Dispose();

        var cts2 = pool.Rent();

        await Assert.That(cts2).IsNotSameReferenceAs(cts);

        cts2.Dispose();
    }

    [Test]
    public async Task RentedCts_CanBeCancelled()
    {
        var pool = new CancellationTokenSourcePool();
        var cts = pool.Rent();

        cts.Cancel();

        await Assert.That(cts.IsCancellationRequested).IsTrue();

        cts.Dispose();
    }

    [Test]
    public async Task RentedCts_CanBeCancelledAfterDelay()
    {
        var pool = new CancellationTokenSourcePool();
        var cts = pool.Rent();

        // Use TaskCompletionSource for reliable event-based waiting
        var cancelledTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        cts.Token.Register(() => cancelledTcs.TrySetResult(true));

        await Assert.That(cts.IsCancellationRequested).IsFalse();

        // Use longer timeout for CI reliability (Windows CI timers can be delayed under load)
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        // Wait for cancellation event with generous timeout
        var completed = await Task.WhenAny(cancelledTcs.Task, Task.Delay(TimeSpan.FromSeconds(30))) == cancelledTcs.Task;

        await Assert.That(completed).IsTrue();
        await Assert.That(cts.IsCancellationRequested).IsTrue();

        cts.Dispose();
    }

    [Test]
    public async Task Pool_ReusesCts_AfterReset()
    {
        var pool = new CancellationTokenSourcePool();
        var cts1 = pool.Rent();

        // Set timeout and return before it fires
        cts1.CancelAfter(TimeSpan.FromSeconds(10));
        cts1.Dispose();

        // Get same instance back
        var cts2 = pool.Rent();

        // Should be reset (no longer has the old timeout)
        await Assert.That(cts1).IsSameReferenceAs(cts2);
        await Assert.That(cts2.IsCancellationRequested).IsFalse();

        cts2.Dispose();
    }

    [Test]
    public async Task Clear_DisposesAllPooledInstances()
    {
        var pool = new CancellationTokenSourcePool();

        // Add multiple instances to pool via Dispose (auto-return)
        var cts1 = pool.Rent();
        var cts2 = pool.Rent();
        var cts3 = pool.Rent();
        cts1.Dispose();
        cts2.Dispose();
        cts3.Dispose();

        pool.Clear();

        // After clear, renting should return new instances
        var newCts = pool.Rent();
        await Assert.That(newCts).IsNotSameReferenceAs(cts1);
        await Assert.That(newCts).IsNotSameReferenceAs(cts2);
        await Assert.That(newCts).IsNotSameReferenceAs(cts3);

        newCts.Dispose();
    }

    [Test]
    public async Task Rent_ReturnsPooledCancellationTokenSource()
    {
        var pool = new CancellationTokenSourcePool();

        var cts = pool.Rent();

        await Assert.That(cts).IsTypeOf<CancellationTokenSourcePool.PooledCancellationTokenSource>();

        cts.Dispose();
    }

    [Test]
    public async Task Dispose_IsIdempotent()
    {
        var pool = new CancellationTokenSourcePool();
        var cts = pool.Rent();

        // Multiple disposes should not throw
        cts.Dispose();
        cts.Dispose();
        cts.Dispose();

        await Task.CompletedTask;
    }

    [Test]
    public async Task UsingPattern_AutoReturnsToPool()
    {
        var pool = new CancellationTokenSourcePool();
        CancellationTokenSourcePool.PooledCancellationTokenSource original;

        // Simulate using var pattern
        using (var cts = pool.Rent())
        {
            original = cts;
            cts.CancelAfter(TimeSpan.FromSeconds(10));
        }
        // cts.Dispose() called here, auto-returns to pool

        var reused = pool.Rent();
        await Assert.That(reused).IsSameReferenceAs(original);

        reused.Dispose();
    }

    [Test]
    public async Task ConcurrentRentDispose_IsThreadSafe()
    {
        var pool = new CancellationTokenSourcePool();
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 50; j++)
                    {
                        var cts = pool.Rent();
                        cts.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions.Count).IsEqualTo(0);
    }
}
