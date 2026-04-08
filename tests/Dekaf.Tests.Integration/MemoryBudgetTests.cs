using Dekaf.Internal;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests verifying that <see cref="Dekaf.Internal.DekafMemoryBudget"/> rebalances
/// live producer instances when additional producers are built and disposed.
/// </summary>
public class MemoryBudgetTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private const ulong ProducerFloorBytes = 32UL * 1024 * 1024;

    [Test]
    [NotInParallel("DekafMemoryBudget")]
    public async Task Producer_AutoTuned_RebalancesOnSecondBuild()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);

        // Pin the budget so rebalance is deterministic. BufferMemory = share / 4 (overhead
        // divisor), so 512 MiB → 128 MiB for one producer, 64 MiB for two. Both are above
        // the 32 MiB producer floor so the "rebalanced < initial" assertion is meaningful.
        DekafMemoryBudget.SetBudget(512UL * 1024 * 1024);
        try
        {
        // Build first auto-tuned producer (no explicit BufferMemory).
        await using var producer1 = (KafkaProducer<string, string>)await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("budget-test-1")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var initialLimit = producer1.RecordAccumulator.MaxBufferMemory;
        Console.WriteLine($"[MemoryBudgetTests] producer1 initial limit: {initialLimit / (1024 * 1024)} MiB");

        // Build a second auto-tuned producer - this should trigger rebalance.
        var producer2 = (KafkaProducer<string, string>)await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("budget-test-2")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        try
        {
            var rebalancedLimit1 = producer1.RecordAccumulator.MaxBufferMemory;
            var limit2 = producer2.RecordAccumulator.MaxBufferMemory;
            Console.WriteLine($"[MemoryBudgetTests] after 2nd build - p1: {rebalancedLimit1 / (1024 * 1024)} MiB, p2: {limit2 / (1024 * 1024)} MiB");

            // producer1's limit should have shrunk (unless it was already at the floor).
            if (initialLimit > ProducerFloorBytes)
            {
                await Assert.That(rebalancedLimit1).IsLessThan(initialLimit);
            }

            // Both producers should receive the same per-instance share, respecting the floor.
            await Assert.That(rebalancedLimit1).IsEqualTo(limit2);
            await Assert.That(rebalancedLimit1).IsGreaterThanOrEqualTo(ProducerFloorBytes);

            // Verify both producers still function end-to-end.
            var result1 = await producer1.ProduceAsync(topic, "k1", "v1");
            var result2 = await producer2.ProduceAsync(topic, "k2", "v2");
            await Assert.That(result1.Offset).IsGreaterThanOrEqualTo(0);
            await Assert.That(result2.Offset).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            await producer2.DisposeAsync();
        }

        // After disposing producer2, producer1 should grow back to (roughly) the initial limit.
        await WaitForConditionAsync(
            () => producer1.RecordAccumulator.MaxBufferMemory >= initialLimit,
            TimeSpan.FromSeconds(5));

        var finalLimit = producer1.RecordAccumulator.MaxBufferMemory;
        Console.WriteLine($"[MemoryBudgetTests] producer1 final limit: {finalLimit / (1024 * 1024)} MiB");
        await Assert.That(finalLimit).IsGreaterThanOrEqualTo(initialLimit);
        }
        finally
        {
            DekafMemoryBudget.ResetForTesting();
        }
    }
}
