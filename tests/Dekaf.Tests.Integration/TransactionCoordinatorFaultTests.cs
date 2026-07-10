using System.Runtime.ExceptionServices;
using Dekaf.Consumer;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Microsoft.Extensions.Logging;
using TUnit.Logging.Microsoft;

namespace Dekaf.Tests.Integration;

[ClassDataSource<TransactionFaultKafkaContainer>(Shared = SharedType.PerTestSession)]
[Category("Transaction")]
[NotInParallel("TransactionFaultKafkaContainer")]
public sealed class TransactionCoordinatorFaultTests(TransactionFaultKafkaContainer kafka)
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(2);

    [Test]
    public async Task Abort_CoordinatorResponseDelayed_RecoversWithoutVisibility()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync();

        await using (var seedProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.ProducerBootstrapServers)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken))
        {
            _ = await seedProducer.ProduceAsync(topic, "seed-key", "seed-value", cancellationToken);
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId($"transaction-fault-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        var seed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken);
        await Assert.That(seed).IsNotNull();
        await Assert.That(seed!.Value.Value).IsEqualTo("seed-value");

        var endTxnObserver = new EndTxnRequestObserver();
        using var capturedLogs = new CapturingLoggerProvider(endTxnObserver.Observe);
        using var producerLoggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddTUnit(TestContext.Current!);
            builder.AddProvider(capturedLogs);
        });

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.ProducerBootstrapServers)
            .WithTransactionalId($"transaction-fault-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithLoggerFactory(producerLoggerFactory)
            .BuildAsync(cancellationToken);
        await producer.InitTransactionsAsync(cancellationToken);

        var transaction = producer.BeginTransaction();
        using var backgroundCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<ConsumeResult<string, string>?>? visibilityTask = null;
        Task? abortTask = null;
        Exception? testFailure = null;
        try
        {
            _ = await transaction.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "aborted-key",
                Value = "aborted-value",
            }, cancellationToken);

            visibilityTask = consumer
                .ConsumeOneAsync(TimeSpan.FromSeconds(30), backgroundCancellation.Token)
                .AsTask();

            await kafka.AddCoordinatorLatencyAsync(cancellationToken);
            abortTask = transaction.AbortAsync(backgroundCancellation.Token).AsTask();

            var endTxnEndpoint = await endTxnObserver.WaitForEndTxnAsync(cancellationToken);
            await Assert.That(endTxnEndpoint).IsEqualTo(kafka.ProducerBootstrapServers);

            await Assert.That(abortTask.IsCompleted).IsFalse();
            await Assert.That(visibilityTask.IsCompleted).IsFalse();

            await kafka.HealCoordinatorAsync(cancellationToken);
            await abortTask.WaitAsync(cancellationToken);

            await using (var recoveryTransaction = producer.BeginTransaction())
            {
                _ = await recoveryTransaction.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = "recovery-key",
                    Value = "recovery-value",
                }, cancellationToken);
                await recoveryTransaction.CommitAsync(cancellationToken);
            }

            var visible = await visibilityTask.WaitAsync(cancellationToken);
            await Assert.That(visible).IsNotNull();
            await Assert.That(visible!.Value.Key).IsEqualTo("recovery-key");
            await Assert.That(visible.Value.Value).IsEqualTo("recovery-value");
        }
        catch (Exception ex)
        {
            testFailure = ex;
        }

        var cleanup = new CleanupFailureCollector();
        await cleanup.CaptureTaskAsync("coordinator healing", () => kafka.HealCoordinatorAsync());
        cleanup.Capture("background task cancellation", backgroundCancellation.Cancel);

        if (abortTask is not null)
        {
            await cleanup.CaptureTaskAsync(
                "abort task observation",
                () => ObserveBackgroundTaskAsync(abortTask, backgroundCancellation.Token));
        }

        if (visibilityTask is not null)
        {
            await cleanup.CaptureTaskAsync(
                "visibility task observation",
                () => ObserveBackgroundTaskAsync(visibilityTask, backgroundCancellation.Token));
        }

        await cleanup.CaptureValueTaskAsync("transaction disposal", transaction.DisposeAsync);

        if (testFailure is not null)
        {
            cleanup.WriteFailuresToConsole();
            ExceptionDispatchInfo.Capture(testFailure).Throw();
        }

        cleanup.ThrowIfAny();
    }

    private static async Task ObserveBackgroundTaskAsync(Task task, CancellationToken cleanupCancellation)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cleanupCancellation.IsCancellationRequested)
        {
            // Expected when cleanup cancels an incomplete background operation.
        }
    }

    private sealed class EndTxnRequestObserver
    {
        private readonly TaskCompletionSource<string> _endTxnSeen =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _sync = new();
        private int? _endTxnCorrelationId;
        private string? _endTxnEndpoint;

        public Task<string> WaitForEndTxnAsync(CancellationToken cancellationToken) =>
            _endTxnSeen.Task.WaitAsync(cancellationToken);

        public void Observe(CapturedLogEntry entry)
        {
            if (entry.CategoryName != typeof(KafkaConnection).FullName
                || entry.LogLevel != LogLevel.Debug
                || !entry.TryGetProperty<int>("CorrelationId", out var correlationId))
            {
                return;
            }

            lock (_sync)
            {
                if (entry.EventId.Id == KafkaConnection.SendingRequestEventId
                    && entry.TryGetProperty<ApiKey>("ApiKey", out var apiKey)
                    && apiKey == ApiKey.EndTxn
                    && entry.TryGetProperty<string>("Host", out var host)
                    && host is not null
                    && entry.TryGetProperty<int>("Port", out var port))
                {
                    _endTxnCorrelationId = correlationId;
                    _endTxnEndpoint = $"{host}:{port}";
                    return;
                }

                if (_endTxnCorrelationId == correlationId && _endTxnEndpoint is not null
                    && entry.EventId.Id == KafkaConnection.RequestSentWaitingForResponseEventId)
                {
                    _endTxnSeen.TrySetResult(_endTxnEndpoint);
                }
            }
        }
    }
}
