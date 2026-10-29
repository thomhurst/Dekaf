using Dekaf.Internal;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests verifying that <see cref="Dekaf.Internal.DekafMemoryBudget"/> rebalances
/// live producer instances when additional producers are built and disposed.
/// </summary>
[Category("Producer")]
public class MemoryBudgetTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private const ulong ProducerFloorBytes = 32UL * 1024 * 1024;

    // The budget is process-wide: every auto-tuned producer another test builds or disposes
    // while this runs changes producer1's share, so the "shrinks on second build, grows back
    // on dispose" arithmetic only holds when nothing else runs concurrently (the Producer
    // category runs up to 16 tests in parallel). A keyed constraint only serializes tests
    // that share the key.
    [Test]
    [NotInParallel]
    public async Task Producer_AutoTuned_RebalancesOnSecondBuild()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);

        // Pin the budget so rebalance is deterministic. BufferMemory = share / 6 (overhead
        // divisor), so 512 MiB gives about 85 MiB for one producer, 42 MiB for two. Both are above
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

    [Test]
    [NotInParallel]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConcurrentProducers_RebalanceAfterBuildAndDisposal(bool useClientBudget)
    {
        const ulong budget = 1200UL * 1024 * 1024;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(budget);
        await using var client = useClientBudget
            ? Kafka.Connect(KafkaContainer.BootstrapServers, builder => builder.WithMemoryBudget(budget))
            : null;

        async Task<KafkaProducer<string, string>> BuildProducerAsync(int index)
        {
            var builder = client is null
                ? Kafka.CreateProducer<string, string>().WithBootstrapServers(KafkaContainer.BootstrapServers)
                : client.CreateProducer<string, string>();
            return (KafkaProducer<string, string>)await builder
                .WithClientId($"concurrent-budget-{index}")
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();
        }

        var builds = new[] { BuildProducerAsync(1), BuildProducerAsync(2), BuildProducerAsync(3) };
        try
        {
            var producers = await Task.WhenAll(builds);
            var sharedLimit = budget / 3 / DekafMemoryBudget.ProducerOverheadDivisor;
            foreach (var producer in producers)
            {
                await Assert.That(producer.RecordAccumulator.MaxBufferMemory).IsEqualTo(sharedLimit);
                var result = await producer.ProduceAsync(topic, "shared", "value");
                await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
            }

            await Task.WhenAll(producers[1].DisposeAsync().AsTask(), producers[2].DisposeAsync().AsTask());

            await Assert.That(producers[0].RecordAccumulator.MaxBufferMemory)
                .IsEqualTo(budget / DekafMemoryBudget.ProducerOverheadDivisor);
            var finalResult = await producers[0].ProduceAsync(topic, "survivor", "value");
            await Assert.That(finalResult.Offset).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            foreach (var build in builds)
            {
                if (build.IsCompletedSuccessfully)
                    await (await build).DisposeAsync();
            }
            DekafMemoryBudget.ResetForTesting();
        }
    }
}
