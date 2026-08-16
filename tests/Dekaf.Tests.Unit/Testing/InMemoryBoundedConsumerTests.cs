using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryBoundedConsumerTests
{
    [Test]
    public async Task SeekToTailAsync_UsesOffsetCountAndClampsToLowWatermark()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceRangeAsync(producer, "events", partition: 0, count: 5);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        var partition = new TopicPartition("events", 0);
        consumer.Assign(partition);

        var tail = await consumer.SeekToTailAsync(partition, offsetCount: 2);
        var values = await CollectValuesAsync(consumer.ConsumeSnapshotAsync());
        var clamped = await consumer.SeekToTailAsync(partition, offsetCount: int.MaxValue);

        await Assert.That(tail.Offset).IsEqualTo(3L);
        await Assert.That(values).IsEquivalentTo(["value-3", "value-4"]);
        await Assert.That(clamped.Offset).IsEqualTo(0L);
    }

    [Test]
    public async Task SeekToTailAsync_ZeroSeeksToCapturedEnd()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceRangeAsync(producer, "events", partition: 0, count: 3);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        var partition = new TopicPartition("events", 0);
        consumer.Assign(partition);

        var tail = await consumer.SeekToTailAsync(partition, offsetCount: 0);
        var values = await CollectValuesAsync(consumer.ConsumeSnapshotAsync());

        await Assert.That(tail.Offset).IsEqualTo(3L);
        await Assert.That(values).IsEmpty();
    }

    [Test]
    public async Task ConsumeSnapshotAsync_ExcludesRecordsAppendedAfterCapture()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceRangeAsync(producer, "events", partition: 0, count: 2);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        consumer.Assign(new TopicPartition("events", 0));

        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        var first = snapshot.Current.Value;
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "events",
            Partition = 0,
            Key = "key-2",
            Value = "value-2"
        });
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        var second = snapshot.Current.Value;
        await Assert.That(await snapshot.MoveNextAsync()).IsFalse();

        var appended = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
        await Assert.That(new[] { first, second }).IsEquivalentTo(["value-0", "value-1"]);
        await Assert.That(appended!.Value.Value).IsEqualTo("value-2");
    }

    [Test]
    public async Task ConsumeSnapshotAsync_CapturesMultiplePartitionsAndCompletesEmptyOnes()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events", partitionCount: 3);
        var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceRangeAsync(producer, "events", partition: 0, count: 2);
        await ProduceRangeAsync(producer, "events", partition: 1, count: 2);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        consumer.Assign(
            new TopicPartition("events", 0),
            new TopicPartition("events", 1),
            new TopicPartition("events", 2));

        var values = await CollectValuesAsync(consumer.ConsumeSnapshotAsync());

        await Assert.That(values).Count().IsEqualTo(4);
        await Assert.That(values).Contains("value-0");
        await Assert.That(values).Contains("value-1");
    }

    [Test]
    public async Task ConsumeSnapshotAsync_GroupMembersCaptureOnlyOwnedPartitions()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events", partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceRangeAsync(producer, "events", partition: 0, count: 1);
        await ProduceRangeAsync(producer, "events", partition: 1, count: 1);
        await using var first = CreateGroupConsumer(cluster, "a");
        await using var second = CreateGroupConsumer(cluster, "b");
        first.Subscribe("events");
        second.Subscribe("events");

        var firstRecords = await CollectAsync(first.ConsumeSnapshotAsync());
        var secondRecords = await CollectAsync(second.ConsumeSnapshotAsync());

        await Assert.That(firstRecords).Count().IsEqualTo(1);
        await Assert.That(secondRecords).Count().IsEqualTo(1);
        await Assert.That(firstRecords[0].Partition).IsNotEqualTo(secondRecords[0].Partition);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_GroupOwnershipChangeThrows()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events", partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceRangeAsync(producer, "events", partition: 0, count: 2);
        await ProduceRangeAsync(producer, "events", partition: 1, count: 2);
        await using var first = CreateGroupConsumer(cluster, "a");
        first.Subscribe("events");
        await using var snapshot = first.ConsumeSnapshotAsync().GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();

        await using var second = CreateGroupConsumer(cluster, "b");
        second.Subscribe("events");

        await Assert.That(async () => await snapshot.MoveNextAsync().AsTask())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ConsumeSnapshotAsync_AssignmentChangeThrows()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events", partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceRangeAsync(producer, "events", partition: 0, count: 2);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        consumer.Assign(new TopicPartition("events", 0));
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();

        consumer.IncrementalAssign([new TopicPartitionOffset("events", 1, 0)]);

        await Assert.That(async () => await snapshot.MoveNextAsync().AsTask())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ConsumeSnapshotAsync_PauseAfterCaptureThrows()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceRangeAsync(producer, "events", partition: 0, count: 2);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        var partition = new TopicPartition("events", 0);
        consumer.Assign(partition);
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();

        consumer.Pause(partition);

        await Assert.That(async () => await snapshot.MoveNextAsync().AsTask())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ConsumeSnapshotAsync_PauseDuringAsyncDeserializationThrowsWithoutAdvancing()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceRangeAsync(producer, "events", partition: 0, count: 1);
        var deserializer = new BlockingAsyncDeserializer();
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            Serializers.String,
            deserializer,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        var partition = new TopicPartition("events", 0);
        consumer.Assign(partition);
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();
        var moveNext = snapshot.MoveNextAsync().AsTask();
        await deserializer.WaitUntilEnteredAsync();

        consumer.Pause(partition);
        deserializer.Release();

        await Assert.That(async () => await moveNext).Throws<InvalidOperationException>();
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(0L);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_SeekDuringEnumerationThrows()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceRangeAsync(producer, "events", partition: 0, count: 2);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        var partition = new TopicPartition("events", 0);
        consumer.Assign(partition);
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();

        await Assert.That(() => consumer.Seek(new TopicPartitionOffset("events", 0, 0)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => consumer.SeekToBeginning(partition))
            .Throws<InvalidOperationException>();
        await Assert.That(() => consumer.SeekToEnd(partition))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ConsumeSnapshotAsync_ConcurrentEnumerationThrows()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceRangeAsync(producer, "events", partition: 0, count: 2);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        consumer.Assign(new TopicPartition("events", 0));
        await using var first = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();
        await using var second = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();
        await Assert.That(await first.MoveNextAsync()).IsTrue();

        await Assert.That(async () => await second.MoveNextAsync().AsTask())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ConsumeSnapshotAsync_PositionBeyondBoundIsNotRewound()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceRangeAsync(producer, "events", partition: 0, count: 3);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        var partition = new TopicPartition("events", 0);
        consumer.Assign(partition);
        consumer.Seek(new TopicPartitionOffset("events", 0, 10));

        var values = await CollectValuesAsync(consumer.ConsumeSnapshotAsync());

        await Assert.That(values).IsEmpty();
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(10L);
    }

    [Test]
    public async Task BoundedExtensions_ReachBuiltInCapabilityThroughInterface()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        IKafkaConsumer<string, string> consumer = new InMemoryConsumer<string, string>(cluster);
        await using var disposable = consumer;
        var partition = new TopicPartition("events", 0);
        consumer.Partitions.Assign(partition);

        var tail = await consumer.SeekToTailAsync(partition, 0);
        var values = await CollectValuesAsync(consumer.ConsumeSnapshotAsync());

        await Assert.That(tail.Offset).IsEqualTo(0L);
        await Assert.That(values).IsEmpty();
    }

    private static async Task ProduceRangeAsync(
        InMemoryProducer<string, string> producer,
        string topic,
        int partition,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = partition,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }
    }

    private static InMemoryConsumer<string, string> CreateGroupConsumer(
        InMemoryKafkaCluster cluster,
        string memberId) =>
        new(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "snapshot-group",
                MemberId = memberId,
                AutoOffsetReset = AutoOffsetReset.Earliest
            });

    private static async Task<List<ConsumeResult<string, string>>> CollectAsync(
        IAsyncEnumerable<ConsumeResult<string, string>> records)
    {
        var results = new List<ConsumeResult<string, string>>();
        await foreach (var record in records)
            results.Add(record);
        return results;
    }

    private static async Task<List<string>> CollectValuesAsync(
        IAsyncEnumerable<ConsumeResult<string, string>> records)
    {
        var values = new List<string>();
        await foreach (var record in records)
            values.Add(record.Value);
        return values;
    }

    private sealed class BlockingAsyncDeserializer : IAsyncDeserializer<string>
    {
        private readonly TaskCompletionSource _entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return Encoding.UTF8.GetString(data.Span);
        }

        public Task WaitUntilEnteredAsync() => _entered.Task;

        public void Release() => _release.TrySetResult();
    }
}
