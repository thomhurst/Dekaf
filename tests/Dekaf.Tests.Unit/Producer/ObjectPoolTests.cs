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
    }

    private sealed class TestPool(int maxPoolSize) : ObjectPool<TestItem>(maxPoolSize)
    {
        private int _nextId;

        protected override TestItem Create() => new() { Id = Interlocked.Increment(ref _nextId) };
        protected override void Reset(TestItem item) => item.WasReset = true;
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
    public async Task Return_WhenPoolFull_DiscardsItem()
    {
        var pool = new TestPool(2);
        var item1 = pool.Rent();
        var item2 = pool.Rent();
        var item3 = pool.Rent();
        pool.Return(item1);
        pool.Return(item2);
        pool.Return(item3); // Pool is full (max 2), this should be discarded

        await Assert.That(pool.ApproximateCount).IsEqualTo(2);
    }

    [Test]
    public async Task PreWarm_FillsPoolWithItems()
    {
        var pool = new TestPool(10);

        pool.PreWarm(5);

        await Assert.That(pool.ApproximateCount).IsEqualTo(5);
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
}
