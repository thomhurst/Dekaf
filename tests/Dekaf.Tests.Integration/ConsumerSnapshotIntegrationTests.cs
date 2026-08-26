using System.Text;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
public sealed class ConsumerSnapshotIntegrationTests(KafkaTestContainer kafka)
    : TransactionalKafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ConsumeSnapshotAsync_EmptyPartitionCompletesWithoutWaiting()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await using var consumer = await CreateConsumerAsync(queuedMinMessages: 10).ConfigureAwait(false);
        consumer.Partitions.Assign(new TopicPartition(topic, 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var records = await CollectAsync(consumer.ConsumeSnapshotAsync(timeout.Token)).ConfigureAwait(false);

        await Assert.That(records).IsEmpty();
    }

    [Test]
    public async Task SeekToTailAndSnapshot_MultiplePartitionsReturnFinalOffsetsWithPrefetch()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 5).ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 1, count: 5).ConfigureAwait(false);
        await using var consumer = await CreateConsumerAsync(queuedMinMessages: 10).ConfigureAwait(false);
        var partition0 = new TopicPartition(topic, 0);
        var partition1 = new TopicPartition(topic, 1);
        consumer.Partitions.Assign(partition0, partition1);
        await consumer.SeekToTailAsync(partition0, 2).ConfigureAwait(false);
        await consumer.SeekToTailAsync(partition1, 2).ConfigureAwait(false);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var records = await CollectAsync(consumer.ConsumeSnapshotAsync(timeout.Token)).ConfigureAwait(false);

        await Assert.That(records).Count().IsEqualTo(4);
        await Assert.That(records.Where(record => record.Partition == 0).Select(record => record.Offset))
            .IsEquivalentTo([3L, 4L]);
        await Assert.That(records.Where(record => record.Partition == 1).Select(record => record.Offset))
            .IsEquivalentTo([3L, 4L]);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_RecordAppendedAfterCaptureIsLeftForNormalConsumption()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 2).ConfigureAwait(false);
        await using var consumer = await CreateConsumerAsync(queuedMinMessages: 10).ConfigureAwait(false);
        consumer.Partitions.Assign(new TopicPartition(topic, 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var snapshot = consumer.ConsumeSnapshotAsync(timeout.Token).GetAsyncEnumerator();

        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        var first = snapshot.Current;
        await ProduceRangeAsync(topic, partition: 0, start: 2, count: 1).ConfigureAwait(false);
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        var second = snapshot.Current;
        await Assert.That(await snapshot.MoveNextAsync()).IsFalse();
        var appended = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), timeout.Token)
            .ConfigureAwait(false);

        await Assert.That(new[] { first.Offset, second.Offset }).IsEquivalentTo([0L, 1L]);
        await Assert.That(appended).IsNotNull();
        await Assert.That(appended!.Value.Offset).IsEqualTo(2L);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_PrefetchedCursorAheadUsesDeliveredPosition()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 5).ConfigureAwait(false);
        await using var consumer = await CreateConsumerAsync(queuedMinMessages: 10).ConfigureAwait(false);
        consumer.Partitions.Assign(new TopicPartition(topic, 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var first = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), timeout.Token)
            .ConfigureAwait(false);
        var remaining = await CollectAsync(consumer.ConsumeSnapshotAsync(timeout.Token)).ConfigureAwait(false);

        await Assert.That(first).IsNotNull();
        await Assert.That(first!.Value.Offset).IsEqualTo(0L);
        await Assert.That(remaining.Select(static record => record.Offset))
            .IsEquivalentTo([1L, 2L, 3L, 4L]);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_EarlyDisposeDoesNotLeakSnapshotEofToNormalConsume()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 5).ConfigureAwait(false);
        await using var consumer = await CreateConsumerAsync(queuedMinMessages: 10).ConfigureAwait(false);
        consumer.Partitions.Assign(new TopicPartition(topic, 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var snapshot = consumer.ConsumeSnapshotAsync(timeout.Token).GetAsyncEnumerator();

        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        await Assert.That(snapshot.Current.Offset).IsEqualTo(0L);
        await snapshot.DisposeAsync();
        await ProduceRangeAsync(topic, partition: 0, start: 5, count: 1).ConfigureAwait(false);

        var offsets = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), timeout.Token)
                .ConfigureAwait(false);
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Value.IsPartitionEof).IsFalse();
            offsets.Add(result.Value.Offset);
        }

        await Assert.That(offsets).IsEquivalentTo([1L, 2L, 3L, 4L, 5L]);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_SeekDuringEnumerationThrows()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 2).ConfigureAwait(false);
        await using var consumer = await CreateConsumerAsync(queuedMinMessages: 1).ConfigureAwait(false);
        var partition = new TopicPartition(topic, 0);
        consumer.Partitions.Assign(partition);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var snapshot = consumer.ConsumeSnapshotAsync(timeout.Token).GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();

        await Assert.That(() => consumer.Seek(new TopicPartitionOffset(topic, 0, 0)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => consumer.SeekToBeginning(partition))
            .Throws<InvalidOperationException>();
        await Assert.That(() => consumer.SeekToEnd(partition))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ConsumeSnapshotAsync_PauseDuringAsyncDeserializationDoesNotAdvance()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 1).ConfigureAwait(false);
        var deserializer = new BlockingAsyncDeserializer(blockOnCall: 1);
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1)
            .WithValueDeserializer(deserializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);
        var partition = new TopicPartition(topic, 0);
        consumer.Partitions.Assign(partition);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var snapshot = consumer.ConsumeSnapshotAsync(timeout.Token).GetAsyncEnumerator();
        var moveNext = snapshot.MoveNextAsync().AsTask();
        await deserializer.WaitUntilBlockedAsync(timeout.Token).ConfigureAwait(false);

        consumer.Partitions.Pause(partition);
        deserializer.Release();

        await Assert.That(async () => await moveNext.ConfigureAwait(false))
            .Throws<InvalidOperationException>();
        await Assert.That(consumer.Positions.GetPosition(partition)).IsEqualTo(0L);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_PauseDuringSynchronousDeserializationDoesNotAdvance()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 1).ConfigureAwait(false);
        var deserializer = new CallbackDeserializer();
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1)
            .WithValueDeserializer(deserializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);
        var partition = new TopicPartition(topic, 0);
        consumer.Partitions.Assign(partition);
        deserializer.SetCallback(() => consumer.Partitions.Pause(partition));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var snapshot = consumer.ConsumeSnapshotAsync(timeout.Token).GetAsyncEnumerator();

        await Assert.That(async () => await snapshot.MoveNextAsync().AsTask().ConfigureAwait(false))
            .Throws<InvalidOperationException>();
        await Assert.That(consumer.Positions.GetPosition(partition)).IsEqualTo(0L);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_StateChangeAfterFinalYieldThrows()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 1).ConfigureAwait(false);
        await using var consumer = await CreateConsumerAsync(queuedMinMessages: 1).ConfigureAwait(false);
        var partition = new TopicPartition(topic, 0);
        consumer.Partitions.Assign(partition);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var snapshot = consumer.ConsumeSnapshotAsync(timeout.Token).GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();

        consumer.Partitions.Pause(partition);

        await Assert.That(async () => await snapshot.MoveNextAsync().AsTask())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ConsumeSnapshotAsync_CanceledStartProvesPriorDelivery()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 2).ConfigureAwait(false);
        var groupId = $"snapshot-prior-delivery-{Guid.NewGuid():N}";
        var deserializer = new BlockingAsyncDeserializer(blockOnCall: 2);
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1)
            .WithValueDeserializer(deserializer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);
        var partition = new TopicPartition(topic, 0);
        consumer.Subscribe(topic);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using (var normal = consumer.ConsumeAsync(timeout.Token).GetAsyncEnumerator())
        {
            await Assert.That(await normal.MoveNextAsync()).IsTrue();
            await Assert.That(normal.Current.Offset).IsEqualTo(0L);
        }

        using var snapshotCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        await using var snapshot = consumer.ConsumeSnapshotAsync(snapshotCancellation.Token).GetAsyncEnumerator();
        var moveNext = snapshot.MoveNextAsync().AsTask();
        await deserializer.WaitUntilBlockedAsync(timeout.Token).ConfigureAwait(false);
        snapshotCancellation.Cancel();
        try
        {
            await Assert.That(async () => await moveNext.ConfigureAwait(false))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            deserializer.Release();
        }

        await consumer.CommitAsync(timeout.Token).ConfigureAwait(false);
        var committed = await consumer.GetCommittedOffsetAsync(partition, timeout.Token).ConfigureAwait(false);
        await Assert.That(committed).IsEqualTo(1L);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_ReadCommittedAbortedOnlyLogCompletesWithoutRecords()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"snapshot-abort-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);
        await producer.InitTransactionsAsync().ConfigureAwait(false);
        await using (var transaction = producer.BeginTransaction())
        {
            for (var i = 0; i < 3; i++)
            {
                await transaction.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = 0,
                    Key = $"aborted-{i}",
                    Value = $"aborted-{i}"
                }, CancellationToken.None).ConfigureAwait(false);
            }

            await transaction.AbortAsync().ConfigureAwait(false);
        }

        await using var consumer = await CreateConsumerAsync(
            queuedMinMessages: 1,
            isolationLevel: IsolationLevel.ReadCommitted).ConfigureAwait(false);
        var partition = new TopicPartition(topic, 0);
        consumer.Partitions.Assign(partition);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var capturedWatermarks = await WaitForConditionAsync(
            async () => await consumer.Offsets.QueryWatermarkOffsetsAsync(partition, timeout.Token)
                .ConfigureAwait(false),
            static watermarks => watermarks.High >= 4,
            description: "aborted transaction watermark visibility").ConfigureAwait(false);

        var records = await CollectAsync(consumer.ConsumeSnapshotAsync(timeout.Token)).ConfigureAwait(false);

        await Assert.That(records).IsEmpty();
        await Assert.That(consumer.Positions.GetPosition(partition)).IsEqualTo(capturedWatermarks.High);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_RetentionGapResetsAndCompletesAtCapturedEnd()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 10, valueSize: 8 * 1024).ConfigureAwait(false);
        var partition = new TopicPartition(topic, 0);
        await using var consumer = await CreateConsumerAsync(
            queuedMinMessages: 1,
            maxPartitionFetchBytes: 9 * 1024).ConfigureAwait(false);
        consumer.Partitions.Assign(partition);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var snapshot = consumer.ConsumeSnapshotAsync(timeout.Token).GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        var offsets = new List<long> { snapshot.Current.Offset };

        await using (var admin = new AdminClientBuilder()
                         .WithBootstrapServers(KafkaContainer.BootstrapServers)
                         .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                         .Build())
        {
            var deleted = await admin.DeleteRecordsAsync(new Dictionary<TopicPartition, long>
            {
                [partition] = 5
            }).ConfigureAwait(false);
            await Assert.That(deleted[partition]).IsGreaterThanOrEqualTo(5L);
        }

        while (await snapshot.MoveNextAsync())
            offsets.Add(snapshot.Current.Offset);

        await Assert.That(offsets).IsEquivalentTo([0L, 5L, 6L, 7L, 8L, 9L]);
        await Assert.That(consumer.Positions.GetPosition(partition)).IsEqualTo(10L);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_GroupRebalanceInvalidatesCapturedAssignment()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 3).ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 1, count: 3).ConfigureAwait(false);
        var groupId = $"snapshot-rebalance-{Guid.NewGuid():N}";
        var rebalanceListener = new RevocationListener();
        await using var firstConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1)
            .WithRebalanceListener(rebalanceListener)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);
        firstConsumer.Subscribe(topic);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var snapshot = firstConsumer.ConsumeSnapshotAsync(timeout.Token).GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();

        await using var secondConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);
        secondConsumer.Subscribe(topic);
        var secondResult = await secondConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token)
            .ConfigureAwait(false);
        await Assert.That(secondResult).IsNotNull();
        await rebalanceListener.WaitForRevocationAsync(timeout.Token).ConfigureAwait(false);

        await Assert.That(async () => await snapshot.MoveNextAsync().AsTask())
            .Throws<InvalidOperationException>();
    }

    private async ValueTask<IKafkaConsumer<string, string>> CreateConsumerAsync(
        int queuedMinMessages,
        IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted,
        int? maxPartitionFetchBytes = null)
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(queuedMinMessages)
            .WithIsolationLevel(isolationLevel)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());
        if (maxPartitionFetchBytes.HasValue)
            builder.WithMaxPartitionFetchBytes(maxPartitionFetchBytes.Value);

        return await builder.BuildAsync().ConfigureAwait(false);
    }

    private async Task ProduceRangeAsync(
        string topic,
        int partition,
        int count,
        int start = 0,
        int valueSize = 0)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);

        for (var i = start; i < start + count; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = partition,
                Key = $"key-{i}",
                Value = valueSize > 0 ? new string((char)('a' + i % 26), valueSize) : $"value-{i}"
            }, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task<List<ConsumeResult<string, string>>> CollectAsync(
        IAsyncEnumerable<ConsumeResult<string, string>> source)
    {
        var records = new List<ConsumeResult<string, string>>();
        await foreach (var record in source.ConfigureAwait(false))
            records.Add(record);
        return records;
    }

    private sealed class RevocationListener : IRebalanceListener
    {
        private readonly TaskCompletionSource _revoked = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask OnPartitionsAssignedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnPartitionsRevokedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            _revoked.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public Task WaitForRevocationAsync(CancellationToken cancellationToken) =>
            _revoked.Task.WaitAsync(cancellationToken);
    }

    private sealed class BlockingAsyncDeserializer(int blockOnCall) : IAsyncDeserializer<string>
    {
        private readonly TaskCompletionSource _blocked = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public async ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _callCount) == blockOnCall)
            {
                _blocked.TrySetResult();
                await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return Encoding.UTF8.GetString(data.Span);
        }

        public Task WaitUntilBlockedAsync(CancellationToken cancellationToken) =>
            _blocked.Task.WaitAsync(cancellationToken);

        public void Release() => _release.TrySetResult();
    }

    private sealed class CallbackDeserializer : IDeserializer<string>
    {
        private Action? _callback;

        internal void SetCallback(Action callback) => _callback = callback;

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Interlocked.Exchange(ref _callback, null)?.Invoke();
            return Encoding.UTF8.GetString(data.Span);
        }
    }
}
