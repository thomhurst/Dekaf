using System.Diagnostics.Metrics;
using Dekaf.Diagnostics;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// End-to-end coverage for the per-broker producer state gauges: produces against
/// a real broker and asserts the broker-tagged instruments report sane values,
/// not just that the instrument names exist (unit tests cover the buffer gauges,
/// but the per-broker dictionaries only populate with a live connection).
/// </summary>
[Category("ProducerStateGauges")]
public sealed class ProducerStateGaugeIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private static readonly string[] BrokerLongGauges =
    [
        "dekaf.producer.broker.budget_bytes",
        "dekaf.producer.broker.unacked_bytes",
        "dekaf.producer.broker.min_rtt",
        "dekaf.producer.broker.max_delivery_rate",
        "dekaf.producer.broker.queue_latency_ewma",
        "dekaf.producer.broker.seal_to_ack_latency_ewma",
        "dekaf.producer.broker.admission_blocks",
        "dekaf.producer.broker.capacity_probe.successes",
        "dekaf.producer.broker.capacity_probe.failures",
        "dekaf.producer.broker.connections",
        "dekaf.producer.broker.in_flight_bytes",
        "dekaf.producer.broker.in_flight_requests",
    ];

    private sealed record BrokerSample(string Instrument, double Value, string? ClientId, object? BrokerId);

    [Test]
    public async Task BrokerGauges_AfterProducing_ReportBrokerTaggedMeasurements()
    {
        var clientId = $"state-gauge-{Guid.NewGuid():N}";
        var samples = new List<BrokerSample>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName &&
                instrument.Name.StartsWith("dekaf.producer.broker.", StringComparison.Ordinal))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            RecordSample(samples, instrument.Name, measurement, tags));
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            RecordSample(samples, instrument.Name, measurement, tags));
        listener.Start();

        var topic = await KafkaContainer.CreateTestTopicAsync();
        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId(clientId)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        try
        {
            for (var i = 0; i < 50; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"value-{i}"
                }, CancellationToken.None);
            }

            lock (samples)
            {
                samples.Clear();
            }
            listener.RecordObservableInstruments();

            List<BrokerSample> observed;
            lock (samples)
            {
                observed = samples.Where(sample => sample.ClientId == clientId).ToList();
            }

            foreach (var instrument in BrokerLongGauges)
            {
                var instrumentSamples = observed.Where(sample => sample.Instrument == instrument).ToList();
                await Assert.That(instrumentSamples).IsNotEmpty()
                    .Because($"{instrument} should report for a producer that has produced");
                foreach (var sample in instrumentSamples)
                {
                    await Assert.That(sample.BrokerId).IsNotNull()
                        .Because($"{instrument} measurements must carry the broker id tag");
                    await Assert.That(sample.Value).IsGreaterThanOrEqualTo(0)
                        .Because($"{instrument} must never report a negative value");
                }
            }

            // Value sanity beyond presence: at least one live connection, a positive
            // budget (cold start publishes the cap), and a derating factor in (0, 1].
            var connections = observed.Where(s => s.Instrument == "dekaf.producer.broker.connections").ToList();
            await Assert.That(connections.All(s => s.Value >= 1)).IsTrue()
                .Because("an actively producing sender has at least one connection");

            var budgets = observed.Where(s => s.Instrument == "dekaf.producer.broker.budget_bytes").ToList();
            await Assert.That(budgets.All(s => s.Value > 0)).IsTrue()
                .Because("the unacked-byte budget starts at its cap, never zero");

            var scales = observed.Where(s => s.Instrument == "dekaf.producer.broker.latency_budget_scale").ToList();
            await Assert.That(scales).IsNotEmpty();
            await Assert.That(scales.All(s => s.Value > 0 && s.Value <= 1.0)).IsTrue()
                .Because("the latency budget scale derates within (0, 1]");
        }
        finally
        {
            await producer.DisposeAsync();
        }

        // Disposal must unregister the source: no measurements for this client id afterwards.
        lock (samples)
        {
            samples.Clear();
        }
        listener.RecordObservableInstruments();
        List<BrokerSample> postDispose;
        lock (samples)
        {
            postDispose = samples.Where(sample => sample.ClientId == clientId).ToList();
        }
        await Assert.That(postDispose).IsEmpty()
            .Because("a disposed producer must stop reporting gauge measurements");
    }

    private static void RecordSample(
        List<BrokerSample> samples, string instrumentName, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        string? clientId = null;
        object? brokerId = null;
        foreach (var tag in tags)
        {
            if (tag.Key == DekafDiagnostics.MessagingClientId)
                clientId = tag.Value as string;
            else if (tag.Key == DekafDiagnostics.MessagingKafkaBrokerId)
                brokerId = tag.Value;
        }

        lock (samples)
        {
            samples.Add(new BrokerSample(instrumentName, value, clientId, brokerId));
        }
    }
}
