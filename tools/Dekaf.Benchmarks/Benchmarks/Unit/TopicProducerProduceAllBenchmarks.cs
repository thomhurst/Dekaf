using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Telemetry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class TopicProducerProduceAllBenchmarks
{
    private ITopicProducer<string, string> _producer = null!;
    private TopicProducerMessage<string, string>[] _messages = null!;

    public enum MessageSource
    {
        Array,
        Enumerable,
    }

    [Params(1, 100)]
    public int MessageCount { get; set; }

    [ParamsAllValues]
    public MessageSource Source { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _producer = new BenchmarkProducer(MessageCount).ForTopic("benchmark-topic");
        _messages = Enumerable.Range(0, MessageCount)
            .Select(static i => new TopicProducerMessage<string, string>
            {
                Key = i.ToString(),
                Value = "value",
                Partition = i % 3,
                Timestamp = DateTimeOffset.UnixEpoch,
            })
            .ToArray();
    }

    [Benchmark]
    public Task<RecordMetadata[]> ProduceAllAsync() => _producer.ProduceAllAsync(GetMessages());

    private IEnumerable<TopicProducerMessage<string, string>> GetMessages() =>
        Source is MessageSource.Array ? _messages : EnumerateMessages();

    private IEnumerable<TopicProducerMessage<string, string>> EnumerateMessages()
    {
        for (var i = 0; i < _messages.Length; i++)
            yield return _messages[i];
    }

    private sealed class BenchmarkProducer : IKafkaProducer<string, string>, IProducerFastPath<string, string>
    {
        private readonly int _expectedCount;
        private int _checksum;

        public BenchmarkProducer(int expectedCount) => _expectedCount = expectedCount;

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<RecordMetadata> ProduceAsync(
            ProducerMessage<string, string> message,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<RecordMetadata> ProduceAsync(
            string topic,
            string? key,
            string value,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        ValueTask<RecordMetadata> IProducerFastPath<string, string>.ProduceAsync(
            string topic,
            string? key,
            string value,
            Headers? headers,
            int? partition,
            DateTimeOffset? timestamp,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask FireAsync(ProducerMessage<string, string> message) => throw new NotSupportedException();

        public ValueTask FireAsync(string topic, string? key, string value) => throw new NotSupportedException();

        public ValueTask FireAsync(
            ProducerMessage<string, string> message,
            Action<RecordMetadata, Exception?> deliveryHandler) => throw new NotSupportedException();

        public Task<RecordMetadata[]> ProduceAllAsync(
            IEnumerable<ProducerMessage<string, string>> messages,
            CancellationToken cancellationToken = default)
        {
            var results = new RecordMetadata[_expectedCount];
            var checksum = 0;
            var index = 0;
            foreach (var message in messages)
            {
                checksum += message.Value.Length;
                results[index] = CreateMetadata(index++);
            }

            ValidateCount(index);
            Volatile.Write(ref _checksum, checksum);
            return Task.FromResult(results);
        }

        public Task<RecordMetadata[]> ProduceAllAsync(
            string topic,
            IEnumerable<(string? Key, string Value)> messages,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RecordMetadata[]> ProduceAllAsync(
            string topic,
            IEnumerable<TopicProducerMessage<string, string>> messages,
            CancellationToken cancellationToken = default)
        {
            var results = new RecordMetadata[_expectedCount];
            var checksum = 0;
            var index = 0;
            foreach (var message in messages)
            {
                checksum += message.Value.Length;
                results[index] = CreateMetadata(index++);
            }

            ValidateCount(index);
            Volatile.Write(ref _checksum, checksum);
            return Task.FromResult(results);
        }

        private static RecordMetadata CreateMetadata(int index) => new()
        {
            Topic = "benchmark-topic",
            Partition = index % 3,
            Offset = index,
            Timestamp = DateTimeOffset.UnixEpoch,
        };

        private void ValidateCount(int count)
        {
            if (count != _expectedCount)
                throw new InvalidOperationException($"Expected {_expectedCount} messages but received {count}.");
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PurgeAsync(PurgeOptions options, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
        {
        }

        public void UnregisterMetricFromSubscription(string name)
        {
        }

        public ITransaction<string, string> BeginTransaction() => throw new NotSupportedException();

        public ValueTask InitTransactionsAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask InitTransactionsAsync(
            bool keepPreparedTransaction,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask CompletePreparedTransactionAsync(
            PreparedTransactionState preparedState,
            bool committed,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ITopicProducer<string, string> ForTopic(string topic) =>
            new TopicProducer<string, string>(this, topic, ownsProducer: false);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
