using System.Reflection;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

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

        ((KafkaProducer<string, string>)producer)._transactionState = TransactionState.FatalError;

        var act = () => producer.BeginTransaction();
        await Assert.That(act).Throws<InvalidOperationException>();
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
            await producer.CompletePreparedTransactionAsync(PreparedTransactionState.Empty);
        }).Throws<ArgumentException>();
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
}
