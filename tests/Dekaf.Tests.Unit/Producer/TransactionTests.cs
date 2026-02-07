using Dekaf.Producer;

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
    public async Task InitTransactionsAsync_WithoutTransactionalId_Throws()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        var act = () => producer.InitTransactionsAsync().AsTask();
        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task TransactionState_Enum_HasExpectedValues()
    {
        // Verify enum values exist and are distinct
        var values = Enum.GetValues<TransactionState>();
        await Assert.That(values).Count().IsEqualTo(6);
        await Assert.That(values).Contains(TransactionState.Uninitialized);
        await Assert.That(values).Contains(TransactionState.Ready);
        await Assert.That(values).Contains(TransactionState.InTransaction);
        await Assert.That(values).Contains(TransactionState.CommittingTransaction);
        await Assert.That(values).Contains(TransactionState.AbortingTransaction);
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
    public async Task WithTransactionalId_CanChainWithAcks()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("test-txn-id")
            .WithAcks(Dekaf.Producer.Acks.All)
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
        await Assert.That(methodNames).Contains("AbortAsync");
        await Assert.That(methodNames).Contains("SendOffsetsToTransactionAsync");
    }

    [Test]
    public async Task TopicPartitionOffset_RecordStruct_HasExpectedProperties()
    {
        var tpo = new Dekaf.Producer.TopicPartitionOffset("test-topic", 0, 42);

        await Assert.That(tpo.Topic).IsEqualTo("test-topic");
        await Assert.That(tpo.Partition).IsEqualTo(0);
        await Assert.That(tpo.Offset).IsEqualTo(42L);
    }

    [Test]
    public async Task TopicPartitionOffset_Equality()
    {
        var tpo1 = new Dekaf.Producer.TopicPartitionOffset("topic", 1, 100);
        var tpo2 = new Dekaf.Producer.TopicPartitionOffset("topic", 1, 100);
        var tpo3 = new Dekaf.Producer.TopicPartitionOffset("topic", 2, 100);

        await Assert.That(tpo1).IsEqualTo(tpo2);
        await Assert.That(tpo1).IsNotEqualTo(tpo3);
    }
}
