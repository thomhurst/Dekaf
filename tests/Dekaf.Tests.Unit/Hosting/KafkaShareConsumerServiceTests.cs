using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.Retry;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

#if NET10_0_OR_GREATER
using StringSet = System.Collections.Generic.IReadOnlySet<string>;
using PartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#else
using StringSet = System.Collections.Generic.IReadOnlyCollection<string>;
using PartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#endif

namespace Dekaf.Tests.Unit.Hosting;

public sealed class KafkaShareConsumerServiceTests
{
    [Test]
    public async Task Success_AcceptsCommitsClosesAndDisposes()
    {
        var consumer = new TestConsumer(Record(0), Record(1));
        var calls = 0;
        await using (var service = new TestService(consumer, (_, _) => { calls++; return ValueTask.CompletedTask; }))
            await RunAsync(service);
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(consumer.Acknowledgements.Select(x => x.Type)).IsEquivalentTo([AcknowledgeType.Accept, AcknowledgeType.Accept]);
        await Assert.That(consumer.Events).IsEquivalentTo(["initialize", "subscribe", "commit", "close", "dispose"]);
    }

    [Test]
    public async Task TerminalRetry_ReleasesFailedRecordAndStopsBeforeNext()
    {
        var consumer = new TestConsumer(Record(0), Record(1));
        var error = new InvalidOperationException("processing failed");
        await using var service = new TestService(consumer, (_, _) => throw error);
        await Assert.That(async () => await RunAsync(service)).Throws<InvalidOperationException>();
        await Assert.That(consumer.Acknowledgements.Single().Type).IsEqualTo(AcknowledgeType.Release);
        await Assert.That(service.FailureContext!.Value.ProcessingException).IsSameReferenceAs(error);
        await Assert.That(service.FailureContext.Value.Result.Offset).IsEqualTo(0);
        await Assert.That(consumer.Delivered).IsEqualTo(1);
    }

    [Test]
    public async Task Discard_RejectsAndContinues()
    {
        var consumer = new TestConsumer(Record(0), Record(1));
        await using var service = new TestService(consumer, (record, _) => record.Offset == 0
            ? ValueTask.FromException(new InvalidOperationException()) : ValueTask.CompletedTask)
        { Disposition = MessageFailureDisposition.Discard };
        await RunAsync(service);
        await Assert.That(consumer.Acknowledgements.Select(x => x.Type)).IsEquivalentTo([AcknowledgeType.Reject, AcknowledgeType.Accept]);
    }

    [Test]
    public async Task RetryPolicy_RetriesSameRecordBeforeAcceptance()
    {
        var consumer = new TestConsumer(Record(0));
        var retry = Substitute.For<IRetryPolicy>();
        retry.GetNextDelay(1, Arg.Any<Exception>()).Returns(TimeSpan.Zero);
        var calls = 0;
        await using var service = new TestService(consumer, (_, _) => ++calls == 1
            ? ValueTask.FromException(new InvalidOperationException()) : ValueTask.CompletedTask, retryPolicy: retry);
        await RunAsync(service);
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(consumer.Acknowledgements.Single().Type).IsEqualTo(AcknowledgeType.Accept);
    }

    [Test]
    public async Task ImplicitAcknowledgements_AreRejectedBeforeInitialization()
    {
        var consumer = new TestConsumer { AcknowledgementMode = ShareAcknowledgementMode.Implicit };
        await using var service = new TestService(consumer);
        await Assert.That(async () => await RunAsync(service)).Throws<InvalidOperationException>();
        await Assert.That(consumer.Events).IsEmpty();
    }

