using System.Reflection;
using System.Runtime.Loader;
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

        // Rent, complete, and await
        var source = pool.Rent();
        source.SetResult(42);
        await source.Task.ConfigureAwait(false);

        // A second rent returns the same instance, proving the first source auto-returned.
        var reused = pool.Rent();
        await Assert.That(reused).IsSameReferenceAs(source);
        reused.SetResult(43);
        await reused.Task.ConfigureAwait(false);
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

        var originalSources = new HashSet<PooledValueTaskSource<int>>(
            sources,
            ReferenceEqualityComparer.Instance);
        var secondRent = new List<PooledValueTaskSource<int>>(sources.Count);
        var reusedCount = 0;
        for (var i = 0; i < sources.Count; i++)
        {
            var rented = pool.Rent();
            secondRent.Add(rented);
            if (originalSources.Contains(rented))
                reusedCount++;
        }

        await Assert.That(reusedCount).IsEqualTo(maxSize);
        foreach (var source in secondRent)
        {
            source.SetResult(2);
            await source.Task.ConfigureAwait(false);
        }
    }

    [Test]
    public async Task MaxPoolSize_ReturnsConfiguredValue()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 512);

        await Assert.That(pool.MaxPoolSize).IsEqualTo(512);
    }

    [Test]
    public async Task FallbackMaxPoolSize_Is4096()
    {
        var fallbackSize = ValueTaskSourcePool.FallbackMaxPoolSize;
        await Assert.That(fallbackSize).IsEqualTo(4096);
    }

    [Test]
    public async Task CalculatePoolSize_ScalesWithBufferMemoryAndBatchSize()
    {
        // 64 MB buffer / 1 MB batch = 64 batches * 1024 msgs/batch = 65536, clamped to max
        var poolSize = ValueTaskSourcePool.CalculatePoolSize(67108864UL, 1048576);
        await Assert.That(poolSize).IsEqualTo(ValueTaskSourcePool.MaxAutoPoolSize);

        // 2 MB buffer / 1 MB batch = 2 batches * 1024 msgs/batch = 2048
        var poolSize2 = ValueTaskSourcePool.CalculatePoolSize(2097152UL, 1048576);
        await Assert.That(poolSize2).IsEqualTo(2048);
    }

    [Test]
    public async Task CalculatePoolSize_ClampsToMinimum()
    {
        // 256 KB buffer / 1 MB batch = 0 batches * 1024 = 0, should clamp to MinAutoPoolSize
        var poolSize = ValueTaskSourcePool.CalculatePoolSize(262144UL, 1048576);
        await Assert.That(poolSize).IsEqualTo(ValueTaskSourcePool.MinAutoPoolSize);
    }

    [Test]
    public async Task CalculatePoolSize_ClampsToMaximum()
    {
        // 100 GB buffer / 16 KB batch = ~6.5M batches, should clamp to MaxAutoPoolSize
        var poolSize = ValueTaskSourcePool.CalculatePoolSize(107374182400UL, 16384);
        await Assert.That(poolSize).IsEqualTo(ValueTaskSourcePool.MaxAutoPoolSize);
    }

    [Test]
    public async Task CalculatePoolSize_ThrowsForZeroBatchSize()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => ValueTaskSourcePool.CalculatePoolSize(1073741824UL, 0));

        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task CalculatePoolSize_ThrowsForNegativeBatchSize()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => ValueTaskSourcePool.CalculatePoolSize(1073741824UL, -1));

        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task CalculatePoolSize_SingleBatchBuffer_Returns1024()
    {
        // 1 MB buffer / 1 MB batch = 1 batch * 1024 msgs = 1024 (above MinAutoPoolSize, no clamping)
        var poolSize = ValueTaskSourcePool.CalculatePoolSize(1048576UL, 1048576);
        await Assert.That(poolSize).IsEqualTo(1024);
    }

    [Test]
    public async Task Dispose_PreventsRent()
    {
        var pool = new ValueTaskSourcePool<int>();
        await pool.DisposeAsync().ConfigureAwait(false);

        var exception = Assert.Throws<ObjectDisposedException>(() => pool.Rent());

        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Dispose_ClearsPoolAndPreventsFurtherRent()
    {
        var pool = new ValueTaskSourcePool<int>();

        // Add items to pool
        var source = pool.Rent();
        source.SetResult(42);
        await source.Task.ConfigureAwait(false);

        await pool.DisposeAsync().ConfigureAwait(false);

        await Assert.That(() => pool.Rent()).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Constructor_ThrowsForInvalidMaxSize()
    {
        var exception1 = Assert.Throws<ArgumentOutOfRangeException>(
            () => _ = new ValueTaskSourcePool<int>(maxPoolSize: 0));

        await Assert.That(exception1).IsNotNull();

        var exception2 = Assert.Throws<ArgumentOutOfRangeException>(
            () => _ = new ValueTaskSourcePool<int>(maxPoolSize: -1));

        await Assert.That(exception2).IsNotNull();
    }

    [Test]
    public async Task Return_AfterDispose_IsSilent()
    {
        var pool = new ValueTaskSourcePool<int>();
        var source = pool.Rent();

        // Dispose the pool
        await pool.DisposeAsync().ConfigureAwait(false);

        // Complete the source - Return should be silent (not throw)
        source.SetResult(42);
        await source.Task.ConfigureAwait(false);
    }

    [Test]
    public async Task HighContention_StressTest()
    {
        // Stress test with many concurrent operations
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 50);
        var completedCount = 0;
        const int operationCount = 1000;
        const int threadCount = 20;

        var tasks = new List<Task>();
        for (int t = 0; t < threadCount; t++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < operationCount / threadCount; i++)
                {
                    var source = pool.Rent();
                    source.SetResult(i);
                    await source.Task.ConfigureAwait(false);
                    Interlocked.Increment(ref completedCount);
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(completedCount).IsEqualTo(operationCount);
    }

    [Test]
    public async Task Pool_WithMaxSize1_ForcesReuse()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 1);
        PooledValueTaskSource<int>? firstSource = null;
        var reuseCount = 0;

        for (int i = 0; i < 10; i++)
        {
            var source = pool.Rent();

            if (firstSource == null)
            {
                firstSource = source;
            }
            else if (ReferenceEquals(source, firstSource))
            {
                reuseCount++;
            }

            source.SetResult(i);
            await source.Task.ConfigureAwait(false);
        }

        // With max size 1, after the first iteration, we should always reuse
        await Assert.That(reuseCount).IsEqualTo(9);
    }

    [Test]
    public async Task DefaultConstructor_UsesDefaultMaxSize()
    {
        var pool = new ValueTaskSourcePool<int>();

        await Assert.That(pool.MaxPoolSize).IsEqualTo(ValueTaskSourcePool.FallbackMaxPoolSize);
    }

    [Test]
    public async Task ApproximateCount_ReturnsZero_WhenDiagnosticsAreNotEnabled()
    {
        var pool = new ValueTaskSourcePool<int>(maxPoolSize: 10);

        await Assert.That(pool.ApproximateCount).IsEqualTo(0);

        // Rent and return 5 sources
        var sources = new List<PooledValueTaskSource<int>>();
        for (int i = 0; i < 5; i++)
        {
            sources.Add(pool.Rent());
        }

        await Assert.That(pool.ApproximateCount).IsEqualTo(0);

        // Complete and await all
        for (int i = 0; i < 5; i++)
        {
            sources[i].SetResult(i);
            await sources[i].Task.ConfigureAwait(false);
        }

        // Tracking stays disabled even after sources return to the pool.
        await Assert.That(pool.ApproximateCount).IsEqualTo(0);
    }

    [Test]
    [NotInParallel("ValueTaskSourcePoolDiagnostics")]
    public async Task ApproximateCount_TracksPoolSize_WhenDiagnosticsAreEnabledBeforeLoad()
    {
        // Lock the normal test assembly into the default-off mode before changing the process-wide
        // switch. A separately loaded Dekaf assembly then models an application opting in before
        // its first pool use.
        await Assert.That(ValueTaskSourcePool.TrackRetainedCount).IsFalse();

        AppContext.SetSwitch(ValueTaskSourcePool.TrackRetainedCountSwitchName, true);
        try
        {
            var count = await RunDiagnosticsScenarioInIsolatedContextAsync().ConfigureAwait(false);
            await Assert.That(count).IsEqualTo(1);
        }
        finally
        {
            AppContext.SetSwitch(ValueTaskSourcePool.TrackRetainedCountSwitchName, false);
        }
    }

    [Test]
    public async Task ConcurrentRentAndReturn_MaintainsConsistency()
    {
        const int maxPoolSize = 100;
        const int workerCount = 10;
        var pool = new ValueTaskSourcePool<int>(maxPoolSize);
        var barrier = new Barrier(workerCount);
        var tasks = new List<Task>();

        // Concurrent workers exercise the shared storage.
        for (var t = 0; t < workerCount; t++)
        {
            tasks.Add(Task.Run(async () =>
            {
                barrier.SignalAndWait();

                for (int i = 0; i < 100; i++)
                {
                    var source = pool.Rent();
                    source.SetResult(i);
                    var result = await source.Task.ConfigureAwait(false);
                    await Assert.That(result).IsEqualTo(i);
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Pool remains usable after concurrent rent/return operations.
        var source = pool.Rent();
        source.SetResult(42);
        await Assert.That(await source.Task.ConfigureAwait(false)).IsEqualTo(42);
    }

    private static async Task<int> RunDiagnosticsScenarioInIsolatedContextAsync()
    {
        var assemblyPath = typeof(ValueTaskSourcePool).Assembly.Location;
        var loadContext = new IsolatedLoadContext(assemblyPath);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var openPoolType = assembly.GetType("Dekaf.Producer.ValueTaskSourcePool`1", throwOnError: true)!;
            var poolType = openPoolType.MakeGenericType(typeof(int));
            var pool = Activator.CreateInstance(poolType, 10)!;
            var source = poolType.GetMethod(nameof(ValueTaskSourcePool<int>.Rent))!.Invoke(pool, null)!;
            var sourceType = source.GetType();

            sourceType.GetMethod(nameof(PooledValueTaskSource<int>.SetResult))!.Invoke(source, [42]);
            var task = (ValueTask<int>)sourceType
                .GetProperty(nameof(PooledValueTaskSource<int>.Task))!
                .GetValue(source)!;
            await task.ConfigureAwait(false);

            var count = (int)poolType
                .GetProperty(nameof(ValueTaskSourcePool<int>.ApproximateCount))!
                .GetValue(pool)!;
            var disposeTask = (ValueTask)poolType
                .GetMethod(nameof(ValueTaskSourcePool<int>.DisposeAsync))!
                .Invoke(pool, null)!;
            await disposeTask.ConfigureAwait(false);
            return count;
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private sealed class IsolatedLoadContext(string assemblyPath) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}
