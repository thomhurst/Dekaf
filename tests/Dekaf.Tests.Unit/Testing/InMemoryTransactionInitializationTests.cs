using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryTransactionInitializationTests
{
    private const string NotInitialized =
        "Transactions not initialized. Call InitTransactionsAsync() before BeginTransaction().";

    [Test]
    public async Task BeginTransaction_RequiresSuccessfulInitializationOnEachInstance()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        var exception = Assert.Throws<InvalidOperationException>(() => producer.BeginTransaction());
        await Assert.That(exception!.Message).IsEqualTo(NotInitialized);

        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        var state = await transaction.PrepareAsync();
        await producer.CompletePreparedTransactionAsync(state, committed: true);

        await using var replacement = new InMemoryProducer<string, string>(cluster);
        var replacementException = Assert.Throws<InvalidOperationException>(() => replacement.BeginTransaction());
        await Assert.That(replacementException!.Message).IsEqualTo(NotInitialized);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InitTransactions_FailureLeavesUninitializedAndAllowsRetry(bool keepPreparedTransaction)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.InitializeTransactions),
            new KafkaTimeoutException("coordinator unavailable"));

        await Assert.That(async () => await producer.InitTransactionsAsync(keepPreparedTransaction))
            .Throws<KafkaTimeoutException>();
        var exception = Assert.Throws<InvalidOperationException>(() => producer.BeginTransaction());
        await Assert.That(exception!.Message).IsEqualTo(NotInitialized);

        await producer.InitTransactionsAsync(keepPreparedTransaction);
        await using var transaction = producer.BeginTransaction();
        await transaction.AbortAsync();
    }

    [Test]
    public async Task InitTransactions_PausedThenCanceledDoesNotInitialize()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        var barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.InitializeTransactions));
        using var cancellation = new CancellationTokenSource();
        var initialization = producer.InitTransactionsAsync(cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        await Assert.That(() => producer.BeginTransaction()).Throws<InvalidOperationException>();
        cancellation.Cancel();
        await Assert.That(async () => await initialization).Throws<OperationCanceledException>();
        barrier.Release();
        var exception = Assert.Throws<InvalidOperationException>(() => producer.BeginTransaction());
        await Assert.That(exception!.Message).IsEqualTo(NotInitialized);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
    }

    [Test]
    public async Task InitTransactions_PreCanceledDoesNotInitialize()
    {
        await using var producer = new InMemoryProducer<string, string>(new InMemoryKafkaCluster());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(async () => await producer.InitTransactionsAsync(cancellation.Token))
            .Throws<OperationCanceledException>();
        var exception = Assert.Throws<InvalidOperationException>(() => producer.BeginTransaction());
        await Assert.That(exception!.Message).IsEqualTo(NotInitialized);
    }

    [Test]
    public async Task InitTransactions_FailedReinitializationDoesNotForgetSuccessfulInitialization()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.InitializeTransactions),
            new KafkaTimeoutException("coordinator unavailable"));
        await Assert.That(async () => await producer.InitTransactionsAsync()).Throws<KafkaTimeoutException>();
        await Assert.That(async () => await producer.ProduceAsync("orders", "k", "outside"))
            .Throws<TransactionException>();
        await using var transaction = producer.BeginTransaction();
        await transaction.AbortAsync();
    }

    [Test]
    public async Task CompletePreparedTransaction_RequiresInitialization()
    {
        var cluster = new InMemoryKafkaCluster();
        var options = new InMemoryProducerOptions { TransactionalId = "writer" };
        await using var original = new InMemoryProducer<string, string>(cluster, options);
        await original.InitTransactionsAsync();
        await using var transaction = original.BeginTransaction();
        var state = await transaction.PrepareAsync();
        await using var replacement = new InMemoryProducer<string, string>(cluster, options);
        var exception = await Assert.ThrowsAsync<TransactionException>(() =>
            replacement.CompletePreparedTransactionAsync(state, committed: true).AsTask());
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidTxnState);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await replacement.CompletePreparedTransactionAsync(state, committed: true);
    }

    [Test]
    public async Task InitTransactions_FailedPreparedRecoveryDoesNotInitialize()
    {
        var cluster = new InMemoryKafkaCluster();
        var options = new InMemoryProducerOptions { TransactionalId = "writer" };
        await using var original = new InMemoryProducer<string, string>(cluster, options);
        await original.InitTransactionsAsync();
        await using var transaction = original.BeginTransaction();
        var state = await transaction.PrepareAsync();
        await original.DisposeAsync();
        await using var replacement = new InMemoryProducer<string, string>(cluster, options);
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.AbortTransaction),
            new KafkaTimeoutException("abort unavailable"));

        await Assert.That(async () => await replacement.InitTransactionsAsync()).Throws<KafkaTimeoutException>();
        var exception = Assert.Throws<InvalidOperationException>(() => replacement.BeginTransaction());
        await Assert.That(exception!.Message).IsEqualTo(NotInitialized);
        var completionException = await Assert.ThrowsAsync<TransactionException>(() =>
            replacement.CompletePreparedTransactionAsync(state, committed: true).AsTask());
        await Assert.That(completionException!.ErrorCode).IsEqualTo(ErrorCode.InvalidTxnState);

        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await replacement.CompletePreparedTransactionAsync(state, committed: true);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Produce_TransactionalModeRejectsSendsWithoutTransaction(bool explicitIdentity)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var producer = new InMemoryProducer<string, string>(cluster,
            new InMemoryProducerOptions { TransactionalId = explicitIdentity ? "writer" : null });
        if (explicitIdentity)
            await AssertProducerSendsRejectedAsync(producer, cluster);
        await producer.InitTransactionsAsync();
        await AssertProducerSendsRejectedAsync(producer, cluster);
        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.ProduceAsync("orders", "k", "committed");
            await transaction.CommitAsync();
        }
        await AssertProducerSendsRejectedAsync(producer, cluster);
        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.AbortAsync();
        }
        await AssertProducerSendsRejectedAsync(producer, cluster);
    }

    [Test]
    public async Task Produce_PausedCommitRejectsSends()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync("orders", "k", "committed");
        var barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.CommitTransaction));
        var commit = transaction.CommitAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        try
        {
            await AssertProducerSendsRejectedAsync(producer, cluster);
        }
        finally
        {
            barrier.Release();
        }
        await commit;
    }

    private static async Task AssertProducerSendsRejectedAsync(
        InMemoryProducer<string, string> producer, InMemoryKafkaCluster cluster)
    {
        var before = cluster.GetWatermarks(new TopicPartition("orders", 0)).High;
        var message = new ProducerMessage<string, string> { Topic = "orders", Key = "k", Value = "outside" };
        var exception = await Assert.ThrowsAsync<TransactionException>(() => producer.ProduceAsync(message).AsTask());
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidTxnState);
        await Assert.That(async () => await producer.ProduceAsync("orders", "k", "outside"))
            .Throws<TransactionException>();
        await Assert.That(async () => await producer.ProduceAllAsync([message])).Throws<TransactionException>();
        await Assert.That(async () => await producer.ProduceAllAsync("orders", [("k", "outside")]))
            .Throws<TransactionException>();
        await Assert.That(async () => await producer.FireAsync(message)).Throws<TransactionException>();
        await Assert.That(async () => await producer.FireAsync("orders", "k", "outside")).Throws<TransactionException>();
        Exception? callbackError = null;
        var callbacks = 0;
        await producer.FireAsync(message, (_, error) =>
        {
            callbackError = error;
            callbacks++;
        });
        await Assert.That(callbacks).IsEqualTo(1);
        await Assert.That(callbackError).IsTypeOf<TransactionException>();
        await Assert.That(((TransactionException)callbackError!).ErrorCode).IsEqualTo(ErrorCode.InvalidTxnState);
        await Assert.That(cluster.GetWatermarks(new TopicPartition("orders", 0)).High)
            .IsEqualTo(before);
    }
}
