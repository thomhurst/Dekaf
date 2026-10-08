using System.Collections.Concurrent;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Extensions.Hosting;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekaf.Tests.Integration;

/// <summary>
/// The default <see cref="MessageFailureDisposition.Redeliver"/> redelivers a failed record in
/// process instead of faulting the service and, with the default host behavior, the host.
/// </summary>
[Category("Messaging")]
public sealed class HostedRedeliveryTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Timeout(90_000)]
    public async Task Consumer_DefaultDisposition_ReprocessesFailedRecordAndCommitsAfterSuccess(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"hosted-redeliver-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken);
        for (var index = 0; index < 3; index++)
            await producer.ProduceAsync(topic, "key", $"value-{index}", cancellationToken);

        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync(cancellationToken);
        var service = new RedeliveringConsumerService(consumer, topic, failuresBeforeSuccess: 2, expected: 3);
        await service.StartAsync(cancellationToken);
        try
        {
            await service.Completed.Task.WaitAsync(cancellationToken);
            await Assert.That(service.ExecuteTask!.IsCompleted).IsFalse();
            await Assert.That(service.Successes).IsEquivalentTo(["value-0", "value-1", "value-2"]);
            await Assert.That(service.Attempts["value-1"]).IsEqualTo(3);
        }
        finally
        {
            await service.StopAsync(cancellationToken);
            await service.DisposeAsync();
        }

        // The redelivered record committed only after it succeeded: nothing remains for the group.
        await producer.ProduceAsync(topic, "key", "sentinel", cancellationToken);
        await using var verifier = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync(cancellationToken);
        verifier.Subscribe(topic);
        var next = await verifier.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken);
        await Assert.That(next).IsNotNull();
        await Assert.That(next!.Value.Value).IsEqualTo("sentinel");
    }

    [Test]
    [Timeout(90_000)]
    [Category("ShareConsumer")]
    [SupportsKafka(420)]
    [NotInParallel("ShareConsumerKafka42")]
    public async Task ShareConsumer_DefaultDisposition_ReleasesAndReprocessesWithoutStopping(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"hosted-share-redeliver-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        }, cancellationToken: cancellationToken);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken);
        for (var index = 0; index < 3; index++)
            await producer.ProduceAsync(topic, "key", $"value-{index}", cancellationToken);

        var consumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).Build();
        var service = new RedeliveringShareConsumerService(consumer, topic, expected: 3);
        await service.StartAsync(cancellationToken);
        try
        {
            await service.Completed.Task.WaitAsync(cancellationToken);
            await Assert.That(service.ExecuteTask!.IsCompleted).IsFalse();
            await Assert.That(service.Successes.Keys).IsEquivalentTo(["value-0", "value-1", "value-2"]);
            // The released record came back as a new delivery of the same acquisition.
            await Assert.That(service.Successes["value-1"]).IsGreaterThanOrEqualTo(2);
        }
        finally
        {
            await service.StopAsync(cancellationToken);
            await service.DisposeAsync();
        }
    }

    private sealed class RedeliveringConsumerService(
        IKafkaConsumer<string, string> consumer, string topic, int failuresBeforeSuccess, int expected)
        : KafkaConsumerService<string, string>(consumer, NullLogger.Instance,
            serviceOptions: new KafkaConsumerServiceOptions
            {
                DrainOnShutdown = false,
                PollRetryBackoff = TimeSpan.FromMilliseconds(50)
            })
    {
        public ConcurrentDictionary<string, int> Attempts { get; } = new();
        public ConcurrentQueue<string> Successes { get; } = new();
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override IEnumerable<string> Topics => [topic];

        protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            var attempt = Attempts.AddOrUpdate(result.Value!, 1, static (_, count) => count + 1);
            if (result.Value == "value-1" && attempt <= failuresBeforeSuccess)
                throw new InvalidOperationException("Transient handler failure.");
            Successes.Enqueue(result.Value!);
            if (Successes.Count == expected)
                Completed.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RedeliveringShareConsumerService(
        IKafkaShareConsumer<string, string> consumer, string topic, int expected)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance,
            serviceOptions: new KafkaShareConsumerServiceOptions { DrainOnShutdown = false })
    {
        public ConcurrentDictionary<string, int> Successes { get; } = new();
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override IEnumerable<string> Topics => [topic];

        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            if (result.Value == "value-1" && result.DeliveryCount == 1)
                throw new InvalidOperationException("Transient handler failure.");
            Successes.TryAdd(result.Value!, result.DeliveryCount);
            if (Successes.Count == expected)
                Completed.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}
