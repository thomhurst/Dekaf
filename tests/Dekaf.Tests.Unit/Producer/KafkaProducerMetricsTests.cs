using System.Diagnostics;
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
        long messagesSent = 0;
        long bytesSent = 0;
        double operationDuration = -1;
        string? messagesTopic = null;
        string? bytesTopic = null;
        string? durationTopic = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == "messaging.client.sent.messages")
            {
                messagesSent += measurement;
                messagesTopic = GetTag(tags, DekafDiagnostics.MessagingDestinationName);
            }
            else if (instrument.Name == "messaging.client.sent.bytes")
            {
                bytesSent += measurement;
                bytesTopic = GetTag(tags, DekafDiagnostics.MessagingDestinationName);
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == "messaging.client.operation.duration")
            {
                operationDuration = measurement;
                durationTopic = GetTag(tags, DekafDiagnostics.MessagingDestinationName);
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
            Topic = "orders",
            Partition = 1,
            Offset = 42,
            Timestamp = DateTimeOffset.UtcNow,
            KeySize = 3,
            ValueSize = 5
        });

        var metadata = await InvokeAwaitWithMetrics(producer, completion, "orders");

        await Assert.That(metadata.Topic).IsEqualTo("orders");
        await Assert.That(messagesSent).IsEqualTo(1);
        await Assert.That(bytesSent).IsEqualTo(8);
        await Assert.That(operationDuration).IsGreaterThanOrEqualTo(0);
        await Assert.That(messagesTopic).IsEqualTo("orders");
        await Assert.That(bytesTopic).IsEqualTo("orders");
        await Assert.That(durationTopic).IsEqualTo("orders");
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
}
