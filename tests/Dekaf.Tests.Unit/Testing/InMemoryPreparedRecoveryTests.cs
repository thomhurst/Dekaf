using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryPreparedRecoveryTests
{
    private static readonly InMemoryProducerOptions Writer = new() { TransactionalId = "writer" };

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task InitWithoutKeepPrepared_AbortsAndUnblocksReadCommitted(bool useDefaultOverload)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var original = new InMemoryProducer<string, string>(cluster, Writer);
        await using var transaction = original.BeginTransaction();
        _ = await transaction.ProduceAsync("orders", "k", "aborted");
        await transaction.SendOffsetsToTransactionAsync([new TopicPartitionOffset("orders", 0, 17)], "billing");
        var prepared = await transaction.PrepareAsync();
        await original.DisposeAsync();
        var partition = new TopicPartition("orders", 0);
        await Assert.That(cluster.TryRead(partition, 0, out _, out var blocked)).IsFalse();
        await Assert.That(blocked).IsTrue();

        await using var replacement = new InMemoryProducer<string, string>(cluster, Writer);
        if (useDefaultOverload)
            await replacement.InitTransactionsAsync();
        else
            await replacement.InitTransactionsAsync(keepPreparedTransaction: false);

        await using var next = replacement.BeginTransaction();
        _ = await next.ProduceAsync("orders", "k", "committed");
        await next.CommitAsync();
        await using var consumer = new InMemoryConsumer<string, string>(cluster, new InMemoryConsumerOptions
        {
            IsolationLevel = IsolationLevel.ReadCommitted,
            AutoOffsetReset = AutoOffsetReset.Earliest
        });
        consumer.Assign([partition]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var records = consumer.ConsumeAsync(timeout.Token).GetAsyncEnumerator();
        await Assert.That(await records.MoveNextAsync()).IsTrue();
        await Assert.That(records.Current.Value).IsEqualTo("committed");
        await Assert.That(cluster.GetCommittedOffset("billing", partition)).IsNull();
        await AssertInvalidStateAsync(replacement, prepared);
    }

    [Test]
    public async Task Recovery_RejectsOtherIdentityAndKeepsOwnPendingTransaction()
    {
        var cluster = new InMemoryKafkaCluster();
        var otherWriter = new InMemoryProducerOptions { TransactionalId = "other-writer" };
        await using var first = new InMemoryProducer<string, string>(cluster, Writer);
        await using var second = new InMemoryProducer<string, string>(cluster, otherWriter);
        await using var firstTransaction = first.BeginTransaction();
        await using var secondTransaction = second.BeginTransaction();
        _ = await firstTransaction.ProduceAsync("orders", "k", "first");
        _ = await secondTransaction.ProduceAsync("other-orders", "k", "second");
        var firstState = await firstTransaction.PrepareAsync();
        var secondState = await secondTransaction.PrepareAsync();
        await first.DisposeAsync();
        await second.DisposeAsync();

        await using var replacement = new InMemoryProducer<string, string>(cluster, Writer);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await AssertInvalidStateAsync(replacement, secondState);
        await Assert.That(() => replacement.BeginTransaction()).Throws<InvalidOperationException>();
        await replacement.CompletePreparedTransactionAsync(firstState, committed: true);
        await Assert.That(cluster.ReadRecords("orders")).Count().IsEqualTo(1);
        await Assert.That(cluster.TryRead(new TopicPartition("other-orders", 0), 0, out _, out var blocked)).IsFalse();
        await Assert.That(blocked).IsTrue();

        await using var otherReplacement = new InMemoryProducer<string, string>(cluster, otherWriter);
        await otherReplacement.InitTransactionsAsync();
        await Assert.That(cluster.ReadRecords("other-orders")).IsEmpty();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Completion_AfterAdoptedHandleCompletes_CanCompleteNextPreparedTransaction(
        bool useReplacement, bool commitAdopted)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var original = new InMemoryProducer<string, string>(cluster, Writer);
        await using var transaction = original.BeginTransaction();
        _ = await transaction.ProduceAsync("original", "k", "first");
        _ = await transaction.PrepareAsync();
        await using var replacement = new InMemoryProducer<string, string>(cluster, Writer);
        var recovering = useReplacement ? replacement : original;
        await recovering.InitTransactionsAsync(keepPreparedTransaction: true);

        if (commitAdopted)
            await transaction.CommitAsync();
        else
            await transaction.AbortAsync();

        await using var next = recovering.BeginTransaction();
        _ = await next.ProduceAsync("next", "k", "second");
        var nextState = await next.PrepareAsync();
        await recovering.CompletePreparedTransactionAsync(nextState, committed: !commitAdopted);

        await Assert.That(cluster.ReadRecords("original")).Count().IsEqualTo(commitAdopted ? 1 : 0);
        await Assert.That(cluster.ReadRecords("next")).Count().IsEqualTo(commitAdopted ? 0 : 1);
        await using var final = recovering.BeginTransaction();
        await final.AbortAsync();
    }

    [Test]
    public async Task Recovery_RequiresAdoptionDuringInit()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var replacement = new InMemoryProducer<string, string>(cluster, Writer);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await using var original = new InMemoryProducer<string, string>(cluster, Writer);
        await using var transaction = original.BeginTransaction();
        var state = await transaction.PrepareAsync();
        await original.DisposeAsync();

        await AssertInvalidStateAsync(replacement, state);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await replacement.CompletePreparedTransactionAsync(state, committed: false);
        await AssertInvalidStateAsync(replacement, state);
        await using var next = replacement.BeginTransaction();
        await next.AbortAsync();
    }

    [Test]
    public async Task Recovery_WithoutIdentityCannotAdoptAnotherProducer()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var original = new InMemoryProducer<string, string>(cluster);
        await using var transaction = original.BeginTransaction();
        var state = await transaction.PrepareAsync();
        await using var replacement = new InMemoryProducer<string, string>(cluster);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await AssertInvalidStateAsync(replacement, state);
        await replacement.InitTransactionsAsync();
        await original.CompletePreparedTransactionAsync(state, committed: false);
    }

    [Test]
    public async Task Completion_RejectsMissingMismatchedAndEmptyState()
    {
        await using var producer = new InMemoryProducer<string, string>(new InMemoryKafkaCluster(), Writer);
        await AssertInvalidStateAsync(producer, new PreparedTransactionState(123, 0));
        await using var transaction = producer.BeginTransaction();
        await AssertInvalidStateAsync(producer, new PreparedTransactionState(123, 0));
        var state = await transaction.PrepareAsync();
        await AssertInvalidStateAsync(producer, new PreparedTransactionState(state.ProducerId, 1));
        await Assert.That(async () => await producer.CompletePreparedTransactionAsync(PreparedTransactionState.Empty, committed: true))
            .Throws<ArgumentException>();
        await producer.CompletePreparedTransactionAsync(state, committed: false);
    }

    [Test]
    public async Task Recovery_CancelledCompletionRemainsPendingAndCanRetry()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var original = new InMemoryProducer<string, string>(cluster, Writer);
        await using var transaction = original.BeginTransaction();
        var state = await transaction.PrepareAsync();
        await original.DisposeAsync();
        await using var replacement = new InMemoryProducer<string, string>(cluster, Writer);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        var barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.CommitTransaction));
        using var cancellation = new CancellationTokenSource();
        var completion = replacement.CompletePreparedTransactionAsync(state, true, cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        await cancellation.CancelAsync();
        await Assert.That(async () => await completion).Throws<OperationCanceledException>();
        await Assert.That(() => replacement.BeginTransaction()).Throws<InvalidOperationException>();
        await replacement.CompletePreparedTransactionAsync(state, committed: false);
        await using var next = replacement.BeginTransaction();
        await next.AbortAsync();
    }

    [Test]
    public async Task Init_FailurePreservesPreparedTransactionForRetry()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var original = new InMemoryProducer<string, string>(cluster, Writer);
        await using var transaction = original.BeginTransaction();
        var state = await transaction.PrepareAsync();
        await original.DisposeAsync();
        await using var replacement = new InMemoryProducer<string, string>(cluster, Writer);
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.InitializeTransactions),
            new InvalidOperationException("initialization failed"));

        await Assert.That(async () => await replacement.InitTransactionsAsync()).Throws<InvalidOperationException>();
        await AssertInvalidStateAsync(replacement, state);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await replacement.CompletePreparedTransactionAsync(state, committed: false);
    }

    [Test]
    public async Task Init_ClaimsLifecycleAndDisposalWaitsForAbort()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var original = new InMemoryProducer<string, string>(cluster, Writer);
        await using var transaction = original.BeginTransaction();
        _ = await transaction.ProduceAsync("orders", "k", "aborted");
        _ = await transaction.PrepareAsync();
        await original.DisposeAsync();
        await using var replacement = new InMemoryProducer<string, string>(cluster, Writer);
        var barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.AbortTransaction));
        var initialization = replacement.InitTransactionsAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();

        await Assert.That(() => replacement.BeginTransaction()).Throws<InvalidOperationException>();
        var disposal = replacement.DisposeAsync().AsTask();
        await Assert.That(disposal.IsCompleted).IsFalse();
        await Assert.That(barrier.Release()).IsTrue();
        await initialization;
        await disposal;
        await Assert.That(cluster.TryRead(new TopicPartition("orders", 0), 0, out _, out var blocked)).IsFalse();
        await Assert.That(blocked).IsFalse();
    }

    [Test]
    public async Task AddDekafInMemory_PreservesConfiguredProducerIdentity()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Writer);
        services.AddTransient(typeof(IKafkaProducer<,>), typeof(InMemoryProducer<,>));
        services.AddDekafInMemory();
        await using var provider = services.BuildServiceProvider();
        var original = provider.GetRequiredService<IKafkaProducer<string, string>>();
        await using var transaction = original.BeginTransaction();
        var state = await transaction.PrepareAsync();
        await original.DisposeAsync();

        var replacement = provider.GetRequiredService<IKafkaProducer<string, string>>();
        await Assert.That(replacement).IsNotSameReferenceAs(original);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await replacement.CompletePreparedTransactionAsync(state, committed: true);
        var exception = await Assert.ThrowsAsync<TransactionException>(() =>
            replacement.CompletePreparedTransactionAsync(state, committed: true).AsTask());
        await Assert.That(exception!.TransactionalId).IsEqualTo(Writer.TransactionalId);
    }

    private static async Task AssertInvalidStateAsync(InMemoryProducer<string, string> producer, PreparedTransactionState state)
    {
        var exception = await Assert.ThrowsAsync<TransactionException>(() =>
            producer.CompletePreparedTransactionAsync(state, committed: true).AsTask());
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidTxnState);
    }
}