    [Test]
    public async Task UnknownAcknowledgementConfiguration_IsRejected()
    {
        var consumer = Substitute.For<IKafkaShareConsumer<string, string>>();
        await using var service = new TestService(consumer);
        await Assert.That(async () => await RunAsync(service)).Throws<InvalidOperationException>();
        await consumer.DidNotReceive().InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FireAndForgetRouting_IsRejected()
    {
        var consumer = new TestConsumer(Record(0));
        await using var service = new TestService(consumer, deadLetterOptions: new DeadLetterOptions { AwaitDelivery = false });
        await Assert.That(async () => await RunAsync(service)).Throws<InvalidOperationException>();
        await Assert.That(consumer.Events).IsEmpty();
    }

    [Test]
    public async Task DeadLetterRouting_AcceptsOnlyAfterDurableDeliveryAndPreservesTombstone()
    {
        var consumer = new TestConsumer(Record(0)) { RawValue = null };
        var delivered = new TaskCompletionSource<RecordMetadata>(TaskCreationOptions.RunContinuationsAsynchronously);
        var routing = Signal();
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        ProducerMessage<byte[]?, byte[]?>? sent = null;
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(call => { sent = call.ArgAt<ProducerMessage<byte[]?, byte[]?>>(0); routing.SetResult(); return new(delivered.Task); });
        await using var service = new TestService(consumer, (_, _) => throw new InvalidOperationException("failed"),
            deadLetterOptions: new DeadLetterOptions()) { Producer = producer };
        await service.StartAsync(default);
        await routing.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(consumer.Acknowledgements).IsEmpty();
        delivered.SetResult(default);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(sent!.Topic).IsEqualTo("orders.DLQ");
        await Assert.That(sent.Value).IsNull();
        await Assert.That(consumer.Acknowledgements.Single().Type).IsEqualTo(AcknowledgeType.Accept);
        await producer.Received(1).FlushAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RoutingFailure_PreservesRecordAndProvidesFailureContext()
    {
        var consumer = new TestConsumer(Record(0));
        var routingError = new InvalidOperationException("broker failure");
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<RecordMetadata>(routingError));
        await using var service = new TestService(consumer, (_, _) => throw new InvalidOperationException("process failure"),
            deadLetterOptions: new DeadLetterOptions()) { Producer = producer };
        await Assert.That(async () => await RunAsync(service)).Throws<InvalidOperationException>();
        await Assert.That(service.RoutingFailures).IsEqualTo(1);
        await Assert.That(service.FailureContext!.Value.RoutingException).IsSameReferenceAs(routingError);
        await Assert.That(service.FailureContext.Value.Stage).IsEqualTo(MessageFailureStage.DeadLetterRouting);
        await Assert.That(consumer.Acknowledgements.Single().Type).IsEqualTo(AcknowledgeType.Release);
    }

    [Test]
    [Arguments("orders")]
    [Arguments("spoofed")]
    public async Task RetryTopic_RoutesThenExhaustionFallsBackToDeadLetter(string claimedSource)
    {
        var headers = new Dekaf.Serialization.Headers().Add(RetryTopicHeaders.FailureCountKey, "1")
            .Add(RetryTopicHeaders.SourceTopicKey, claimedSource);
        var consumer = new TestConsumer(Record(0), Record(1, "orders-retry-1s", headers));
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        var topics = new List<string>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(call => { topics.Add(call.ArgAt<ProducerMessage<byte[]?, byte[]?>>(0).Topic!); return new(default(RecordMetadata)); });
        await using var service = new TestService(consumer, (_, _) => throw new InvalidOperationException(),
            deadLetterOptions: new DeadLetterOptions { RetryTopics = new RetryTopicOptions { Delays = [TimeSpan.FromSeconds(1)] } })
        { Producer = producer };
        await RunAsync(service);
        await Assert.That(topics).IsEquivalentTo(["orders-retry-1s", "orders.DLQ"]);
        await Assert.That(consumer.Subscription).IsEquivalentTo(["orders", "orders-retry-1s"]);
        await Assert.That(consumer.Acknowledgements.All(x => x.Type == AcknowledgeType.Accept)).IsTrue();
    }

    [Test]
    [Arguments("4102444800000", false)]
    [Arguments("9223372036854775807", false)]
    [Arguments("4102444800000", true)]
    public async Task SourceRetryHeaders_DoNotDelayProcessing(string dueTimestamp, bool explicitRetryNamedSource)
    {
        var headers = new Dekaf.Serialization.Headers().Add(RetryTopicHeaders.DueTimestampMsKey, dueTimestamp);
        var processed = Signal();
        var consumer = new TestConsumer(Record(0, explicitRetryNamedSource ? "orders-retry-1s" : "orders", headers));
        await using var service = new TestService(consumer, (_, _) =>
        {
            processed.TrySetResult();
            return ValueTask.CompletedTask;
        }, deadLetterOptions: new DeadLetterOptions
        {
            RetryTopics = new RetryTopicOptions { Delays = [TimeSpan.FromSeconds(1)] }
        })
        {
            Producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>(),
            SourceTopics = explicitRetryNamedSource ? ["orders", "orders-retry-1s"] : ["orders"]
        };

        await service.StartAsync(default);
        await processed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(consumer.Acknowledgements.Single().Type).IsEqualTo(AcknowledgeType.Accept);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SourceRetryHeaders_CannotRedirectRoutingOrSkipRetries(bool retryTopicsEnabled)
    {
        var headers = new Dekaf.Serialization.Headers()
            .Add(RetryTopicHeaders.SourceTopicKey, "spoofed")
            .Add(RetryTopicHeaders.SourcePartitionKey, "99")
            .Add(RetryTopicHeaders.SourceOffsetKey, "999")
            .Add(RetryTopicHeaders.FailureCountKey, "2147483647")
            .Add(RetryTopicHeaders.DelayMsKey, "999999")
            .Add("application", "preserved");
        var consumer = new TestConsumer(Record(7, headers: headers));
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        ProducerMessage<byte[]?, byte[]?>? sent = null;
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(call => { sent = call.ArgAt<ProducerMessage<byte[]?, byte[]?>>(0); return new(default(RecordMetadata)); });
        await using var service = new TestService(consumer, (_, _) => throw new InvalidOperationException(),
            deadLetterOptions: new DeadLetterOptions
            {
                RetryTopics = retryTopicsEnabled ? new RetryTopicOptions { Delays = [TimeSpan.FromSeconds(1)] } : null
            }) { Producer = producer };

        await RunAsync(service);

        await Assert.That(sent!.Topic).IsEqualTo(retryTopicsEnabled ? "orders-retry-1s" : "orders.DLQ");
        var sourceKey = retryTopicsEnabled ? RetryTopicHeaders.SourceTopicKey : DeadLetterHeaders.SourceTopicKey;
        var partitionKey = retryTopicsEnabled ? RetryTopicHeaders.SourcePartitionKey : DeadLetterHeaders.SourcePartitionKey;
        var offsetKey = retryTopicsEnabled ? RetryTopicHeaders.SourceOffsetKey : DeadLetterHeaders.SourceOffsetKey;
        var countKey = retryTopicsEnabled ? RetryTopicHeaders.FailureCountKey : DeadLetterHeaders.FailureCountKey;
        var sentHeaders = sent.Headers!;
        await Assert.That(sentHeaders.First(h => h.Key == sourceKey).GetValueAsString()).IsEqualTo("orders");
        await Assert.That(sentHeaders.First(h => h.Key == partitionKey).GetValueAsString()).IsEqualTo("0");
        await Assert.That(sentHeaders.First(h => h.Key == offsetKey).GetValueAsString()).IsEqualTo("7");
        await Assert.That(sentHeaders.First(h => h.Key == countKey).GetValueAsString()).IsEqualTo("1");
        await Assert.That(sentHeaders.First(h => h.Key == "application").GetValueAsString()).IsEqualTo("preserved");
        await Assert.That(RetryTopicHeaders.GetSourceTopic(headers)).IsEqualTo("spoofed");
    }

    [Test]
    public async Task CancellationDuringProcessing_ReleasesWithoutAccepting()
    {
        var entered = Signal();
        var consumer = new TestConsumer(Record(0), Record(1));
        await using var service = new TestService(consumer, async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, options: new KafkaShareConsumerServiceOptions { DrainOnShutdown = false });
        await service.StartAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await service.StopAsync(default);
        await Assert.That(consumer.Delivered).IsEqualTo(1);
        await Assert.That(consumer.Acknowledgements.Single().Type).IsEqualTo(AcknowledgeType.Release);
    }

    [Test]
    public async Task CancellationDuringRouting_ReleasesWithoutAccepting()
    {
        var entered = Signal();
        var consumer = new TestConsumer(Record(0));
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<RecordMetadata>(WaitForRoutingCancellationAsync(call.ArgAt<CancellationToken>(1))));
        async Task<RecordMetadata> WaitForRoutingCancellationAsync(CancellationToken token)
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return default;
        }
        await using var service = new TestService(consumer, (_, _) => throw new InvalidOperationException(),
            options: new KafkaShareConsumerServiceOptions { DrainOnShutdown = false }, deadLetterOptions: new DeadLetterOptions())
        { Producer = producer };
        await service.StartAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await service.StopAsync(default);
        await Assert.That(consumer.Acknowledgements.Single().Type).IsEqualTo(AcknowledgeType.Release);
    }

