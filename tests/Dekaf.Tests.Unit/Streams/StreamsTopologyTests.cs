using Dekaf.Serialization;
using Dekaf.Streams;
using Dekaf.Streams.Testing;

namespace Dekaf.Tests.Unit.Streams;

public sealed class StreamsTopologyTests
{
    private static readonly ISerde<string> Text = Serializers.String;
    private static readonly ISerde<long> Long = Serializers.Int64;

    [Test]
    public async Task StatelessOperators_TransformAndRouteRecords()
    {
        var builder = new StreamsBuilder("app");
        var peeked = new List<string>();
        var lines = builder.Stream("lines", Text, Text);
        var other = builder.Stream("other", Text, Text);

        lines
            .Where(static (_, line) => line.Length > 0)
            .SelectMany<string>(static (_, line, sink) =>
            {
                foreach (var word in line.Split(' '))
                {
                    sink.Add(word);
                }
            })
            .Merge(other)
            .Peek((_, word) => peeked.Add(word))
            .Select(static (_, word) => (long)word.Length)
            .ToTopic("lengths", Text, Long);

        using var driver = new TopologyTestDriver(builder.Build());
        var input = driver.CreateInputTopic("lines", Text, Text);
        var otherInput = driver.CreateInputTopic("other", Text, Text);
        var output = driver.CreateOutputTopic("lengths", Text, Long);

        input.Pipe("k1", "to be", timestamp: 5);
        input.Pipe("k2", "", timestamp: 6);
        otherInput.Pipe("k3", "merged", timestamp: 7);

        var records = output.ReadAll();
        await Assert.That(records.Select(static r => (r.Key, r.Value, r.Timestamp)))
            .IsEquivalentTo([("k1", 2L, 5L), ("k1", 2L, 5L), ("k3", 6L, 7L)]);
        await Assert.That(peeked).IsEquivalentTo(["to", "be", "merged"]);
    }

    [Test]
    public async Task WordCount_RepartitionsCountsAndLogsEveryUpdate()
    {
        var builder = new StreamsBuilder("word-count");
        builder.Stream("sentences", Text, Text)
            .SelectMany<string>(static (_, sentence, sink) =>
            {
                foreach (var word in sentence.Split(' '))
                {
                    sink.Add(word);
                }
            })
            .GroupBy(static (_, word) => word, Text, Text)
            .Count("counts")
            .ToStream()
            .ToTopic("word-counts", Text, Long);
        var topology = builder.Build();

        await Assert.That(topology.Subtopologies.Count).IsEqualTo(2);
        await Assert.That(topology.Subtopologies[0].RepartitionSinkTopics).IsEquivalentTo(["word-count-counts-repartition"]);
        await Assert.That(topology.Subtopologies[1].RepartitionSourceTopics).IsEquivalentTo(["word-count-counts-repartition"]);
        await Assert.That(topology.Subtopologies[1].ChangelogTopics).IsEquivalentTo(["word-count-counts-changelog"]);

        using var driver = new TopologyTestDriver(topology);
        var input = driver.CreateInputTopic("sentences", Text, Text);
        input.Pipe("a", "hello streams");
        input.Pipe("b", "hello again");

        var counts = driver.CreateOutputTopic("word-counts", Text, Long).ReadAll();
        await Assert.That(counts.Select(static r => (r.Key, r.Value)))
            .IsEquivalentTo([("hello", 1L), ("streams", 1L), ("hello", 2L), ("again", 1L)]);

        var store = driver.GetKeyValueStore<string, long>("counts");
        await Assert.That(store.Count).IsEqualTo(3);
        await Assert.That(store.TryGet("hello", out var hello)).IsTrue();
        await Assert.That(hello).IsEqualTo(2);
        await Assert.That(store.TryGet("missing", out _)).IsFalse();

        var changelog = driver.CreateOutputTopic("word-count-counts-changelog", Text, Long).ReadAll();
        await Assert.That(changelog.Count).IsEqualTo(4);
        await Assert.That((changelog[2].Key, changelog[2].Value)).IsEqualTo(("hello", 2L));
    }

