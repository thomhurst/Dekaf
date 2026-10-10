using System.Collections.Concurrent;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.Hosting;
using Dekaf.Retry;
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
    public async Task Consumer_StopDuringRedeliveryBackoff_OnDeliveryManualCommit_DoesNotCommitPastFailedRecord(
        CancellationToken cancellationToken)
    {
        // On-delivery staging stores offset + 1 before processing. The redelivery seek must replace it,
        // so the final explicit commit during shutdown publishes the failed offset, not the next one.
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"hosted-redeliver-ondelivery-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken);
        for (var index = 0; index < 3; index++)
            await producer.ProduceAsync(topic, "key", $"value-{index}", cancellationToken);

        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithOffsetStoreTiming(OffsetStoreTiming.OnDelivery)
            .BuildAsync(cancellationToken);
        var service = new RedeliveringConsumerService(consumer, topic, failuresBeforeSuccess: int.MaxValue, expected: int.MaxValue,
            pollRetryBackoff: TimeSpan.FromHours(1));
        await service.StartAsync(cancellationToken);
        try
        {
            await service.FirstFailure.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            await service.StopAsync(cancellationToken);
            await service.DisposeAsync();
        }

        await Assert.That(service.Successes).IsEquivalentTo(["value-0"]);
        await using var offsetProbe = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .BuildAsync(cancellationToken);
        var committed = await offsetProbe.GetCommittedOffsetAsync(new TopicPartition(topic, 0), cancellationToken);
        await Assert.That(committed).IsEqualTo(1);
    }

    [Test]
    [Timeout(120_000)]
    public async Task Consumer_RedeliveryAfterPartitionLostDuringProcessing_DoesNotRewindNewOwnersCommit(
        CancellationToken cancellationToken)
    {
        // The record's processing outlasts the max poll interval, so the member loses the partition
        // and another member processes and commits it. The late failure must not rewind the
        // partition: a seek would store the failed offset for this member's next commit.
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var partition = new TopicPartition(topic, 0);
        var group = $"hosted-redeliver-lost-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken);
        for (var index = 0; index < 3; index++)
            await producer.ProduceAsync(topic, "key", $"value-{index}", cancellationToken);
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();

        var stalledConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithMaxPollInterval(TimeSpan.FromSeconds(2))
            .BuildAsync(cancellationToken);
        var stalled = new StalledFirstRecordConsumerService(stalledConsumer, topic);
        await using var newOwner = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync(cancellationToken);
        await stalled.StartAsync(cancellationToken);
        try
        {
            await stalled.Processing.Task.WaitAsync(cancellationToken);

            // The new owner gets the partition once the stalled member is evicted.
            newOwner.Subscribe(topic);
            for (var consumed = 0; consumed < 3;)
            {
                if (await newOwner.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken) is not null)
                    consumed++;
            }

            await newOwner.CommitAsync(cancellationToken);
            await WaitUntilAsync(() => Task.FromResult(!stalledConsumer.Assignment.Contains(partition)), cancellationToken);

            stalled.Release.TrySetResult();
            await stalled.FailureResolved.Task.WaitAsync(cancellationToken);
            // Let the stalled member rejoin so the commit at shutdown is accepted again.
            await WaitUntilAsync(
                async () => (await admin.DescribeConsumerGroupsAsync([group], cancellationToken))[group].Members.Count == 2,
                cancellationToken);
            // A pause would outlive the ownership and hold the partition if it came back here.
            await Assert.That(stalledConsumer.Paused.Contains(partition)).IsFalse();
        }
        finally
        {
            stalled.Release.TrySetResult();
            await stalled.StopAsync(cancellationToken);
            await stalled.DisposeAsync();
        }

        await using var offsetProbe = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .BuildAsync(cancellationToken);
        var committed = await offsetProbe.GetCommittedOffsetAsync(partition, cancellationToken);
        await Assert.That(committed).IsEqualTo(3);
    }

    [Test]
    [Arguments("localRetry")]
    [Arguments("deadLetter")]
    [Arguments("retryTopic")]
    [Timeout(120_000)]
    public async Task Consumer_PartitionLostDuringProcessing_StopsRetryingAndRoutesNothing(
        string failureHandling,
        CancellationToken cancellationToken)
    {
        // The record's processing outlasts the max poll interval, so the member loses the partition
        // and another member processes and commits it. The late failure must neither retry the
        // handler, nor produce a DLQ or retry-topic copy the new owner would duplicate, nor store
        // or commit an offset for the lost partition. The revocation is synchronized before the
        // handler fails, so the documented produce-window duplicate cannot occur here.
        var topic = await KafkaContainer.CreateTestTopicAsync();
        // Only the stalled member subscribes to this topic, so it keeps a partition after it
        // rejoins. Its sentinel record is processed once the failed record's handling is over,
        // with or without the ownership checks: the test waits on that, never on a timeout.
        var sideTopic = await KafkaContainer.CreateTestTopicAsync();
        var partition = new TopicPartition(topic, 0);
        var group = $"hosted-lost-routing-{Guid.NewGuid():N}";
        string? routingTarget = failureHandling switch
        {
            "deadLetter" => topic + ".DLQ",
            "retryTopic" => topic + "-retry-5m",
            _ => null
        };
        if (routingTarget is not null)
            await KafkaContainer.CreateTopicAsync(routingTarget, partitions: 1);
        if (failureHandling == "retryTopic")
            await KafkaContainer.CreateTopicAsync(sideTopic + "-retry-5m", partitions: 1);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken);
        for (var index = 0; index < 3; index++)
            await producer.ProduceAsync(topic, "key", $"value-{index}", cancellationToken);
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();

        var stalledConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithMaxPollInterval(TimeSpan.FromSeconds(2))
            .BuildAsync(cancellationToken);
        var stalled = new StalledRoutingConsumerService(
            stalledConsumer,
            topic,
            sideTopic,
            // Immediate retries: without the ownership checks the handler runs 3 times.
            failureHandling == "localRetry" ? new ImmediateRetryPolicy(retries: 2) : null,
            failureHandling switch
            {
                "deadLetter" => new DeadLetterOptions { BootstrapServers = KafkaContainer.BootstrapServers },
                "retryTopic" => new DeadLetterOptions
                {
                    BootstrapServers = KafkaContainer.BootstrapServers,
                    RetryTopics = new RetryTopicOptions { Delays = [TimeSpan.FromMinutes(5)] }
                },
                _ => null
            });
        await using var newOwner = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync(cancellationToken);
        await stalled.StartAsync(cancellationToken);
        try
        {
            await stalled.Processing.Task.WaitAsync(cancellationToken);

            newOwner.Subscribe(topic);
            var newOwnerValues = new List<string>();
            while (newOwnerValues.Count < 3)
            {
                if (await newOwner.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken) is { } record)
                    newOwnerValues.Add(record.Value);
            }

            await newOwner.CommitAsync(cancellationToken);
            await Assert.That(newOwnerValues).IsEquivalentTo(["value-0", "value-1", "value-2"]);
            await WaitUntilAsync(() => Task.FromResult(!stalledConsumer.Assignment.Contains(partition)), cancellationToken);

            stalled.Release.TrySetResult();
            await WaitUntilAsync(
                async () => (await admin.DescribeConsumerGroupsAsync([group], cancellationToken))[group].Members.Count == 2,
                cancellationToken);
            await producer.ProduceAsync(sideTopic, "key", "sentinel", cancellationToken);
            await stalled.SentinelProcessed.Task.WaitAsync(cancellationToken);

            await Assert.That(stalled.FailedAttempts).IsEqualTo(1);
            await Assert.That(stalledConsumer.Paused.Contains(partition)).IsFalse();
        }
        finally
        {
            stalled.Release.TrySetResult();
            await stalled.StopAsync(cancellationToken);
            await stalled.DisposeAsync();
        }

        if (routingTarget is not null)
        {
            // Routed copies are awaited before the loop moves on to the sentinel, so every copy
            // from the stalled member is below the high watermark now.
            await using var routedProbe = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync(cancellationToken);
            var routedPartition = new TopicPartition(routingTarget, 0);
            var high = (await routedProbe.QueryWatermarkOffsetsAsync(routedPartition, cancellationToken)).High;
            routedProbe.Assign(routedPartition);
            var routedCopies = 0;
            while (routedCopies < high
                && await routedProbe.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken) is not null)
            {
                routedCopies++;
            }

            await Assert.That(routedCopies).IsEqualTo(0);
        }

        await using var offsetProbe = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .BuildAsync(cancellationToken);
        var committed = await offsetProbe.GetCommittedOffsetAsync(partition, cancellationToken);
        await Assert.That(committed).IsEqualTo(3);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, CancellationToken cancellationToken)
    {
        while (!await condition())
            await Task.Delay(50, cancellationToken);
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
        IKafkaConsumer<string, string> consumer, string topic, int failuresBeforeSuccess, int expected,
        TimeSpan? pollRetryBackoff = null)
        : KafkaConsumerService<string, string>(consumer, NullLogger.Instance,
            serviceOptions: new KafkaConsumerServiceOptions
            {
                DrainOnShutdown = false,
                PollRetryBackoff = pollRetryBackoff ?? TimeSpan.FromMilliseconds(50),
                MaxPollRetryBackoff = pollRetryBackoff ?? TimeSpan.FromSeconds(30)
            })
    {
        public ConcurrentDictionary<string, int> Attempts { get; } = new();
        public TaskCompletionSource FirstFailure { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<string> Successes { get; } = new();
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override IEnumerable<string> Topics => [topic];

        protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            var attempt = Attempts.AddOrUpdate(result.Value!, 1, static (_, count) => count + 1);
            if (result.Value == "value-1" && attempt <= failuresBeforeSuccess)
            {
                FirstFailure.TrySetResult();
                throw new InvalidOperationException("Transient handler failure.");
            }
            Successes.Enqueue(result.Value!);
            if (Successes.Count == expected)
                Completed.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>The first record blocks until released and then fails, as does every retry of it.</summary>
    private sealed class StalledRoutingConsumerService(
        IKafkaConsumer<string, string> consumer,
        string topic,
        string sideTopic,
        IRetryPolicy? retryPolicy,
        DeadLetterOptions? deadLetterOptions)
        : KafkaConsumerService<string, string>(consumer, NullLogger.Instance, deadLetterOptions, retryPolicy,
            serviceOptions: new KafkaConsumerServiceOptions
            {
                DrainOnShutdown = false,
                PollRetryBackoff = TimeSpan.FromHours(1),
                MaxPollRetryBackoff = TimeSpan.FromHours(1)
            })
    {
        private int _failedAttempts;
        public TaskCompletionSource Processing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SentinelProcessed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int FailedAttempts => Volatile.Read(ref _failedAttempts);
        protected override IEnumerable<string> Topics => [topic, sideTopic];

        protected override async ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            if (result.Topic == sideTopic)
            {
                SentinelProcessed.TrySetResult();
                return;
            }

            if (result.Topic != topic || result.Offset != 0)
                return;

            if (Interlocked.Increment(ref _failedAttempts) == 1)
            {
                Processing.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }

            throw new InvalidOperationException("Handler failed after its partition was lost.");
        }
    }

    private sealed class ImmediateRetryPolicy(int retries) : IRetryPolicy
    {
        public TimeSpan? GetNextDelay(int attemptNumber, Exception exception)
            => attemptNumber <= retries ? TimeSpan.Zero : null;
    }

    private sealed class StalledFirstRecordConsumerService(IKafkaConsumer<string, string> consumer, string topic)
        : KafkaConsumerService<string, string>(consumer, NullLogger.Instance,
            serviceOptions: new KafkaConsumerServiceOptions
            {
                DrainOnShutdown = false,
                PollRetryBackoff = TimeSpan.FromHours(1),
                MaxPollRetryBackoff = TimeSpan.FromHours(1)
            })
    {
        private int _processed;
        public TaskCompletionSource Processing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FailureResolved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override IEnumerable<string> Topics => [topic];

        protected override async ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _processed) != 1)
                return;

            Processing.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            throw new InvalidOperationException("Handler failed after its partition was lost.");
        }

        protected override async ValueTask<MessageFailureDisposition> GetFailureDispositionAsync(
            MessageFailureContext<string, string> context, CancellationToken cancellationToken)
        {
            var disposition = await base.GetFailureDispositionAsync(context, cancellationToken);
            FailureResolved.TrySetResult();
            return disposition;
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
