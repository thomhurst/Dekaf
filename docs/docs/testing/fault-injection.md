---
sidebar_position: 2
description: "Script deterministic producer, consumer, group, transaction, Admin, and Share Consumer failures with Dekaf.Testing."
---

# Deterministic fault injection

`InMemoryKafkaCluster.FaultPlan` scripts client failures and pause points without a broker,
wall-clock sleeps, or production-code hooks. Every in-memory client backed by the cluster consumes
the same thread-safe plan.

```csharp
var cluster = new InMemoryKafkaCluster();
var observations = new List<KafkaFaultObservation>();
cluster.FaultPlan.FaultConsumed += observations.Add;

cluster.FaultPlan.Fail(
    new KafkaFaultScope(
        KafkaFaultOperation.Produce,
        topic: "orders",
        partition: 0),
    new KafkaTimeoutException("acknowledgement timed out"),
    occurrenceCount: 2);

await using var producer = new InMemoryProducer<string, string>(cluster);
```

The fault plan belongs only to `Dekaf.Testing`. Production clients and production hot paths do not
reference it.

## Matching and precedence

A `KafkaFaultScope` always selects an operation. Its optional `topic`, `partition`, and `groupId`
selectors narrow the rule; a null selector is a wildcard. A rule matches only when every non-null
selector equals the concrete operation scope.

Rules are evaluated in insertion order. The earliest queued matching rule wins, even when a later
rule is more specific. For operations that expose several concrete resources, such as a commit of
several offsets, rule order is considered first and resource order second. Put the failure that
must occur first into the plan first.

The operations and concrete selectors supplied by the in-memory adapters are:

| Client area | Operations | Selectors when applicable |
| --- | --- | --- |
| Producer | `Produce` | topic, resolved partition |
| Consumer delivery | `Fetch`, `Consume` | topic, partition, group ID |
| Consumer offsets | `StoreOffset`, `Commit` | topic, partition, group ID |
| Consumer groups | `JoinGroup`, `SyncGroup`, `Rebalance` | group ID |
| Transactions | `InitializeTransactions`, `TransactionProduce`, `SendOffsetsToTransaction`, `CommitTransaction`, `AbortTransaction` | transaction produce: topic and partition; offset send: group ID |
| Admin | `Admin` | selectors vary with the Admin operation |
| Share Consumer | `ShareConsume`, `ShareAcknowledge` | topic, partition, group ID |

`JoinGroup`, `SyncGroup`, and `Rebalance` reject topic and partition selectors because group
transitions do not target one resource.

## Occurrences and clearing

Choose the lifetime that represents the behavior under test:

| API | Behavior |
| --- | --- |
| `Fail(scope, exception)` | Throws once on the next matching operation. |
| `Fail(scope, exception, occurrenceCount: n)` | Throws on the next `n` matching operations. The plan still reports one queued entry. |
| `FailPersistently(scope, exception)` | Throws on every matching operation until cleared. |
| `PauseNext(scope)` | Pauses the next matching operation at a one-shot barrier. |

`Clear(scope)` removes rules whose configured scope exactly equals `scope`; it does not perform
wildcard matching. `Clear()` removes all queued rules. Both methods return the number of removed
entries. Clearing an unentered barrier cancels `WaitUntilEnteredAsync` and marks the barrier
released. An entered barrier has already been consumed, so its owner must call `Release()`.

A cancellation token rejected before matching does not consume a rule. Once a barrier is consumed,
cancelling the paused operation stops that operation but does not put the one-shot rule back into
the plan.

## Deterministic barriers

Use a barrier to prove that an operation reached an exact point. Do not replace the entered signal
with `Task.Delay`.

```csharp
public static class ProduceBarrierScenario
{
    public static async Task RunAsync()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var producer = new InMemoryProducer<string, string>(cluster);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Produce, topic: "orders"));

        var pending = producer.ProduceAsync("orders", "order-1", "created").AsTask();
        await barrier.WaitUntilEnteredAsync();

        if (pending.IsCompleted)
            throw new InvalidOperationException("Produce passed the barrier before release.");

        if (!barrier.Release())
            throw new InvalidOperationException("Barrier was already released.");

        _ = await pending;
    }
}
```

## Observations

`FaultConsumed` runs synchronously after a matching entry is consumed and before its throw or pause
action runs. Each `KafkaFaultObservation` exposes:

- `RuleScope`: the configured scope, including wildcard selectors.
- `OperationScope`: the concrete operation that matched.
- `Action`: `Throw` or `Pause`.
- `Exception`: the configured exception for a throw action; otherwise null.
- `IsPersistent`: whether the rule remains active until cleared.
- `RemainingOccurrences`: the next-N count after this consumption, or null for a persistent rule.

Keep handlers deterministic and non-blocking. An exception thrown by a handler becomes the client
operation's exception.

## Scenario: singleton producer fatal handling

A `FatalTransactionException` injected at a producer or transaction fault boundary is captured by
that producer. Every later operation on the same long-lived instance throws the same exception
object. Recovery therefore requires replacing the producer, which lets an application test its
singleton lifecycle policy without killing a broker.

