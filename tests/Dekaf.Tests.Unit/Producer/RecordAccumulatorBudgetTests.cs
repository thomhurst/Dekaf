using Dekaf.Producer;

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
