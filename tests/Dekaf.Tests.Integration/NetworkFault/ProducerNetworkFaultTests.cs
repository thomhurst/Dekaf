using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration.NetworkFault;

/// <summary>
/// Producer behaviour under TCP-level faults a killed broker never produces: a connection reset
/// with requests in flight, a connection cut in the middle of a response frame, and a black hole
/// that accepts connections but passes no data. Faults go through the producer proxy only; the
/// verifying consumer reads through the separate consumer proxy.
/// </summary>
[ClassDataSource<TransactionFaultKafkaContainer>(Shared = SharedType.PerTestSession)]
[Category("NetworkPartition")]
[NotInParallel("TransactionFaultKafkaContainer")]
public sealed class ProducerNetworkFaultTests(TransactionFaultKafkaContainer kafka)
{
    private const int MessagesPerWave = 20;
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan FaultDuration = TimeSpan.FromSeconds(2);

    [Test]
    public async Task ProduceAsync_ConnectionResetWithRequestsInFlight_DeliversEveryRecordOnceInOrder()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;

        await ProduceThroughFaultAsync(
            ct => kafka.AddResetPeerAsync(ToxiproxyLane.Producer, ct),
            cancellationToken);
    }

    [Test]
    public async Task ProduceAsync_ConnectionCutMidResponse_DeliversEveryRecordOnceInOrder()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;

        // Ten bytes is less than any response frame, so the broker appends the batch and the
        // connection dies with the acknowledgement half read: the retry carries the same
        // sequence and must be deduplicated, not appended again.
        await ProduceThroughFaultAsync(
            ct => kafka.AddLimitDataAsync(ToxiproxyLane.Producer, bytes: 10, ct),
            cancellationToken);
    }

    [Test]
    public async Task InitTransactionsAsync_BlackHoledCoordinator_TimesOutWithinMaxBlockAndRecovers()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync();
        var maxBlock = TimeSpan.FromSeconds(3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.ProducerBootstrapServers)
            .WithTransactionalId($"network-fault-init-{Guid.NewGuid():N}")
            .WithMaxBlock(maxBlock)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        try
        {
            // Upstream: the requests never reach the broker, so nothing is left half applied.
            await kafka.AddTimeoutAsync(
                ToxiproxyLane.Producer, cancellationToken, direction: ToxiproxyDirection.Upstream);
            var stopwatch = Stopwatch.StartNew();

            var exception = await Assert.That(() => producer.InitTransactionsAsync(cancellationToken).AsTask())
                .Throws<KafkaTimeoutException>();

            await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
            await Assert.That(stopwatch.Elapsed).IsLessThan(maxBlock + TimeSpan.FromSeconds(3));
        }
        finally
        {
            await kafka.HealNetworkFaultsAsync(CancellationToken.None);
        }

        await producer.InitTransactionsAsync(cancellationToken);
        await using (var transaction = producer.BeginTransaction())
        {
            _ = await transaction.ProduceAsync(topic, "recovered-key", "recovered-value", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        var visible = await ReadCommittedAsync(topic, expectedCount: 1, cancellationToken);
        await Assert.That(string.Join(',', visible)).IsEqualTo("recovered-value");
    }

    [Test]
    public async Task CommitAsync_BlackHoledCoordinator_TimesOutWithinMaxBlock_AndTheTransactionStaysAtomic()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync();
        var transactionalId = $"network-fault-commit-{Guid.NewGuid():N}";
        var maxBlock = TimeSpan.FromSeconds(3);
        string[] firstTransaction = ["first-0", "first-1", "first-2"];
        string[] secondTransaction = ["second-0", "second-1", "second-2"];

        await using (var producer = await CreateTransactionalProducerAsync(transactionalId, maxBlock, cancellationToken))
        {
            await producer.InitTransactionsAsync(cancellationToken);
            await using var transaction = producer.BeginTransaction();
            foreach (var value in firstTransaction)
                _ = await transaction.ProduceAsync(topic, "key", value, cancellationToken);

            try
            {
                // Downstream: EndTxn reaches the coordinator but its answer never comes back, so
                // the client cannot know whether the transaction committed.
                await kafka.AddTimeoutAsync(ToxiproxyLane.Producer, cancellationToken);
                var stopwatch = Stopwatch.StartNew();

                var exception = await Assert.That(() => transaction.CommitAsync(cancellationToken).AsTask())
                    .Throws<KafkaTimeoutException>();

                await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Transaction);
                await Assert.That(stopwatch.Elapsed).IsLessThan(maxBlock + TimeSpan.FromSeconds(3));
            }
            finally
            {
                await kafka.HealNetworkFaultsAsync(CancellationToken.None);
            }

            // An EndTxn that was written and never answered has an unknown outcome. The producer
            // must not start another transaction on top of it.
            await Assert.That(() => producer.BeginTransaction()).Throws<FatalTransactionException>();
        }

        // Recovery is a new producer with the same transactional id: InitTransactions fences the
        // old one and settles the pending transaction one way or the other.
        await using (var successor = await CreateTransactionalProducerAsync(
            transactionalId, TimeSpan.FromSeconds(60), cancellationToken))
        {
            await successor.InitTransactionsAsync(cancellationToken);
            await using var transaction = successor.BeginTransaction();
            foreach (var value in secondTransaction)
                _ = await transaction.ProduceAsync(topic, "key", value, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        // The second transaction is the fence: once its records are visible, the first one is
        // settled. It is visible whole and once, or not at all; never in part and never twice.
        var visible = await ReadCommittedUntilAsync(topic, secondTransaction[^1], cancellationToken);
        var visibleFirst = visible.Where(value => value.StartsWith("first-", StringComparison.Ordinal)).ToArray();
        var visibleSecond = visible.Where(value => value.StartsWith("second-", StringComparison.Ordinal)).ToArray();

        await Assert.That(string.Join(',', visibleSecond)).IsEqualTo(string.Join(',', secondTransaction));
        if (visibleFirst.Length != 0)
            await Assert.That(string.Join(',', visibleFirst)).IsEqualTo(string.Join(',', firstTransaction));
        await Assert.That(visible.Count).IsEqualTo(visibleFirst.Length + visibleSecond.Length);
    }

    private async Task ProduceThroughFaultAsync(
        Func<CancellationToken, Task> injectFaultAsync,
        CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateTestTopicAsync();
        using var handshakes = new ConnectionHandshakeObserver();
        using var loggerFactory = handshakes.CreateLoggerFactory();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.ProducerBootstrapServers)
            .WithAcks(Acks.All)
            .WithIdempotence(true)
            .WithDeliveryTimeout(TimeSpan.FromSeconds(90))
            .WithLoggerFactory(loggerFactory)
            .BuildAsync(cancellationToken);

        var next = 0;
        await ProduceWaveAsync(producer, topic, ref next, cancellationToken);

        Task faultedWave;
        try
        {
            var handshakesBeforeFault = handshakes.Handshakes;
            await injectFaultAsync(cancellationToken);

            // Started while the fault is active: these requests meet it on the wire.
            faultedWave = ProduceWaveAsync(producer, topic, ref next, cancellationToken);

            // A handshake after the injection means the fault cut the connection and the client
            // is reconnecting into it, so the test cannot pass on a fault that never bit.
            await handshakes.WaitForHandshakeAfterAsync(handshakesBeforeFault, cancellationToken);
            await Task.Delay(FaultDuration, cancellationToken);
        }
        finally
        {
            await kafka.HealNetworkFaultsAsync(CancellationToken.None);
        }

        // The same producer instance recovers: the wave caught by the fault completes, and so
        // does one sent after the heal.
        await faultedWave;
        await ProduceWaveAsync(producer, topic, ref next, cancellationToken);

        var expected = Enumerable.Range(0, next).Select(static i => $"value-{i}").ToArray();
        var consumed = await ReadCommittedAsync(topic, expected.Length, cancellationToken);

        // Exactly the produced sequence: a lost record, a duplicate or a reorder all break it.
        await Assert.That(string.Join(',', consumed)).IsEqualTo(string.Join(',', expected));
    }

    private static Task ProduceWaveAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        ref int next,
        CancellationToken cancellationToken)
    {
        // Issued back to back without awaiting, so several requests are in flight at once and the
        // single partition's append order is the call order.
        var sends = new Task[MessagesPerWave];
        for (var i = 0; i < sends.Length; i++)
        {
            sends[i] = producer.ProduceAsync(topic, "key", $"value-{next}", cancellationToken).AsTask();
            next++;
        }

        return Task.WhenAll(sends);
    }

    private async Task<IKafkaProducer<string, string>> CreateTransactionalProducerAsync(
        string transactionalId,
        TimeSpan maxBlock,
        CancellationToken cancellationToken) =>
        await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.ProducerBootstrapServers)
            .WithTransactionalId(transactionalId)
            .WithMaxBlock(maxBlock)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

    private Task<List<string>> ReadCommittedAsync(
        string topic,
        int expectedCount,
        CancellationToken cancellationToken) =>
        ReadCommittedCoreAsync(topic, values => values.Count >= expectedCount, cancellationToken);

    private Task<List<string>> ReadCommittedUntilAsync(
        string topic,
        string lastExpectedValue,
        CancellationToken cancellationToken) =>
        ReadCommittedCoreAsync(
            topic,
            values => values.Count > 0 && values[^1] == lastExpectedValue,
            cancellationToken);

    private async Task<List<string>> ReadCommittedCoreAsync(
        string topic,
        Func<List<string>, bool> isComplete,
        CancellationToken cancellationToken)
    {
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId($"network-fault-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        var values = new List<string>();
        while (!isComplete(values))
        {
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(60), cancellationToken);
            if (result is null)
                break;

            values.Add(result.Value.Value);
        }

        // One more poll: a duplicate appended after the expected tail would arrive here.
        var extra = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(3), cancellationToken);
        if (extra is not null)
            values.Add(extra.Value.Value);

        return values;
    }
}
