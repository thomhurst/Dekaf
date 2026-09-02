using System.Reflection;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Covers the per-batch BufferMemory lease taken by the single-lock span append path: records
/// consume a batch-local lease, only an exhausted lease touches the accumulator-wide counter,
/// a lease degrades to the exact record when quantum headroom is unavailable, the unused
/// remainder is refunded at seal, and failure/dispose paths refund the whole lease.
/// </summary>
public sealed class BufferMemoryBatchLeaseTests
{
    private const string Topic = "buffer-lease-topic";
    private const int Quantum = 64 * 1024; // BatchSize (1 MB) clamped to the 64 KB lease cap

    private static ProducerOptions CreateOptions(ulong bufferMemory) => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "buffer-lease",
        BufferMemory = bufferMemory,
        BatchSize = 1_048_576,
        LingerMs = 60_000,
        MaxBlockMs = 30_000,
    };

    private static ValueTask<bool> AppendAsync(
        RecordAccumulator accumulator,
        ReadOnlySpan<byte> value,
        int partition = 0,
        CancellationToken cancellationToken = default)
        => accumulator.AppendFromSpansAsync(
            Topic,
            partition,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            keyData: default,
            keyIsNull: true,
            valueData: value,
            valueIsNull: false,
            headers: null,
            headerCount: 0,
            callback: null,
            cancellationToken);

    private static int EstimatedSize(byte[] value)
        => PartitionBatch.EstimateRecordSize(0, value.Length, null, 0);

    private static async ValueTask SealAllAsync(RecordAccumulator accumulator)
    {
        var method = typeof(RecordAccumulator).GetMethod(
            "SealBatchesAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (ValueTask)method.Invoke(accumulator, [true, CancellationToken.None])!;
    }

    private static List<ReadyBatch> DrainAll(RecordAccumulator accumulator)
    {
        var batches = new List<ReadyBatch>();
        while (accumulator.TryDrainPublishedBatch(out var batch))
            batches.Add(batch);
        return batches;
    }

    private static void RetireAll(RecordAccumulator accumulator, IEnumerable<ReadyBatch> batches)
    {
        var offset = 0;
        foreach (var batch in batches)
            LeakGateHarness.RetireBatch(accumulator, batch, offset++);
    }

    [Test]
    public async Task FastPath_LeasesOneQuantum_ConsumesItLocally_AndSealRefundsTheUnusedRemainder()
    {
        var accumulator = new RecordAccumulator(CreateOptions(64UL * 1024 * 1024));
        var value = new byte[100];
        var estimated = EstimatedSize(value);

        try
        {
            // The first record opens the batch on the two-phase path with an exact reservation.
            await Assert.That(await AppendAsync(accumulator, value)).IsTrue();
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(estimated);

            // The second record finds no batch-local credit and leases one quantum.
            await Assert.That(await AppendAsync(accumulator, value)).IsTrue();
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(estimated + Quantum);

            // Later records consume the lease without touching the accumulator-wide counter.
            for (var i = 0; i < 50; i++)
                await Assert.That(await AppendAsync(accumulator, value)).IsTrue();
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(estimated + Quantum);

            await Assert.That(accumulator.TryGetBatch(Topic, 0, out var open)).IsTrue();
            await Assert.That(open!.RecordCount).IsEqualTo(52);
            await Assert.That(open.MemoryLeaseBytes).IsEqualTo(estimated + Quantum);
            await Assert.That(open.ReservedSize).IsEqualTo(52 * estimated);
            await Assert.That(open.OverestimatedBytes).IsEqualTo(estimated + Quantum - open.EstimatedSize);

            // Seal refunds exactly the unused remainder: what stays reserved is the encoded size.
            await SealAllAsync(accumulator);
            var batches = DrainAll(accumulator);
            await Assert.That(batches.Count).IsEqualTo(1);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(batches[0].DataSize);
            await Assert.That(batches[0].DataSize).IsLessThan(estimated + Quantum);

            RetireAll(accumulator, batches);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task FastPath_RefillsAnotherQuantum_WhenTheLeaseIsConsumed()
    {
        var accumulator = new RecordAccumulator(CreateOptions(64UL * 1024 * 1024));
        var value = new byte[1_000];
        var estimated = EstimatedSize(value);
        var recordsPerQuantum = Quantum / estimated;

        try
        {
            // First record: exact. Second: first quantum. Then the quantum runs out once more.
            for (var i = 0; i < recordsPerQuantum + 2; i++)
                await Assert.That(await AppendAsync(accumulator, value)).IsTrue();

            await Assert.That(accumulator.BufferedBytes).IsEqualTo(estimated + 2L * Quantum);
            await Assert.That(accumulator.TryGetBatch(Topic, 0, out var open)).IsTrue();
            await Assert.That(open!.MemoryLeaseBytes).IsEqualTo(estimated + 2 * Quantum);

            await SealAllAsync(accumulator);
            var batches = DrainAll(accumulator);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(batches.Sum(static b => (long)b.DataSize));
            RetireAll(accumulator, batches);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task Lease_DegradesToTheExactRecord_WhenQuantumHeadroomIsUnavailable()
    {
        // Room for one quantum plus a little: the second partition's lease must not wait for
        // quantum headroom while its record itself fits.
        var value = new byte[100];
        var estimated = EstimatedSize(value);
        var accumulator = new RecordAccumulator(CreateOptions((ulong)(Quantum + 8 * estimated)));

        try
        {
            await Assert.That(await AppendAsync(accumulator, value, partition: 0)).IsTrue();
            await Assert.That(await AppendAsync(accumulator, value, partition: 0)).IsTrue();
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(estimated + Quantum);

            // Partition 1: exact first record, then a refill that can only grant the record.
            await Assert.That(await AppendAsync(accumulator, value, partition: 1)).IsTrue();
            var before = accumulator.BufferedBytes;
            var second = AppendAsync(accumulator, value, partition: 1);
            await Assert.That(second.IsCompleted).IsTrue();
            await Assert.That(await second).IsTrue();
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(before + estimated);
            await Assert.That((ulong)accumulator.BufferedBytes).IsLessThanOrEqualTo(accumulator.MaxBufferMemory);

            await Assert.That(accumulator.TryGetBatch(Topic, 1, out var second1)).IsTrue();
            await Assert.That(second1!.MemoryLeaseBytes).IsEqualTo(2 * estimated);

            await SealAllAsync(accumulator);
            var batches = DrainAll(accumulator);
            await Assert.That(batches.Count).IsEqualTo(2);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(batches.Sum(static b => (long)b.DataSize));
            RetireAll(accumulator, batches);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task ManyPartitionsHoldingLeases_NeverExceedTheLimit_AndBlockOnlyWhenTheRecordDoesNotFit(
        CancellationToken cancellationToken)
    {
        const int partitions = 8;
        var value = new byte[200];
        var estimated = EstimatedSize(value);
        // Three quanta of room: the other partitions must degrade to exact reservations.
        var limit = (ulong)(3 * Quantum + partitions * 4 * estimated);
        var accumulator = new RecordAccumulator(CreateOptions(limit));

        try
        {
            for (var round = 0; round < 3; round++)
            {
                for (var partition = 0; partition < partitions; partition++)
                {
                    var append = AppendAsync(accumulator, value, partition, cancellationToken);
                    await Assert.That(append.IsCompleted).IsTrue();
                    await Assert.That(await append).IsTrue();
                    await Assert.That((ulong)accumulator.BufferedBytes).IsLessThanOrEqualTo(limit);
                }
            }

            var quantumHolders = 0;
            for (var partition = 0; partition < partitions; partition++)
            {
                await Assert.That(accumulator.TryGetBatch(Topic, partition, out var open)).IsTrue();
                if (open!.MemoryLeaseBytes >= Quantum)
                    quantumHolders++;
                await Assert.That(open.MemoryLeaseBytes).IsGreaterThanOrEqualTo(open.ReservedSize);
            }
            await Assert.That(quantumHolders).IsEqualTo(3);

            // Fill the remainder until a record no longer fits: that append queues instead of
            // failing, and seal-time refunds of the three unused quanta let it through.
            ValueTask<bool> blocked;
            while (true)
            {
                blocked = AppendAsync(accumulator, value, partition: 0, cancellationToken);
                if (!blocked.IsCompleted)
                    break;
                await Assert.That(await blocked).IsTrue();
                await Assert.That((ulong)accumulator.BufferedBytes).IsLessThanOrEqualTo(limit);
            }

            await Assert.That((ulong)accumulator.BufferedBytes).IsLessThanOrEqualTo(limit);
            await SealAllAsync(accumulator);
            await Assert.That(await blocked).IsTrue();

            await SealAllAsync(accumulator);
            var batches = DrainAll(accumulator);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(batches.Sum(static b => (long)b.DataSize));
            RetireAll(accumulator, batches);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task Dispose_WithOpenLeases_RefundsEverything()
    {
        var accumulator = new RecordAccumulator(CreateOptions(64UL * 1024 * 1024));
        var value = new byte[100];

        for (var partition = 0; partition < 3; partition++)
        {
            await Assert.That(await AppendAsync(accumulator, value, partition)).IsTrue();
            await Assert.That(await AppendAsync(accumulator, value, partition)).IsTrue();
        }
        await Assert.That(accumulator.BufferedBytes).IsGreaterThan(3L * Quantum);

        await accumulator.DisposeAsync();

        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task TryReserveMemoryLease_GrantsQuantum_ThenExact_ThenNothing()
    {
        var accumulator = new RecordAccumulator(CreateOptions((ulong)(Quantum + 300)));

        try
        {
            await Assert.That(accumulator.TryReserveMemoryLease(100)).IsEqualTo(Quantum);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(Quantum);

            // 300 bytes left: the quantum does not fit, the record does.
            await Assert.That(accumulator.TryReserveMemoryLease(100)).IsEqualTo(100);
            await Assert.That(accumulator.TryReserveMemoryLease(200)).IsEqualTo(200);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(Quantum + 300);

            // Nothing left: no partial grant, no overshoot.
            await Assert.That(accumulator.TryReserveMemoryLease(1)).IsEqualTo(0);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(Quantum + 300);

            // A record larger than the quantum leases exactly itself when it fits.
            accumulator.ReleaseMemory(Quantum + 300);
            await Assert.That(accumulator.TryReserveMemoryLease(Quantum + 1)).IsEqualTo(Quantum + 1);
            accumulator.ReleaseMemory(Quantum + 1);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }
}
