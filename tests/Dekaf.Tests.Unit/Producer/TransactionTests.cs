using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for transaction state validation in KafkaProducer.
/// These tests verify the state machine behavior without requiring a Kafka broker.
/// </summary>
public sealed class TransactionTests
{
    [Test]
    public async Task BeginTransaction_WithoutTransactionalId_Throws()
    {
        // Producer without TransactionalId cannot begin transactions
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        var act = () => producer.BeginTransaction();
        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task BeginTransaction_BeforeInit_Throws()
    {
        // Producer with TransactionalId but without InitTransactionsAsync
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id")
            .Build();

        var act = () => producer.BeginTransaction();
        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task BeginTransaction_InAbortableErrorState_Throws()
    {
        // A transaction that hit an abortable error must be aborted before a new one can start.
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id")
            .Build();

        ((KafkaProducer<string, string>)producer)._transactionState = TransactionState.AbortableError;

        var act = () => producer.BeginTransaction();
        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task BeginTransaction_InFatalErrorState_Throws()
    {
        // A producer in a fatal error state cannot start any further transactions.
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id")
            .Build();

        var kafkaProducer = (KafkaProducer<string, string>)producer;
        kafkaProducer._transactionState = TransactionState.FatalError;
        kafkaProducer._lastTransactionError = ErrorCode.ProducerFenced;

        var act = () => producer.BeginTransaction();
        var exception = await Assert.That(act).Throws<FatalTransactionException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ProducerFenced);
        await Assert.That(exception.TransactionalId).IsEqualTo("test-txn-id");
    }

    [Test]
    public async Task FatalErrorState_AllTransactionOperationsFailFast()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id")
            .Build();

        var kafkaProducer = (KafkaProducer<string, string>)producer;
        SetInstanceField(kafkaProducer, "_initialized", true);
        kafkaProducer._transactionState = TransactionState.FatalError;
        kafkaProducer._lastTransactionError = ErrorCode.ProducerFenced;

        await using var transaction = new Transaction<string, string>(kafkaProducer);
        var message = new ProducerMessage<string, string>
        {
            Topic = "test-topic",
            Key = "key",
            Value = "value"
        };

        await Assert.That(() => transaction.ProduceAsync(message).AsTask())
            .Throws<FatalTransactionException>();
        await Assert.That(() => transaction.ProduceAsync("test-topic", "key", "value").AsTask())
            .Throws<FatalTransactionException>();
        await Assert.That(() => transaction.SendOffsetsToTransactionAsync(
                [new TopicPartitionOffset("test-topic", 0, 1)], "test-group").AsTask())
            .Throws<FatalTransactionException>();
        await Assert.That(() => transaction.PrepareAsync().AsTask())
            .Throws<FatalTransactionException>();
        await Assert.That(() => transaction.CommitAsync().AsTask())
            .Throws<FatalTransactionException>();
        await Assert.That(() => transaction.AbortAsync().AsTask())
            .Throws<FatalTransactionException>();
        await Assert.That(() => producer.InitTransactionsAsync().AsTask())
            .Throws<FatalTransactionException>();
        await Assert.That(() => producer.BeginTransaction())
            .Throws<FatalTransactionException>();
    }

    [Test]
    public async Task DisposeAsync_WhenAbortIsFenced_PreservesFatalError()
    {
        var preparedState = new PreparedTransactionState(42, 5);
        await using var harness = BuildPreparedCompletionHarness(
            preparedState,
            currentProducerId: preparedState.ProducerId,
            currentProducerEpoch: preparedState.ProducerEpoch,
            endTxnError: ErrorCode.ProducerFenced);

        harness.Producer._transactionState = TransactionState.InTransaction;
        var transaction = new Transaction<string, string>(harness.Producer);

        await transaction.DisposeAsync();

        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(harness.Producer._lastTransactionError).IsEqualTo(ErrorCode.ProducerFenced);

        var exception = await Assert.That(() => harness.Producer.BeginTransaction())
            .Throws<FatalTransactionException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ProducerFenced);
    }

