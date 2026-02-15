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
    public async Task Return_AddsToPool_WhenNotCancelled()
    {
        var pool = new CancellationTokenSourcePool();
        var cts = pool.Rent();

        pool.Return(cts);

        var cts2 = pool.Rent();

        await Assert.That(cts2).IsSameReferenceAs(cts);

        pool.Return(cts2);
    }

    [Test]
    public async Task Return_DisposesInsteadOfPooling_WhenCancelled()
    {
        var pool = new CancellationTokenSourcePool();
        var cts = pool.Rent();
        cts.Cancel();

        pool.Return(cts);

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

        pool.Return(cts);
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

        pool.Return(cts);
    }

    [Test]
    public async Task Pool_ReusesCts_AfterReset()
    {
        var pool = new CancellationTokenSourcePool();
        var cts1 = pool.Rent();

        // Set timeout and return before it fires
        cts1.CancelAfter(TimeSpan.FromSeconds(10));
        pool.Return(cts1);

        // Get same instance back
        var cts2 = pool.Rent();

        // Should be reset (no longer has the old timeout)
        await Assert.That(cts1).IsSameReferenceAs(cts2);
        await Assert.That(cts2.IsCancellationRequested).IsFalse();

        pool.Return(cts2);
    }

    [Test]
    public async Task Clear_DisposesAllPooledInstances()
    {
        var pool = new CancellationTokenSourcePool();

        // Add multiple instances to pool
        var cts1 = pool.Rent();
        var cts2 = pool.Rent();
        var cts3 = pool.Rent();
        pool.Return(cts1);
        pool.Return(cts2);
        pool.Return(cts3);

        pool.Clear();

        // After clear, renting should return new instances
        var newCts = pool.Rent();
        await Assert.That(newCts).IsNotSameReferenceAs(cts1);
        await Assert.That(newCts).IsNotSameReferenceAs(cts2);
        await Assert.That(newCts).IsNotSameReferenceAs(cts3);

        newCts.Dispose();
    }

    [Test]
    public async Task Pool_LimitsSize()
    {
        var pool = new CancellationTokenSourcePool();

        // Rent and return more than max pool size
        var instances = new List<CancellationTokenSource>();
        for (int i = 0; i < 20; i++)
        {
            instances.Add(pool.Rent());
        }

        foreach (var cts in instances)
        {
            pool.Return(cts);
        }

        // Rent back and verify not all were pooled
        var reused = new HashSet<CancellationTokenSource>();
        for (int i = 0; i < 20; i++)
        {
            var cts = pool.Rent();
            if (instances.Contains(cts))
            {
                reused.Add(cts);
            }
            pool.Return(cts);
        }

        // Some should have been pooled (not all 20)
        await Assert.That(reused.Count).IsLessThanOrEqualTo(16);
    }

    [Test]
    public async Task ConcurrentRentReturn_IsThreadSafe()
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
                        pool.Return(cts);
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
