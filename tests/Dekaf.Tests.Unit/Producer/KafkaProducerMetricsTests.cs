using System.Diagnostics.Metrics;
using System.Reflection;
using Dekaf.Diagnostics;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

public sealed class KafkaProducerMetricsTests
{
    [Test]
    [NotInParallel("MeterListener")]
    public async Task AwaitWithMetrics_MeterOnlyListener_RecordsDurationOnly()
    {
        // Sent counters are per-batch (see ReadyBatchDeliveryMetricsTests); the awaited
        // path must emit only the operation duration, never the counters.
        var topic = $"orders-{Guid.NewGuid():N}";
        long messagesSent = 0;
        long bytesSent = 0;
        double operationDuration = -1;
        string? durationTopic = null;

        using var listener = AccumulatorTestHelpers.StartMeterListener(
            ProducerSuccessMetrics,
            onLong: (instrument, measurement, tags, _) =>
            {
                if (GetTag(tags, DekafDiagnostics.MessagingDestinationName) != topic)
                    return;

                if (instrument.Name == "messaging.client.sent.messages")
                    messagesSent += measurement;
                else if (instrument.Name == "dekaf.producer.sent.bytes")
                    bytesSent += measurement;
            },
            onDouble: (instrument, measurement, tags, _) =>
            {
                var metricTopic = GetTag(tags, DekafDiagnostics.MessagingDestinationName);
                if (metricTopic != topic)
                    return;

                if (instrument.Name == "messaging.client.operation.duration")
                {
                    operationDuration = measurement;
                    durationTopic = metricTopic;
                }
            });

        await Assert.That(OperationScopedMetricsEnabled()).IsTrue();

        await using var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "metrics-test"
            },
            Serializers.String,
            Serializers.String);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        var completion = pool.Rent();
        completion.TrySetResult(new RecordMetadata
        {
            Topic = topic,
            Partition = 1,
            Offset = 42,
            Timestamp = DateTimeOffset.UtcNow
        });

        var metadata = await InvokeAwaitWithMetrics(producer, completion, topic);

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(messagesSent).IsEqualTo(0);
        await Assert.That(bytesSent).IsEqualTo(0);
        await Assert.That(operationDuration).IsGreaterThanOrEqualTo(0);
        await Assert.That(durationTopic).IsEqualTo(topic);
    }

    [Test]
    [NotInParallel("MeterListener")]
    public async Task AwaitWithMetrics_Failure_RecordsErrorTypeOnErrorCountAndDuration()
    {
        var topic = $"orders-{Guid.NewGuid():N}";
        long errorCount = 0;
        string? errorCountErrorType = null;
        double operationDuration = -1;
        string? durationErrorType = null;

        using var listener = AccumulatorTestHelpers.StartMeterListener(
            ["dekaf.producer.send.errors", "messaging.client.operation.duration"],
            onLong: (instrument, measurement, tags, _) =>
            {
                if (GetTag(tags, DekafDiagnostics.MessagingDestinationName) != topic)
                    return;

                if (instrument.Name == "dekaf.producer.send.errors")
                {
                    errorCount += measurement;
                    errorCountErrorType = GetTag(tags, DekafDiagnostics.ErrorType);
                }
            },
            onDouble: (instrument, measurement, tags, _) =>
            {
                if (GetTag(tags, DekafDiagnostics.MessagingDestinationName) != topic)
                    return;

                if (instrument.Name == "messaging.client.operation.duration")
                {
                    operationDuration = measurement;
                    durationErrorType = GetTag(tags, DekafDiagnostics.ErrorType);
                }
            });

        await using var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "metrics-test"
            },
            Serializers.String,
            Serializers.String);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        var completion = pool.Rent();
        completion.TrySetException(new InvalidOperationException("delivery failed"));

        await Assert.That(async () => await InvokeAwaitWithMetrics(producer, completion, topic))
            .Throws<InvalidOperationException>();

        // Failed operations must record duration with error.type per the messaging semconv.
        await Assert.That(errorCount).IsEqualTo(1);
        await Assert.That(errorCountErrorType).IsEqualTo(typeof(InvalidOperationException).FullName);
        await Assert.That(operationDuration).IsGreaterThanOrEqualTo(0);
        await Assert.That(durationErrorType).IsEqualTo(typeof(InvalidOperationException).FullName);
    }

    [Test]
    public async Task AwaitWithMetrics_Cancellation_CancelsCompletion()
    {
        await using var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "metrics-test"
            },
            Serializers.String,
            Serializers.String);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        var completion = pool.Rent();
        using var cts = new CancellationTokenSource();

        var pending = InvokeAwaitWithMetrics(producer, completion, "orders", cts.Token);
        cts.Cancel();

        await Assert.That(async () => await pending).Throws<OperationCanceledException>();
    }

    private static bool OperationScopedMetricsEnabled()
    {
        var method = typeof(KafkaProducer<string, string>).GetMethod(
            "OperationScopedMetricsEnabled",
            BindingFlags.NonPublic | BindingFlags.Static);

        return (bool)method!.Invoke(null, null)!;
    }

    private static async ValueTask<RecordMetadata> InvokeAwaitWithMetrics(
        KafkaProducer<string, string> producer,
        PooledValueTaskSource<RecordMetadata> completion,
        string topic,
        CancellationToken cancellationToken = default)
    {
        var method = typeof(KafkaProducer<string, string>).GetMethod(
            "AwaitWithMetrics",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var result = (ValueTask<RecordMetadata>)method!.Invoke(
            producer,
            [completion, topic, cancellationToken])!;
        return await result;
    }

    private static string? GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
        => AccumulatorTestHelpers.GetTag(tags, key);

    private static readonly string[] ProducerSuccessMetrics =
    [
        "messaging.client.sent.messages",
        "dekaf.producer.sent.bytes",
        "messaging.client.operation.duration"
    ];
}
