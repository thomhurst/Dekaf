using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class ProducerWarmupTests
{
    [Test]
    public async Task Warmup_ReusesReturnedObjectsAndExcludesDrainTimeFromWorkload()
    {
        var pool = new Stack<object>();
        var created = 0;
        var runs = 0;
        var snapshot = await ProducerWarmup.RunAsync(60, RunAsync, CancellationToken.None);
        await Assert.That(snapshot.WorkloadSeconds).IsEqualTo(60);
        await Assert.That(snapshot.ElapsedSeconds).IsEqualTo(660);
        await Assert.That(snapshot.CompletedMessages).IsEqualTo(60);
        await Assert.That(snapshot.DrainCycles).IsEqualTo(6);
        await Assert.That(runs).IsEqualTo(6);
        await Assert.That(created).IsEqualTo(10);
        await Assert.That(snapshot.Samples[^1].CompletedMessages).IsEqualTo(60);

        Task<ProducerWorkloadResult> RunAsync(TimeSpan duration, CancellationToken token)
        {
            var pending = new List<object>();
            for (var index = 0; index < 10; index++)
            {
                if (pool.Count == 0) { pool.Push(new object()); created++; }
                pending.Add(pool.Pop());
            }
            foreach (var item in pending) pool.Push(item);
            runs++;
            return Task.FromResult(Cycle(duration.TotalSeconds, drainSeconds: 100));
        }
    }

    [Test]
    public async Task Warmup_RejectsShortDurationBeforeRunning()
    {
        await Assert.That(async () => await ProducerWarmup.RunAsync(19,
            static (_, _) => throw new InvalidOperationException("must not run"), CancellationToken.None))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Warmup_RejectsShortOrFailedCycle()
    {
        foreach (var cycle in new[] { Cycle(1), Cycle(10, errors: 1) })
        {
            await Assert.That(async () => await ProducerWarmup.RunAsync(60,
                (_, _) => Task.FromResult(cycle), CancellationToken.None)).Throws<InvalidOperationException>();
        }
    }

    [Test]
    public async Task Warmup_CancellationDoesNotStartAnotherCycle()
    {
        using var cancellation = new CancellationTokenSource();
        var runs = 0;
        await Assert.That(async () => await ProducerWarmup.RunAsync(60,
            (duration, _) => { runs++; cancellation.Cancel(); return Task.FromResult(Cycle(duration.TotalSeconds)); },
            cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(runs).IsEqualTo(1);
    }

    [Test]
    public async Task DeliverySamples_DrainWaitsForLastCallbackWithoutRetainingTasks()
    {
        var latency = new LatencyTracker();
        latency.BeginDeliverySample();
        latency.BeginDeliverySample();
        var drained = latency.WaitForDeliverySamplesAsync();
        latency.CompleteDeliverySample();
        await Assert.That(drained.IsCompleted).IsFalse();
        latency.CompleteDeliverySample();
        await drained;
    }

    [Test]
    public async Task DeliverySamples_AlreadyCompletedDoesNotMissWakeup()
    {
        var latency = new LatencyTracker();
        latency.BeginDeliverySample();
        latency.CompleteDeliverySample();
        await latency.WaitForDeliverySamplesAsync();
    }

    private static ProducerWorkloadResult Cycle(double workloadSeconds, double drainSeconds = 0, long errors = 0) => new(
        workloadSeconds,
        new ThroughputSnapshot
        {
            TotalMessages = 10, TotalBytes = 100, TotalErrors = errors,
            ElapsedSeconds = workloadSeconds + drainSeconds, AverageMessagesPerSecond = 1,
            AverageMegabytesPerSecond = 1, MessagesPerSecondSamples = [],
            RuntimeStart = new RuntimeObservation(), RuntimeEnd = new RuntimeObservation(),
            IntervalSamples = []
        },
        new GcSnapshot { Gen0Collections = 0, Gen1Collections = 0, Gen2Collections = 0, AllocatedBytes = 0 });
}
