using System.Reflection;
using System.Runtime.CompilerServices;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.Retry;
using Dekaf.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Dekaf.Tests.Unit.Hosting;

public sealed class KafkaConsumerServiceTests
{
    #region ExecuteAsync

    [Test]
    public async Task ExecuteAsync_SubscribesToTopics()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.ArgAt<CancellationToken>(0)));

        var service = new TestConsumerService(consumer, ["topic-a", "topic-b"]);

        await service.StartAsync(CancellationToken.None);

        // Poll until Subscribe is called rather than using a fixed delay
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                consumer.Received(1).Subscribe(Arg.Is<string[]>(t => t != null && t.Length == 2 && t[0] == "topic-a" && t[1] == "topic-b"));
                break;
            }
            catch (NSubstitute.Exceptions.ReceivedCallsException)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        await service.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task ExecuteAsync_WithRetryTopics_SubscribesToSourceAndRetryTopics()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.ArgAt<CancellationToken>(0)));

        var deadLetterOptions = new DeadLetterOptions
        {
            RetryTopics = new RetryTopicOptions
            {
                Delays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)]
            }
        };
        var service = new TestConsumerService(
            consumer,
            ["orders"],
            options: new KafkaConsumerServiceOptions { DrainOnShutdown = false },
            deadLetterOptions: deadLetterOptions);

        await service.StartAsync(CancellationToken.None);
        await WaitForSubscribeAsync(consumer);
        await service.StopAsync(CancellationToken.None);

        consumer.Received(1).Subscribe(Arg.Is<string[]>(topics =>
            topics != null && topics.Contains("orders") &&
            topics.Contains("orders-retry-5s") &&
            topics.Contains("orders-retry-30s") &&
            topics.Length == 3));
    }

    [Test]
    public async Task ExecuteAsync_ProcessesMessages()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(CreateResults(("topic-a", 0, 0), ("topic-a", 0, 1)));

        var service = new TestConsumerService(consumer, ["topic-a"]);

        await service.StartAsync(CancellationToken.None);

        // Poll until messages are processed rather than using a fixed delay
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (service.ProcessedMessages.Count < 2 && !timeout.IsCancellationRequested)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        await service.StopAsync(CancellationToken.None);

        await Assert.That(service.ProcessedMessages).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ExecuteAsync_OnProcessError_CallsOnErrorAsync()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(CreateResults(("topic-a", 0, 0)));

        var service = new FailingConsumerService(consumer, ["topic-a"]);

        await service.StartAsync(CancellationToken.None);

        // Poll until the error is recorded rather than using a fixed delay
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (service.Errors.Count == 0 && !timeout.IsCancellationRequested)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        await service.StopAsync(CancellationToken.None);

        await Assert.That(service.Errors).Count().IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task ExecuteAsync_RetryTopicMessageNotDue_PausesAndSeeksWithoutProcessing()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var positions = Substitute.For<IConsumerPositions>();
        var partitions = Substitute.For<IConsumerPartitions>();
        consumer.Positions.Returns(positions);
        consumer.Partitions.Returns(partitions);

        var source = CreateResult("orders", partition: 1, offset: 42);
        var retryHeaders = RetryTopicHeaders.Build(
            source,
            failureCount: 1,
            delay: TimeSpan.FromSeconds(5),
            dueAt: DateTimeOffset.UtcNow.AddMinutes(5));
        var retryResult = CreateResult(
            "orders-retry-5s",
            partition: 3,
            offset: 99,
            headers: retryHeaders.ToList());
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(CreateResults(retryResult));

        var deadLetterOptions = new DeadLetterOptions
        {
            RetryTopics = new RetryTopicOptions
            {
                Delays = [TimeSpan.FromSeconds(5)]
            }
        };
        var service = new TestConsumerService(
            consumer,
            ["orders"],
            options: new KafkaConsumerServiceOptions { DrainOnShutdown = false },
            deadLetterOptions: deadLetterOptions);

        await service.StartAsync(CancellationToken.None);
        await WaitForPauseAsync(partitions);
        await service.StopAsync(CancellationToken.None);

        await Assert.That(service.ProcessedMessages).IsEmpty();
        positions.Received(1).Seek(Arg.Is<TopicPartitionOffset>(offset =>
            offset.Topic == "orders-retry-5s" &&
            offset.Partition == 3 &&
            offset.Offset == 99));
        partitions.Received(1).Pause(Arg.Is<TopicPartition[]>(items =>
            items != null && items.Length == 1 &&
            items[0].Topic == "orders-retry-5s" &&
            items[0].Partition == 3));
    }

    [Test]
    public async Task ExecuteAsync_RetryTopicMessageWithLaterOffset_DoesNotOverwritePendingSeek()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var positions = Substitute.For<IConsumerPositions>();
        var partitions = Substitute.For<IConsumerPartitions>();
        consumer.Positions.Returns(positions);
        consumer.Partitions.Returns(partitions);

        var source = CreateResult("orders", partition: 1, offset: 42);
        var retryHeaders = RetryTopicHeaders.Build(
            source,
            failureCount: 1,
            delay: TimeSpan.FromSeconds(5),
            dueAt: DateTimeOffset.UtcNow.AddMinutes(5));
        var firstRetryResult = CreateResult(
            "orders-retry-5s",
            partition: 3,
            offset: 99,
            headers: retryHeaders.ToList());
        var secondRetryResult = CreateResult(
            "orders-retry-5s",
            partition: 3,
            offset: 100,
            headers: retryHeaders.ToList());
        var consumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(CreateResultsAndSignal(consumed, firstRetryResult, secondRetryResult));

        var deadLetterOptions = new DeadLetterOptions
        {
            RetryTopics = new RetryTopicOptions
            {
                Delays = [TimeSpan.FromSeconds(5)]
            }
        };
        var service = new TestConsumerService(
            consumer,
            ["orders"],
            options: new KafkaConsumerServiceOptions { DrainOnShutdown = false },
            deadLetterOptions: deadLetterOptions);

        await service.StartAsync(CancellationToken.None);
        await consumed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await service.StopAsync(CancellationToken.None);

        await Assert.That(service.ProcessedMessages).IsEmpty();
        positions.Received(1).Seek(Arg.Is<TopicPartitionOffset>(offset =>
            offset.Topic == "orders-retry-5s" &&
            offset.Partition == 3 &&
            offset.Offset == 99));
        positions.DidNotReceive().Seek(Arg.Is<TopicPartitionOffset>(offset =>
            offset.Topic == "orders-retry-5s" &&
            offset.Partition == 3 &&
            offset.Offset == 100));
        partitions.Received(1).Pause(Arg.Is<TopicPartition[]>(items =>
            items != null && items.Length == 1 &&
            items[0].Topic == "orders-retry-5s" &&
            items[0].Partition == 3));
    }

    [Test]
    public async Task RetryTopicDelay_WithLongDelay_WaitsInCancelableChunks()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var waitTask = DelayUntilRetryTopicDueAsync(
            DateTimeOffset.UtcNow.AddDays(30),
            cts.Token);

        await Assert.That(async () => await waitTask).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task ProcessWithRetriesAsync_RetryTopicsExhausted_RoutesToDeadLetterEvenBelowMaxFailures()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<RecordMetadata>(default(RecordMetadata)));

        var source = CreateResult("orders", partition: 1, offset: 42);
        var retryHeaders = RetryTopicHeaders.Build(
            source,
            failureCount: 1,
            delay: TimeSpan.FromSeconds(5),
            dueAt: DateTimeOffset.UtcNow.AddSeconds(-1));
        var retryResult = CreateResult(
            "orders-retry-5s",
            partition: 3,
            offset: 99,
            headers: retryHeaders.ToList());

        var deadLetterOptions = new DeadLetterOptions
        {
            MaxFailures = 3,
            RetryTopics = new RetryTopicOptions
            {
                Delays = [TimeSpan.FromSeconds(5)]
            }
        };
        var service = new FailingConsumerService(consumer, ["orders"], deadLetterOptions);
        SetDlqProducer(service, producer);

        await ProcessWithRetriesAsync(service, retryResult, CancellationToken.None);

        await producer.Received(1).ProduceAsync(Arg.Is<ProducerMessage<byte[]?, byte[]?>>(message =>
            message != null && message.Topic == "orders.DLQ" &&
            message.Headers!.GetFirstAsString(DeadLetterHeaders.FailureCountKey) == "2"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessWithRetriesAsync_RetryPolicy_UsesNextRetryTopicTierAfterLocalRetries()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<RecordMetadata>(default(RecordMetadata)));

        var deadLetterOptions = new DeadLetterOptions
        {
            MaxFailures = 10,
            RetryTopics = new RetryTopicOptions
            {
                Delays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)]
            }
        };
        var retryPolicy = new FixedDelayRetryPolicy
        {
            Delay = TimeSpan.Zero,
            MaxAttempts = 1
        };
        var service = new FailingConsumerService(consumer, ["orders"], deadLetterOptions, retryPolicy);
        SetDlqProducer(service, producer);

        await ProcessWithRetriesAsync(service, CreateResult("orders", partition: 1, offset: 42), CancellationToken.None);

        await producer.Received(1).ProduceAsync(Arg.Is<ProducerMessage<byte[]?, byte[]?>>(message =>
            message != null && message.Topic == "orders-retry-5s" &&
            message.Headers!.GetFirstAsString(RetryTopicHeaders.FailureCountKey) == "1"),
            Arg.Any<CancellationToken>());
        await producer.DidNotReceive().ProduceAsync(Arg.Is<ProducerMessage<byte[]?, byte[]?>>(message =>
            message != null && message.Topic == "orders-retry-30s"),
            Arg.Any<CancellationToken>());
        await Assert.That(service.Errors).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ProcessWithRetriesAsync_CustomDeadLetterPolicy_ControlsRoutingAndTopic()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<RecordMetadata>(default(RecordMetadata)));

        var policy = Substitute.For<IDeadLetterPolicy<string, string>>();
        policy.ShouldDeadLetter(Arg.Any<ConsumeResult<string, string>>(), Arg.Any<Exception>(), Arg.Any<int>())
            .Returns(true);
        policy.GetDeadLetterTopic("orders").Returns("poison-messages");

        var service = new FailingConsumerService(
            consumer, ["orders"],
            deadLetterOptions: new DeadLetterOptions { MaxFailures = 100 },
            deadLetterPolicy: policy);
        SetDlqProducer(service, producer);

        await ProcessWithRetriesAsync(service, CreateResult("orders", partition: 1, offset: 42), CancellationToken.None);

        await producer.Received(1).ProduceAsync(Arg.Is<ProducerMessage<byte[]?, byte[]?>>(message =>
            message != null && message.Topic == "poison-messages"),
            Arg.Any<CancellationToken>());
        policy.Received(1).ShouldDeadLetter(Arg.Any<ConsumeResult<string, string>>(),
            Arg.Any<InvalidOperationException>(), 1);
    }

    [Test]
    public async Task ProcessWithRetriesAsync_FireAndForget_RoutesToDeadLetterViaFireAsync()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.FireAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>())
            .Returns(ValueTask.CompletedTask);

        var service = new FailingConsumerService(
            consumer, ["orders"],
            deadLetterOptions: new DeadLetterOptions { AwaitDelivery = false });
        SetDlqProducer(service, producer);

        await ProcessWithRetriesAsync(service, CreateResult("orders", partition: 1, offset: 42), CancellationToken.None);

        await producer.Received(1).FireAsync(Arg.Is<ProducerMessage<byte[]?, byte[]?>>(message =>
            message != null && message.Topic == "orders.DLQ"));
        await producer.DidNotReceive().ProduceAsync(
            Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessWithRetriesAsync_ShutdownCancelsDlqWrite_PropagatesInsteadOfSwallowing()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        using var cts = new CancellationTokenSource();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.FromCanceled<RecordMetadata>(cts.Token);
            });

        var service = new FailingConsumerService(
            consumer, ["orders"], deadLetterOptions: new DeadLetterOptions());
        SetDlqProducer(service, producer);

        OperationCanceledException? caught = null;
        try
        {
            await ProcessWithRetriesAsync(service, CreateResult("orders", partition: 1, offset: 42), cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
    }

    [Test]
    public async Task StopAsync_ShutdownCancelsDlqWrite_SkipsDrainSoInDoubtRecordIsNotCommitted()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => YieldOneThenWait(
                CreateResult("orders", partition: 1, offset: 42),
                callInfo.ArgAt<CancellationToken>(0)));

        var dlqWriteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new ValueTask<RecordMetadata>(
                BlockUntilCancelledAsync(dlqWriteStarted, callInfo.ArgAt<CancellationToken>(1))));

        var service = new FailingConsumerService(
            consumer, ["orders"], deadLetterOptions: new DeadLetterOptions());
        SetDlqProducer(service, producer);

        await service.StartAsync(CancellationToken.None);
        await dlqWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await service.StopAsync(CancellationToken.None);

        // Draining would prove the in-doubt record processed, and an explicit CommitAsync
        // vouches for the in-doubt record itself — both must be skipped so the record is
        // redelivered on restart instead of committed away without its dead-letter copy.
        // (The consumer's own close path commits proven offsets only, during disposal.)
        await consumer.DidNotReceive().ConsumeOneAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await consumer.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<ConsumeResult<string, string>> YieldOneThenWait(
        ConsumeResult<string, string> result,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return result;
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private static async Task<RecordMetadata> BlockUntilCancelledAsync(
        TaskCompletionSource started, CancellationToken cancellationToken)
    {
        started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return default;
    }

    [Test]
    public async Task Constructor_DeadLetterPolicyWithoutOptions_Throws()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var policy = Substitute.For<IDeadLetterPolicy<string, string>>();

        await Assert.That(() => new FailingConsumerService(
            consumer, ["orders"], deadLetterPolicy: policy)).Throws<ArgumentException>();
    }

    #endregion

    #region StopAsync

    [Test]
    public async Task StopAsync_CommitsOffsets()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.ArgAt<CancellationToken>(0)));

        var options = new KafkaConsumerServiceOptions
        {
            DrainOnShutdown = false
        };
        var service = new TestConsumerService(consumer, ["topic-a"], options);

        await service.StartAsync(CancellationToken.None);
        await WaitForSubscribeAsync(consumer);
        await service.StopAsync(CancellationToken.None);

        await consumer.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StopAsync_CommitFails_DoesNotThrow()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.ArgAt<CancellationToken>(0)));
        consumer.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("Commit failed"));

        // DrainOnShutdown = false: this test is about commit failure, not drain behavior
        var options = new KafkaConsumerServiceOptions { DrainOnShutdown = false };
        var service = new TestConsumerService(consumer, ["topic-a"], options);

        await service.StartAsync(CancellationToken.None);
        await WaitForSubscribeAsync(consumer);

        // StopAsync should not throw even if commit fails
        var act = async () => await service.StopAsync(CancellationToken.None);

        await Assert.That(act).ThrowsNothing();
    }

    #endregion

    #region Graceful Shutdown

    [Test]
    public async Task StopAsync_DrainOnShutdownTrue_DrainsBufferedMessages()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.ArgAt<CancellationToken>(0)));

        // Set up ConsumeOneAsync to return one message then null (buffer empty)
        var callCount = 0;
        consumer.ConsumeOneAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var count = Interlocked.Increment(ref callCount);
                if (count <= 2)
                {
                    return new ValueTask<ConsumeResult<string, string>?>(
                        new ConsumeResult<string, string>(
                            topic: "topic-a",
                            partition: 0,
                            offset: count - 1,
                            keyData: default,
                            isKeyNull: true,
                            valueData: default,
                            isValueNull: true,
                            headers: null,
                            timestampMs: 0,
                            timestampType: TimestampType.NotAvailable,
                            leaderEpoch: null,
                            keyDeserializer: null,
                            valueDeserializer: null));
                }
                return new ValueTask<ConsumeResult<string, string>?>((ConsumeResult<string, string>?)null);
            });

        var options = new KafkaConsumerServiceOptions
        {
            DrainOnShutdown = true,
            ShutdownTimeout = TimeSpan.FromSeconds(5)
        };
        var service = new TestConsumerService(consumer, ["topic-a"], options);

        await service.StartAsync(CancellationToken.None);
        await WaitForSubscribeAsync(consumer);
        await service.StopAsync(CancellationToken.None);

        // The drain should have processed 2 messages via ConsumeOneAsync
        await Assert.That(service.ProcessedMessages).Count().IsEqualTo(2);
    }

    [Test]
    public async Task StopAsync_DrainOnShutdownFalse_SkipsDrain()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.ArgAt<CancellationToken>(0)));

        var options = new KafkaConsumerServiceOptions
        {
            DrainOnShutdown = false,
            ShutdownTimeout = TimeSpan.FromSeconds(5)
        };
        var service = new TestConsumerService(consumer, ["topic-a"], options);

        await service.StartAsync(CancellationToken.None);
        await WaitForSubscribeAsync(consumer);
        await service.StopAsync(CancellationToken.None);

        // ConsumeOneAsync should never be called when drain is disabled
        await consumer.DidNotReceive().ConsumeOneAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StopAsync_DrainOnShutdown_CommitsOffsetsAfterDrain()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.ArgAt<CancellationToken>(0)));

        // Buffer returns null immediately (no buffered messages)
        consumer.ConsumeOneAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ConsumeResult<string, string>?>((ConsumeResult<string, string>?)null));

        var options = new KafkaConsumerServiceOptions
        {
            DrainOnShutdown = true,
            ShutdownTimeout = TimeSpan.FromSeconds(5)
        };
        var service = new TestConsumerService(consumer, ["topic-a"], options);

        await service.StartAsync(CancellationToken.None);
        await WaitForSubscribeAsync(consumer);
        await service.StopAsync(CancellationToken.None);

        // CommitAsync should be called once (the final commit after drain)
        await consumer.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StopAsync_DrainTimeoutElapsed_CompletesAndCommits()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        consumer.ConsumeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellation(callInfo.ArgAt<CancellationToken>(0)));

        // ConsumeOneAsync keeps returning messages indefinitely (buffer never empties)
        consumer.ConsumeOneAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.ArgAt<CancellationToken>(1).ThrowIfCancellationRequested();
                return new ValueTask<ConsumeResult<string, string>?>(
                    new ConsumeResult<string, string>(
                        topic: "topic-a",
                        partition: 0,
                        offset: 0,
                        keyData: default,
                        isKeyNull: true,
                        valueData: default,
                        isValueNull: true,
                        headers: null,
                        timestampMs: 0,
                        timestampType: TimestampType.NotAvailable,
                        leaderEpoch: null,
                        keyDeserializer: null,
                        valueDeserializer: null));
            });

        var options = new KafkaConsumerServiceOptions
        {
            DrainOnShutdown = true,
            // Use 100ms to avoid spinning CPU for a full second
            ShutdownTimeout = TimeSpan.FromMilliseconds(100)
        };
        var service = new TestConsumerService(consumer, ["topic-a"], options);

        await service.StartAsync(CancellationToken.None);
        await WaitForSubscribeAsync(consumer);

        // StopAsync should complete within a reasonable time despite infinite messages
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var stopTask = service.StopAsync(CancellationToken.None);
        var completedTask = await Task.WhenAny(stopTask, Task.Delay(Timeout.Infinite, timeoutCts.Token));

        await Assert.That(completedTask).IsEqualTo(stopTask);

        // Observe any exceptions from stopTask to prevent unobserved task exception
        await stopTask;

        // CommitAsync should still be called after drain times out
        await consumer.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task KafkaConsumerServiceOptions_DefaultValues()
    {
        var options = new KafkaConsumerServiceOptions();

        await Assert.That(options.ShutdownTimeout).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(options.DrainOnShutdown).IsTrue();
    }

    [Test]
    public async Task KafkaConsumerServiceOptions_CustomValues()
    {
        var options = new KafkaConsumerServiceOptions
        {
            ShutdownTimeout = TimeSpan.FromSeconds(10),
            DrainOnShutdown = false
        };

        await Assert.That(options.ShutdownTimeout).IsEqualTo(TimeSpan.FromSeconds(10));
        await Assert.That(options.DrainOnShutdown).IsFalse();
    }

    #endregion

    #region Disposal

    [Test]
    public async Task DisposeAsync_AwaitsConsumerDisposal()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var disposal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        consumer.DisposeAsync().Returns(_ => new ValueTask(disposal.Task));
        var service = new TestConsumerService(consumer, ["topic-a"]);

        var disposeTask = service.DisposeAsync().AsTask();
        await Task.Yield();

        await Assert.That(disposeTask.IsCompleted).IsFalse();

        disposal.SetResult();
        await disposeTask;

        await consumer.Received(1).DisposeAsync();
    }

    [Test]
    public async Task Dispose_InvokesConsumerDisposal()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();
        var service = new TestConsumerService(consumer, ["topic-a"]);

        service.Dispose();

        await consumer.Received(1).DisposeAsync();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Polls until Subscribe() has been called on the mock consumer, indicating
    /// that ExecuteAsync has started. Uses deterministic synchronization instead
    /// of a fixed Task.Delay.
    /// </summary>
    private static async Task WaitForSubscribeAsync(IKafkaConsumer<string, string> consumer)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                consumer.Received(1).Subscribe(Arg.Any<string[]>());
                return;
            }
            catch (NSubstitute.Exceptions.ReceivedCallsException)
            {
                await Task.Delay(10, timeout.Token);
            }
        }

        throw new TimeoutException("ExecuteAsync did not call Subscribe() within timeout");
    }

    private static async Task WaitForPauseAsync(IConsumerPartitions partitions)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                partitions.Received(1).Pause(Arg.Any<TopicPartition[]>());
                return;
            }
            catch (NSubstitute.Exceptions.ReceivedCallsException)
            {
                await Task.Delay(10, timeout.Token);
            }
        }

        throw new TimeoutException("Retry topic partition was not paused within timeout");
    }

    private static async IAsyncEnumerable<ConsumeResult<string, string>> WaitForCancellation(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected - service is stopping
        }

        yield break;
    }

    private static async IAsyncEnumerable<ConsumeResult<string, string>> CreateResults(
        params (string Topic, int Partition, long Offset)[] items)
    {
        foreach (var (topic, partition, offset) in items)
        {
            yield return CreateResult(topic, partition, offset);
        }

        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<ConsumeResult<string, string>> CreateResults(
        params ConsumeResult<string, string>[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<ConsumeResult<string, string>> CreateResultsAndSignal(
        TaskCompletionSource completion,
        params ConsumeResult<string, string>[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        completion.TrySetResult();
        await Task.CompletedTask;
    }

    private static Task DelayUntilRetryTopicDueAsync(
        DateTimeOffset dueAt,
        CancellationToken cancellationToken)
    {
        var method = typeof(Dekaf.Extensions.Hosting.KafkaConsumerService<string, string>).GetMethod(
            "DelayUntilRetryTopicDueAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DelayUntilRetryTopicDueAsync method not found.");

        return (Task)method.Invoke(null, [dueAt, cancellationToken])!;
    }

    private static ValueTask ProcessWithRetriesAsync(
        TestableKafkaConsumerService service,
        ConsumeResult<string, string> result,
        CancellationToken cancellationToken)
    {
        var method = typeof(Dekaf.Extensions.Hosting.KafkaConsumerService<string, string>).GetMethod(
            "ProcessWithRetriesAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ProcessWithRetriesAsync method not found.");

        return (ValueTask)method.Invoke(service, [result, cancellationToken])!;
    }

    private static void SetDlqProducer(
        TestableKafkaConsumerService service,
        IKafkaProducer<byte[]?, byte[]?> producer)
    {
        var field = typeof(Dekaf.Extensions.Hosting.KafkaConsumerService<string, string>).GetField(
            "_dlqProducer",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_dlqProducer field not found.");

        field.SetValue(service, producer);
    }

    private static ConsumeResult<string, string> CreateResult(
        string topic,
        int partition = 0,
        long offset = 0,
        IReadOnlyList<Header>? headers = null)
    {
        return new ConsumeResult<string, string>(
            topic: topic,
            partition: partition,
            offset: offset,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: headers,
            timestampMs: 0,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);
    }

    private sealed class TestConsumerService : TestableKafkaConsumerService
    {
        public List<ConsumeResult<string, string>> ProcessedMessages { get; } = [];

        public TestConsumerService(
            IKafkaConsumer<string, string> consumer,
            string[] topics,
            KafkaConsumerServiceOptions? options = null,
            DeadLetterOptions? deadLetterOptions = null)
            : base(consumer, topics, options, deadLetterOptions)
        {
        }

        protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            ProcessedMessages.Add(result);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingConsumerService : TestableKafkaConsumerService
    {
        public List<Exception> Errors { get; } = [];

        public FailingConsumerService(
            IKafkaConsumer<string, string> consumer,
            string[] topics,
            DeadLetterOptions? deadLetterOptions = null,
            IRetryPolicy? retryPolicy = null,
            IDeadLetterPolicy<string, string>? deadLetterPolicy = null)
            : base(consumer, topics, deadLetterOptions: deadLetterOptions, retryPolicy: retryPolicy,
                deadLetterPolicy: deadLetterPolicy)
        {
        }

        protected override ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Processing failed");
        }

        protected override ValueTask OnErrorAsync(Exception exception, ConsumeResult<string, string>? result, CancellationToken cancellationToken)
        {
            Errors.Add(exception);
            return ValueTask.CompletedTask;
        }
    }

    private abstract class TestableKafkaConsumerService : Dekaf.Extensions.Hosting.KafkaConsumerService<string, string>
    {
        private readonly string[] _topics;

        protected TestableKafkaConsumerService(
            IKafkaConsumer<string, string> consumer,
            string[] topics,
            KafkaConsumerServiceOptions? options = null,
            DeadLetterOptions? deadLetterOptions = null,
            IRetryPolicy? retryPolicy = null,
            IDeadLetterPolicy<string, string>? deadLetterPolicy = null)
            : base(consumer, NullLogger.Instance, deadLetterOptions, retryPolicy, options, deadLetterPolicy)
        {
            _topics = topics;
        }

        protected override IEnumerable<string> Topics => _topics;
    }

    #endregion
}
