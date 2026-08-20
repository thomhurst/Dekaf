using System.Buffers;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Serialization;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryProducerFaultTests
{
    [Test]
    public async Task Produce_FaultsHonorScopeOccurrenceAndRetryBoundaries()
    {
        var plan = new KafkaFaultPlan();
        var cluster = new InMemoryKafkaCluster(plan);
        cluster.CreateTopic("orders", partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        var retriable = new ProduceException(ErrorCode.NotLeaderOrFollower, "retry");
        var nonRetriable = new ProduceException(ErrorCode.MessageTooLarge, "reject");
        var scope = new KafkaFaultScope(KafkaFaultOperation.Produce, "orders", partition: 1);
        plan.Fail(scope, retriable);
        plan.Fail(scope, nonRetriable);

        var first = await Assert.ThrowsAsync<ProduceException>(() => ProduceToPartitionAsync(producer, 1));
        var second = await Assert.ThrowsAsync<ProduceException>(() => ProduceToPartitionAsync(producer, 1));
        var otherPartition = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "orders",
            Partition = 0,
            Key = "k",
            Value = "other"
        });
        var recovered = await ProduceToPartitionAsync(producer, 1);

        await Assert.That(first).IsSameReferenceAs(retriable);
        await Assert.That(first!.IsRetriable).IsTrue();
        await Assert.That(second).IsSameReferenceAs(nonRetriable);
        await Assert.That(second!.IsRetriable).IsFalse();
        await Assert.That(otherPartition.Partition).IsEqualTo(0);
        await Assert.That(recovered.Partition).IsEqualTo(1);
        await Assert.That(cluster.ReadRecords("orders", 1)).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Produce_PartitionScopedFaultMatchesResolvedRoundRobinPartition()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        var failure = new InvalidOperationException("partition zero");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Produce, "orders", partition: 0),
            failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            producer.ProduceAsync("orders", key: null, "first").AsTask());
        var recovered = await producer.ProduceAsync("orders", key: null, "second");

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(recovered.Partition).IsEqualTo(1);
    }

    [Test]
    public async Task Produce_TopicDeletedDuringPartitionFaultPauseFailsAsUnknownPartition()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Produce, "orders", partition: 1));
        var message = new ProducerMessage<string, string>
        {
            Topic = "orders",
            Partition = 1,
            Key = "k",
            Value = "v"
        };

        var pending = producer.ProduceAsync(message).AsTask();
        await barrier.WaitUntilEnteredAsync();
        await Assert.That(cluster.DeleteTopic("orders")).IsTrue();
        await Assert.That(barrier.Release()).IsTrue();

        var failure = await Assert.ThrowsAsync<ProduceException>(() => pending);
        await Assert.That(failure!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(failure.Topic).IsEqualTo("orders");
        await Assert.That(failure.Partition).IsEqualTo(1);

        var recovered = await producer.ProduceAsync("orders", "k", "recovered");
        await Assert.That(recovered.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task Produce_LegacyFailureTakesPriorityWithoutConsumingScript()
    {
        var plan = new KafkaFaultPlan();
        var cluster = new InMemoryKafkaCluster(plan);
        var producer = new InMemoryProducer<string, string>(cluster);
        var legacy = new InvalidOperationException("legacy");
        var scripted = new TimeoutException("scripted");
        cluster.FailProduces("orders", legacy);
        plan.Fail(new KafkaFaultScope(KafkaFaultOperation.Produce, "orders"), scripted);

        var first = await Assert.ThrowsAsync<InvalidOperationException>(
            () => producer.ProduceAsync("orders", "k", "v").AsTask());
        cluster.ClearProduceFailure("orders");
        var second = await Assert.ThrowsAsync<TimeoutException>(
            () => producer.ProduceAsync("orders", "k", "v").AsTask());

        await Assert.That(first).IsSameReferenceAs(legacy);
        await Assert.That(second).IsSameReferenceAs(scripted);
    }

    [Test]
    public async Task FireAsync_ConsumesFaultWithoutSurfacingDeliveryFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Produce, "events"),
            new InvalidOperationException("delivery failed"));

        await producer.FireAsync("events", "k", "first");
        var metadata = await producer.ProduceAsync("events", "k", "second");

        await Assert.That(metadata.Offset).IsEqualTo(0);
        await Assert.That(cluster.ReadRecords("events")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ProduceBarrier_CancellationDoesNotAppendAndRetryRecovers()
    {
        var plan = new KafkaFaultPlan();
        var cluster = new InMemoryKafkaCluster(plan);
        var producer = new InMemoryProducer<string, string>(cluster);
        var barrier = plan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Produce, "orders", partition: 0));
        using var cancellation = new CancellationTokenSource();
        var message = new ProducerMessage<string, string>
        {
            Topic = "orders",
            Partition = 0,
            Key = "k",
            Value = "v"
        };

        var pending = producer.ProduceAsync(message, cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        await Assert.That(barrier.Release()).IsTrue();
        await Assert.That(cluster.ReadRecords("orders")).IsEmpty();
        var recovered = await producer.ProduceAsync(message);
        await Assert.That(recovered.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task InitTransactions_OneShotFailureAllowsRetry()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var failure = new KafkaTimeoutException("coordinator unavailable");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.InitializeTransactions),
            failure);

        var actual = await Assert.ThrowsAsync<KafkaTimeoutException>(
            () => producer.InitTransactionsAsync().AsTask());
        await producer.InitTransactionsAsync();

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task TransactionProduce_FailureIsScopedAndRetryable()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        var failure = new ProduceException(ErrorCode.NotLeaderOrFollower, "retry transaction produce");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.TransactionProduce, "orders", partition: 1),
            failure);
        await using var transaction = producer.BeginTransaction();
        var message = new ProducerMessage<string, string>
        {
            Topic = "orders",
            Partition = 1,
            Key = "k",
            Value = "v"
        };

        var actual = await Assert.ThrowsAsync<ProduceException>(
            () => transaction.ProduceAsync(message).AsTask());
        var recovered = await transaction.ProduceAsync(message);
        await transaction.CommitAsync();

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(recovered.Partition).IsEqualTo(1);
    }

    [Test]
    public async Task TransactionProduce_AbortableFailureRequiresAbortBeforeReuse()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var abortable = new AbortableTransactionException(
            ErrorCode.TransactionAbortable,
            "abort required");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.TransactionProduce, "orders"),
            abortable);
        var transaction = producer.BeginTransaction();

        var initial = await Assert.ThrowsAsync<AbortableTransactionException>(() =>
            transaction.ProduceAsync("orders", "k", "v").AsTask());
        var produce = await Assert.ThrowsAsync<AbortableTransactionException>(() =>
            transaction.ProduceAsync("orders", "k", "retry").AsTask());
        var offsets = await Assert.ThrowsAsync<AbortableTransactionException>(() =>
            transaction.SendOffsetsToTransactionAsync(
                [new TopicPartitionOffset("orders", 0, 1)],
                "billing").AsTask());
        var prepare = await Assert.ThrowsAsync<AbortableTransactionException>(() =>
            transaction.PrepareAsync().AsTask());
        var commit = await Assert.ThrowsAsync<AbortableTransactionException>(() =>
            transaction.CommitAsync().AsTask());

        await Assert.That(initial).IsSameReferenceAs(abortable);
        await Assert.That(produce).IsSameReferenceAs(abortable);
        await Assert.That(offsets).IsSameReferenceAs(abortable);
        await Assert.That(prepare).IsSameReferenceAs(abortable);
        await Assert.That(commit).IsSameReferenceAs(abortable);

        await transaction.AbortAsync();
        await using var recovered = producer.BeginTransaction();
        await recovered.AbortAsync();
    }

    [Test]
    public async Task TransactionProduce_PausedMutationRejectsCommitUntilAppendCompletes()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var transaction = producer.BeginTransaction();
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.TransactionProduce, "orders"));

        var pendingProduce = transaction.ProduceAsync("orders", "k", "v").AsTask();
        await barrier.WaitUntilEnteredAsync();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transaction.CommitAsync().AsTask());
        await Assert.That(barrier.Release()).IsTrue();
        _ = await pendingProduce;
        await transaction.CommitAsync();

        await Assert.That(cluster.ReadRecords("orders")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task TransactionOffsets_PausedMutationRejectsAbortUntilMutationCompletes()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var transaction = producer.BeginTransaction();
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.SendOffsetsToTransaction, groupId: "billing"));

        var pendingOffsets = transaction.SendOffsetsToTransactionAsync(
            [new TopicPartitionOffset("orders", 0, 17)],
            "billing").AsTask();
        await barrier.WaitUntilEnteredAsync();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transaction.AbortAsync().AsTask());
        await Assert.That(barrier.Release()).IsTrue();
        await pendingOffsets;
        await transaction.AbortAsync();

        await Assert.That(cluster.GetCommittedOffset("billing", new TopicPartition("orders", 0))).IsNull();
    }

    [Test]
    public async Task TransactionOperations_FailBeforeMutationAndRecoverWithoutDelays()
    {
        var plan = new KafkaFaultPlan();
        var cluster = new InMemoryKafkaCluster(plan);
        var producer = new InMemoryProducer<string, string>(cluster);
        var offsetFailure = new KafkaTimeoutException("offset send failed");
        var commitFailure = new KafkaTimeoutException("commit failed");
        var abortFailure = new KafkaTimeoutException("abort failed");
        plan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.SendOffsetsToTransaction, groupId: "billing"),
            offsetFailure);
        plan.Fail(new KafkaFaultScope(KafkaFaultOperation.CommitTransaction), commitFailure);
        plan.Fail(new KafkaFaultScope(KafkaFaultOperation.AbortTransaction), abortFailure);

        await using (var transaction = producer.BeginTransaction())
        {
            var offsets = new[] { new TopicPartitionOffset("orders", 0, 17) };
            var actualOffsetFailure = await Assert.ThrowsAsync<KafkaTimeoutException>(
                () => transaction.SendOffsetsToTransactionAsync(offsets, "billing").AsTask());
            await transaction.SendOffsetsToTransactionAsync(offsets, "billing");
            var actualCommitFailure = await Assert.ThrowsAsync<KafkaTimeoutException>(
                () => transaction.CommitAsync().AsTask());

            await Assert.That(actualOffsetFailure).IsSameReferenceAs(offsetFailure);
            await Assert.That(actualCommitFailure).IsSameReferenceAs(commitFailure);
            await Assert.That(cluster.GetCommittedOffset("billing", new TopicPartition("orders", 0))).IsNull();
            await transaction.CommitAsync();
        }

        await Assert.That(cluster.GetCommittedOffset("billing", new TopicPartition("orders", 0))).IsEqualTo(17);

        await using (var transaction = producer.BeginTransaction())
        {
            var actualAbortFailure = await Assert.ThrowsAsync<KafkaTimeoutException>(
                () => transaction.AbortAsync().AsTask());
            await Assert.That(actualAbortFailure).IsSameReferenceAs(abortFailure);
            await transaction.AbortAsync();
        }

        await using var recoveredTransaction = producer.BeginTransaction();
        await recoveredTransaction.AbortAsync();
    }

    [Test]
    public async Task TransactionalRecords_AreVisibleOnlyAfterCommitAndNeverAfterAbort()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);

        await using (var committed = producer.BeginTransaction())
        {
            _ = await committed.ProduceAsync("orders", "k", "committed");
            await Assert.That(cluster.ReadRecords("orders")).IsEmpty();
            await committed.CommitAsync();
        }

        await using (var aborted = producer.BeginTransaction())
        {
            _ = await aborted.ProduceAsync("orders", "k", "aborted");
            await Assert.That(cluster.ReadRecords("orders")).Count().IsEqualTo(1);
            await aborted.AbortAsync();
        }

        _ = await producer.ProduceAsync("orders", "k", "ordinary");
        var visible = cluster.ReadRecords("orders");

        await Assert.That(visible.Select(static record => Encoding.UTF8.GetString(record.Value)))
            .IsEquivalentTo(["committed", "ordinary"]);
        await Assert.That(visible.Select(static record => record.Offset))
            .IsEquivalentTo([0L, 2L]);

        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        consumer.Subscribe("orders");
        var first = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        var second = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(first!.Value.Offset).IsEqualTo(0);
        await Assert.That(second!.Value.Offset).IsEqualTo(2);
    }

    [Test]
    public async Task ProducerProduce_RejectsSendOutsideActiveTransaction()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var transaction = producer.BeginTransaction();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            producer.ProduceAsync("orders", "k", "outside").AsTask());
        await transaction.AbortAsync();
        var metadata = await producer.ProduceAsync("orders", "k", "after");

        await Assert.That(metadata.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task ReadRecords_StopsAtOngoingTransactionBoundary()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        _ = await producer.ProduceAsync("orders", "k", "before");
        await using var transaction = producer.BeginTransaction();
        _ = await transaction.ProduceAsync("orders", "k", "pending");
        var otherProducer = new InMemoryProducer<string, string>(cluster);
        _ = await otherProducer.ProduceAsync("orders", "k", "after");

        var blocked = cluster.ReadRecords("orders");
        await Assert.That(blocked.Select(static record => record.Offset)).IsEquivalentTo([0L]);

        await transaction.CommitAsync();
        var committed = cluster.ReadRecords("orders");
        await Assert.That(committed.Select(static record => record.Offset))
            .IsEquivalentTo([0L, 1L, 2L]);
    }

    [Test]
    public async Task TransactionDispose_AbortFaultIsBestEffortAndReleasesProducerSlot()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var transaction = producer.BeginTransaction();
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.AbortTransaction),
            new InvalidOperationException("injected abort failure"));

        await transaction.DisposeAsync();

        await using var recovered = producer.BeginTransaction();
        await recovered.AbortAsync();
    }

    [Test]
    public async Task ProducerDispose_ClaimsLifecycleBeforeAwaitingTransactionAbort()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        _ = producer.BeginTransaction();
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.AbortTransaction));

        var pendingDispose = producer.DisposeAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        await Assert.That(() => producer.BeginTransaction()).Throws<ObjectDisposedException>();
        await Assert.That(barrier.Release()).IsTrue();
        await pendingDispose;
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            producer.ProduceAsync("orders", "k", "v").AsTask());
    }

    [Test]
    public async Task ProducerDispose_PreventsPausedOffsetsFromMutatingCompletedTransaction()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var transaction = producer.BeginTransaction();
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.SendOffsetsToTransaction, groupId: "billing"));
        var pendingOffsets = transaction.SendOffsetsToTransactionAsync(
            [new TopicPartitionOffset("orders", 0, 17)],
            "billing").AsTask();

        await barrier.WaitUntilEnteredAsync();
        await producer.DisposeAsync();
        await Assert.That(barrier.Release()).IsTrue();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => pendingOffsets);

        await Assert.That(cluster.GetCommittedOffset("billing", new TopicPartition("orders", 0))).IsNull();
    }

    [Test]
    public async Task ProducerDispose_PreventsPausedProduceFromAppendingAfterAbort()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var transaction = producer.BeginTransaction();
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.TransactionProduce, "orders"));
        var pendingProduce = transaction.ProduceAsync("orders", "k", "v").AsTask();

        await barrier.WaitUntilEnteredAsync();
        await producer.DisposeAsync();
        await Assert.That(barrier.Release()).IsTrue();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => pendingProduce);

        await Assert.That(cluster.ReadRecords("orders")).IsEmpty();
    }

    [Test]
    public async Task TransactionCommit_RejectsStaleConsumerGroupMetadataWithoutCommittingOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { GroupId = "billing" });
        consumer.Subscribe("orders");
        var metadata = consumer.ConsumerGroupMetadata!;
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await using var transaction = producer.BeginTransaction();
        await transaction.SendOffsetsToTransactionAsync(
            [new TopicPartitionOffset("orders", 0, 17)],
            metadata);

        await using var replacement = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { GroupId = "billing" });
        replacement.Subscribe("orders");

        var failure = await Assert.ThrowsAsync<FatalTransactionException>(
            () => transaction.CommitAsync().AsTask());
        var poisoned = await Assert.ThrowsAsync<FatalTransactionException>(
            () => producer.ProduceAsync("orders", "k", "v").AsTask());

        await Assert.That(failure!.ErrorCode).IsEqualTo(ErrorCode.IllegalGeneration);
        await Assert.That(poisoned).IsSameReferenceAs(failure);
        await Assert.That(() => producer.BeginTransaction()).Throws<FatalTransactionException>();
        await Assert.That(cluster.GetCommittedOffset("billing", new TopicPartition("orders", 0))).IsNull();
    }

    [Test]
    public async Task TransactionOffsets_ConcurrentStagingCommitsEveryOffset()
    {
        const int operationCount = 32;
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await using var transaction = producer.BeginTransaction();
        var scope = new KafkaFaultScope(
            KafkaFaultOperation.SendOffsetsToTransaction,
            groupId: "billing");
        var barriers = CreateOffsetBarriers(cluster, scope, operationCount);
        var operations = StageOffsets(transaction, operationCount);
        await WaitUntilEnteredAsync(barriers);
        await ReleaseAsync(barriers);
        await Task.WhenAll(operations);
        await transaction.CommitAsync();
        await AssertOffsetsCommittedAsync(cluster, operationCount);
    }

    private static KafkaFaultBarrier[] CreateOffsetBarriers(
        InMemoryKafkaCluster cluster,
        KafkaFaultScope scope,
        int operationCount)
    {
        var barriers = new KafkaFaultBarrier[operationCount];
        for (var index = 0; index < barriers.Length; index++)
            barriers[index] = cluster.FaultPlan.PauseNext(scope);
        return barriers;
    }

    private static Task[] StageOffsets(
        ITransaction<string, string> transaction,
        int operationCount)
    {
        var operations = new Task[operationCount];
        for (var index = 0; index < operations.Length; index++)
        {
            operations[index] = transaction.SendOffsetsToTransactionAsync(
                [new TopicPartitionOffset($"orders-{index}", 0, index + 1)],
                "billing").AsTask();
        }
        return operations;
    }

    private static async Task WaitUntilEnteredAsync(KafkaFaultBarrier[] barriers)
    {
        for (var index = 0; index < barriers.Length; index++)
            await barriers[index].WaitUntilEnteredAsync();
    }

    private static async Task ReleaseAsync(KafkaFaultBarrier[] barriers)
    {
        for (var index = 0; index < barriers.Length; index++)
            await Assert.That(barriers[index].Release()).IsTrue();
    }

    private static async Task AssertOffsetsCommittedAsync(
        InMemoryKafkaCluster cluster,
        int operationCount)
    {
        for (var index = 0; index < operationCount; index++)
        {
            await Assert.That(cluster.GetCommittedOffset(
                    "billing",
                    new TopicPartition($"orders-{index}", 0)))
                .IsEqualTo(index + 1);
        }
    }

    [Test]
    public async Task DeleteRecords_PreservesOpenTransactionBoundary()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        var firstTransactionalProducer = new InMemoryProducer<string, string>(cluster);
        var secondTransactionalProducer = new InMemoryProducer<string, string>(cluster);
        var ordinaryProducer = new InMemoryProducer<string, string>(cluster);
        await using var firstTransaction = firstTransactionalProducer.BeginTransaction();
        await using var secondTransaction = secondTransactionalProducer.BeginTransaction();
        _ = await firstTransaction.ProduceAsync("orders", "k", "first-pending");
        _ = await secondTransaction.ProduceAsync("orders", "k", "second-pending");
        _ = await ordinaryProducer.ProduceAsync("orders", "k", "later");
        await using var admin = new InMemoryAdminClient(cluster);
        var topicPartition = new TopicPartition("orders", 0);

        _ = await admin.DeleteRecordsAsync(new Dictionary<TopicPartition, long>
        {
            [topicPartition] = 2
        });

        await Assert.That(cluster.TryRead(
            topicPartition,
            2,
            out _,
            out var blockedByOngoingTransaction)).IsFalse();
        await Assert.That(blockedByOngoingTransaction).IsTrue();

        await firstTransaction.CommitAsync();

        await Assert.That(cluster.TryRead(
            topicPartition,
            2,
            out _,
            out blockedByOngoingTransaction)).IsFalse();
        await Assert.That(blockedByOngoingTransaction).IsTrue();

        await secondTransaction.CommitAsync();

        await Assert.That(cluster.TryRead(topicPartition, 2, out var visible)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(visible.Value)).IsEqualTo("later");
    }

    [Test]
    public async Task TransactionCommit_AcceptsCurrentStaticConsumerGroupMetadata()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { GroupId = "billing" });
        consumer.Subscribe("orders");
        var current = consumer.ConsumerGroupMetadata!;
        var staticMetadata = new ConsumerGroupMetadata
        {
            GroupId = current.GroupId,
            GenerationId = current.GenerationId,
            MemberId = current.MemberId,
            GroupInstanceId = "billing-worker-1"
        };
        var producer = new InMemoryProducer<string, string>(cluster);
        await using var transaction = producer.BeginTransaction();
        await transaction.SendOffsetsToTransactionAsync(
            [new TopicPartitionOffset("orders", 0, 17)],
            staticMetadata);

        await transaction.CommitAsync();

        await Assert.That(cluster.GetCommittedOffset("billing", new TopicPartition("orders", 0)))
            .IsEqualTo(17);
    }

    [Test]
    public async Task PreparedTransaction_ProducerCompletionSupportsCommitAndAbort()
    {
        var producer = new InMemoryProducer<string, string>(new InMemoryKafkaCluster());

        await using (var transaction = producer.BeginTransaction())
        {
            var prepared = await transaction.PrepareAsync();
            await Assert.That(prepared.HasTransaction).IsTrue();
            await producer.CompletePreparedTransactionAsync(prepared, committed: true);
        }

        await using (var transaction = producer.BeginTransaction())
        {
            var prepared = await transaction.PrepareAsync();
            await producer.CompletePreparedTransactionAsync(prepared, committed: false);
        }

        await using var recovered = producer.BeginTransaction();
        await recovered.AbortAsync();
    }

    [Test]
    public async Task PreparedTransaction_ReplacementProducerRecoversCommitAndAbort()
    {
        var cluster = new InMemoryKafkaCluster();
        var original = new InMemoryProducer<string, string>(cluster);
        var committedTransaction = original.BeginTransaction();
        _ = await committedTransaction.ProduceAsync("orders", "k", "committed");
        var committedState = await committedTransaction.PrepareAsync();
        await original.DisposeAsync();

        var replacement = new InMemoryProducer<string, string>(cluster);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await replacement.CompletePreparedTransactionAsync(committedState, committed: true);

        var abortedTransaction = replacement.BeginTransaction();
        _ = await abortedTransaction.ProduceAsync("orders", "k", "aborted");
        var abortedState = await abortedTransaction.PrepareAsync();
        await replacement.DisposeAsync();

        var secondReplacement = new InMemoryProducer<string, string>(cluster);
        await secondReplacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await secondReplacement.CompletePreparedTransactionAsync(abortedState, committed: false);

        var visible = cluster.ReadRecords("orders");
        await Assert.That(visible).Count().IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(visible[0].Value)).IsEqualTo("committed");
        await committedTransaction.DisposeAsync();
        await abortedTransaction.DisposeAsync();
    }

    [Test]
    public async Task PreparedTransaction_DisposingHandlePreservesRecoveryState()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var transaction = producer.BeginTransaction();
        _ = await transaction.ProduceAsync("orders", "k", "prepared");
        var prepared = await transaction.PrepareAsync();

        await transaction.DisposeAsync();
        await producer.CompletePreparedTransactionAsync(prepared, committed: true);

        await Assert.That(cluster.ReadRecords("orders")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task PreparedTransaction_RecoveryRejectsAnotherActiveTransaction()
    {
        var cluster = new InMemoryKafkaCluster();
        var original = new InMemoryProducer<string, string>(cluster);
        var preparedTransaction = original.BeginTransaction();
        _ = await preparedTransaction.ProduceAsync("orders", "k", "prepared");
        var prepared = await preparedTransaction.PrepareAsync();
        await original.DisposeAsync();

        var replacement = new InMemoryProducer<string, string>(cluster);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        var activeTransaction = replacement.BeginTransaction();

        await Assert.That(async () =>
                await replacement.CompletePreparedTransactionAsync(prepared, committed: true))
            .Throws<InvalidOperationException>();

        await activeTransaction.AbortAsync();
        await replacement.CompletePreparedTransactionAsync(prepared, committed: true);
        await Assert.That(cluster.ReadRecords("orders")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task PreparedTransaction_RecoveryClaimsLifecycleBeforeAwaitingCompletion()
    {
        var cluster = new InMemoryKafkaCluster();
        var original = new InMemoryProducer<string, string>(cluster);
        var transaction = original.BeginTransaction();
        _ = await transaction.ProduceAsync("orders", "k", "prepared");
        var prepared = await transaction.PrepareAsync();
        await original.DisposeAsync();

        var replacement = new InMemoryProducer<string, string>(cluster);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.CommitTransaction));
        var pendingRecovery = replacement
            .CompletePreparedTransactionAsync(prepared, committed: true)
            .AsTask();

        await barrier.WaitUntilEnteredAsync();
        await Assert.That(() => replacement.BeginTransaction()).Throws<InvalidOperationException>();
        await Assert.That(barrier.Release()).IsTrue();
        await pendingRecovery;

        await using var recovered = replacement.BeginTransaction();
        await recovered.AbortAsync();
        await Assert.That(cluster.ReadRecords("orders")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task PreparedTransaction_StatesAreUniqueAcrossProducerGenericTypes()
    {
        var cluster = new InMemoryKafkaCluster();
        var firstProducer = new InMemoryProducer<ProducerKeyA, string>(
            cluster,
            ProducerKeySerializer<ProducerKeyA>.Instance,
            Serializers.String);
        var secondProducer = new InMemoryProducer<ProducerKeyB, string>(
            cluster,
            ProducerKeySerializer<ProducerKeyB>.Instance,
            Serializers.String);
        var firstTransaction = firstProducer.BeginTransaction();
        var secondTransaction = secondProducer.BeginTransaction();
        _ = await firstTransaction.ProduceAsync("strings", new ProducerKeyA(), "value");
        _ = await secondTransaction.ProduceAsync("ints", new ProducerKeyB(), "value");
        var firstState = await firstTransaction.PrepareAsync();
        var secondState = await secondTransaction.PrepareAsync();
        await firstProducer.DisposeAsync();
        await secondProducer.DisposeAsync();

        await Assert.That(firstState).IsNotEqualTo(secondState);

        var firstReplacement = new InMemoryProducer<ProducerKeyA, string>(
            cluster,
            ProducerKeySerializer<ProducerKeyA>.Instance,
            Serializers.String);
        await firstReplacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await firstReplacement.CompletePreparedTransactionAsync(firstState, committed: true);
        var secondReplacement = new InMemoryProducer<ProducerKeyB, string>(
            cluster,
            ProducerKeySerializer<ProducerKeyB>.Instance,
            Serializers.String);
        await secondReplacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await secondReplacement.CompletePreparedTransactionAsync(secondState, committed: true);

        await Assert.That(cluster.ReadRecords("strings")).Count().IsEqualTo(1);
        await Assert.That(cluster.ReadRecords("ints")).Count().IsEqualTo(1);
        await firstTransaction.DisposeAsync();
        await secondTransaction.DisposeAsync();
    }

    [Test]
    public async Task PreparedTransaction_RecoveryUsesReplacementProducerFatalState()
    {
        var cluster = new InMemoryKafkaCluster();
        var original = new InMemoryProducer<string, string>(cluster);
        var transaction = original.BeginTransaction();
        _ = await transaction.ProduceAsync("orders", "k", "v");
        var prepared = await transaction.PrepareAsync();
        var fenced = new FatalTransactionException(ErrorCode.ProducerFenced, "original fenced");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.CommitTransaction),
            fenced);
        _ = await Assert.ThrowsAsync<FatalTransactionException>(() =>
            original.CompletePreparedTransactionAsync(prepared, committed: true).AsTask());
        await original.DisposeAsync();

        var replacement = new InMemoryProducer<string, string>(cluster);
        await replacement.InitTransactionsAsync(keepPreparedTransaction: true);
        await replacement.CompletePreparedTransactionAsync(prepared, committed: true);

        await Assert.That(cluster.ReadRecords("orders")).Count().IsEqualTo(1);
        await transaction.DisposeAsync();
    }

    [Test]
    public async Task ProducerFencing_CapturesOneFatalInstanceAndPoisonsOnlyProducer()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        var fenced = new FatalTransactionException(ErrorCode.ProducerFenced, "producer fenced");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.TransactionProduce, "orders"),
            fenced);
        await using var transaction = producer.BeginTransaction();

        var initial = await Assert.ThrowsAsync<FatalTransactionException>(
            () => transaction.ProduceAsync("orders", "k", "v").AsTask());
        var commit = await Assert.ThrowsAsync<FatalTransactionException>(
            () => transaction.CommitAsync().AsTask());
        var initialize = await Assert.ThrowsAsync<FatalTransactionException>(
            () => producer.InitTransactionsAsync().AsTask());
        var produce = await Assert.ThrowsAsync<FatalTransactionException>(
            () => producer.ProduceAsync("orders", "k", "v").AsTask());

        await Assert.That(initial).IsSameReferenceAs(fenced);
        await Assert.That(commit).IsSameReferenceAs(fenced);
        await Assert.That(initialize).IsSameReferenceAs(fenced);
        await Assert.That(produce).IsSameReferenceAs(fenced);
        await Assert.That(() => producer.BeginTransaction()).Throws<FatalTransactionException>();

        var replacement = new InMemoryProducer<string, string>(cluster);
        var metadata = await replacement.ProduceAsync("orders", "k", "recovered");
        await Assert.That(metadata.Offset).IsEqualTo(0);
    }

    private static Task<RecordMetadata> ProduceToPartitionAsync(
        InMemoryProducer<string, string> producer,
        int partition) =>
        producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "orders",
            Partition = partition,
            Key = "k",
            Value = "v"
        }).AsTask();

    private readonly struct ProducerKeyA;

    private readonly struct ProducerKeyB;

    private sealed class ProducerKeySerializer<T> : ISerializer<T>
    {
        public static readonly ProducerKeySerializer<T> Instance = new();

        public void Serialize<TWriter>(
            T value,
            ref TWriter destination,
            SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
        {
            destination.GetSpan(1)[0] = 1;
            destination.Advance(1);
        }
    }
}
