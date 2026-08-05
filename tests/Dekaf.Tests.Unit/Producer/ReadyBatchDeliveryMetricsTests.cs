using System.Buffers;
using System.Diagnostics.Metrics;
using Dekaf.Diagnostics;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Pins the per-batch producer counter emission in <see cref="ReadyBatch.CompleteSend"/>:
/// every delivered batch adds its record count to <c>messaging.client.sent.messages</c> and
/// its encoded wire bytes to <c>dekaf.producer.sent.bytes</c> exactly once, including
/// fire-and-forget batches that never touch the awaited-operation metrics path.
/// </summary>
public sealed class ReadyBatchDeliveryMetricsTests
{
    [Test]
    [NotInParallel("MeterListener")]
    public async Task CompleteSend_EmitsRecordCountAndEncodedBytes_OncePerBatch()
    {
        var topic = $"delivery-metrics-{Guid.NewGuid():N}";
        var (listener, counters) = CreateSentCounterListener(topic);
        using var _ = listener;

        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        var batch = CreateBatch(pool, topic, recordCount: 3, encodedSize: 4096);

        batch.CompleteSend(baseOffset: 7, DateTimeOffset.UtcNow);
        // Second call must hit the completion guard — no double count.
        batch.CompleteSend(baseOffset: 7, DateTimeOffset.UtcNow);

        await Assert.That(counters.Messages).IsEqualTo(3);
        await Assert.That(counters.Bytes).IsEqualTo(4096);
    }

    [Test]
    [NotInParallel("MeterListener")]
    public async Task CompleteSend_FireAndForgetBatch_NoCompletionSources_StillEmits()
    {
        var topic = $"delivery-metrics-fnf-{Guid.NewGuid():N}";
        var (listener, counters) = CreateSentCounterListener(topic);
        using var _ = listener;

        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition(topic, 0),
            new RecordBatch { Records = Array.Empty<Record>() },
            completionSourcesArray: null,
            completionSourcesCount: 0,
            recordCount: 5,
            dataSize: 100);
        batch.TrySetMemoryReleased();
        batch.SetEncodedSize(2048);

        // Fire-and-forget acks=0 path completes with baseOffset -1.
        batch.CompleteSend(baseOffset: -1, DateTimeOffset.UtcNow);

        await Assert.That(counters.Messages).IsEqualTo(5);
        await Assert.That(counters.Bytes).IsEqualTo(2048);
    }

    private sealed class SentCounters
    {
        public long Messages;
        public long Bytes;
    }

    private static (MeterListener Listener, SentCounters Counters) CreateSentCounterListener(string topic)
    {
        var counters = new SentCounters();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName &&
                instrument.Name is "messaging.client.sent.messages" or "dekaf.producer.sent.bytes")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (AccumulatorTestHelpers.GetTag(tags, DekafDiagnostics.MessagingDestinationName) != topic)
                return;

            if (instrument.Name == "messaging.client.sent.messages")
                counters.Messages += measurement;
            else
                counters.Bytes += measurement;
        });
        listener.Start();
        return (listener, counters);
    }

    private static ReadyBatch CreateBatch(
        ValueTaskSourcePool<RecordMetadata> pool, string topic, int recordCount, int encodedSize)
    {
        var batch = new ReadyBatch();
        var sources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(1);
        sources[0] = pool.Rent();

        batch.Initialize(
            new TopicPartition(topic, 0),
            new RecordBatch { Records = Array.Empty<Record>() },
            sources,
            completionSourcesCount: 1,
            recordCount: recordCount,
            dataSize: 100);
        batch.TrySetMemoryReleased();
        batch.SetEncodedSize(encodedSize);
        return batch;
    }
}
