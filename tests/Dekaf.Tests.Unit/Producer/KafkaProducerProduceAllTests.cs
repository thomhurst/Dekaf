using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for <c>KafkaProducer.ProduceAllAsync</c>'s synchronous-throw handling: a produce call
/// that throws before registering (here: producer never initialized) must be recorded via the
/// completion's abort path and surface from the aggregate await instead of hanging or leaking.
/// </summary>
public class KafkaProducerProduceAllTests
{
    [Test]
    public async Task ProduceAllAsync_KeyValueOverload_SyncThrow_FaultsAggregateWithoutHanging()
    {
        // Build() without InitializeAsync: the first ProduceAsync throws synchronously at entry,
        // driving ProduceAllAsync's catch block (RecordFailure + AbortRegistration at index 0).
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        try
        {
            var messages = new (string? Key, string Value)[] { ("k0", "v0"), ("k1", "v1"), ("k2", "v2") };

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await producer.ProduceAllAsync("test-topic", messages)
                    .WaitAsync(TimeSpan.FromSeconds(10)));

            await Assert.That(thrown!.Message).Contains("InitializeAsync");
        }
        finally
        {
            await producer.DisposeAsync();
        }
    }

    [Test]
    public async Task ProduceAllAsync_MessageListOverload_SyncThrow_FaultsAggregateWithoutHanging()
    {
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        try
        {
            var messages = Enumerable.Range(0, 3).Select(i => new ProducerMessage<string, string>
            {
                Topic = "test-topic",
                Key = $"k{i}",
                Value = $"v{i}"
            }).ToList();

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await producer.ProduceAllAsync(messages)
                    .WaitAsync(TimeSpan.FromSeconds(10)));

            await Assert.That(thrown!.Message).Contains("InitializeAsync");
        }
        finally
        {
            await producer.DisposeAsync();
        }
    }

    [Test]
    public async Task TopicProducer_ProduceAllAsync_NonListSyncThrow_FaultsAggregateWithoutHanging()
    {
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        try
        {
            var topicProducer = producer.ForTopic("test-topic");

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await topicProducer.ProduceAllAsync(EnumerateMessages())
                    .WaitAsync(TimeSpan.FromSeconds(10)));

            await Assert.That(thrown!.Message).Contains("InitializeAsync");
        }
        finally
        {
            await producer.DisposeAsync();
        }
    }

    [Test]
    public async Task TopicProducer_ProduceAllAsync_EmptyInputs_ReturnsEmptyResults()
    {
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        try
        {
            var topicProducer = producer.ForTopic("test-topic");

            var listResult = await topicProducer.ProduceAllAsync(
                Array.Empty<TopicProducerMessage<string, string>>());
            var enumerableResult = await topicProducer.ProduceAllAsync(EnumerateNoMessages());

            await Assert.That(listResult).IsEmpty();
            await Assert.That(enumerableResult).IsEmpty();
        }
        finally
        {
            await producer.DisposeAsync();
        }
    }

    [Test]
    public async Task TopicProducer_ProduceAllAsync_MutatingListCount_DoesNotHang()
    {
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        try
        {
            var topicProducer = producer.ForTopic("test-topic");
            var messages = new ChangingCountList();

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await topicProducer.ProduceAllAsync(messages)
                    .WaitAsync(TimeSpan.FromSeconds(10)));

            await Assert.That(thrown!.Message).Contains("InitializeAsync");
            await Assert.That(messages.CountReads).IsEqualTo(1);
        }
        finally
        {
            await producer.DisposeAsync();
        }
    }

    private static IEnumerable<TopicProducerMessage<string, string>> EnumerateMessages()
    {
        yield return new TopicProducerMessage<string, string> { Key = "k0", Value = "v0" };
        yield return new TopicProducerMessage<string, string> { Key = "k1", Value = "v1" };
        yield return new TopicProducerMessage<string, string> { Key = "k2", Value = "v2" };
    }

    private static IEnumerable<TopicProducerMessage<string, string>> EnumerateNoMessages()
    {
        yield break;
    }

    private sealed class ChangingCountList : IList<TopicProducerMessage<string, string>>
    {
        private int _countReads;

        public int CountReads => _countReads;

        public int Count => Interlocked.Increment(ref _countReads) <= 2 ? 2 : 0;

        public bool IsReadOnly => true;

        public TopicProducerMessage<string, string> this[int index]
        {
            get => new() { Key = $"k{index}", Value = $"v{index}" };
            set => throw new NotSupportedException();
        }

        public IEnumerator<TopicProducerMessage<string, string>> GetEnumerator() =>
            throw new NotSupportedException();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public int IndexOf(TopicProducerMessage<string, string> item) => throw new NotSupportedException();

        public void Insert(int index, TopicProducerMessage<string, string> item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        public void Add(TopicProducerMessage<string, string> item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(TopicProducerMessage<string, string> item) => throw new NotSupportedException();

        public void CopyTo(TopicProducerMessage<string, string>[] array, int arrayIndex) =>
            throw new NotSupportedException();

        public bool Remove(TopicProducerMessage<string, string> item) => throw new NotSupportedException();
    }
}
