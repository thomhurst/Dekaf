using System.Diagnostics;
using System.Reflection;
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
            initProducerIdRetriableFailuresBeforeSuccess: 10);
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
            emptyCoordinatorResponsesBeforeSuccess: 1);
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
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            initProducerIdWaitsForCancellation: true,
            findCoordinatorDelayMs: 1200,
            maxBlockMs: 2000);
        harness.Producer._transactionState = TransactionState.Uninitialized;
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.That(() => harness.Producer.InitTransactionsAsync(
                cancellationToken).AsTask())
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromMilliseconds(2800));
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
    public async Task EndTransactionAsync_RetriesBeyondPreviousAttemptLimit()
    {
        await using var harness = BuildPreparedCompletionHarness(
            PreparedTransactionState.Empty,
            currentProducerId: 2002,
            currentProducerEpoch: 9,
            endTxnRetriableFailuresBeforeSuccess: 5);

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
            addPartitionsRetriableFailuresBeforeSuccess: 5);

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
            addPartitionsRetriableError: ErrorCode.NotCoordinator);

        await harness.Producer.AddPartitionsToTransactionAsync(
            [new TopicPartition("orders", 0), new TopicPartition("orders", 1)],
            CancellationToken.None);

        await Assert.That(harness.AddPartitionsRequests).IsEqualTo(2);
        await Assert.That(harness.FindCoordinatorRequests).IsEqualTo(1);
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
    public async Task TransactionPartitionEnrollment_BatchesCoalescedPartitions()
    {
        var requestStarted = new TaskCompletionSource<IReadOnlyList<TopicPartition>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completeRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        var failNextEnrollment = 0;

        async ValueTask AddPartitions(
            IReadOnlyList<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref requestCount) == 1)
                throw new IOException("Transient connection failure");
            if (Interlocked.Exchange(ref failNextEnrollment, 0) == 1)
                throw new TransactionException("Partition enrollment failed.");

            requestStarted.TrySetResult([.. partitions]);
            await completeRequest.Task.WaitAsync(cancellationToken);
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
        var requestedPartitions = await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.That(requestCount).IsEqualTo(2);
        await Assert.That(requestedPartitions).IsEquivalentTo(new[]
        {
            new TopicPartition("topic-a", 0),
            new TopicPartition("topic-a", 1),
            new TopicPartition("topic-b", 0)
        });

        completeRequest.SetResult();
        await Assert.That(await enrollmentCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1))).IsNull();
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
        await Assert.That(await mixedEnrollmentCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1))).IsNull();

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
        await Assert.That(await failedEnrollmentCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1))).IsNull();

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
        await Assert.That(await unrelatedEnrollmentCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1))).IsNull();
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
        int findCoordinatorDelayMs = 0,
        int emptyCoordinatorResponsesBeforeSuccess = 0,
        ErrorCode addPartitionsRetriableError = ErrorCode.CoordinatorLoadInProgress,
        int retryBackoffMs = 0,
        int maxBlockMs = 1000)
    {
        var connection = new LeaseTrackingConnection(
            preparedState,
            currentProducerId,
            currentProducerEpoch,
            endTxnError,
            coordinatorRetriableFailuresBeforeSuccess,
            initProducerIdRetriableFailuresBeforeSuccess,
            addPartitionsRetriableFailuresBeforeSuccess,
            endTxnRetriableFailuresBeforeSuccess,
            endTxnWaitsForCancellation,
            initProducerIdWaitsForCancellation,
            findCoordinatorDelayMs,
            emptyCoordinatorResponsesBeforeSuccess,
            addPartitionsRetriableError);

        var connectionPool = new ConnectionPool(
            clientId: "test-producer",
            connectionOptions: null,
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) => new ValueTask<IKafkaConnection>(connection));
        connectionPool.RegisterBroker(1, "localhost", 9092);

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
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
            DekafMemoryBudget.Global);

        SetInstanceField(producer, "_initialized", true);
        SetInstanceField(producer, "_producerId", currentProducerId);
        SetInstanceField(producer, "_producerEpoch", currentProducerEpoch);
        SetInstanceField(producer, "_transactionCoordinatorId", 1);
        SetInstanceField(producer, "_currentTransactionUsesTV2", transactionFeatureVersion >= 2);
        SetInstanceField(producer, "_currentTransactionFeatureVersion", transactionFeatureVersion);
        producer._preparedTransactionState = preparedState;
        producer._transactionState = TransactionState.PreparedTransaction;

        return new PreparedCompletionHarness(producer, connectionPool, connection);
    }

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
        LeaseTrackingConnection connection) : IAsyncDisposable
    {
        public KafkaProducer<string, string> Producer { get; } = producer;

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
        int findCoordinatorDelayMs,
        int emptyCoordinatorResponsesBeforeSuccess,
        ErrorCode addPartitionsRetriableError) : IKafkaConnection, IRetirableKafkaConnection,
        IKafkaPipelinedWriteCompletionConnection
    {
        private readonly TrackingResponseSource<EndTxnResponse> _pipelinedResponseSource = new();
        private int _leaseCount;
        private int _leaseCountDuringRequest = -1;

        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
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

            if (request is FindCoordinatorRequest delayedFindCoordinatorRequest
                && findCoordinatorDelayMs > 0)
            {
                return CreateDelayedFindCoordinatorResponseAsync<TResponse>(
                    delayedFindCoordinatorRequest,
                    cancellationToken);
            }

            IKafkaResponse response = request switch
            {
                EndTxnRequest endTxnRequest => CreateEndTxnResponse(endTxnRequest),
                InitProducerIdRequest => CreateInitProducerIdResponse(),
                FindCoordinatorRequest findCoordinatorRequest => CreateFindCoordinatorResponse(findCoordinatorRequest),
                AddPartitionsToTxnRequest addPartitionsRequest => CreateAddPartitionsResponse(addPartitionsRequest),
                _ => throw new NotSupportedException()
            };

            return ValueTask.FromResult((TResponse)response);
        }

        private static async ValueTask<TResponse> WaitForCancellationAsync<TResponse>(
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return default!;
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
            if (FindCoordinatorRequests <= emptyCoordinatorResponsesBeforeSuccess)
                return new FindCoordinatorResponse { Coordinators = [] };

            return new FindCoordinatorResponse
            {
                Coordinators =
                [
                    new Coordinator
                    {
                        Key = request.Key,
                        NodeId = 1,
                        Host = "localhost",
                        Port = 9092,
                        ErrorCode = FindCoordinatorRequests <= coordinatorRetriableFailuresBeforeSuccess
                            ? ErrorCode.CoordinatorNotAvailable
                            : ErrorCode.None
                    }
                ]
            };
        }

        private InitProducerIdResponse CreateInitProducerIdResponse()
        {
            InitProducerIdRequests++;
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

        private EndTxnResponse CreateEndTxnResponse(EndTxnRequest request)
        {
            EndTxnRequests++;
            CapturedEndTxnRequest = request;
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
            SendPipelinedWithCallerTimeoutAfterWriteAsync<TRequest, TResponse>(
                TRequest request,
                short apiVersion,
                CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();
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
