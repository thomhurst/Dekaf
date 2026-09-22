using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for KIP-1050 standardized transaction error handling: verifying that a
/// fenced producer surfaces a <see cref="FatalTransactionException"/> and becomes unusable.
/// </summary>
/// <remarks>
/// The fence reaches a producer on two paths: the transaction coordinator (EndTxn, covered by the
/// first test, whose produce completes before the fence) and the partition leader (a produce after
/// the fence, covered by the second). The abortable path is covered deterministically at the unit
/// level (state-machine transitions in <c>TransactionTests</c>, produce-response handling in
/// <c>BrokerSenderSendLoopTests</c> and classification in <c>TransactionErrorClassifierTests</c>).
/// </remarks>
[Category("Transaction")]
public class TransactionErrorHandlingTests(KafkaTestContainer kafka) : TransactionalKafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Transaction_FencedByNewerProducer_AllOperationsFailFatalAndWritesStayInvisible()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-fence-{Guid.NewGuid():N}";
        var consumerGroupId = $"txn-fence-group-{Guid.NewGuid():N}";

        await using var producer1 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer1.InitTransactionsAsync();

        await using var txn1 = producer1.BeginTransaction();

        // Produce and await delivery so AddPartitionsToTxn + Produce complete with the current
        // epoch. This makes the fence below observable only on EndTxn — a deterministic path.
        await txn1.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "fenced-key",
            Value = "fenced-value"
        }, CancellationToken.None);

        // A second producer with the same transactional id bumps the epoch, fencing producer1.
        await using var producer2 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer2.InitTransactionsAsync();

        // producer1's commit (EndTxn) detects the fence and transitions the producer to a
        // permanent fatal state.
        await AssertFencedAsync(() => txn1.CommitAsync().AsTask(), txnId);

        // Every transaction operation must now fail locally with the same fatal error. None may
        // overwrite the fatal state or attempt to resurrect the producer with a new epoch.
        await AssertFencedAsync(() => txn1.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "post-fence-key",
            Value = "post-fence-value"
        }).AsTask(), txnId);

        await AssertFencedAsync(() => txn1.SendOffsetsToTransactionAsync(
            [new TopicPartitionOffset(topic, 0, 1)], consumerGroupId).AsTask(), txnId);

        await AssertFencedAsync(() => txn1.PrepareAsync().AsTask(), txnId);
        await AssertFencedAsync(() => txn1.CommitAsync().AsTask(), txnId);
        await AssertFencedAsync(() => txn1.AbortAsync().AsTask(), txnId);
        await AssertFencedAsync(() => producer1.InitTransactionsAsync().AsTask(), txnId);

        var beginException = await Assert.That(() => producer1.BeginTransaction())
            .Throws<FatalTransactionException>();

        await Assert.That(beginException!.ErrorCode).IsEqualTo(ErrorCode.ProducerFenced);
        await Assert.That(beginException.TransactionalId).IsEqualTo(txnId);

        // The replacement producer remains healthy and can commit with the newer epoch.
        await using (var txn2 = producer2.BeginTransaction())
        {
            await txn2.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "replacement-key",
                Value = "replacement-value"
            });
            await txn2.CommitAsync();
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-fence-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var visible = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

        await Assert.That(visible).IsNotNull();
        var visibleMessage = visible!.Value;
        await Assert.That(visibleMessage.Key).IsEqualTo("replacement-key");
        await Assert.That(visibleMessage.Value).IsEqualTo("replacement-value");

        var unexpected = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts.Token);
        await Assert.That(unexpected).IsNull();
    }

    /// <summary>
    /// A fenced producer's next produce reaches the partition leader, which rejects the old epoch.
    /// That rejection must fail the produce promptly with a transaction error (not a delivery
    /// timeout after retrying the same stamp), stop the commit, and the abort that follows must
    /// report the fence.
    /// </summary>
    [Test]
    public async Task Transaction_FencedProducerProduces_FailsPromptlyAndCommitIsRefused()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-fence-produce-{Guid.NewGuid():N}";

        await using var producer1 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer1.InitTransactionsAsync();

        await using var txn1 = producer1.BeginTransaction();

        // Enroll the partition and put producer1's current epoch on it before the fence.
        await txn1.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Partition = 0,
            Key = "before-fence",
            Value = "before-fence"
        }, CancellationToken.None);

        await using var producer2 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer2.InitTransactionsAsync();

        // The default delivery timeout is two minutes; the old behaviour retried the fenced
        // stamp for that long. A prompt failure answers well inside this bound.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var produceException = await Assert.That(() => txn1.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Partition = 0,
            Key = "after-fence",
            Value = "after-fence"
        }).AsTask()).Throws<KafkaException>();
        stopwatch.Stop();

        // The broker answers a fenced epoch with ProducerFenced or InvalidProducerEpoch (both
        // surface as transaction exceptions), or, under transaction verification, with another
        // non-retriable code; none of them may turn into a delivery timeout.
        await Assert.That(produceException).IsNotTypeOf<KafkaTimeoutException>();
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(30));

        await Assert.That(() => txn1.CommitAsync().AsTask()).Throws<TransactionException>();

        // ProducerFenced on the produce is fatal at once; InvalidProducerEpoch is abortable
        // (Java parity) and the abort's EndTxn then reports the fence. Either way the producer
        // ends fenced.
        await AssertFencedAsync(() => txn1.AbortAsync().AsTask(), txnId);
    }

    private static async Task AssertFencedAsync(Func<Task> action, string transactionalId)
    {
        var exception = await Assert.That(action).Throws<FatalTransactionException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ProducerFenced);
        await Assert.That(exception.TransactionalId).IsEqualTo(transactionalId);
    }
}