    [Test]
    public async Task Drain_FinishesCurrentRecordWithoutPollingAnother()
    {
        var entered = Signal();
        var finish = Signal();
        var consumer = new TestConsumer(Record(0), Record(1));
        await using var service = new TestService(consumer, async (_, token) =>
        {
            entered.SetResult();
            await finish.Task.WaitAsync(token);
        });
        await service.StartAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var stop = service.StopAsync(default);
        await Assert.That(stop.IsCompleted).IsFalse();
        finish.SetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(consumer.Delivered).IsEqualTo(1);
        await Assert.That(consumer.Acknowledgements.Single().Type).IsEqualTo(AcknowledgeType.Accept);
    }

    [Test]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Shutdown_FinalAcknowledgementFailureFaultsExecution(bool reportCallback, bool throwFromCommit)
    {
        var failure = new InvalidOperationException("Final acknowledgement failed.");
        var consumer = new TestConsumer(Record(0))
        {
            FinalCommitFailure = failure,
            ReportCommitFailure = reportCallback,
            ThrowCommitFailure = throwFromCommit
        };
        await using var service = new TestService(consumer);
        await Assert.That(async () => await RunAsync(service)).Throws<InvalidOperationException>();
        await Assert.That(consumer.Events.Contains("close")).IsTrue();
        await Assert.That(service.ExecuteTask!.Exception!.InnerException).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task Shutdown_RequestBudgetRemainsActiveUntilCallerDeadline()
    {
        var entered = Signal();
        var consumer = new TestConsumer(Record(0));
        await using var service = new TestService(consumer, async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
        using var shutdown = new CancellationTokenSource();
        await service.StartAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var stop = service.StopAsync(shutdown.Token);
        try
        {
            await Assert.That(consumer.RequestCancellationToken.CanBeCanceled).IsTrue();
            await Assert.That(consumer.RequestCancellationToken.IsCancellationRequested).IsFalse();
        }
        finally
        {
            await shutdown.CancelAsync();
        }
        await stop.WaitAsync(TimeSpan.FromSeconds(10));
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(consumer.RequestCancellationToken.IsCancellationRequested).IsTrue();
        await Assert.That(consumer.Acknowledgements.Single().Type).IsEqualTo(AcknowledgeType.Release);
    }

    [Test]
    public async Task Shutdown_UnconfirmedAcknowledgementCancellationRemainsFailure()
    {
        var entered = Signal();
        var finish = Signal();
        var consumer = new TestConsumer(Record(0));
        await using var service = new TestService(consumer, async (_, token) =>
        {
            entered.SetResult();
            await finish.Task.WaitAsync(token);
        });
        await service.StartAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var stop = service.StopAsync(default);
        consumer.ReportAcknowledgementFailure(new OperationCanceledException("Acknowledgement response unavailable."));
        finish.TrySetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(async () => await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10)))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task ShutdownBudget_CancelsCurrentWorkAndDoesNotRaceDisposal()
    {
        var entered = Signal();
        var cancelled = Signal();
        var finish = Signal();
        var consumer = new TestConsumer(Record(0), Record(1));
        await using var service = new TestService(consumer, async (_, token) =>
        {
            entered.SetResult();
            using var registration = token.Register(() => cancelled.TrySetResult());
            await finish.Task;
            token.ThrowIfCancellationRequested();
        }, options: new KafkaShareConsumerServiceOptions { ShutdownTimeout = TimeSpan.FromMilliseconds(50) });
        await service.StartAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await service.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(10));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var disposal = service.DisposeAsync().AsTask();
        await Assert.That(disposal.IsCompleted).IsFalse();
        await Assert.That(consumer.Events.Contains("dispose")).IsFalse();
        finish.SetResult();
        await disposal.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(consumer.Acknowledgements.Single().Type).IsEqualTo(AcknowledgeType.Release);
        await Assert.That(consumer.Events.Last()).IsEqualTo("dispose");
    }

