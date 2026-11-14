---
sidebar_position: 1
description: "Dekaf.Streams runs stateful stream-processing topologies on Kafka 4.4+: a typed DSL, changelog-backed state, windows, joins and exactly-once processing."
---

# Stream Processing

`Dekaf.Streams` is a stream-processing library on top of the Dekaf client. You declare a topology of typed operators; the library runs it as a member of a Kafka streams group, keeps its state in changelog topics, and by default commits output, state and consumed offsets in one transaction.

## Requirements

- **Kafka 4.4 or later** with the streams group protocol enabled, which is the default on clusters created with Kafka 4.2 or later. There is no fallback to consumer groups.
- The application's principal needs permission to read and write its topics and to create its internal topics. The group coordinator creates the internal topics when the first instance joins.

## Packages

| Package | Contents |
| --- | --- |
| `Dekaf.Streams` | Topology DSL, processor API, the runtime, in-memory state |
| `Dekaf.Streams.Storage.ZoneTree` | State kept on disk in ZoneTree, a managed LSM engine |
| `Dekaf.Streams.Extensions.Hosting` | Runs an application with the .NET generic host |
| `Dekaf.Streams.Testing` | `TopologyTestDriver` for tests without a broker |

## A first application

```csharp
using Dekaf.Serialization;
using Dekaf.Streams;

var topology = new StreamsBuilder("word-count");
topology.Stream("sentences", Serializers.String, Serializers.String)
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

var streams = KafkaStreams.Create(topology.Build())
    .WithBootstrapServers("localhost:9092")
    .Build();

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

await streams.RunAsync(shutdown.Token);
```

`RunAsync` returns when the token is cancelled, after it has committed, closed its tasks and left the group. It throws when the application fails.

The application id (`word-count`) is the streams group id and the prefix of every internal topic. This topology creates two:

- `word-count-counts-repartition`, because `GroupBy` changes the key and the count must see all records of a word in one partition;
- `word-count-counts-changelog`, which records every update of the `counts` store.

## Running with the generic host

```csharp
using Dekaf.Serialization;
using Dekaf.Streams;
using Dekaf.Streams.Extensions.Hosting;

var host = Host.CreateApplicationBuilder(args);
host.Services.AddDekafStreams(
    "word-count",
    topology => topology
        .Stream("sentences", Serializers.String, Serializers.String)
        .GroupByKey()
        .Count("counts")
        .ToStream()
        .ToTopic("sentence-counts", Serializers.String, Serializers.Int64),
    streams => streams.WithBootstrapServers(host.Configuration["Kafka:BootstrapServers"]!));

await host.Build().RunAsync();
```

The application starts and stops with the host and logs through the host's logger factory. If it fails, the hosted service faults, which stops the host under the default `BackgroundServiceExceptionBehavior`.

## How it runs

A topology is split into **subtopologies** wherever records pass through a repartition topic. Each subtopology runs as one **task** per input partition. A task owns its partitions, its processors and its own instance of every state store, and is executed by one thread at a time, so operators and stores need no locking.

The group coordinator assigns tasks to the members of the group. Each processing thread of each instance is one member. To scale out, start more instances with the same application id, or raise `WithThreads`. When the assignment changes, a task is committed and closed where it was, then restored and resumed where it goes.

`Topology.Describe()` prints the subtopologies, their topics and their stores.

## Processing guarantees

```csharp
using Dekaf.Streams;

Topology topology = null!;

var streams = KafkaStreams.Create(topology)
    .WithBootstrapServers("localhost:9092")
    .WithProcessingGuarantee(ProcessingGuarantee.ExactlyOnce) // the default
    .WithCommitInterval(TimeSpan.FromMilliseconds(100))       // the default
    .Build();
```

| Guarantee | What a commit does | After a failure |
| --- | --- | --- |
| `ExactlyOnce` (default) | Output records, changelog records and consumed offsets commit in one Kafka transaction | Uncommitted work is aborted and redone. Each input record affects output and state once |
| `AtLeastOnce` | Output is flushed, then offsets are committed, without a transaction | Input since the last commit is processed again, so output and state updates can repeat |

Exactly-once covers Kafka only. A side effect in your own code, such as an HTTP call, can run more than once.

Under exactly-once, consumers of your output topics must read with `IsolationLevel.ReadCommitted` to see only committed results, and they see them once per commit interval.

