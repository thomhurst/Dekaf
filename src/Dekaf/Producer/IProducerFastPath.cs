using Dekaf.Serialization;

namespace Dekaf.Producer;

internal interface IProducerFastPath<TKey, TValue>
{
    ValueTask<RecordMetadata> ProduceAsync(
        string topic,
        TKey? key,
        TValue value,
        Headers? headers,
        int? partition,
        DateTimeOffset? timestamp,
        CancellationToken cancellationToken);

    Task<RecordMetadata[]> ProduceAllAsync(
        string topic,
        IEnumerable<TopicProducerMessage<TKey, TValue>> messages,
        CancellationToken cancellationToken);
}