    [Test]
    public async Task LongProcessing_RenewsBeforeAccepting()
    {
        var consumer = new TestConsumer(Record(0));
        await using var service = new TestService(consumer, async (_, token) =>
            await consumer.Renewed.Task.WaitAsync(token),
            options: new KafkaShareConsumerServiceOptions { RenewalInterval = TimeSpan.FromMilliseconds(5) });
        await RunAsync(service);
        await Assert.That(consumer.Acknowledgements.First().Type).IsEqualTo(AcknowledgeType.Renew);
        await Assert.That(consumer.Acknowledgements.Last().Type).IsEqualTo(AcknowledgeType.Accept);
    }

    [Test]
    public async Task RenewalFailure_CancelsProcessingAndReleases()
    {
        var consumer = new TestConsumer(Record(0)) { FailCommit = true };
        await using var service = new TestService(consumer, async (_, token) =>
            await Task.Delay(Timeout.InfiniteTimeSpan, token),
            options: new KafkaShareConsumerServiceOptions { RenewalInterval = TimeSpan.FromMilliseconds(5) });
        await Assert.That(async () => await RunAsync(service)).Throws<InvalidOperationException>();
        await Assert.That(consumer.Acknowledgements.Any(x => x.Type == AcknowledgeType.Accept)).IsFalse();
        await Assert.That(consumer.Acknowledgements.Last().Type).IsEqualTo(AcknowledgeType.Release);
    }

