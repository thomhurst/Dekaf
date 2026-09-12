using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol;
using Testcontainers.Kafka;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
public sealed class TransactionListingIntegrationTests(KafkaTestContainer kafka) : TransactionalKafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ListTransactions_FindsOngoingTransactionAndItsCompletedState(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var transactionId = $"listing-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(transactionId).WithAcks(Acks.All).BuildAsync(cancellationToken);
        await producer.InitTransactionsAsync(cancellationToken);
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic, Key = "key", Value = "value"
        }, cancellationToken);
        await using var admin = KafkaContainer.CreateAdminClient();
        var ongoing = await admin.ListTransactionsAsync(new ListTransactionsOptions
        {
            StateFilters = ["Ongoing"]
        }, cancellationToken);
        var listed = ongoing.Transactions.Single(item => item.TransactionalId == transactionId);
        await Assert.That(listed.TransactionState).IsEqualTo("Ongoing");
        var described = (await admin.DescribeTransactionsAsync([transactionId], cancellationToken))[transactionId];
        await Assert.That(listed.ProducerId).IsEqualTo(described.ProducerId);
        await Assert.That(listed.CoordinatorId).IsEqualTo(described.CoordinatorId);

        await transaction.CommitAsync(cancellationToken);
        await TestWait.WaitForConditionAsync(async () =>
        {
            var result = await admin.ListTransactionsAsync(new ListTransactionsOptions
            {
                ProducerIdFilters = [listed.ProducerId], StateFilters = ["CompleteCommit"]
            }, cancellationToken);
            return result.Transactions.Any(item => item.TransactionalId == transactionId);
        }, static ready => ready, description: "committed transaction appears in ListTransactions");
        var remaining = await admin.ListTransactionsAsync(new ListTransactionsOptions
        {
            ProducerIdFilters = [listed.ProducerId], StateFilters = ["Ongoing"]
        }, cancellationToken);
        await Assert.That(remaining.Transactions).IsEmpty();
    }
}

public sealed class MultiLogDirKafkaContainer : KafkaContainerDefault
{
    protected override KafkaBuilder ConfigureBuilder(KafkaBuilder builder) => base.ConfigureBuilder(builder)
        .WithEnvironment("KAFKA_LOG_DIRS", "/tmp/dekaf-log-a,/tmp/dekaf-log-b");
}

[Category("Admin")]
[ClassDataSource<MultiLogDirKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ReplicaLogDirMovementIntegrationTests(MultiLogDirKafkaContainer kafka)
{
    [Test]
    public async Task AlterReplicaLogDirs_MovesStoredRecordsAndPreservesSubsequentDelivery(CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers).BuildAsync(cancellationToken);
        await producer.ProduceAsync(topic, "before", "before", cancellationToken);
        await using var admin = kafka.CreateAdminClient();
        var description = (await admin.DescribeTopicsAsync([topic], cancellationToken))[topic];
        var replica = new TopicPartitionReplica(topic, 0, description.Partitions.Single().LeaderId);
        var current = (await admin.DescribeReplicaLogDirsAsync([replica], cancellationToken: cancellationToken))[replica];
        await Assert.That(current.CurrentReplicaLogDir).IsNotNull();
        var target = current.CurrentReplicaLogDir == "/tmp/dekaf-log-a" ? "/tmp/dekaf-log-b" : "/tmp/dekaf-log-a";

        var altered = await admin.AlterReplicaLogDirsAsync(new Dictionary<TopicPartitionReplica, string>
        {
            [replica] = target
        }, cancellationToken);
        await Assert.That(altered[replica].ErrorCode).IsEqualTo(ErrorCode.None);
        await TestWait.WaitForConditionAsync(async () =>
        {
            var result = (await admin.DescribeReplicaLogDirsAsync([replica], cancellationToken: cancellationToken))[replica];
            return result.CurrentReplicaLogDir == target && result.FutureReplicaLogDir is null;
        }, static ready => ready, description: "replica finishes moving to the requested directory");

        await producer.ProduceAsync(topic, "after", "after", cancellationToken);
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers).WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync(cancellationToken);
        consumer.Assign(new TopicPartition(topic, 0));
        foreach (var expected in new[] { "before", "after" })
        {
            var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await Assert.That(record).IsNotNull();
            await Assert.That(record!.Value.Value).IsEqualTo(expected);
        }
    }
}
