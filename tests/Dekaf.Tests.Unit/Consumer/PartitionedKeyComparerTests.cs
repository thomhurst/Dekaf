using Dekaf.Consumer;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PartitionedKeyComparerTests
{
    [Test]
    public async Task CustomComparer_NullKeysNeverReachComparerOrMatchNonNull()
    {
        var comparer = new CustomPartitionMessageKeyComparer<CustomerKey>(new CustomerKeyComparer());
        var nullKey = PartitionMessageKey<CustomerKey>.From(null);
        var valueKey = PartitionMessageKey<CustomerKey>.From(new CustomerKey(1));
        await Assert.That(comparer.Equals(nullKey, nullKey)).IsTrue();
        await Assert.That(comparer.Equals(nullKey, valueKey)).IsFalse();
        await Assert.That(comparer.GetHashCode(nullKey)).IsEqualTo(0);
    }

    [Test]
    public async Task CustomComparer_DictionaryUsesEqualityWhenHashesCollide()
    {
        var comparer = new CustomPartitionMessageKeyComparer<CustomerKey>(new CustomerKeyComparer());
        var keys = new Dictionary<PartitionMessageKey<CustomerKey>, string>(comparer)
        {
            [PartitionMessageKey<CustomerKey>.From(new CustomerKey(1))] = "one",
            [PartitionMessageKey<CustomerKey>.From(new CustomerKey(2))] = "two",
            [PartitionMessageKey<CustomerKey>.From(null)] = "null"
        };
        await Assert.That(keys[PartitionMessageKey<CustomerKey>.From(new CustomerKey(1))]).IsEqualTo("one");
        await Assert.That(keys[PartitionMessageKey<CustomerKey>.From(new CustomerKey(2))]).IsEqualTo("two");
        await Assert.That(keys[PartitionMessageKey<CustomerKey>.From(null)]).IsEqualTo("null");
        await Assert.That(keys.Count).IsEqualTo(3);
    }

    [Test]
    public async Task CustomComparer_RejectsNullAtBothPublicOverloads()
    {
        await using var consumer = new InMemoryConsumer<string, string>(new InMemoryKafkaCluster());
        Assert.Throws<ArgumentNullException>(() => consumer.RunPartitionedAsync(
            static (_, _, _) => ValueTask.CompletedTask, null, null!, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => consumer.RunPartitionedBatchesAsync(
            static (_, _, _) => ValueTask.CompletedTask, null, null!, CancellationToken.None));
    }

    private sealed class CustomerKey(byte id)
    {
        public byte Id { get; } = id;
    }

    private sealed class CustomerKeyComparer : IEqualityComparer<CustomerKey>
    {
        public bool Equals(CustomerKey? x, CustomerKey? y) => x!.Id == y!.Id;
        public int GetHashCode(CustomerKey obj) => 0;
    }
}
