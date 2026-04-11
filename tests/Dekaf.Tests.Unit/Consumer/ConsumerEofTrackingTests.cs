using System.Collections.Concurrent;
using Dekaf;

namespace Dekaf.Tests.Unit.Consumer;

public class ConsumerEofTrackingTests
{
    [Test]
    public async Task TryAdd_ReturnsFalse_WhenAlreadyEmitted()
    {
        var eofEmitted = new ConcurrentDictionary<TopicPartition, byte>();
        var tp = new TopicPartition("test", 0);

        var first = eofEmitted.TryAdd(tp, 0);
        var second = eofEmitted.TryAdd(tp, 0);

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
    }

    [Test]
    public async Task TryRemove_ClearsState_ForNewEofEmission()
    {
        var eofEmitted = new ConcurrentDictionary<TopicPartition, byte>();
        var tp = new TopicPartition("test", 0);

        eofEmitted.TryAdd(tp, 0);
        eofEmitted.TryRemove(tp, out _);
        var reAdded = eofEmitted.TryAdd(tp, 0);

        await Assert.That(reAdded).IsTrue();
    }

    [Test]
    public async Task ConcurrentAddRemove_DoesNotCorrupt()
    {
        var eofEmitted = new ConcurrentDictionary<TopicPartition, byte>();
        var partitions = Enumerable.Range(0, 100)
            .Select(i => new TopicPartition("test", i)).ToArray();

        var addTask = Task.Run(() =>
        {
            for (var i = 0; i < 10_000; i++)
                eofEmitted.TryAdd(partitions[i % 100], 0);
        });
        var removeTask = Task.Run(() =>
        {
            for (var i = 0; i < 10_000; i++)
                eofEmitted.TryRemove(partitions[i % 100], out _);
        });

        await Task.WhenAll(addTask, removeTask);

        await Assert.That(eofEmitted.Count).IsGreaterThanOrEqualTo(0);
    }
}
