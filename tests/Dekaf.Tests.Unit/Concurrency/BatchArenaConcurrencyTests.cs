using System.Collections.Concurrent;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Concurrency;

/// <summary>
/// Tests for BatchArena's single-writer allocation contract and the pool's concurrent
/// RentOrCreate/ReturnToPool operations. Production only calls <see cref="BatchArena.TryAllocate"/>
/// and <see cref="BatchArena.TryRewindLastAllocation"/> while the owning partition lock is held,
/// so the contended tests serialize allocations under a lock exactly like the append path does.
/// </summary>
[Repeat(25)]
public class BatchArenaConcurrencyTests
{
    [Test]
    public async Task TryAllocate_SerializedAllocationsFromManyThreads_NoOverlappingRegions()
    {
        // Threads take turns under a lock (the partition-lock contract) and must each get
        // unique, non-overlapping regions with the position published to lock-free readers.

        const int threadCount = 8;
        const int allocsPerThread = 50;
        const int allocSize = 64;
        var arena = new BatchArena(threadCount * allocsPerThread * allocSize + 1024);
        var gate = new object();

        var allocations = new ConcurrentBag<(int Offset, int Length)>();

        var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
        {
            for (var j = 0; j < allocsPerThread; j++)
            {
                lock (gate)
                {
                    if (arena.TryAllocate(allocSize, out _, out var offset))
                    {
                        allocations.Add((offset, allocSize));
                    }
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Check that no two allocations overlap
        var sorted = allocations.OrderBy(a => a.Offset).ToArray();
        for (var i = 1; i < sorted.Length; i++)
        {
            var prevEnd = sorted[i - 1].Offset + sorted[i - 1].Length;
            await Assert.That(sorted[i].Offset).IsGreaterThanOrEqualTo(prevEnd);
        }

        // All allocations should have succeeded (arena is large enough)
        await Assert.That(allocations.Count).IsEqualTo(threadCount * allocsPerThread);
        await Assert.That(arena.Position).IsEqualTo(threadCount * allocsPerThread * allocSize);
    }

    [Test]
    public async Task TryAllocate_ArenaFull_ReportsFailureWithoutCorruptingPosition()
    {
        // Once the arena is full every further allocation fails cleanly and the position
        // stays at capacity, so the caller can rotate the batch.

        const int threadCount = 8;
        const int allocSize = 128;
        // Arena that can hold exactly 4 allocations
        var arena = new BatchArena(allocSize * 4);
        var gate = new object();

        var successCount = 0;
        var failCount = 0;

        var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
        {
            for (var j = 0; j < 4; j++)
            {
                bool allocated;
                lock (gate)
                {
                    allocated = arena.TryAllocate(allocSize, out _, out _);
                }

                if (allocated)
                    Interlocked.Increment(ref successCount);
                else
                    Interlocked.Increment(ref failCount);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        await Assert.That(successCount).IsEqualTo(4);
        await Assert.That(successCount + failCount).IsEqualTo(threadCount * 4);
        await Assert.That(arena.RemainingCapacity).IsEqualTo(0);
    }

    [Test]
    public async Task TryAllocate_WriteToAllocatedSpan_DataIsolated()
    {
        // Data written to a region allocated under the lock must not be disturbed by
        // regions other threads allocate afterwards.

        const int threadCount = 8;
        const int allocSize = 32;
        var arena = new BatchArena(threadCount * allocSize + 256);
        var gate = new object();

        var offsets = new ConcurrentDictionary<int, byte>();

        var tasks = Enumerable.Range(0, threadCount).Select(threadIndex => Task.Run(() =>
        {
            Span<byte> span;
            int offset;
            bool allocated;
            lock (gate)
            {
                allocated = arena.TryAllocate(allocSize, out span, out offset);
            }

            if (allocated)
            {
                // Fill with a thread-specific byte pattern
                var pattern = (byte)(threadIndex + 1);
                span.Fill(pattern);
                offsets[offset] = pattern;
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Verify each allocated region contains its expected pattern
        foreach (var (offset, pattern) in offsets)
        {
            // Copy to array since ReadOnlySpan can't cross await boundaries
            var data = arena.GetSpan(offset, allocSize).ToArray();
            for (var i = 0; i < data.Length; i++)
            {
                await Assert.That((int)data[i]).IsEqualTo((int)pattern);
            }
        }
    }

    [Test]
    public async Task TryRewindLastAllocation_OnlyRewindsTheMostRecentAllocation()
    {
        var arena = new BatchArena(1024);

        await Assert.That(arena.TryAllocate(100, out _, out var first)).IsTrue();
        await Assert.That(arena.TryAllocate(50, out _, out var second)).IsTrue();
        await Assert.That(second).IsEqualTo(first + 100);
        await Assert.That(arena.Position).IsEqualTo(150);

        // Not the last allocation: refused, position untouched.
        await Assert.That(arena.TryRewindLastAllocation(first, 100)).IsFalse();
        await Assert.That(arena.Position).IsEqualTo(150);

        // The last allocation rewinds and its offset is handed out again.
        await Assert.That(arena.TryRewindLastAllocation(second, 50)).IsTrue();
        await Assert.That(arena.Position).IsEqualTo(100);
        await Assert.That(arena.TryAllocate(50, out _, out var reused)).IsTrue();
        await Assert.That(reused).IsEqualTo(second);
    }

    [Test]
    public async Task RentOrCreate_ReturnToPool_ConcurrentPoolAccess()
    {
        // RentOrCreate and ReturnToPool use ConcurrentQueue and Interlocked.
        // Concurrent access must not corrupt the pool or lose arenas.

        const int threadCount = 8;
        const int cyclesPerThread = 50;

        var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
        {
            for (var j = 0; j < cyclesPerThread; j++)
            {
                var arena = BatchArena.RentOrCreate(1024);
                // Use the arena briefly
                arena.TryAllocate(64, out _, out _);
                // Return to pool
                BatchArena.ReturnToPool(arena);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // If we get here without exceptions, the pool is thread-safe.
        // Verify we can still rent from the pool after concurrent operations.
        var finalArena = BatchArena.RentOrCreate(1024);
        var success = finalArena.TryAllocate(128, out _, out _);
        await Assert.That(success).IsTrue();
        BatchArena.ReturnToPool(finalArena);
    }

    [Test]
    public async Task ReturnToPool_OversizedArenaAbovePoolLimit_DropsBuffer()
    {
        var dropsBefore = BatchArena.Drops;
        var arena = new BatchArena(capacity: 4096, maxPooledCapacity: 1024);

        await Assert.That(arena.Capacity).IsEqualTo(4096);

        BatchArena.ReturnToPool(arena);

        await Assert.That(arena.Buffer).IsNull();
        await Assert.That(BatchArena.Drops).IsGreaterThanOrEqualTo(dropsBefore + 1);
    }
}
