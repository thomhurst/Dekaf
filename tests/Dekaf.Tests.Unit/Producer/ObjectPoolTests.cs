using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for <see cref="ObjectPool{T}"/> base class verifying rent/return, overflow,
/// pre-warming, and miss tracking behavior.
/// </summary>
public class ObjectPoolTests
{
    private sealed class TestItem
    {
        public int Id { get; set; }
        public bool WasReset { get; set; }
        public bool WasDestroyed { get; set; }
    }

    private sealed class TestPool(int maxPoolSize, bool threadLocalFastPath = true)
        : ObjectPool<TestItem>(maxPoolSize, threadLocalFastPath)
    {
        private int _nextId;

        protected override TestItem Create() => new() { Id = Interlocked.Increment(ref _nextId) };
        protected override void Reset(TestItem item)
        {
            item.WasReset = true;
            item.WasDestroyed = false;
        }

        protected override void Destroy(TestItem item) => item.WasDestroyed = true;
    }

    [Test]
    public async Task Rent_WhenPoolEmpty_CreatesNewItem()
    {
        var pool = new TestPool(10);

        var item = pool.Rent();

        await Assert.That(item).IsNotNull();
        await Assert.That(item.Id).IsEqualTo(1);
    }

    [Test]
    public async Task Rent_WhenPoolEmpty_IncrementsMissCounter()
    {
        var pool = new TestPool(10);

        _ = pool.Rent();
        _ = pool.Rent();

        await Assert.That(pool.Misses).IsEqualTo(2);
    }

    [Test]
    public async Task Rent_AfterReturn_ReusesItem()
    {
        var pool = new TestPool(10);
        var item = pool.Rent();
        pool.Return(item);

        var reused = pool.Rent();

        await Assert.That(reused).IsSameReferenceAs(item);
    }

    [Test]
    public async Task Rent_AfterReturn_DoesNotIncrementMissCounter()
    {
        var pool = new TestPool(10);
        var item = pool.Rent();
        var missesBeforeReturn = pool.Misses;
        pool.Return(item);

        _ = pool.Rent(); // Should hit pool, not miss

        await Assert.That(pool.Misses).IsEqualTo(missesBeforeReturn);
    }

    [Test]
    public async Task Return_CallsReset()
    {
        var pool = new TestPool(10);
        var item = pool.Rent();

        await Assert.That(item.WasReset).IsFalse();

        pool.Return(item);

        await Assert.That(item.WasReset).IsTrue();
    }

    [Test]
    public async Task Return_RetainsOneThreadLocalItemBeyondSharedCapacity()
    {
        var pool = new TestPool(2);
        var item1 = pool.Rent();
        var item2 = pool.Rent();
        var item3 = pool.Rent();
        pool.Return(item1);
        pool.Return(item2);
        pool.Return(item3);

        var retainedCount = pool.ApproximateCount;

        await Assert.That(retainedCount).IsEqualTo(3);
        await Assert.That(item1.WasDestroyed).IsFalse();
        await Assert.That(item2.WasDestroyed).IsFalse();
        await Assert.That(item3.WasDestroyed).IsFalse();
    }

    [Test]
    public async Task Return_WhenStrictPoolFull_DiscardsResetItem()
    {
        var pool = new TestPool(2, threadLocalFastPath: false);
        var item1 = pool.Rent();
        var item2 = pool.Rent();
        var item3 = pool.Rent();
        pool.Return(item1);
        pool.Return(item2);
        pool.Return(item3);

        await Assert.That(pool.ApproximateCount).IsEqualTo(2);
        await Assert.That(item3.WasReset).IsTrue();
        var rented = pool.Rent();
        await Assert.That(rented).IsNotSameReferenceAs(item3);
    }

    [Test]
    public async Task PreWarm_FillsPoolWithItems()
    {
        var pool = new TestPool(10);

        pool.PreWarm(5);

        await Assert.That(pool.ApproximateCount).IsEqualTo(5);
    }

