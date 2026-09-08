using Dekaf.StressTests.Metrics;

namespace Dekaf.StressTests.Scenarios;

internal static class ProducerWarmup
{
    internal const int DefaultSeconds = 180;
    internal const int CycleCount = 6;

    // Run the actual measured loop and observers on the same producer. Every cycle
    // drains outstanding deliveries so later cycles exercise returned pooled objects.
    internal static async Task<ProducerWarmupSnapshot> RunAsync(
        int seconds, Func<TimeSpan, CancellationToken, Task<ProducerWorkloadResult>> runCycle,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(seconds, 20);
        var duration = TimeSpan.FromSeconds((double)seconds / CycleCount);
        var samples = new List<ProducerWarmupSample>(seconds + CycleCount * 2);
        var completed = 0L;
        var workloadSeconds = 0.0;
        var elapsedSeconds = 0.0;

        for (var cycle = 0; cycle < CycleCount; cycle++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var run = await runCycle(duration, cancellationToken).ConfigureAwait(false);
            var throughput = run.Throughput;
            if (run.WorkloadSeconds < duration.TotalSeconds || throughput.TotalMessages <= 0
                || throughput.TotalErrors != 0 || throughput.TotalDeliveryErrors != 0)
            {
                throw new InvalidOperationException(
                    $"Warmup cycle {cycle + 1} incomplete: workload={run.WorkloadSeconds:F6}s, " +
                    $"required={duration.TotalSeconds:F6}s, accepted={throughput.TotalMessages}, " +
                    $"errors={throughput.TotalErrors}, deliveryErrors={throughput.TotalDeliveryErrors}.");
            }
            AddSample(0, 0, throughput.RuntimeStart, drained: false);
            foreach (var sample in throughput.IntervalSamples)
            {
                AddSample(sample.ElapsedSeconds, sample.AcceptedMessages, sample.Runtime, drained: false);
            }
            AddSample(throughput.ElapsedSeconds, throughput.TotalMessages, throughput.RuntimeEnd, drained: true);
            workloadSeconds += run.WorkloadSeconds;
            elapsedSeconds += throughput.ElapsedSeconds;
            completed += throughput.TotalMessages;

            void AddSample(double elapsed, long accepted, RuntimeObservation? runtime, bool drained)
            {
                samples.Add(new ProducerWarmupSample
                {
                    ElapsedSeconds = elapsedSeconds + elapsed,
                    WorkloadSeconds = workloadSeconds + Math.Min(elapsed, run.WorkloadSeconds),
                    AcceptedMessages = completed + accepted,
                    CompletedMessages = completed + (drained ? accepted : 0),
                    Runtime = runtime ?? throw new InvalidOperationException("Warmup runtime observation is missing.")
                });
            }
        }
        return new ProducerWarmupSnapshot
        {
            RequestedSeconds = seconds, WorkloadSeconds = workloadSeconds, ElapsedSeconds = elapsedSeconds,
            CompletedMessages = completed, DrainCycles = CycleCount, Samples = samples
        };
    }
}

internal sealed class ProducerWarmupSnapshot
{
    public required int RequestedSeconds { get; init; }
    public required double WorkloadSeconds { get; init; }
    public required double ElapsedSeconds { get; init; }
    public required long CompletedMessages { get; init; }
    public required int DrainCycles { get; init; }
    public required List<ProducerWarmupSample> Samples { get; init; }
}

internal sealed class ProducerWarmupSample
{
    public required double ElapsedSeconds { get; init; }
    public required double WorkloadSeconds { get; init; }
    public required long AcceptedMessages { get; init; }
    // Conservative completion count: advances only after a successful full drain.
    public required long CompletedMessages { get; init; }
    public required RuntimeObservation Runtime { get; init; }
}
