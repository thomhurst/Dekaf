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

    private static ProducerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "flush-checkpoint-tests",
        BufferMemory = ulong.MaxValue,
        BatchSize = 100,
        LingerMs = 60_000
    };

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
