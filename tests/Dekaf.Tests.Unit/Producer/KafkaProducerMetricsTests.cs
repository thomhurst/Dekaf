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
    public async Task AwaitWithMetrics_MeterOnlyListener_RecordsSuccessMetrics()
    {
        var topic = $"orders-{Guid.NewGuid():N}";
        long messagesSent = 0;
        long bytesSent = 0;
        double operationDuration = -1;
        string? messagesTopic = null;
        string? bytesTopic = null;
        string? durationTopic = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName &&
                IsProducerSuccessMetric(instrument.Name))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var metricTopic = GetTag(tags, DekafDiagnostics.MessagingDestinationName);
            if (metricTopic != topic)
                return;

            if (instrument.Name == "messaging.client.sent.messages")
            {
                messagesSent += measurement;
                messagesTopic = metricTopic;
            }
            else if (instrument.Name == "messaging.client.sent.bytes")
            {
                bytesSent += measurement;
                bytesTopic = metricTopic;
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
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
        listener.Start();

        await Assert.That(ProducerMetricsEnabled()).IsTrue();

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
            Timestamp = DateTimeOffset.UtcNow,
            KeySize = 3,
            ValueSize = 5
        });

        var metadata = await InvokeAwaitWithMetrics(producer, completion, topic);

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(messagesSent).IsEqualTo(1);
        await Assert.That(bytesSent).IsEqualTo(8);
        await Assert.That(operationDuration).IsGreaterThanOrEqualTo(0);
        await Assert.That(messagesTopic).IsEqualTo(topic);
        await Assert.That(bytesTopic).IsEqualTo(topic);
        await Assert.That(durationTopic).IsEqualTo(topic);
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

    private static bool ProducerMetricsEnabled()
    {
        var method = typeof(KafkaProducer<string, string>).GetMethod(
            "ProducerMetricsEnabled",
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
    {
        foreach (var tag in tags)
        {
            if (tag.Key == key)
                return tag.Value?.ToString();
        }

        return null;
    }

    private static bool IsProducerSuccessMetric(string name) =>
        name is "messaging.client.sent.messages"
            or "messaging.client.sent.bytes"
            or "messaging.client.operation.duration";
}
