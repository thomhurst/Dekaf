using System.Text;
using Dekaf.Producer;
using Dekaf.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryPartitioningTests
{
    private const int PartitionCount = 7;

    [Test]
    public async Task ProduceAsync_KeyedRecord_UsesRealDefaultPartitionerMapping()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", PartitionCount);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        var defaultPartitioner = new DefaultPartitioner();
        var murmur2Partitioner = new Murmur2Partitioner();

        for (var i = 0; i < 200; i++)
        {
            var key = "customer-" + i;
            var keyBytes = Encoding.UTF8.GetBytes(key);

            var metadata = await producer.ProduceAsync("orders", key, "value");

            await Assert.That(metadata.Partition)
                .IsEqualTo(defaultPartitioner.Partition("orders", keyBytes, keyIsNull: false, PartitionCount));
            await Assert.That(metadata.Partition)
                .IsEqualTo(murmur2Partitioner.Partition("orders", keyBytes, keyIsNull: false, PartitionCount));
        }
    }

    [Test]
    public async Task ProduceAsync_NullAndEmptyKeysWithoutPartitioner_RotateAcrossPartitions()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", 3);
        await using var producer = new InMemoryProducer<string?, string>(cluster);

        var first = await producer.ProduceAsync("orders", null, "a");
        var second = await producer.ProduceAsync("orders", "", "b");
        var third = await producer.ProduceAsync("orders", null, "c");

        await Assert.That(first.Partition).IsEqualTo(0);
        await Assert.That(second.Partition).IsEqualTo(1);
        await Assert.That(third.Partition).IsEqualTo(2);
    }

    [Test]
    public async Task ProduceAsync_ConfiguredPartitioner_HashesEmptyKeysToOnePartition()
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions
        {
            Partitioner = new Murmur2RandomPartitioner()
        });
        cluster.CreateTopic("outbox", PartitionCount);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        var expected = new Murmur2Partitioner().Partition("outbox", [], keyIsNull: false, PartitionCount);

        for (var i = 0; i < 5; i++)
        {
            var metadata = await producer.ProduceAsync("outbox", "", "value-" + i);
            await Assert.That(metadata.Partition).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task ProduceAsync_ExplicitPartition_BypassesConfiguredPartitioner()
    {
        var partitioner = new FixedPartitioner(1);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { Partitioner = partitioner });
        cluster.CreateTopic("orders", 3);
        await using var producer = new InMemoryProducer<string, string>(cluster);

        var routed = await producer.ProduceAsync("orders", "key", "routed");
        var explicitPartition = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "orders",
            Key = "key",
            Value = "explicit",
            Partition = 2
        });

        await Assert.That(routed.Partition).IsEqualTo(1);
        await Assert.That(explicitPartition.Partition).IsEqualTo(2);
        await Assert.That(partitioner.Calls).IsEqualTo(1);
    }

    [Test]
    public async Task ProduceAsync_PartitionerReturnsOutOfRange_Throws()
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions
        {
            Partitioner = new FixedPartitioner(3)
        });
        cluster.CreateTopic("orders", 3);
        await using var producer = new InMemoryProducer<string, string>(cluster);

        await Assert.That(async () => await producer.ProduceAsync("orders", "key", "value"))
            .Throws<InvalidOperationException>();
        for (var partition = 0; partition < 3; partition++)
            await Assert.That(cluster.ReadRecords("orders", partition)).IsEmpty();
    }

    [Test]
    public async Task AddDekafInMemory_ConfiguredPartitioner_ReachesResolvedProducer()
    {
        var services = new ServiceCollection();
        services.AddDekafInMemory(options =>
        {
            options.DefaultPartitionCount = 3;
            options.Partitioner = new FixedPartitioner(2);
        });
        await using var provider = services.BuildServiceProvider();
        var producer = provider.GetRequiredService<IKafkaProducer<string, string>>();

        var metadata = await producer.ProduceAsync("orders", "key", "value");

        await Assert.That(metadata.Partition).IsEqualTo(2);
    }

    [Test]
    [Arguments(false, false, null)]
    [Arguments(false, false, "")]
    [Arguments(false, true, "key")]
    [Arguments(true, false, null)]
    [Arguments(true, false, "")]
    [Arguments(true, true, "key")]
    public async Task ProduceAsync_ConfiguredStickyPartitioner_RejectsStickySelection(
        bool useDefaultPartitioner, bool ignoreKeys, string? key)
    {
        IPartitioner partitioner = useDefaultPartitioner
            ? new DefaultPartitioner(ignoreKeys: ignoreKeys)
            : new StickyPartitioner(ignoreKeys: ignoreKeys);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { Partitioner = partitioner });
        cluster.CreateTopic("orders", 3);
        await using var producer = new InMemoryProducer<string?, string>(cluster);

        await Assert.That(async () => await producer.ProduceAsync("orders", key, "value"))
            .Throws<NotSupportedException>();
        for (var partition = 0; partition < 3; partition++)
            await Assert.That(cluster.ReadRecords("orders", partition)).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ProduceAsync_ConfiguredStickyPartitioner_AllowsKeyHashingAndExplicitPartition(
        bool useDefaultPartitioner)
    {
        IPartitioner partitioner = useDefaultPartitioner ? new DefaultPartitioner() : new StickyPartitioner();
        var options = new InMemoryKafkaClusterOptions { Partitioner = partitioner };
        var cluster = new InMemoryKafkaCluster(options);
        cluster.CreateTopic("orders", PartitionCount);
        await using var producer = new InMemoryProducer<string, string>(cluster);

        var keyed = await producer.ProduceAsync("orders", "key", "value");
        await Assert.That(keyed.Partition).IsEqualTo(
            new Murmur2Partitioner().Partition("orders", Encoding.UTF8.GetBytes("key"), false, PartitionCount));

        options.Partitioner = useDefaultPartitioner
            ? new DefaultPartitioner(ignoreKeys: true)
            : new StickyPartitioner(ignoreKeys: true);
        var explicitPartition = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "orders",
            Key = "key",
            Value = "explicit",
            Partition = 2
        });
        await Assert.That(explicitPartition.Partition).IsEqualTo(2);
    }

    private sealed class FixedPartitioner(int partition) : IPartitioner
    {
        public int Calls { get; private set; }

        public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
        {
            Calls++;
            return partition;
        }
    }
}
