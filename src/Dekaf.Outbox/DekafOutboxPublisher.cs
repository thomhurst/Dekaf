using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Outbox;

/// <summary>
/// Default <see cref="IOutboxPublisher"/> backed by a Dekaf byte-array producer. The producer
/// should keep idempotence enabled (the Dekaf default) so broker-side retries cannot reorder
/// records within a partition, which keeps the relay's contiguous-prefix accounting sound.
/// </summary>
public sealed class DekafOutboxPublisher : IOutboxPublisher
{
    private readonly IKafkaProducer<byte[]?, byte[]?> _producer;
    private readonly bool _ownsProducer;

    /// <summary>
    /// Wraps an existing producer.
    /// </summary>
    /// <param name="producer">The producer to publish with.</param>
    /// <param name="ownsProducer">True to dispose the producer when this publisher is disposed.</param>
    public DekafOutboxPublisher(IKafkaProducer<byte[]?, byte[]?> producer, bool ownsProducer = true)
    {
        ArgumentNullException.ThrowIfNull(producer);
        _producer = producer;
        _ownsProducer = ownsProducer;
    }

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
        => _producer.InitializeAsync(cancellationToken);

    public async ValueTask<OutboxPublishResult> PublishAsync(
        IReadOnlyList<OutboxMessage> messages,
        string messageIdHeaderName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentException.ThrowIfNullOrEmpty(messageIdHeaderName);

        if (messages.Count == 0)
            return new OutboxPublishResult(0, null);

        // Fire every produce before awaiting any, then await in order. Per-record outcomes
        // are required for prefix accounting, which is why this is not ProduceAllAsync
        // (its counting completion exposes only an aggregate result). AsTask() follows the
        // producer contract that ValueTasks must not be stored; the Task allocations are
        // relay-side, per-batch-attempt cost.
        //
        // Firing the whole batch is order-safe: the producer keeps per-partition FIFO from
        // pending-append through idempotent sequencing, so a broker-side failure of record
        // N also fails every later record on that partition (no gap can be acked) and the
        // contiguous-prefix accounting below stays truthful. Publishing one-record-at-a-time
        // would defeat batching entirely (one linger + round trip per row).
        // The per-row Headers, id buffer, ProducerMessage, and Task allocations below are
        // accepted: the relay is not the Kafka client's hot path, and the header value
        // cannot be pooled - a produce cancelled after append keeps delivering in the
        // background, so its header memory stays referenced after this method returns.
        var tasks = new Task<RecordMetadata>[messages.Count];
        var startedCount = 0;
        Exception? firstError = null;
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            try
            {
                // Remove drops any caller-supplied header colliding with the reserved dedup
                // name, so the stamped message id is always the one consumers read. In-place
                // mutation is safe: Decode builds a fresh Headers per call.
                var headers = OutboxHeaderCodec.Decode(message.Headers)?.Remove(messageIdHeaderName)
                    ?? new Headers(1);
                var messageIdUtf8 = new byte[36];
                message.MessageId.TryFormat(messageIdUtf8, out _, "D");
                headers.Add(messageIdHeaderName, messageIdUtf8);

                tasks[i] = _producer.ProduceAsync(new ProducerMessage<byte[]?, byte[]?>
                {
                    Topic = message.Topic,
                    Key = message.Key,
                    Value = message.Value,
                    Headers = headers,
                    Partition = message.Partition,
                    Timestamp = message.CreatedAtUtc
                }, cancellationToken).AsTask();
                startedCount++;
            }
            catch (Exception ex)
            {
                // Any synchronous per-row failure - a corrupt persisted header blob, a
                // disposed producer, a custom implementation validating eagerly - must not
                // skip the await loop below: the tasks already started still need awaiting
                // so their acks count toward the prefix and no fault goes unobserved.
                firstError = ex;
                break;
            }
        }

        // Awaiting each already-started task waits for all of them and observes every
        // exception, so no fault can surface as an unobserved task exception.
        var ackedCount = 0;
        for (var i = 0; i < startedCount; i++)
        {
            try
            {
                await tasks[i].ConfigureAwait(false);
                // The contiguous prefix is intact at index i iff every prior task acked;
                // once any task fails, ackedCount freezes below all later indexes.
                if (ackedCount == i)
                    ackedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutdown, not a broker failure: the unacked rows simply retry on the next
                // start, so don't record an error that would log as a publish failure. The
                // prefix still stops here - later acks must not be marked past this row.
            }
            catch (Exception ex)
            {
                firstError ??= ex;
            }
        }

        return new OutboxPublishResult(ackedCount, firstError);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsProducer)
            await _producer.DisposeAsync().ConfigureAwait(false);
    }
}
