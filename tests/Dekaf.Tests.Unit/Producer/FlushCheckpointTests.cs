using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// FlushAsync waits for a checkpoint, not for global quiescence (#3386): it completes once
/// every batch in the pipeline at its checkpoint has left, even while newer batches from
/// concurrent producers stay in flight.
/// </summary>
public sealed class FlushCheckpointTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    [Test]
    public async Task FlushAsync_CompletesAtCheckpoint_WhileNewerBatchStaysInFlight()
    {
        const string topic = "flush-checkpoint";
        var accumulator = new RecordAccumulator(CreateOptions());
        AccumulatorTestHelpers.KeepBatchesOpenDespiteAppLimitedBypass(accumulator);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        ReadyBatch? flushedBatch = null;
        ReadyBatch? newerBatch = null;

        try
        {
            var beforeFlush = pool.Rent();
            var beforeFlushTask = beforeFlush.Task;
            await Assert.That(accumulator.TryAppendWithCompletion(
                topic,
                partition: 0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null,
                PooledMemory.Null,
                headers: null,
                headerCount: 0,
                beforeFlush)).IsTrue();

            var checkpointCaptured = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
            accumulator.AfterFlushCheckpointCapturedForTest = checkpoint => checkpointCaptured.TrySetResult(checkpoint);

            var flushTask = accumulator.FlushAsync(CancellationToken.None).AsTask();
            var flushCheckpoint = await checkpointCaptured.Task.WaitAsync(Bound);

            // Concurrent production after the checkpoint: a newer batch enters the pipeline.
            await Assert.That(await AccumulatorTestHelpers.AppendNullRecordAsync(
                accumulator, topic, partition: 1, partitionCount: 2)).IsTrue();
            await AccumulatorTestHelpers.SealAllAsync(accumulator);

            flushedBatch = await DrainAsync(accumulator, new TopicPartition(topic, 0));
            newerBatch = await DrainAsync(accumulator, new TopicPartition(topic, 1));

            await Assert.That(accumulator.IsFlushCheckpointReached(flushCheckpoint)).IsFalse();
            await Assert.That(flushTask.IsCompleted).IsFalse();

            CompleteAndReturn(accumulator, flushedBatch, baseOffset: 0);
            flushedBatch = null;

            // Before #3386 this waited for the newer batch too, so it never completed while
            // production continued.
            await flushTask.WaitAsync(Bound);
            await Assert.That((await beforeFlushTask).Offset).IsEqualTo(0);
            await Assert.That(accumulator.InFlightBatchCount).IsEqualTo(1)
                .Because("the batch produced after the checkpoint is still in flight");
        }
        finally
        {
            accumulator.AfterFlushCheckpointCapturedForTest = null;
            if (flushedBatch is not null)
                CompleteAndReturn(accumulator, flushedBatch, baseOffset: 0);
            if (newerBatch is not null)
                CompleteAndReturn(accumulator, newerBatch, baseOffset: 1);

            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task FlushAsync_CompletesUnderContinuousConcurrentProduction()
    {
        const string topic = "flush-checkpoint-continuous";
        const int partitionCount = 3;
        const int recordsBeforeFlush = 9;
        var accumulator = new RecordAccumulator(CreateOptions());
        AccumulatorTestHelpers.KeepBatchesOpenDespiteAppLimitedBypass(accumulator);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        using var stop = new CancellationTokenSource();
        Task? producer = null;
        Task<Queue<ReadyBatch>>? sender = null;
        long nextOffset = 0;

        try
        {
            var beforeFlush = new ValueTask<RecordMetadata>[recordsBeforeFlush];
            for (var i = 0; i < recordsBeforeFlush; i++)
            {
                var completion = pool.Rent();
                beforeFlush[i] = completion.Task;
                await Assert.That(accumulator.TryAppendWithCompletion(
                    topic,
                    partition: i % partitionCount,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    PooledMemory.Null,
                    PooledMemory.Null,
                    headers: null,
                    headerCount: 0,
                    completion)).IsTrue();
            }

            // One thread produces continuously to several partitions.
            producer = Task.Run(async () =>
            {
                var partition = 0;
                while (!stop.IsCancellationRequested)
                {
                    await AccumulatorTestHelpers.AppendNullRecordAsync(
                        accumulator, topic, partition, partitionCount);
                    partition = (partition + 1) % partitionCount;
                }
            });

            // A sender stand-in that always keeps its newest drained batch in flight, so the
            // pipeline never goes quiet while the producer runs. It completes older batches.
            sender = Task.Run(() =>
            {
                var inFlight = new Queue<ReadyBatch>();
                while (!stop.IsCancellationRequested)
                {
                    if (!accumulator.TryDrainPublishedBatch(out var batch))
                    {
                        Thread.Yield();
                        continue;
                    }

                    inFlight.Enqueue(batch);
                    while (inFlight.Count > 1)
                    {
                        var completed = inFlight.Dequeue();
                        CompleteAndReturn(accumulator, completed, Interlocked.Add(ref nextOffset, completed.RecordCount));
                    }
                }

                return inFlight;
            });

            await TestWait.UntilAsync(() => accumulator.InFlightBatchCount > 0, Bound);

            await accumulator.FlushAsync(CancellationToken.None).AsTask().WaitAsync(Bound);

            for (var i = 0; i < recordsBeforeFlush; i++)
            {
                await Assert.That(beforeFlush[i].IsCompletedSuccessfully).IsTrue()
                    .Because("every record appended before the flush started is delivered when it returns");
                _ = await beforeFlush[i];
            }

            await Assert.That(accumulator.InFlightBatchCount).IsGreaterThan(0)
                .Because("the flush returned while concurrent production kept batches in flight");
        }
        finally
        {
            stop.Cancel();
            if (producer is not null)
                await producer;

            if (sender is not null)
            {
                foreach (var batch in await sender)
                    CompleteAndReturn(accumulator, batch, Interlocked.Add(ref nextOffset, batch.RecordCount));
            }

            await AccumulatorTestHelpers.SealAllAsync(accumulator);
            while (accumulator.TryDrainBatch(out var remaining))
                CompleteAndReturn(accumulator, remaining, Interlocked.Add(ref nextOffset, remaining.RecordCount));

            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task FlushAsync_WaitsForRecordHandedToAppendWorker()
    {
        // A backpressured ProduceAsync returns once its record is queued for an append worker,
        // before the record is appended. The flush must still cover it.
        const string topic = "flush-checkpoint-handoff";
        var accumulator = new RecordAccumulator(CreateOptions());
        AccumulatorTestHelpers.KeepBatchesOpenDespiteAppLimitedBypass(accumulator);
        using var workerCts = new CancellationTokenSource();
        accumulator.StartAppendWorkers(workerCts.Token);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        using var releaseWorker = new ManualResetEventSlim(false);
        var workerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        accumulator.BeforeAppendWorkerAppendForTest = () =>
        {
            workerEntered.TrySetResult();
            releaseWorker.Wait(Bound);
        };
        ReadyBatch? batch = null;

        try
        {
            var completion = pool.Rent();
            var handedOff = completion.Task;
            accumulator.EnqueueAppend(
                topic,
                partition: 0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null,
                PooledMemory.Null,
                headers: null,
                headerCount: 0,
                completion,
                CancellationToken.None);
            await workerEntered.Task.WaitAsync(Bound);

            // Nothing is unsealed or in flight yet: before the handoff wait this took the
            // flush fast path and returned while the record was still queued.
            var flushTask = accumulator.FlushAsync(CancellationToken.None).AsTask();
            await Assert.That(flushTask.IsCompleted).IsFalse();

            releaseWorker.Set();
            batch = await DrainAsync(accumulator, new TopicPartition(topic, 0));
            await Assert.That(flushTask.IsCompleted).IsFalse();

            CompleteAndReturn(accumulator, batch, baseOffset: 0);
            batch = null;

            await flushTask.WaitAsync(Bound);
            await Assert.That((await handedOff).Offset).IsEqualTo(0);
        }
        finally
        {
            releaseWorker.Set();
            accumulator.BeforeAppendWorkerAppendForTest = null;
            if (batch is not null)
                CompleteAndReturn(accumulator, batch, baseOffset: 0);

            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task FlushAsync_IgnoresHandoffToLaterWorkerAfterFlushStarted()
    {
        // The flush waits for append workers one at a time. A record handed to a later worker
        // after the flush started must not join its checkpoint while it waits on an earlier one.
        const string topic = "flush-checkpoint-handoff-later-worker";
        var accumulator = new RecordAccumulator(CreateOptions());
        Skip.When(accumulator.AppendWorkerCountForTest < 2, "needs at least two append workers");
        AccumulatorTestHelpers.KeepBatchesOpenDespiteAppLimitedBypass(accumulator);
        using var workerCts = new CancellationTokenSource();
        accumulator.StartAppendWorkers(workerCts.Token);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        using var releaseFirst = new ManualResetEventSlim(false);
        using var releaseLater = new ManualResetEventSlim(false);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entries = 0;
        accumulator.BeforeAppendWorkerAppendForTest = () =>
        {
            // Blocks longer than Bound so the flush below cannot finish by outwaiting it.
            if (Interlocked.Increment(ref entries) == 1)
            {
                firstEntered.TrySetResult();
                releaseFirst.Wait(Bound * 3);
            }
            else
            {
                laterEntered.TrySetResult();
                releaseLater.Wait(Bound * 3);
            }
        };
        ReadyBatch? firstBatch = null;
        ReadyBatch? laterBatch = null;

        try
        {
            // Partition p goes to worker p % count, so partitions 0 and 1 use workers 0 and 1.
            var first = pool.Rent();
            var firstTask = first.Task;
            EnqueueNullRecord(accumulator, topic, partition: 0, first);
            await firstEntered.Task.WaitAsync(Bound);

            // FlushAsync runs synchronously up to its first wait, so it has taken its snapshot
            // when it returns; it is now waiting for worker 0.
            var flushTask = accumulator.FlushAsync(CancellationToken.None).AsTask();
            await Assert.That(flushTask.IsCompleted).IsFalse();

            var later = pool.Rent();
            var laterTask = later.Task;
            EnqueueNullRecord(accumulator, topic, partition: 1, later);
            await laterEntered.Task.WaitAsync(Bound);

            releaseFirst.Set();
            firstBatch = await DrainAsync(accumulator, new TopicPartition(topic, 0));
            CompleteAndReturn(accumulator, firstBatch, baseOffset: 0);
            firstBatch = null;

            // Before the fix the flush read worker 1's sequence only after worker 0 caught up,
            // so it also waited for the record handed off after it started.
            await flushTask.WaitAsync(Bound);
            await Assert.That((await firstTask).Offset).IsEqualTo(0);
            await Assert.That(laterTask.IsCompleted).IsFalse()
                .Because("the record handed off after the flush started is still in its worker");

            releaseLater.Set();
            await TestWait.UntilAsync(() => accumulator.UnsealedBatchCount > 0, Bound);
            await AccumulatorTestHelpers.SealAllAsync(accumulator);
            laterBatch = await DrainAsync(accumulator, new TopicPartition(topic, 1));
            CompleteAndReturn(accumulator, laterBatch, baseOffset: 0);
            laterBatch = null;
            await Assert.That((await laterTask).Offset).IsEqualTo(0);
        }
        finally
        {
            releaseFirst.Set();
            releaseLater.Set();
            accumulator.BeforeAppendWorkerAppendForTest = null;
            if (firstBatch is not null)
                CompleteAndReturn(accumulator, firstBatch, baseOffset: 0);
            if (laterBatch is not null)
                CompleteAndReturn(accumulator, laterBatch, baseOffset: 0);

            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task FlushAsync_SealsOpenBatchWhileAppendWorkerWaitsForBufferMemory()
    {
        // An open batch holds all of BufferMemory and a handed-off record waits in its append
        // worker for that memory. Only the open batch's delivery releases it, so the flush must
        // seal that batch before waiting for the worker; otherwise both wait until linger expires.
        const string topic = "flush-checkpoint-handoff-memory";
        var recordSize = PartitionBatch.EstimateRecordSize(0, 0, null, 0);
        var accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "flush-checkpoint-tests",
            BufferMemory = (ulong)recordSize,
            MaxBlockMs = 60_000,
            BatchSize = 100,
            LingerMs = 60_000
        });
        AccumulatorTestHelpers.KeepBatchesOpenDespiteAppLimitedBypass(accumulator);
        using var workerCts = new CancellationTokenSource();
        accumulator.StartAppendWorkers(workerCts.Token);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        ReadyBatch? batch = null;
        long nextOffset = 0;

        try
        {
            var first = pool.Rent();
            var firstTask = first.Task;
            await Assert.That(accumulator.TryAppendWithCompletion(
                topic,
                partition: 0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null,
                PooledMemory.Null,
                headers: null,
                headerCount: 0,
                first)).IsTrue();

            var handoff = pool.Rent();
            var handoffTask = handoff.Task;
            accumulator.EnqueueAppend(
                topic,
                partition: 0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PooledMemory.Null,
                PooledMemory.Null,
                headers: null,
                headerCount: 0,
                handoff,
                CancellationToken.None);
            await TestWait.UntilAsync(() => accumulator.PendingAppendCountForTest > 0, Bound);

            var flushTask = accumulator.FlushAsync(CancellationToken.None).AsTask();

            // Before the fix the flush waited for the worker without sealing, so this batch
            // stayed open (linger is 60 s) and never drained.
            batch = await DrainAsync(accumulator, new TopicPartition(topic, 0));
            await Assert.That(batch.RecordCount).IsEqualTo(1);
            CompleteAndReturn(accumulator, batch, nextOffset++);
            batch = null;

            // The released memory lets the worker append; the flush then seals that batch too.
            batch = await DrainAsync(accumulator, new TopicPartition(topic, 0));
            await Assert.That(flushTask.IsCompleted).IsFalse();
            CompleteAndReturn(accumulator, batch, nextOffset++);
            batch = null;

            await flushTask.WaitAsync(Bound);
            await Assert.That((await firstTask).Offset).IsEqualTo(0);
            await Assert.That((await handoffTask).Offset).IsEqualTo(1);
        }
        finally
        {
            if (batch is not null)
                CompleteAndReturn(accumulator, batch, nextOffset);

            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    private static ProducerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "flush-checkpoint-tests",
        BufferMemory = ulong.MaxValue,
        BatchSize = 100,
        LingerMs = 60_000
    };

    private static void EnqueueNullRecord(
        RecordAccumulator accumulator, string topic, int partition, PooledValueTaskSource<RecordMetadata> completion) =>
        accumulator.EnqueueAppend(
            topic,
            partition,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PooledMemory.Null,
            PooledMemory.Null,
            headers: null,
            headerCount: 0,
            completion,
            CancellationToken.None,
            partitionCount: 2);

    private static async Task<ReadyBatch> DrainAsync(RecordAccumulator accumulator, TopicPartition topicPartition)
    {
        ReadyBatch? batch = null;
        await TestWait.UntilAsync(() => accumulator.TryDrainBatch(topicPartition, out batch), Bound);
        return batch!;
    }

    private static void CompleteAndReturn(RecordAccumulator accumulator, ReadyBatch batch, long baseOffset)
    {
        batch.CompleteSend(baseOffset, DateTimeOffset.UnixEpoch);
        accumulator.ReleaseBatchMemory(batch);
        accumulator.OnBatchExitsPipeline(batch);
        accumulator.ReturnReadyBatch(batch);
    }
}
