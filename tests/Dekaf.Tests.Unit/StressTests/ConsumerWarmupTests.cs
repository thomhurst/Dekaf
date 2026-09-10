using Dekaf.StressTests.Diagnostics;
using Dekaf.StressTests.Scenarios;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class ConsumerWarmupTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    public async Task IncompleteFailedOrCancelledReplay_DoesNotStartMeasurement(bool fail, bool cancel)
    {
        var directory = Path.Join(Path.GetTempPath(), "dekaf-consumer-warmup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var watchdog = new ProgressWatchdog(directory);
            var options = new StressTestOptions
            {
                BootstrapServers = "unused:9092", Topic = "test", DurationMinutes = 1,
                MessageSizeBytes = 1000, ProducerWarmupSeconds = 20, ProgressWatchdog = watchdog
            };
            var cycles = 0;
            using var cancellation = new CancellationTokenSource();
            async Task RunAsync() => await StressTestHelpers.RunConsumerAsync(options,
                new ConsumerBatchStressTest(), (throughput, _) =>
                {
                    cycles++;
                    throughput.RecordMessage(1000);
                    if (cancel) cancellation.Cancel();
                    return fail ? Task.FromException(new InvalidOperationException("replay failed")) : Task.CompletedTask;
                }, connectionsPerBroker: 3, cancellationToken: cancellation.Token);
            if (cancel)
                await Assert.That(RunAsync).Throws<OperationCanceledException>();
            else
                await Assert.That(RunAsync).Throws<InvalidOperationException>();
            await Assert.That(cycles).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
