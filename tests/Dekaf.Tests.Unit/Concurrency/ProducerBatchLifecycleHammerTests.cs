using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Concurrency;

/// <summary>
/// Repeatedly races the three production cleanup owners for a batch: successful sender
/// completion, failed sender completion, and the disposal orphan sweep. Persistent workers
/// and a reusable barrier explore interleavings without multiplying TUnit test instances.
/// </summary>
[NotInParallel]
public sealed class ProducerBatchLifecycleHammerTests
{
    private const int IterationCount = 10_000;
    private const int WorkerCount = 3;

    private static readonly FieldInfo InFlightBatchCountField = typeof(RecordAccumulator)
        .GetField("_inFlightBatchCount", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly FieldInfo ReadyBatchPoolField = typeof(RecordAccumulator)
        .GetField("_readyBatchPool", BindingFlags.NonPublic | BindingFlags.Instance)!;

    [Test]
    [Timeout(120_000)]
    public async Task CompleteFailAndDisposeRace_AllAcceptedMessagesTerminateExactlyOnce(
        CancellationToken cancellationToken)
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "batch-lifecycle-hammer",
            BufferMemory = 16 * 1024 * 1024,
            BatchSize = 1024,
            LingerMs = 1_000
        };

        var accumulator = new RecordAccumulator(options);
        var completionPool = new ValueTaskSourcePool<RecordMetadata>();
        using var workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var phaseBarrier = new Barrier(WorkerCount + 1);
        var workerErrors = new ConcurrentQueue<Exception>();

        ReadyBatch? currentBatch = null;
        var currentIteration = 0;

