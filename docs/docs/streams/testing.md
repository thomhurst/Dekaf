---
sidebar_position: 3
description: "Test Dekaf.Streams topologies without a broker using TopologyTestDriver: pipe records in with explicit timestamps, read output and inspect state stores."
---

# Testing Topologies

`Dekaf.Streams.Testing` provides `TopologyTestDriver`, which runs a topology in memory. It uses the same task engine as the runtime, with no broker, no threads and no waiting: `Pipe` returns after the record has been processed all the way through, including across repartition topics.

```csharp
using Dekaf.Serialization;
using Dekaf.Streams;
using Dekaf.Streams.Testing;

var builder = new StreamsBuilder("word-count");
builder.Stream("sentences", Serializers.String, Serializers.String)
    .SelectMany<string>(static (key, sentence, words) =>
    {
        foreach (var word in sentence.Split(' '))
        {
            words.Add(word);
        }
    })
    .GroupBy(static (key, word) => word, Serializers.String, Serializers.String)
    .Count("counts")
    .ToStream()
    .ToTopic("word-counts", Serializers.String, Serializers.Int64);

using var driver = new TopologyTestDriver(builder.Build());
var input = driver.CreateInputTopic("sentences", Serializers.String, Serializers.String);
var output = driver.CreateOutputTopic("word-counts", Serializers.String, Serializers.Int64);

input.Pipe("a", "hello streams", timestamp: 1_000);
input.Pipe("b", "hello again", timestamp: 2_000);

IReadOnlyList<Record<string, long>> counts = output.ReadAll();
// hello=1, streams=1, hello=2, again=1

var store = driver.GetKeyValueStore<string, long>("counts");
store.TryGet("hello", out var hello); // 2
```

## What the driver does

- Every topic has one partition, and each subtopology runs as one task.
- Timestamps are the ones you pass, in Unix milliseconds or as a `DateTimeOffset`. Windowed operators therefore behave deterministically: a window closes when you pipe a record late enough to close it.
- A null key or value is written as a null, so `input.Pipe(key, null!)` sends a tombstone to a table topic.
- `CreateOutputTopic` reads any topic the topology writes, including changelog and repartition topics, which is useful for asserting what would be restored.
- `DroppedRecordCount` counts records skipped for a null key or for arriving after their window closed.
- A processing failure follows the topology's `OnError` handler. With the default, `Pipe` throws `StreamsException`.

## What it does not cover

The driver does not exercise the parts that need a broker: group membership, rebalancing, restoring from a changelog, transactions and commit intervals. Test those against a real cluster.
