using System.Diagnostics;
using Dekaf.Diagnostics;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Outbox;

/// <summary>
/// Default <see cref="IOutboxPublisher"/> backed by a Dekaf byte-array producer. The producer
/// should keep idempotence enabled (the Dekaf default) for retries of admitted batches.
/// Partial failures can still let later rows arrive before a failed row is retried; message-id
/// deduplication does not restore enqueue order in that case.
/// </summary>
/// <remarks>
/// When a row carries the trace context captured by <see cref="OutboxMessage.Create"/> and a
/// listener is attached to <see cref="DekafDiagnostics.ActivitySourceName"/>, each row is
/// published under an <c>outbox relay {topic}</c> span parented to that stored context. The
/// producer's send span becomes its child and replaces the stored <c>traceparent</c> and
/// <c>tracestate</c> headers, so consumers continue the enqueuing request's trace. Without a
/// listener the stored headers are published unchanged.
/// </remarks>
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
        // Later rows can succeed even when an earlier row is rejected locally or by Kafka.
        // Prefix accounting keeps those rows for retry; it cannot undo their delivery or
        // restore consumer-observed order, even after message-id deduplication.
        // Concurrent sends preserve batching (rather than one round trip per row).
        // The per-row Headers, id buffer, ProducerMessage, and Task allocations below are
        // accepted: the relay is not the Kafka client's hot path, and the header value
        // cannot be pooled - a produce cancelled after append keeps delivering in the
        // background, so its header memory stays referenced after this method returns.
        var tasks = new Task<RecordMetadata>[messages.Count];
        Activity?[]? activities = null;
        var startedCount = 0;
        Exception? firstError = null;
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            Activity? activity = null;
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

                // StartActivity makes the relay span Activity.Current so the producer's send
                // span parents to it; the finally restores the relay loop's own context.
                var previousActivity = Activity.Current;
                activity = StartRelayActivity(message, headers);
                try
                {
                    // Timestamp is deliberately left unset (publish time): stamping the enqueue
                    // time would let a record published after a long backlog land already past
                    // a CreateTime topic's retention.ms - the broker acks, the row is deleted,
                    // and the segment can be cleaned before any consumer reads it (loss).
                    tasks[i] = _producer.ProduceAsync(new ProducerMessage<byte[]?, byte[]?>
                    {
                        Topic = message.Topic,
                        Key = message.Key,
                        Value = message.Value,
                        Headers = headers,
                        Partition = message.Partition
                    }, cancellationToken).AsTask();
                }
                finally
                {
                    if (activity is not null)
                        Activity.Current = previousActivity;
                }

                if (activity is not null)
                {
                    activities ??= new Activity?[messages.Count];
                    activities[i] = activity;
                }

                startedCount++;
            }
            catch (Exception ex)
            {
                if (activity is not null)
                {
                    DekafDiagnostics.RecordException(activity, ex);
                    activity.Stop();
                }

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
            var activity = activities?[i];
            try
            {
                await tasks[i].ConfigureAwait(false);
                activity?.SetStatus(ActivityStatusCode.Ok);
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
                if (activity is not null)
                    DekafDiagnostics.RecordException(activity, ex);
                firstError ??= ex;
            }
            finally
            {
                activity?.Stop();
            }
        }

        return new OutboxPublishResult(ackedCount, firstError);
    }

    /// <summary>
    /// Starts the relay span parented to the trace context stored on the row, and drops the
    /// stored trace headers so the producer's send span context is the only one published.
    /// Returns null, leaving the headers unchanged, when no listener samples the span or the
    /// row carries no valid stored context.
    /// </summary>
    private static Activity? StartRelayActivity(OutboxMessage message, Headers headers)
    {
        if (!DekafDiagnostics.Source.HasListeners())
            return null;

        var traceparent = headers.GetFirstAsString(TraceContextPropagator.TraceparentHeader);
        if (traceparent is null
            || !ActivityContext.TryParse(
                traceparent,
                headers.GetFirstAsString(TraceContextPropagator.TracestateHeader),
                isRemote: true,
                out var parentContext))
        {
            return null;
        }

        var activity = DekafDiagnostics.Source.StartActivity(
            string.Concat("outbox relay ", message.Topic), ActivityKind.Internal, parentContext);
        if (activity is null)
            return null;

        activity.SetTag(DekafDiagnostics.MessagingSystem, DekafDiagnostics.MessagingSystemValue);
        activity.SetTag(DekafDiagnostics.MessagingDestinationName, message.Topic);
        activity.SetTag(DekafDiagnostics.MessagingMessageId, message.MessageId.ToString("D"));
        headers.Remove(TraceContextPropagator.TraceparentHeader);
        headers.Remove(TraceContextPropagator.TracestateHeader);
        return activity;
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsProducer)
            await _producer.DisposeAsync().ConfigureAwait(false);
    }
}