    [Test]
    public async Task DisposeAsync_WhenAbortIsRejected_ReturnsProducerToReady()
    {
        var preparedState = new PreparedTransactionState(42, 5);
        await using var harness = BuildPreparedCompletionHarness(
            preparedState,
            currentProducerId: preparedState.ProducerId,
            currentProducerEpoch: preparedState.ProducerEpoch,
            endTxnError: ErrorCode.InvalidTxnState);

        harness.Producer._transactionState = TransactionState.InTransaction;
        var transaction = new Transaction<string, string>(harness.Producer);

        await transaction.DisposeAsync();

        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.Ready);
        await Assert.That(harness.Producer._lastTransactionError).IsEqualTo(ErrorCode.InvalidTxnState);
    }

    [Test]
    [Timeout(5_000)]
    public async Task DisposeAsync_WhenAbortTimesOutBeforeWrite_PreservesAbortableError(
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 42,
            currentProducerEpoch: 5,
            endTxnWaitsBeforeWriteForCancellation: true,
            maxBlockMs: 1000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        var transaction = new Transaction<string, string>(harness.Producer);

        await transaction.DisposeAsync().AsTask().WaitAsync(cancellationToken);

        await Assert.That(harness.EndTxnRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<InvalidOperationException>();
    }

    private static KafkaProducer<string, string> BuildTransactionalProducer(
        TransactionState state,
        long producerId = 42,
        short producerEpoch = 5)
    {
        var producer = (KafkaProducer<string, string>)Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id")
            .Build();
        SetInstanceField(producer, "_initialized", true);
        SetInstanceField(producer, "_producerId", producerId);
        SetInstanceField(producer, "_producerEpoch", producerEpoch);
        producer._transactionState = state;
        return producer;
    }

    [Test]
    [Arguments(ErrorCode.ProducerFenced, (int)TransactionState.InTransaction)]
    [Arguments(ErrorCode.ProducerFenced, (int)TransactionState.Ready)]
    [Arguments(ErrorCode.ProducerFenced, (int)TransactionState.AbortableError)]
    [Arguments(ErrorCode.TransactionalIdAuthorizationFailed, (int)TransactionState.InTransaction)]
    [Arguments(ErrorCode.ClusterAuthorizationFailed, (int)TransactionState.CommittingTransaction)]
    public async Task TransactionalBatchFailure_FatalErrorForCurrentProducer_MovesToFatalError(
        ErrorCode errorCode,
        int initialStateValue)
    {
        var initialState = (TransactionState)initialStateValue;
        await using var producer = BuildTransactionalProducer(initialState);
        var transaction = new Transaction<string, string>(producer);

        producer.OnTransactionalBatchFailed(42, 5, errorCode);

        await Assert.That(producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(producer._lastTransactionError).IsEqualTo(errorCode);
        var produceException = await Assert.That(
                () => transaction.ProduceAsync("test-topic", "key", "value").AsTask())
            .Throws<FatalTransactionException>();
        await Assert.That(produceException!.ErrorCode).IsEqualTo(errorCode);
        await Assert.That(() => transaction.CommitAsync().AsTask())
            .Throws<FatalTransactionException>();
    }

    [Test]
    [Arguments(ErrorCode.InvalidProducerEpoch, (int)TransactionState.InTransaction)]
    [Arguments(ErrorCode.OutOfOrderSequenceNumber, (int)TransactionState.InTransaction)]
    [Arguments(ErrorCode.UnknownProducerId, (int)TransactionState.InTransaction)]
    [Arguments(ErrorCode.MessageTooLarge, (int)TransactionState.InTransaction)]
    [Arguments(ErrorCode.RequestTimedOut, (int)TransactionState.InTransaction)]
    [Arguments(ErrorCode.OutOfOrderSequenceNumber, (int)TransactionState.CommittingTransaction)]
    public async Task TransactionalBatchFailure_OtherErrorInOpenTransaction_MovesToAbortableError(
        ErrorCode errorCode,
        int initialStateValue)
    {
        var initialState = (TransactionState)initialStateValue;
        await using var producer = BuildTransactionalProducer(initialState);

        producer.OnTransactionalBatchFailed(42, 5, errorCode);

        await Assert.That(producer._transactionState).IsEqualTo(TransactionState.AbortableError);
        await Assert.That(producer._lastTransactionError).IsEqualTo(errorCode);

        // Produce and a later CommitAsync must refuse until the caller aborts.
        var transaction = new Transaction<string, string>(producer);
        var produceException = await Assert.That(
                () => transaction.ProduceAsync("test-topic", "key", "value").AsTask())
            .Throws<AbortableTransactionException>();
        await Assert.That(produceException!.ErrorCode).IsEqualTo(errorCode);
        var commitException = await Assert.That(() => transaction.CommitAsync().AsTask())
            .Throws<AbortableTransactionException>();
        await Assert.That(commitException!.ErrorCode).IsEqualTo(errorCode);
    }

    /// <summary>
    /// A send loop can fence the producer while the caller's thread makes the transaction
    /// abortable (a partition-enrollment error found by the pre-commit flush check, a flush
    /// timeout, an abortable control-plane response). The abortable transition must not
    /// downgrade FatalError: the caller could then abort and reuse a fenced producer.
    /// </summary>
    [Test]
    public async Task MarkTransactionAbortable_AfterConcurrentFatalBatch_KeepsFatalError()
    {
        await using var producer = BuildTransactionalProducer(TransactionState.InTransaction);
        producer.OnTransactionalBatchFailed(42, 5, ErrorCode.ProducerFenced);

        var marked = producer.MarkTransactionAbortable(ErrorCode.NetworkException);

        await Assert.That(marked).IsFalse();
        await Assert.That(producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(producer._lastTransactionError).IsEqualTo(ErrorCode.ProducerFenced);
        await Assert.That(() => producer.ThrowIfTransactionFailedDuringFlush("Cannot commit transaction"))
            .Throws<FatalTransactionException>();
    }

    /// <summary>
    /// A send loop fences the producer while the abort's EndTxn is answered with an abortable error.
    /// The abortable transition is refused, and the caller must see the fence: a fatal exception
    /// carrying ProducerFenced, not an abortable one that overwrites the fatal error code.
    /// </summary>
    [Test]
    public async Task AbortAsync_AbortableEndTxnErrorAfterConcurrentFence_ThrowsFatalWithTheFenceCode()
    {
        var preparedState = new PreparedTransactionState(42, 5);
        await using var harness = BuildPreparedCompletionHarness(
            preparedState,
            currentProducerId: preparedState.ProducerId,
            currentProducerEpoch: preparedState.ProducerEpoch,
            endTxnError: ErrorCode.TransactionAbortable);
        harness.Producer._transactionState = TransactionState.InTransaction;
        var transaction = new Transaction<string, string>(harness.Producer);
        harness.BeforeEndTxnResponse = () =>
            harness.Producer.OnTransactionalBatchFailed(42, 5, ErrorCode.ProducerFenced);

        var exception = await Assert.That(() => transaction.AbortAsync().AsTask())
            .Throws<FatalTransactionException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ProducerFenced);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(harness.Producer._lastTransactionError).IsEqualTo(ErrorCode.ProducerFenced);
    }

    /// <summary>
    /// A fatal control-plane answer (here an EndTxn whose outcome is unknown) races a caller's
    /// abortable transition. Whatever the interleaving, the producer must end fatal: the abortable
    /// transition must never overwrite a fatal state written concurrently.
    /// </summary>
    [Test]
    [Timeout(60_000)]
    public async Task FatalTransition_RacingAbortableTransition_AlwaysEndsFatal(CancellationToken cancellationToken)
    {
        await using var producer = BuildTransactionalProducer(TransactionState.InTransaction);
        var preserveTimeoutState = typeof(KafkaProducer<string, string>).GetMethod(
            "PreserveEndTransactionTimeoutState",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var markFatal = preserveTimeoutState.CreateDelegate<Action<bool>>(producer);

        var lost = RaceTransactionTransitions(
            reset: () =>
            {
                producer._lastTransactionError = ErrorCode.None;
                producer._transactionState = TransactionState.InTransaction;
            },
            first: () => producer.MarkTransactionAbortable(ErrorCode.NetworkException),
            second: () => markFatal(true),
            isValid: () => producer._transactionState == TransactionState.FatalError
                && producer._lastTransactionError == ErrorCode.RequestTimedOut,
            cancellationToken);

        await Assert.That(lost).IsEqualTo(0);
    }

    /// <summary>
    /// A successful abort finalizes the state while a send loop reports a fenced batch stamped with
    /// the transaction's identity. The finalizer must not read AbortingTransaction, lose the race to
    /// the fatal report, and then overwrite FatalError with Ready.
    /// </summary>
    [Test]
    [Timeout(60_000)]
    public async Task FinalizeAfterAbort_RacingFencedBatchReport_AlwaysEndsFatal(CancellationToken cancellationToken)
    {
        await using var producer = BuildTransactionalProducer(TransactionState.AbortingTransaction);

        var lost = RaceTransactionTransitions(
            reset: () =>
            {
                producer._lastTransactionError = ErrorCode.None;
                producer._transactionState = TransactionState.AbortingTransaction;
            },
            first: () => producer.FinalizeCompletedTransactionState(preserveAbortableError: false),
            second: () => producer.OnTransactionalBatchFailed(42, 5, ErrorCode.ProducerFenced),
            isValid: () => producer._transactionState == TransactionState.FatalError,
            cancellationToken);

        await Assert.That(lost).IsEqualTo(0);
    }

    /// <summary>
    /// Runs <paramref name="first"/> and <paramref name="second"/> on two threads released together,
    /// many times, with a varying spin offset so the two sweep across each other's check-then-write
    /// windows. Returns the number of rounds that ended in a state <paramref name="isValid"/> rejects.
    /// </summary>
    private static int RaceTransactionTransitions(
        Action reset,
        Action first,
        Action second,
        Func<bool> isValid,
        CancellationToken cancellationToken)
    {
        const int rounds = 20_000;
        var lost = 0;
        var round = 0;
        var secondDone = 0;

        var secondThread = new Thread(() =>
        {
            for (var i = 1; i <= rounds; i++)
            {
                while (Volatile.Read(ref round) < i)
                    Thread.SpinWait(1);
                Thread.SpinWait((i / 16) % 16);
                second();
                Volatile.Write(ref secondDone, i);
            }
        }) { IsBackground = true };
        secondThread.Start();

        for (var i = 1; i <= rounds && !cancellationToken.IsCancellationRequested; i++)
        {
            reset();
            Volatile.Write(ref round, i);
            Thread.SpinWait(i % 16);
            first();
            while (Volatile.Read(ref secondDone) < i)
                Thread.SpinWait(1);
            if (!isValid())
                lost++;
        }

        cancellationToken.ThrowIfCancellationRequested();
        secondThread.Join();
        return lost;
    }

    /// <summary>
    /// The flush that precedes EndTxn(commit) runs in CommittingTransaction: a batch failed during
    /// it must stop the commit instead of committing the transaction without its records.
    /// </summary>
    [Test]
    public async Task TransactionalBatchFailure_DuringCommitFlush_CommitThrowsAbortable()
    {
        await using var producer = BuildTransactionalProducer(TransactionState.CommittingTransaction);

        producer.OnTransactionalBatchFailed(42, 5, ErrorCode.OutOfOrderSequenceNumber);

        var exception = await Assert.That(
                () => producer.ThrowIfTransactionFailedDuringFlush("Cannot commit transaction"))
            .Throws<AbortableTransactionException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.OutOfOrderSequenceNumber);
    }

    /// <summary>
    /// A batch stamped with an earlier producer identity belongs to a transaction that was already
    /// aborted (abort bumps the epoch under both TV1 and TV2): its rejection says nothing about the
    /// current transaction or the current epoch.
    /// </summary>
    [Test]
    [Arguments(ErrorCode.ProducerFenced, 42L, (short)4)]
    [Arguments(ErrorCode.InvalidProducerEpoch, 42L, (short)4)]
    [Arguments(ErrorCode.OutOfOrderSequenceNumber, 41L, (short)5)]
    public async Task TransactionalBatchFailure_FromEarlierProducerIdentity_IsIgnored(
        ErrorCode errorCode,
        long batchProducerId,
        short batchEpoch)
    {
        await using var producer = BuildTransactionalProducer(TransactionState.InTransaction);

        producer.OnTransactionalBatchFailed(batchProducerId, batchEpoch, errorCode);

        await Assert.That(producer._transactionState).IsEqualTo(TransactionState.InTransaction);
        await Assert.That(producer._lastTransactionError).IsEqualTo(ErrorCode.None);
    }

    /// <summary>
    /// Outside an open transaction there is nothing to abort; a late abortable failure must not
    /// block the next BeginTransaction, and it must not replace a fatal error.
    /// </summary>
    [Test]
    [Arguments((int)TransactionState.Ready)]
    [Arguments((int)TransactionState.AbortingTransaction)]
    [Arguments((int)TransactionState.FatalError)]
    public async Task TransactionalBatchFailure_AbortableErrorOutsideOpenTransaction_LeavesStateAlone(
        int initialStateValue)
    {
        var initialState = (TransactionState)initialStateValue;
        await using var producer = BuildTransactionalProducer(initialState);

        producer.OnTransactionalBatchFailed(42, 5, ErrorCode.OutOfOrderSequenceNumber);

        await Assert.That(producer._transactionState).IsEqualTo(initialState);
    }

    /// <summary>
    /// A send loop can report a failed batch just as the abort starts (it read InTransaction before
    /// the abort moved the state on). The completed abort resolves that error too; keeping it would
    /// refuse the next BeginTransaction with nothing left to abort.
    /// </summary>
    [Test]
    public async Task AbortAsync_BatchFailureReportedWhileAborting_IsResolvedByTheAbort()
    {
        var preparedState = new PreparedTransactionState(42, 5);
        await using var harness = BuildPreparedCompletionHarness(
            preparedState,
            currentProducerId: preparedState.ProducerId,
            currentProducerEpoch: preparedState.ProducerEpoch);
        harness.Producer._transactionState = TransactionState.InTransaction;
        var transaction = new Transaction<string, string>(harness.Producer);
        harness.BeforeEndTxnResponse = () =>
        {
            harness.Producer._lastTransactionError = ErrorCode.OutOfOrderSequenceNumber;
            harness.Producer._transactionState = TransactionState.AbortableError;
        };

        await transaction.AbortAsync();

        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.Ready);
        await using var next = harness.Producer.BeginTransaction();
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.InTransaction);
        harness.Producer._transactionState = TransactionState.Ready;
    }

    [Test]
    public async Task InitTransactionsAsync_WithoutTransactionalId_Throws()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        var act = () => producer.InitTransactionsAsync().AsTask();
        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task PurgeAsync_InTransaction_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id")
            .Build();

        var kafkaProducer = (KafkaProducer<string, string>)producer;
        SetInstanceField(kafkaProducer, "_initialized", true);
        kafkaProducer._transactionState = TransactionState.InTransaction;

        await Assert.That(async () =>
        {
            await producer.PurgeAsync(PurgeOptions.All);
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task TransactionState_Enum_HasExpectedValues()
    {
        // Verify enum values exist and are distinct
        var values = Enum.GetValues<TransactionState>();
        await Assert.That(values).Count().IsEqualTo(8);
        await Assert.That(values).Contains(TransactionState.Uninitialized);
        await Assert.That(values).Contains(TransactionState.Ready);
        await Assert.That(values).Contains(TransactionState.InTransaction);
        await Assert.That(values).Contains(TransactionState.PreparedTransaction);
        await Assert.That(values).Contains(TransactionState.CommittingTransaction);
        await Assert.That(values).Contains(TransactionState.AbortingTransaction);
        await Assert.That(values).Contains(TransactionState.AbortableError);
        await Assert.That(values).Contains(TransactionState.FatalError);
    }

    [Test]
    public async Task TransactionState_ValuesAreDistinct()
    {
        var values = Enum.GetValues<TransactionState>();
        var distinctValues = values.Distinct().ToArray();
        await Assert.That(distinctValues).Count().IsEqualTo(values.Length);
    }

    [Test]
    public async Task ProducerOptions_TransactionalId_DefaultsToNull()
    {
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        await Assert.That(options.TransactionalId).IsNull();
    }

    [Test]
    public async Task ProducerOptions_EnableTwoPhaseCommit_DefaultsToFalse()
    {
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        await Assert.That(options.EnableTwoPhaseCommit).IsFalse();
    }

    [Test]
    public async Task ProducerOptions_TransactionTimeoutMs_DefaultsTo60000()
    {
        var options = new ProducerOptions { BootstrapServers = ["localhost:9092"] };
        await Assert.That(options.TransactionTimeoutMs).IsEqualTo(60000);
    }

    [Test]
    public async Task ProducerOptions_TransactionalId_CanBeSet()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            TransactionalId = "my-txn-id"
        };
        await Assert.That(options.TransactionalId).IsEqualTo("my-txn-id");
    }

    [Test]
    public async Task ProducerOptions_EnableTwoPhaseCommit_CanBeSet()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            EnableTwoPhaseCommit = true
        };
        await Assert.That(options.EnableTwoPhaseCommit).IsTrue();
    }

    [Test]
    public async Task ProducerOptions_TransactionTimeoutMs_CanBeSet()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            TransactionTimeoutMs = 30000
        };
        await Assert.That(options.TransactionTimeoutMs).IsEqualTo(30000);
    }

    [Test]
    public async Task WithTransactionalId_ReturnsBuilderForChaining()
    {
        var originalBuilder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092");

        var returnedBuilder = originalBuilder.WithTransactionalId("test-txn-id");

        await Assert.That(returnedBuilder).IsSameReferenceAs(originalBuilder);
    }

    [Test]
    public async Task WithTransactionalId_BuildsProducer()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id")
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task WithTwoPhaseCommit_ReturnsBuilderForChaining()
    {
        var originalBuilder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id");

        var returnedBuilder = originalBuilder.WithTwoPhaseCommit();

        await Assert.That(returnedBuilder).IsSameReferenceAs(originalBuilder);
    }

    [Test]
    public async Task Build_WithTwoPhaseCommitWithoutTransactionalId_ThrowsInvalidOperationException()
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTwoPhaseCommit();

        await Assert.That(() => builder.Build()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task WithTransactionalId_CanChainWithAcks()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id")
            .WithAcks(Acks.All)
            .Build();

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task ITransaction_Interface_HasExpectedMethods()
    {
        // Verify the interface shape at compile time by checking method existence
        var methods = typeof(ITransaction<string, string>).GetMethods();
        var methodNames = methods.Select(m => m.Name).ToArray();

        await Assert.That(methodNames).Contains("ProduceAsync");
        await Assert.That(methodNames).Contains("CommitAsync");
        await Assert.That(methodNames).Contains("PrepareAsync");
        await Assert.That(methodNames).Contains("AbortAsync");
        await Assert.That(methodNames).Contains("SendOffsetsToTransactionAsync");
    }

    [Test]
    public async Task IKafkaProducer_Interface_HasExpectedTransactionMethods()
    {
        var methods = typeof(IKafkaProducer<string, string>).GetMethods();
        var methodNames = methods.Select(m => m.Name).ToArray();

        await Assert.That(methodNames).Contains("BeginTransaction");
        await Assert.That(methodNames).Contains("InitTransactionsAsync");
        await Assert.That(methodNames).Contains("CompletePreparedTransactionAsync");

        var completePreparedMethod = methods.Single(m => m.Name == "CompletePreparedTransactionAsync");
        await Assert.That(completePreparedMethod.GetParameters().Any(p =>
            p.Name == "committed" && p.ParameterType == typeof(bool))).IsTrue();
    }

    [Test]
    public async Task PreparedTransactionState_ToStringAndParse_RoundTrips()
    {
        var state = new PreparedTransactionState(42, 7);
        var text = state.ToString();
        var parsed = PreparedTransactionState.Parse(text);

        await Assert.That(text).IsEqualTo("42:7");
        await Assert.That(parsed).IsEqualTo(state);
        await Assert.That(parsed.HasTransaction).IsTrue();
    }

    [Test]
    public async Task PreparedTransactionState_Empty_HasNoTransaction()
    {
        var state = PreparedTransactionState.Empty;

        await Assert.That(state.HasTransaction).IsFalse();
        await Assert.That(state.ToString()).IsEqualTo(string.Empty);
        await Assert.That(PreparedTransactionState.Parse(string.Empty)).IsEqualTo(state);
    }

    [Test]
    public async Task PrepareAsync_WithTwoPhaseCommit_SetsPreparedState()
    {
        await using var producer = BuildInitializedTransactionalProducer(enableTwoPhaseCommit: true);
        await using var transaction = producer.BeginTransaction();

        var state = await transaction.PrepareAsync();

        await Assert.That(state).IsEqualTo(new PreparedTransactionState(42, 5));
        await Assert.That(producer._transactionState).IsEqualTo(TransactionState.PreparedTransaction);
        await Assert.That(producer._preparedTransactionState).IsEqualTo(state);
    }

    [Test]
    public async Task PrepareAsync_WithoutTwoPhaseCommit_ThrowsTransactionException()
    {
        await using var producer = BuildInitializedTransactionalProducer(enableTwoPhaseCommit: false);
        var transaction = producer.BeginTransaction();

        try
        {
            await Assert.That(async () =>
            {
                await transaction.PrepareAsync();
            }).Throws<TransactionException>();
        }
        finally
        {
            producer._transactionState = TransactionState.Ready;
            await transaction.DisposeAsync();
        }
    }

    [Test]
    public async Task BeginTransaction_WithPreparedTransaction_ThrowsInvalidOperationException()
    {
        await using var producer = BuildInitializedTransactionalProducer(enableTwoPhaseCommit: true);
        await using var transaction = producer.BeginTransaction();
        await transaction.PrepareAsync();

        await Assert.That(() => producer.BeginTransaction()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ProduceAsync_AfterPrepare_ThrowsInvalidOperationException()
    {
        await using var producer = BuildInitializedTransactionalProducer(enableTwoPhaseCommit: true);
        await using var transaction = producer.BeginTransaction();
        await transaction.PrepareAsync();

        await Assert.That(async () =>
        {
            await transaction.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "orders",
                Value = "value"
            });
        }).Throws<InvalidOperationException>();
        await Assert.That(async () =>
        {
            await transaction.ProduceAsync("orders", key: null, "value");
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task SendOffsetsToTransactionAsync_AfterPrepare_ThrowsInvalidOperationException()
    {
        await using var producer = BuildInitializedTransactionalProducer(enableTwoPhaseCommit: true);
        await using var transaction = producer.BeginTransaction();
        await transaction.PrepareAsync();

        await Assert.That(async () =>
        {
            await transaction.SendOffsetsToTransactionAsync(
                [new TopicPartitionOffset("orders", 0, 10)],
                "group-1");
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task CompletePreparedTransactionAsync_WithEmptyState_ThrowsArgumentException()
    {
        await using var producer = BuildInitializedTransactionalProducer(enableTwoPhaseCommit: true);

        await Assert.That(async () =>
        {
            await producer.CompletePreparedTransactionAsync(PreparedTransactionState.Empty, committed: true);
        }).Throws<ArgumentException>();
    }

    [Test]
    public async Task CompletePreparedTransactionAsync_Commit_UsesPreparedTransactionProducerIdentity()
    {
        var preparedState = new PreparedTransactionState(1001, 4);
        await using var harness = BuildPreparedCompletionHarness(
            preparedState,
            currentProducerId: 2002,
            currentProducerEpoch: 9);

        await harness.Producer.CompletePreparedTransactionAsync(preparedState, committed: true);

        var request = harness.CapturedRequest;
        await Assert.That(request.ProducerId).IsEqualTo(preparedState.ProducerId);
        await Assert.That(request.ProducerEpoch).IsEqualTo(preparedState.ProducerEpoch);
        await Assert.That(request.Committed).IsTrue();
        await Assert.That(GetInstanceField<long>(harness.Producer, "_producerId")).IsEqualTo(2002);
        await Assert.That(GetInstanceField<short>(harness.Producer, "_producerEpoch")).IsEqualTo((short)9);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.Ready);
        await Assert.That(harness.Producer._preparedTransactionState).IsEqualTo(PreparedTransactionState.Empty);
    }

    [Test]
    public async Task CompletePreparedTransactionAsync_HoldsConnectionLeaseDuringRequest()
    {
        var preparedState = new PreparedTransactionState(1001, 4);
        await using var harness = BuildPreparedCompletionHarness(
            preparedState,
            currentProducerId: 2002,
            currentProducerEpoch: 9);

        await harness.Producer.CompletePreparedTransactionAsync(preparedState, committed: true);

        await Assert.That(harness.LeaseCountDuringRequest).IsEqualTo(1);
        await Assert.That(harness.LeaseCount).IsEqualTo(0);
    }

    [Test]
    public async Task CommitAsync_FeatureDriftPreservesFatalState()
    {
        var preparedState = new PreparedTransactionState(42, 5);
        await using var harness = BuildPreparedCompletionHarness(
            preparedState,
            currentProducerId: preparedState.ProducerId,
            currentProducerEpoch: preparedState.ProducerEpoch);
        harness.Producer._preparedTransactionState = PreparedTransactionState.Empty;
        harness.Producer._transactionState = TransactionState.InTransaction;
        await using var transaction = new Transaction<string, string>(harness.Producer);
        SetFinalizedTransactionVersion(harness.Producer, 2);

        var exception = await Assert.That(() => transaction.CommitAsync().AsTask())
            .Throws<FatalTransactionException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnsupportedVersion);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<FatalTransactionException>();
    }

    [Test]
    public async Task CommitAfterRequestWrittenAsync_CallbackFailure_AbandonsResponse()
    {
        var preparedState = new PreparedTransactionState(1001, 4);
        await using var harness = BuildPreparedCompletionHarness(
            preparedState,
            currentProducerId: 2002,
            currentProducerEpoch: 9);
        harness.Producer._transactionState = TransactionState.InTransaction;
        await using var transaction = new Transaction<string, string>(harness.Producer);
        var callbackFailure = new InvalidOperationException("callback failed");

        var exception = await Assert.That(() => transaction.CommitAfterRequestWrittenAsync(
                () => ValueTask.FromException(callbackFailure)).AsTask())
            .Throws<InvalidOperationException>();

        await Assert.That(exception).IsSameReferenceAs(callbackFailure);
        await Assert.That(harness.PipelinedResponseAbandonCalls).IsEqualTo(1);
    }

    [Test]
    public async Task ReinitializeProducerIdAsync_HoldsConnectionLeaseDuringRequest()
    {
        var preparedState = new PreparedTransactionState(1001, 4);
        await using var harness = BuildPreparedCompletionHarness(
            preparedState,
            currentProducerId: 2002,
            currentProducerEpoch: 9);

        await harness.Producer.ReinitializeProducerIdAsync(CancellationToken.None);

        await Assert.That(harness.LeaseCountDuringRequest).IsEqualTo(1);
        await Assert.That(harness.LeaseCount).IsEqualTo(0);
    }

    [Test]
    public async Task InitTransactionsAsync_RetriesBeyondPreviousAttemptLimits()
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            coordinatorRetriableFailuresBeforeSuccess: 5,
            initProducerIdRetriableFailuresBeforeSuccess: 10,
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.Uninitialized;

        await harness.Producer.InitTransactionsAsync();

        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(6);
        await Assert.That(harness.InitProducerIdRequests).IsEqualTo(11);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.Ready);
    }

    [Test]
    public async Task InitTransactionsAsync_EmptyCoordinatorResponse_RetriesWithinDeadline()
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            emptyCoordinatorResponsesBeforeSuccess: 1,
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.Uninitialized;

        await harness.Producer.InitTransactionsAsync();

        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(2);
        await Assert.That(harness.InitProducerIdRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.Ready);
    }

    [Test]
    [Timeout(5_000)]
    public async Task InitTransactionsAsync_SharedDeadlineSpansCoordinatorAndProducerId(
        CancellationToken cancellationToken)
    {
        var transactionClock = new FakeTransactionClock();
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            initProducerIdRetriableFailuresBeforeSuccess: 1,
            transactionClock: transactionClock,
            findCoordinatorAdvanceMs: 20_000,
            initProducerIdAdvanceMs: 40_000,
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.Uninitialized;

        var exception = await Assert.That(() => harness.Producer.InitTransactionsAsync(
                cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
        await Assert.That(exception.Elapsed).IsEqualTo(TimeSpan.FromSeconds(60));
        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(1);
        await Assert.That(harness.InitProducerIdRequests).IsEqualTo(1);
    }

    [Test]
    [Timeout(5_000)]
    public async Task InitTransactionsAsync_MaxBlockDeadline_IncludesLockAcquisition(
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            maxBlockMs: 1000);
        harness.Producer._transactionState = TransactionState.Uninitialized;
        var transactionLock = GetInstanceField<SemaphoreSlim>(harness.Producer, "_transactionLock");
        await transactionLock.WaitAsync(cancellationToken);

        try
        {
            var exception = await Assert.That(() => harness.Producer.InitTransactionsAsync(
                    cancellationToken).AsTask())
                .Throws<KafkaTimeoutException>();

            await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
            await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(0);
        }
        finally
        {
            transactionLock.Release();
        }
    }

    [Test]
    [Timeout(5_000)]
    public async Task CommitAsync_MaxBlockDeadline_IncludesFlush(
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            maxBlockMs: 1000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        var accumulator = GetInstanceField<RecordAccumulator>(harness.Producer, "_accumulator");
        SetInstanceField(accumulator, "_inFlightBatchCount", 1L);
        await using var transaction = new Transaction<string, string>(harness.Producer);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var exception = await Assert.That(() => transaction.CommitAsync(cancellationToken).AsTask())
                .Throws<KafkaTimeoutException>();

            await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
            await Assert.That(exception.Configured).IsEqualTo(TimeSpan.FromMilliseconds(1000));
            await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromMilliseconds(2500));
            await Assert.That(harness.EndTxnRequests).IsEqualTo(0);
            await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);
        }
        finally
        {
            SetInstanceField(accumulator, "_inFlightBatchCount", 0L);
            GetInstanceField<TaskCompletionSource<bool>?>(accumulator, "_flushTcs")?.TrySetResult(true);
        }
    }

    [Test]
    public async Task ReinitializeProducerIdAsync_MaxBlockDeadline_ThrowsTransactionTimeout()
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            initProducerIdRetriableFailuresBeforeSuccess: int.MaxValue,
            retryBackoffMs: 10,
            maxBlockMs: 1000);

        var exception = await Assert.That(() => harness.Producer.ReinitializeProducerIdAsync(
                CancellationToken.None).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
        await Assert.That(exception.Configured).IsEqualTo(TimeSpan.FromMilliseconds(1000));
        await Assert.That(exception.Message).Contains("max.block.ms (1000ms)");
    }

    [Test]
    [Timeout(5_000)]
    public async Task ReinitializeProducerIdAsync_MaxBlockDeadline_CancelsInFlightRequest(
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            initProducerIdWaitsForCancellation: true,
            maxBlockMs: 1000);

        var exception = await Assert.That(() => harness.Producer.ReinitializeProducerIdAsync(
                cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
        await Assert.That(harness.InitProducerIdRequests).IsEqualTo(1);
    }

    [Test]
    [Timeout(5_000)]
    public async Task InitTransactionsAsync_ReadyProducerInFlightInitTimeout_PreservesFatalState(
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            initProducerIdWaitsForCancellation: true,
            maxBlockMs: 1000);
        harness.Producer._transactionState = TransactionState.Ready;

        var exception = await Assert.That(() => harness.Producer.InitTransactionsAsync(
                cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
        await Assert.That(harness.InitProducerIdRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<FatalTransactionException>();
    }

    [Test]
    [Timeout(5_000)]
    public async Task InitTransactionsAsync_CallerCancellationInFlight_PreservesFatalState(
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            initProducerIdWaitsForCancellation: true,
            maxBlockMs: 4000);
        harness.Producer._transactionState = TransactionState.Ready;
        using var callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var initializationTask = harness.Producer.InitTransactionsAsync(callerCancellation.Token).AsTask();

        await harness.InitProducerIdStarted.WaitAsync(cancellationToken);
        callerCancellation.Cancel();

        await Assert.That(() => initializationTask).Throws<OperationCanceledException>();
        await Assert.That(harness.InitProducerIdRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<FatalTransactionException>();
    }

    [Test]
    [Timeout(5_000)]
    public async Task InitTransactionsAsync_CancellationBeforeWrite_DoesNotPoisonProducer(
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            initProducerIdWaitsBeforeWriteForCancellation: true,
            maxBlockMs: 4000);
        harness.Producer._transactionState = TransactionState.Ready;
        using var callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var initializationTask = harness.Producer.InitTransactionsAsync(callerCancellation.Token).AsTask();

        await harness.InitProducerIdStarted.WaitAsync(cancellationToken);
        callerCancellation.Cancel();

        await Assert.That(() => initializationTask).Throws<OperationCanceledException>();
        await Assert.That(harness.InitProducerIdRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.Ready);
    }

    [Test]
    public async Task EndTransactionAsync_RetriesBeyondPreviousAttemptLimit()
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnRetriableFailuresBeforeSuccess: 5,
            maxBlockMs: 60_000);

        await harness.Producer.EndTransactionAsync(committed: true, CancellationToken.None);

        await Assert.That(harness.EndTxnRequests).IsEqualTo(6);
    }

    [Test]
    public async Task AddPartitionsToTransactionAsync_RetriesBeyondPreviousAttemptLimit()
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            addPartitionsRetriableFailuresBeforeSuccess: 5,
            maxBlockMs: 60_000);

        await harness.Producer.AddPartitionsToTransactionAsync(
            [new TopicPartition("orders", 0)],
            CancellationToken.None);

        await Assert.That(harness.AddPartitionsRequests).IsEqualTo(6);
    }

    [Test]
    public async Task AddPartitionsToTransactionAsync_MultipleNotCoordinatorErrors_RediscoversOnce()
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            addPartitionsRetriableFailuresBeforeSuccess: 1,
            addPartitionsRetriableError: ErrorCode.NotCoordinator,
            maxBlockMs: 60_000);

        await harness.Producer.AddPartitionsToTransactionAsync(
            [new TopicPartition("orders", 0), new TopicPartition("orders", 1)],
            CancellationToken.None);

        await Assert.That(harness.AddPartitionsRequests).IsEqualTo(2);
        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(1);
    }

    // Transport failures (a broker that resets or refuses connections, a DNS miss, a connection
    // retired by pool churn) used to escape every transaction control-plane loop on the first
    // attempt because their catch filters only matched OperationCanceledException.

    [Test]
    [Arguments(TransportFailureKind.ConnectionReset)]
    [Arguments(TransportFailureKind.ConnectionRefused)]
    [Arguments(TransportFailureKind.IoFailure)]
    [Arguments(TransportFailureKind.DnsFailure)]
    [Arguments(TransportFailureKind.SetupTimeout)]
    [Arguments(TransportFailureKind.RetiredConnection)]
    public async Task InitTransactionsAsync_CoordinatorLookupConnectionFailure_RetriesAndSucceeds(
        TransportFailureKind failureKind)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            connectionFailures: new Queue<Exception>([CreateTransportFailure(failureKind)]),
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.Uninitialized;

        await harness.Producer.InitTransactionsAsync();

        await Assert.That(harness.ConnectionAttempts).IsGreaterThanOrEqualTo(2);
        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(1);
        await Assert.That(harness.InitProducerIdRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.Ready);
    }

    [Test]
    public async Task InitTransactionsAsync_CoordinatorLookupSendFailure_RetriesAndSucceeds()
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            findCoordinatorFailures: new Queue<Exception>([new IOException("Connection reset by peer")]),
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.Uninitialized;

        await harness.Producer.InitTransactionsAsync();

        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(2);
        await Assert.That(harness.InitProducerIdRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.Ready);
    }

    [Test]
    public async Task InitTransactionsAsync_InitProducerIdTransportFailure_RediscoversCoordinatorAndRetries()
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            initProducerIdFailures: new Queue<Exception>([new IOException("Connection reset by peer")]),
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.Uninitialized;

        await harness.Producer.InitTransactionsAsync();

        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(2);
        await Assert.That(harness.InitProducerIdRequests).IsEqualTo(2);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.Ready);
    }

    [Test]
    [Timeout(5_000)]
    public async Task InitTransactionsAsync_PersistentCoordinatorTransportFailure_TimesOutWithTransportCause(
        CancellationToken cancellationToken)
    {
        var transactionClock = new FakeTransactionClock();
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            findCoordinatorFailures: RepeatedFailures(
                static () => new SocketException((int)SocketError.ConnectionReset)),
            transportFailureAdvanceMs: 20_000,
            transactionClock: transactionClock,
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.Uninitialized;

        var exception = await Assert.That(() => harness.Producer.InitTransactionsAsync(
                cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
        await Assert.That(exception.InnerException).IsTypeOf<SocketException>();
        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(3);
        await Assert.That(harness.InitProducerIdRequests).IsEqualTo(0);
    }

    [Test]
    public async Task InitTransactionsAsync_TlsHandshakeFailure_PropagatesWithoutRetry()
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            connectionFailures: new Queue<Exception>([new AuthenticationException("TLS handshake failed")]),
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.Uninitialized;

        await Assert.That(() => harness.Producer.InitTransactionsAsync().AsTask())
            .Throws<AuthenticationException>();

        await Assert.That(harness.ConnectionAttempts).IsEqualTo(1);
        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(0);
    }

    [Test]
    public async Task AddPartitionsToTransactionAsync_TransportFailure_RediscoversCoordinatorAndRetries()
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            addPartitionsFailures: new Queue<Exception>([new SocketException((int)SocketError.ConnectionReset)]),
            maxBlockMs: 60_000);

        await harness.Producer.AddPartitionsToTransactionAsync(
            [new TopicPartition("orders", 0)],
            CancellationToken.None);

        await Assert.That(harness.AddPartitionsRequests).IsEqualTo(2);
        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EndTransactionAsync_TransportFailure_RediscoversCoordinatorAndRetries(bool failsAfterWrite)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnFailures: new Queue<Exception>([new IOException("Connection reset by peer")]),
            endTxnFailsAfterWrite: failsAfterWrite,
            maxBlockMs: 60_000);

        await harness.Producer.EndTransactionAsync(committed: true, CancellationToken.None);

        await Assert.That(harness.EndTxnRequests).IsEqualTo(2);
        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(1);
    }

    [Test]
    [Timeout(5_000)]
    public async Task CommitAsync_PersistentTransportFailureAfterWrite_PreservesFatalState(
        CancellationToken cancellationToken)
    {
        // The EndTxn request was written before the connection dropped, so whether the broker
        // committed is unknown. Once the budget is exhausted the producer must not return to
        // Ready and let a new transaction start under a possibly bumped epoch.
        var transactionClock = new FakeTransactionClock();
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnFailures: RepeatedFailures(static () => new IOException("Connection reset by peer")),
            endTxnFailsAfterWrite: true,
            transportFailureAdvanceMs: 30_000,
            transactionClock: transactionClock,
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        await using var transaction = new Transaction<string, string>(harness.Producer);

        var exception = await Assert.That(() => transaction.CommitAsync(cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
        await Assert.That(exception.InnerException).IsTypeOf<IOException>();
        await Assert.That(harness.EndTxnRequests).IsEqualTo(2);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(harness.Producer._lastTransactionError).IsEqualTo(ErrorCode.RequestTimedOut);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<FatalTransactionException>();
    }

    [Test]
    [Timeout(5_000)]
    public async Task CommitAsync_PersistentTransportFailureBeforeWrite_PreservesAbortableState(
        CancellationToken cancellationToken)
    {
        var transactionClock = new FakeTransactionClock();
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnFailures: RepeatedFailures(
                static () => new SocketException((int)SocketError.ConnectionRefused)),
            transportFailureAdvanceMs: 30_000,
            transactionClock: transactionClock,
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        await using var transaction = new Transaction<string, string>(harness.Producer);

        var exception = await Assert.That(() => transaction.CommitAsync(cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.InnerException).IsTypeOf<SocketException>();
        await Assert.That(harness.EndTxnRequests).IsEqualTo(2);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<InvalidOperationException>();
    }

    [Test]
    [Timeout(5_000)]
    public async Task CommitAsync_InAbortableErrorState_ThrowsWithoutEndTxn_AndAbortRecovers(
        CancellationToken cancellationToken)
    {
        // Records that failed (enrollment, produce error) are not part of the transaction. A
        // caller that ignored those failures must not be able to commit the rest.
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9);
        harness.Producer._transactionState = TransactionState.AbortableError;
        harness.Producer._lastTransactionError = ErrorCode.NetworkException;
        await using var transaction = new Transaction<string, string>(harness.Producer);

        var exception = await Assert.That(() => transaction.CommitAsync(cancellationToken).AsTask())
            .Throws<AbortableTransactionException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.NetworkException);
        await Assert.That(harness.EndTxnRequests).IsEqualTo(0);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);

        await transaction.AbortAsync(cancellationToken);

        await Assert.That(harness.EndTxnRequests).IsEqualTo(1);
        await Assert.That(harness.CapturedRequest.Committed).IsFalse();
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.Ready);
    }

    [Test]
    [Timeout(5_000)]
    public async Task PrepareAsync_InAbortableErrorState_ThrowsAbortableTransactionException(
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9);
        harness.Producer._transactionState = TransactionState.AbortableError;
        harness.Producer._lastTransactionError = ErrorCode.NetworkException;
        var transaction = new Transaction<string, string>(harness.Producer);

        await Assert.That(() => transaction.PrepareAsync(cancellationToken).AsTask())
            .Throws<AbortableTransactionException>();

        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);
        harness.Producer._transactionState = TransactionState.Ready;
    }

    [Test]
    [Timeout(15_000)]
    public async Task CommitAsync_EnrollmentFailedDuringCommitFlush_ThrowsWithoutEndTxn(
        CancellationToken cancellationToken)
    {
        // An unawaited produce can still be enrolling its partition when CommitAsync starts.
        // If that enrollment fails while the commit flushes, the partition's batches are failed
        // and EndTxn(commit) would commit the transaction without them.
        ValueTask AddPartitions(IReadOnlyList<TopicPartition> partitions, CancellationToken token) =>
            ValueTask.FromException(new AuthenticationException("Invalid credentials."));

        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            transactionFeatureVersion: 1,
            enableTwoPhaseCommit: false,
            addPartitionsToTransaction: AddPartitions);
        harness.Producer._transactionState = TransactionState.CommittingTransaction;
        var batch = CreateEnrollmentBatch("topic-a", 0);
        var enrollmentCompleted = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var pending = harness.Producer.TryEnsurePartitionsInTransaction(
            [batch],
            1,
            enrollmentCompleted.SetResult,
            [],
            []);
        await Assert.That(pending.IsEnrolled).IsFalse();
        await enrollmentCompleted.Task.WaitAsync(cancellationToken);

        var exception = await Assert.That(
                () => harness.Producer.CommitTransactionAsync(null, cancellationToken).AsTask())
            .Throws<AbortableTransactionException>();

        await Assert.That(exception!.InnerException).IsTypeOf<AuthenticationException>();
        await Assert.That(harness.EndTxnRequests).IsEqualTo(0);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);
        harness.Producer._transactionState = TransactionState.Ready;
    }

    [Test]
    [Timeout(5_000)]
    public async Task CommitAsync_RetriableResponseAfterUnansweredWrite_BudgetExpiry_PreservesFatalState(
        CancellationToken cancellationToken)
    {
        // Attempt 1 is written and the connection drops: the coordinator may have committed.
        // Attempt 2 is answered with a retriable code, which says nothing about attempt 1, so
        // an exhausted budget must still report the ambiguous outcome as fatal.
        var transactionClock = new FakeTransactionClock();
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnFailures: new Queue<Exception>([new IOException("Connection reset by peer")]),
            endTxnFailsAfterWrite: true,
            endTxnRetriableFailuresBeforeSuccess: 64,
            transportFailureAdvanceMs: 30_000,
            endTxnAdvanceMs: 30_000,
            transactionClock: transactionClock,
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        await using var transaction = new Transaction<string, string>(harness.Producer);

        var exception = await Assert.That(() => transaction.CommitAsync(cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.InnerException).IsTypeOf<IOException>();
        await Assert.That(harness.EndTxnRequests).IsEqualTo(2);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<FatalTransactionException>();
    }

    [Test]
    [Timeout(5_000)]
    public async Task CommitAsync_RetriableResponseWithoutUnansweredWrite_BudgetExpiry_StaysAbortable(
        CancellationToken cancellationToken)
    {
        // Control for the test above: every attempt was answered, so nothing is ambiguous.
        var transactionClock = new FakeTransactionClock();
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnRetriableFailuresBeforeSuccess: 64,
            endTxnAdvanceMs: 30_000,
            transactionClock: transactionClock,
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        await using var transaction = new Transaction<string, string>(harness.Producer);

        await Assert.That(() => transaction.CommitAsync(cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);
    }

    [Test]
    [Timeout(5_000)]
    public async Task ReinitializeProducerIdAsync_RetriableResponseAfterUnansweredWrite_BudgetExpiry_PreservesFatalState(
        CancellationToken cancellationToken)
    {
        var transactionClock = new FakeTransactionClock();
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            initProducerIdFailures: new Queue<Exception>([new IOException("Connection reset by peer")]),
            initProducerIdFailsAfterWrite: true,
            initProducerIdRetriableFailuresBeforeSuccess: 64,
            transportFailureAdvanceMs: 30_000,
            initProducerIdAdvanceMs: 30_000,
            transactionClock: transactionClock,
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.Ready;

        await Assert.That(() => harness.Producer.ReinitializeProducerIdAsync(cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(harness.InitProducerIdRequests).IsEqualTo(2);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
    }

    [Test]
    [Timeout(5_000)]
    public async Task CommitAsync_UnansweredWriteThenRediscoveryFailure_PreservesFatalState(
        CancellationToken cancellationToken)
    {
        // The exit is neither a timeout nor a cancellation: re-discovery after the dropped
        // EndTxn answers with a non-retriable error. The written request is still unanswered,
        // so the producer must not return to Ready.
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnFailures: new Queue<Exception>([new IOException("Connection reset by peer")]),
            endTxnFailsAfterWrite: true,
            findCoordinatorError: ErrorCode.TransactionalIdAuthorizationFailed,
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        await using var transaction = new Transaction<string, string>(harness.Producer);

        var exception = await Assert.That(() => transaction.CommitAsync(cancellationToken).AsTask())
            .Throws<TransactionException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TransactionalIdAuthorizationFailed);
        await Assert.That(harness.EndTxnRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<FatalTransactionException>();
    }

    [Test]
    [Timeout(10_000)]
    public async Task CommitAsync_TransportFailureAfterBudgetTokenFired_TimesOutWithTransportCause(
        CancellationToken cancellationToken)
    {
        // The budget runs out on its cancellation token (real time), not on the fake clock: the
        // socket reports its failure after the token fired. The caller must still see the
        // timeout with the transport cause, not a raw SocketException.
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnFailsAfterCancellation: true,
            maxBlockMs: 300);
        harness.Producer._transactionState = TransactionState.InTransaction;
        await using var transaction = new Transaction<string, string>(harness.Producer);

        var exception = await Assert.That(() => transaction.CommitAsync(cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
        await Assert.That(exception.InnerException).IsTypeOf<SocketException>();
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);
    }

    [Test]
    [Timeout(10_000)]
    public async Task InitTransactionsAsync_TransportFailureAfterBudgetTokenFired_TimesOutWithTransportCause(
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            findCoordinatorFailsAfterCancellation: true,
            maxBlockMs: 300);
        harness.Producer._transactionState = TransactionState.Uninitialized;

        var exception = await Assert.That(() => harness.Producer.InitTransactionsAsync(cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
        await Assert.That(exception.InnerException).IsTypeOf<SocketException>();
    }

    [Test]
    [Timeout(10_000)]
    public async Task CommitAsync_TransportFailureAfterCallerCancellation_ThrowsOperationCanceled(
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnFailsAfterCancellation: true,
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        // Not disposed: the best-effort abort would wait out the whole budget on the same fake.
        var transaction = new Transaction<string, string>(harness.Producer);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var commit = transaction.CommitAsync(caller.Token).AsTask();
        await harness.EndTxnStarted.WaitAsync(cancellationToken);
        caller.Cancel();

        await Assert.That(() => commit).Throws<OperationCanceledException>();
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);
        harness.Producer._transactionState = TransactionState.Ready;
    }

    [Test]
    [Timeout(10_000)]
    public async Task InitTransactionsAsync_SecondCoordinatorLookup_DoesNotRestartAtTheDeadBroker(
        CancellationToken cancellationToken)
    {
        // Every re-discovery used to restart the rotation at the first known broker, so a
        // black-holed first broker cost each retry of every operation a connection setup.
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            brokerCount: 3,
            maxBlockMs: 60_000);
        var brokers = harness.Brokers;
        var deadBrokerId = brokers[0].NodeId;
        harness.KillBroker(deadBrokerId);
        harness.CoordinatorNodeId = brokers[1].NodeId;
        harness.Producer._transactionState = TransactionState.Uninitialized;

        await harness.Producer.InitTransactionsAsync(cancellationToken);
        await Assert.That(harness.ConnectionAttemptsTo(deadBrokerId)).IsEqualTo(1);

        await harness.Producer.InitTransactionsAsync(cancellationToken);

        await Assert.That(harness.ConnectionAttemptsTo(deadBrokerId)).IsEqualTo(1);
        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(2);
    }

    [Test]
    [Timeout(10_000)]
    public async Task DisposeAsync_WhileCommitIsRetrying_EndsTheCommitPromptly(
        CancellationToken cancellationToken)
    {
        // The retry budget is a minute; disposal must not leave CommitAsync retrying against
        // a producer that is gone for the rest of it.
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnFailures: RepeatedFailures(
                static () => new SocketException((int)SocketError.ConnectionRefused)),
            retryBackoffMs: 200,
            maxBlockMs: 60_000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        var transaction = new Transaction<string, string>(harness.Producer);

        var commit = transaction.CommitAsync(cancellationToken).AsTask();
        await harness.EndTxnStarted.WaitAsync(cancellationToken);
        await harness.Producer.DisposeAsync();

        var completed = await Task.WhenAny(commit, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
        await Assert.That(completed).IsSameReferenceAs(commit);
        await Assert.That(commit.IsFaulted || commit.IsCanceled).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_WhenAbortFailsUnexpectedly_KeepsTransactionAbortableWithoutThrowing()
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 42,
            currentProducerEpoch: 5,
            endTxnFailures: new Queue<Exception>([new InvalidOperationException("unexpected abort failure")]));
        harness.Producer._transactionState = TransactionState.InTransaction;
        var transaction = new Transaction<string, string>(harness.Producer);

        await transaction.DisposeAsync();

        await Assert.That(harness.EndTxnRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<InvalidOperationException>();
    }

    [Test]
    [Timeout(5_000)]
    public async Task AbortAsync_TV1ProducerIdReinitializationTimeout_PreservesFatalState(
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            transactionFeatureVersion: 1,
            enableTwoPhaseCommit: false,
            initProducerIdWaitsForCancellation: true,
            maxBlockMs: 1000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        await using var transaction = new Transaction<string, string>(harness.Producer);

        await Assert.That(() => transaction.AbortAsync(cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<FatalTransactionException>();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    [Timeout(5_000)]
    public async Task EndTransactionAsync_InFlightDeadline_PreservesFatalState(
        bool committed,
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnWaitsForCancellation: true,
            maxBlockMs: 1000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        await using var transaction = new Transaction<string, string>(harness.Producer);
        var stopwatch = Stopwatch.StartNew();

        var exception = committed
            ? await Assert.That(() => transaction.CommitAsync(cancellationToken).AsTask())
                .Throws<KafkaTimeoutException>()
            : await Assert.That(() => transaction.AbortAsync(cancellationToken).AsTask())
                .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
        await Assert.That(exception.Configured).IsEqualTo(TimeSpan.FromMilliseconds(1000));
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromMilliseconds(2500));
        await Assert.That(harness.EndTxnRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<FatalTransactionException>();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    [Timeout(5_000)]
    public async Task EndTransactionAsync_CallerCancellationInFlight_PreservesFatalState(
        bool committed,
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnWaitsForCancellation: true,
            maxBlockMs: 4000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        await using var transaction = new Transaction<string, string>(harness.Producer);
        using var callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var completionTask = committed
            ? transaction.CommitAsync(callerCancellation.Token).AsTask()
            : transaction.AbortAsync(callerCancellation.Token).AsTask();

        await harness.EndTxnStarted.WaitAsync(cancellationToken);
        callerCancellation.Cancel();

        await Assert.That(() => completionTask).Throws<OperationCanceledException>();
        await Assert.That(harness.EndTxnRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<FatalTransactionException>();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    [Timeout(5_000)]
    public async Task EndTransactionAsync_CancellationBeforeWrite_PreservesAbortableState(
        bool committed,
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnWaitsBeforeWriteForCancellation: true,
            maxBlockMs: 4000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        var transaction = new Transaction<string, string>(harness.Producer);
        using var callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var completionTask = committed
            ? transaction.CommitAsync(callerCancellation.Token).AsTask()
            : transaction.AbortAsync(callerCancellation.Token).AsTask();

        await harness.EndTxnStarted.WaitAsync(cancellationToken);
        callerCancellation.Cancel();

        await Assert.That(() => completionTask).Throws<OperationCanceledException>();
        await Assert.That(harness.EndTxnRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.AbortableError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<InvalidOperationException>();

        harness.Producer._transactionState = TransactionState.Ready;
        await transaction.DisposeAsync();
    }

    [Test]
    [Timeout(5_000)]
    public async Task AbortAsync_TV1CallerCancellationAfterEndTxn_PreservesFatalState(
        CancellationToken cancellationToken)
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            transactionFeatureVersion: 1,
            enableTwoPhaseCommit: false,
            initProducerIdWaitsForCancellation: true,
            maxBlockMs: 4000);
        harness.Producer._transactionState = TransactionState.InTransaction;
        await using var transaction = new Transaction<string, string>(harness.Producer);
        using var callerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var abortTask = transaction.AbortAsync(callerCancellation.Token).AsTask();

        await harness.InitProducerIdStarted.WaitAsync(cancellationToken);
        callerCancellation.Cancel();

        await Assert.That(() => abortTask).Throws<OperationCanceledException>();
        await Assert.That(harness.EndTxnRequests).IsEqualTo(1);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.FatalError);
        await Assert.That(() => harness.Producer.BeginTransaction()).Throws<FatalTransactionException>();
    }

    [Test]
    public async Task CompletePreparedTransactionAsync_Abort_UsesPreparedTransactionProducerIdentity()
    {
        var preparedState = new PreparedTransactionState(1001, 4);
        await using var harness = BuildPreparedCompletionHarness(
            preparedState,
            currentProducerId: 2002,
            currentProducerEpoch: 9);

        await harness.Producer.CompletePreparedTransactionAsync(preparedState, committed: false);

        var request = harness.CapturedRequest;
        await Assert.That(request.ProducerId).IsEqualTo(preparedState.ProducerId);
        await Assert.That(request.ProducerEpoch).IsEqualTo(preparedState.ProducerEpoch);
        await Assert.That(request.Committed).IsFalse();
        await Assert.That(GetInstanceField<long>(harness.Producer, "_producerId")).IsEqualTo(2002);
        await Assert.That(GetInstanceField<short>(harness.Producer, "_producerEpoch")).IsEqualTo((short)9);
        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.Ready);
        await Assert.That(harness.Producer._preparedTransactionState).IsEqualTo(PreparedTransactionState.Empty);
    }

    [Test]
    public async Task CompletePreparedTransactionAsync_MismatchedState_ThrowsTransactionException()
    {
        var preparedState = new PreparedTransactionState(1001, 4);
        await using var harness = BuildPreparedCompletionHarness(
            preparedState,
            currentProducerId: 2002,
            currentProducerEpoch: 9);

        await Assert.That(async () =>
        {
            await harness.Producer.CompletePreparedTransactionAsync(
                new PreparedTransactionState(9999, 1),
                committed: false);
        }).Throws<TransactionException>();

        await Assert.That(harness.Producer._transactionState).IsEqualTo(TransactionState.PreparedTransaction);
        await Assert.That(harness.Producer._preparedTransactionState).IsEqualTo(preparedState);
    }

    [Test]
    public async Task InitTransactionsAsync_WithKeepPreparedAndUnsupportedFeature_ThrowsBrokerVersionException()
    {
        await using var harness = BuildPreparedCompletionHarness(
            new PreparedTransactionState(1001, 4),
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            transactionFeatureVersion: 2);

        await Assert.That(async () =>
        {
            await harness.Producer.ReinitializeProducerIdAsync(
                CancellationToken.None,
                keepPreparedTransaction: true);
        }).Throws<BrokerVersionException>();
    }

    [Test]
    public async Task TopicPartitionOffset_RecordStruct_HasExpectedProperties()
    {
        var tpo = new TopicPartitionOffset("test-topic", 0, 42);

        await Assert.That(tpo.Topic).IsEqualTo("test-topic");
        await Assert.That(tpo.Partition).IsEqualTo(0);
        await Assert.That(tpo.Offset).IsEqualTo(42L);
    }

    [Test]
    public async Task TopicPartitionOffset_Equality()
    {
        var tpo1 = new TopicPartitionOffset("topic", 1, 100);
        var tpo2 = new TopicPartitionOffset("topic", 1, 100);
        var tpo3 = new TopicPartitionOffset("topic", 2, 100);

        await Assert.That(tpo1).IsEqualTo(tpo2);
        await Assert.That(tpo1).IsNotEqualTo(tpo3);
    }

    [Test]
    [Timeout(120_000)]
    public async Task TransactionPartitionEnrollment_BatchesCoalescedPartitions(
        CancellationToken cancellationToken)
    {
        var firstRequestStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failFirstRequest = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var retryRequestStarted = new TaskCompletionSource<IReadOnlyList<TopicPartition>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completeRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        var failNextEnrollment = 0;

        async ValueTask AddPartitions(
            IReadOnlyList<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref requestCount) == 1)
            {
                firstRequestStarted.TrySetResult();
                await failFirstRequest.Task.WaitAsync(cancellationToken);
                throw new IOException("Transient connection failure");
            }
            if (Interlocked.Exchange(ref failNextEnrollment, 0) == 1)
                throw new TransactionException("Partition enrollment failed.");

            retryRequestStarted.TrySetResult([.. partitions]);
            await completeRequest.Task.WaitAsync(cancellationToken);
        }

        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            TransactionalId = "test-txn-id",
            CloseTimeoutMs = 100,
            RetryBackoffMs = 0,
            RetryBackoffMaxMs = 0
        };
        await using var connectionPool = new ConnectionPool(
            options.ClientId,
            connectionOptions: null,
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) =>
                throw new InvalidOperationException("Enrollment test must use the injected request callback."));
        await using var metadataManager = new MetadataManager(connectionPool, options.BootstrapServers);
        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager,
            DekafMemoryBudget.Global,
            addPartitionsToTransaction: AddPartitions);
        producer._currentTransactionUsesTV2 = true;
        var implicitBatch = CreateEnrollmentBatch("implicit-topic", 0);
        var implicitResult = producer.TryEnsurePartitionsInTransaction(
            [implicitBatch],
            1,
            static _ => { },
            [],
            []);
        await Assert.That(implicitResult.IsEnrolled).IsTrue();
        await Assert.That(requestCount).IsEqualTo(0);
        await Assert.That(producer._partitionsInTransaction)
            .Contains(implicitBatch.TopicPartition);

        producer._currentTransactionUsesTV2 = false;
        var batches = new[]
        {
            CreateEnrollmentBatch("topic-a", 0),
            CreateEnrollmentBatch("topic-a", 1),
            CreateEnrollmentBatch("topic-b", 0)
        };
        var enrollmentCompleted = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingPartitions = new HashSet<TopicPartition>();

        var enrolled = producer.TryEnsurePartitionsInTransaction(
            batches,
            batches.Length,
            enrollmentCompleted.SetResult,
            pendingPartitions,
            []);

        await Assert.That(enrolled.IsEnrolled).IsFalse();
        await Assert.That(enrolled.Error).IsNull();
        await Assert.That(pendingPartitions).IsEquivalentTo(batches.Select(batch => batch.TopicPartition));
        await firstRequestStarted.Task.WaitAsync(cancellationToken);
        failFirstRequest.SetResult();
        var requestedPartitions = await retryRequestStarted.Task.WaitAsync(cancellationToken);
        await Assert.That(requestCount).IsEqualTo(2);
        await Assert.That(requestedPartitions).IsEquivalentTo(new[]
        {
            new TopicPartition("topic-a", 0),
            new TopicPartition("topic-a", 1),
            new TopicPartition("topic-b", 0)
        });

        completeRequest.SetResult();
        await Assert.That(await enrollmentCompleted.Task.WaitAsync(cancellationToken)).IsNull();
        await Assert.That(producer.TryEnsurePartitionsInTransaction(
            batches,
            batches.Length,
            static _ => { },
            [],
            []).IsEnrolled).IsTrue();

        var mixedBatches = new[] { batches[0], CreateEnrollmentBatch("topic-c", 2) };
        var mixedPendingPartitions = new HashSet<TopicPartition>();
        var mixedEnrollmentCompleted = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var mixedResult = producer.TryEnsurePartitionsInTransaction(
            mixedBatches,
            mixedBatches.Length,
            mixedEnrollmentCompleted.SetResult,
            mixedPendingPartitions,
            []);

        await Assert.That(mixedResult.IsEnrolled).IsFalse();
        await Assert.That(mixedPendingPartitions).IsEquivalentTo(
            [new TopicPartition("topic-c", 2)]);
        await Assert.That(await mixedEnrollmentCompleted.Task.WaitAsync(cancellationToken)).IsNull();

        Interlocked.Exchange(ref failNextEnrollment, 1);
        var failedBatch = CreateEnrollmentBatch("failed-topic", 0);
        var failedEnrollmentCompleted = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failedPendingPartitions = new HashSet<TopicPartition>();
        var pendingFailure = producer.TryEnsurePartitionsInTransaction(
            [failedBatch],
            1,
            failedEnrollmentCompleted.SetResult,
            failedPendingPartitions,
            []);
        await Assert.That(pendingFailure.IsEnrolled).IsFalse();
        await Assert.That(await failedEnrollmentCompleted.Task.WaitAsync(cancellationToken)).IsNull();

        failedPendingPartitions.Clear();
        var failedResult = producer.TryEnsurePartitionsInTransaction(
            [failedBatch],
            1,
            static _ => { },
            [],
            failedPendingPartitions);
        await Assert.That(failedResult.Error).IsTypeOf<TransactionException>();
        await Assert.That(failedPendingPartitions).IsEquivalentTo([failedBatch.TopicPartition]);

        var unrelatedBatch = CreateEnrollmentBatch("unrelated-topic", 0);
        var unrelatedEnrollmentCompleted = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var unrelatedResult = producer.TryEnsurePartitionsInTransaction(
            [unrelatedBatch],
            1,
            unrelatedEnrollmentCompleted.SetResult,
            [],
            []);
        await Assert.That(unrelatedResult.Error).IsNull();
        await Assert.That(await unrelatedEnrollmentCompleted.Task.WaitAsync(cancellationToken)).IsNull();
        await Assert.That(producer.TryEnsurePartitionsInTransaction(
            [unrelatedBatch],
            1,
            static _ => { },
            [],
            []).IsEnrolled).IsTrue();
    }

    [Test]
    public async Task TransactionPartitionEnrollment_ResetWakesWaitersAndIgnoresStaleCompletion()
    {
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completeRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async ValueTask AddPartitions(
            IReadOnlyList<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            requestStarted.TrySetResult();
            await completeRequest.Task.WaitAsync(cancellationToken);
            requestReturned.TrySetResult();
        }

        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            TransactionalId = "test-txn-id",
            CloseTimeoutMs = 100
        };
        await using var connectionPool = new ConnectionPool(
            options.ClientId,
            connectionOptions: null,
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) =>
                throw new InvalidOperationException("Enrollment test must use the injected request callback."));
        await using var metadataManager = new MetadataManager(connectionPool, options.BootstrapServers);
        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager,
            DekafMemoryBudget.Global,
            addPartitionsToTransaction: AddPartitions);
        var batch = CreateEnrollmentBatch("topic-a", 0);
        var enrollmentReset = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        producer.TryEnsurePartitionsInTransaction(
            [batch],
            1,
            enrollmentReset.SetResult,
            [],
            []);
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        producer.FinalizeCompletedTransactionState();
        await Assert.That(await enrollmentReset.Task.WaitAsync(TimeSpan.FromSeconds(1)))
            .IsTypeOf<TransactionException>();
        completeRequest.SetResult();
        await requestReturned.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await Assert.That(producer._partitionsInTransaction).IsEmpty();
    }

    [Test]
    public async Task TransactionPartitionEnrollment_AuthenticationFailure_DoesNotRetry()
    {
        var requestCount = 0;

        ValueTask AddPartitions(
            IReadOnlyList<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestCount);
            throw new AuthenticationException("Invalid credentials.");
        }

        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            TransactionalId = "test-txn-id",
            CloseTimeoutMs = 100
        };
        await using var connectionPool = new ConnectionPool(
            options.ClientId,
            connectionOptions: null,
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) =>
                throw new InvalidOperationException("Enrollment test must use the injected request callback."));
        await using var metadataManager = new MetadataManager(connectionPool, options.BootstrapServers);
        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager,
            DekafMemoryBudget.Global,
            addPartitionsToTransaction: AddPartitions);
        var batch = CreateEnrollmentBatch("auth-failure-topic", 0);
        var enrollmentCompleted = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var result = producer.TryEnsurePartitionsInTransaction(
            [batch],
            1,
            enrollmentCompleted.SetResult,
            [],
            []);

        await Assert.That(result.IsEnrolled).IsFalse();
        await Assert.That(await enrollmentCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1))).IsNull();
        await Assert.That(requestCount).IsEqualTo(1);
        await Assert.That(producer.TryEnsurePartitionsInTransaction(
            [batch],
            1,
            static _ => { },
            [],
            []).Error).IsTypeOf<AuthenticationException>();
    }

    [Test]
    [Arguments(false, (short)2)]
    [Arguments(true, (short)1)]
    public async Task BeginTransaction_FeatureVersionChangedBetweenTransactions_UsesNewSnapshot(
        bool initializedWithTV2,
        short finalizedVersion)
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id")
            .Build();
        var kafkaProducer = (KafkaProducer<string, string>)producer;
        kafkaProducer._transactionState = TransactionState.Ready;
        kafkaProducer._currentTransactionUsesTV2 = initializedWithTV2;
        SetInstanceField(
            kafkaProducer,
            "_currentTransactionFeatureVersion",
            initializedWithTV2 ? (short)2 : (short)1);
        SetFinalizedTransactionVersion(kafkaProducer, finalizedVersion);

        var transaction = producer.BeginTransaction();

        await Assert.That(kafkaProducer._transactionState).IsEqualTo(TransactionState.InTransaction);
        await Assert.That(kafkaProducer._currentTransactionUsesTV2)
            .IsEqualTo(finalizedVersion >= 2);
        await Assert.That(GetInstanceField<short>(
            kafkaProducer,
            "_currentTransactionFeatureVersion")).IsEqualTo(finalizedVersion);
        kafkaProducer._transactionState = TransactionState.Ready;
        await transaction.DisposeAsync();
    }

    [Test]
    [Timeout(15_000)]
    public async Task TransactionPartitionEnrollment_PermanentTransportFailure_MovesTransactionToAbortableError(
        CancellationToken cancellationToken)
    {
        // Records of a partition that never enrolled are failed back to the caller, so the
        // transaction must refuse to commit as though they had been written.
        var requestCount = 0;

        ValueTask AddPartitions(IReadOnlyList<TopicPartition> partitions, CancellationToken token)
        {
            Interlocked.Increment(ref requestCount);
            return ValueTask.FromException(new SocketException((int)SocketError.ConnectionReset));
        }

        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            TransactionalId = "test-txn-id",
            CloseTimeoutMs = 100,
            RetryBackoffMs = 0,
            RetryBackoffMaxMs = 0
        };
        await using var connectionPool = new ConnectionPool(
            options.ClientId,
            connectionOptions: null,
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) =>
                throw new InvalidOperationException("Enrollment test must use the injected request callback."));
        await using var metadataManager = new MetadataManager(connectionPool, options.BootstrapServers);
        await using var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager,
            DekafMemoryBudget.Global,
            addPartitionsToTransaction: AddPartitions);
        producer._currentTransactionUsesTV2 = false;
        producer._transactionState = TransactionState.InTransaction;
        var batch = CreateEnrollmentBatch("topic-a", 0);
        var enrollmentCompleted = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var pending = producer.TryEnsurePartitionsInTransaction(
            [batch],
            1,
            enrollmentCompleted.SetResult,
            [],
            []);
        await Assert.That(pending.IsEnrolled).IsFalse();
        await enrollmentCompleted.Task.WaitAsync(cancellationToken);

        var failedPartitions = new HashSet<TopicPartition>();
        var failed = producer.TryEnsurePartitionsInTransaction(
            [batch],
            1,
            static _ => { },
            [],
            failedPartitions);
        await Assert.That(failed.Error).IsTypeOf<SocketException>();
        await Assert.That(failedPartitions).IsEquivalentTo([batch.TopicPartition]);
        await Assert.That(requestCount).IsEqualTo(5);
        await Assert.That(producer._transactionState).IsEqualTo(TransactionState.AbortableError);
        await Assert.That(producer._lastTransactionError).IsEqualTo(ErrorCode.NetworkException);
        await Assert.That(() => producer.BeginTransaction()).Throws<InvalidOperationException>();
        producer._transactionState = TransactionState.Ready;
    }

    private static ReadyBatch CreateEnrollmentBatch(string topic, int partition)
    {
        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition(topic, partition),
            new RecordBatch(),
            completionSourcesArray: null,
            completionSourcesCount: 1,
            dataSize: 1,
            recordCount: 1);
        return batch;
    }

    private static KafkaProducer<string, string> BuildInitializedTransactionalProducer(bool enableTwoPhaseCommit)
    {
        var builder = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id");

        if (enableTwoPhaseCommit)
            builder.WithTwoPhaseCommit();

        var producer = (KafkaProducer<string, string>)builder.Build();
        SetInstanceField(producer, "_initialized", true);
        SetInstanceField(producer, "_producerId", 42L);
        SetInstanceField(producer, "_producerEpoch", (short)5);
        SetFinalizedTransactionVersion(producer, 3);
        producer._currentTransactionUsesTV2 = true;
        SetInstanceField(producer, "_currentTransactionFeatureVersion", (short)3);
        producer._transactionState = TransactionState.Ready;
        return producer;
    }

    private static PreparedCompletionHarness BuildPreparedCompletionHarness(
        PreparedTransactionState preparedState,
        long currentProducerId,
        short currentProducerEpoch,
        ErrorCode endTxnError = ErrorCode.None,
        short transactionFeatureVersion = 3,
        bool enableTwoPhaseCommit = true,
        int coordinatorRetriableFailuresBeforeSuccess = 0,
        int initProducerIdRetriableFailuresBeforeSuccess = 0,
        int addPartitionsRetriableFailuresBeforeSuccess = 0,
        int endTxnRetriableFailuresBeforeSuccess = 0,
        bool endTxnWaitsForCancellation = false,
        bool initProducerIdWaitsForCancellation = false,
        bool endTxnWaitsBeforeWriteForCancellation = false,
        bool initProducerIdWaitsBeforeWriteForCancellation = false,
        int findCoordinatorDelayMs = 0,
        int emptyCoordinatorResponsesBeforeSuccess = 0,
        ErrorCode addPartitionsRetriableError = ErrorCode.CoordinatorLoadInProgress,
        int retryBackoffMs = 0,
        FakeTransactionClock? transactionClock = null,
        int findCoordinatorAdvanceMs = 0,
        int initProducerIdAdvanceMs = 0,
        int maxBlockMs = 1000,
        Queue<Exception>? connectionFailures = null,
        Queue<Exception>? findCoordinatorFailures = null,
        Queue<Exception>? initProducerIdFailures = null,
        Queue<Exception>? addPartitionsFailures = null,
        Queue<Exception>? endTxnFailures = null,
        bool endTxnFailsAfterWrite = false,
        int transportFailureAdvanceMs = 0,
        bool initProducerIdFailsAfterWrite = false,
        int endTxnAdvanceMs = 0,
        ErrorCode findCoordinatorError = ErrorCode.None,
        bool endTxnFailsAfterCancellation = false,
        bool findCoordinatorFailsAfterCancellation = false,
        int brokerCount = 1,
        Func<IReadOnlyList<TopicPartition>, CancellationToken, ValueTask>? addPartitionsToTransaction = null)
    {
        var connection = new LeaseTrackingConnection(
            preparedState,
            producerId: currentProducerId,
            producerEpoch: currentProducerEpoch,
            endTxnError: endTxnError,
            coordinatorRetriableFailuresBeforeSuccess: coordinatorRetriableFailuresBeforeSuccess,
            initProducerIdRetriableFailuresBeforeSuccess: initProducerIdRetriableFailuresBeforeSuccess,
            addPartitionsRetriableFailuresBeforeSuccess: addPartitionsRetriableFailuresBeforeSuccess,
            endTxnRetriableFailuresBeforeSuccess: endTxnRetriableFailuresBeforeSuccess,
            endTxnWaitsForCancellation: endTxnWaitsForCancellation,
            initProducerIdWaitsForCancellation: initProducerIdWaitsForCancellation,
            endTxnWaitsBeforeWriteForCancellation: endTxnWaitsBeforeWriteForCancellation,
            initProducerIdWaitsBeforeWriteForCancellation: initProducerIdWaitsBeforeWriteForCancellation,
            findCoordinatorDelayMs: findCoordinatorDelayMs,
            emptyCoordinatorResponsesBeforeSuccess: emptyCoordinatorResponsesBeforeSuccess,
            addPartitionsRetriableError: addPartitionsRetriableError,
            transactionClock: transactionClock,
            findCoordinatorAdvanceMs: findCoordinatorAdvanceMs,
            initProducerIdAdvanceMs: initProducerIdAdvanceMs,
            findCoordinatorFailures: findCoordinatorFailures,
            initProducerIdFailures: initProducerIdFailures,
            addPartitionsFailures: addPartitionsFailures,
            endTxnFailures: endTxnFailures,
            endTxnFailsAfterWrite: endTxnFailsAfterWrite,
            transportFailureAdvanceMs: transportFailureAdvanceMs,
            initProducerIdFailsAfterWrite: initProducerIdFailsAfterWrite,
            endTxnAdvanceMs: endTxnAdvanceMs,
            findCoordinatorError: findCoordinatorError,
            endTxnFailsAfterCancellation: endTxnFailsAfterCancellation,
            findCoordinatorFailsAfterCancellation: findCoordinatorFailsAfterCancellation);

        var connectionAttempts = new StrongBox<int>();
        var connectionAttemptBrokerIds = new List<int>();
        var deadBrokerIds = new HashSet<int>();
        var connectionPool = new ConnectionPool(
            clientId: "test-producer",
            connectionOptions: new ConnectionOptions { ReconnectBackoff = TimeSpan.Zero },
            connectionsPerBroker: 1,
            connectionFactory: (brokerId, _, _, _, _) =>
            {
                Interlocked.Increment(ref connectionAttempts.Value);
                lock (connectionAttemptBrokerIds)
                {
                    connectionAttemptBrokerIds.Add(brokerId);
                    if (deadBrokerIds.Contains(brokerId))
                    {
                        return ValueTask.FromException<IKafkaConnection>(
                            new SocketException((int)SocketError.ConnectionRefused));
                    }
                }

                // Connection setup failures (refused, reset, DNS, TLS) surface from the pool's
                // lease exactly as the real connection factory raises them.
                if (connectionFailures is { Count: > 0 })
                {
                    transactionClock?.Advance(transportFailureAdvanceMs);
                    return ValueTask.FromException<IKafkaConnection>(connectionFailures.Dequeue());
                }

                return new ValueTask<IKafkaConnection>(connection);
            });
        var brokers = new BrokerMetadata[brokerCount];
        for (var i = 0; i < brokerCount; i++)
        {
            brokers[i] = new BrokerMetadata { NodeId = i + 1, Host = "localhost", Port = 9092 + i };
            connectionPool.RegisterBroker(brokers[i].NodeId, brokers[i].Host, brokers[i].Port);
        }

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = brokers,
            Topics = []
        });
        metadataManager.SetApiVersion(
            ApiKey.FindCoordinator,
            FindCoordinatorRequest.LowestSupportedVersion,
            FindCoordinatorRequest.HighestSupportedVersion);
        metadataManager.SetApiVersion(
            ApiKey.AddPartitionsToTxn,
            AddPartitionsToTxnRequest.LowestSupportedVersion,
            AddPartitionsToTxnRequest.HighestSupportedVersion);
        metadataManager.SetApiVersion(
            ApiKey.EndTxn,
            EndTxnRequest.LowestSupportedVersion,
            EndTxnRequest.HighestSupportedVersion);
        metadataManager.SetApiVersion(
            ApiKey.InitProducerId,
            InitProducerIdRequest.LowestSupportedVersion,
            InitProducerIdRequest.HighestSupportedVersion);
        metadataManager.SetApiVersion(
            ApiKey.Metadata,
            MetadataRequest.LowestSupportedVersion,
            MetadataRequest.HighestSupportedVersion);
        PublishFinalizedTransactionVersion(metadataManager, transactionFeatureVersion);

        var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                TransactionalId = "test-txn-id",
                EnableTwoPhaseCommit = enableTwoPhaseCommit,
                RetryBackoffMs = retryBackoffMs,
                RetryBackoffMaxMs = retryBackoffMs,
                MaxBlockMs = maxBlockMs,
                CloseTimeoutMs = 100
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager,
            DekafMemoryBudget.Global,
            addPartitionsToTransaction: addPartitionsToTransaction,
            transactionTimestampProvider: transactionClock is null ? null : transactionClock.GetMilliseconds);

        SetInstanceField(producer, "_initialized", true);
        SetInstanceField(producer, "_producerId", currentProducerId);
        SetInstanceField(producer, "_producerEpoch", currentProducerEpoch);
        SetInstanceField(producer, "_transactionCoordinatorId", 1);
        SetInstanceField(producer, "_currentTransactionUsesTV2", transactionFeatureVersion >= 2);
        SetInstanceField(producer, "_currentTransactionFeatureVersion", transactionFeatureVersion);
        producer._preparedTransactionState = preparedState;
        producer._transactionState = TransactionState.PreparedTransaction;

        return new PreparedCompletionHarness(
            producer,
            connectionPool,
            metadataManager,
            connection,
            connectionAttempts,
            connectionAttemptBrokerIds,
            deadBrokerIds);
    }

    /// <summary>
    /// Enough failures to outlast any budget the tests configure; the fakes dequeue one per attempt.
    /// </summary>
    private static Queue<Exception> RepeatedFailures(Func<Exception> failure)
    {
        const int count = 64;
        var failures = new Queue<Exception>(count);
        for (var i = 0; i < count; i++)
            failures.Enqueue(failure());
        return failures;
    }

    public enum TransportFailureKind
    {
        ConnectionReset,
        ConnectionRefused,
        IoFailure,
        DnsFailure,
        SetupTimeout,
        RetiredConnection
    }

    private static Exception CreateTransportFailure(TransportFailureKind kind) => kind switch
    {
        TransportFailureKind.ConnectionReset => new SocketException((int)SocketError.ConnectionReset),
        TransportFailureKind.ConnectionRefused => new SocketException((int)SocketError.ConnectionRefused),
        TransportFailureKind.IoFailure => new IOException("Connection closed unexpectedly"),
        TransportFailureKind.DnsFailure => new DnsResolutionException(
            "coordinator", 9092, new SocketException((int)SocketError.HostNotFound)),
        TransportFailureKind.SetupTimeout => new TimeoutException("Connection setup timed out"),
        _ => new ObjectDisposedException("KafkaConnection")
    };

    private static void SetFinalizedTransactionVersion(KafkaProducer<string, string> producer, short version)
    {
        var metadataManager = GetInstanceField<MetadataManager>(producer, "_metadataManager");
        PublishFinalizedTransactionVersion(metadataManager, version);
    }

    private static long s_finalizedFeatureEpoch;

    private static void PublishFinalizedTransactionVersion(
        MetadataManager metadataManager,
        short version)
    {
        metadataManager.ObserveClusterCapabilities(
            "cluster-a",
            KafkaConnectionCapabilities.Create(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys = [],
                FinalizedFeaturesEpoch = Interlocked.Increment(ref s_finalizedFeatureEpoch),
                FinalizedFeatures =
                [
                    new FinalizedFeature("transaction.version", version, version)
                ]
            }));
    }

    private static void SetInstanceField<T>(object target, string name, T value)
    {
        const BindingFlags instanceFieldFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var field = target.GetType().GetField(name, instanceFieldFlags);
        field!.SetValue(target, value);
    }

    private static T GetInstanceField<T>(object target, string name)
    {
        const BindingFlags instanceFieldFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var field = target.GetType().GetField(name, instanceFieldFlags);
        return (T)field!.GetValue(target)!;
    }

    private sealed class PreparedCompletionHarness(
        KafkaProducer<string, string> producer,
        ConnectionPool connectionPool,
        MetadataManager metadataManager,
        LeaseTrackingConnection connection,
        StrongBox<int> connectionAttempts,
        List<int> connectionAttemptBrokerIds,
        HashSet<int> deadBrokerIds) : IAsyncDisposable
    {
        public KafkaProducer<string, string> Producer { get; } = producer;

        public int ConnectionAttempts => Volatile.Read(ref connectionAttempts.Value);

        public IReadOnlyList<BrokerNode> Brokers => metadataManager.Metadata.GetBrokers();

        public int CoordinatorNodeId
        {
            get => connection.CoordinatorNodeId;
            set => connection.CoordinatorNodeId = value;
        }

        /// <summary>Runs while the coordinator answers each EndTxn, before the answer returns.</summary>
        public Action? BeforeEndTxnResponse
        {
            set => connection.BeforeEndTxnResponse = value;
        }

        public int ConnectionAttemptsTo(int brokerId)
        {
            lock (connectionAttemptBrokerIds)
                return connectionAttemptBrokerIds.Count(id => id == brokerId);
        }

        /// <summary>Connection setup to this broker is refused from now on.</summary>
        public void KillBroker(int brokerId)
        {
            lock (connectionAttemptBrokerIds)
                deadBrokerIds.Add(brokerId);
        }

        public EndTxnRequest CapturedRequest => connection.CapturedEndTxnRequest
            ?? throw new InvalidOperationException("EndTxn request was not captured.");
        public int LeaseCountDuringRequest => connection.LeaseCountDuringRequest;
        public int LeaseCount => connection.LeaseCount;
        public int PipelinedResponseAbandonCalls => connection.PipelinedResponseAbandonCalls;
        public int FindCoordinatorRequests => connection.FindCoordinatorRequests;
        public int InitProducerIdRequests => connection.InitProducerIdRequests;
        public int AddPartitionsRequests => connection.AddPartitionsRequests;
        public int EndTxnRequests => connection.EndTxnRequests;
        public Task InitProducerIdStarted => connection.InitProducerIdStarted;
        public Task EndTxnStarted => connection.EndTxnStarted;

        public async ValueTask DisposeAsync()
        {
            await Producer.DisposeAsync().ConfigureAwait(false);
            await connectionPool.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class LeaseTrackingConnection(
        PreparedTransactionState preparedState,
        long producerId,
        short producerEpoch,
        ErrorCode endTxnError,
        int coordinatorRetriableFailuresBeforeSuccess,
        int initProducerIdRetriableFailuresBeforeSuccess,
        int addPartitionsRetriableFailuresBeforeSuccess,
        int endTxnRetriableFailuresBeforeSuccess,
        bool endTxnWaitsForCancellation,
        bool initProducerIdWaitsForCancellation,
        bool endTxnWaitsBeforeWriteForCancellation,
        bool initProducerIdWaitsBeforeWriteForCancellation,
        int findCoordinatorDelayMs,
        int emptyCoordinatorResponsesBeforeSuccess,
        ErrorCode addPartitionsRetriableError,
        FakeTransactionClock? transactionClock,
        int findCoordinatorAdvanceMs,
        int initProducerIdAdvanceMs,
        Queue<Exception>? findCoordinatorFailures,
        Queue<Exception>? initProducerIdFailures,
        Queue<Exception>? addPartitionsFailures,
        Queue<Exception>? endTxnFailures,
        bool endTxnFailsAfterWrite,
        int transportFailureAdvanceMs,
        bool initProducerIdFailsAfterWrite,
        int endTxnAdvanceMs,
        ErrorCode findCoordinatorError,
        bool endTxnFailsAfterCancellation,
        bool findCoordinatorFailsAfterCancellation) : IKafkaConnection, IRetirableKafkaConnection,
        IKafkaPipelinedWriteCompletionConnection, IKafkaRequestWriteObserverConnection
    {
        /// <summary>
        /// Dequeues an injected transport failure for the request, counting the attempt the way
        /// a real connection would have counted a request that was written and then lost.
        /// </summary>
        private bool TryTakeTransportFailure<TRequest>(TRequest request, out Exception failure)
        {
            var failures = request switch
            {
                FindCoordinatorRequest => findCoordinatorFailures,
                InitProducerIdRequest => initProducerIdFailures,
                AddPartitionsToTxnRequest => addPartitionsFailures,
                EndTxnRequest => endTxnFailures,
                _ => null
            };

            if (failures is not { Count: > 0 })
            {
                failure = null!;
                return false;
            }

            switch (request)
            {
                case FindCoordinatorRequest:
                    FindCoordinatorRequests++;
                    break;
                case InitProducerIdRequest:
                    InitProducerIdRequests++;
                    break;
                case AddPartitionsToTxnRequest:
                    AddPartitionsRequests++;
                    break;
                case EndTxnRequest endTxnRequest:
                    EndTxnRequests++;
                    CapturedEndTxnRequest = endTxnRequest;
                    _endTxnStarted.TrySetResult();
                    break;
            }

            transactionClock?.Advance(transportFailureAdvanceMs);
            failure = failures.Dequeue();
            return true;
        }

        private readonly TrackingResponseSource<EndTxnResponse> _pipelinedResponseSource = new();
        private int _leaseCount;
        private int _leaseCountDuringRequest = -1;

        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public int CoordinatorNodeId { get; set; } = 1;
        public EndTxnRequest? CapturedEndTxnRequest { get; private set; }
        public int LeaseCount => Volatile.Read(ref _leaseCount);
        public int LeaseCountDuringRequest => Volatile.Read(ref _leaseCountDuringRequest);
        public int ActiveOperationCount => 0;
        public int PipelinedResponseAbandonCalls => _pipelinedResponseSource.AbandonCalls;
        public int FindCoordinatorRequests { get; private set; }
        public int InitProducerIdRequests { get; private set; }
        public int AddPartitionsRequests { get; private set; }
        public int EndTxnRequests { get; private set; }
        public Task InitProducerIdStarted => _initProducerIdStarted.Task;
        public Task EndTxnStarted => _endTxnStarted.Task;

        private readonly TaskCompletionSource _initProducerIdStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _endTxnStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool TryAcquireLease()
        {
            Interlocked.Increment(ref _leaseCount);
            return true;
        }

        public void ReleaseLease() => Interlocked.Decrement(ref _leaseCount);
        public void BeginRetirement() { }
        public void CompleteRetirement() { }
        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            Volatile.Write(ref _leaseCountDuringRequest, LeaseCount);
            if (request is InitProducerIdRequest && initProducerIdWaitsForCancellation)
            {
                InitProducerIdRequests++;
                _initProducerIdStarted.TrySetResult();
                return WaitForCancellationAsync<TResponse>(cancellationToken);
            }

            if (request is EndTxnRequest waitingEndTxnRequest && endTxnWaitsForCancellation)
            {
                EndTxnRequests++;
                CapturedEndTxnRequest = waitingEndTxnRequest;
                _endTxnStarted.TrySetResult();
                return WaitForCancellationAsync<TResponse>(cancellationToken);
            }

            if (request is FindCoordinatorRequest && findCoordinatorFailsAfterCancellation)
            {
                FindCoordinatorRequests++;
                return FailAfterCancellationAsync<TResponse>(cancellationToken);
            }

            if (request is FindCoordinatorRequest delayedFindCoordinatorRequest
                && findCoordinatorDelayMs > 0)
            {
                return CreateDelayedFindCoordinatorResponseAsync<TResponse>(
                    delayedFindCoordinatorRequest,
                    cancellationToken);
            }

            if (request is not (InitProducerIdRequest or EndTxnRequest)
                && TryTakeTransportFailure(request, out var transportFailure))
            {
                return ValueTask.FromException<TResponse>(transportFailure);
            }

            IKafkaResponse response = request switch
            {
                EndTxnRequest endTxnRequest => CreateEndTxnResponse(endTxnRequest),
                InitProducerIdRequest => CreateInitProducerIdResponse(),
                FindCoordinatorRequest findCoordinatorRequest => CreateFindCoordinatorResponse(findCoordinatorRequest),
                AddPartitionsToTxnRequest addPartitionsRequest => CreateAddPartitionsResponse(addPartitionsRequest),
                // Transport-failure retries refresh metadata between attempts.
                MetadataRequest => new MetadataResponse
                {
                    Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
                    Topics = []
                },
                _ => throw new NotSupportedException()
            };

            return ValueTask.FromResult((TResponse)response);
        }

        public ValueTask<TResponse> SendWithWriteObservationAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            Action requestWriteStarted,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            if (request is InitProducerIdRequest && initProducerIdWaitsBeforeWriteForCancellation)
            {
                InitProducerIdRequests++;
                _initProducerIdStarted.TrySetResult();
                return WaitForCancellationAsync<TResponse>(cancellationToken);
            }

            if (request is EndTxnRequest endTxnRequest && endTxnWaitsBeforeWriteForCancellation)
            {
                EndTxnRequests++;
                CapturedEndTxnRequest = endTxnRequest;
                _endTxnStarted.TrySetResult();
                return WaitForCancellationAsync<TResponse>(cancellationToken);
            }

            if (request is EndTxnRequest lateFailingEndTxnRequest && endTxnFailsAfterCancellation)
            {
                EndTxnRequests++;
                CapturedEndTxnRequest = lateFailingEndTxnRequest;
                _endTxnStarted.TrySetResult();
                return FailAfterCancellationAsync<TResponse>(cancellationToken);
            }

            // Write-observed requests fail either before the write started (connection setup or
            // a reset on the first byte) or after it (response never arrives), which decides how
            // the producer must treat an exhausted retry budget.
            var failsAfterWrite = request is EndTxnRequest && endTxnFailsAfterWrite
                || request is InitProducerIdRequest && initProducerIdFailsAfterWrite;
            if (!failsAfterWrite && TryTakeTransportFailure(request, out var failureBeforeWrite))
                return ValueTask.FromException<TResponse>(failureBeforeWrite);

            requestWriteStarted();
            if (failsAfterWrite && TryTakeTransportFailure(request, out var failureAfterWrite))
                return ValueTask.FromException<TResponse>(failureAfterWrite);

            return SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
        }

        private static async ValueTask<TResponse> WaitForCancellationAsync<TResponse>(
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return default!;
        }

        /// <summary>
        /// A socket that reports its failure only after the request's token has already fired:
        /// the retry loop then observes the transport exception, not an
        /// <see cref="OperationCanceledException"/>, with the budget (or the caller) already cancelled.
        /// </summary>
        private static async ValueTask<TResponse> FailAfterCancellationAsync<TResponse>(
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            throw new SocketException((int)SocketError.ConnectionReset);
        }

        private async ValueTask<TResponse> CreateDelayedFindCoordinatorResponseAsync<TResponse>(
            FindCoordinatorRequest request,
            CancellationToken cancellationToken)
            where TResponse : IKafkaResponse
        {
            await Task.Delay(findCoordinatorDelayMs, cancellationToken).ConfigureAwait(false);
            return (TResponse)(IKafkaResponse)CreateFindCoordinatorResponse(request);
        }

        private FindCoordinatorResponse CreateFindCoordinatorResponse(FindCoordinatorRequest request)
        {
            FindCoordinatorRequests++;
            transactionClock?.Advance(findCoordinatorAdvanceMs);
            if (FindCoordinatorRequests <= emptyCoordinatorResponsesBeforeSuccess)
                return new FindCoordinatorResponse { Coordinators = [] };

            return new FindCoordinatorResponse
            {
                Coordinators =
                [
                    new Coordinator
                    {
                        Key = request.Key,
                        NodeId = CoordinatorNodeId,
                        Host = "localhost",
                        Port = 9092 + CoordinatorNodeId - 1,
                        ErrorCode = FindCoordinatorRequests <= coordinatorRetriableFailuresBeforeSuccess
                            ? ErrorCode.CoordinatorNotAvailable
                            : findCoordinatorError
                    }
                ]
            };
        }

        private InitProducerIdResponse CreateInitProducerIdResponse()
        {
            InitProducerIdRequests++;
            transactionClock?.Advance(initProducerIdAdvanceMs);
            return new InitProducerIdResponse
            {
                ErrorCode = InitProducerIdRequests <= initProducerIdRetriableFailuresBeforeSuccess
                    ? ErrorCode.CoordinatorLoadInProgress
                    : ErrorCode.None,
                ProducerId = producerId,
                ProducerEpoch = producerEpoch
            };
        }

        private AddPartitionsToTxnResponse CreateAddPartitionsResponse(AddPartitionsToTxnRequest request)
        {
            AddPartitionsRequests++;
            var errorCode = AddPartitionsRequests <= addPartitionsRetriableFailuresBeforeSuccess
                ? addPartitionsRetriableError
                : ErrorCode.None;
            return new AddPartitionsToTxnResponse
            {
                Results = request.Topics.Select(topic => new AddPartitionsToTxnTopicResult
                {
                    Name = topic.Name,
                    Partitions = topic.Partitions.Select(partition => new AddPartitionsToTxnPartitionResult
                    {
                        PartitionIndex = partition,
                        ErrorCode = errorCode
                    }).ToArray()
                }).ToArray()
            };
        }

        public Action? BeforeEndTxnResponse { get; set; }

        private EndTxnResponse CreateEndTxnResponse(EndTxnRequest request)
        {
            BeforeEndTxnResponse?.Invoke();
            EndTxnRequests++;
            CapturedEndTxnRequest = request;
            transactionClock?.Advance(endTxnAdvanceMs);
            return new EndTxnResponse
            {
                ErrorCode = EndTxnRequests <= endTxnRetriableFailuresBeforeSuccess
                    ? ErrorCode.CoordinatorLoadInProgress
                    : endTxnError,
                ProducerId = preparedState.ProducerId,
                ProducerEpoch = (short)(preparedState.ProducerEpoch + 1)
            };
        }

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public ValueTask<PipelinedResponse<TResponse>> SendPipelinedAfterWriteAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            if (request is not EndTxnRequest endTxnRequest)
                throw new NotSupportedException();

            Volatile.Write(ref _leaseCountDuringRequest, LeaseCount);
            _pipelinedResponseSource.SetResult(CreateEndTxnResponse(endTxnRequest));
            return ValueTask.FromResult(new PipelinedResponse<TResponse>(
                (IPipelinedResponseSource<TResponse>)(object)_pipelinedResponseSource,
                token: 0));
        }

        public ValueTask<PipelinedResponse<TResponse>>
            SendPipelinedWithWriteObservationAfterWriteAsync<TRequest, TResponse>(
                TRequest request,
                short apiVersion,
                Action requestWriteStarted,
                CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            requestWriteStarted();
            return SendPipelinedAfterWriteAsync<TRequest, TResponse>(
                request,
                apiVersion,
                cancellationToken);
        }

        public ValueTask<PipelinedResponse<TResponse>>
            SendPipelinedWithCallerTimeoutAfterWriteAsync<TRequest, TResponse>(
                TRequest request,
                short apiVersion,
                CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();
    }

    private sealed class FakeTransactionClock
    {
        private long _milliseconds;

        public long GetMilliseconds() => Volatile.Read(ref _milliseconds);

        public void Advance(int milliseconds) => Interlocked.Add(ref _milliseconds, milliseconds);
    }

    private sealed class TrackingResponseSource<TResponse> : IPipelinedResponseSource<TResponse>
    {
        private TResponse? _response;

        public int AbandonCalls { get; private set; }

        public void SetResult(TResponse response) => _response = response;

        public TResponse GetResult(short token) => _response!;

        public ValueTaskSourceStatus GetStatus(short token) => ValueTaskSourceStatus.Succeeded;

        public void OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags) => continuation(state);

        public void Abandon(short token) => AbandonCalls++;
    }
}
