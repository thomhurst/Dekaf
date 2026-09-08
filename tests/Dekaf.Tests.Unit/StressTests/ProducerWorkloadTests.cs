using System.Text.Json;
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
    public async Task FinalFireAppend_IsDrainedOrReportsDeadlineBeforeMeasurementStops(bool drainExpires)
    {
        var directory = Path.Join(Path.GetTempPath(), "dekaf-workload-" + Guid.NewGuid().ToString("N"));
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
            var admission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstFlush = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var finalFlush = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var finalDelivery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var throughput = new ThroughputTracker();
            var producer = Substitute.For<IKafkaProducer<string, string>>();
            producer.ProduceAsync("test", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(default(RecordMetadata)));
            producer.FireAsync("test", Arg.Any<string>(), Arg.Any<string>())
                .Returns(_ => new ValueTask(admission.Task));
            var flushes = 0;
            producer.FlushAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                if (Interlocked.Increment(ref flushes) == 1)
                {
                    firstFlush.TrySetResult();
                    return ValueTask.CompletedTask;
                }
                finalFlush.TrySetResult();
                return new ValueTask(finalDelivery.Task);
            });
            var run = ProducerWorkload.RunAsync(producer, options, "Dekaf", "producer", throughput, new LatencyTracker(),
                TimeSpan.FromMilliseconds(100), awaitDelivery: false, CancellationToken.None,
                drainTimeout: TimeSpan.FromSeconds(drainExpires ? 1 : 5));
            try
            {
                await firstFlush.Task.WaitAsync(TimeSpan.FromSeconds(5));
                admission.TrySetResult();
                await finalFlush.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await Assert.That(throughput.MessageCount).IsEqualTo(2);
                await Assert.That(run.IsCompleted).IsFalse();
                if (!drainExpires) finalDelivery.TrySetResult();
                var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
                await Assert.That(flushes).IsEqualTo(2);
                await Assert.That(result.Throughput.ErrorSamples.Any(sample => sample.ExceptionType == "DeliveryDrainTimeout")).IsEqualTo(drainExpires);
                if (!drainExpires) await Assert.That(result.Throughput.TotalErrors).IsEqualTo(0);
                await Assert.That(result.WorkloadSeconds).IsLessThan(result.Throughput.ElapsedSeconds);
            }
            finally
            {
                admission.TrySetResult();
                finalDelivery.TrySetResult();
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
    public async Task DrainTimeout_ReturnsSerializableFailureWithRuntimeSamples(bool confluent)
    {
        var directory = Path.Join(Path.GetTempPath(), "dekaf-workload-" + Guid.NewGuid().ToString("N"));
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
            Task<ProducerWorkloadResult> run;
            if (confluent)
            {
                var deliveryToken = CancellationToken.None;
                var producer = Substitute.For<ConfluentKafka.IProducer<string, string>>();
                producer.ProduceAsync("test", Arg.Any<ConfluentKafka.Message<string, string>>(), Arg.Any<CancellationToken>())
                    .Returns(async call =>
                    {
                        deliveryToken = call.Arg<CancellationToken>();
                        await Task.Delay(Timeout.InfiniteTimeSpan, deliveryToken);
                        return new ConfluentKafka.DeliveryResult<string, string>();
                    });
                producer.Flush(Arg.Any<TimeSpan>()).Returns(_ =>
                {
                    throughput.TakeSample();
                    if (!deliveryToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("The delivery deadline was not armed");
                    return 1;
                });
                run = ProducerWorkload.RunAsync(producer, options, "Confluent", "producer-async", throughput, new LatencyTracker(),
                    TimeSpan.FromMilliseconds(100), awaitDelivery: true, CancellationToken.None, drainTimeout: TimeSpan.FromMilliseconds(100));
            }
            else
            {
                var producer = Substitute.For<IKafkaProducer<string, string>>();
                producer.ProduceAsync("test", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(call => new ValueTask<RecordMetadata>(WaitForCancellationAsync(call.Arg<CancellationToken>())));
                producer.FlushAsync(Arg.Any<CancellationToken>()).Returns(call =>
                {
                    throughput.TakeSample();
                    return new ValueTask(Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>()));
                });
                run = ProducerWorkload.RunAsync(producer, options, "Dekaf", "producer-async", throughput, new LatencyTracker(),
                    TimeSpan.FromMilliseconds(100), awaitDelivery: true, CancellationToken.None, drainTimeout: TimeSpan.FromMilliseconds(100));
            }
            var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.That(result.Throughput.TotalErrors).IsGreaterThan(0);
            await Assert.That(result.Throughput.ErrorSamples.Any(sample => sample.ExceptionType == "DeliveryDrainTimeout")).IsTrue();
            if (confluent)
                await Assert.That(result.Throughput.ErrorSamples.Any(sample => sample.ExceptionType == "FlushTimeout")).IsTrue();
            var json = JsonSerializer.Serialize(result.Throughput, JsonSerializerOptions.Web);
            using var document = JsonDocument.Parse(json);
            await Assert.That(document.RootElement.GetProperty("runtimeEnd").ValueKind).IsEqualTo(JsonValueKind.Object);
            await Assert.That(document.RootElement.GetProperty("intervalSamples").GetArrayLength()).IsGreaterThan(0);
            await Assert.That(document.RootElement.GetProperty("totalErrors").GetInt64()).IsGreaterThan(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task StalledWorkload_ReportsClientAndScenario(bool confluent)
    {
        var client = confluent ? "Confluent" : "Dekaf";
        var directory = Path.Join(Path.GetTempPath(), "dekaf-workload-" + Guid.NewGuid().ToString("N"));
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        try
        {
            using var watchdog = new ProgressWatchdog(directory,
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
                var artifacts = Directory.GetFiles(Path.Join(directory, ProgressWatchdog.ArtifactsDirectoryName), "*-stacks.txt");
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
                if (run is not null)
                    await Assert.That(async () => await run.WaitAsync(TimeSpan.FromSeconds(5))).Throws<OperationCanceledException>();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<RecordMetadata> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return default;
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FireAndForget_StopsIngressAndWaitsForSampledDeliveryAfterFlush(bool confluent)
    {
        var directory = Path.Join(Path.GetTempPath(), "dekaf-workload-" + Guid.NewGuid().ToString("N"));
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
                Action<ConfluentKafka.DeliveryReport<string, string>>? deliveryHandler = null;
                var producer = Substitute.For<ConfluentKafka.IProducer<string, string>>();
                producer.When(p => p.Produce("test", Arg.Any<ConfluentKafka.Message<string, string>>(),
                    Arg.Any<Action<ConfluentKafka.DeliveryReport<string, string>>>())).Do(call =>
                {
                    var handler = call.Arg<Action<ConfluentKafka.DeliveryReport<string, string>>>();
                    if (handler is not null)
                    {
                        deliveryHandler = handler;
                        return;
                    }
                    // Keep unsampled sends under backpressure until ingress expires.
                    throw new ConfluentKafka.ProduceException<string, string>(
                        new ConfluentKafka.Error(ConfluentKafka.ErrorCode.Local_QueueFull),
                        new ConfluentKafka.DeliveryResult<string, string>());
                });
                producer.Flush(Arg.Any<TimeSpan>()).Returns(_ => { flushStarted.TrySetResult(); return 0; });
                completeDelivery = () => Interlocked.Exchange(ref deliveryHandler, null)?.Invoke(
                    new ConfluentKafka.DeliveryReport<string, string>
                    {
                        Error = new ConfluentKafka.Error(ConfluentKafka.ErrorCode.NoError)
                    });
                run = ProducerWorkload.RunAsync(producer, options, "Confluent", "producer", throughput, latency,
                    TimeSpan.FromMilliseconds(100), awaitDelivery: false, CancellationToken.None);
            }
            else
            {
                var sampledDelivery = new TaskCompletionSource<RecordMetadata>(TaskCreationOptions.RunContinuationsAsynchronously);
                var bufferedSend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var producer = Substitute.For<IKafkaProducer<string, string>>();
                producer.ProduceAsync("test", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(_ => new ValueTask<RecordMetadata>(sampledDelivery.Task));
                producer.FireAsync("test", Arg.Any<string>(), Arg.Any<string>())
                    .Returns(_ => new ValueTask(bufferedSend.Task));
                producer.FlushAsync(Arg.Any<CancellationToken>()).Returns(_ =>
                {
                    bufferedSend.TrySetResult();
                    flushStarted.TrySetResult();
                    return ValueTask.CompletedTask;
                });
                completeDelivery = () => sampledDelivery.TrySetResult(default);
                run = ProducerWorkload.RunAsync(producer, options, "Dekaf", "producer", throughput, latency,
                    TimeSpan.FromMilliseconds(100), awaitDelivery: false, CancellationToken.None);
            }
            try
            {
                await flushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await Assert.That(run.IsCompleted).IsFalse();
                completeDelivery();
                var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
                await Assert.That(result.Throughput.TotalMessages).IsEqualTo(confluent ? 1 : 2);
                await Assert.That(result.Throughput.TotalErrors).IsEqualTo(0);
                await Assert.That(result.Throughput.TotalDeliveryErrors).IsEqualTo(0);
                await Assert.That(latency.GetSnapshot().Count).IsEqualTo(1);
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
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task AwaitedSend_StopsIngressBeforeFinalDeliveryAndRecordsOutcome(bool confluent, bool fails)
    {
        var client = confluent ? "Confluent" : "Dekaf";
        var directory = Path.Join(Path.GetTempPath(), "dekaf-workload-" + Guid.NewGuid().ToString("N"));
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
        var directory = Path.Join(Path.GetTempPath(), "dekaf-workload-" + Guid.NewGuid().ToString("N"));
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
