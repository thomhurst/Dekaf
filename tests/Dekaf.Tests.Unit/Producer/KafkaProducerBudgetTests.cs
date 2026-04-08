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

    // BufferMemory is share / ProducerOverheadDivisor. Budget chosen so per-instance math
    // divides cleanly by 6 and by 2, and the result stays above the 32 MiB floor.
    private const ulong TestBudget = 1536UL * 1024 * 1024; // 1.5 GiB
    private const int ProducerDivisor = DekafMemoryBudget.ProducerOverheadDivisor;

    [Test]
    public async Task ProducerBuilder_WithoutExplicitBufferMemory_UsesBudget()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(TestBudget);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        var maxBuffer = ((KafkaProducer<string, string>)producer).RecordAccumulator.MaxBufferMemory;
        await Assert.That(maxBuffer).IsEqualTo(TestBudget / ProducerDivisor);

        DekafMemoryBudget.ResetForTesting();
    }

    [Test]
    public async Task ProducerBuilder_WithExplicitBufferMemory_DoesNotUseBudget()
    {
        DekafMemoryBudget.ResetForTesting();
        DekafMemoryBudget.SetBudget(TestBudget);

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
        DekafMemoryBudget.SetBudget(TestBudget);

        await using var p1 = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092").Build();
        var p1Limit1 = ((KafkaProducer<string, string>)p1).RecordAccumulator.MaxBufferMemory;
        await Assert.That(p1Limit1).IsEqualTo(TestBudget / ProducerDivisor);

        await using var p2 = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092").Build();

        var p1Limit2 = ((KafkaProducer<string, string>)p1).RecordAccumulator.MaxBufferMemory;
        var p2Limit = ((KafkaProducer<string, string>)p2).RecordAccumulator.MaxBufferMemory;
        await Assert.That(p1Limit2).IsEqualTo(TestBudget / 2 / ProducerDivisor);
        await Assert.That(p2Limit).IsEqualTo(TestBudget / 2 / ProducerDivisor);

        DekafMemoryBudget.ResetForTesting();
    }
}
