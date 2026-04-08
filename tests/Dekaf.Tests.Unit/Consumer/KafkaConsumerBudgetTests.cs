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

    // Consumer limits have no overhead divisor and no ceiling — the full share is assigned.
    // Budget chosen to stay above the 16 MiB consumer floor with room for split assertions.
    private const ulong TestBudget = 256UL * 1024 * 1024;

    [Test]
    public async Task ConsumerBuilder_AutoTuned_GetsBudgetShare()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(TestBudget);

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test")
            .Build();

        var limit = ((KafkaConsumer<string, string>)consumer).CurrentQueuedMaxBytes;
        await Assert.That(limit).IsEqualTo(TestBudget);

        DekafMemoryBudget.ResetForTesting();
    }

    [Test]
    public async Task ProducerAndConsumer_BothAutoTuned_SplitSeventyFiveTwentyFive()
    {
        DekafMemoryBudget.ResetForTesting();
        // Budget = 384 MiB → producer share = 288 MiB / 6 divisor = 48 MiB BufferMemory.
        // Consumer share = 96 MiB (no divisor).
        DekafMemoryBudget.SetBudget(384UL * 1024 * 1024);

        await using var p = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092").Build();
        await using var c = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("test").Build();

        var pLimit = ((KafkaProducer<string, string>)p).RecordAccumulator.MaxBufferMemory;
        var cLimit = ((KafkaConsumer<string, string>)c).CurrentQueuedMaxBytes;

        await Assert.That(pLimit).IsEqualTo(48UL * 1024 * 1024);
        await Assert.That(cLimit).IsEqualTo(96UL * 1024 * 1024);

        DekafMemoryBudget.ResetForTesting();
    }

    [Test]
    public async Task DisposingConsumer_RebalancesRemainingConsumers()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(TestBudget);

        await using var c1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("t1").Build();
        var c2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092").WithGroupId("t2").Build();

        var splitLimit = ((KafkaConsumer<string, string>)c1).CurrentQueuedMaxBytes;
        await Assert.That(splitLimit).IsEqualTo(TestBudget / 2);

        await c2.DisposeAsync();

        var grownLimit = ((KafkaConsumer<string, string>)c1).CurrentQueuedMaxBytes;
        await Assert.That(grownLimit).IsEqualTo(TestBudget);

        DekafMemoryBudget.ResetForTesting();
    }
}
