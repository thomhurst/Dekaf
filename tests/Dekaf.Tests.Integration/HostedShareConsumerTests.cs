using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Dekaf.Admin;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.Hosting;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public class HostedShareConsumerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task RepeatedKeyedWorkers_ProcessAndAcknowledgeAllRecords()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"hosted-share-{Guid.NewGuid():N}";
        await ConfigureEarliestAsync(group);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 1);
        var state = new WorkerState(topic, 20);
        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services.AddSingleton(state);
        hostBuilder.Services.AddDekaf(builder => builder
            .AddShareConsumerService<Worker, string, string>("first", c => c
                .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
                .WithAcknowledgementCommitCallback(state.ObserveAcknowledgements))
            .AddShareConsumerService<Worker, string, string>("second", c => c
                .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
                .WithAcknowledgementCommitCallback(state.ObserveAcknowledgements)));
        using var host = hostBuilder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await host.StartAsync(timeout.Token);
        var workers = host.Services.GetServices<IHostedService>().OfType<Worker>().ToArray();
        await state.FirstWorkerEntered.Task.WaitAsync(timeout.Token);
        for (var index = 1; index < 20; index++)
            await producer.ProduceAsync(topic, $"key-{index}", $"value-{index}", timeout.Token);
        try
        {
            await state.Completed.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException exception)
        {
            throw new InvalidOperationException($"Hosted workers processed {state.Values.Count}/20 unique values: " +
                string.Join(",", state.Values.Keys) + "; tasks: " +
                string.Join(",", workers.Select(worker => $"{worker.Processed}:{worker.ExecuteTask!.Status}:{worker.ExecuteTask.Exception}")), exception);
        }
        await host.StopAsync(timeout.Token);
        await Assert.That(workers.Length).IsEqualTo(2);
        await Assert.That(ReferenceEquals(workers[0].Consumer, workers[1].Consumer)).IsFalse();
        await Assert.That(workers.All(worker => worker.Processed > 0)).IsTrue();
        await Assert.That(workers.Sum(worker => worker.Processed)).IsGreaterThanOrEqualTo(20);
        await Assert.That(state.Values.Count).IsEqualTo(20);
        await Task.WhenAll(workers.Select(worker => worker.ExecuteTask!)).WaitAsync(timeout.Token);
        await Assert.That(state.AcknowledgementFailures).IsEmpty();
        await Assert.That(state.Acknowledged.Count).IsEqualTo(20);

        // A sentinel proves a replacement consumer has joined and fetched beyond accepted work.
        await producer.ProduceAsync(topic, "sentinel", "sentinel", timeout.Token);
        await using var replacement = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).BuildAsync(timeout.Token);
        replacement.Subscribe(topic);
        await foreach (var record in replacement.PollAsync(timeout.Token))
        {
            replacement.Acknowledge(record);
            await Assert.That(record.Value).IsEqualTo("sentinel");
            break;
        }
        await replacement.CommitAsync(timeout.Token);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InterruptedOrFailedProcessing_RedeliversUnfinishedAcquisitions(bool throwProcessingError)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"hosted-share-release-{Guid.NewGuid():N}";
        await ConfigureEarliestAsync(group);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 3);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var consumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).Build();
        await using (var interrupted = new InterruptedWorker(consumer, topic, throwProcessingError))
        {
            await interrupted.StartAsync(timeout.Token);
            await interrupted.Entered.Task.WaitAsync(timeout.Token);
            if (throwProcessingError)
                await Assert.That(async () => await interrupted.ExecuteTask!.WaitAsync(timeout.Token)).Throws<ProcessingException>();
            await interrupted.StopAsync(timeout.Token);
        }
        await using var replacement = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).BuildAsync(timeout.Token);
        replacement.Subscribe(topic);
        var values = new HashSet<string>();
        var redelivered = false;
        await foreach (var record in replacement.PollAsync(timeout.Token))
        {
            values.Add(record.Value);
            redelivered |= record.DeliveryCount > 1;
            replacement.Acknowledge(record);
            if (values.Count == 3) break;
        }
        await replacement.CommitAsync(timeout.Token);
        await Assert.That(values).IsEquivalentTo(["value-0", "value-1", "value-2"]);
        await Assert.That(redelivered).IsTrue();
    }

    [Test]
    public async Task RenewedProcessing_DoesNotReplayCompletedRecordBeforeNextFetch()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"hosted-share-renewal-{Guid.NewGuid():N}";
        await ConfigureEarliestAsync(group);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await producer.ProduceAsync(topic, "key", "slow");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var renewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithAcknowledgementCommitCallback(results =>
            {
                // No terminal acknowledgement exists yet: the first submitted offset is Renew.
                foreach (ref readonly var result in results)
                    if (result.Succeeded && result.Offsets.Length > 0) renewed.TrySetResult();
            }).Build();
        await using var service = new RenewedWorker(consumer, topic, renewed.Task);
        await service.StartAsync(timeout.Token);
        await renewed.Task.WaitAsync(timeout.Token);
        await producer.ProduceAsync(topic, "sentinel", "sentinel", timeout.Token);
        await service.SentinelProcessed.Task.WaitAsync(timeout.Token);
        await service.StopAsync(timeout.Token);
        await service.ExecuteTask!.WaitAsync(timeout.Token);

        await Assert.That(service.SlowCalls).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DurableDeadLetterRouting_PreservesOriginalBytes(bool spoofRetryControls)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await KafkaContainer.CreateTopicAsync(topic + ".DLQ");
        var group = $"hosted-share-dlq-{Guid.NewGuid():N}";
        await ConfigureEarliestAsync(group);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await producer.ProduceAsync(new Dekaf.Producer.ProducerMessage<string, string>
        {
            Topic = topic, Key = "original-key", Value = "original-value",
            Headers = spoofRetryControls ? new Dekaf.Serialization.Headers()
                .Add(RetryTopicHeaders.SourceTopicKey, "spoofed")
                .Add(RetryTopicHeaders.FailureCountKey, "2147483647") : null
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var consumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).Build();
        await using var service = new DeadLetterWorker(consumer, topic, KafkaContainer.BootstrapServers);
        await service.StartAsync(timeout.Token);
        await using var deadLetters = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId($"dlq-reader-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Dekaf.Consumer.AutoOffsetReset.Earliest).BuildAsync(timeout.Token);
        deadLetters.Subscribe(topic + ".DLQ");
        await foreach (var record in deadLetters.ConsumeAsync(timeout.Token))
        {
            await Assert.That(record.Key).IsEqualTo("original-key");
            await Assert.That(record.Value).IsEqualTo("original-value");
            await Assert.That(record.Headers.First(header => header.Key == DeadLetterHeaders.SourceTopicKey).GetValueAsString())
                .IsEqualTo(topic);
            break;
        }
        await service.StopAsync(timeout.Token);
        await service.ExecuteTask!.WaitAsync(timeout.Token);
    }

    private async Task ConfigureEarliestAsync(string group)
    {
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        });
    }

    private sealed class WorkerState(string topic, int count)
    {
        public string Topic { get; } = topic;
        public readonly ConcurrentDictionary<string, byte> Values = new();
        public readonly ConcurrentDictionary<TopicPartitionOffset, byte> Acknowledged = new();
        public readonly ConcurrentQueue<Exception> AcknowledgementFailures = new();
        public void ObserveAcknowledgements(ReadOnlySpan<ShareAcknowledgementCommitResult> results)
        {
            foreach (ref readonly var result in results)
            {
                if (result.Exception is { } failure)
                    AcknowledgementFailures.Enqueue(failure);
                else
                    foreach (var offset in result.Offsets)
                        Acknowledged.TryAdd(new TopicPartitionOffset(
                            result.TopicPartition.Topic, result.TopicPartition.Partition, offset), 0);
            }
        }
        public readonly TaskCompletionSource FirstWorkerEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _bothWorkersEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _enteredWorkers;
        public async ValueTask EnterWorkerAsync(CancellationToken token)
        {
            if (Interlocked.Increment(ref _enteredWorkers) == 1) FirstWorkerEntered.TrySetResult();
            else _bothWorkersEntered.TrySetResult();
            await _bothWorkersEntered.Task.WaitAsync(token);
        }
        public readonly TaskCompletionSource Completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Process(string value)
        {
            Values.TryAdd(value, 0);
            if (Values.Count == count) Completed.TrySetResult();
        }
    }

    private sealed class Worker(IKafkaShareConsumer<string, string> consumer, WorkerState state)
        : KafkaShareConsumerService<string, string>(consumer, GlobalTestSetup.GetLoggerFactory().CreateLogger("HostedShareConsumer"))
    {
        public IKafkaShareConsumer<string, string> Consumer { get; } = consumer;
        public int Processed { get; private set; }
        protected override IEnumerable<string> Topics => [state.Topic];
        protected override async ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken)
        {
            if (++Processed == 1) await state.EnterWorkerAsync(cancellationToken);
            state.Process(record.Value);
        }
    }

    private sealed class InterruptedWorker(IKafkaShareConsumer<string, string> consumer, string topic, bool throwProcessingError)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance,
            serviceOptions: new KafkaShareConsumerServiceOptions { DrainOnShutdown = false })
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override IEnumerable<string> Topics => [topic];
        protected override async ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            if (throwProcessingError) throw new ProcessingException();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class RenewedWorker(IKafkaShareConsumer<string, string> consumer, string topic, Task renewed)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance,
            serviceOptions: new KafkaShareConsumerServiceOptions { RenewalInterval = TimeSpan.FromMilliseconds(100) })
    {
        public readonly TaskCompletionSource SentinelProcessed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int SlowCalls { get; private set; }
        protected override IEnumerable<string> Topics => [topic];
        protected override async ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken)
        {
            if (record.Value == "slow")
            {
                SlowCalls++;
                await renewed.WaitAsync(cancellationToken);
            }
            else SentinelProcessed.TrySetResult();
        }
    }

    private sealed class DeadLetterWorker(IKafkaShareConsumer<string, string> consumer, string topic, string servers)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance,
            new Dekaf.Consumer.DeadLetter.DeadLetterOptions { BootstrapServers = servers })
    {
        protected override IEnumerable<string> Topics => [topic];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken cancellationToken)
            => ValueTask.FromException(new ProcessingException());
    }

    private sealed class ProcessingException : Exception;
}
