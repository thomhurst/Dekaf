namespace Dekaf.Tests.Unit.Consumer;

using Dekaf.Consumer;
using Dekaf.Internal;
using Dekaf.Producer;

[NotInParallel("DekafMemoryBudget")]
public class KafkaConsumerBudgetTests
{
    [Test]
    public async Task KafkaConsumer_ImplementsIBudgetedInstance()
    {
        var type = typeof(KafkaConsumer<,>).MakeGenericType(typeof(string), typeof(string));
        await Assert.That(typeof(IBudgetedInstance).IsAssignableFrom(type)).IsTrue();
    }

    // Budgets are chosen so the per-instance split falls at or below the 64 MiB consumer
    // ceiling; any larger budget would be clamped and obscure the split math. See
    // DekafMemoryBudgetTests.CeilingAppliedWhenBudgetExceedsCap for the clamp regression guard.
    private const ulong ConsumerCeiling = 64UL * 1024 * 1024;

    [Test]
    public async Task ConsumerBuilder_AutoTuned_GetsBudgetShare()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(ConsumerCeiling);

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test")
            .Build();

        var limit = ((KafkaConsumer<string, string>)consumer).CurrentQueuedMaxBytes;
        await Assert.That(limit).IsEqualTo(ConsumerCeiling);

        DekafMemoryBudget.ResetForTesting();
    }

    [Test]
    public async Task ProducerAndConsumer_BothAutoTuned_SplitSeventyFiveTwentyFive()
    {
        DekafMemoryBudget.ResetForTesting();
        // Budget = 256 MiB → producer gets 192 MiB (below 256 MiB ceiling),
        // consumer gets 64 MiB (exactly at ceiling — no clamp).
        DekafMemoryBudget.SetBudget(256UL * 1024 * 1024);

        await using var p = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092").Build();
        await using var c = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("test").Build();

        var pLimit = ((KafkaProducer<string, string>)p).RecordAccumulator.MaxBufferMemory;
        var cLimit = ((KafkaConsumer<string, string>)c).CurrentQueuedMaxBytes;

        await Assert.That(pLimit).IsEqualTo(192UL * 1024 * 1024);
        await Assert.That(cLimit).IsEqualTo(64UL * 1024 * 1024);

        DekafMemoryBudget.ResetForTesting();
    }

    [Test]
    public async Task DisposingConsumer_RebalancesRemainingConsumers()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(ConsumerCeiling);

        await using var c1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("t1").Build();
        var c2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("t2").Build();

        var splitLimit = ((KafkaConsumer<string, string>)c1).CurrentQueuedMaxBytes;
        await Assert.That(splitLimit).IsEqualTo(ConsumerCeiling / 2);

        await c2.DisposeAsync();

        var grownLimit = ((KafkaConsumer<string, string>)c1).CurrentQueuedMaxBytes;
        await Assert.That(grownLimit).IsEqualTo(ConsumerCeiling);

        DekafMemoryBudget.ResetForTesting();
    }
}
