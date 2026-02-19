using Dekaf.Resilience;

namespace Dekaf.Tests.Unit.Resilience;

public class BrokerCircuitBreakerRegistryTests
{
    [Test]
    public async Task Registry_GetOrCreate_ReturnsSameInstanceForSameBroker()
    {
        var registry = new BrokerCircuitBreakerRegistry(new CircuitBreakerOptions());

        var cb1 = registry.GetOrCreate(1);
        var cb2 = registry.GetOrCreate(1);

        await Assert.That(cb1).IsSameReferenceAs(cb2);
    }

    [Test]
    public async Task Registry_GetOrCreate_ReturnsDifferentInstancesForDifferentBrokers()
    {
        var registry = new BrokerCircuitBreakerRegistry(new CircuitBreakerOptions());

        var cb1 = registry.GetOrCreate(1);
        var cb2 = registry.GetOrCreate(2);

        await Assert.That(cb1).IsNotSameReferenceAs(cb2);
    }

    [Test]
    public async Task Registry_TryGet_ReturnsFalseForUnknownBroker()
    {
        var registry = new BrokerCircuitBreakerRegistry(new CircuitBreakerOptions());

        var found = registry.TryGet(99, out var cb);

        await Assert.That(found).IsFalse();
        await Assert.That(cb).IsNull();
    }

    [Test]
    public async Task Registry_TryGet_ReturnsTrueAfterGetOrCreate()
    {
        var registry = new BrokerCircuitBreakerRegistry(new CircuitBreakerOptions());

        registry.GetOrCreate(1);
        var found = registry.TryGet(1, out var cb);

        await Assert.That(found).IsTrue();
        await Assert.That(cb).IsNotNull();
    }

    [Test]
    public async Task Registry_GetAllStates_ReturnsAllBrokerStates()
    {
        var registry = new BrokerCircuitBreakerRegistry(new CircuitBreakerOptions
        {
            FailureThreshold = 1
        });

        var cb1 = registry.GetOrCreate(1);
        var cb2 = registry.GetOrCreate(2);

        cb1.RecordFailure(); // Trip broker 1 to open

        var states = registry.GetAllStates();

        await Assert.That(states).Count().IsEqualTo(2);
        await Assert.That(states[1]).IsEqualTo(CircuitBreakerState.Open);
        await Assert.That(states[2]).IsEqualTo(CircuitBreakerState.Closed);
    }

    [Test]
    public async Task Registry_ResetAll_ResetsAllCircuitBreakers()
    {
        var registry = new BrokerCircuitBreakerRegistry(new CircuitBreakerOptions
        {
            FailureThreshold = 1
        });

        var cb1 = registry.GetOrCreate(1);
        var cb2 = registry.GetOrCreate(2);

        cb1.RecordFailure();
        cb2.RecordFailure();

        await Assert.That(cb1.State).IsEqualTo(CircuitBreakerState.Open);
        await Assert.That(cb2.State).IsEqualTo(CircuitBreakerState.Open);

        registry.ResetAll();

        await Assert.That(cb1.State).IsEqualTo(CircuitBreakerState.Closed);
        await Assert.That(cb2.State).IsEqualTo(CircuitBreakerState.Closed);
    }

    [Test]
    public async Task Registry_UsesConfiguredOptions()
    {
        var registry = new BrokerCircuitBreakerRegistry(new CircuitBreakerOptions
        {
            FailureThreshold = 3
        });

        var cb = registry.GetOrCreate(1);

        // Two failures should not trip the circuit (threshold is 3)
        cb.RecordFailure();
        cb.RecordFailure();
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Closed);

        // Third failure trips it
        cb.RecordFailure();
        await Assert.That(cb.State).IsEqualTo(CircuitBreakerState.Open);
    }
}
