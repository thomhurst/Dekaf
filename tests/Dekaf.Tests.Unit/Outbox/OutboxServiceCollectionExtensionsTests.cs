using System.Reflection;
using Dekaf.Outbox;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Outbox;

public class OutboxServiceCollectionExtensionsTests
{
    [Test]
    public async Task RelayProducerBuilder_CallerCannotDowngradeDeliveryGuarantees()
    {
        // A delegate that tries to disable every guarantee the outbox depends on: durable
        // acks, per-partition sequencing, and key-respecting partitioning.
        var builder = OutboxServiceCollectionExtensions.CreateRelayProducerBuilder(
            producer => producer
                .WithBootstrapServers("localhost:1")
                .WithAcks(Acks.Leader)
                .WithIdempotence(false)
                .WithPartitioner(PartitionerType.RoundRobin)
                .WithPartitionerIgnoreKeys()
                .WithCustomPartitioner(new RoundRobinPartitioner()),
            loggerFactory: null);

        // ProducerBuilder exposes no options getter, so pin the invariant via the private
        // fields. If these fields are renamed, update this test - it guards against the
        // mandatory enforcement calls being reordered before the delegate.
        await Assert.That(ReadField<Acks>(builder, "_acks")).IsEqualTo(Acks.All);
        await Assert.That(ReadField<bool>(builder, "_enableIdempotence")).IsTrue();
        // The custom partitioner takes precedence over WithPartitioner/WithPartitionerIgnoreKeys,
        // so pinning it pins the effective partitioning behavior.
        await Assert.That(ReadField<IPartitioner?>(builder, "_customPartitioner"))
            .IsTypeOf<Murmur2RandomPartitioner>();
    }

    private static T ReadField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"Field '{fieldName}' not found on {instance.GetType().Name}; update this test to match the builder.");
        return (T)field.GetValue(instance)!;
    }
}
