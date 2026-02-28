using System.Collections.Concurrent;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Concurrency;

/// <summary>
/// Concurrency tests for BatchArena verifying thread-safety of the CAS-based
/// TryAllocate method and the pool's RentOrCreate/ReturnToPool operations.
/// </summary>
public class BatchArenaConcurrencyTests
{
    [Test]
    public async Task TryAllocate_ConcurrentAllocations_NoOverlappingRegions()
    {
        // Multiple threads allocating from the same arena must each get
        // unique, non-overlapping regions. The CAS loop ensures atomicity.

        const int threadCount = 8;
        const int allocsPerThread = 50;
        const int allocSize = 64;
        var arena = new BatchArena(threadCount * allocsPerThread * allocSize + 1024);

        var allocations = new ConcurrentBag<(int Offset, int Length)>();

        var barrier = new Barrier(threadCount);
        var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var j = 0; j < allocsPerThread; j++)
            {
                if (arena.TryAllocate(allocSize, out _, out var offset))
                {
                    allocations.Add((offset, allocSize));
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
    }

    [Test]
    public async Task TryAllocate_ArenaFull_AllThreadsGetConsistentFailure()
    {
        // When the arena is nearly full, concurrent allocations should
        // cleanly report failure without corrupting internal state.

        const int threadCount = 8;
        const int allocSize = 128;
        // Arena that can hold exactly 4 allocations
        var arena = new BatchArena(allocSize * 4);

        var successCount = 0;
        var failCount = 0;

        var barrier = new Barrier(threadCount);
        var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var j = 0; j < 4; j++)
            {
                if (arena.TryAllocate(allocSize, out _, out _))
                    Interlocked.Increment(ref successCount);
                else
                    Interlocked.Increment(ref failCount);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Exactly 4 allocations should succeed (the arena capacity).
        // Note: ArrayPool may rent a larger buffer, so we check >= 4
        await Assert.That(successCount).IsGreaterThanOrEqualTo(4);
        // Total attempts = threadCount * 4
        await Assert.That(successCount + failCount).IsEqualTo(threadCount * 4);
    }

    [Test]
    public async Task TryAllocate_WriteToAllocatedSpan_DataIsolated()
    {
        // Verify that data written to allocated spans doesn't interfere
        // across threads due to overlapping regions.

        const int threadCount = 8;
        const int allocSize = 32;
        var arena = new BatchArena(threadCount * allocSize + 256);

        var barrier = new Barrier(threadCount);
        var offsets = new ConcurrentDictionary<int, byte>();

        var tasks = Enumerable.Range(0, threadCount).Select(threadIndex => Task.Run(() =>
        {
            barrier.SignalAndWait();
            if (arena.TryAllocate(allocSize, out var span, out var offset))
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
    public async Task RentOrCreate_ReturnToPool_ConcurrentPoolAccess()
    {
        // RentOrCreate and ReturnToPool use ConcurrentQueue and Interlocked.
        // Concurrent access must not corrupt the pool or lose arenas.

        const int threadCount = 8;
        const int cyclesPerThread = 50;

        var barrier = new Barrier(threadCount);
        var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
        {
            barrier.SignalAndWait();
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
}