    [Test]
    public async Task AsyncDisposal_WaitsForConsumerCleanupAndRunsOnce()
    {
        var consumer = new TestConsumer { DisposalGate = Signal() };
        var service = new TestService(consumer);
        await RunAsync(service);
        var first = service.DisposeAsync().AsTask();
        var second = service.DisposeAsync().AsTask();
        await Assert.That(first.IsCompleted).IsFalse();
        await Assert.That(second.IsCompleted).IsFalse();
        consumer.DisposalGate.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(consumer.Events.Count(x => x == "dispose")).IsEqualTo(1);
    }

    [Test]
    public async Task ExpiredAcquisition_IsReleasedBeforeProcessing()
    {
        var consumer = new TestConsumer(Record(0))
        {
            AcquisitionTimestamp = System.Diagnostics.Stopwatch.GetTimestamp() - System.Diagnostics.Stopwatch.Frequency * 2,
            AcquisitionLockTimeoutMs = 100
        };
        var processed = false;
        await using var service = new TestService(consumer, (_, _) => { processed = true; return ValueTask.CompletedTask; });
        await Assert.That(async () => await RunAsync(service)).Throws<Dekaf.Errors.KafkaException>();
        await Assert.That(processed).IsFalse();
        await Assert.That(consumer.Acknowledgements.Single().Type).IsEqualTo(AcknowledgeType.Release);
    }