    [Test]
    public async Task AggregateAndReduce_KeepStatePerKey()
    {
        var builder = new StreamsBuilder("app");
        var amounts = builder.Stream("amounts", Text, Long);
        amounts.GroupByKey()
            .Aggregate("totals", static () => 100L, static (_, amount, total) => total + amount, Long)
            .ToStream()
            .ToTopic("totals-out", Text, Long);
        amounts.GroupByKey()
            .Reduce("max", static (current, next) => Math.Max(current, next), Long)
            .ToStream()
            .ToTopic("max-out", Text, Long);

        using var driver = new TopologyTestDriver(builder.Build());
        var input = driver.CreateInputTopic("amounts", Text, Long);
        input.Pipe("a", 5);
        input.Pipe("a", 3);
        input.Pipe("b", 1);
        input.Pipe(null!, 9);

        await Assert.That(driver.CreateOutputTopic("totals-out", Text, Long).ReadAll().Select(static r => (r.Key, r.Value)))
            .IsEquivalentTo([("a", 105L), ("a", 108L), ("b", 101L)]);
        await Assert.That(driver.CreateOutputTopic("max-out", Text, Long).ReadAll().Select(static r => (r.Key, r.Value)))
            .IsEquivalentTo([("a", 5L), ("a", 5L), ("b", 1L)]);

        // The null-key record is skipped by both aggregations.
        await Assert.That(driver.DroppedRecordCount).IsEqualTo(2);
    }

    [Test]
    public async Task StreamTableJoin_UsesTheLatestTableValueAndHonoursTombstones()
    {
        var builder = new StreamsBuilder("app");
        var customers = builder.Table("customers", "customers-store", Text, Text);
        var orders = builder.Stream("orders", Text, Long);
        orders.Join(customers, static (amount, name) => $"{name}:{amount}").ToTopic("joined", Text, Text);
        orders.LeftJoin(customers, static (amount, name, found) => found ? $"{name}:{amount}" : $"?:{amount}")
            .ToTopic("left-joined", Text, Text);
        var topology = builder.Build();

        // The stream and the table share a subtopology and must be co-partitioned.
        await Assert.That(topology.Subtopologies.Count).IsEqualTo(1);
        var group = topology.ToStreamsGroupTopology().Subtopologies[0];
        await Assert.That(group.SourceTopics).IsEquivalentTo(["customers", "orders"]);
        await Assert.That(group.CopartitionGroups.Count).IsEqualTo(2);
        await Assert.That(group.CopartitionGroups[0].SourceTopics.Order()).IsEquivalentTo(new short[] { 0, 1 });
        await Assert.That(group.StateChangelogTopics.Count).IsEqualTo(0);

        using var driver = new TopologyTestDriver(topology);
        var customerInput = driver.CreateInputTopic("customers", Text, Text);
        var orderInput = driver.CreateInputTopic("orders", Text, Long);
        var joined = driver.CreateOutputTopic("joined", Text, Text);
        var leftJoined = driver.CreateOutputTopic("left-joined", Text, Text);

        orderInput.Pipe("c1", 10);
        customerInput.Pipe("c1", "Ada");
        orderInput.Pipe("c1", 20);
        customerInput.Pipe("c1", "Grace");
        orderInput.Pipe("c1", 30);
        customerInput.Pipe("c1", null!);
        orderInput.Pipe("c1", 40);

        await Assert.That(joined.ReadAll().Select(static r => r.Value)).IsEquivalentTo(["Ada:20", "Grace:30"]);
        await Assert.That(leftJoined.ReadAll().Select(static r => r.Value))
            .IsEquivalentTo(["?:10", "Ada:20", "Grace:30", "?:40"]);
        await Assert.That(driver.GetKeyValueStore<string, string>("customers-store").Count).IsEqualTo(0);
    }

