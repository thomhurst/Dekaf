using System.Diagnostics.Metrics;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

[NotInParallel("MeterListener")]
public sealed class ConsumerFetchBufferMetricsTests
{
    private sealed record Sample(string Name, long Value, string? ClientId);

    [Test]
    public async Task UsedAndFreeGauges_ReportCurrentReservations()
    {
        const string clientId = "fetch-buffer-metrics-test";
        var samples = new List<Sample>();
        using var listener = CreateListener(samples);
        await using var consumer = CreateConsumer(clientId);
        var pool = GetPool(consumer);
        using var reservation = await pool.ReserveAsync(40, CancellationToken.None);

        listener.RecordObservableInstruments();

        await Assert.That(samples).Contains(new Sample(
            "dekaf.consumer.fetch_buffer.used_bytes",
            40,
            clientId));
        await Assert.That(samples).Contains(new Sample(
            "dekaf.consumer.fetch_buffer.free_bytes",
            60,
            clientId));

    }

    [Test]
    public async Task DisposedConsumer_StopsReportingFetchBufferMeasurements()
    {
        const string clientId = "disposed-fetch-buffer-metrics-test";
        var samples = new List<Sample>();
        using var listener = CreateListener(samples);
        var consumer = CreateConsumer(clientId);

        listener.RecordObservableInstruments();
        await Assert.That(samples.Any(sample => sample.ClientId == clientId)).IsTrue();

        await consumer.DisposeAsync();
        samples.Clear();
        listener.RecordObservableInstruments();

        await Assert.That(samples.Any(sample => sample.ClientId == clientId)).IsFalse();
    }

    private static MeterListener CreateListener(List<Sample> samples)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName
                && instrument.Name is "dekaf.consumer.fetch_buffer.used_bytes"
                    or "dekaf.consumer.fetch_buffer.free_bytes")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? clientId = null;
            foreach (var tag in tags)
            {
                if (tag.Key == DekafDiagnostics.MessagingClientId)
                    clientId = tag.Value as string;
            }

            samples.Add(new Sample(instrument.Name, value, clientId));
        });
        listener.Start();
        return listener;
    }

    private static KafkaConsumer<string, string> CreateConsumer(string clientId) => new(
        new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = clientId,
            FetchMaxBytes = 50,
            FetchBufferMemoryBytes = 100
        },
        Serializers.String,
        Serializers.String);

    private static FetchBufferMemoryPool GetPool(KafkaConsumer<string, string> consumer) =>
        (FetchBufferMemoryPool)(typeof(KafkaConsumer<string, string>)
            .GetField("_fetchBufferMemoryPool", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(consumer)!);
}
