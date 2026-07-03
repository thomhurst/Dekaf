using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.Producer;

internal sealed class FastPathProducerSpy<TKey, TValue> : IKafkaProducer<TKey, TValue>, IProducerFastPath<TKey, TValue>
{
    public int FastPathCalls { get; private set; }
    public int MessageCalls { get; private set; }
    public string? CapturedTopic { get; private set; }
    public TKey? CapturedKey { get; private set; }
    public TValue? CapturedValue { get; private set; }
    public Headers? CapturedHeaders { get; private set; }
    public int? CapturedPartition { get; private set; }
    public DateTimeOffset? CapturedTimestamp { get; private set; }
    public CancellationToken CapturedCancellationToken { get; private set; }

    public RecordMetadata Result { get; set; } = new()
    {
        Topic = "my-topic",
        Partition = 0,
        Offset = 42,
        Timestamp = DateTimeOffset.UnixEpoch
    };

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        MessageCalls++;
        CapturedTopic = message.Topic;
        CapturedKey = message.Key;
        CapturedValue = message.Value;
        CapturedHeaders = message.Headers;
        CapturedPartition = message.Partition;
        CapturedTimestamp = message.Timestamp;
        CapturedCancellationToken = cancellationToken;
        return ValueTask.FromResult(Result);
    }

    public ValueTask<RecordMetadata> ProduceAsync(
        string topic,
        TKey? key,
        TValue value,
        CancellationToken cancellationToken = default)
        => ((IProducerFastPath<TKey, TValue>)this).ProduceAsync(
            topic, key, value, headers: null, partition: null, timestamp: null, cancellationToken);

    ValueTask<RecordMetadata> IProducerFastPath<TKey, TValue>.ProduceAsync(
        string topic,
        TKey? key,
        TValue value,
        Headers? headers,
        int? partition,
        DateTimeOffset? timestamp,
        CancellationToken cancellationToken)
    {
        FastPathCalls++;
        CapturedTopic = topic;
        CapturedKey = key;
        CapturedValue = value;
        CapturedHeaders = headers;
        CapturedPartition = partition;
        CapturedTimestamp = timestamp;
        CapturedCancellationToken = cancellationToken;
        return ValueTask.FromResult(Result);
    }

    public ValueTask FireAsync(ProducerMessage<TKey, TValue> message) => ValueTask.CompletedTask;

    public ValueTask FireAsync(string topic, TKey? key, TValue value) => ValueTask.CompletedTask;

    public ValueTask FireAsync(
        ProducerMessage<TKey, TValue> message,
        Action<RecordMetadata, Exception?> deliveryHandler)
        => ValueTask.CompletedTask;

    public Task<RecordMetadata[]> ProduceAllAsync(
        IEnumerable<ProducerMessage<TKey, TValue>> messages,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<RecordMetadata>());

    public Task<RecordMetadata[]> ProduceAllAsync(
        string topic,
        IEnumerable<(TKey? Key, TValue Value)> messages,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<RecordMetadata>());

    public ValueTask FlushAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
    {
    }

    public void UnregisterMetricFromSubscription(string name)
    {
    }

    public ITransaction<TKey, TValue> BeginTransaction() => throw new NotSupportedException();

    public ValueTask InitTransactionsAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ITopicProducer<TKey, TValue> ForTopic(string topic) => new TopicProducer<TKey, TValue>(this, topic, ownsProducer: false);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
