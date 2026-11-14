using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for the commit observer on the consumer's automatic commits.
/// </summary>
[Category("Offsets")]
public class ConsumerCommitObserverTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task CommitObserver_AutoCommit_ObservesPeriodicCommit()
    {
        var topic = await ProduceAsync(3).ConfigureAwait(false);
        var observer = new RecordingObserver();

        await using var consumer = await CreateConsumer(observer, TimeSpan.FromMilliseconds(100))
            .BuildAsync().ConfigureAwait(false);
        consumer.Subscribe(topic);
        await ConsumeAsync(consumer, 3).ConfigureAwait(false);

        // Offsets 0-1 are proven by the time the third record is requested.
        var observed = await observer.WaitForAsync(
                o => o.Reason == OffsetCommitReason.Periodic
                     && o.Offsets.Any(offset => offset.Topic == topic && offset.Offset >= 2))
            .ConfigureAwait(false);

        await Assert.That(observed.Exception).IsNull();
    }

    [Test]
    public async Task CommitObserver_Close_ObservesStopCommit()
    {
        var topic = await ProduceAsync(3).ConfigureAwait(false);
        var observer = new RecordingObserver();

        await using var consumer = await CreateConsumer(observer, TimeSpan.FromMinutes(5))
            .BuildAsync().ConfigureAwait(false);
        consumer.Subscribe(topic);
        await ConsumeAsync(consumer, 3).ConfigureAwait(false);

        await consumer.CloseAsync().ConfigureAwait(false);

        var observed = await observer.WaitForAsync(o => o.Reason == OffsetCommitReason.Stop).ConfigureAwait(false);
        await Assert.That(observed.Exception).IsNull();
        await Assert.That(observed.Offsets).Contains(offset => offset.Topic == topic && offset.Offset == 2);
        await Assert.That(observer.Completed.Any(o => o.Reason == OffsetCommitReason.Periodic)).IsFalse();
    }

    [Test]
    public async Task CommitObserver_WithoutAutoOffsetStore_StopCommitReportsStoredRecords()
    {
        var topic = await ProduceAsync(3).ConfigureAwait(false);
        var observer = new RecordingObserver();

        await using var consumer = await CreateConsumer(observer, TimeSpan.FromMinutes(5))
            .WithAutoOffsetStore(false)
            .BuildAsync().ConfigureAwait(false);
        consumer.Subscribe(topic);

        await ConsumeAndStoreAsync(consumer, 3).ConfigureAwait(false);

        await consumer.CloseAsync().ConfigureAwait(false);

        var observed = await observer.WaitForAsync(o => o.Reason == OffsetCommitReason.Stop).ConfigureAwait(false);
        await Assert.That(observed.Exception).IsNull();
        await Assert.That(observed.Offsets).Contains(offset => offset.Topic == topic && offset.Offset == 3);
        await Assert.That(observed.CompletedRecordCount).IsEqualTo(3L);
    }

    [Test]
    public async Task CommitObserver_WithoutAutoOffsetStore_UnsubscribeRevokeCommitReportsStoredRecords()
    {
        var topic = await ProduceAsync(3).ConfigureAwait(false);
        var observer = new RecordingObserver();

        await using var consumer = await CreateConsumer(observer, TimeSpan.FromMinutes(5))
            .WithAutoOffsetStore(false)
            .BuildAsync().ConfigureAwait(false);
        consumer.Subscribe(topic);
        await ConsumeAndStoreAsync(consumer, 3).ConfigureAwait(false);

        // The leave commits the offsets the departing partitions held when they were cleared.
        consumer.Unsubscribe();

        var observed = await observer.WaitForAsync(o => o.Reason == OffsetCommitReason.Revoke).ConfigureAwait(false);
        await Assert.That(observed.Exception).IsNull();
        await Assert.That(observed.Offsets).Contains(offset => offset.Topic == topic && offset.Offset == 3);
        await Assert.That(observed.CompletedRecordCount).IsEqualTo(3L);
    }

    private static async Task ConsumeAndStoreAsync(IKafkaConsumer<string, string> consumer, int count)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = 0;
        await foreach (var result in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            consumer.StoreOffset(result);
            if (++consumed >= count)
                break;
        }
    }

    private async Task<string> ProduceAsync(int count)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);

        for (var i = 0; i < count; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, CancellationToken.None).ConfigureAwait(false);
        }

        return topic;
    }

    private ConsumerBuilder<string, string> CreateConsumer(RecordingObserver observer, TimeSpan autoCommitInterval) =>
        Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithAutoCommitInterval(autoCommitInterval)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithCommitObserver(observer)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());

    private static async Task ConsumeAsync(IKafkaConsumer<string, string> consumer, int count)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = 0;
        await foreach (var _ in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            if (++consumed >= count)
                break;
        }
    }

    private sealed record ObservedCommit(
        OffsetCommitReason Reason,
        IReadOnlyList<TopicPartitionOffset> Offsets,
        Exception? Exception,
        long CompletedRecordCount);

    private sealed class RecordingObserver : IOffsetCommitObserver
    {
        private readonly ConcurrentQueue<ObservedCommit> _completed = new();
        private readonly SemaphoreSlim _signal = new(0);

        public IEnumerable<ObservedCommit> Completed => _completed;

        public object? OnCommitStarting(OffsetCommitObservation observation) => observation.Reason;

        public void OnCommitCompleted(OffsetCommitObservation observation, object? state, Exception? exception)
        {
            if (!Equals(state, observation.Reason))
                throw new InvalidOperationException("State from OnCommitStarting did not reach OnCommitCompleted.");

            _completed.Enqueue(new ObservedCommit(
                observation.Reason, observation.Offsets, exception, observation.CompletedRecordCount));
            _signal.Release();
        }

        public async Task<ObservedCommit> WaitForAsync(Func<ObservedCommit, bool> match)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (true)
            {
                if (_completed.FirstOrDefault(match) is { } observed)
                    return observed;
                await _signal.WaitAsync(cts.Token).ConfigureAwait(false);
            }
        }
    }
}