    [Test]
    public async Task InlineAcknowledgementFailure_StopsBeforeProcessingNewRecords()
    {
        var consumer = new TestConsumer(Record(0)) { InlineFailure = new InvalidOperationException("inline acknowledgement failed") };
        var processed = false;
        await using var service = new TestService(consumer, (_, _) => { processed = true; return ValueTask.CompletedTask; });
        await Assert.That(async () => await RunAsync(service)).Throws<InvalidOperationException>();
        await Assert.That(processed).IsFalse();
        await Assert.That(consumer.Acknowledgements.Any(x => x.Type == AcknowledgeType.Accept)).IsFalse();
    }

    [Test]
    public async Task RetryTopicDelay_RenewsAndReleasesOnCancellationWithoutProcessingEarly()
    {
        var headers = new Dekaf.Serialization.Headers().Add(RetryTopicHeaders.DueTimestampMsKey,
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        var consumer = new TestConsumer(Record(0, "orders-retry-1s", headers));
        var processed = false;
        await using var service = new TestService(consumer, (_, _) => { processed = true; return ValueTask.CompletedTask; },
            options: new KafkaShareConsumerServiceOptions { DrainOnShutdown = false, RenewalInterval = TimeSpan.FromMilliseconds(5) },
            deadLetterOptions: new DeadLetterOptions { RetryTopics = new RetryTopicOptions { Delays = [TimeSpan.FromSeconds(1)] } })
        { Producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>() };
        await service.StartAsync(default);
        await consumer.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await service.StopAsync(default);
        await Assert.That(processed).IsFalse();
        await Assert.That(consumer.Acknowledgements.Last().Type).IsEqualTo(AcknowledgeType.Release);
    }

    [Test]
    public async Task RetryDelay_RenewsAndCancelsWithoutAcceptance()
    {
        var consumer = new TestConsumer(Record(0));
        var policy = Substitute.For<IRetryPolicy>();
        policy.GetNextDelay(Arg.Any<int>(), Arg.Any<Exception>()).Returns(TimeSpan.FromHours(1));
        await using var service = new TestService(consumer, (_, _) => throw new InvalidOperationException(), retryPolicy: policy,
            options: new KafkaShareConsumerServiceOptions { DrainOnShutdown = false, RenewalInterval = TimeSpan.FromMilliseconds(5) });
        await service.StartAsync(default);
        await consumer.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await service.StopAsync(default);
        await Assert.That(consumer.Acknowledgements.Any(x => x.Type == AcknowledgeType.Accept)).IsFalse();
        await Assert.That(consumer.Acknowledgements.Last().Type).IsEqualTo(AcknowledgeType.Release);
    }

    [Test]
    public async Task ProducerDisposalFailure_DoesNotSkipConsumerDisposal()
    {
        var consumer = new TestConsumer();
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.DisposeAsync().Returns(ValueTask.FromException(new InvalidOperationException("dispose failed")));
        var service = new TestService(consumer, deadLetterOptions: new DeadLetterOptions()) { Producer = producer };
        await RunAsync(service);
        await service.DisposeAsync();
        await producer.Received(1).DisposeAsync();
        await Assert.That(consumer.Events.Last()).IsEqualTo("dispose");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SingleUseValueTask_IsRegisteredAndConsumedOnce(bool cancel)
    {
        var consumer = new TestConsumer(Record(0));
        var pending = new PendingProcessing();
        await using var service = new TestService(consumer, (_, _) => pending.Operation,
            options: new KafkaShareConsumerServiceOptions
            {
                DrainOnShutdown = !cancel, ShutdownTimeout = TimeSpan.FromMilliseconds(50),
                RenewalInterval = TimeSpan.FromMilliseconds(5)
            });
        await service.StartAsync(default);
        await pending.Registered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (cancel) await service.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(10));
        else await consumer.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        pending.Complete();
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(pending.Registrations).IsEqualTo(1);
        await Assert.That(pending.Consumptions).IsEqualTo(1);
        await Assert.That(consumer.Acknowledgements.Last().Type).IsEqualTo(cancel ? AcknowledgeType.Release : AcknowledgeType.Accept);
    }

    [Test]
    public async Task ThrowingApplicationCancellationCallback_DoesNotPreventCleanup()
    {
        var entered = Signal();
        var consumer = new TestConsumer(Record(0));
        await using var service = new TestService(consumer, async (_, token) =>
        {
            using var registration = token.Register(static () => throw new InvalidOperationException("callback failed"));
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }, options: new KafkaShareConsumerServiceOptions { DrainOnShutdown = false });
        await service.StartAsync(default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await service.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(consumer.Acknowledgements.Last().Type).IsEqualTo(AcknowledgeType.Release);
        await Assert.That(consumer.Events.Contains("close")).IsTrue();
    }

    private sealed class PendingProcessing : IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<bool> _source;
        internal readonly TaskCompletionSource Registered = Signal();
        internal int Registrations;
        internal int Consumptions;
        internal ValueTask Operation => new(this, _source.Version);
        internal void Complete() => _source.SetResult(true);
        public void GetResult(short token) { Consumptions++; _source.GetResult(token); }
        public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (++Registrations != 1) throw new InvalidOperationException("A second ValueTask continuation was registered.");
            _source.OnCompleted(continuation, state, token, flags);
            Registered.TrySetResult();
        }
    }

