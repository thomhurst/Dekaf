using Dekaf.Internal;
using System.Collections.Concurrent;
using System.Reflection;

namespace Dekaf.Tests.Unit.Internal;

public class LockFreeStackTests
{
    private sealed class Item
    {
        public int Value { get; init; }
    }

    [Test]
    public async Task TryPush_TryPop_RoundTrip()
    {
        var stack = new LockFreeStack<Item>(4);
        var item = new Item { Value = 42 };

        var pushed = stack.TryPush(item);
        var popped = stack.TryPop(out var result);

        await Assert.That(pushed).IsTrue();
        await Assert.That(popped).IsTrue();
        await Assert.That(result).IsSameReferenceAs(item);
    }

    [Test]
    public async Task TryPush_ReturnsFalse_WhenFull()
    {
        var stack = new LockFreeStack<Item>(2);

        var push1 = stack.TryPush(new Item { Value = 1 });
        var push2 = stack.TryPush(new Item { Value = 2 });
        var push3 = stack.TryPush(new Item { Value = 3 });

        await Assert.That(push1).IsTrue();
        await Assert.That(push2).IsTrue();
        await Assert.That(push3).IsFalse();
    }

    [Test]
    public async Task TryPop_ReturnsFalse_WhenEmpty()
    {
        var stack = new LockFreeStack<Item>(4);

        var popped = stack.TryPop(out var result);

        await Assert.That(popped).IsFalse();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TryPop_AfterPausedPushCompletes_ReturnsPublishedItem()
    {
        var pushPaused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumePush = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stack = new LockFreeStack<Item>(1, () =>
        {
            pushPaused.SetResult();
            resumePush.Task.GetAwaiter().GetResult();
        });
        var item = new Item { Value = 42 };
        var pushTask = Task.Run(() => stack.TryPush(item));
        bool poppedWhilePushPaused;

        try
        {
            await pushPaused.Task.WaitAsync(TimeSpan.FromSeconds(10));
            poppedWhilePushPaused = stack.TryPop(out _);
        }
        finally
        {
            resumePush.TrySetResult();
        }

        var pushed = await pushTask.WaitAsync(TimeSpan.FromSeconds(10));
        var poppedAfterPushCompleted = stack.TryPop(out var result);

        await Assert.That(poppedWhilePushPaused).IsFalse();
        await Assert.That(pushed).IsTrue();
        await Assert.That(poppedAfterPushCompleted).IsTrue();
        await Assert.That(result).IsSameReferenceAs(item);
    }

    [Test]
    public async Task Count_ReflectsStackState()
    {
        var stack = new LockFreeStack<Item>(4);

        await Assert.That(stack.Count).IsEqualTo(0);

        stack.TryPush(new Item { Value = 1 });
        await Assert.That(stack.Count).IsEqualTo(1);

        stack.TryPush(new Item { Value = 2 });
        await Assert.That(stack.Count).IsEqualTo(2);

        stack.TryPop(out _);
        await Assert.That(stack.Count).IsEqualTo(1);
    }

    [Test]
    public async Task TryPush_TryPop_DoNotAllocateInSteadyState()
    {
        var stack = new LockFreeStack<Item>(1);
        var item = new Item { Value = 42 };

        for (var i = 0; i < 100; i++)
        {
            stack.TryPush(item);
            stack.TryPop(out _);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            stack.TryPush(item);
            stack.TryPop(out _);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task Capacity_MatchesConstructorArgument()
    {
        var stack = new LockFreeStack<Item>(16);

        await Assert.That(stack.Capacity).IsEqualTo(16);
    }

    [Test]
    public async Task Clear_ResetsCountToZero()
    {
        var stack = new LockFreeStack<Item>(4);
        stack.TryPush(new Item { Value = 1 });
        stack.TryPush(new Item { Value = 2 });

        stack.Clear();

        await Assert.That(stack.Count).IsEqualTo(0);
        await Assert.That(stack.TryPop(out _)).IsFalse();
    }

    [Test]
    public async Task TryPop_ReturnsItems_InLifoOrder()
    {
        var stack = new LockFreeStack<Item>(4);
        var item1 = new Item { Value = 1 };
        var item2 = new Item { Value = 2 };
        var item3 = new Item { Value = 3 };

        stack.TryPush(item1);
        stack.TryPush(item2);
        stack.TryPush(item3);

        stack.TryPop(out var result3);
        stack.TryPop(out var result2);
        stack.TryPop(out var result1);

        await Assert.That(result3).IsSameReferenceAs(item3);
        await Assert.That(result2).IsSameReferenceAs(item2);
        await Assert.That(result1).IsSameReferenceAs(item1);
    }

    [Test]
    public async Task SmallCapacity_UsesSingleStripe()
    {
        var stack = new LockFreeStack<Item>(16);

        await Assert.That(GetStripeCount(stack)).IsEqualTo(1);
    }

    [Test]
    public async Task LargeCapacity_UsesMultipleStripes()
    {
        var stack = new LockFreeStack<Item>(256);
        var stripeCount = GetStripeCount(stack);

        if (Environment.ProcessorCount > 1)
            await Assert.That(stripeCount).IsGreaterThan(1);
        else
            await Assert.That(stripeCount).IsEqualTo(1);
    }

    [Test]
    public async Task LargeCapacity_CanUseFullCapacityAcrossStripes()
    {
        const int capacity = 256;
        var stack = new LockFreeStack<Item>(capacity);

        for (var i = 0; i < capacity; i++)
            await Assert.That(stack.TryPush(new Item { Value = i })).IsTrue();

        await Assert.That(stack.TryPush(new Item { Value = capacity })).IsFalse();
        await Assert.That(stack.Count).IsEqualTo(capacity);

        var seen = new HashSet<int>();
        while (stack.TryPop(out var item))
            seen.Add(item.Value);

        await Assert.That(seen.Count).IsEqualTo(capacity);
        await Assert.That(stack.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_ThrowsOnInvalidCapacity()
    {
        await Assert.That(() => new LockFreeStack<Item>(0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new LockFreeStack<Item>(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ConcurrentPushPop_NoDuplicatesOrLoss()
    {
        const int capacity = 64;
        const int operationsPerThread = 10_000;
        const int threadCount = 4;
        var stack = new LockFreeStack<Item>(capacity);
        var pushCount = 0;
        var popCount = 0;
        var duplicateCount = 0;
        var poppedValues = new ConcurrentDictionary<int, byte>();

        var tasks = new Task[threadCount * 2];

        // Producer threads
        for (var i = 0; i < threadCount; i++)
        {
            var threadId = i;
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < operationsPerThread; j++)
                {
                    if (stack.TryPush(new Item { Value = threadId * operationsPerThread + j }))
                        Interlocked.Increment(ref pushCount);
                }
            });
        }

        // Consumer threads
        for (var i = 0; i < threadCount; i++)
        {
            tasks[threadCount + i] = Task.Run(() =>
            {
                for (var j = 0; j < operationsPerThread; j++)
                {
                    if (stack.TryPop(out var item))
                    {
                        Interlocked.Increment(ref popCount);
                        if (!poppedValues.TryAdd(item.Value, 0))
                            Interlocked.Increment(ref duplicateCount);
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        // Drain remaining
        while (stack.TryPop(out var item))
        {
            Interlocked.Increment(ref popCount);
            if (!poppedValues.TryAdd(item.Value, 0))
                Interlocked.Increment(ref duplicateCount);
        }

        // Every accepted item must be returned exactly once after producers quiesce.
        await Assert.That(popCount).IsGreaterThan(0);
        await Assert.That(popCount).IsEqualTo(pushCount);
        await Assert.That(poppedValues.Count).IsEqualTo(pushCount);
        await Assert.That(duplicateCount).IsEqualTo(0);
        await Assert.That(stack.Count).IsEqualTo(0);
    }

    private static int GetStripeCount(LockFreeStack<Item> stack)
    {
        var field = typeof(LockFreeStack<Item>).GetField("_stripes", BindingFlags.Instance | BindingFlags.NonPublic);
        var stripes = (Array)field!.GetValue(stack)!;
        return stripes.Length;
    }
}