        var workers = Enumerable.Range(0, WorkerCount)
            .Select(workerId => Task.Factory.StartNew(
                () => RunWorker(workerId),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        ProducerDebugCounters.Reset();

        try
        {
            for (var iteration = 0; iteration < IterationCount; iteration++)
            {
                var topicPartition = new TopicPartition("hammer-topic", iteration & 3);
                var completion = completionPool.Rent();
                var completionTask = completion.Task.AsTask();
                var callbackInvocations = 0;
                Exception? callbackException = null;
                var awaitedValue = RentPayload(iteration);
                var callbackValue = RentPayload(~iteration);

                var callbackAccepted = await accumulator.AppendAsync(
                    topicPartition.Topic,
                    topicPartition.Partition,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    PooledMemory.Null,
                    callbackValue,
                    headers: null,
                    headerCount: 0,
                    completionSource: null,
                    (_, exception) =>
                    {
                        Volatile.Write(ref callbackException, exception);
                        Interlocked.Increment(ref callbackInvocations);
                    },
                    cancellationToken);
                var awaitedAccepted = await accumulator.AppendAsync(
                    topicPartition.Topic,
                    topicPartition.Partition,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    PooledMemory.Null,
                    awaitedValue,
                    headers: null,
                    headerCount: 0,
                    completion,
                    callback: null,
                    cancellationToken);

                await Assert.That(awaitedAccepted).IsTrue();
                await Assert.That(callbackAccepted).IsTrue();

                // FlushAsync deterministically seals both records, then remains pending on the
                // in-flight counter while the three cleanup owners race below.
                var flushTask = accumulator.FlushAsync(cancellationToken).AsTask();
                await Assert.That(accumulator.TryDrainBatch(out var batch)).IsTrue();

                var generation = batch!.Generation;
                var doneTask = batch.DoneTask.AsTask();
                await Assert.That(batch.RecordBatch.Records.Count).IsEqualTo(2);
                await Assert.That(batch.RecordBatch.Records[0].Value.Length).IsEqualTo(64);
                await Assert.That(batch.RecordBatch.Records[1].Value.Length).IsEqualTo(64);
                currentBatch = batch;
                currentIteration = iteration;

                phaseBarrier.SignalAndWait(cancellationToken);
                phaseBarrier.SignalAndWait(cancellationToken);
                await flushTask.WaitAsync(cancellationToken);

                if (!workerErrors.IsEmpty)
                    throw new AggregateException(workerErrors);

                var completed = false;
                try
                {
                    _ = await completionTask.WaitAsync(cancellationToken);
                    completed = true;
                }
                catch (Exception exception) when (exception is ExpectedHammerException or ObjectDisposedException)
                {
                    // Expected failure winner; awaiting proves completion source terminated.
                }

                var delivered = await doneTask.WaitAsync(cancellationToken);

                await Assert.That(delivered).IsEqualTo(completed);
                await Assert.That(Volatile.Read(ref callbackInvocations)).IsEqualTo(1);
                await Assert.That(Volatile.Read(ref callbackException) is null).IsEqualTo(completed);
                await Assert.That(batch.IsCurrentIncarnation(generation)).IsFalse();
                await Assert.That(batch.IsReturnedToPool).IsTrue();
                await Assert.That(accumulator.BufferedBytes).IsEqualTo(0L);
                await Assert.That(GetInFlightBatchCount(accumulator)).IsEqualTo(0L);
            }

#if DEBUG
            var counters = ProducerDebugCounters.GetSnapshot();
            await Assert.That(counters.MessagesAppended).IsEqualTo(IterationCount * 2);
            await Assert.That(counters.MessagesAppendedWithCompletion).IsEqualTo(IterationCount);
            await Assert.That(counters.MessagesAppendedFireAndForget).IsEqualTo(IterationCount);
            await Assert.That(counters.MessagesAppendedWithCompletion)
                .IsEqualTo(counters.CompletionSourcesStoredInBatch);
            await Assert.That(counters.CompletionSourcesStoredInBatch)
                .IsEqualTo(counters.CompletionSourcesCompleted + counters.CompletionSourcesFailed);
            await Assert.That(counters.BatchesCompleted).IsEqualTo(IterationCount);
            await Assert.That(counters.BatchesSentSuccessfully + counters.BatchesFailed)
                .IsEqualTo(IterationCount);
            await Assert.That(counters.ReadyBatchesRented).IsEqualTo(IterationCount);
            await Assert.That(counters.ReadyBatchesReturned).IsEqualTo(IterationCount);
            await Assert.That(counters.ReadyBatchDuplicateReturns)
                .IsGreaterThanOrEqualTo(IterationCount);
            await Assert.That(counters.FlushCalls).IsEqualTo(IterationCount);
#endif

            await AssertConcurrentReadyBatchRentsAreUnique(accumulator, cancellationToken);
        }
        finally
        {
            workerCts.Cancel();
            try
            {
                await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            }
            catch (OperationCanceledException) when (workerCts.IsCancellationRequested)
            {
                // Expected when controller leaves before all hammer phases complete.
            }

            await accumulator.DisposeAsync();
            await completionPool.DisposeAsync();
            ProducerDebugCounters.Reset();
        }

        void RunWorker(int workerId)
        {
            try
            {
                for (var iteration = 0; iteration < IterationCount; iteration++)
                {
                    phaseBarrier.SignalAndWait(workerCts.Token);

                    try
                    {
                        PerturbSchedule(currentIteration, workerId);
                        var batch = currentBatch!;
                        switch ((currentIteration + workerId) % WorkerCount)
                        {
                            case 0:
                                CompleteAndCleanup(batch, succeeded: true);
                                break;
                            case 1:
                                CompleteAndCleanup(batch, succeeded: false);
                                break;
                            default:
                                accumulator.ForceFailAllInFlightBatches();
                                break;
                        }
                    }
                    catch (Exception exception)
                    {
                        workerErrors.Enqueue(exception);
                    }
                    finally
                    {
                        phaseBarrier.SignalAndWait(workerCts.Token);
                    }
                }
            }
            catch (OperationCanceledException) when (workerCts.IsCancellationRequested)
            {
                // Controller failed or test timed out; leave persistent worker promptly.
            }
        }

        void CompleteAndCleanup(ReadyBatch batch, bool succeeded)
        {
            if (succeeded)
                batch.CompleteSend(currentIteration, DateTimeOffset.UtcNow);
            else
                batch.Fail(new ExpectedHammerException());

            accumulator.ReleaseBatchMemory(batch);

            accumulator.OnBatchExitsPipeline(batch);
            accumulator.ReturnReadyBatch(batch);
        }
    }

    private static void PerturbSchedule(int iteration, int workerId)
    {
        Thread.SpinWait(((iteration + 1) * (workerId + 3) * 17) & 63);
        if (((iteration << 1) + workerId) % 7 == 0)
            Thread.Yield();
    }

    private static long GetInFlightBatchCount(RecordAccumulator accumulator)
        => (long)InFlightBatchCountField.GetValue(accumulator)!;

    private static PooledMemory RentPayload(int seed)
    {
        var buffer = ProducerDataPool.BytePool.Rent(64);
        buffer.AsSpan(0, 64).Fill((byte)seed);
        return new PooledMemory(buffer, 64);
    }

    private static async Task AssertConcurrentReadyBatchRentsAreUnique(
        RecordAccumulator accumulator,
        CancellationToken cancellationToken)
    {
        var pool = (ReadyBatchPool)ReadyBatchPoolField.GetValue(accumulator)!;
        using var barrier = new Barrier(3);
        var rents = Enumerable.Range(0, 2)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    barrier.SignalAndWait(cancellationToken);
                    return pool.Rent();
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        barrier.SignalAndWait(cancellationToken);
        var batches = await Task.WhenAll(rents).WaitAsync(cancellationToken);

        try
        {
            await Assert.That(ReferenceEquals(batches[0], batches[1])).IsFalse();
        }
        finally
        {
            pool.Return(batches[0]);
            if (!ReferenceEquals(batches[0], batches[1]))
                pool.Return(batches[1]);
        }
    }

    private sealed class ExpectedHammerException : Exception;
}