    [Test]
    public async Task TumblingWindow_ForwardsEveryUpdateAndDropsLateRecords()
    {
        var builder = new StreamsBuilder("app");
        builder.Stream("clicks", Text, Text)
            .GroupByKey()
            .WindowedBy(TimeWindows.Tumbling(TimeSpan.FromSeconds(10)).WithGrace(TimeSpan.FromSeconds(5)))
            .Count("clicks-per-window")
            .ToStream()
            .ToTopic("out", WindowedSerde.Create(Text, TimeSpan.FromSeconds(10)), Long);

        using var driver = new TopologyTestDriver(builder.Build());
        var input = driver.CreateInputTopic("clicks", Text, Text);
        var output = driver.CreateOutputTopic("out", WindowedSerde.Create(Text, TimeSpan.FromSeconds(10)), Long);

        input.Pipe("u", "x", timestamp: 1_000);
        input.Pipe("u", "x", timestamp: 9_999);
        input.Pipe("u", "x", timestamp: 12_000);
        input.Pipe("u", "x", timestamp: 8_000);  // out of order, inside grace: window [0, 10s) closes at 15s
        input.Pipe("u", "x", timestamp: 15_000); // closes window [0, 10s)
        input.Pipe("u", "x", timestamp: 9_000);  // late

        var results = output.ReadAll().Select(static r => (r.Key.Start, r.Key.End, r.Value));
        await Assert.That(results).IsEquivalentTo(
        [
            (0L, 10_000L, 1L),
            (0L, 10_000L, 2L),
            (10_000L, 20_000L, 1L),
            (0L, 10_000L, 3L),
            (10_000L, 20_000L, 2L)
        ]);
        await Assert.That(driver.DroppedRecordCount).IsEqualTo(1);
    }

    [Test]
    public async Task EmitOnClose_ForwardsEachWindowOnceWhenItCloses()
    {
        var builder = new StreamsBuilder("app");
        var windows = TimeWindows.Tumbling(TimeSpan.FromSeconds(10)).WithGrace(TimeSpan.FromSeconds(2)).EmitOnClose();
        var serde = WindowedSerde.Create(Text, windows.Size);
        builder.Stream("clicks", Text, Text)
            .GroupByKey()
            .WindowedBy(windows)
            .Count("final-counts")
            .ToStream()
            .ToTopic("out", serde, Long);

        using var driver = new TopologyTestDriver(builder.Build());
        var input = driver.CreateInputTopic("clicks", Text, Text);
        var output = driver.CreateOutputTopic("out", serde, Long);

        input.Pipe("a", "x", timestamp: 1_000);
        input.Pipe("b", "x", timestamp: 2_000);
        input.Pipe("a", "x", timestamp: 11_000);
        await Assert.That(output.Count).IsEqualTo(0);

        input.Pipe("a", "x", timestamp: 12_000); // stream time reaches 10s + 2s grace
        var closed = output.ReadAll();
        await Assert.That(closed.Select(static r => (r.Key.Key, r.Key.Start, r.Value)))
            .IsEquivalentTo([("a", 0L, 1L), ("b", 0L, 1L)]);
        await Assert.That(closed[0].Timestamp).IsEqualTo(9_999);

        // Four updates, the record of how far windows are closed, and a tombstone per closed entry.
        var changelog = driver.CreateOutputTopic("app-final-counts-changelog", serde, Long).ReadAll();
        await Assert.That(changelog.Count).IsEqualTo(7);
        await Assert.That(changelog.Count(static record => record.IsValueNull)).IsEqualTo(2);
    }

