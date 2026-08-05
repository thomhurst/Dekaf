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
    private static readonly string[] SentInstrumentNames =
        ["messaging.client.sent.messages", "dekaf.producer.sent.bytes"];

    [Test]
    [NotInParallel("MeterListener")]
    public async Task CompleteSend_EmitsRecordCountAndEncodedBytes_OncePerBatch()
    {
        var topic = $"delivery-metrics-{Guid.NewGuid():N}";
        var (listener, counters) = CreateSentCounterListener(topic);
        using var _ = listener;

        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        var (batch, _) = CreateBatch(pool, topic, recordCount: 3, encodedSize: 4096);

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

    [Test]
    [NotInParallel("MeterListener")]
    public async Task CompleteSend_ThrowingMetricsListener_StillCompletesAwaiters()
    {
        // A listener callback is user (or exporter) code running inline inside Counter.Add,
        // after CompleteSend has claimed the once-only completion guard. If it escaped, the
        // batch would retire with its awaiters unsignalled and every later Fail would
        // short-circuit on the same guard — ProduceAsync callers would wait forever.
        var topic = $"delivery-metrics-throw-{Guid.NewGuid():N}";
        var (listener, _) = CreateSentCounterListener(
            topic, onMeasurement: () => throw new InvalidOperationException("exporter blew up"));
        using var _1 = listener;

        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        var (batch, delivery) = CreateBatch(pool, topic, recordCount: 1, encodedSize: 512);

        batch.CompleteSend(baseOffset: 7, DateTimeOffset.UtcNow);

        var metadata = await delivery.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(metadata.Offset).IsEqualTo(7);
    }

    [Test]
    [NotInParallel("MeterListener")]
    public async Task Fail_ThrowingMetricsListener_StillFaultsAwaiters()
    {
        // Same hazard on the failure path: the unobserved-error counter is emitted after the
        // guard is claimed but before the completion sources receive the exception.
        var topic = $"delivery-metrics-fail-throw-{Guid.NewGuid():N}";
        using var listener = AccumulatorTestHelpers.StartMeterListener(
            ["dekaf.producer.send.errors"],
            onLong: (_, _, tags, _) =>
            {
                if (AccumulatorTestHelpers.GetTag(tags, DekafDiagnostics.MessagingDestinationName) == topic)
                    throw new InvalidOperationException("exporter blew up");
            });

        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        // 3 records, 1 awaiter — the other two are unobserved, so the error counter fires.
        var (batch, delivery) = CreateBatch(pool, topic, recordCount: 3, encodedSize: 512);

        batch.Fail(new InvalidOperationException("delivery failed"));

        await Assert.That(async () => await delivery.WaitAsync(TimeSpan.FromSeconds(5)))
            .Throws<InvalidOperationException>();
    }

    private sealed class SentCounters
    {
        public long Messages;
        public long Bytes;
    }

    private static (MeterListener Listener, SentCounters Counters) CreateSentCounterListener(
        string topic, Action? onMeasurement = null)
    {
        var counters = new SentCounters();
        var listener = AccumulatorTestHelpers.StartMeterListener(
            SentInstrumentNames,
            onLong: (instrument, measurement, tags, _) =>
            {
                if (AccumulatorTestHelpers.GetTag(tags, DekafDiagnostics.MessagingDestinationName) != topic)
                    return;

                onMeasurement?.Invoke();

                if (instrument.Name == "messaging.client.sent.messages")
                    counters.Messages += measurement;
                else
                    counters.Bytes += measurement;
            });

        return (listener, counters);
    }

    private static (ReadyBatch Batch, Task<RecordMetadata> Delivery) CreateBatch(
        ValueTaskSourcePool<RecordMetadata> pool, string topic, int recordCount, int encodedSize)
    {
        var batch = new ReadyBatch();
        var sources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(1);
        var source = pool.Rent();
        sources[0] = source;
        var delivery = source.Task.AsTask();

        batch.Initialize(
            new TopicPartition(topic, 0),
            new RecordBatch { Records = Array.Empty<Record>() },
            sources,
            completionSourcesCount: 1,
            recordCount: recordCount,
            dataSize: 100);
        batch.TrySetMemoryReleased();
        batch.SetEncodedSize(encodedSize);
        return (batch, delivery);
    }
}
