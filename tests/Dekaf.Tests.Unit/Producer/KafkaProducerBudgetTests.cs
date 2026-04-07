namespace Dekaf.Tests.Unit.Producer;

using Dekaf.Internal;
using Dekaf.Producer;

[NotInParallel("DekafMemoryBudget")]
public class KafkaProducerBudgetTests
{
    [Test]
    public async Task KafkaProducer_ImplementsIBudgetedInstance()
    {
        var type = typeof(KafkaProducer<,>).MakeGenericType(typeof(string), typeof(string));
        var implements = typeof(IBudgetedInstance).IsAssignableFrom(type);
        await Assert.That(implements).IsTrue();
    }

    [Test]
    public async Task ProducerBuilder_WithoutExplicitBufferMemory_UsesBudget()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(1_000_000_000UL);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        var maxBuffer = ((KafkaProducer<string, string>)producer).RecordAccumulator.MaxBufferMemory;
        await Assert.That(maxBuffer).IsEqualTo(1_000_000_000UL);

        DekafMemoryBudget.ResetForTesting();
    }

    [Test]
    public async Task ProducerBuilder_WithExplicitBufferMemory_DoesNotUseBudget()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(1_000_000_000UL);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithBufferMemory(64UL * 1024 * 1024)
            .Build();

        var maxBuffer = ((KafkaProducer<string, string>)producer).RecordAccumulator.MaxBufferMemory;
        await Assert.That(maxBuffer).IsEqualTo(64UL * 1024 * 1024);

        DekafMemoryBudget.ResetForTesting();
    }

    [Test]
    public async Task TwoProducers_Built_RebalanceToHalfEach()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(1_000_000_000UL);

        await using var p1 = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092").Build();
        var p1Limit1 = ((KafkaProducer<string, string>)p1).RecordAccumulator.MaxBufferMemory;
        await Assert.That(p1Limit1).IsEqualTo(1_000_000_000UL);

        await using var p2 = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092").Build();

        var p1Limit2 = ((KafkaProducer<string, string>)p1).RecordAccumulator.MaxBufferMemory;
        var p2Limit = ((KafkaProducer<string, string>)p2).RecordAccumulator.MaxBufferMemory;
        await Assert.That(p1Limit2).IsEqualTo(500_000_000UL);
        await Assert.That(p2Limit).IsEqualTo(500_000_000UL);

        DekafMemoryBudget.ResetForTesting();
    }
}