If an instance crashes with a transaction open, the broker aborts that transaction after the transaction timeout (`WithTransactionTimeout`, 10 seconds by default). Until then, read-committed consumers of the affected partitions wait.

## State

Stores are in memory by default. When a task starts, its stores are rebuilt from their changelog topics before it processes anything, so in-memory state survives restarts and moves with the task. The cost is restore time: the whole changelog is read every time a task starts.

To keep state on disk, add `Dekaf.Streams.Storage.ZoneTree`:

```csharp
using Dekaf.Streams;
using Dekaf.Streams.Storage.ZoneTree;

Topology topology = null!;

var streams = KafkaStreams.Create(topology)
    .WithBootstrapServers("localhost:9092")
    .UseZoneTreeState("/var/lib/word-count/state")
    .Build();
```

- After a **clean shutdown**, each task leaves a checkpoint, and the next start reads only the changelog records written since.
- After a **crash**, there is no checkpoint. The local state may contain uncommitted writes, so it is discarded and rebuilt from the changelog.
- Instances that run on the same machine need separate directories. A task directory is locked while in use.

The changelog is always the source of truth; the directory is a cache of it. It is safe to delete while the instance is stopped.

A table read from a topic with `StreamsBuilder.Table` uses that topic as its changelog. Give such topics `cleanup.policy=compact`.

## Failures

A record that fails to deserialize or process goes to the handler set with `StreamsBuilder.OnError`:

```csharp
using Dekaf.Streams;

var topology = new StreamsBuilder("orders");
topology.OnError(error => error.Exception is FormatException
    ? ErrorResponse.DeadLetter("orders-dead-letter")
    : ErrorResponse.Fail);
```

| Response | Effect |
| --- | --- |
| `Fail` (default) | The application stops and `RunAsync` throws. Nothing from the current commit interval is committed |
| `Continue` | The record is skipped |
| `DeadLetter(topic)` | The record's original key, value, timestamp and headers are written to the topic, and it is skipped |

When an operator fails after earlier operators already forwarded output for the same record, that output is not withdrawn.

Failures that are not about one record are handled by the runtime:

- A commit that cannot complete, for example because the group rebalanced, is aborted. The affected tasks are rebuilt from committed state and their input is processed again.
- A member that the coordinator fenced, for example after a long pause, discards its uncommitted work and rejoins.
- A produce failure that a retry cannot fix, such as a record the broker rejects as too large, stops the application.

## Observability

Logs go to the logger factory passed to `WithLoggerFactory`. Metrics are published on the `Dekaf.Streams` meter:

| Instrument | Meaning |
| --- | --- |
| `dekaf.streams.records.processed` | Records processed, counted at commit |
| `dekaf.streams.records.restored` | Changelog records applied while restoring state |
| `dekaf.streams.commits` | Completed commits |
| `dekaf.streams.commits.aborted` | Aborted commits |
| `dekaf.streams.commit.duration` | Commit time in seconds |
| `dekaf.streams.tasks.active` | Tasks currently owned |

The underlying producers and consumers publish the client metrics described in [Observability](../observability.md). The group itself can be inspected with the [streams group admin API](../admin/streams-group-management.md).

## Performance

Per record, the topology engine deserializes once at the source, makes one virtual call per operator, serializes once per sink or store write, and allocates nothing of its own. Deserializing into a reference type allocates what your serde allocates.

The runtime adds, per record written to Kafka, one copy of the serialized key and value and one `ProducerMessage` object. A consumed record that has headers gets one copy of them.

## Changing a topology

Kafka 4.4 does not let a streams group change its topology. When a changed topology is started under an existing application id, the coordinator rejects it and `RunAsync` throws a `StreamsException` that says so. Use a new application id for a changed topology, or stop every instance and delete the group and its internal topics first.

Operator and store names are part of the topology: they name the internal topics. Renaming a stateful operator starts it with empty state.

## Current limits

- No stream-stream joins, foreign-key joins or global tables. Joins are stream-table.
- Windows are tumbling and hopping. No session or sliding windows.
- No timers or punctuation in the processor API.
- A table supports `ToStream` and being joined against; it has no `Where` or `Select` of its own.
- Standby and warmup tasks are acknowledged but not kept up to date. A task that moves restores from its changelog.
- Stores cannot be queried from outside the topology.
- A windowed operator does not remember how far event time had advanced across a restart. A record that arrives after a restart for a window that had already closed is accepted into a new window instead of being dropped.
