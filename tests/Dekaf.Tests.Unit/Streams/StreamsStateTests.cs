using System.Buffers.Binary;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Streams;
using Dekaf.Streams.Internal;
using Dekaf.Streams.Internal.Runtime;
using Dekaf.Streams.Storage.ZoneTree;
using NSubstitute;

namespace Dekaf.Tests.Unit.Streams;

public sealed class StreamsStateTests
{
    [Test]
    public async Task StateDirectory_HandsOutACheckpointOnceAndDiscardsStateWithoutOne()
    {
        using var root = new TemporaryDirectory();
        var directory = new StateDirectory(root.Path, "app");
        var id = new TaskId(1, 3);

        var first = await directory.OpenAsync(id, CancellationToken.None);
        await Assert.That(first.Path).IsEqualTo(Path.Combine(root.Path, "app", "1_3"));
        await Assert.That(first.CheckpointOf("changelog")).IsEqualTo(-1);
        Directory.CreateDirectory(Path.Combine(first.Path, "store"));
        File.WriteAllText(Path.Combine(first.Path, "store", "data"), "state");
        first.Close(new Dictionary<string, long> { ["changelog"] = 42, ["topic with spaces"] = 7 });

        // A clean close left a checkpoint: the state is kept and the offsets come back.
        var second = await directory.OpenAsync(id, CancellationToken.None);
        await Assert.That(second.CheckpointOf("changelog")).IsEqualTo(42);
        await Assert.That(second.CheckpointOf("topic with spaces")).IsEqualTo(7);
        await Assert.That(File.Exists(Path.Combine(second.Path, "store", "data"))).IsTrue();

        // The checkpoint was taken on open, so a crash from here on leaves none behind.
        await Assert.That(File.Exists(Path.Combine(second.Path, TaskDirectory.CheckpointFile))).IsFalse();
        second.Close(offsets: null);

        var third = await directory.OpenAsync(id, CancellationToken.None);
        await Assert.That(third.CheckpointOf("changelog")).IsEqualTo(-1);
        await Assert.That(Directory.Exists(Path.Combine(third.Path, "store"))).IsFalse();
        third.Close(offsets: null);
    }

