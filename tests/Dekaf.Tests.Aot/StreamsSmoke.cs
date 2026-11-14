using Dekaf.Serialization;
using Dekaf.Streams;
using Dekaf.Streams.Storage.ZoneTree;
using Dekaf.Streams.Testing;

/// <summary>
/// Runs a stateful Dekaf.Streams topology and the ZoneTree state engine under Native AOT: generic
/// operators over value and reference types, windowing, the processor API and the managed LSM store.
/// </summary>
internal static class StreamsSmoke
{
    internal static void Run()
    {
        var builder = new StreamsBuilder("aot-streams");
        var words = builder.Stream("sentences", Serializers.String, Serializers.String)
            .SelectMany<string>(static (_, sentence, sink) =>
            {
                foreach (var word in sentence.Split(' '))
                    sink.Add(word);
            });
        words.GroupBy(static (_, word) => word, Serializers.String, Serializers.String)
            .Count("word-counts")
            .ToStream()
            .ToTopic("counts", Serializers.String, Serializers.Int64);
        words.GroupByKey()
            .WindowedBy(TimeWindows.Tumbling(TimeSpan.FromSeconds(10)).EmitOnClose())
            .Count("words-per-window")
            .ToStream()
            .ToTopic("windowed", WindowedSerde.Create(Serializers.String, TimeSpan.FromSeconds(10)), Serializers.Int64);
        words.Process<long, string, long>(
                "running-length",
                Serializers.Int64,
                static (in Record<string, string> record, IValueState<long> total, IForwarder<string, long> output) =>
                {
                    total.TryGet(out var current);
                    current += record.Value.Length;
                    total.Set(current);
                    output.Forward(record.WithValue(current));
                })
            .ToTopic("lengths", Serializers.String, Serializers.Int64);

        using (var driver = new TopologyTestDriver(builder.Build()))
        {
            var input = driver.CreateInputTopic("sentences", Serializers.String, Serializers.String);
            input.Pipe("k", "hello aot", timestamp: 1_000);
            input.Pipe("k", "hello", timestamp: 12_000);

            var counts = driver.CreateOutputTopic("counts", Serializers.String, Serializers.Int64).ReadAll();
            var windowed = driver.CreateOutputTopic(
                "windowed", WindowedSerde.Create(Serializers.String, TimeSpan.FromSeconds(10)), Serializers.Int64).ReadAll();
            var lengths = driver.CreateOutputTopic("lengths", Serializers.String, Serializers.Int64).ReadAll();
            if (counts.Count != 3 || counts[2].Key != "hello" || counts[2].Value != 2
                || windowed.Count != 1 || windowed[0].Key.Start != 0 || windowed[0].Value != 2
                || lengths.Count != 3 || lengths[2].Value != 13
                || !driver.GetKeyValueStore<string, long>("word-counts").TryGet("aot", out var aot) || aot != 1)
                throw new InvalidOperationException("Dekaf.Streams topology produced unexpected results under Native AOT.");
        }

        var directory = Path.Combine(Path.GetTempPath(), $"dekaf-aot-streams-{Guid.NewGuid():N}");
        try
        {
            var context = new StateStoreContext("aot-streams", "store", "0", 0, directory);
            using (var store = ZoneTreeStateStoreEngine.Instance.CreateKeyValueStore(context))
                store.Put([1, 2], [3]);
            using var reopened = ZoneTreeStateStoreEngine.Instance.CreateKeyValueStore(context);
            if (!reopened.TryGet([1, 2], out var value) || value.Length != 1 || value.Span[0] != 3)
                throw new InvalidOperationException("ZoneTree state store lost its data under Native AOT.");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