```csharp
public static class SingletonProducerFatalScenario
{
    public static async Task RunAsync()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        var fatal = new FatalTransactionException(
            ErrorCode.InvalidProducerEpoch,
            "fabricated fatal producer state");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Produce, topic: "orders"),
            fatal);

        await using var singletonProducer = new InMemoryProducer<string, string>(cluster);
        FatalTransactionException? first = null;
        try
        {
            _ = await singletonProducer.ProduceAsync("orders", "order-1", "created");
        }
        catch (FatalTransactionException exception)
        {
            first = exception;
        }

        FatalTransactionException? repeated = null;
        try
        {
            _ = await singletonProducer.ProduceAsync("orders", "order-2", "created");
        }
        catch (FatalTransactionException exception)
        {
            repeated = exception;
        }

        if (!ReferenceEquals(first, fatal) || !ReferenceEquals(repeated, fatal))
            throw new InvalidOperationException("The producer did not retain its fatal state.");

        await using var replacement = new InMemoryProducer<string, string>(cluster);
        var recovered = await replacement.ProduceAsync("orders", "order-3", "created");
        if (recovered.Offset != 0)
            throw new InvalidOperationException("Replacement producer did not recover.");
    }
}
```

## Scenario: consumer commit recovery

A failed commit leaves the stored offset available for retry. The following test processes one
record, injects a resource-scoped commit failure, proves nothing was committed, and retries.

```csharp
public static class ConsumerCommitRecoveryScenario
{
    public static async Task RunAsync()
    {
        const string topic = "orders";
        const string groupId = "billing";
        var partition = new TopicPartition(topic, 0);
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(topic);

        await using var producer = new InMemoryProducer<string, string>(cluster);
        _ = await producer.ProduceAsync(topic, "order-1", "created");

        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Manual,
                EnableAutoOffsetStore = false
            });
        consumer.Assign(partition);
        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero)
            ?? throw new InvalidOperationException("Expected one record.");
        consumer.StoreOffset(result);

        var failure = new KafkaTimeoutException("commit timed out");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(
                KafkaFaultOperation.Commit,
                topic,
                partition: 0,
                groupId),
            failure);

        try
        {
            await consumer.CommitAsync();
            throw new InvalidOperationException("Expected the injected commit failure.");
        }
        catch (KafkaTimeoutException exception) when (ReferenceEquals(exception, failure))
        {
        }

        if (await consumer.GetCommittedOffsetAsync(partition) is not null)
            throw new InvalidOperationException("The failed commit changed broker state.");

        await consumer.CommitAsync();
        if (await consumer.GetCommittedOffsetAsync(partition) != 1)
            throw new InvalidOperationException("The retry did not commit the stored offset.");
    }
}
```

## Scenario: rebalance recovery

A failed group transition preserves the existing subscription and assignment. Retrying the same
transition consumes no further fault and completes normally.

```csharp
public static class RebalanceRecoveryScenario
{
    public static async Task RunAsync()
    {
        const string groupId = "billing";
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        cluster.CreateTopic("payments");
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest
            });
        consumer.Subscribe("orders");

        var failure = new InvalidOperationException("rebalance failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Rebalance, groupId: groupId),
            failure);

        try
        {
            consumer.Subscribe("payments");
            throw new InvalidOperationException("Expected the injected rebalance failure.");
        }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception, failure))
        {
        }

        if (!consumer.Subscription.SetEquals(["orders"]) ||
            !consumer.Assignment.SetEquals([new TopicPartition("orders", 0)]))
        {
            throw new InvalidOperationException("The failed rebalance changed assignment.");
        }

        consumer.Subscribe("payments");
        if (!consumer.Subscription.SetEquals(["payments"]))
            throw new InvalidOperationException("The rebalance retry did not recover.");
    }
}
```

## Scenario: transaction fencing

Inject `ProducerFenced` at the transactional produce boundary to prove that the transaction and its
producer retain one fatal exception while a replacement producer remains healthy.

```csharp
public static class TransactionFencingScenario
{
    public static async Task RunAsync()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        var fenced = new FatalTransactionException(
            ErrorCode.ProducerFenced,
            "producer fenced");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.TransactionProduce, topic: "orders"),
            fenced);

        await using var producer = new InMemoryProducer<string, string>(cluster);
        await using var transaction = producer.BeginTransaction();
        FatalTransactionException? produceFailure = null;
        try
        {
            _ = await transaction.ProduceAsync("orders", "order-1", "created");
        }
        catch (FatalTransactionException exception)
        {
            produceFailure = exception;
        }

        FatalTransactionException? commitFailure = null;
        try
        {
            await transaction.CommitAsync();
        }
        catch (FatalTransactionException exception)
        {
            commitFailure = exception;
        }

        if (!ReferenceEquals(produceFailure, fenced) || !ReferenceEquals(commitFailure, fenced))
            throw new InvalidOperationException("Fencing did not poison the transaction producer.");

        await using var replacement = new InMemoryProducer<string, string>(cluster);
        var recovered = await replacement.ProduceAsync("orders", "order-2", "created");
        if (recovered.Offset != 0)
            throw new InvalidOperationException("Replacement producer did not recover.");
    }
}
```

## Feature coverage

The parent [scripted fault-injection feature](https://github.com/thomhurst/Dekaf/issues/2757) is
split into independently reviewed surfaces:

- [Fault-plan engine and ordering](https://github.com/thomhurst/Dekaf/issues/2798)
- [Producer and transaction adapters](https://github.com/thomhurst/Dekaf/issues/2799)
- [Consumer and group adapters](https://github.com/thomhurst/Dekaf/issues/2800)
- [Admin and Share Consumer adapters](https://github.com/thomhurst/Dekaf/issues/2801)
- [Documentation and scenarios](https://github.com/thomhurst/Dekaf/issues/2802)

Use a real broker or Testcontainers for wire-protocol, broker-version, and network-partition tests.
The in-memory plan tests application recovery semantics at deterministic client boundaries.