    private static async Task RunAsync(TestService service)
    {
        await service.StartAsync(default);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
    }

    internal static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal static ShareConsumeResult<string, string> Record(long offset, string topic = "orders",
        IReadOnlyList<Dekaf.Serialization.Header>? headers = null) => new()
    {
        Topic = topic, Partition = 0, Offset = offset, Key = "key", Value = "value", DeliveryCount = 1,
        Headers = headers ?? []
    };

    internal sealed class TestConsumer(params ShareConsumeResult<string, string>[] records) :
        IKafkaShareConsumer<string, string>, IShareConsumerConfiguration, IRawShareRecordAccessor, IHostedShareConsumer
    {
        public readonly List<string> Events = [];
        public readonly List<(ShareConsumeResult<string, string> Record, AcknowledgeType Type)> Acknowledgements = [];
        public readonly TaskCompletionSource Renewed = Signal();
        public TaskCompletionSource? DisposalGate { get; init; }
        public ShareAcknowledgementMode AcknowledgementMode { get; init; } = ShareAcknowledgementMode.Explicit;
        public bool FailCommit { get; init; }
        public Exception? FinalCommitFailure { get; init; }
        public bool ReportCommitFailure { get; init; }
        public bool ThrowCommitFailure { get; init; }
        public byte[]? RawValue { get; init; } = [1, 2, 3];
        public int Delivered { get; private set; }
        public StringSet Subscription { get; private set; } = new HashSet<string>();
        public PartitionSet Assignment => new HashSet<TopicPartition>();
        public string? MemberId => "test-member";
        public int? AcquisitionLockTimeoutMs { get; init; } = 30_000;
        public long? AcquisitionTimestamp { get; init; }
        public long AcquisitionStartedTimestamp => AcquisitionTimestamp ?? System.Diagnostics.Stopwatch.GetTimestamp();
        public Exception? InlineFailure { get; init; }
        private ShareAcknowledgementCommitCallback? _observer;
        public CancellationToken RequestCancellationToken { get; private set; }
        public void ObserveAcknowledgements(ShareAcknowledgementCommitCallback observer)
            => ObserveAcknowledgements(observer, default);
        public void ObserveAcknowledgements(ShareAcknowledgementCommitCallback observer,
            CancellationToken requestCancellationToken)
        {
            _observer = observer;
            RequestCancellationToken = requestCancellationToken;
        }
        internal void ReportAcknowledgementFailure(Exception exception)
            => _observer?.Invoke([new ShareAcknowledgementCommitResult(new TopicPartition("orders", 0), default, exception)]);
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) { Events.Add("initialize"); return ValueTask.CompletedTask; }
        public IKafkaShareConsumer<string, string> Subscribe(params string[] topics) { Subscription = topics.ToHashSet(); Events.Add("subscribe"); return this; }
        public IKafkaShareConsumer<string, string> Unsubscribe() => this;
        public async IAsyncEnumerable<ShareConsumeResult<string, string>> PollAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            if (InlineFailure is not null)
                _observer?.Invoke([new ShareAcknowledgementCommitResult(new TopicPartition("orders", 0), default, InlineFailure)]);
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Delivered++;
                yield return record;
            }
        }
        public void Acknowledge(ShareConsumeResult<string, string> record, AcknowledgeType type = AcknowledgeType.Accept)
        {
            Acknowledgements.Add((record, type));
            if (type == AcknowledgeType.Renew) Renewed.TrySetResult();
        }
        public ValueTask CommitAsync(CancellationToken cancellationToken = default)
        {
            Events.Add("commit");
            if (FinalCommitFailure is { } failure)
            {
                if (ReportCommitFailure) ReportAcknowledgementFailure(failure);
                if (ThrowCommitFailure) return ValueTask.FromException(failure);
            }
            return FailCommit ? ValueTask.FromException(new InvalidOperationException("acknowledgement failed")) : ValueTask.CompletedTask;
        }
        public ValueTask CloseAsync(CancellationToken cancellationToken = default) { Events.Add("close"); return ValueTask.CompletedTask; }
        public async ValueTask DisposeAsync() { Events.Add("dispose"); if (DisposalGate is not null) await DisposalGate.Task; }
        public void EnableRawRecordTracking() { }
        public bool TryGetRawRecord(TopicPartitionOffset record, out byte[]? key, out byte[]? value) { key = []; value = RawValue; return true; }
    }

    internal sealed class TestService(
        IKafkaShareConsumer<string, string> consumer,
        Func<ShareConsumeResult<string, string>, CancellationToken, ValueTask>? process = null,
        KafkaShareConsumerServiceOptions? options = null, IRetryPolicy? retryPolicy = null,
        DeadLetterOptions? deadLetterOptions = null) : KafkaShareConsumerService<string, string>(consumer,
            NullLogger.Instance, deadLetterOptions, retryPolicy, options)
    {
        public MessageFailureDisposition Disposition { get; init; } = MessageFailureDisposition.Retry;
        public ShareMessageFailureContext<string, string>? FailureContext { get; private set; }
        public int RoutingFailures { get; private set; }
        public IKafkaProducer<byte[]?, byte[]?>? Producer { get; init; }
        public string[] SourceTopics { get; init; } = ["orders"];
        protected override IEnumerable<string> Topics => SourceTopics;
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> result, CancellationToken token)
            => process?.Invoke(result, token) ?? ValueTask.CompletedTask;
        protected override ValueTask<MessageFailureDisposition> GetFailureDispositionAsync(ShareMessageFailureContext<string, string> context, CancellationToken token)
        { FailureContext = context; return new(Disposition); }
        protected override ValueTask OnDeadLetterRoutingFailedAsync(Exception exception, ShareConsumeResult<string, string> result, CancellationToken token)
        { RoutingFailures++; return ValueTask.CompletedTask; }
        protected override IKafkaProducer<byte[]?, byte[]?> CreateDeadLetterProducer() => Producer!;
    }
}
