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
    long IHostedShareConsumer.AcquisitionStartedTimestamp => _acquisitionStartedTimestamp;

    void IHostedShareConsumer.ObserveAcknowledgements(ShareAcknowledgementCommitCallback observer)
    {
        _hostedProcessing = true;
        var applicationCallback = _acknowledgementCommitCallback;
        _acknowledgementCommitCallback = results =>
        {
            observer(results);
            applicationCallback?.Invoke(results);
        };
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
