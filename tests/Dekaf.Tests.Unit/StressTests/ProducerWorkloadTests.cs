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
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task AwaitedSend_StopsIngressBeforeFinalDeliveryAndRecordsOutcome(bool confluent, bool fails)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dekaf-workload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
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
            run = ProducerWorkload.RunAsync(producer, options, throughput, latency,
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
            run = ProducerWorkload.RunAsync(producer, options, throughput, latency,
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
            watchdog.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnexpectedSendException_FaultsWorkload(bool confluent)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dekaf-workload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var watchdog = new ProgressWatchdog(directory);
        var options = new StressTestOptions
        {
            BootstrapServers = "unused:9092", Topic = "test", DurationMinutes = 1,
            MessageSizeBytes = 1000, ProgressWatchdog = watchdog
        };
        var error = new InvalidOperationException("unexpected harness failure");
        Task<ProducerWorkloadResult> run;
        if (confluent)
        {
            var producer = Substitute.For<ConfluentKafka.IProducer<string, string>>();
            producer.ProduceAsync("test", Arg.Any<ConfluentKafka.Message<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<ConfluentKafka.DeliveryResult<string, string>>(error));
            run = ProducerWorkload.RunAsync(producer, options, new ThroughputTracker(), new LatencyTracker(),
                TimeSpan.FromMinutes(1), awaitDelivery: true, CancellationToken.None);
        }
        else
        {
            var producer = Substitute.For<IKafkaProducer<string, string>>();
            producer.ProduceAsync("test", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(_ => ValueTask.FromException<RecordMetadata>(error));
            run = ProducerWorkload.RunAsync(producer, options, new ThroughputTracker(), new LatencyTracker(),
                TimeSpan.FromMinutes(1), awaitDelivery: true, CancellationToken.None);
        }
        try
        {
            await Assert.That(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)))
                .Throws<InvalidOperationException>();
        }
        finally
        {
            watchdog.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }
}
