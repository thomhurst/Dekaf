using System.Globalization;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Serialization;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    /// <summary>
    /// Writes the dead-letter record that a KIP-1191 broker writes for a rejected share record.
    /// Like the broker, a missing topic does not fail the acknowledgement; the record is skipped.
    /// </summary>
    internal void AppendShareDeadLetterRecord(
        string deadLetterTopic,
        bool copyRecord,
        string groupId,
        InMemoryRecord source,
        int deliveryCount)
    {
        // Kafka 4.4.0 copies the key and value when asked, but never the original headers.
        var headers = new List<Header>(6);
        headers.Add(new Header(ShareGroupDeadLetterHeaders.TopicKey, Utf8(source.Topic)));
        headers.Add(new Header(ShareGroupDeadLetterHeaders.PartitionKey, Utf8(source.Partition.ToString(CultureInfo.InvariantCulture))));
        headers.Add(new Header(ShareGroupDeadLetterHeaders.OffsetKey, Utf8(source.Offset.ToString(CultureInfo.InvariantCulture))));
        headers.Add(new Header(ShareGroupDeadLetterHeaders.GroupKey, Utf8(groupId)));
        headers.Add(new Header(ShareGroupDeadLetterHeaders.DeliveryCountKey, Utf8(deliveryCount.ToString(CultureInfo.InvariantCulture))));
        headers.Add(new Header(ShareGroupDeadLetterHeaders.MessageKey, Utf8("Offset rejected by client.")));

        TaskCompletionSource signal;
        lock (_gate)
        {
            if (!_topics.ContainsKey(deadLetterTopic) && !_options.AutoCreateTopics)
                return;

            var state = GetOrAutoCreateTopic(deadLetterTopic);
            var key = copyRecord ? source.Key : [];
            var value = copyRecord ? source.Value : [];
            AppendRecordUnderLock(
                deadLetterTopic,
                state,
                SelectPartition(state, requestedPartition: null, key, isKeyNull: !copyRecord || source.IsKeyNull),
                key,
                isKeyNull: !copyRecord || source.IsKeyNull,
                value,
                isValueNull: !copyRecord || source.IsValueNull,
                headers,
                DateTimeOffset.UtcNow,
                transactionMarker: null,
                out signal);
        }

        signal.TrySetResult();

        static byte[] Utf8(string value) => System.Text.Encoding.UTF8.GetBytes(value);
    }
}
