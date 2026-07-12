using System.Reflection;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
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
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id")
            .Build();

        SetInstanceField(producer, "_initialized", true);

        await Assert.That(async () =>
        {
            await producer.InitTransactionsAsync(keepPreparedTransaction: true);
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
    [Arguments(true, (short)12, false)]
    [Arguments(true, (short)11, true)]
    [Arguments(false, (short)12, true)]
    public async Task EnsurePartitionInTransaction_UsesCompatibleEnrollment(
        bool usesTV2,
        short produceApiVersion,
        bool expectsExplicitEnrollment)
    {
        var (producer, connectionPool, connection) = CreatePartitionEnrollmentProducer(
            usesTV2,
            produceApiVersion);
        try
        {
            var topicPartition = new TopicPartition("test-topic", 3);

            await InvokeEnsurePartitionInTransactionAsync(producer, topicPartition);

            if (expectsExplicitEnrollment)
            {
                _ = connection.Received(1).SendAsync<AddPartitionsToTxnRequest, AddPartitionsToTxnResponse>(
                    Arg.Is<AddPartitionsToTxnRequest>(request => HasSinglePartition(request, topicPartition)),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>());
            }
            else
            {
                _ = connection.DidNotReceive().SendAsync<AddPartitionsToTxnRequest, AddPartitionsToTxnResponse>(
                    Arg.Any<AddPartitionsToTxnRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>());
            }

            await Assert.That(producer._partitionsInTransaction.Contains(topicPartition)).IsTrue();
        }
        finally
        {
            await producer.DisposeAsync();
            await connectionPool.DisposeAsync();
        }
    }

    private static async ValueTask InvokeEnsurePartitionInTransactionAsync(
        KafkaProducer<string, string> producer,
        TopicPartition topicPartition)
    {
        var method = typeof(KafkaProducer<string, string>).GetMethod(
            "EnsurePartitionInTransactionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var invocation = (ValueTask)method.Invoke(
            producer,
            [topicPartition, CancellationToken.None])!;
        await invocation;
    }

    private static bool HasSinglePartition(
        AddPartitionsToTxnRequest? request,
        TopicPartition topicPartition)
        => request?.Topics is [{ } topic]
           && topic.Name == topicPartition.Topic
           && topic.Partitions.Count == 1
           && topic.Partitions[0] == topicPartition.Partition;

    private static (KafkaProducer<string, string> Producer, ConnectionPool ConnectionPool,
        IKafkaConnection Connection)
        CreatePartitionEnrollmentProducer(bool usesTV2, short produceApiVersion)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.SendAsync<AddPartitionsToTxnRequest, AddPartitionsToTxnResponse>(
                Arg.Any<AddPartitionsToTxnRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(new AddPartitionsToTxnResponse
            {
                Results =
                [
                    new AddPartitionsToTxnTopicResult
                    {
                        Name = "test-topic",
                        Partitions =
                        [
                            new AddPartitionsToTxnPartitionResult
                            {
                                PartitionIndex = 3,
                                ErrorCode = ErrorCode.None
                            }
                        ]
                    }
                ]
            });

        var connectionPool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: null,
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) => ValueTask.FromResult(connection));
        connectionPool.RegisterBroker(1, "localhost", 9092);
        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.AddPartitionsToTxn,
            AddPartitionsToTxnRequest.LowestSupportedVersion,
            AddPartitionsToTxnRequest.HighestSupportedVersion);
        var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                TransactionalId = "test-txn-id",
                CloseTimeoutMs = 100
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager,
            DekafMemoryBudget.Global);
        SetInstanceField(producer, "_initialized", true);
        SetInstanceField(producer, "_producerId", 42L);
        SetInstanceField(producer, "_producerEpoch", (short)5);
        SetInstanceField(producer, "_transactionCoordinatorId", 1);
        SetInstanceField(producer, "_currentTransactionUsesTV2", usesTV2);
        SetInstanceField(producer, "_produceApiVersion", (int)produceApiVersion);
        producer._transactionState = TransactionState.InTransaction;
        return (producer, connectionPool, connection);
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
        producer._transactionState = TransactionState.Ready;
        return producer;
    }

    private static PreparedCompletionHarness BuildPreparedCompletionHarness(
        PreparedTransactionState preparedState,
        long currentProducerId,
        short currentProducerEpoch,
        ErrorCode endTxnError = ErrorCode.None)
    {
        var connection = new LeaseTrackingConnection(
            preparedState,
            currentProducerId,
            currentProducerEpoch,
            endTxnError);

        var connectionPool = new ConnectionPool(
            clientId: "test-producer",
            connectionOptions: null,
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) => new ValueTask<IKafkaConnection>(connection));
        connectionPool.RegisterBroker(1, "localhost", 9092);

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.EndTxn,
            EndTxnRequest.LowestSupportedVersion,
            EndTxnRequest.HighestSupportedVersion);
        metadataManager.SetApiVersion(
            ApiKey.InitProducerId,
            InitProducerIdRequest.LowestSupportedVersion,
            InitProducerIdRequest.HighestSupportedVersion);

        var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                TransactionalId = "test-txn-id",
                EnableTwoPhaseCommit = true,
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
        SetInstanceField(producer, "_currentTransactionUsesTV2", true);
        producer._preparedTransactionState = preparedState;
        producer._transactionState = TransactionState.PreparedTransaction;

        return new PreparedCompletionHarness(producer, connectionPool, connection);
    }

    private static void SetFinalizedTransactionVersion(KafkaProducer<string, string> producer, short version)
    {
        var metadataManager = GetInstanceField<object>(producer, "_metadataManager");
        SetInstanceField<IReadOnlyList<FinalizedFeature>>(
            metadataManager,
            "_finalizedFeatures",
            [new FinalizedFeature("transaction.version", version, version)]);
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
        ErrorCode endTxnError) : IKafkaConnection, IRetirableKafkaConnection
    {
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
            IKafkaResponse response = request switch
            {
                EndTxnRequest endTxnRequest => CreateEndTxnResponse(endTxnRequest),
                InitProducerIdRequest => new InitProducerIdResponse
                {
                    ErrorCode = ErrorCode.None,
                    ProducerId = producerId,
                    ProducerEpoch = producerEpoch
                },
                _ => throw new NotSupportedException()
            };

            return ValueTask.FromResult((TResponse)response);
        }

        private EndTxnResponse CreateEndTxnResponse(EndTxnRequest request)
        {
            CapturedEndTxnRequest = request;
            return new EndTxnResponse
            {
                ErrorCode = endTxnError,
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
    }
}
