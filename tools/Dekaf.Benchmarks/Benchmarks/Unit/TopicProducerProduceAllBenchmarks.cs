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

    [Params(1, 100)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _producer = new BenchmarkProducer().ForTopic("benchmark-topic");
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
    public Task<RecordMetadata[]> ProduceAllAsync() => _producer.ProduceAllAsync(_messages);

    private sealed class BenchmarkProducer : IKafkaProducer<string, string>, IProducerFastPath<string, string>
    {
        private static readonly RecordMetadata[] Results = [];
        private static readonly Task<RecordMetadata[]> CompletedResults = Task.FromResult(Results);
        private int _checksum;

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
            var checksum = 0;
            foreach (var message in messages)
                checksum += message.Value.Length;

            Volatile.Write(ref _checksum, checksum);
            return CompletedResults;
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
            var checksum = 0;
            foreach (var message in messages)
                checksum += message.Value.Length;

            Volatile.Write(ref _checksum, checksum);
            return CompletedResults;
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