    [Test]
    public async Task PreWarm_DoesNotResetFreshItems()
    {
        var pool = new TestPool(2);

        pool.PreWarm(1);
        var item = pool.Rent();

        await Assert.That(item.WasReset).IsFalse();
    }

    [Test]
    public async Task PreWarm_RentDoesNotIncrementMissCounter()
    {
        var pool = new TestPool(10);
        pool.PreWarm(5);

        _ = pool.Rent();
        _ = pool.Rent();

        await Assert.That(pool.Misses).IsEqualTo(0);
    }

    [Test]
    public async Task PreWarm_CapsAtMaxPoolSize()
    {
        var pool = new TestPool(3);

        pool.PreWarm(100);

        await Assert.That(pool.ApproximateCount).IsEqualTo(3);
    }

    [Test]
    public async Task Clear_EmptiesPool()
    {
        var pool = new TestPool(10);
        pool.PreWarm(5);

        pool.Clear();

        await Assert.That(pool.ApproximateCount).IsEqualTo(0);
    }

    [Test]
    public async Task MaxPoolSize_ReturnsConfiguredValue()
    {
        var pool = new TestPool(42);

        await Assert.That(pool.MaxPoolSize).IsEqualTo(42);
    }

    [Test]
    public async Task RatchetMaxPoolSize_GrowsAndPreservesRetainedItems()
    {
        var pool = new TestPool(1);
        var item = pool.Rent();
        pool.Return(item);
        var missesBeforeRatchet = pool.Misses;

        pool.RatchetMaxPoolSize(4);
        var retained = pool.Rent();

        await Assert.That(pool.MaxPoolSize).IsEqualTo(4);
        await Assert.That(item.WasDestroyed).IsFalse();
        await Assert.That(retained).IsSameReferenceAs(item);
        await Assert.That(pool.Misses).IsEqualTo(missesBeforeRatchet);
    }

    [Test]
    public async Task RatchetMaxPoolSize_DoesNotShrink()
    {
        var pool = new TestPool(4);

        pool.RatchetMaxPoolSize(2);

        await Assert.That(pool.MaxPoolSize).IsEqualTo(4);
    }

    [Test]
    public async Task Clear_AfterRatchet_DestroysMigratedItems()
    {
        var pool = new TestPool(1);
        var item = pool.Rent();
        pool.Return(item);
        pool.RatchetMaxPoolSize(2);

        pool.Clear();

        await Assert.That(item.WasDestroyed).IsTrue();
        await Assert.That(pool.Rent()).IsNotSameReferenceAs(item);
    }

    [Test]
    [Repeat(10)]
    public async Task ConcurrentRentReturn_MaintainsBounds()
    {
        const int maxPool = 32;
        const int threadCount = 8;
        const int opsPerThread = 500;
        var pool = new TestPool(maxPool);
        pool.PreWarm(maxPool);

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < opsPerThread; i++)
            {
                var item = pool.Rent();
                pool.Return(item);
            }
        }));

        await Task.WhenAll(tasks);

        // Pool count should be within bounds (approximate due to lock-free design)
        await Assert.That(pool.ApproximateCount).IsGreaterThanOrEqualTo(0);
        await Assert.That(pool.ApproximateCount).IsLessThanOrEqualTo(maxPool + threadCount); // Small overshoot OK
    }

    [Test]
    [Repeat(10)]
    public async Task ConcurrentOverflow_NeverRentsDestroyedItem()
    {
        const int threadCount = 8;
        const int opsPerThread = 1_000;
        var pool = new TestPool(1);
        pool.PreWarm(1);
        var corruptedRents = 0;

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < opsPerThread; i++)
            {
                var item = pool.Rent();
                if (item.WasDestroyed)
                    Interlocked.Increment(ref corruptedRents);

                pool.Return(item);
            }
        }));

        await Task.WhenAll(tasks);

        await Assert.That(corruptedRents).IsEqualTo(0);
        await Assert.That(pool.ApproximateCount).IsBetween(0, threadCount + 1);
    }
}
