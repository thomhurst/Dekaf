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
        var accumulator = new RecordAccumulator(CreateOptions(OneMb));

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
        var accumulator = new RecordAccumulator(CreateOptions(2 * OneMb));

        await Assert.That(accumulator.TryReserveMemoryForTest((3 * OneMb) / 2)).IsTrue();

        accumulator.SetMaxBufferMemory(OneMb);

        await Assert.That(accumulator.TryReserveMemoryForTest(1)).IsFalse();
        await Assert.That(accumulator.MaxBufferMemory).IsEqualTo((ulong)OneMb);
    }
}
