using Dekaf.Errors;
using Dekaf.Resilience;

namespace Dekaf.Tests.Unit.Resilience;

public class CircuitBreakerOpenExceptionTests
{
    [Test]
    public async Task CircuitBreakerOpenException_InheritsFromKafkaException()
    {
        var ex = new CircuitBreakerOpenException();
        await Assert.That(ex).IsAssignableTo<KafkaException>();
    }

    [Test]
    public async Task CircuitBreakerOpenException_DefaultMessage_ContainsCircuitBreaker()
    {
        var ex = new CircuitBreakerOpenException();
        await Assert.That(ex.Message).Contains("Circuit breaker");
    }

    [Test]
    public async Task CircuitBreakerOpenException_WithBrokerId_ContainsBrokerId()
    {
        var ex = new CircuitBreakerOpenException(42);
        await Assert.That(ex.Message).Contains("42");
        await Assert.That(ex.BrokerId).IsEqualTo(42);
    }

    [Test]
    public async Task CircuitBreakerOpenException_WithCustomMessage_UsesMessage()
    {
        var ex = new CircuitBreakerOpenException("custom message");
        await Assert.That(ex.Message).IsEqualTo("custom message");
    }

    [Test]
    public async Task CircuitBreakerOpenException_WithInnerException_PreservesInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new CircuitBreakerOpenException("outer", inner);
        await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
    }
}
