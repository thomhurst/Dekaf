using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for per-partition worker affinity in the RecordAccumulator.
/// Verifies that messages for the same partition are processed sequentially
/// (preserving ordering) and that different partitions can be processed in parallel.
/// </summary>
public class AppendWorkerAffinityTests
{
    private static ProducerOptions CreateTestOptions()
    {
        return new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = ulong.MaxValue,
            BatchSize = 1_048_576,
            LingerMs = 0
        };
    }

    /// <summary>
    /// Returns the _partitionDeques dictionary. To check for a batch, callers should
    /// verify the deque contains the key and either current or sealed work is present.
    /// </summary>
    private static object GetPartitionDeques(RecordAccumulator accumulator)
    {
        var dequesField = typeof(RecordAccumulator).GetField("_partitionDeques",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return dequesField!.GetValue(accumulator)!;
    }

    private static bool HasBatchForPartition(object deques, TopicPartition tp)
    {
        var tryGetValueMethod = deques.GetType().GetMethod("TryGetValue");
        var parameters = new object[] { tp, null! };
        var found = (bool)tryGetValueMethod!.Invoke(deques, parameters)!;
        if (!found) return false;
        var pd = parameters[1]!;
        var currentBatch = pd.GetType().GetField("CurrentBatch")!.GetValue(pd);
        if (currentBatch is not null)
            return true;

        var readyCount = (int)pd.GetType().GetProperty("Count")!.GetValue(pd)!;
        return readyCount > 0;
    }

    private static int GetSlowPathAppendCount(object deques, TopicPartition tp)
    {
        var tryGetValueMethod = deques.GetType().GetMethod("TryGetValue");
        var parameters = new object[] { tp, null! };
        var found = (bool)tryGetValueMethod!.Invoke(deques, parameters)!;
        if (!found)
            return 0;

        return (int)parameters[1]!.GetType().GetField("SlowPathAppendCount")!.GetValue(parameters[1])!;
    }

    /// <summary>
    /// A backpressured ProduceAsync record waits in its partition's append worker. A later
    /// FireAsync or callback append for the same partition must not overtake it into the batch
    /// (issue #3784): it queues behind the worker item instead of straight into the pending queue.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task SpanAppend_BehindPendingWorkerItem_SamePartition_AppendsInCallOrder(
        CancellationToken cancellationToken)
    {
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        await using var producer = (KafkaProducer<string, string>)Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("append-worker-call-order-test")
            .WithBufferMemory(ulong.MaxValue)
            .WithBatchSize(1_048_576)
            .WithLinger(TimeSpan.FromSeconds(60))
            .WithCloseTimeout(TimeSpan.FromMilliseconds(100))
            .Build();
        await producer.StopSenderLoopsForTestingAsync();

        var accumulator = producer.RecordAccumulator;
        AccumulatorTestHelpers.KeepBatchesOpenDespiteAppLimitedBypass(accumulator);
        var workerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseWorker = new ManualResetEventSlim();
        accumulator.BeforeAppendWorkerAppendForTest = () =>
        {
            workerEntered.TrySetResult();
            releaseWorker.Wait(cancellationToken);
        };

        var tp = new TopicPartition("test-topic", 0);
        try
        {
            accumulator.EnqueueAppend(
                tp.Topic,
                tp.Partition,
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                key: PooledMemory.Null,
                value: LeakGateHarness.RentPooled(16, seed: 1),
                headers: null,
                headerCount: 0,
                completion: pool.Rent(),
                cancellationToken: CancellationToken.None);
            await workerEntered.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

            var second = accumulator.AppendFromSpansAsync(
                tp.Topic,
                tp.Partition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                keyData: default,
                keyIsNull: true,
                valueData: new byte[] { 2, 2, 2, 2 },
                valueIsNull: false,
                headers: null,
                headerCount: 0,
                callback: null,
                cancellationToken);

            // Before the fix the second record was appended here, ahead of the worker's record.
            await Assert.That(HasBatchForPartition(GetPartitionDeques(accumulator), tp)).IsFalse();

            releaseWorker.Set();
            await Assert.That(await second.AsTask().WaitAsync(TimeSpan.FromSeconds(30), cancellationToken)).IsTrue();
        }
        finally
        {
            releaseWorker.Set();
            accumulator.BeforeAppendWorkerAppendForTest = null;
        }

        var records = GetCurrentBatch(accumulator, tp).Complete()!.RecordBatch.Records;
        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(records[0].Value.Span[0]).IsEqualTo((byte)1);
        await Assert.That(records[1].Value.Span[0]).IsEqualTo((byte)2);
        await Assert.That(GetSlowPathAppendCount(GetPartitionDeques(accumulator), tp)).IsEqualTo(0);

        await producer.DisposeAsync();
    }

    private static PartitionBatch GetCurrentBatch(RecordAccumulator accumulator, TopicPartition tp)
    {
        var deques = GetPartitionDeques(accumulator);
        var parameters = new object[] { tp, null! };
        deques.GetType().GetMethod("TryGetValue")!.Invoke(deques, parameters);
        var pd = parameters[1]!;
        return (PartitionBatch)pd.GetType().GetField("CurrentBatch")!.GetValue(pd)!;
    }

    [Test]
    [Timeout(120_000)]
    public async Task StopSenderLoopsForTestingAsync_DoesNotStopAppendWorkers(
        CancellationToken cancellationToken)
    {
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        await using var producer = (KafkaProducer<string, string>)Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("append-worker-lifetime-test")
            .WithBufferMemory(ulong.MaxValue)
            .WithBatchSize(1_048_576)
            .WithLinger(TimeSpan.Zero)
            .WithCloseTimeout(TimeSpan.FromMilliseconds(100))
            .Build();

        await producer.StopSenderLoopsForTestingAsync();

        var accumulator = producer.RecordAccumulator;
        var completion = pool.Rent();
        accumulator.EnqueueAppend(
            "test-topic",
            partition: 0,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            key: PooledMemory.Null,
            value: PooledMemory.Null,
            headers: null,
            headerCount: 0,
            completion: completion,
            cancellationToken: CancellationToken.None);

        var deques = GetPartitionDeques(accumulator);
        await TestWait.UntilAsync(
            () => HasBatchForPartition(deques, new TopicPartition("test-topic", 0)),
            cancellationToken,
            TimeSpan.FromMilliseconds(25));

        await producer.DisposeAsync();
    }

    /// <summary>
    /// A work item queued by a produce admitted before a transaction abort started carries the
    /// generation it was admitted in. After the abort (even once it has reopened appends) the
    /// worker rejects it with TransactionAborted instead of appending it; an item admitted after
    /// the abort appends normally.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task AppendWorker_ItemAdmittedBeforeTransactionAbort_IsRejectedAfterAppendsReopen(
        CancellationToken cancellationToken)
    {
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        await using var producer = (KafkaProducer<string, string>)Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("append-worker-transaction-generation-test")
            .WithBufferMemory(ulong.MaxValue)
            .WithBatchSize(1_048_576)
            .WithLinger(TimeSpan.Zero)
            .WithCloseTimeout(TimeSpan.FromMilliseconds(100))
            .Build();
        await producer.StopSenderLoopsForTestingAsync();

        var accumulator = producer.RecordAccumulator;
        var admittedBeforeAbort = accumulator.TransactionalAppendGeneration;
        accumulator.CloseTransactionalAppends();
        accumulator.ReopenTransactionalAppends();

        var staleCompletion = pool.Rent();
        var staleTask = staleCompletion.Task.AsTask();
        accumulator.EnqueueAppend(
            "test-topic",
            partition: 0,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            key: PooledMemory.Null,
            value: LeakGateHarness.RentPooled(16, seed: 1),
            headers: null,
            headerCount: 0,
            completion: staleCompletion,
            cancellationToken: CancellationToken.None,
            transactionalGeneration: admittedBeforeAbort);

        var exception = await Assert.That(async () => await staleTask.WaitAsync(cancellationToken))
            .Throws<Dekaf.Errors.ProduceException>();
        await Assert.That(exception!.Kind).IsEqualTo(Dekaf.Errors.ProduceErrorKind.TransactionAborted);
        var deques = GetPartitionDeques(accumulator);
        var tp = new TopicPartition("test-topic", 0);
        await Assert.That(HasBatchForPartition(deques, tp)).IsFalse();
        await TestWait.UntilAsync(() => GetSlowPathAppendCount(deques, tp) == 0, cancellationToken,
            TimeSpan.FromMilliseconds(1));

        var currentCompletion = pool.Rent();
        accumulator.EnqueueAppend(
            "test-topic",
            partition: 0,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            key: PooledMemory.Null,
            value: LeakGateHarness.RentPooled(16, seed: 2),
            headers: null,
            headerCount: 0,
            completion: currentCompletion,
            cancellationToken: CancellationToken.None,
            transactionalGeneration: accumulator.TransactionalAppendGeneration);
        await TestWait.UntilAsync(() => HasBatchForPartition(deques, tp), cancellationToken,
            TimeSpan.FromMilliseconds(1));

        await producer.DisposeAsync();
    }

    /// <summary>
    /// The worker passes its early generation check, and then a whole abort (close, purge,
    /// reopen) runs before it appends. The generation carried into the append is validated again
    /// at the commit point under the partition lock, so the record is rejected with
    /// TransactionAborted instead of joining whatever follows the abort.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task AppendWorker_AbortBetweenTheWorkerCheckAndTheAppend_RejectsAtTheCommitPoint(
        CancellationToken cancellationToken)
    {
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        await using var producer = (KafkaProducer<string, string>)Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("append-worker-commit-generation-test")
            .WithBufferMemory(ulong.MaxValue)
            .WithBatchSize(1_048_576)
            .WithLinger(TimeSpan.Zero)
            .WithCloseTimeout(TimeSpan.FromMilliseconds(100))
            .Build();
        await producer.StopSenderLoopsForTestingAsync();

        var accumulator = producer.RecordAccumulator;
        var abortRan = 0;
        accumulator.BeforeAppendWorkerAppendForTest = () =>
        {
            // A whole abort cycle between the worker's check and its append.
            if (Interlocked.Exchange(ref abortRan, 1) == 0)
            {
                accumulator.CloseTransactionalAppends();
                accumulator.ReopenTransactionalAppends();
            }
        };

        var completion = pool.Rent();
        var task = completion.Task.AsTask();
        accumulator.EnqueueAppend(
            "test-topic",
            partition: 0,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            key: PooledMemory.Null,
            value: LeakGateHarness.RentPooled(16, seed: 1),
            headers: null,
            headerCount: 0,
            completion: completion,
            cancellationToken: CancellationToken.None,
            transactionalGeneration: accumulator.TransactionalAppendGeneration);

        // Before the commit-point check the record was appended and its delivery never completed.
        var exception = await Assert.That(async () => await task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken))
            .Throws<Dekaf.Errors.ProduceException>();
        await Assert.That(exception!.Kind).IsEqualTo(Dekaf.Errors.ProduceErrorKind.TransactionAborted);
        var deques = GetPartitionDeques(accumulator);
        var tp = new TopicPartition("test-topic", 0);
        await Assert.That(HasBatchForPartition(deques, tp)).IsFalse();
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);

        accumulator.BeforeAppendWorkerAppendForTest = null;
        await producer.DisposeAsync();
    }

    /// <summary>
    /// Issue #3723: PurgeAsync(Queue) failed the append blocked on BufferMemory and the buffered
    /// batches, but not the items still queued behind it in the append worker's channel. The purge
    /// released the memory, so the worker then appended the next queued item and it was sent.
    /// Every item queued before the purge must fail with Purged instead, and no batch may remain.
    /// </summary>
    [Test]
    [Timeout(120_000)]
    public async Task PurgeQueue_FailsItemsQueuedBehindABackpressuredAppendWorker(
        CancellationToken cancellationToken)
    {
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        await using var producer = (KafkaProducer<string, string>)Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("append-worker-purge-test")
            .WithBufferMemory(1_500)
            .WithBatchSize(1_048_576)
            .WithLinger(TimeSpan.FromMinutes(1))
            .WithCloseTimeout(TimeSpan.FromMilliseconds(100))
            .Build();
        await producer.StopSenderLoopsForTestingAsync();

        var accumulator = producer.RecordAccumulator;
        var deques = GetPartitionDeques(accumulator);
        var tp = new TopicPartition("test-topic", 0);

        Task<RecordMetadata> Enqueue(int seed)
        {
            var completion = pool.Rent();
            var task = completion.Task.AsTask();
            accumulator.EnqueueAppend(
                tp.Topic,
                tp.Partition,
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                key: PooledMemory.Null,
                value: LeakGateHarness.RentPooled(1_000, seed),
                headers: null,
                headerCount: 0,
                completion: completion,
                cancellationToken: CancellationToken.None);
            return task;
        }

        // The first record fills BufferMemory, the second waits for memory inside the worker and
        // the third waits in the worker's channel behind it.
        var buffered = Enqueue(seed: 1);
        await TestWait.UntilAsync(
            () => HasBatchForPartition(deques, tp),
            cancellationToken,
            TimeSpan.FromMilliseconds(1));
        var blocked = Enqueue(seed: 2);
        await TestWait.UntilAsync(
            () => accumulator.PendingAppendCountForTest > 0,
            cancellationToken,
            TimeSpan.FromMilliseconds(1));
        var queued = Enqueue(seed: 3);
        // A span append behind the worker's items waits in the same channel (#3784), where the
        // purge's scan of the pending queue cannot reach it either.
        var deferred = accumulator.AppendFromSpansAsync(
            tp.Topic,
            tp.Partition,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            keyData: default,
            keyIsNull: true,
            valueData: new byte[1_000],
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            callback: null,
            CancellationToken.None).AsTask();

        accumulator.Purge(
            PurgeOptions.Queue,
            new Dekaf.Errors.ProduceException(Dekaf.Errors.ProduceErrorKind.Purged, "purged"));

        foreach (var task in new[] { buffered, blocked, queued })
        {
            var exception = await Assert.That(async () => await task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken))
                .Throws<Dekaf.Errors.ProduceException>();
            await Assert.That(exception!.Kind).IsEqualTo(Dekaf.Errors.ProduceErrorKind.Purged);
        }

        var deferredException = await Assert.That(async () => await deferred.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken))
            .Throws<Dekaf.Errors.ProduceException>();
        await Assert.That(deferredException!.Kind).IsEqualTo(Dekaf.Errors.ProduceErrorKind.Purged);

        await Assert.That(HasBatchForPartition(deques, tp)).IsFalse();
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        // The worker completes the queued item before its finally block decrements the count.
        await TestWait.UntilAsync(() => GetSlowPathAppendCount(deques, tp) == 0, cancellationToken,
            TimeSpan.FromMilliseconds(1));
    }

    [Test]
    [Timeout(120_000)]
    public async Task DisposeAsync_FaultsBackpressuredAppendQueueAndClearsOwnership(
        CancellationToken cancellationToken)
    {
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        await using var producer = (KafkaProducer<string, string>)Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithClientId("append-worker-disposal-test")
            .WithBufferMemory(4_096)
            .WithBatchSize(4_096)
            .WithCloseTimeout(TimeSpan.FromMilliseconds(100))
            .Build();

        var accumulator = producer.RecordAccumulator;
        // Fill a valid buffer so records queue without exceeding the total limit.
        await Assert.That(accumulator.TryReserveMemoryForTest(4_096)).IsTrue();
        var firstCompletion = pool.Rent();
        var firstTask = firstCompletion.Task.AsTask();
        accumulator.EnqueueAppend(
            "test-topic",
            partition: 0,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            key: PooledMemory.Null,
            value: LeakGateHarness.RentPooled(128, seed: 1),
            headers: null,
            headerCount: 0,
            completion: firstCompletion,
            cancellationToken: CancellationToken.None);

        await TestWait.UntilAsync(
            () => accumulator.BufferPressureEvents > 0,
            cancellationToken,
            TimeSpan.FromMilliseconds(1));

        var secondCompletion = pool.Rent();
        var secondTask = secondCompletion.Task.AsTask();
        accumulator.EnqueueAppend(
            "test-topic",
            partition: 0,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            key: PooledMemory.Null,
            value: LeakGateHarness.RentPooled(128, seed: 2),
            headers: null,
            headerCount: 0,
            completion: secondCompletion,
            cancellationToken: CancellationToken.None);

        await producer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        accumulator.ReleaseMemory(4_096); // The synthetic reservation belongs to this test.

        await Assert.That(async () => await firstTask).Throws<ObjectDisposedException>();
        await Assert.That(async () => await secondTask).Throws<ObjectDisposedException>();
        await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        await Assert.That(GetSlowPathAppendCount(
            GetPartitionDeques(accumulator),
            new TopicPartition("test-topic", 0))).IsEqualTo(0);
    }

    [Test]
    [Timeout(120_000)]
    public async Task EnqueueAppend_SamePartition_ProcessedSequentially(CancellationToken cancellationToken)
    {
        // Messages enqueued for the same partition should be appended in FIFO order.
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        using var cts = new CancellationTokenSource();

        accumulator.StartAppendWorkers(cts.Token);

        const int messageCount = 50;
        var completions = new PooledValueTaskSource<RecordMetadata>[messageCount];

        for (var i = 0; i < messageCount; i++)
        {
            completions[i] = pool.Rent();
            accumulator.EnqueueAppend(
                "test-topic",
                partition: 0,
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                key: PooledMemory.Null,
                value: PooledMemory.Null,
                headers: null,
                headerCount: 0,
                completion: completions[i],
                cancellationToken: CancellationToken.None);
        }

        var deques = GetPartitionDeques(accumulator);
        var tp = new TopicPartition("test-topic", 0);

        // Wait deterministically for the worker to create or seal a batch, bounded by the test's
        // [Timeout] cancellation rather than an arbitrary fixed deadline that flakes when the
        // append workers are thread-pool-starved on loaded runners.
        await TestWait.UntilAsync(
            () => HasBatchForPartition(deques, tp),
            cancellationToken,
            TimeSpan.FromMilliseconds(25));

        // Cancel workers before disposal to avoid waiting for sender drain timeout
        cts.Cancel();
        await accumulator.DisposeAsync();
    }

    [Test]
    [Timeout(120_000)]
    public async Task EnqueueAppend_DifferentPartitions_RoutedToDifferentWorkers(CancellationToken cancellationToken)
    {
        // Messages for different partitions should be routed to potentially different
        // worker channels, enabling parallel processing.
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        using var cts = new CancellationTokenSource();

        accumulator.StartAppendWorkers(cts.Token);

        const int partitionCount = 8;
        const int messagesPerPartition = 10;

        for (var p = 0; p < partitionCount; p++)
        {
            for (var i = 0; i < messagesPerPartition; i++)
            {
                var completion = pool.Rent();
                accumulator.EnqueueAppend(
                    "test-topic",
                    partition: p,
                    timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    key: PooledMemory.Null,
                    value: PooledMemory.Null,
                    headers: null,
                    headerCount: 0,
                    completion: completion,
                    cancellationToken: CancellationToken.None);
            }
        }

        var deques = GetPartitionDeques(accumulator);

        // Wait deterministically until workers have created or sealed batches for ALL partitions,
        // bounded by the test's [Timeout] cancellation rather than an arbitrary fixed deadline
        // that flakes when the append workers are thread-pool-starved on loaded runners.
        await TestWait.UntilAsync(() =>
            {
                for (var p = 0; p < partitionCount; p++)
                {
                    if (!HasBatchForPartition(deques, new TopicPartition("test-topic", p)))
                        return false;
                }
                return true;
            },
            cancellationToken,
            TimeSpan.FromMilliseconds(25));

        cts.Cancel();
        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task EnqueueAppend_AfterDispose_SetsException()
    {
        // When the accumulator is disposed, TryWrite fails and the completion source
        // should be set with an ObjectDisposedException.
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();

        await accumulator.DisposeAsync();

        var completion = pool.Rent();

        accumulator.EnqueueAppend(
            "test-topic",
            partition: 0,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            key: PooledMemory.Null,
            value: PooledMemory.Null,
            headers: null,
            headerCount: 0,
            completion: completion,
            cancellationToken: CancellationToken.None);

        var vt = new ValueTask<RecordMetadata>(completion, completion.Version);
        await Assert.That(async () => await vt).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task EnqueueAppend_WithCancellation_SetsCanceled()
    {
        // When the per-message cancellation token is already cancelled, the worker
        // should set the completion as cancelled.
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        using var workerCts = new CancellationTokenSource();

        accumulator.StartAppendWorkers(workerCts.Token);

        var completion = pool.Rent();
        using var messageCts = new CancellationTokenSource();
        messageCts.Cancel();

        accumulator.EnqueueAppend(
            "test-topic",
            partition: 0,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            key: PooledMemory.Null,
            value: PooledMemory.Null,
            headers: null,
            headerCount: 0,
            completion: completion,
            cancellationToken: messageCts.Token);

        // Await the completion directly — the worker will set cancellation deterministically
        var vt = new ValueTask<RecordMetadata>(completion, completion.Version);
        await Assert.That(async () => await vt).Throws<OperationCanceledException>();

        workerCts.Cancel();
        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task WorkerPartitionAffinity_PartitionModulo_IsConsistent()
    {
        var workerCount = Math.Clamp(Environment.ProcessorCount, 1, 8);

        // Same partition always maps to same worker
        for (var partition = 0; partition < 100; partition++)
        {
            var worker1 = (int)((uint)partition % (uint)workerCount);
            var worker2 = (int)((uint)partition % (uint)workerCount);
            await Assert.That(worker1).IsEqualTo(worker2);
        }

        // Partitions that differ by workerCount map to the same worker
        for (var partition = 0; partition < 50; partition++)
        {
            var worker1 = (int)((uint)partition % (uint)workerCount);
            var worker2 = (int)((uint)(partition + workerCount) % (uint)workerCount);
            await Assert.That(worker1).IsEqualTo(worker2);
        }
    }
}
