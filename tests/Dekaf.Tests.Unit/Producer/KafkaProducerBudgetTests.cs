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
}
