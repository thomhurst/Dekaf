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

    [Test]
    public async Task ConsumerBuilder_AutoTuned_GetsBudgetShare()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(1_000_000_000UL);

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test")
            .Build();

        var limit = ((KafkaConsumer<string, string>)consumer).CurrentQueuedMaxBytes;
        // Consumer-only → 100% of 1GB (rebalance sets exact byte value post-registration)
        await Assert.That(limit).IsEqualTo(1_000_000_000UL);

        DekafMemoryBudget.ResetForTesting();
    }

    [Test]
    public async Task ProducerAndConsumer_BothAutoTuned_SplitSeventyFiveTwentyFive()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(1_000_000_000UL);

        await using var p = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092").Build();
        await using var c = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("test").Build();

        var pLimit = ((KafkaProducer<string, string>)p).RecordAccumulator.MaxBufferMemory;
        var cLimit = ((KafkaConsumer<string, string>)c).CurrentQueuedMaxBytes;

        await Assert.That(pLimit).IsEqualTo(750_000_000UL);
        await Assert.That(cLimit).IsEqualTo(250_000_000UL);

        DekafMemoryBudget.ResetForTesting();
    }

    [Test]
    public async Task DisposingConsumer_RebalancesRemainingConsumers()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(1_000_000_000UL);

        await using var c1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("t1").Build();
        var c2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("t2").Build();

        var splitLimit = ((KafkaConsumer<string, string>)c1).CurrentQueuedMaxBytes;
        await Assert.That(splitLimit).IsEqualTo(500_000_000UL);

        await c2.DisposeAsync();

        var grownLimit = ((KafkaConsumer<string, string>)c1).CurrentQueuedMaxBytes;
        await Assert.That(grownLimit).IsEqualTo(1_000_000_000UL);

        DekafMemoryBudget.ResetForTesting();
    }
}
