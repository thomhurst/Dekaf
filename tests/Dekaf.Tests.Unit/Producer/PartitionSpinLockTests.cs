#if NET10_0_OR_GREATER
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public sealed class PartitionSpinLockTests
{
    [Test]
    public async Task Enter_IsMutuallyExclusiveAcrossThreads()
    {
        var holder = new LockHolder();
        var counter = 0;
        var maxInside = 0;
        var inside = 0;
        const int iterationsPerThread = 200_000;

        var threads = new Thread[4];
        for (var t = 0; t < threads.Length; t++)
        {
            threads[t] = new Thread(() =>
            {
                for (var i = 0; i < iterationsPerThread; i++)
                {
                    var taken = false;
                    holder.Lock.Enter(ref taken);
                    try
                    {
                        var now = Interlocked.Increment(ref inside);
                        if (now > Volatile.Read(ref maxInside))
                            Volatile.Write(ref maxInside, now);
                        counter++;
                        Interlocked.Decrement(ref inside);
                    }
                    finally
                    {
                        if (taken) holder.Lock.Exit();
                    }
                }
            });
            threads[t].Start();
        }

        foreach (var thread in threads)
            thread.Join();

        await Assert.That(maxInside).IsEqualTo(1);
        await Assert.That(counter).IsEqualTo(iterationsPerThread * threads.Length);
    }

    [Test]
    public async Task Exit_ReleasesForTheNextWaiter()
    {
        var holder = new LockHolder();
        var taken = false;
        holder.Lock.Enter(ref taken);
        await Assert.That(taken).IsTrue();

        var waiterEntered = new ManualResetEventSlim();
        var waiter = new Thread(() =>
        {
            var waiterTaken = false;
            holder.Lock.Enter(ref waiterTaken);
            waiterEntered.Set();
            holder.Lock.Exit();
        });
        waiter.Start();

        await Assert.That(waiterEntered.Wait(200)).IsFalse();
        holder.Lock.Exit();
        await Assert.That(waiterEntered.Wait(5_000)).IsTrue();
        waiter.Join();
    }

    private sealed class LockHolder
    {
        public PartitionSpinLock Lock;
    }
}
#endif
