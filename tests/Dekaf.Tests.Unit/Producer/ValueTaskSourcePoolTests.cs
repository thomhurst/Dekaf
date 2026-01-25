using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class ValueTaskSourcePoolTests
{
    [Test]
    public async Task Rent_ReturnsNewInstance()
    {
        var pool = new ValueTaskSourcePool<int>();

        var source = pool.Rent();

        await Assert.That(source).IsNotNull();
    }

    [Test]
    public async Task Rent_SetResult_AwaitReturnsValue()
    {
        var pool = new ValueTaskSourcePool<int>();

        var source = pool.Rent();
        source.SetResult(42);

        var result = await source.Task.ConfigureAwait(false);
        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Rent_SetException_AwaitThrows()
    {
        var pool = new ValueTaskSourcePool<int>();

        var source = pool.Rent();
        var expectedException = new InvalidOperationException("Test exception");
        source.SetException(expectedException);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await source.Task.ConfigureAwait(false);
        });
    }

    [Test]
    public async Task Source_AutoReturnsToPool_AfterAwait()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 10);

        // Pool should start empty
        await Assert.That(pool.ApproximateCount).IsEqualTo(0);

        // Rent, complete, and await
        var source = pool.Rent();
        source.SetResult(42);
        await source.Task.ConfigureAwait(false);

        // Source should have auto-returned to pool
        await Assert.That(pool.ApproximateCount).IsEqualTo(1);
    }

    [Test]
    public async Task Source_CanBeReused_AfterReturnToPool()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 1);
        var results = new List<int>();

        // First use
        var source1 = pool.Rent();
        source1.SetResult(1);
        results.Add(await source1.Task.ConfigureAwait(false));

        // Second use - should get the same instance back from pool
        var source2 = pool.Rent();
        source2.SetResult(2);
        results.Add(await source2.Task.ConfigureAwait(false));

        await Assert.That(results[0]).IsEqualTo(1);
        await Assert.That(results[1]).IsEqualTo(2);

        // Both operations should succeed with the same pooled instance
        await Assert.That(source1).IsSameReferenceAs(source2);
    }

    [Test]
    public async Task MultipleRentAwait_WorksCorrectly()
    {
        var pool = new ValueTaskSourcePool<string>();
        var results = new List<string>();

        for (int i = 0; i < 10; i++)
        {
            var source = pool.Rent();
            var value = $"test-{i}";
            source.SetResult(value);
            results.Add(await source.Task.ConfigureAwait(false));
        }

        await Assert.That(results.Count).IsEqualTo(10);
        for (int i = 0; i < 10; i++)
        {
            await Assert.That(results[i]).IsEqualTo($"test-{i}");
        }
    }

    [Test]
    public async Task TrySetResult_ReturnsFalse_WhenAlreadyCompleted()
    {
        var pool = new ValueTaskSourcePool<int>();

        var source = pool.Rent();
        var first = source.TrySetResult(42);
        var second = source.TrySetResult(43);

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();

        var result = await source.Task.ConfigureAwait(false);
        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task TrySetException_ReturnsFalse_WhenAlreadyCompleted()
    {
        var pool = new ValueTaskSourcePool<int>();

        var source = pool.Rent();
        var first = source.TrySetResult(42);
        var second = source.TrySetException(new InvalidOperationException("Should fail"));

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
    }

    [Test]
    public async Task TrySetCanceled_ReturnsFalse_WhenAlreadyCompleted()
    {
        var pool = new ValueTaskSourcePool<int>();

        var source = pool.Rent();
        var first = source.TrySetResult(42);
        var second = source.TrySetCanceled(CancellationToken.None);

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
    }

    [Test]
    public async Task ConcurrentRentAwait_IsThreadSafe()
    {
        var pool = new ValueTaskSourcePool<int>();
        var tasks = new List<Task>();
        var completedCount = 0;

        // Simulate concurrent rent/return operations
        for (int i = 0; i < 100; i++)
        {
            var localI = i;
            var task = Task.Run(async () =>
            {
                var source = pool.Rent();
                source.SetResult(localI);
                await source.Task.ConfigureAwait(false);
                Interlocked.Increment(ref completedCount);
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(completedCount).IsEqualTo(100);
    }

    [Test]
    public async Task Pool_RespectsMaxSize()
    {
        const int maxSize = 5;
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: maxSize);

        // Create more sources than the max pool size
        var sources = new List<PooledValueTaskSource<int>>();
        for (int i = 0; i < maxSize + 3; i++)
        {
            sources.Add(pool.Rent());
        }

        // Complete and await all - they will try to return to pool
        foreach (var source in sources)
        {
            source.SetResult(1);
            await source.Task.ConfigureAwait(false);
        }

        // Pool should only contain maxSize items (extras are discarded)
        await Assert.That(pool.ApproximateCount).IsLessThanOrEqualTo(maxSize);
    }

    [Test]
    public async Task MaxPoolSize_ReturnsConfiguredValue()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 512);

        await Assert.That(pool.MaxPoolSize).IsEqualTo(512);
    }

    [Test]
    public async Task DefaultMaxPoolSize_Is1024()
    {
        var defaultSize = ValueTaskSourcePool<int>.DefaultMaxPoolSize;
        await Assert.That(defaultSize).IsEqualTo(1024);
    }

    [Test]
    public async Task Dispose_PreventsRent()
    {
        var pool = new ValueTaskSourcePool<int>();
        await pool.DisposeAsync().ConfigureAwait(false);

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
        {
            pool.Rent();
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task Dispose_ClearsPool()
    {
        var pool = new ValueTaskSourcePool<int>();

        // Add items to pool
        var source = pool.Rent();
        source.SetResult(42);
        await source.Task.ConfigureAwait(false);
        await Assert.That(pool.ApproximateCount).IsEqualTo(1);

        // Dispose
        await pool.DisposeAsync().ConfigureAwait(false);

        // Pool should be empty
        await Assert.That(pool.ApproximateCount).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_ThrowsForInvalidMaxSize()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
        {
            _ = new ValueTaskSourcePool<int>(maxPoolSize: 0);
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
        {
            _ = new ValueTaskSourcePool<int>(maxPoolSize: -1);
            return Task.CompletedTask;
        });
    }
}
