using System.Diagnostics.Metrics;
using Dekaf.Diagnostics;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

[NotInParallel("MeterListener")]
public sealed class PooledCompletionSourceMetricsTests
{
    [Test]
    public async Task ThrowingInlineContinuation_RecordsReleaseMetric()
    {
        long recordedExceptions = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == DekafDiagnostics.MeterName
                && instrument.Name == "dekaf.producer.inline_continuation.exceptions")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "dekaf.producer.inline_continuation.exceptions")
                Interlocked.Add(ref recordedExceptions, measurement);
        });
        listener.Start();

        await using var pool = new ValueTaskSourcePool<RecordMetadata>(maxPoolSize: 1);
        var source = pool.Rent();
        source.SetRunContinuationsAsynchronously(false);
        var awaiter = source.Task.GetAwaiter();
        awaiter.UnsafeOnCompleted(static () => throw new InvalidOperationException("expected"));

        var completed = PooledCompletionSource.TrySetResult(source, new RecordMetadata
        {
            Topic = "test-topic",
            Partition = 0,
            Offset = 0,
            Timestamp = DateTimeOffset.UnixEpoch
        });

        await Assert.That(completed).IsTrue();
        await Assert.That(Volatile.Read(ref recordedExceptions)).IsEqualTo(1);
        _ = awaiter.GetResult();
    }
}
