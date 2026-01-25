using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class TaskCompletionSourcePoolTests
{
    [Test]
    public async Task Rent_ReturnsNewInstance()
    {
        var pool = new TaskCompletionSourcePool<int>();

        var tcs = pool.Rent();

        await Assert.That(tcs).IsNotNull();
        await Assert.That(tcs.Task.IsCompleted).IsFalse();
    }

    [Test]
    public async Task Return_IsNoOp_TCSCannotBeReused()
    {
        var pool = new TaskCompletionSourcePool<int>();

        // Rent and complete a TCS
        var tcs1 = pool.Rent();
        tcs1.SetResult(42);
        await Assert.That(tcs1.Task.Result).IsEqualTo(42);

        // Return is a no-op (TCS cannot be reset/reused)
        pool.Return(tcs1);

        // Rent another TCS - always gets a fresh instance
        var tcs2 = pool.Rent();
        await Assert.That(tcs2).IsNotNull();
        await Assert.That(tcs2.Task.IsCompleted).IsFalse();
        // Verify it's a different instance
        await Assert.That(tcs2).IsNotSameReferenceAs(tcs1);
    }

    [Test]
    public async Task MultipleRentReturn_WorksCorrectly()
    {
        var pool = new TaskCompletionSourcePool<string>();
        var results = new List<string>();

        // Simulate multiple operations
        for (int i = 0; i < 10; i++)
        {
            var tcs = pool.Rent();
            var value = $"test-{i}";
            tcs.SetResult(value);
            results.Add(await tcs.Task.ConfigureAwait(false));
            pool.Return(tcs);
        }

        await Assert.That(results.Count).IsEqualTo(10);
        for (int i = 0; i < 10; i++)
        {
            await Assert.That(results[i]).IsEqualTo($"test-{i}");
        }
    }

    [Test]
    public async Task Rent_WithCustomCreationOptions_CreatesCorrectly()
    {
        var pool = new TaskCompletionSourcePool<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var tcs = pool.Rent();
        var taskOptions = tcs.Task.CreationOptions;

        await Assert.That(taskOptions.HasFlag(TaskCreationOptions.RunContinuationsAsynchronously)).IsTrue();
    }

    [Test]
    public async Task ConcurrentRentReturn_IsThreadSafe()
    {
        var pool = new TaskCompletionSourcePool<int>();
        var tasks = new List<Task>();
        var completedCount = 0;

        // Simulate concurrent rent/return operations
        for (int i = 0; i < 100; i++)
        {
            var task = Task.Run(async () =>
            {
                var tcs = pool.Rent();
                tcs.SetResult(i);
                await tcs.Task.ConfigureAwait(false);
                pool.Return(tcs);
                Interlocked.Increment(ref completedCount);
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(completedCount).IsEqualTo(100);
    }

    [Test]
    public async Task ReturnCallback_IsCached()
    {
        var pool = new TaskCompletionSourcePool<int>();

        // Getting ReturnCallback multiple times should return the same instance
        var callback1 = pool.ReturnCallback;
        var callback2 = pool.ReturnCallback;

        await Assert.That(callback1).IsSameReferenceAs(callback2);
    }
}
