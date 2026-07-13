using System.Diagnostics.Metrics;
using System.Reflection;
using System.Threading.Tasks.Sources;
using Dekaf.Diagnostics;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

[NotInParallel("MeterListener")]
public sealed class PooledCompletionSourceMetricsTests
{
    [Test]
    [Arguments(CompletionKind.Result)]
    [Arguments(CompletionKind.Exception)]
    [Arguments(CompletionKind.Canceled)]
    public async Task AsynchronousCompletionSourceFault_IsRethrown(CompletionKind completionKind)
    {
        long recordedExceptions = 0;
        using var listener = CreateInlineContinuationExceptionListener(
            measurement => Interlocked.Add(ref recordedExceptions, measurement));

        await using var pool = new ValueTaskSourcePool<RecordMetadata>(maxPoolSize: 1);
        var source = pool.Rent();
        CompleteCoreWithoutUpdatingSourceState(source);

        await Assert.That(() => Complete(source, completionKind)).Throws<InvalidOperationException>();
        await Assert.That(Volatile.Read(ref recordedExceptions)).IsEqualTo(0);
    }

    [Test]
    public async Task ThrowingInlineContinuation_RecordsReleaseMetric()
    {
        long recordedExceptions = 0;
        using var listener = CreateInlineContinuationExceptionListener(
            measurement => Interlocked.Add(ref recordedExceptions, measurement));

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

    private static MeterListener CreateInlineContinuationExceptionListener(Action<long> onMeasurement)
    {
        var listener = new MeterListener();
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
                onMeasurement(measurement);
        });
        listener.Start();
        return listener;
    }

    private static void CompleteCoreWithoutUpdatingSourceState(PooledValueTaskSource<RecordMetadata> source)
    {
        var coreField = typeof(PooledValueTaskSource<RecordMetadata>).GetField(
            "_core",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var core = (ManualResetValueTaskSourceCore<RecordMetadata>)coreField.GetValue(source)!;
        core.SetResult(default!);
        coreField.SetValue(source, core);
    }

    private static bool Complete(
        PooledValueTaskSource<RecordMetadata> source,
        CompletionKind completionKind) => completionKind switch
    {
        CompletionKind.Result => PooledCompletionSource.TrySetResult(source, new RecordMetadata
        {
            Topic = "test-topic",
            Partition = 0,
            Offset = 0,
            Timestamp = DateTimeOffset.UnixEpoch
        }),
        CompletionKind.Exception => PooledCompletionSource.TrySetException(
            source,
            new InvalidOperationException("expected source failure")),
        CompletionKind.Canceled => PooledCompletionSource.TrySetCanceled(source, CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(completionKind))
    };

    public enum CompletionKind
    {
        Result,
        Exception,
        Canceled
    }
}
