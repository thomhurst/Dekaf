using System.Diagnostics.Metrics;
using Dekaf.Diagnostics;

namespace Dekaf.StressTests.Metrics;

/// <summary>
/// Counts Dekaf delivery failures into a <see cref="ThroughputTracker"/> by listening to
/// the producer's <c>messaging.client.sent.errors</c> metric. Fire-and-forget appends have
/// no awaiter or callback, so this metric is the only signal that an accepted message was
/// never delivered — without it a produce run can lose messages with a zero error count.
/// Cost: free on the FireAsync hot path (failures are counted once per failed batch), but
/// enabling the instrument routes awaited ProduceAsync calls through the producer's
/// metrics-emitting await path — cheap next to the broker round-trip they already await,
/// and in the fire-and-forget scenarios only the 1-in-1000 sampled sends take that path.
/// </summary>
internal sealed class DekafDeliveryErrorListener : IDisposable
{
    private readonly MeterListener _listener;

    public DekafDeliveryErrorListener(ThroughputTracker throughput, string? topic = null)
    {
        // Resolved before the listener starts: touching DekafMetrics inside the
        // InstrumentPublished callback can re-enter its static initializer (the callback
        // fires while the instruments are being constructed) and poison the type.
        var produceErrorsName = DekafMetrics.ProduceErrors.Name;

        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == DekafDiagnostics.MeterName &&
                    instrument.Name == produceErrorsName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        _listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            if (topic is null || HasTopic(tags, topic))
                throughput.RecordDeliveryErrors(measurement);
        });
        _listener.Start();
    }

    private static bool HasTopic(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        string expectedTopic)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == DekafDiagnostics.MessagingDestinationName
                && tag.Value is string topic
                && topic == expectedTopic)
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose() => _listener.Dispose();
}
