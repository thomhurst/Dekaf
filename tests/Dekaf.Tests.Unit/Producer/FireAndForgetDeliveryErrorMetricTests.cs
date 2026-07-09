using System.Diagnostics.Metrics;
using Dekaf.Diagnostics;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Fire-and-forget records have no completion source, so a failed batch used to vanish
/// without any signal: no exception, no callback, and no metric. These tests pin the fix —
/// ReadyBatch.Fail must count records without completion sources on
/// messaging.client.sent.errors so fire-and-forget delivery failures are observable.
/// </summary>
public sealed class FireAndForgetDeliveryErrorMetricTests
{
    [Test]
    [NotInParallel("MeterListener")]
    public async Task Fail_FireAndForgetRecords_CountsProduceErrors()
    {
        var topic = $"fnf-errors-{Guid.NewGuid():N}";
        long produceErrors = 0;

        using var listener = CreateProduceErrorListener(topic, v => produceErrors += v);

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);

        try
        {
            for (var i = 0; i < 3; i++)
            {
                await AppendFireAndForgetAsync(accumulator, topic);
            }

            var readyBatch = AccumulatorTestHelpers.CompleteCurrentBatch(accumulator, topic);
            readyBatch.Fail(new InvalidOperationException("Simulated delivery failure"));

            await Assert.That(produceErrors).IsEqualTo(3);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [NotInParallel("MeterListener")]
    public async Task Fail_MixedBatch_CountsOnlyRecordsWithoutCompletionSources()
    {
        var topic = $"fnf-errors-{Guid.NewGuid():N}";
        long produceErrors = 0;

        using var listener = CreateProduceErrorListener(topic, v => produceErrors += v);

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            // Two awaited records (completion sources observe the failure themselves and
            // the ProduceAsync awaiter increments the metric) plus one fire-and-forget
            // record that only ReadyBatch.Fail can account for.
            var completions = new[] { pool.Rent(), pool.Rent() };
            foreach (var completion in completions)
            {
                var appended = accumulator.TryAppendWithCompletion(
                    topic,
                    partition: 0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    new PooledMemory(null, 0, isNull: true),
                    new PooledMemory(null, 0, isNull: true),
                    null,
                    0,
                    completion);
                await Assert.That(appended).IsTrue();
            }

            await AppendFireAndForgetAsync(accumulator, topic);

            var readyBatch = AccumulatorTestHelpers.CompleteCurrentBatch(accumulator, topic);
            readyBatch.Fail(new InvalidOperationException("Simulated delivery failure"));

            await Assert.That(produceErrors).IsEqualTo(1);

            // Observe the completion source exceptions so they don't surface elsewhere.
            foreach (var completion in completions)
            {
                await Assert.That(async () => await completion.Task).Throws<InvalidOperationException>();
            }
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    private static ProducerOptions CreateTestOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        ClientId = "fnf-delivery-error-metric-test",
        LingerMs = 60_000
    };

    private static MeterListener CreateProduceErrorListener(
        string topic,
        Action<long> record)
    {
        // Resolved before the listener starts: touching DekafMetrics inside the
        // InstrumentPublished callback can re-enter its static initializer and
        // poison the type for the whole test process.
        var produceErrorsName = DekafMetrics.ProduceErrors.Name;

        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == DekafDiagnostics.MeterName &&
                    instrument.Name == produceErrorsName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            if (AccumulatorTestHelpers.GetTag(tags, DekafDiagnostics.MessagingDestinationName) == topic)
            {
                record(measurement);
            }
        });

        listener.Start();
        return listener;
    }

    private static async Task AppendFireAndForgetAsync(RecordAccumulator accumulator, string topic)
    {
        var appended = await accumulator.AppendAsync(
            topic,
            partition: 0,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            new PooledMemory(null, 0, isNull: true),
            new PooledMemory(null, 0, isNull: true),
            null,
            0,
            completionSource: null,
            callback: null,
            CancellationToken.None);
        await Assert.That(appended).IsTrue();
    }
}
