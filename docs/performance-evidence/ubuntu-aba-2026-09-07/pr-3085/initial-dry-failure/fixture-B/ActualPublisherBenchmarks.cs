using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using Dekaf.Outbox;
using Dekaf.Producer;
using Dekaf.Telemetry;

[MemoryDiagnoser]
[MedianColumn, MaxColumn]
public class ActualPublisherBenchmarks
{
    [ParamsSource(nameof(Modes))]
    public bool Enabled { get; set; }
    public IEnumerable<bool> Modes() => new RelayMetricsBenchmarks().Modes();
    private RelayMetricsBenchmarks.Store _store = null!;
    private AckProducer _producer = null!;
    private DekafOutboxPublisher _publisher = null!;
    private OutboxRelayService _relay = null!;
    private Func<CancellationToken, ValueTask> _cycle = null!;
    private MeterListener? _listener;
    public long Acknowledged;
    public long Deleted => _store.Deleted;
    public long Submitted => _producer.Submitted;

    [GlobalSetup]
    public void Setup()
    {
        if (Enabled)
        {
            _listener = new MeterListener();
            _listener.InstrumentPublished = static (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Dekaf.Outbox") listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            {
                if (instrument.Name == "dekaf.outbox.publish.acknowledged") Acknowledged += value;
            });
            _listener.SetMeasurementEventCallback<double>(static (_, _, _, _) => { });
            _listener.Start();
        }
        _store = new();
        _producer = new();
        _publisher = new(_producer, ownsProducer: false);
        _relay = RelayMetricsBenchmarks.CreateRelay(_store, _publisher);
        _cycle = RelayMetricsBenchmarks.Bind(_relay);
    }

    [Benchmark]
    public ValueTask RelayBatch() => _cycle(default);
    [Benchmark]
    public ValueTask<OutboxPublishResult> PublisherControl() =>
        _publisher.PublishAsync(_store.Rows, "x-outbox-message-id");

    [GlobalCleanup]
    public void Cleanup() { _relay.Dispose(); _listener?.Dispose(); }

    private sealed class AckProducer : IKafkaProducer<byte[]?, byte[]?>
    {
        internal long Submitted;
        public ValueTask<RecordMetadata> ProduceAsync(ProducerMessage<byte[]?, byte[]?> message, CancellationToken cancellationToken = default)
        {
            Submitted++;
            return new(new RecordMetadata { Topic = message.Topic, Partition = 0, Offset = 0, Timestamp = DateTimeOffset.UnixEpoch });
        }
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask DisposeAsync() => default;
        public ValueTask<RecordMetadata> ProduceAsync(string topic, byte[]? key, byte[]? value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask FireAsync(ProducerMessage<byte[]?, byte[]?> message) => throw new NotSupportedException();
        public ValueTask FireAsync(string topic, byte[]? key, byte[]? value) => throw new NotSupportedException();
        public ValueTask FireAsync(ProducerMessage<byte[]?, byte[]?> message, Action<RecordMetadata, Exception?> handler) => throw new NotSupportedException();
        public Task<RecordMetadata[]> ProduceAllAsync(IEnumerable<ProducerMessage<byte[]?, byte[]?>> messages, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RecordMetadata[]> ProduceAllAsync(string topic, IEnumerable<(byte[]? Key, byte[]? Value)> messages, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask FlushAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask PurgeAsync(PurgeOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric) => throw new NotSupportedException();
        public void UnregisterMetricFromSubscription(string name) => throw new NotSupportedException();
        public ITransaction<byte[]?, byte[]?> BeginTransaction() => throw new NotSupportedException();
        public ValueTask InitTransactionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask InitTransactionsAsync(bool keepPreparedTransaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask CompletePreparedTransactionAsync(PreparedTransactionState state, bool committed, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ITopicProducer<byte[]?, byte[]?> ForTopic(string topic) => throw new NotSupportedException();
    }
}
