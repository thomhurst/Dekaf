using System.Buffers.Binary;
using Dekaf.Serialization;
using Dekaf.Streams;
using Dekaf.Streams.Internal;

namespace Dekaf.Tests.Unit.Streams;

public sealed class StreamTaskTests
{
    private static readonly ISerde<long> Long = Serializers.Int64;

    [Test]
    public async Task Restore_RebuildsStoresFromTheirChangelogs()
    {
        var topology = BuildCountingTopology();
        var changelog = new RecordingCollector();
        using (var original = new StreamTask(topology, subtopology: 0, partition: 3, changelog))
        {
            for (var i = 0; i < 6; i++)
            {
                original.Process("numbers", Raw(key: i % 2, value: i, timestamp: i * 1_000));
            }
        }

        using var restored = new StreamTask(topology, subtopology: 0, partition: 3, new RecordingCollector());
        foreach (var record in changelog.Records)
        {
            if (record.Topic.EndsWith("-changelog", StringComparison.Ordinal))
            {
                // Changelog records are written to the task's own partition.
                await Assert.That(record.Partition).IsEqualTo(3);
                restored.Restore(record.Topic, record.Key, record.Value, record.Value is null);
            }
        }

        var counts = (IReadOnlyKeyValueStore<long, long>)restored.GetStore("counts")!;
        await Assert.That(counts.Count).IsEqualTo(2);
        await Assert.That(counts.TryGet(1, out var odd)).IsTrue();
        await Assert.That(odd).IsEqualTo(3);

        // The window [0, 2s) closed at stream time 2s and was removed by its tombstone, so the restored
        // store continues from the open windows only.
        var output = new RecordingCollector();
        using var continued = new StreamTask(topology, subtopology: 0, partition: 3, output);
        foreach (var record in changelog.Records.Where(static r => r.Topic == "app-windowed-changelog"))
        {
            continued.Restore(record.Topic, record.Key, record.Value, record.Value is null);
        }

        continued.Process("numbers", Raw(key: 1, value: 0, timestamp: 5_500));
        var update = output.Records.Single(static r => r.Topic == "windowed-out");
        await Assert.That(BinaryPrimitives.ReadInt64BigEndian(update.Value)).IsEqualTo(2);
    }

    [Test]
    public async Task Process_RejectsATopicTheSubtopologyDoesNotRead()
    {
        using var task = new StreamTask(BuildCountingTopology(), 0, 0, new RecordingCollector());
        await Assert.That(() => task.Process("elsewhere", Raw(1, 1, 0))).Throws<StreamsException>();
        await Assert.That(() => task.Restore("elsewhere", default, default, true)).Throws<StreamsException>();
    }

    [Test]
    public async Task SteadyStateProcessing_AllocatesNothing()
    {
        var builder = new StreamsBuilder("app");
        var numbers = builder.Stream("numbers", Long, Long);
        numbers
            .Where(static (_, value) => value >= 0)
            .Select(static (_, value) => value * 2)
            .ToTopic("doubled", Long, Long);
        numbers.GroupByKey().Count("counts").ToStream().ToTopic("counts-out", Long, Long);
        numbers.GroupByKey()
            .WindowedBy(TimeWindows.Tumbling(TimeSpan.FromHours(1)))
            .Aggregate("sums", static () => 0L, static (_, value, sum) => sum + value, Long);

        using var task = new StreamTask(builder.Build(), 0, 0, new DiscardingCollector());
        var records = new RawRecord[64];
        for (var i = 0; i < records.Length; i++)
        {
            records[i] = Raw(key: i % 8, value: i, timestamp: i);
        }

        // Warm-up creates the store entries and compiles the path.
        for (var round = 0; round < 50; round++)
        {
            foreach (ref readonly var record in records.AsSpan())
            {
                task.Process("numbers", in record);
            }
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 20; round++)
        {
            foreach (ref readonly var record in records.AsSpan())
            {
                task.Process("numbers", in record);
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(allocated).IsEqualTo(0);
    }

    private static Topology BuildCountingTopology()
    {
        var builder = new StreamsBuilder("app");
        var numbers = builder.Stream("numbers", Long, Long);
        numbers.GroupByKey().Count("counts");
        numbers.GroupByKey()
            .WindowedBy(TimeWindows.Tumbling(TimeSpan.FromSeconds(2)))
            .Count("windowed")
            .ToStream()
            .ToTopic("windowed-out", WindowedSerde.Create(Long, TimeSpan.FromSeconds(2)), Long);
        return builder.Build();
    }

    private static RawRecord Raw(long key, long value, long timestamp)
    {
        var keyBytes = new byte[sizeof(long)];
        var valueBytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(keyBytes, key);
        BinaryPrimitives.WriteInt64BigEndian(valueBytes, value);
        return new RawRecord(keyBytes, false, valueBytes, false, timestamp, offset: 0, headers: null);
    }

    private sealed record Sent(string Topic, int Partition, byte[] Key, byte[]? Value);

    private sealed class RecordingCollector : IRecordCollector
    {
        public List<Sent> Records { get; } = [];

        public void Send(
            string topic,
            int partition,
            ReadOnlySpan<byte> key,
            bool keyIsNull,
            ReadOnlySpan<byte> value,
            bool valueIsNull,
            long timestamp,
            Headers? headers) =>
            Records.Add(new Sent(topic, partition, key.ToArray(), valueIsNull ? null : value.ToArray()));
    }

    private sealed class DiscardingCollector : IRecordCollector
    {
        public void Send(
            string topic,
            int partition,
            ReadOnlySpan<byte> key,
            bool keyIsNull,
            ReadOnlySpan<byte> value,
            bool valueIsNull,
            long timestamp,
            Headers? headers)
        {
        }
    }
}
