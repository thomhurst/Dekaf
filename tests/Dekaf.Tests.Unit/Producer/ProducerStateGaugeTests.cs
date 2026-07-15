using System.Diagnostics.Metrics;
using Dekaf.Diagnostics;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

[NotInParallel("MeterListener")]
public sealed class ProducerStateGaugeTests
{
    private const ulong BufferMemoryBytes = 64UL * 1024 * 1024;

    private sealed record GaugeSample(long Value, string? ClientId);

    private static MeterListener CreateListener(
        string instrumentName, List<GaugeSample> samples)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName &&
                instrument.Name == instrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            string? clientId = null;
            foreach (var tag in tags)
            {
                if (tag.Key == DekafDiagnostics.MessagingClientId)
                    clientId = tag.Value as string;
            }
            samples.Add(new GaugeSample(measurement, clientId));
        });
        listener.Start();
        return listener;
    }

    private static KafkaProducer<string, string> CreateProducer(string clientId)
        => new(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = clientId,
                BufferMemory = BufferMemoryBytes
            },
            Serializers.String,
            Serializers.String);

    [Test]
    public async Task BufferLimitGauge_ReportsConfiguredBufferMemoryWithClientIdTag()
    {
        var clientId = $"gauge-test-{Guid.NewGuid():N}";
        var samples = new List<GaugeSample>();
        using var listener = CreateListener("dekaf.producer.buffer.limit_bytes", samples);

        await using var producer = CreateProducer(clientId);
        listener.RecordObservableInstruments();

        var sample = samples.SingleOrDefault(s => s.ClientId == clientId);
        await Assert.That(sample).IsNotNull();
        await Assert.That(sample!.Value).IsEqualTo((long)BufferMemoryBytes);
    }

    [Test]
    public async Task BufferUsedGauge_ReportsZeroForIdleProducer()
    {
        var clientId = $"gauge-test-{Guid.NewGuid():N}";
        var samples = new List<GaugeSample>();
        using var listener = CreateListener("dekaf.producer.buffer.used_bytes", samples);

        await using var producer = CreateProducer(clientId);
        listener.RecordObservableInstruments();

        var sample = samples.SingleOrDefault(s => s.ClientId == clientId);
        await Assert.That(sample).IsNotNull();
        await Assert.That(sample!.Value).IsEqualTo(0);
    }

    [Test]
    public async Task DisposedProducer_StopsReportingGaugeMeasurements()
    {
        var clientId = $"gauge-test-{Guid.NewGuid():N}";
        var samples = new List<GaugeSample>();
        using var listener = CreateListener("dekaf.producer.buffer.limit_bytes", samples);

        var producer = CreateProducer(clientId);
        listener.RecordObservableInstruments();
        await Assert.That(samples.Any(s => s.ClientId == clientId)).IsTrue();

        await producer.DisposeAsync();
        samples.Clear();
        listener.RecordObservableInstruments();

        await Assert.That(samples.Any(s => s.ClientId == clientId)).IsFalse();
    }
}
