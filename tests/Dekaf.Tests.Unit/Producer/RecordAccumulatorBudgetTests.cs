using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for dynamic adjustment of <see cref="RecordAccumulator"/>'s buffer
/// memory limit via <c>SetMaxBufferMemory</c>.
/// </summary>
public class RecordAccumulatorBudgetTests
{
    private static ProducerOptions CreateOptions(ulong bufferMemory) =>
        new()
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "budget-test",
            BufferMemory = bufferMemory,
            BatchSize = 16384,
            LingerMs = 1000,
        };

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Append_RecordLargerThanBufferMemory_FailsWithoutQueueing(bool fromSpans)
    {
        const int BufferMemory = 1024;
        await using var accumulator = new RecordAccumulator(CreateOptions(BufferMemory));
        var value = new byte[BufferMemory];
        var append = fromSpans
            ? accumulator.AppendFromSpansAsync(
                "oversized", 0, 0, default, true, value, false,
                null, 0, null, CancellationToken.None)
            : AppendValue(accumulator, value.Length);

        await Assert.That(append.IsCompleted).IsTrue();
        var exception = await Assert.That(async () => await append).Throws<ProduceException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.MessageTooLarge);
        await Assert.That(exception.IsRetriable).IsFalse();
        await Assert.That(exception.Topic).IsEqualTo("oversized");
        await Assert.That(exception.Partition).IsEqualTo(0);
        await Assert.That(exception.Message).Contains("BufferMemory");
        await Assert.That(exception.Message).Contains(BufferMemory.ToString());
        await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);
    }

    [Test]
    public async Task Append_RecordEqualToBufferMemory_Succeeds()
    {
        const int ValueLength = 512;
        var recordSize = PartitionBatch.EstimateRecordSize(0, ValueLength, null, 0);
        await using var accumulator = new RecordAccumulator(CreateOptions((ulong)recordSize));

        await Assert.That(await AppendValue(accumulator, ValueLength)).IsTrue();
        await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DrainPendingAppends_LimitShrinks_RejectsOversizedRecordAndUnblocksOtherPartitions(
        bool queueSmallRecordFirst)
    {
        const int OriginalLimit = 4096;
        await using var accumulator = new RecordAccumulator(CreateOptions(OriginalLimit));
        await Assert.That(accumulator.TryReserveMemoryForTest(OriginalLimit)).IsTrue();

        ValueTask<bool> smallAppend = default;
        if (queueSmallRecordFirst)
            smallAppend = AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, "small", partition: 1);
        var oversizedAppend = AppendValue(accumulator, 2048);
        if (!queueSmallRecordFirst)
            smallAppend = AccumulatorTestHelpers.AppendNullRecordAsync(accumulator, "small", partition: 1);
        await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(2);

        accumulator.SetMaxBufferMemory(1024);
        accumulator.DrainPendingAppends();

        // Reject even behind an ordinary waiter that still cannot reserve memory.
        await Assert.That(oversizedAppend.IsCompleted).IsTrue();
        var exception = await Assert.That(async () => await oversizedAppend).Throws<ProduceException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.MessageTooLarge);
        await Assert.That(exception.Message).Contains("1024");
        await Assert.That(smallAppend.IsCompleted).IsFalse();
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(OriginalLimit);
        await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(1);

        accumulator.ReleaseMemory(OriginalLimit);

        await Assert.That(smallAppend.IsCompleted).IsTrue();
        await Assert.That(await smallAppend).IsTrue();
        await Assert.That(accumulator.PendingAppendCountForTest).IsEqualTo(0);
        await Assert.That(accumulator.TryGetBatch("oversized", 0, out _)).IsFalse();
    }

    private static ValueTask<bool> AppendValue(RecordAccumulator accumulator, int valueLength)
    {
        var value = ProducerDataPool.BytePool.Rent(valueLength);
        return accumulator.AppendAsync(
            "oversized", 0, 0, PooledMemory.Null, new PooledMemory(value, valueLength),
            null, 0, null, null, CancellationToken.None);
    }

    [Test]
    public async Task SetMaxBufferMemory_Grow_AllowsAdditionalReservation()
    {
        const int OneMb = 1024 * 1024;
        await using var accumulator = new RecordAccumulator(CreateOptions(OneMb));

        await Assert.That(accumulator.TryReserveMemoryForTest(OneMb)).IsTrue();
        await Assert.That(accumulator.TryReserveMemoryForTest(1)).IsFalse();

        accumulator.SetMaxBufferMemory(2 * OneMb);

        await Assert.That(accumulator.TryReserveMemoryForTest(OneMb)).IsTrue();
        await Assert.That(accumulator.MaxBufferMemory).IsEqualTo((ulong)(2 * OneMb));
    }

    [Test]
    public async Task SetMaxBufferMemory_Shrink_BlocksFurtherReservation()
    {
        const int OneMb = 1024 * 1024;
        await using var accumulator = new RecordAccumulator(CreateOptions(2 * OneMb));

        await Assert.That(accumulator.TryReserveMemoryForTest((3 * OneMb) / 2)).IsTrue();

        accumulator.SetMaxBufferMemory(OneMb);

        await Assert.That(accumulator.TryReserveMemoryForTest(1)).IsFalse();
        await Assert.That(accumulator.MaxBufferMemory).IsEqualTo((ulong)OneMb);
    }

    [Test]
    public async Task TryReserveMemory_OverLimit_RollsBackReservation()
    {
        await using var accumulator = new RecordAccumulator(CreateOptions(100));

        await Assert.That(accumulator.TryReserveMemoryForTest(90)).IsTrue();
        await Assert.That(accumulator.TryReserveMemoryForTest(20)).IsFalse();
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(90);
    }

    [Test]
    public async Task TryReserveMemory_ConcurrentReservationsBelowLimit_AllSucceed()
    {
        var taskCount = Math.Clamp(Environment.ProcessorCount * 2, 4, 32);
        const int OpsPerTask = 1_000;
        await using var accumulator = new RecordAccumulator(CreateOptions((ulong)(taskCount * OpsPerTask)));
        using var start = new ManualResetEventSlim();
        var failures = 0;

        var tasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(() =>
        {
            start.Wait();

            for (var i = 0; i < OpsPerTask; i++)
            {
                if (!accumulator.TryReserveMemoryForTest(1))
                    Interlocked.Increment(ref failures);
            }
        })).ToArray();

        start.Set();
        await Task.WhenAll(tasks);

        await Assert.That(failures).IsEqualTo(0);
        await Assert.That(accumulator.BufferedBytes).IsEqualTo(taskCount * OpsPerTask);
    }
}
