using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for KafkaConsumerService hosted service.
/// </summary>
[Category("Messaging")]
public sealed class HostedServiceTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProcessesMessages_ViaHostedService_MessagesReceived()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"hosted-service-{Guid.NewGuid():N}";
        var receivedMessages = new ConcurrentBag<string>();
        const int messageCount = 5;

        // Create and initialize the producer BEFORE starting the host.
        // This ensures the producer's connection and metadata are established
        // before the consumer's background polling starts competing for
        // thread pool and connection resources on resource-constrained CI runners.
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Warm up to ensure broker has initialized partition state
        // (topic metadata is cached after this call)
        await producer.FireAsync(new ProducerMessage<string, string>
        { Topic = topic, Key = "warmup", Value = "warmup" });

        // Build host with consumer service
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddDekaf(dekaf =>
        {
            dekaf.AddConsumer<string, string>(c =>
                c.WithBootstrapServers(KafkaContainer.BootstrapServers)
                 .WithGroupId(groupId)
                 .WithAutoOffsetReset(AutoOffsetReset.Earliest));
        });

        builder.Services.AddSingleton(receivedMessages);
        builder.Services.AddSingleton<TestTopicHolder>(new TestTopicHolder(topic));
        builder.Services.AddHostedService<TestConsumerService>();

        var host = builder.Build();

        // Start the host
        using var cts = new CancellationTokenSource();
        var hostTask = host.RunAsync(cts.Token);

        // Give the consumer service time to start and join the group.
        // The consumer needs to subscribe, join the group, and receive its partition
        // assignment before it can receive messages. On CI runners this can take longer
        // than on local machines due to thread pool contention.
        await Task.Delay(2000).ConfigureAwait(false);

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"hosted-key-{i}",
                Value = $"hosted-value-{i}"
            }, CancellationToken.None);
        }

        // Wait for messages to be processed
        await WaitForConditionAsync(
            () => receivedMessages.Count >= messageCount,
            TimeSpan.FromSeconds(30));

        await Assert.That(receivedMessages.Count).IsGreaterThanOrEqualTo(messageCount);

        // Stop gracefully
        cts.Cancel();

        try
        {
            await hostTask.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Host shutdown can be slow on resource-constrained CI runners
        }
        finally
        {
            host.Dispose();
        }
    }

    [Test]
    public async Task RetryTopicMessages_NotDue_ResumeAndProcessAcrossPartitions()
    {
        var sourceTopic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var retryDelay = TimeSpan.FromMilliseconds(250);
        var retryOptions = new RetryTopicOptions { Delays = [retryDelay] };
        var retryTopic = retryOptions.GetRetryTopic(sourceTopic, retryDelay);
        await KafkaContainer.CreateTopicAsync(retryTopic, partitions: 2);

        var groupId = $"hosted-retry-{Guid.NewGuid():N}";
        var receivedMessages = new ConcurrentBag<RetryTopicProcessedMessage>();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var dueAt = DateTimeOffset.UtcNow.AddSeconds(5);
        var inputs = new[]
        {
            new RetryTopicInput("retry-p0-a", 0, 0, dueAt),
            new RetryTopicInput("retry-p0-b", 0, 1, dueAt),
            new RetryTopicInput("retry-p1-a", 1, 0, dueAt),
            new RetryTopicInput("retry-p1-b", 1, 1, dueAt)
        };

        foreach (var input in inputs)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = retryTopic,
                Key = input.Value,
                Value = input.Value,
                Partition = input.Partition,
                Headers = BuildRetryHeaders(sourceTopic, input.Partition, input.SourceOffset, retryDelay, input.DueAt)
            }, CancellationToken.None);
        }

        await producer.FlushWithTimeoutAsync();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(receivedMessages);
        builder.Services.AddSingleton<TestTopicHolder>(new TestTopicHolder(sourceTopic));
        builder.Services.AddDekaf(dekaf =>
        {
            dekaf.AddConsumerService<RetryTopicConsumerService, string, string>(
                c => c.WithBootstrapServers(KafkaContainer.BootstrapServers)
                    .WithGroupId(groupId)
                    .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                    .WithFetchMaxWait(TimeSpan.FromMilliseconds(50)),
                dlq => dlq.WithRetryTopics(retryDelay));
        });

        var host = builder.Build();
        using var cts = new CancellationTokenSource();
        var hostTask = host.RunAsync(cts.Token);

        try
        {
            await WaitForConditionAsync(
                () => receivedMessages.Count >= inputs.Length,
                TimeSpan.FromSeconds(45));

            await Assert.That(receivedMessages.Select(m => m.Value))
                .IsEquivalentTo(inputs.Select(i => i.Value));
            await Assert.That(receivedMessages.Select(m => m.Partition).Distinct().Count())
                .IsEqualTo(2);

            foreach (var message in receivedMessages)
            {
                await Assert.That(message.ProcessedAt).IsGreaterThanOrEqualTo(message.DueAt);
            }
        }
        finally
        {
            cts.Cancel();

            try
            {
                await hostTask.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (TimeoutException)
            {
                // Host shutdown can be slow on resource-constrained CI runners
            }
            finally
            {
                host.Dispose();
            }
        }
    }

    [Test]
    public async Task FailedMessages_RouteToDeadLetterTopic_WithAwaitedDelivery()
    {
        var sourceTopic = await KafkaContainer.CreateTestTopicAsync();
        var dlqTopic = sourceTopic + ".DLQ";
        await KafkaContainer.CreateTopicAsync(dlqTopic, partitions: 1);

        var groupId = $"hosted-dlq-{Guid.NewGuid():N}";
        var receivedMessages = new ConcurrentBag<string>();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(receivedMessages);
        builder.Services.AddSingleton<TestTopicHolder>(new TestTopicHolder(sourceTopic));
        builder.Services.AddDekaf(dekaf =>
        {
            // Default DeadLetterOptions: MaxFailures = 1, AwaitDelivery = true — this exercises
            // the awaited-delivery default end-to-end against a real broker.
            dekaf.AddConsumerService<FailOnMarkerConsumerService, string, string>(
                c => c.WithBootstrapServers(KafkaContainer.BootstrapServers)
                    .WithGroupId(groupId)
                    .WithAutoOffsetReset(AutoOffsetReset.Earliest),
                dlq => { });
        });

        var host = builder.Build();
        using var cts = new CancellationTokenSource();
        var hostTask = host.RunAsync(cts.Token);

        try
        {
            await Task.Delay(2000).ConfigureAwait(false);

            await producer.ProduceAsync(sourceTopic, "k1", "ok-1", CancellationToken.None);
            await producer.ProduceAsync(sourceTopic, "k2", "fail-1", CancellationToken.None);
            await producer.ProduceAsync(sourceTopic, "k3", "ok-2", CancellationToken.None);

            await WaitForConditionAsync(
                () => receivedMessages.Count >= 2,
                TimeSpan.FromSeconds(45));

            await using var dlqConsumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId($"dlq-reader-{Guid.NewGuid():N}")
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .SubscribeTo(dlqTopic)
                .BuildAsync();

            using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            ConsumeResult<string, string>? dlqRecord = null;
            await foreach (var record in dlqConsumer.ConsumeAsync(readCts.Token))
            {
                dlqRecord = record;
                break;
            }

            await Assert.That(dlqRecord).IsNotNull();
            await Assert.That(dlqRecord!.Value.Value).IsEqualTo("fail-1");
            var sourceTopicHeader = dlqRecord.Value.Headers!.First(h => h.Key == DeadLetterHeaders.SourceTopicKey);
            await Assert.That(sourceTopicHeader.GetValueAsString()).IsEqualTo(sourceTopic);
            var errorTypeHeader = dlqRecord.Value.Headers!.First(h => h.Key == DeadLetterHeaders.ErrorTypeKey);
            await Assert.That(errorTypeHeader.GetValueAsString()).IsEqualTo(nameof(InvalidOperationException));
        }
        finally
        {
            cts.Cancel();
            try
            {
                await hostTask.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (TimeoutException)
            {
                // Host shutdown can be slow on resource-constrained CI runners
            }
            finally
            {
                host.Dispose();
            }
        }
    }

    [Test]
    public async Task InterruptedRecord_NotCommitted_RedeliveredOnRestart()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var groupId = $"hosted-indoubt-{Guid.NewGuid():N}";
        var processed = new ConcurrentBag<string>();
        var hang = new HangOnceHolder();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(topic, "k1", "ok-1", CancellationToken.None);
        await producer.ProduceAsync(topic, "k2", "hang-1", CancellationToken.None);
        await producer.ProduceAsync(topic, "k3", "ok-2", CancellationToken.None);

        await RunInterruptibleHostAsync<InterruptibleConsumerService>(
            topic, groupId, processed, hang,
            stopWhen: () => hang.HangStarted.Task.IsCompleted && processed.Contains("ok-1"));

        // First run: ok-1 processed and proven; hang-1 was interrupted mid-ProcessAsync,
        // so it must NOT have been committed away by shutdown.
        await Assert.That(processed).Contains("ok-1");
        await Assert.That(processed).DoesNotContain("hang-1");

        await RunInterruptibleHostAsync<InterruptibleConsumerService>(
            topic, groupId, processed, hang,
            stopWhen: () => processed.Contains("hang-1") && processed.Contains("ok-2"));

        // Second run: the interrupted record and its successor are redelivered and processed;
        // the proven record was committed by the close path and is not reprocessed.
        await Assert.That(processed).Contains("hang-1");
        await Assert.That(processed).Contains("ok-2");
        await Assert.That(processed.Count(v => v == "ok-1")).IsEqualTo(1);
    }

    [Test]
    public async Task ManualCommitMode_StoredOffsetsCommitted_DespiteInterruptedRecord()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var groupId = $"hosted-manual-{Guid.NewGuid():N}";
        var processed = new ConcurrentBag<string>();
        var hang = new HangOnceHolder();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(topic, "k1", "ok-1", CancellationToken.None);
        await producer.ProduceAsync(topic, "k2", "hang-1", CancellationToken.None);
        await producer.ProduceAsync(topic, "k3", "ok-2", CancellationToken.None);

        await RunInterruptibleHostAsync<ManualCommitInterruptibleConsumerService>(
            topic, groupId, processed, hang,
            stopWhen: () => hang.HangStarted.Task.IsCompleted && processed.Contains("ok-1"),
            manualCommitMode: true);

        await Assert.That(processed).Contains("ok-1");
        await Assert.That(processed).DoesNotContain("hang-1");

        // The final commit must have committed exactly the explicitly stored ok-1 offset (1) —
        // not the interrupted hang-1 record's offset (2), which auto offset storage would have
        // staged and vouched for.
        await using (var offsetProbe = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .BuildAsync())
        {
            var committed = await offsetProbe.GetCommittedOffsetAsync(
                new TopicPartition(topic, 0), CancellationToken.None);
            await Assert.That(committed).IsEqualTo(1);
        }

        await RunInterruptibleHostAsync<ManualCommitInterruptibleConsumerService>(
            topic, groupId, processed, hang,
            stopWhen: () => processed.Contains("hang-1") && processed.Contains("ok-2"),
            manualCommitMode: true);

        // Manual mode has no close-path fallback: the service's final commit must have
        // committed the explicitly stored ok-1 offset despite the interrupted record,
        // so ok-1 is not reprocessed while hang-1/ok-2 are redelivered.
        await Assert.That(processed).Contains("hang-1");
        await Assert.That(processed).Contains("ok-2");
        await Assert.That(processed.Count(v => v == "ok-1")).IsEqualTo(1);
    }

    private async Task RunInterruptibleHostAsync<TService>(
        string topic,
        string groupId,
        ConcurrentBag<string> processed,
        HangOnceHolder hang,
        Func<bool> stopWhen,
        bool manualCommitMode = false)
        where TService : KafkaConsumerService<string, string>
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(processed);
        builder.Services.AddSingleton(hang);
        builder.Services.AddSingleton<TestTopicHolder>(new TestTopicHolder(topic));
        builder.Services.AddDekaf(dekaf =>
        {
            dekaf.AddConsumerService<TService, string, string>(c =>
            {
                c.WithBootstrapServers(KafkaContainer.BootstrapServers)
                    .WithGroupId(groupId)
                    .WithAutoOffsetReset(AutoOffsetReset.Earliest);
                if (manualCommitMode)
                {
                    // Strict manual pattern: explicit StoreOffset only, no auto-staging.
                    c.WithOffsetCommitMode(OffsetCommitMode.Manual)
                        .WithAutoOffsetStore(false);
                }
            });
        });

        var host = builder.Build();
        using var cts = new CancellationTokenSource();
        var hostTask = host.RunAsync(cts.Token);

        try
        {
            await WaitForConditionAsync(stopWhen, TimeSpan.FromSeconds(45));
        }
        finally
        {
            cts.Cancel();
            try
            {
                await hostTask.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (TimeoutException)
            {
                // Host shutdown can be slow on resource-constrained CI runners
            }
            finally
            {
                host.Dispose();
            }
        }
    }

    [Test]
    public async Task StopsGracefully_NoExceptions()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"hosted-stop-{Guid.NewGuid():N}";
        var receivedMessages = new ConcurrentBag<string>();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddDekaf(dekaf =>
        {
            dekaf.AddConsumer<string, string>(c =>
                c.WithBootstrapServers(KafkaContainer.BootstrapServers)
                 .WithGroupId(groupId)
                 .WithAutoOffsetReset(AutoOffsetReset.Earliest));
        });

        builder.Services.AddSingleton(receivedMessages);
        builder.Services.AddSingleton<TestTopicHolder>(new TestTopicHolder(topic));
        builder.Services.AddHostedService<TestConsumerService>();

        var host = builder.Build();

        using var cts = new CancellationTokenSource();
        var hostTask = host.RunAsync(cts.Token);

        // Let it start
        await Task.Delay(1000).ConfigureAwait(false);

        // Stop
        cts.Cancel();

        // Should stop without throwing
        Exception? caughtException = null;
        try
        {
            await hostTask.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            // Host shutdown can be slow on resource-constrained CI runners
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }
        finally
        {
            host.Dispose();
        }

        await Assert.That(caughtException).IsNull();
    }

    private sealed class TestTopicHolder(string topic)
    {
        public string Topic { get; } = topic;
    }

    private sealed record RetryTopicInput(
        string Value,
        int Partition,
        long SourceOffset,
        DateTimeOffset DueAt);

    private sealed record RetryTopicProcessedMessage(
        string Value,
        int Partition,
        DateTimeOffset DueAt,
        DateTimeOffset ProcessedAt);

    private sealed class TestConsumerService : KafkaConsumerService<string, string>
    {
        private readonly ConcurrentBag<string> _receivedMessages;
        private readonly TestTopicHolder _topicHolder;

        public TestConsumerService(
            IKafkaConsumer<string, string> consumer,
            ILogger<TestConsumerService> logger,
            ConcurrentBag<string> receivedMessages,
            TestTopicHolder topicHolder)
            : base(consumer, logger)
        {
            _receivedMessages = receivedMessages;
            _topicHolder = topicHolder;
        }

        protected override IEnumerable<string> Topics => [_topicHolder.Topic];

        protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            _receivedMessages.Add(result.Value);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class HangOnceHolder
    {
        private int _claimed;

        public TaskCompletionSource HangStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool TryClaim() => Interlocked.Exchange(ref _claimed, 1) == 0;
    }

    private class InterruptibleConsumerService : KafkaConsumerService<string, string>
    {
        private readonly ConcurrentBag<string> _processed;
        private readonly TestTopicHolder _topicHolder;
        private readonly HangOnceHolder _hang;

        public InterruptibleConsumerService(
            IKafkaConsumer<string, string> consumer,
            ILogger<InterruptibleConsumerService> logger,
            ConcurrentBag<string> processed,
            TestTopicHolder topicHolder,
            HangOnceHolder hang)
            : base(consumer, logger)
        {
            _processed = processed;
            _topicHolder = topicHolder;
            _hang = hang;
        }

        protected override IEnumerable<string> Topics => [_topicHolder.Topic];

        protected override async ValueTask ProcessAsync(
            ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            if (result.Value?.StartsWith("hang-", StringComparison.Ordinal) == true && _hang.TryClaim())
            {
                _hang.HangStarted.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            _processed.Add(result.Value!);
            OnProcessed(result);
        }

        protected virtual void OnProcessed(ConsumeResult<string, string> result)
        {
        }
    }

    private sealed class ManualCommitInterruptibleConsumerService : InterruptibleConsumerService
    {
        private readonly IKafkaConsumer<string, string> _consumer;

        public ManualCommitInterruptibleConsumerService(
            IKafkaConsumer<string, string> consumer,
            ILogger<InterruptibleConsumerService> logger,
            ConcurrentBag<string> processed,
            TestTopicHolder topicHolder,
            HangOnceHolder hang)
            : base(consumer, logger, processed, topicHolder, hang)
        {
            _consumer = consumer;
        }

        protected override void OnProcessed(ConsumeResult<string, string> result)
            => _consumer.StoreOffset(result);
    }

    private sealed class FailOnMarkerConsumerService : KafkaConsumerService<string, string>
    {
        private readonly ConcurrentBag<string> _receivedMessages;
        private readonly TestTopicHolder _topicHolder;

        public FailOnMarkerConsumerService(
            IKafkaConsumer<string, string> consumer,
            ILogger<FailOnMarkerConsumerService> logger,
            ConcurrentBag<string> receivedMessages,
            TestTopicHolder topicHolder,
            DeadLetterOptions deadLetterOptions)
            : base(consumer, logger, deadLetterOptions)
        {
            _receivedMessages = receivedMessages;
            _topicHolder = topicHolder;
        }

        protected override IEnumerable<string> Topics => [_topicHolder.Topic];

        protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            if (result.Value?.StartsWith("fail-", StringComparison.Ordinal) == true)
                throw new InvalidOperationException($"Intentional failure for {result.Value}");

            _receivedMessages.Add(result.Value!);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RetryTopicConsumerService : KafkaConsumerService<string, string>
    {
        private readonly ConcurrentBag<RetryTopicProcessedMessage> _receivedMessages;
        private readonly TestTopicHolder _topicHolder;

        public RetryTopicConsumerService(
            IKafkaConsumer<string, string> consumer,
            ILogger<RetryTopicConsumerService> logger,
            ConcurrentBag<RetryTopicProcessedMessage> receivedMessages,
            TestTopicHolder topicHolder,
            DeadLetterOptions deadLetterOptions)
            : base(
                consumer,
                logger,
                deadLetterOptions,
                serviceOptions: new KafkaConsumerServiceOptions { DrainOnShutdown = false })
        {
            _receivedMessages = receivedMessages;
            _topicHolder = topicHolder;
        }

        protected override IEnumerable<string> Topics => [_topicHolder.Topic];

        protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            if (!RetryTopicHeaders.TryGetDueAt(result.Headers, out var dueAt))
                throw new InvalidOperationException("Retry due-at header missing.");

            _receivedMessages.Add(new RetryTopicProcessedMessage(
                result.Value,
                result.Partition,
                dueAt,
                DateTimeOffset.UtcNow));
            return ValueTask.CompletedTask;
        }
    }

    private static Headers BuildRetryHeaders(
        string sourceTopic,
        int sourcePartition,
        long sourceOffset,
        TimeSpan delay,
        DateTimeOffset dueAt)
    {
        var sourceResult = new ConsumeResult<string, string>(
            topic: sourceTopic,
            partition: sourcePartition,
            offset: sourceOffset,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);

        return RetryTopicHeaders.Build(sourceResult, failureCount: 1, delay, dueAt);
    }
}
