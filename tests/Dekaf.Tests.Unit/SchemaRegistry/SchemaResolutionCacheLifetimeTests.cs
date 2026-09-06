using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public class SchemaResolutionCacheLifetimeTests
{
    [Test]
    [Arguments(1)]
    [Arguments(3)]
    public async Task RepeatedInvalidation_ReleasesValuesAndBookkeeping(int keyCount)
    {
        var cache = new SchemaResolutionCache<object>(4);
        var references = PopulateAndInvalidate(cache, keyCount);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Assert.That(cache.CachedEntryCount).IsEqualTo(0);
        await Assert.That(BookkeepingCount(cache)).IsEqualTo(0);
        await Assert.That(PooledEntryCount(cache)).IsLessThanOrEqualTo(4);
        foreach (var reference in references)
            await Assert.That(reference.IsAlive).IsFalse();
        GC.KeepAlive(cache);
    }

    [Test]
    public async Task StaleRemoval_DoesNotRemoveReplacementOrEvictStableEntry()
    {
        var cache = new SchemaResolutionCache<object>(2);
        var schema = new Schema { SchemaString = "{}", SchemaType = SchemaType.Json };
        var stable = await Resolve(cache, "stable", schema);
        var previous = await Resolve(cache, "refresh", schema);
        for (var index = 0; index < 1_000; index++)
        {
            await Assert.That(cache.TryRemove("refresh", schema, previous)).IsTrue();
            var replacement = await Resolve(cache, "refresh", schema);
            await Assert.That(cache.TryRemove("refresh", schema, previous)).IsFalse();
            await Assert.That(cache.TryGet("refresh", schema, out var current)).IsTrue();
            await Assert.That(ReferenceEquals(current, replacement)).IsTrue();
            previous = replacement;
        }

        await Assert.That(cache.TryGet("stable", schema, out var retained)).IsTrue();
        await Assert.That(ReferenceEquals(stable, retained)).IsTrue();
        await Assert.That(cache.CachedEntryCount).IsEqualTo(2);
        await Assert.That(BookkeepingCount(cache)).IsEqualTo(2);
    }

    [Test]
    [Arguments(8)]
    [Arguments(33)]
    public async Task ConcurrentInvalidationAndOverflow_KeepBookkeepingBounded(int capacity)
    {
        var cache = new SchemaResolutionCache<object>(capacity);
        var schema = new Schema { SchemaString = "{}", SchemaType = SchemaType.Json };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workers = new Task[16];
        for (var index = 0; index < workers.Length; index++)
        {
            var subject = $"subject-{index}";
            workers[index] = Task.Run(async () =>
            {
                await gate.Task;
                for (var cycle = 0; cycle < 100; cycle++)
                {
                    var value = await Resolve(cache, subject, schema);
                    cache.TryRemove(subject, schema, value);
                }
            });
        }

        gate.SetResult();
        await Task.WhenAll(workers);
        await Assert.That(cache.CachedEntryCount).IsEqualTo(0);
        await Assert.That(BookkeepingCount(cache)).IsEqualTo(0);
        for (var index = 0; index < capacity * 2; index++)
            await Resolve(cache, $"overflow-{index}", schema);
        await Assert.That(cache.CachedEntryCount).IsEqualTo(capacity);
        await Assert.That(BookkeepingCount(cache)).IsEqualTo(capacity);
        await Assert.That(PooledEntryCount(cache)).IsEqualTo(0);
        for (var index = 0; index < capacity * 2; index++)
            await Assert.That(cache.TryGet($"overflow-{index}", schema, out _)).IsEqualTo(index >= capacity);
    }

    [Test]
    public async Task RemoveMiddleEntry_ReusePreservesInsertionOrder()
    {
        var cache = new SchemaResolutionCache<object>(3);
        var schema = new Schema { SchemaString = "{}", SchemaType = SchemaType.Json };
        await Resolve(cache, "first", schema);
        var middle = await Resolve(cache, "middle", schema);
        await Resolve(cache, "last", schema);
        await Assert.That(cache.TryRemove("middle", schema, middle)).IsTrue();
        await Resolve(cache, "replacement", schema);
        await Resolve(cache, "overflow", schema);

        await Assert.That(cache.TryGet("first", schema, out _)).IsFalse();
        await Assert.That(cache.TryGet("middle", schema, out _)).IsFalse();
        await Assert.That(cache.TryGet("last", schema, out _)).IsTrue();
        await Assert.That(cache.TryGet("replacement", schema, out _)).IsTrue();
        await Assert.That(cache.TryGet("overflow", schema, out _)).IsTrue();
        await Assert.That(BookkeepingCount(cache)).IsEqualTo(3);
    }

    internal static int BookkeepingCount(object cache)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var queue = cache.GetType().GetField("_evictionQueue", flags);
        if (queue is not null)
            return ((ICollection)queue.GetValue(cache)!).Count;

        var nodes = (Array)cache.GetType().GetField("_evictionNodes", flags)!.GetValue(cache)!;
        var index = (int)cache.GetType().GetField("_oldestEntry", flags)!.GetValue(cache)!;
        var count = 0;
        while (index >= 0)
        {
            if (++count > nodes.Length)
                throw new InvalidOperationException("Cycle in eviction bookkeeping.");
            var node = nodes.GetValue(index)!;
            index = (int)node.GetType().GetField("Next", flags)!.GetValue(node)!;
        }
        return count;
    }

    private static int PooledEntryCount(object cache)
    {
        var field = cache.GetType().GetField("_allocatedEntryCount", BindingFlags.Instance | BindingFlags.NonPublic);
        return field is null ? 0 : (int)field.GetValue(cache)! - BookkeepingCount(cache);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] PopulateAndInvalidate(SchemaResolutionCache<object> cache, int keyCount)
    {
        var references = new WeakReference[2_000];
        for (var index = 0; index < references.Length / 2; index++)
        {
            var schema = new Schema { SchemaString = "{}", SchemaType = SchemaType.Json };
            var subject = $"subject-{index % keyCount}";
            var value = Resolve(cache, subject, schema).Result;
            references[index * 2] = new WeakReference(value);
            references[index * 2 + 1] = new WeakReference(schema);
            if (!cache.TryRemove(subject, schema, value))
                throw new InvalidOperationException("Expected the resolved entry to be removed.");
        }
        return references;
    }

    private static ValueTask<object> Resolve(SchemaResolutionCache<object> cache, string subject, Schema schema) =>
        cache.ResolveAsync(subject, schema, 0, static (_, _, _) => Task.FromResult(new object()), CancellationToken.None);
}