    [Test]
    public async Task HoppingWindow_UpdatesEveryWindowThatContainsTheRecord()
    {
        var builder = new StreamsBuilder("app");
        var windows = TimeWindows.Hopping(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
        var serde = WindowedSerde.Create(Text, windows.Size);
        builder.Stream("clicks", Text, Text)
            .GroupByKey()
            .WindowedBy(windows)
            .Count("hopping")
            .ToStream()
            .ToTopic("out", serde, Long);

        using var driver = new TopologyTestDriver(builder.Build());
        driver.CreateInputTopic("clicks", Text, Text).Pipe("a", "x", timestamp: 7_000);

        var starts = driver.CreateOutputTopic("out", serde, Long).ReadAll().Select(static r => r.Key.Start);
        await Assert.That(starts).IsEquivalentTo([5_000L, 0L]);
    }

    [Test]
    public async Task StatefulHandler_KeepsStatePerKey()
    {
        var builder = new StreamsBuilder("app");
        builder.Stream("events", Text, Text)
            .Process<long, string, string>(
                "dedupe",
                Long,
                static (in Record<string, string> record, IValueState<long> lastSeen, IForwarder<string, string> output) =>
                {
                    if (lastSeen.TryGet(out var timestamp) && timestamp >= record.Timestamp)
                    {
                        return;
                    }

                    lastSeen.Set(record.Timestamp);
                    output.Forward(in record);
                })
            .ToTopic("deduped", Text, Text);
        var topology = builder.Build();
        await Assert.That(topology.Subtopologies[0].ChangelogTopics).IsEquivalentTo(["app-dedupe-changelog"]);

        using var driver = new TopologyTestDriver(topology);
        var input = driver.CreateInputTopic("events", Text, Text);
        input.Pipe("a", "first", timestamp: 10);
        input.Pipe("a", "replayed", timestamp: 10);
        input.Pipe("b", "other", timestamp: 5);
        input.Pipe("a", "second", timestamp: 11);

        await Assert.That(driver.CreateOutputTopic("deduped", Text, Text).ReadAll().Select(static r => r.Value))
            .IsEquivalentTo(["first", "other", "second"]);
        await Assert.That(driver.GetKeyValueStore<string, long>("dedupe").TryGet("a", out var last)).IsTrue();
        await Assert.That(last).IsEqualTo(11);
    }

    [Test]
    public async Task Processor_UsesOnlyTheStoresItDeclared()
    {
        var builder = new StreamsBuilder("app");
        builder.Stream("events", Text, Long)
            .Process("running-total", static () => new RunningTotal(), Stores.KeyValue("totals", Text, Long))
            .ToTopic("out", Text, Long);

        using var driver = new TopologyTestDriver(builder.Build());
        var input = driver.CreateInputTopic("events", Text, Long);
        input.Pipe("a", 2);
        input.Pipe("a", 3);

        await Assert.That(driver.CreateOutputTopic("out", Text, Long).ReadAll().Select(static r => r.Value))
            .IsEquivalentTo([2L, 5L]);

        var undeclared = new StreamsBuilder("app");
        undeclared.Stream("events", Text, Long)
            .Process("running-total", static () => new RunningTotal())
            .ToTopic("out", Text, Long);
        await Assert.That(() => new TopologyTestDriver(undeclared.Build())).Throws<StreamsException>();
    }

    [Test]
    public async Task ErrorHandler_DecidesBetweenFailContinueAndDeadLetter()
    {
        static Topology Build(Func<ProcessingError, ErrorResponse>? handler)
        {
            var builder = new StreamsBuilder("app");
            if (handler is not null)
            {
                builder.OnError(handler);
            }

            builder.Stream("numbers", Text, Text)
                .Select(static (_, value) => long.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
                .ToTopic("parsed", Text, Long);
            return builder.Build();
        }

        using (var failing = new TopologyTestDriver(Build(null)))
        {
            var input = failing.CreateInputTopic("numbers", Text, Text);
            var thrown = await Assert.That(() => input.Pipe("k", "nope")).Throws<StreamsException>();
            await Assert.That(thrown!.InnerException).IsTypeOf<FormatException>();
        }

        ProcessingError seen = default;
        using (var skipping = new TopologyTestDriver(Build(error =>
        {
            seen = error;
            return ErrorResponse.Continue;
        })))
        {
            var input = skipping.CreateInputTopic("numbers", Text, Text);
            input.Pipe("k", "1");
            input.Pipe("k", "nope");
            input.Pipe("k", "3");
            await Assert.That(skipping.CreateOutputTopic("parsed", Text, Long).ReadAll().Select(static r => r.Value))
                .IsEquivalentTo([1L, 3L]);
            await Assert.That((seen.Topic, seen.Partition, seen.Offset)).IsEqualTo(("numbers", 0, 1L));
        }

        using var deadLettering = new TopologyTestDriver(Build(static _ => ErrorResponse.DeadLetter("numbers-dlq")));
        deadLettering.CreateInputTopic("numbers", Text, Text).Pipe("k", "nope", timestamp: 42);
        var dead = deadLettering.CreateOutputTopic("numbers-dlq", Text, Text).ReadAll();
        await Assert.That(dead.Count).IsEqualTo(1);
        await Assert.That((dead[0].Key, dead[0].Value, dead[0].Timestamp)).IsEqualTo(("k", "nope", 42L));
    }

    [Test]
    public async Task Build_RejectsTopologiesThatCannotRun()
    {
        await Assert.That(() => new StreamsBuilder("bad name")).Throws<TopologyException>();
        await Assert.That(() => new StreamsBuilder("app").Build()).Throws<TopologyException>();

        var duplicateTopic = new StreamsBuilder("app");
        duplicateTopic.Stream("t", Text, Text);
        await Assert.That(() => duplicateTopic.Table("t", "store", Text, Text)).Throws<TopologyException>();

        var duplicateName = new StreamsBuilder("app");
        var stream = duplicateName.Stream("t", Text, Text);
        stream.GroupByKey().Count("counts");
        await Assert.That(() => stream.GroupByKey().Count("counts")).Throws<TopologyException>();

        // After the key changes, records are not partitioned by it until they are repartitioned.
        var rekeyed = new StreamsBuilder("app").Stream("t", Text, Text).SelectKey(static (_, value) => value);
        await Assert.That(() => rekeyed.GroupByKey()).Throws<TopologyException>();
        var repartitioned = rekeyed.Repartition("by-value", Text, Text);
        await Assert.That(repartitioned.GroupByKey()).IsNotNull();

        var windowedBuilder = new StreamsBuilder("app");
        var windowed = windowedBuilder.Stream("t", Text, Text)
            .GroupByKey()
            .WindowedBy(TimeWindows.Tumbling(TimeSpan.FromSeconds(1)))
            .Count("windowed");
        await Assert.That(() => windowed.ToStream().Join(windowed, static (count, _) => count)).Throws<TopologyException>();

        var built = new StreamsBuilder("app");
        var source = built.Stream("t", Text, Text);
        source.ToTopic("out", Text, Text);
        built.Build();
        await Assert.That(() => source.Where(static (_, _) => true)).Throws<TopologyException>();
    }

    [Test]
    public async Task ToStreamsGroupTopology_DescribesInternalTopicsAndTheProcessingGraph()
    {
        var builder = new StreamsBuilder("app");
        builder.Stream("clicks", Text, Text)
            .GroupBy(static (_, page) => page, Text, Text)
            .WindowedBy(TimeWindows.Tumbling(TimeSpan.FromMinutes(1)).WithGrace(TimeSpan.FromSeconds(30)))
            .Count("per-page")
            .ToStream()
            .ToTopic("out", WindowedSerde.Create(Text, TimeSpan.FromMinutes(1)), Long);
        var topology = builder.Build();
        var group = topology.ToStreamsGroupTopology();

        await Assert.That(group.Subtopologies.Select(static s => s.SubtopologyId)).IsEquivalentTo(["0", "1"]);
        await Assert.That(group.Subtopologies[0].SourceTopics).IsEquivalentTo(["clicks"]);
        await Assert.That(group.Subtopologies[0].RepartitionSinkTopics).IsEquivalentTo(["app-per-page-repartition"]);

        var repartition = group.Subtopologies[1].RepartitionSourceTopics.Single();
        await Assert.That(repartition.Name).IsEqualTo("app-per-page-repartition");
        await Assert.That(repartition.TopicConfigs.Select(static c => (c.Key, c.Value)))
            .IsEquivalentTo([("cleanup.policy", "delete"), ("retention.ms", "-1")]);

        var changelog = group.Subtopologies[1].StateChangelogTopics.Single();
        await Assert.That(changelog.Name).IsEqualTo("app-per-page-changelog");
        await Assert.That(changelog.TopicConfigs.Select(static c => (c.Key, c.Value)))
            .IsEquivalentTo([("cleanup.policy", "compact,delete"), ("retention.ms", "86490000")]);

        var graph = group.Description!.Subtopologies[1].Nodes;
        await Assert.That(graph.Single(static n => n.Name == "per-page").Stores).IsEquivalentTo(["per-page"]);
        await Assert.That(graph[^1].SinkTopic).IsEqualTo("out");
        await Assert.That(topology.Describe()).Contains("Processor per-page (stores: per-page) --> ");
    }

    [Test]
    public async Task WindowedSerde_WritesTheKeyThenTheBigEndianWindowStart()
    {
        var serde = WindowedSerde.Create(Text, TimeSpan.FromSeconds(10));
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var context = new SerializationContext { Topic = "t", Component = SerializationComponent.Key };
        serde.Serialize(new Windowed<string>("ab", 258, 10_258), ref buffer, context);

        await Assert.That(buffer.WrittenSpan.ToArray()).IsEquivalentTo(new byte[] { (byte)'a', (byte)'b', 0, 0, 0, 0, 0, 0, 1, 2 });
        await Assert.That(serde.Deserialize(buffer.WrittenMemory, context)).IsEqualTo(new Windowed<string>("ab", 258, 10_258));
        await Assert.That(() => serde.Deserialize(new byte[3], context)).Throws<StreamsException>();
    }

    [Test]
    public async Task NullKeysAndValues_StayNullForValueTypes()
    {
        var builder = new StreamsBuilder("app");
        var numbers = builder.Stream("numbers", Long, Long);
        numbers.ToTopic("copy", Long, Long);
        numbers.GroupByKey().Count("counts");
        builder.Table("balances", "balances-store", Text, Long).ToStream().ToTopic("balance-changes", Text, Long);

        using var driver = new TopologyTestDriver(builder.Build());
        ISerializer<byte[]?> bytes = Serializers.ByteArray!;
        var raw = driver.CreateInputTopic("numbers", bytes, bytes);
        raw.Pipe(null, new byte[8]);
        raw.Pipe(new byte[8], null);

        // A null key read as long is not key 0: it is not counted, and it is written back as null.
        await Assert.That(driver.GetKeyValueStore<long, long>("counts").Count).IsEqualTo(1);
        await Assert.That(driver.DroppedRecordCount).IsEqualTo(1);
        var copies = driver.CreateOutputTopic("copy", Long, Long).ReadAll();
        await Assert.That(copies[0].IsKeyNull).IsTrue();
        await Assert.That(copies[0].IsValueNull).IsFalse();
        await Assert.That(copies[1].IsKeyNull).IsFalse();
        await Assert.That(copies[1].IsValueNull).IsTrue();

        // A tombstone on a table of longs deletes the key and is forwarded as a tombstone, not as 0.
        var balances = driver.CreateInputTopic("balances", Text, bytes);
        balances.Pipe("a", new byte[8]);
        balances.Pipe("a", null);
        var changes = driver.CreateOutputTopic("balance-changes", Text, Long).ReadAll();
        await Assert.That(changes[0].IsValueNull).IsFalse();
        await Assert.That(changes[1].IsValueNull).IsTrue();
        await Assert.That(driver.GetKeyValueStore<string, long>("balances-store").Count).IsEqualTo(0);
    }

    [Test]
    public async Task EmitOnClose_AFailingResultDoesNotDiscardTheOthers()
    {
        var builder = new StreamsBuilder("app");
        builder.OnError(static _ => ErrorResponse.Continue);
        var windows = TimeWindows.Tumbling(TimeSpan.FromSeconds(10)).EmitOnClose();
        builder.Stream("clicks", Text, Text)
            .GroupByKey()
            .WindowedBy(windows)
            .Count("final")
            .ToStream()
            .Select(static (key, count) => key.Key == "bad" ? throw new InvalidOperationException("bad") : count)
            .SelectKey(static (key, _) => key.Key)
            .ToTopic("out", Text, Long);

        using var driver = new TopologyTestDriver(builder.Build());
        var input = driver.CreateInputTopic("clicks", Text, Text);
        foreach (var key in new[] { "a", "bad", "b", "c" })
        {
            input.Pipe(key, "x", timestamp: 1_000);
        }

        // Closing the window fails on "bad". The record that closed it is skipped; the other results of the
        // window are still in the store and are emitted by the next record.
        input.Pipe("z", "x", timestamp: 10_000);
        input.Pipe("z", "x", timestamp: 10_001);

        var emitted = driver.CreateOutputTopic("out", Text, Long).ReadAll().Select(static r => r.Key).ToArray();
        await Assert.That(emitted.Order()).IsEquivalentTo(["a", "b", "c"]);
    }

    [Test]
    public async Task Stores_GiveValueSerdesTheKeyBytesAndTheTopicTheyAreRestoredFrom()
    {
        var serde = new RecordingSerde();
        var builder = new StreamsBuilder("app");
        var table = builder.Table("customers", "customers-store", Text, serde);
        builder.Stream("orders", Text, Text)
            .Join(table, static (order, customer) => $"{customer}:{order}")
            .ToTopic("out", Text, Text);
        builder.Stream("events", Text, Text)
            .GroupByKey()
            .Aggregate("latest", static () => "", static (_, value, _) => value, serde);

        using var driver = new TopologyTestDriver(builder.Build());
        driver.CreateInputTopic("customers", Text, Text).Pipe("c1", "Ada");
        driver.CreateInputTopic("orders", Text, Text).Pipe("c1", "o1");
        var events = driver.CreateInputTopic("events", Text, Text);
        events.Pipe("e1", "first");
        events.Pipe("e1", "second");

        // The table is restored from its source topic, so its serdes work in that topic's context.
        await Assert.That(serde.Topics).Contains("customers");
        await Assert.That(serde.Topics).Contains("app-latest-changelog");
        await Assert.That(serde.Topics).DoesNotContain("customers-store");

        // Reads from a store pass the key bytes, as a read from the source does.
        await Assert.That(serde.ReadKeys).Contains("c1");
        await Assert.That(serde.ReadKeys).Contains("e1");
        await Assert.That(serde.ReadKeys).DoesNotContain("");
    }

    [Test]
    public async Task Merge_RequiresItsSourceTopicsToBeCopartitioned()
    {
        var builder = new StreamsBuilder("app");
        var first = builder.Stream("first", Text, Text);
        var second = builder.Stream("second", Text, Text);
        first.Merge(second).GroupByKey().Count("merged-counts");

        // Without this the same key could sit in different tasks and be counted twice over.
        var group = builder.Build().ToStreamsGroupTopology().Subtopologies.Single().CopartitionGroups.Single();
        await Assert.That(group.SourceTopics.Order()).IsEquivalentTo(new short[] { 0, 1 });

        // Branching a stream and merging it back reads one topic, which needs no constraint.
        var branched = new StreamsBuilder("app");
        var source = branched.Stream("only", Text, Text);
        source.Where(static (_, value) => value.Length > 1).Merge(source.Where(static (_, value) => value.Length <= 1))
            .ToTopic("out", Text, Text);
        await Assert.That(branched.Build().ToStreamsGroupTopology().Subtopologies.Single().CopartitionGroups).IsEmpty();
    }

    [Test]
    public async Task HandlesFromAnotherBuilder_AreRejected()
    {
        var one = new StreamsBuilder("one");
        var other = new StreamsBuilder("other");
        var stream = one.Stream("a", Text, Text);
        var foreignStream = other.Stream("b", Text, Text);
        var foreignTable = other.Table("c", "store", Text, Text);

        await Assert.That(() => stream.Merge(foreignStream)).Throws<TopologyException>();
        await Assert.That(() => stream.Join(foreignTable, static (left, right) => left + right)).Throws<TopologyException>();
    }

    private sealed class RecordingSerde : ISerde<string>
    {
        public HashSet<string> Topics
        {
            get;
        } = [];

        public HashSet<string> ReadKeys
        {
            get;
        } = [];

        public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
            where TWriter : System.Buffers.IBufferWriter<byte>, allows ref struct
        {
            Topics.Add(context.Topic);
            Serializers.String.Serialize(value, ref destination, context);
        }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Topics.Add(context.Topic);
            ReadKeys.Add(System.Text.Encoding.UTF8.GetString(context.KeyData.Span));
            return Serializers.String.Deserialize(data, context);
        }
    }

    private sealed class RunningTotal : IProcessor<string, long, string, long>
    {
        private IProcessorContext<string, long> _context = null!;
        private IKeyValueStore<string, long> _totals = null!;

        public void Init(IProcessorContext<string, long> context)
        {
            _context = context;
            _totals = context.GetKeyValueStore<string, long>("totals");
        }

        public void Process(in Record<string, long> record)
        {
            _totals.TryGet(record.Key, out var total);
            total += record.Value;
            _totals.Put(record.Key, total);
            _context.Forward(record.WithValue(total));
        }
    }
}
