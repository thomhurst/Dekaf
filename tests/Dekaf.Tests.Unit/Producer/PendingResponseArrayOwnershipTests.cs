using System.Buffers;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for the one-time-return ownership of PendingResponse's rented arrays and the
/// buffer-memory release invariant of the send pipeline.
///
/// Background (CI run 29594249742): a double return of a PendingResponse batches/generations
/// array poisons ArrayPool.Shared process-wide — the pool hands the same array to two renters,
/// letting one request's cleanup corrupt another's slots. The corrupted request then skips its
/// post-write memory release, permanently orphaning BufferMemory reservations: appends block
/// until max.block.ms with the buffer "full" while nothing is in flight.
/// </summary>
public sealed class PendingResponseArrayOwnershipTests : ScriptedProduceResponseFixture
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(15);

    [Test]
    public async Task ArrayReturnGuard_FirstClaimWins_SecondIsRefused()
    {
        var guard = new BrokerSender.ArrayReturnGuard();

        await Assert.That(guard.TryClaim()).IsTrue();
        await Assert.That(guard.TryClaim()).IsFalse();
        await Assert.That(guard.TryClaim()).IsFalse();
    }

    [Test]
    public async Task ArrayReturnGuard_ConcurrentClaims_ExactlyOneWins()
    {
        var guard = new BrokerSender.ArrayReturnGuard();
        var winners = 0;
        var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            if (guard.TryClaim())
                Interlocked.Increment(ref winners);
        }));

        await Task.WhenAll(tasks);

        await Assert.That(winners).IsEqualTo(1);
    }

    [Test]
    public async Task PendingResponse_TryReturnBatchesArray_SecondCallIsNoOp()
    {
        // Standalone struct, never attached to a live sender. The claim lives in the
        // reference-typed guard, so it is shared across every by-value copy — the second
        // call must no-op even from a different copy.
        var pending = new BrokerSender.PendingResponse(
            default,
            ArrayPool<ReadyBatch>.Shared.Rent(1),
            ArrayPool<int>.Shared.Rent(1),
            Count: 0,
            EncodedBytes: 0,
            DataBytes: 0,
            RequestStartTime: 0,
            default);
        var copy = pending;

        var first = pending.TryReturnBatchesArray();
        var second = copy.TryReturnBatchesArray();

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
    }

    [Test]
    [Timeout(120_000)]
    public async Task BufferMemory_AckedWaves_AlwaysReleaseReservations(
        CancellationToken cancellationToken)
    {
        // Leak invariant: after every delivered wave the accumulator's buffered-byte counter
        // must return to zero. In run 29594249742 a delivered wave's reservations were never
        // released, so all subsequent appends starved for the full max.block.ms.
        const int waves = 20;
        const int dataSize = 100;
        var topic = "test-topic";
        var responseQueue = new Queue<TaskCompletionSource<ProduceResponse>>();
        for (var i = 0; i < waves; i++)
        {
            var response = new TaskCompletionSource<ProduceResponse>();
            response.SetResult(CreateSuccessResponse(topic, partition: 0, baseOffset: i));
            responseQueue.Enqueue(response);
        }

        var connection = new TestKafkaConnection();
        var scripted = RegisterScript(responseQueue);
        connection.SendProducePipelinedAfterWrite = () => new ValueTask<Task<ProduceResponse>>(scripted.Dequeue());
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);

        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            Acks = Acks.Leader,
            EnableIdempotence = false,
            RetryBackoffMs = 0,
            RetryBackoffMaxMs = 0,
            LingerMs = 0
        };
        var accumulator = new RecordAccumulator(options);
        var valueTaskSourcePool = new ValueTaskSourcePool<RecordMetadata>();
        var acked = 0;
        var sender = CreateSender(pool, options, accumulator,
            (_, _, _, _, _) => Interlocked.Increment(ref acked));
        var guarded = GuardUnscriptedSends(cancellationToken);

        try
        {
            for (var wave = 0; wave < waves; wave++)
            {
                await Assert.That(accumulator.TryReserveMemory(dataSize)).IsTrue();
                sender.Enqueue(CreateTestBatch(
                    valueTaskSourcePool, topic, partition: 0,
                    markMemoryReleased: false, dataSize: dataSize));

                var expectedAcks = wave + 1;
                await TestWait.UntilAsync(
                    () => Volatile.Read(ref acked) == expectedAcks
                        && accumulator.BufferedBytes == 0,
                    WaitTimeout,
                    () => $"Wave {expectedAcks}: acked={Volatile.Read(ref acked)}, " +
                        $"bufferedBytes={accumulator.BufferedBytes} (expected 0 — orphaned reservation)",
                    guarded,
                    pollInterval: TimeSpan.FromMilliseconds(1));
            }

            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        }
        finally
        {
            await sender.DisposeAsync();
            await accumulator.DisposeAsync();
            await valueTaskSourcePool.DisposeAsync();
        }
    }

}
