using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryProducerFaultTests
{
    [Test]
    public async Task Produce_FaultsHonorScopeOccurrenceAndRetryBoundaries()
    {
        var plan = new KafkaFaultPlan();
        var cluster = new InMemoryKafkaCluster(plan);
        cluster.CreateTopic("orders", partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        var retriable = new ProduceException(ErrorCode.NotLeaderOrFollower, "retry");
        var nonRetriable = new ProduceException(ErrorCode.MessageTooLarge, "reject");
        var scope = new KafkaFaultScope(KafkaFaultOperation.Produce, "orders", partition: 1);
        plan.Fail(scope, retriable);
        plan.Fail(scope, nonRetriable);

        var first = await Assert.ThrowsAsync<ProduceException>(() => ProduceToPartitionAsync(producer, 1));
        var second = await Assert.ThrowsAsync<ProduceException>(() => ProduceToPartitionAsync(producer, 1));
        var otherPartition = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "orders",
            Partition = 0,
            Key = "k",
            Value = "other"
        });
        var recovered = await ProduceToPartitionAsync(producer, 1);

        await Assert.That(first).IsSameReferenceAs(retriable);
        await Assert.That(first!.IsRetriable).IsTrue();
        await Assert.That(second).IsSameReferenceAs(nonRetriable);
        await Assert.That(second!.IsRetriable).IsFalse();
        await Assert.That(otherPartition.Partition).IsEqualTo(0);
        await Assert.That(recovered.Partition).IsEqualTo(1);
        await Assert.That(cluster.ReadRecords("orders", 1)).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Produce_LegacyFailureTakesPriorityWithoutConsumingScript()
    {
        var plan = new KafkaFaultPlan();
        var cluster = new InMemoryKafkaCluster(plan);
        var producer = new InMemoryProducer<string, string>(cluster);
        var legacy = new InvalidOperationException("legacy");
        var scripted = new TimeoutException("scripted");
        cluster.FailProduces("orders", legacy);
        plan.Fail(new KafkaFaultScope(KafkaFaultOperation.Produce, "orders"), scripted);

        var first = await Assert.ThrowsAsync<InvalidOperationException>(
            () => producer.ProduceAsync("orders", "k", "v").AsTask());
        cluster.ClearProduceFailure("orders");
        var second = await Assert.ThrowsAsync<TimeoutException>(
            () => producer.ProduceAsync("orders", "k", "v").AsTask());

        await Assert.That(first).IsSameReferenceAs(legacy);
        await Assert.That(second).IsSameReferenceAs(scripted);
    }

    [Test]
    public async Task FireAsync_ConsumesFaultWithoutSurfacingDeliveryFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Produce, "events"),
            new InvalidOperationException("delivery failed"));

        await producer.FireAsync("events", "k", "first");
        var metadata = await producer.ProduceAsync("events", "k", "second");

        await Assert.That(metadata.Offset).IsEqualTo(0);
        await Assert.That(cluster.ReadRecords("events")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ProduceBarrier_CancellationDoesNotAppendAndRetryRecovers()
    {
        var plan = new KafkaFaultPlan();
        var cluster = new InMemoryKafkaCluster(plan);
        var producer = new InMemoryProducer<string, string>(cluster);
        var barrier = plan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Produce, "orders", partition: 0));
        using var cancellation = new CancellationTokenSource();
        var message = new ProducerMessage<string, string>
        {
            Topic = "orders",
            Partition = 0,
            Key = "k",
            Value = "v"
        };

        var pending = producer.ProduceAsync(message, cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        await Assert.That(barrier.Release()).IsTrue();
        await Assert.That(cluster.ReadRecords("orders")).IsEmpty();
        var recovered = await producer.ProduceAsync(message);
        await Assert.That(recovered.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task InitTransactions_OneShotFailureAllowsRetry()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var failure = new KafkaTimeoutException("coordinator unavailable");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.InitializeTransactions),
            failure);

        var actual = await Assert.ThrowsAsync<KafkaTimeoutException>(
            () => producer.InitTransactionsAsync().AsTask());
        await producer.InitTransactionsAsync();

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task TransactionProduce_FailureIsScopedAndRetryable()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        var failure = new ProduceException(ErrorCode.NotLeaderOrFollower, "retry transaction produce");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.TransactionProduce, "orders", partition: 1),
            failure);
        await using var transaction = producer.BeginTransaction();
        var message = new ProducerMessage<string, string>
        {
            Topic = "orders",
            Partition = 1,
            Key = "k",
            Value = "v"
        };

        var actual = await Assert.ThrowsAsync<ProduceException>(
            () => transaction.ProduceAsync(message).AsTask());
        var recovered = await transaction.ProduceAsync(message);
        await transaction.CommitAsync();

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(recovered.Partition).IsEqualTo(1);
    }

    [Test]
    public async Task TransactionOperations_FailBeforeMutationAndRecoverWithoutDelays()
    {
        var plan = new KafkaFaultPlan();
        var cluster = new InMemoryKafkaCluster(plan);
        var producer = new InMemoryProducer<string, string>(cluster);
        var offsetFailure = new KafkaTimeoutException("offset send failed");
        var commitFailure = new KafkaTimeoutException("commit failed");
        var abortFailure = new KafkaTimeoutException("abort failed");
        plan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.SendOffsetsToTransaction, groupId: "billing"),
            offsetFailure);
        plan.Fail(new KafkaFaultScope(KafkaFaultOperation.CommitTransaction), commitFailure);
        plan.Fail(new KafkaFaultScope(KafkaFaultOperation.AbortTransaction), abortFailure);

        await using (var transaction = producer.BeginTransaction())
        {
            var offsets = new[] { new TopicPartitionOffset("orders", 0, 17) };
            var actualOffsetFailure = await Assert.ThrowsAsync<KafkaTimeoutException>(
                () => transaction.SendOffsetsToTransactionAsync(offsets, "billing").AsTask());
            await transaction.SendOffsetsToTransactionAsync(offsets, "billing");
            var actualCommitFailure = await Assert.ThrowsAsync<KafkaTimeoutException>(
                () => transaction.CommitAsync().AsTask());

            await Assert.That(actualOffsetFailure).IsSameReferenceAs(offsetFailure);
            await Assert.That(actualCommitFailure).IsSameReferenceAs(commitFailure);
            await Assert.That(cluster.GetCommittedOffset("billing", new TopicPartition("orders", 0))).IsNull();
            await transaction.CommitAsync();
        }

        await Assert.That(cluster.GetCommittedOffset("billing", new TopicPartition("orders", 0))).IsEqualTo(17);

        await using (var transaction = producer.BeginTransaction())
        {
            var actualAbortFailure = await Assert.ThrowsAsync<KafkaTimeoutException>(
                () => transaction.AbortAsync().AsTask());
            await Assert.That(actualAbortFailure).IsSameReferenceAs(abortFailure);
            await transaction.AbortAsync();
        }

        await using var recoveredTransaction = producer.BeginTransaction();
        await recoveredTransaction.AbortAsync();
    }

    [Test]
    public async Task PreparedTransaction_ProducerCompletionSupportsCommitAndAbort()
    {
        var producer = new InMemoryProducer<string, string>(new InMemoryKafkaCluster());

        await using (var transaction = producer.BeginTransaction())
        {
            var prepared = await transaction.PrepareAsync();
            await Assert.That(prepared.HasTransaction).IsTrue();
            await producer.CompletePreparedTransactionAsync(prepared, committed: true);
        }

        await using (var transaction = producer.BeginTransaction())
        {
            var prepared = await transaction.PrepareAsync();
            await producer.CompletePreparedTransactionAsync(prepared, committed: false);
        }

        await using var recovered = producer.BeginTransaction();
        await recovered.AbortAsync();
    }

    [Test]
    public async Task ProducerFencing_CapturesOneFatalInstanceAndPoisonsOnlyProducer()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var fenced = new FatalTransactionException(ErrorCode.ProducerFenced, "producer fenced");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.TransactionProduce, "orders"),
            fenced);
        await using var transaction = producer.BeginTransaction();

        var initial = await Assert.ThrowsAsync<FatalTransactionException>(
            () => transaction.ProduceAsync("orders", "k", "v").AsTask());
        var commit = await Assert.ThrowsAsync<FatalTransactionException>(
            () => transaction.CommitAsync().AsTask());
        var initialize = await Assert.ThrowsAsync<FatalTransactionException>(
            () => producer.InitTransactionsAsync().AsTask());
        var produce = await Assert.ThrowsAsync<FatalTransactionException>(
            () => producer.ProduceAsync("orders", "k", "v").AsTask());

        await Assert.That(initial).IsSameReferenceAs(fenced);
        await Assert.That(commit).IsSameReferenceAs(fenced);
        await Assert.That(initialize).IsSameReferenceAs(fenced);
        await Assert.That(produce).IsSameReferenceAs(fenced);
        await Assert.That(() => producer.BeginTransaction()).Throws<FatalTransactionException>();

        var replacement = new InMemoryProducer<string, string>(cluster);
        var metadata = await replacement.ProduceAsync("orders", "k", "recovered");
        await Assert.That(metadata.Offset).IsEqualTo(0);
    }

    private static Task<RecordMetadata> ProduceToPartitionAsync(
        InMemoryProducer<string, string> producer,
        int partition) =>
        producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "orders",
            Partition = partition,
            Key = "k",
            Value = "v"
        }).AsTask();
}