    [Test]
    public async Task StateDirectory_TreatsAnUnreadableCheckpointAsMissingAndLocksTheTask()
    {
        using var root = new TemporaryDirectory();
        var directory = new StateDirectory(root.Path, "app");
        var id = new TaskId(0, 0);
        var path = Path.Combine(root.Path, "app", "0_0");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, TaskDirectory.CheckpointFile), "not-a-number changelog\n");
        File.WriteAllText(Path.Combine(path, "leftover"), "state");

        var opened = await directory.OpenAsync(id, CancellationToken.None);
        await Assert.That(opened.CheckpointOf("changelog")).IsEqualTo(-1);
        await Assert.That(File.Exists(Path.Combine(path, "leftover"))).IsFalse();

        // A second owner waits for the lock instead of sharing the directory.
        using var waiting = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.That(async () => await directory.OpenAsync(id, waiting.Token)).Throws<OperationCanceledException>();

        opened.Close(offsets: null);
        var next = await directory.OpenAsync(id, CancellationToken.None);
        next.Close(offsets: null);
    }

    [Test]
    public async Task ZoneTreeKeyValueStore_KeepsItsDataAcrossAReopen()
    {
        using var root = new TemporaryDirectory();
        var context = new StateStoreContext("app", "store", "0", 0, Path.Combine(root.Path, "store"));
        byte[] key = [1, 2], other = [9];

        using (var store = ZoneTreeStateStoreEngine.Instance.CreateKeyValueStore(context))
        {
            await Assert.That(store.TryGet(key, out _)).IsFalse();
            store.Put(key, [10, 20, 30]);
            store.Put(other, []);
            store.Put(key, [40]);
            await Assert.That(store.TryGet(key, out var value)).IsTrue();
            await Assert.That(value.ToArray()).IsEquivalentTo(new byte[] { 40 });

            // An empty value is a value, not a delete.
            await Assert.That(store.TryGet(other, out var empty)).IsTrue();
            await Assert.That(empty.Length).IsEqualTo(0);
            await Assert.That(store.Count).IsEqualTo(2);
            await Assert.That(store.Delete(other)).IsTrue();
            await Assert.That(store.Delete(other)).IsFalse();
        }

        using var reopened = ZoneTreeStateStoreEngine.Instance.CreateKeyValueStore(context);
        await Assert.That(reopened.TryGet(key, out var kept)).IsTrue();
        await Assert.That(kept.ToArray()).IsEquivalentTo(new byte[] { 40 });
        await Assert.That(reopened.TryGet(other, out _)).IsFalse();
        await Assert.That(reopened.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ZoneTreeWindowStore_RemovesClosedWindowsInWindowOrder()
    {
        using var root = new TemporaryDirectory();
        var context = new StateStoreContext("app", "windows", "0", 0, Path.Combine(root.Path, "windows"));
        byte[] a = [1], b = [2];
        var visitor = new RecordingVisitor();

        using (var store = ZoneTreeStateStoreEngine.Instance.CreateWindowStore(context))
        {
            store.Put(b, 2_000, [22]);
            store.Put(a, 1_000, [11]);
            store.Put(a, 3_000, [13]);
            store.Put(a, 1_000, [12]);
            await Assert.That(store.TryGet(a, 1_000, out var value)).IsTrue();
            await Assert.That(value.ToArray()).IsEquivalentTo(new byte[] { 12 });
            await Assert.That(store.TryGet(b, 1_000, out _)).IsFalse();

            store.RemoveThrough(500, visitor);
            await Assert.That(visitor.Visited).IsEmpty();

            store.RemoveThrough(2_000, visitor);
            await Assert.That(visitor.Visited).IsEquivalentTo([(1, 1_000L, 12), (2, 2_000L, 22)]);
            await Assert.That(store.TryGet(a, 1_000, out _)).IsFalse();
            await Assert.That(store.Delete(b, 2_000)).IsFalse();
            await Assert.That(store.Count).IsEqualTo(1);
        }

        // A reopened store finds its earliest window again, so the next close still sees it.
        using var reopened = ZoneTreeStateStoreEngine.Instance.CreateWindowStore(context);
        visitor.Visited.Clear();
        reopened.RemoveThrough(3_000, visitor);
        await Assert.That(visitor.Visited).IsEquivalentTo([(1, 3_000L, 13)]);
        await Assert.That(reopened.Count).IsEqualTo(0);
    }

    [Test]
    public async Task StreamTask_WithZoneTreeState_ResumesFromItsDirectoryWithoutAChangelogReplay()
    {
        using var root = new TemporaryDirectory();
        var builder = new StreamsBuilder("app");
        var numbers = builder.Stream("numbers", Serializers.Int64, Serializers.Int64);
        numbers.GroupByKey().Count("counts");
        numbers.GroupByKey()
            .WindowedBy(TimeWindows.Tumbling(TimeSpan.FromSeconds(10)))
            .Count("windowed")
            .ToStream()
            .ToTopic("windowed-out", WindowedSerde.Create(Serializers.Int64, TimeSpan.FromSeconds(10)), Serializers.Int64);
        var topology = builder.Build();

        using (var task = new StreamTask(topology, 0, 0, new RecordingCollector(), ZoneTreeStateStoreEngine.Instance, root.Path))
        {
            for (var i = 0; i < 5; i++)
            {
                task.Process("numbers", Raw(key: 7, value: i, timestamp: 1_000 + i));
            }
        }

        var output = new RecordingCollector();
        using var resumed = new StreamTask(topology, 0, 0, output, ZoneTreeStateStoreEngine.Instance, root.Path);
        var counts = (IReadOnlyKeyValueStore<long, long>)resumed.GetStore("counts")!;
        await Assert.That(counts.TryGet(7, out var count)).IsTrue();
        await Assert.That(count).IsEqualTo(5);

        resumed.Process("numbers", Raw(key: 7, value: 0, timestamp: 2_000));
        var update = output.Sent.Single(static record => record.Topic == "windowed-out");
        await Assert.That(BinaryPrimitives.ReadInt64BigEndian(update.Value)).IsEqualTo(6);
    }

    [Test]
    public async Task OutputArena_KeepsCopiesStableUntilResetAndReusesItsChunks()
    {
        using var arena = new OutputArena();
        var scratch = new byte[] { 1, 2, 3 };
        var first = arena.Copy(scratch);
        scratch[0] = 99;
        var second = arena.Copy(scratch);
        var large = arena.Copy(new byte[200_000]);

        await Assert.That(first.ToArray()).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(second.ToArray()).IsEquivalentTo(new byte[] { 99, 2, 3 });
        await Assert.That(large.Length).IsEqualTo(200_000);
        await Assert.That(arena.Copy([]).IsEmpty).IsTrue();

        // More than one chunk, then a reset: the next copy starts at the first chunk again.
        for (var i = 0; i < 40; i++)
        {
            arena.Copy(new byte[4_096]);
        }

        arena.Reset();
        var afterReset = arena.Copy(new byte[] { 5 });
        await Assert.That(System.Runtime.InteropServices.MemoryMarshal.TryGetArray(afterReset, out var segment)).IsTrue();
        await Assert.That(System.Runtime.InteropServices.MemoryMarshal.TryGetArray(first, out var original)).IsTrue();
        await Assert.That(segment.Array).IsSameReferenceAs(original.Array);
        await Assert.That(segment.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task ProducerCollector_KeepsSendFailuresForTheCommitInsteadOfThrowingIntoTheTask()
    {
        var producer = Substitute.For<IKafkaProducer<ReadOnlyMemory<byte>?, ReadOnlyMemory<byte>?>>();
        var sent = new List<ProducerMessage<ReadOnlyMemory<byte>?, ReadOnlyMemory<byte>?>>();
        Action<RecordMetadata, Exception?>? onDelivery = null;
        producer.FireAsync(
                Arg.Do<ProducerMessage<ReadOnlyMemory<byte>?, ReadOnlyMemory<byte>?>>(sent.Add),
                Arg.Do<Action<RecordMetadata, Exception?>>(handler => onDelivery = handler))
            .Returns(ValueTask.CompletedTask);
        using var collector = new ProducerCollector(producer);

        collector.Send("out", partition: -1, [1], false, default, true, timestamp: 5, headers: null);
        collector.Send("changelog", partition: 3, default, true, [], false, timestamp: 6, headers: null);
        await Assert.That(collector.Failure).IsNull();
        await Assert.That(sent[0].Key!.Value.ToArray()).IsEquivalentTo(new byte[] { 1 });
        await Assert.That(sent[0].Value).IsNull();
        await Assert.That(sent[0].Partition).IsNull();
        await Assert.That(sent[0].Timestamp).IsEqualTo(DateTimeOffset.FromUnixTimeMilliseconds(5));
        await Assert.That(sent[1].Key).IsNull();
        await Assert.That(sent[1].Value!.Value.IsEmpty).IsTrue();
        await Assert.That(sent[1].Partition).IsEqualTo(3);

        // A failed delivery is reported on a producer thread; the collector keeps the first one.
        var deliveryFailure = new InvalidOperationException("delivery");
        onDelivery!(default, deliveryFailure);
        onDelivery(default, new InvalidOperationException("later"));
        await Assert.That(collector.Failure).IsSameReferenceAs(deliveryFailure);

        // After a failure nothing more is sent: the commit will abort anyway.
        collector.Send("out", -1, [2], false, [2], false, 7, null);
        await Assert.That(sent.Count).IsEqualTo(2);

        collector.Reset();
        await Assert.That(collector.Failure).IsNull();

        var sendFailure = new InvalidOperationException("send");
        producer.FireAsync(
                Arg.Any<ProducerMessage<ReadOnlyMemory<byte>?, ReadOnlyMemory<byte>?>>(),
                Arg.Any<Action<RecordMetadata, Exception?>>())
            .Returns(_ => throw sendFailure);
        collector.Send("out", -1, [3], false, [3], false, 8, null);
        await Assert.That(collector.Failure).IsSameReferenceAs(sendFailure);
    }

    [Test]
    public async Task NullableBytesSerde_TellsNullFromEmpty()
    {
        var serde = NullableBytesSerde.Instance;
        var context = new SerializationContext { Topic = "t", Component = SerializationComponent.Value };

        await Assert.That(serde.Deserialize(default, context with { IsNull = true })).IsNull();
        var empty = serde.Deserialize(default, context);
        await Assert.That(empty.HasValue).IsTrue();
        await Assert.That(empty!.Value.IsEmpty).IsTrue();

        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        serde.Serialize(new byte[] { 4, 5 }, ref buffer, context);
        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { 4, 5 });
    }

    private static RawRecord Raw(long key, long value, long timestamp)
    {
        var keyBytes = new byte[sizeof(long)];
        var valueBytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(keyBytes, key);
        BinaryPrimitives.WriteInt64BigEndian(valueBytes, value);
        return new RawRecord(keyBytes, false, valueBytes, false, timestamp, offset: 0, headers: null);
    }

    private sealed class RecordingVisitor : IWindowEntryVisitor
    {
        public List<(int Key, long Start, int Value)> Visited { get; } = [];

        public void Visit(ReadOnlyMemory<byte> key, long windowStart, ReadOnlyMemory<byte> value) =>
            Visited.Add((key.Span[0], windowStart, value.Span[0]));
    }

    private sealed class RecordingCollector : IRecordCollector
    {
        public List<(string Topic, byte[] Value)> Sent { get; } = [];

        public void Send(
            string topic,
            int partition,
            ReadOnlySpan<byte> key,
            bool keyIsNull,
            ReadOnlySpan<byte> value,
            bool valueIsNull,
            long timestamp,
            Headers? headers) =>
            Sent.Add((topic, value.ToArray()));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dekaf-streams-{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
