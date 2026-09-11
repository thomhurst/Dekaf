using Dekaf.Networking;
using Dekaf.Protocol;
using System.Runtime.CompilerServices;

namespace Dekaf.ShareConsumer;

// Hosting capabilities are opt-in. Record payload capture reuses storage per poll round;
// ordinary share consumers retain their existing record allocation and acknowledgement paths.
internal sealed partial class KafkaShareConsumer<TKey, TValue>
{
    private long _acquisitionStartedTimestamp;
    // Only hosted renewal buffering needs this map. Reuse one timestamp per partition
    // without adding a timestamp field or another allocation to every delivered record.
    private Dictionary<TopicPartition, long>? _bufferedAcquisitionTimestamps;
    private bool _hostedProcessing;
    private CancellationToken _hostedRequestCancellationToken;
    private Dictionary<int, KafkaRequestWriteContext>? _hostedRequestContexts;
    long IHostedShareConsumer.AcquisitionStartedTimestamp => _acquisitionStartedTimestamp;

    // Legacy hosts do not supply a separate shutdown budget. Keep their per-call cancellation.
    void IHostedShareConsumer.ObserveAcknowledgements(ShareAcknowledgementCommitCallback observer)
        => ((IHostedShareConsumer)this).ObserveAcknowledgements(observer, default);

    void IHostedShareConsumer.ObserveAcknowledgements(ShareAcknowledgementCommitCallback observer,
        CancellationToken requestCancellationToken)
    {
        _hostedProcessing = true;
        _hostedRequestCancellationToken = requestCancellationToken;
        var applicationCallback = _acknowledgementCommitCallback;
        _acknowledgementCommitCallback = results =>
        {
            observer(results);
            applicationCallback?.Invoke(results);
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private KafkaRequestWriteContext? GetHostedRequestContext(int brokerId)
    {
        if (!_hostedRequestCancellationToken.CanBeCanceled)
            return null;
        var contexts = _hostedRequestContexts ??= [];
        if (!contexts.TryGetValue(brokerId, out var context))
        {
            context = new KafkaRequestWriteContext(_hostedRequestCancellationToken);
            contexts.Add(brokerId, context);
        }
        return context;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask<TResponse> SendHostedRequestAsync<TRequest, TResponse>(
        IKafkaConnection connection, TRequest request, short version,
        KafkaRequestWriteContext? context, CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        if (context is not null && context.ResponseCancellationToken.CanBeCanceled)
        {
            if (connection is IKafkaRequestCancellationConnection controlled)
                return controlled.SendWithResponseCancellationAsync<TRequest, TResponse>(
                    request, version, context, cancellationToken);

            // Custom connections without response-cancellation support use the caller's token.
            context.MarkWriteStarted();
            return connection.SendAsync<TRequest, TResponse>(request, version, cancellationToken);
        }

        return SendObservedRequestAsync<TRequest, TResponse>(connection, request, version, context, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask<TResponse> SendObservedRequestAsync<TRequest, TResponse>(
        IKafkaConnection connection, TRequest request, short version,
        KafkaRequestWriteContext? context, CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        if (context is not null && connection is IKafkaRequestWriteObserverConnection observed)
            return observed.SendWithWriteObservationAsync<TRequest, TResponse>(
                request, version, context.WriteStartedCallback, cancellationToken);

        // Custom connections cannot prove their write boundary. Keep caller cancellation and
        // treat a failure after handing them the request as potentially submitted.
        context?.MarkWriteStarted();
        return connection.SendAsync<TRequest, TResponse>(request, version, cancellationToken);
    }

    private Dictionary<TopicPartitionOffset, (int Start, int KeyLength, int ValueLength)>? _rawRecords;
    private System.Buffers.ArrayBufferWriter<byte>? _rawBuffer;

    public ShareAcknowledgementMode AcknowledgementMode => _options.AcknowledgementMode;

    void IRawShareRecordAccessor.EnableRawRecordTracking()
    {
        _rawRecords ??= [];
        _rawBuffer ??= new System.Buffers.ArrayBufferWriter<byte>();
    }

    bool IRawShareRecordAccessor.TryGetRawRecord(TopicPartitionOffset record, out byte[]? key, out byte[]? value)
    {
        if (_rawRecords is not null && _rawRecords.TryGetValue(record, out var raw))
        {
            key = raw.KeyLength < 0 ? null : _rawBuffer!.WrittenMemory.Slice(raw.Start, raw.KeyLength).ToArray();
            value = raw.ValueLength < 0 ? null : _rawBuffer!.WrittenMemory
                .Slice(raw.Start + Math.Max(0, raw.KeyLength), raw.ValueLength).ToArray();
            return true;
        }
        key = null;
        value = null;
        return false;
    }

    private void CaptureRawRecord(TopicPartitionOffset record, ReadOnlyMemory<byte>? key, ReadOnlyMemory<byte>? value)
    {
        // Reuse scratch storage per poll round. Successful processing allocates no raw arrays;
        // only a failed record requesting a durable copy materializes its key and value.
        var start = _rawBuffer!.WrittenCount;
        var length = (key?.Length ?? 0) + (value?.Length ?? 0);
        var destination = _rawBuffer.GetSpan(length);
        key.GetValueOrDefault().Span.CopyTo(destination);
        value.GetValueOrDefault().Span.CopyTo(destination[(key?.Length ?? 0)..]);
        _rawBuffer.Advance(length);
        _rawRecords![record] = (start, key?.Length ?? -1, value?.Length ?? -1);
    }
}
