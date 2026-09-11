using Dekaf.StressTests.Diagnostics;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;

namespace Dekaf.StressTests.Tests;

public class ConsumerObservationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DeadlineDuringPass_ObservesDrainAndRetainsFailure(bool failAfterDrain)
    {
        using var watchdog = new ProgressWatchdog(Path.GetTempPath());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var options = new StressTestOptions
        {
            BootstrapServers = "unused", Topic = "observation-test", DurationMinutes = 0,
            MessageSizeBytes = 8, ProgressWatchdog = watchdog
        };
        var tracker = new ThroughputTracker();
        var cycle = await StressTestHelpers.RunConsumerCycleAsync(options, new KeyedConsumerStressTest(),
            async (throughput, ingress) =>
            {
                // Expire ingress before recording the final pass's completion. The
                // observer must sample this completion before the pass may return.
                var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = ingress.Register(() => expired.TrySetResult());
                await expired.Task.WaitAsync(timeout.Token);
                throughput.RecordMessage(8);
                while (throughput.GetSnapshot().IntervalSamples.Count == 0)
                    await Task.Delay(10, timeout.Token);
                if (failAfterDrain) throw new InvalidOperationException("drain failure");
            }, 1, null, TimeSpan.Zero, tracker, timeout.Token);

        await Assert.That(cycle.Result.Throughput.TotalMessages).IsEqualTo(1L);
        await Assert.That(cycle.Result.Throughput.IntervalSamples[^1].AcceptedMessages).IsEqualTo(1L);
        await Assert.That(cycle.Result.Throughput.TotalErrors).IsEqualTo(failAfterDrain ? 1L : 0L);
        if (failAfterDrain)
            await Assert.That(cycle.Result.Throughput.ErrorSamples.Single().Message).IsEqualTo("drain failure");
    }
}
