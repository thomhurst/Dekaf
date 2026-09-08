using Dekaf.Producer;
using Dekaf.StressTests.Diagnostics;
using Dekaf.StressTests.Metrics;
using Dekaf.StressTests.Scenarios;
using NSubstitute;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class ProducerWorkloadTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task StalledWorkload_ReportsClientAndScenario(bool confluent)
    {
        var client = confluent ? "Confluent" : "Dekaf";
        var directory = Path.Combine(Path.GetTempPath(), "dekaf-workload-" + Guid.NewGuid().ToString("N"));
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var watchdog = new ProgressWatchdog(directory,
            captureAfter: TimeSpan.FromMilliseconds(20), exitAfter: TimeSpan.FromMilliseconds(60),
            pollInterval: TimeSpan.FromMilliseconds(10), exitProcess: _ => exited.TrySetResult(),
            captureManagedStackReport: () => "controlled stalled producer");
        Task<ProducerWorkloadResult>? run = null;
        try
        {
            var options = new StressTestOptions
            {
                BootstrapServers = "unused:9092",
                Topic = "test",
                MessageSizeBytes = 1000,
                DurationMinutes = 1,
                ProgressWatchdog = watchdog
            };
            if (confluent)
            {
                var producer = Substitute.For<ConfluentKafka.IProducer<string, string>>();
                producer.ProduceAsync("test", Arg.Any<ConfluentKafka.Message<string, string>>(), Arg.Any<CancellationToken>())
                    .Returns(async call =>
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
                        return new ConfluentKafka.DeliveryResult<string, string>();
                    });
                run = ProducerWorkload.RunAsync(producer, options, client, "producer-async-idempotent", new ThroughputTracker(), new LatencyTracker(),
                    TimeSpan.FromMinutes(1), awaitDelivery: true, cancellation.Token);
            }
            else
            {
                var producer = Substitute.For<IKafkaProducer<string, string>>();
                producer.ProduceAsync("test", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(call => new ValueTask<RecordMetadata>(WaitForCancellationAsync(call.Arg<CancellationToken>())));
                run = ProducerWorkload.RunAsync(producer, options, client, "producer-async-idempotent", new ThroughputTracker(), new LatencyTracker(),
                    TimeSpan.FromMinutes(1), awaitDelivery: true, cancellation.Token);
            }
            await exited.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(watchdog.WaitForWorkerExit(TimeSpan.FromSeconds(5))).IsTrue();
            var artifacts = Directory.GetFiles(Path.Combine(directory, ProgressWatchdog.ArtifactsDirectoryName), "*-stacks.txt");
            await Assert.That(artifacts.Length).IsEqualTo(2);
            foreach (var artifact in artifacts)
            {
                var text = await File.ReadAllTextAsync(artifact);
                await Assert.That(text).Contains($"Client: {(confluent ? "Confluent" : "Dekaf")}");
                await Assert.That(text).Contains("Scenario: producer-async-idempotent");
            }
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                if (run is not null)
                    await Assert.That(async () => await run.WaitAsync(TimeSpan.FromSeconds(5))).Throws<OperationCanceledException>();
            }
            finally
            {
                watchdog.Dispose();
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static async Task<RecordMetadata> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return default;
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task AwaitedSend_StopsIngressBeforeFinalDeliveryAndRecordsOutcome(bool confluent, bool fails)
    {
        var client = confluent ? "Confluent" : "Dekaf";
        var directory = Path.Combine(Path.GetTempPath(), "dekaf-workload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var watchdog = new ProgressWatchdog(directory);
            var options = new StressTestOptions
            {
                BootstrapServers = "unused:9092",
                Topic = "test",
                DurationMinutes = 1,
                MessageSizeBytes = 1000,
                ProgressWatchdog = watchdog
            };
            var throughput = new ThroughputTracker();
            var latency = new LatencyTracker();
            var flushStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<ProducerWorkloadResult> run;
            Action completeDelivery;
            if (confluent)
            {
                var delivery = new TaskCompletionSource<ConfluentKafka.DeliveryResult<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
                var producer = Substitute.For<ConfluentKafka.IProducer<string, string>>();
                producer.ProduceAsync("test", Arg.Any<ConfluentKafka.Message<string, string>>(), Arg.Any<CancellationToken>())
                    .Returns(delivery.Task);
                producer.Flush(Arg.Any<TimeSpan>()).Returns(_ => { flushStarted.TrySetResult(); return 0; });
                completeDelivery = () =>
                {
                    if (fails) delivery.TrySetException(new ConfluentKafka.KafkaException(
                        new ConfluentKafka.Error(ConfluentKafka.ErrorCode.Local_MsgTimedOut)));
                    else delivery.TrySetResult(new ConfluentKafka.DeliveryResult<string, string>());
                };
                run = ProducerWorkload.RunAsync(producer, options, client, "producer-async-idempotent", throughput, latency,
                    TimeSpan.FromSeconds(1), awaitDelivery: true, CancellationToken.None);
            }
            else
            {
                var delivery = new TaskCompletionSource<RecordMetadata>(TaskCreationOptions.RunContinuationsAsynchronously);
                var producer = Substitute.For<IKafkaProducer<string, string>>();
                producer.ProduceAsync("test", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(_ => new ValueTask<RecordMetadata>(delivery.Task));
                producer.FlushAsync(Arg.Any<CancellationToken>()).Returns(_ =>
                {
                    flushStarted.TrySetResult();
                    return ValueTask.CompletedTask;
                });
                completeDelivery = () =>
                {
                    if (fails) delivery.TrySetException(new Dekaf.Errors.KafkaException("delivery failed"));
                    else delivery.TrySetResult(default);
                };
                run = ProducerWorkload.RunAsync(producer, options, client, "producer-async-idempotent", throughput, latency,
                    TimeSpan.FromSeconds(1), awaitDelivery: true, CancellationToken.None);
            }

            try
            {
                await flushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await Assert.That(run.IsCompleted).IsFalse();
                completeDelivery();
                var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
                await Assert.That(result.Throughput.TotalMessages).IsEqualTo(fails ? 0 : 1);
                await Assert.That(result.Throughput.TotalErrors).IsEqualTo(fails ? 1 : 0);
                await Assert.That(latency.GetSnapshot().Count).IsEqualTo(fails ? 0 : 1);
                await Assert.That(result.WorkloadSeconds).IsLessThan(result.Throughput.ElapsedSeconds);
            }
            finally
            {
                completeDelivery();
                await run.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnexpectedSendException_FaultsWorkload(bool confluent)
    {
        var client = confluent ? "Confluent" : "Dekaf";
        var directory = Path.Combine(Path.GetTempPath(), "dekaf-workload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var watchdog = new ProgressWatchdog(directory);
            var options = new StressTestOptions
            {
                BootstrapServers = "unused:9092",
                Topic = "test",
                DurationMinutes = 1,
                MessageSizeBytes = 1000,
                ProgressWatchdog = watchdog
            };
            var error = new InvalidOperationException("unexpected harness failure");
            Task<ProducerWorkloadResult> run;
            if (confluent)
            {
                var producer = Substitute.For<ConfluentKafka.IProducer<string, string>>();
                producer.ProduceAsync("test", Arg.Any<ConfluentKafka.Message<string, string>>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<ConfluentKafka.DeliveryResult<string, string>>(error));
                run = ProducerWorkload.RunAsync(producer, options, client, "producer-async-idempotent", new ThroughputTracker(), new LatencyTracker(),
                    TimeSpan.FromMinutes(1), awaitDelivery: true, CancellationToken.None);
            }
            else
            {
                var producer = Substitute.For<IKafkaProducer<string, string>>();
                producer.ProduceAsync("test", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(_ => ValueTask.FromException<RecordMetadata>(error));
                run = ProducerWorkload.RunAsync(producer, options, client, "producer-async-idempotent", new ThroughputTracker(), new LatencyTracker(),
                    TimeSpan.FromMinutes(1), awaitDelivery: true, CancellationToken.None);
            }
            await Assert.That(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)))
                .Throws<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}
