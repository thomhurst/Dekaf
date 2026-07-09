using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Microsoft.Extensions.Logging;
using TUnit.Logging.Microsoft;

namespace Dekaf.Tests.Integration;

[ClassDataSource<TransactionFaultKafkaContainer>(Shared = SharedType.PerTestSession)]
[Category("Transaction")]
public sealed class TransactionCoordinatorFaultTests(TransactionFaultKafkaContainer kafka)
{
    [Test]
    public async Task Abort_CoordinatorResponseDelayed_RecoversWithoutVisibility()
    {
        using var testTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
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

        using var endTxnObserver = new EndTxnRequestObserver();
        using var producerLoggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddTUnit(TestContext.Current!);
            builder.AddProvider(endTxnObserver);
        });

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.ProducerBootstrapServers)
            .WithTransactionalId($"transaction-fault-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithLoggerFactory(producerLoggerFactory)
            .BuildAsync(cancellationToken);
        await producer.InitTransactionsAsync(cancellationToken);

        var transaction = producer.BeginTransaction();
        try
        {
            _ = await transaction.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "aborted-key",
                Value = "aborted-value",
            }, cancellationToken);

            var visibilityTask = consumer
                .ConsumeOneAsync(TimeSpan.FromSeconds(30), cancellationToken)
                .AsTask();

            await kafka.AddCoordinatorLatencyAsync(cancellationToken);
            var abortTask = transaction.AbortAsync(cancellationToken).AsTask();

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
        finally
        {
            await kafka.HealCoordinatorAsync();
            await transaction.DisposeAsync();
        }
    }

    private sealed class EndTxnRequestObserver : ILoggerProvider
    {
        private readonly TaskCompletionSource<string> _endTxnSeen =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _sync = new();
        private int? _endTxnCorrelationId;
        private string? _endTxnEndpoint;

        public ILogger CreateLogger(string categoryName) => new ObserverLogger(this);

        public Task<string> WaitForEndTxnAsync(CancellationToken cancellationToken) =>
            _endTxnSeen.Task.WaitAsync(cancellationToken);

        public void Dispose()
        {
        }

        private void Observe<TState>(
            LogLevel logLevel,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Debug
                || state is not IEnumerable<KeyValuePair<string, object?>> properties)
            {
                return;
            }

            int? correlationId = null;
            string? apiKey = null;
            string? host = null;
            int? port = null;
            foreach (var property in properties)
            {
                switch (property.Key)
                {
                    case "ApiKey":
                        apiKey = property.Value?.ToString();
                        break;
                    case "CorrelationId" when property.Value is int correlationValue:
                        correlationId = correlationValue;
                        break;
                    case "Host":
                        host = property.Value as string;
                        break;
                    case "Port" when property.Value is int portValue:
                        port = portValue;
                        break;
                }
            }

            lock (_sync)
            {
                if (apiKey == "EndTxn" && correlationId.HasValue && host is not null && port.HasValue)
                {
                    _endTxnCorrelationId = correlationId;
                    _endTxnEndpoint = $"{host}:{port}";
                    return;
                }

                if (_endTxnCorrelationId == correlationId && _endTxnEndpoint is not null
                    && formatter(state, exception).StartsWith("Request sent, waiting for response", StringComparison.Ordinal))
                {
                    _endTxnSeen.TrySetResult(_endTxnEndpoint);
                }
            }
        }

        private sealed class ObserverLogger(EndTxnRequestObserver observer) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                observer.Observe(logLevel, state, exception, formatter);
            }
        }
    }
}
