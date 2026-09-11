using System.Diagnostics;
using Dekaf.StressTests.Metrics;

namespace Dekaf.StressTests.Scenarios;

/// <summary>Real idle time, with no invented message completions or throughput denominator.</summary>
internal static class OutboxIdlePhase
{
    public static async Task<OutboxIdleSnapshot> RunAsync(TimeSpan duration, Func<OutboxOperationCounts> readOperations,
        OutboxWorkloadState state, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        var samples = new List<OutboxIdleSample>((int)Math.Ceiling(duration.TotalSeconds) + 2);
        var operationsStart = readOperations();
        var runtimeStart = RuntimeObservation.Capture();
        var started = Stopwatch.GetTimestamp();
        samples.Add(new OutboxIdleSample(0, runtimeStart, operationsStart));
        while (true)
        {
            var remaining = duration - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
                break;
            await Task.Delay(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            state.ThrowIfFailed();
            if (state.Reserved != 0 || state.Completed != 0)
                throw new InvalidOperationException("The idle outbox phase contains message activity.");
            samples.Add(new OutboxIdleSample(Stopwatch.GetElapsedTime(started).TotalSeconds, RuntimeObservation.Capture(), readOperations()));
        }
        var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
        var end = RuntimeObservation.Capture();
        var operations = readOperations();
        samples.Add(new OutboxIdleSample(elapsed, end, operations));
        var delta = operations.Since(operationsStart);
        if (delta.Probes == 0 || delta.Published != 0 || delta.Errors != 0)
            throw new InvalidOperationException("The idle phase did not exercise a healthy empty relay.");
        return new OutboxIdleSnapshot(duration.TotalSeconds, elapsed, runtimeStart, end, delta, samples);
    }
}

internal sealed record OutboxIdleSample(double ElapsedSeconds, RuntimeObservation Runtime, OutboxOperationCounts Operations);

internal sealed record OutboxIdleSnapshot(double RequestedSeconds, double ElapsedSeconds,
    RuntimeObservation RuntimeStart, RuntimeObservation RuntimeEnd, OutboxOperationCounts Operations,
    IReadOnlyList<OutboxIdleSample> Samples)
{
    public double CpuMillisecondsPerSecond => (RuntimeEnd.CpuSeconds - RuntimeStart.CpuSeconds) * 1000 / ElapsedSeconds;
    public double AllocatedBytesPerSecond => (RuntimeEnd.AllocatedBytes - RuntimeStart.AllocatedBytes) / ElapsedSeconds;
}

internal sealed record OutboxWorkloadSnapshot(OutboxIdleSnapshot IdleWarmup, OutboxIdleSnapshot Idle,
    double ActiveRequestedSeconds, double ActiveWorkloadSeconds, long CommittedMessages, long UniqueConsumedMessages,
    long DuplicatePublications, OutboxOperationCounts ActiveOperations)
{
    public bool HasCompleteDuration(int configuredMinutes, double activeElapsedSeconds) =>
        double.IsFinite(Idle.RequestedSeconds) && double.IsFinite(Idle.ElapsedSeconds)
        && double.IsFinite(ActiveRequestedSeconds) && double.IsFinite(activeElapsedSeconds)
        && double.IsFinite(ActiveWorkloadSeconds) && ActiveWorkloadSeconds >= ActiveRequestedSeconds
        && activeElapsedSeconds >= ActiveWorkloadSeconds
        && Idle.RequestedSeconds > 0 && ActiveRequestedSeconds > 0
        && Math.Abs(Idle.RequestedSeconds + ActiveRequestedSeconds - configuredMinutes * 60) < 0.001
        && Idle.ElapsedSeconds >= Idle.RequestedSeconds && activeElapsedSeconds >= ActiveRequestedSeconds;
}
