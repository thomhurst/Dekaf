using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration.NetworkFault;

/// <summary>
/// Consumer behaviour under TCP-level faults a killed broker never produces. Faults go through
/// the consumer proxy only; records are seeded through the separate producer proxy.
/// </summary>
[ClassDataSource<TransactionFaultKafkaContainer>(Shared = SharedType.PerTestSession)]
[Category("NetworkPartition")]
[NotInParallel("TransactionFaultKafkaContainer")]
public sealed class ConsumerNetworkFaultTests(TransactionFaultKafkaContainer kafka)
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(3);

    [Test]
    public async Task Join_BlackHoledCoordinator_StaysBoundedByTheRebalanceTimeoutAndRecovers()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync();
        await SeedAsync(topic, ["only-value"], cancellationToken);
        var rebalanceTimeout = TimeSpan.FromSeconds(8);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId($"network-fault-join-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceTimeout(rebalanceTimeout)
            .WithRequestTimeout(TimeSpan.FromSeconds(2))
            .WithConnectionTimeout(TimeSpan.FromSeconds(2))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        try
        {
            await kafka.AddTimeoutAsync(ToxiproxyLane.Consumer, cancellationToken);
            var stopwatch = Stopwatch.StartNew();

            // The poll either waits out its own timeout or reports the join failure; it must do
            // one of the two within the rebalance timeout, and a failure must be a Kafka error,
            // never the raw socket or timer exception underneath it.
            Exception? failure = null;
            ConsumeResult<string, string>? duringFault = null;
            try
            {
                duringFault = await consumer.ConsumeOneAsync(rebalanceTimeout, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                failure = ex;
            }

            await Assert.That(stopwatch.Elapsed).IsLessThan(rebalanceTimeout + TimeSpan.FromSeconds(10));
            await Assert.That(duringFault).IsNull();
            if (failure is not null)
                await Assert.That(failure).IsAssignableTo<KafkaException>();
        }
        finally
        {
            await kafka.HealNetworkFaultsAsync(CancellationToken.None);
        }

        // The same consumer instance joins and consumes once the coordinator is reachable.
        var recovered = await ConsumeNextAsync(consumer, TimeSpan.FromSeconds(90), cancellationToken);
        await Assert.That(recovered.Value).IsEqualTo("only-value");
    }

    [Test]
    public async Task CommitAsync_ConnectionResetDuringTheCommit_SucceedsWithinTheApiTimeout()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"network-fault-commit-{Guid.NewGuid():N}";
        await SeedAsync(topic, ["first", "second"], cancellationToken);

        using var handshakes = new ConnectionHandshakeObserver();
        using var loggerFactory = handshakes.CreateLoggerFactory();

        await using (var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithDefaultApiTimeout(TimeSpan.FromSeconds(60))
            .WithLoggerFactory(loggerFactory)
            .BuildAsync(cancellationToken))
        {
            consumer.Subscribe(topic);
            var first = await ConsumeNextAsync(consumer, TimeSpan.FromSeconds(60), cancellationToken);
            await Assert.That(first.Value).IsEqualTo("first");

            Task commit;
            try
            {
                var handshakesBeforeFault = handshakes.Handshakes;
                await kafka.AddResetPeerAsync(ToxiproxyLane.Consumer, cancellationToken);
                commit = consumer
                    .CommitAsync([new TopicPartitionOffset(topic, 0, first.Offset + 1)], cancellationToken)
                    .AsTask();

                // The outage outlasts a handful of quick retries but is far shorter than the API
                // timeout, so a commit that uses its budget rides it out. A handshake after the
                // injection proves the reset reached this client's connections.
                await handshakes.WaitForHandshakeAfterAsync(handshakesBeforeFault, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
            finally
            {
                await kafka.HealNetworkFaultsAsync(CancellationToken.None);
            }

            await commit;
        }

        // The committed offset is the proof: a new member of the group resumes after "first".
        await using var successor = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        successor.Subscribe(topic);

        var resumed = await ConsumeNextAsync(successor, TimeSpan.FromSeconds(90), cancellationToken);
        await Assert.That(resumed.Value).IsEqualTo("second");
    }

    [Test]
    public async Task Fetch_ConnectionCutMidResponse_MakesProgressAfterHealWithoutLossOrRedelivery()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync();

        // A few small records first, so every connection the consumer needs is open before the
        // fault and a later handshake can only mean a connection was cut.
        const int warmupCount = 5;
        const int largeCount = 200;
        const int recordCount = warmupCount + largeCount;
        await SeedAsync(
            topic,
            Enumerable.Range(0, warmupCount).Select(static i => $"warmup-{i}").ToArray(),
            cancellationToken);

        using var handshakes = new ConnectionHandshakeObserver();
        using var loggerFactory = handshakes.CreateLoggerFactory();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId($"network-fault-fetch-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(loggerFactory)
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        var offsets = new List<long>(recordCount);
        while (offsets.Count < warmupCount)
        {
            var result = await ConsumeNextAsync(consumer, TimeSpan.FromSeconds(60), cancellationToken);
            offsets.Add(result.Offset);
        }

        try
        {
            var handshakesBeforeFault = handshakes.Handshakes;
            await kafka.AddLimitDataAsync(ToxiproxyLane.Consumer, bytes: 32 * 1024, cancellationToken);

            // 200 records of 4 KB: far more than the 32 KB a connection may carry while the fault
            // is active, so a fetch response that holds them is cut part way through a frame.
            var payload = new string('x', 4_096);
            await SeedAsync(
                topic,
                Enumerable.Range(0, largeCount).Select(i => $"{i}:{payload}").ToArray(),
                cancellationToken);

            // Poll into the fault until the client has had to reconnect: a response was cut
            // mid-frame. Records that fit under the limit may still arrive meanwhile.
            var reconnected = handshakes.WaitForHandshakeAfterAsync(handshakesBeforeFault, cancellationToken);
            while (!reconnected.IsCompleted)
            {
                var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), cancellationToken);
                if (result is not null)
                    offsets.Add(result.Value.Offset);
            }

            await reconnected;
        }
        finally
        {
            await kafka.HealNetworkFaultsAsync(CancellationToken.None);
        }

        // Progress, not just the absence of an exception: a truncated response must not leave
        // the fetch position stuck. Every record arrives on the same consumer instance.
        while (offsets.Count < recordCount)
        {
            var result = await ConsumeNextAsync(consumer, TimeSpan.FromSeconds(90), cancellationToken);
            offsets.Add(result.Offset);
        }

        // No rebalance and no seek happened, so at-least-once allows no redelivery here: the
        // offsets are exactly 0..n-1, in order. A gap is loss; a repeat is a partly delivered
        // response that was fetched again.
        var expected = string.Join(',', Enumerable.Range(0, recordCount));
        await Assert.That(string.Join(',', offsets)).IsEqualTo(expected);
    }

    [Test]
    public async Task CloseAsync_CoordinatorResponsesBlackHoled_StillLeavesSoASuccessorNeedNotWaitOutTheSession()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"network-fault-close-{Guid.NewGuid():N}";
        await SeedAsync(topic, ["first", "second"], cancellationToken);
        var closeBudget = TimeSpan.FromSeconds(6);

        var closing = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMinutes(10))
            .WithAutoOffsetStore(false)
            .WithDefaultApiTimeout(closeBudget)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        try
        {
            closing.Subscribe(topic);
            var first = await ConsumeNextAsync(closing, TimeSpan.FromSeconds(60), cancellationToken);
            await Assert.That(first.Value).IsEqualTo("first");
            // A stored offset gives close a final commit to make.
            closing.StoreOffset(first);

            TimeSpan closeElapsed;
            try
            {
                // Requests still reach the broker but no response comes back, so the commit waits
                // for an answer the request timeout (30 s) would only end long after the budget.
                await kafka.AddTimeoutAsync(ToxiproxyLane.Consumer, cancellationToken);
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await closing.CloseAsync(cancellationToken);
                }
                catch (KafkaTimeoutException)
                {
                    // The leave's response is black-holed too, so close may run out its budget.
                }

                closeElapsed = stopwatch.Elapsed;
            }
            finally
            {
                await kafka.HealNetworkFaultsAsync(CancellationToken.None);
            }

            await Assert.That(closeElapsed).IsLessThan(closeBudget + TimeSpan.FromSeconds(5));
        }
        finally
        {
            await closing.DisposeAsync();
        }

        // The commit stopped with part of the budget left and the leave was sent in it, so the
        // broker already removed the member. Otherwise the successor would get the partition
        // only once the closed member's 45 s session timed out.
        var successorStarted = Stopwatch.StartNew();
        await using var successor = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        successor.Subscribe(topic);

        var taken = await ConsumeNextAsync(successor, TimeSpan.FromSeconds(60), cancellationToken);
        await Assert.That(successorStarted.Elapsed).IsLessThan(TimeSpan.FromSeconds(25));
        await Assert.That(taken.Value).IsNotNull();
    }

    [Test]
    public async Task RunPartitionedAsync_PeriodicCommitAfterTheMemberExpired_RejoinsInsteadOfFaulting()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"network-fault-partitioned-{Guid.NewGuid():N}";
        await kafka.UseShortGroupSessionAsync(groupId, cancellationToken);
        await SeedAsync(topic, ["first"], cancellationToken);

        var listener = new LossRecordingListener();
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(listener)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stopRun = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var run = consumer.RunPartitionedAsync(
            async (_, message, handlerCancellation) =>
            {
                if (message.Value == "first" && firstStarted.TrySetResult())
                    await releaseFirst.Task.WaitAsync(handlerCancellation);
                if (message.Value == "second")
                    secondProcessed.TrySetResult();
            },
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.CommitCompletedPeriodically,
                CommitInterval = TimeSpan.FromMilliseconds(200)
            },
            stopRun.Token).AsTask();

        try
        {
            await firstStarted.Task.WaitAsync(cancellationToken);

            // The record completes inside the outage, so a periodic commit is pending when the
            // connection returns, and the broker has expired the member before it can arrive.
            await kafka.ExpireConsumerMemberAsync(cancellationToken, () => releaseFirst.SetResult());

            await listener.Lost.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);

            // A run that survived the fence rejoins and keeps processing new records.
            await SeedAsync(topic, ["second"], cancellationToken);
            await secondProcessed.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
            await Assert.That(run.IsCompleted).IsFalse();
        }
        finally
        {
            await stopRun.CancelAsync();
            try
            {
                await run;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    [Test]
    public async Task ConsumeAsync_AutoCommitAfterTheMemberExpired_KeepsConsumingAfterRejoin()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"network-fault-autocommit-{Guid.NewGuid():N}";
        await kafka.UseShortGroupSessionAsync(groupId, cancellationToken);
        await SeedAsync(topic, ["first"], cancellationToken);

        var listener = new LossRecordingListener();
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(200))
            .WithRebalanceListener(listener)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        var firstReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stopConsuming = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var consuming = Task.Run(async () =>
        {
            await foreach (var record in consumer.ConsumeAsync(stopConsuming.Token))
            {
                if (record.Value == "first")
                    firstReceived.TrySetResult();
                else if (record.Value == "second")
                    secondReceived.TrySetResult();
            }
        }, CancellationToken.None);

        try
        {
            await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);

            // Auto-commits fire throughout the outage and right after it, racing the heartbeat
            // that discovers the expired membership.
            await kafka.ExpireConsumerMemberAsync(cancellationToken);
            await listener.Lost.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);

            await SeedAsync(topic, ["second"], cancellationToken);
            await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
            await Assert.That(consuming.IsCompleted).IsFalse();
        }
        finally
        {
            await stopConsuming.CancelAsync();
            try
            {
                await consuming;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    [Test]
    public async Task CommitAsync_AfterTheMemberExpired_ThrowsNonRetriableGroupExceptionThenCommitsAfterPoll()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"network-fault-expired-commit-{Guid.NewGuid():N}";
        await kafka.UseShortGroupSessionAsync(groupId, cancellationToken);
        await SeedAsync(topic, ["first", "second"], cancellationToken);

        var listener = new LossRecordingListener();
        await using (var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(listener)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken))
        {
            consumer.Subscribe(topic);
            var first = await ConsumeNextAsync(consumer, TimeSpan.FromSeconds(60), cancellationToken);
            await Assert.That(first.Value).IsEqualTo("first");

            await kafka.ExpireConsumerMemberAsync(cancellationToken);
            await listener.Lost.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);

            // The documented contract: a commit outside an active membership is refused with a
            // non-retriable GroupException, never committed under the expired membership and
            // never surfaced as a transport or timeout error.
            var rejected = await Assert.ThrowsAsync<GroupException>(async () => await consumer.CommitAsync(
                [new TopicPartitionOffset(topic, 0, first.Offset + 1)],
                cancellationToken));
            await Assert.That(rejected!.IsRetriable).IsFalse();
            await Assert.That(rejected.ErrorCode is ErrorCode.FencedMemberEpoch
                or ErrorCode.UnknownMemberId
                or ErrorCode.StaleMemberEpoch).IsTrue();

            // Polling rejoins. Nothing was committed, so the group resumes from the start.
            var redelivered = await ConsumeNextAsync(consumer, TimeSpan.FromSeconds(60), cancellationToken);
            await Assert.That(redelivered.Value).IsEqualTo("first");
            await consumer.CommitAsync(
                [new TopicPartitionOffset(topic, 0, redelivered.Offset + 1)],
                cancellationToken);
        }

        await using var successor = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        successor.Subscribe(topic);

        var resumed = await ConsumeNextAsync(successor, TimeSpan.FromSeconds(90), cancellationToken);
        await Assert.That(resumed.Value).IsEqualTo("second");
    }

    [Test]
    [Arguments(PartitionCommitPolicy.CommitCompletedOnRevoke)]
    [Arguments(PartitionCommitPolicy.UserManaged)]
    public async Task RunPartitionedAsync_MemberExpiredWithCompletedWorkUncommitted_RejoinsInsteadOfFaulting(
        PartitionCommitPolicy commitPolicy)
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"network-fault-partitioned-{commitPolicy}-{Guid.NewGuid():N}";
        await kafka.UseShortGroupSessionAsync(groupId, cancellationToken);
        await SeedAsync(topic, ["first"], cancellationToken);

        var listener = new LossRecordingListener();
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(listener)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        var firstProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stopRun = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var run = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                if (message.Value == "first")
                    firstProcessed.TrySetResult();
                else if (message.Value == "second")
                    secondProcessed.TrySetResult();
                return ValueTask.CompletedTask;
            },
            new PartitionedProcessingOptions { CommitPolicy = commitPolicy },
            stopRun.Token).AsTask();

        try
        {
            await firstProcessed.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);

            // The lost partition stops without the revoke commit, and the reassigned lane starts
            // from scratch; neither path may fault the run.
            await kafka.ExpireConsumerMemberAsync(cancellationToken);
            await listener.Lost.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);

            await SeedAsync(topic, ["second"], cancellationToken);
            await secondProcessed.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
            await Assert.That(run.IsCompleted).IsFalse();
        }
        finally
        {
            await stopRun.CancelAsync();
            try
            {
                await run;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private sealed class LossRecordingListener : IRebalanceListener
    {
        public TaskCompletionSource Lost { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask OnPartitionsAssignedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnPartitionsRevokedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnPartitionsLostAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Lost.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    private static async Task<ConsumeResult<string, string>> ConsumeNextAsync(
        IKafkaConsumer<string, string> consumer,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var result = await consumer.ConsumeOneAsync(timeout, cancellationToken);
        if (result is null)
            throw new TimeoutException($"No record was consumed within {timeout}.");

        return result.Value;
    }

    private async Task SeedAsync(string topic, IReadOnlyList<string> values, CancellationToken cancellationToken)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.ProducerBootstrapServers)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        foreach (var value in values)
            _ = await producer.ProduceAsync(topic, "key", value, cancellationToken);
    }
}
