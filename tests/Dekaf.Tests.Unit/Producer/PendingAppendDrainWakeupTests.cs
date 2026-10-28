using System.Diagnostics;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// A pending append whose enqueuer loses the drain guard to an active owner relies on that owner
/// seeing its request when the owner releases the guard (#3858). Releasing the guard and reading
/// the request version is a store followed by a load, which the CPU may reorder unless the
/// release is a full fence: the owner then reads the old version while the enqueuer's CAS still
/// sees the guard held, and the append waits in the queue for max.block.ms with BufferMemory
/// almost empty.
/// </summary>
public sealed class PendingAppendDrainWakeupTests
{
    private const string Topic = "pending-append-drain-wakeup";
    private const int HandoffsPerRound = 256;
    // Two worker-owned partitions: two workers and the flushing thread race for the drain guard.
    private const int Partitions = 2;
    private static readonly TimeSpan RunFor = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StallBound = TimeSpan.FromSeconds(10);

    [Test]
    public async Task AppendWorkerHandoffs_AreNeverStrandedInThePendingQueue()
    {
        var accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "pending-append-drain-wakeup-tests",
            // Every worker handoff waits in the pending queue (the worker's own FIFO count keeps
            // it off the fast path), so each one needs a drain wake; memory is never short.
            BatchSize = 192,
            BufferMemory = 64L * 1024 * 1024,
            LingerMs = 60_000,
            DeliveryLatencyTargetMs = 0,
            MaxBlockMs = 60_000,
        });
        using var workerCts = new CancellationTokenSource();
        accumulator.StartAppendWorkers(workerCts.Token);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var handoffs = new ValueTask<RecordMetadata>[HandoffsPerRound];
        long nextOffset = 0;
        string? stall = null;

        try
        {
            await Task.Factory.StartNew(() =>
            {
                var run = Stopwatch.StartNew();
                while (stall is null && run.Elapsed < RunFor)
                {
                    var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    for (var i = 0; i < HandoffsPerRound; i++)
                    {
                        var completion = pool.Rent();
                        handoffs[i] = completion.Task;
                        accumulator.EnqueueAppend(
                            Topic, i % Partitions, ts, PooledMemory.Null, PooledMemory.Null,
                            null, 0, completion, CancellationToken.None);
                    }

                    // The flush covers every handoff, so it completes only once the worker
                    // appended each one; a stranded pending append holds it until max.block.ms.
                    var flush = accumulator.FlushAsync(CancellationToken.None);
                    var wait = Stopwatch.StartNew();
                    while (!flush.IsCompleted)
                    {
                        if (accumulator.TryDrainPublishedBatch(out var batch))
                        {
                            batch.CompleteSend(nextOffset, DateTimeOffset.UnixEpoch);
                            nextOffset += batch.RecordCount;
                            accumulator.ReleaseBatchMemory(batch);
                            accumulator.OnBatchExitsPipeline(batch);
                            accumulator.ReturnReadyBatch(batch);
                        }
                        else if (wait.Elapsed > StallBound)
                        {
                            stall = $"flush did not complete within {StallBound}: " +
                                $"{accumulator.PendingAppendCountForTest} pending append(s) queued";
                            return;
                        }
                        else
                        {
                            Thread.SpinWait(1);
                        }
                    }

                    flush.GetAwaiter().GetResult();
                    for (var i = 0; i < HandoffsPerRound; i++)
                        handoffs[i].GetAwaiter().GetResult();
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            await Assert.That(stall).IsNull();
        }
        finally
        {
            await accumulator.DisposeAsync();
            workerCts.Cancel();
            await pool.DisposeAsync();
        }
    }
}
