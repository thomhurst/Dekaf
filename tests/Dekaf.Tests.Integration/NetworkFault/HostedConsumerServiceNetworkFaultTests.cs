using Dekaf.Consumer;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration.NetworkFault;

/// <summary>
/// Hosted consumer service behaviour when the broker is unreachable while the service starts.
/// Faults go through the consumer proxy only; records are seeded through the producer proxy.
/// </summary>
[ClassDataSource<TransactionFaultKafkaContainer>(Shared = SharedType.PerTestSession)]
[Category("NetworkPartition")]
[NotInParallel("TransactionFaultKafkaContainer")]
public sealed class HostedConsumerServiceNetworkFaultTests(TransactionFaultKafkaContainer kafka)
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(3);

    [Test]
    public async Task BrokerUnreachableAtStartup_RestartsInsteadOfFaultingAndConsumesAfterHeal()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync();
        await SeedAsync(topic, "only-value", cancellationToken);

        var restartObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var serviceLogs = new CapturingLoggerProvider(entry =>
        {
            if (entry.LogLevel == LogLevel.Warning && entry.Message.Contains("restart attempt", StringComparison.Ordinal))
                restartObserved.TrySetResult();
        });

        // The consumer is not initialized yet: the service initializes it while the lane is black-holed.
        var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId($"hosted-startup-fault-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithDefaultApiTimeout(TimeSpan.FromSeconds(3))
            .WithConnectionTimeout(TimeSpan.FromSeconds(2))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
        await using var service = new RecordingConsumerService(
            consumer,
            serviceLogs.CreateLogger(nameof(RecordingConsumerService)),
            topic,
            new KafkaConsumerServiceOptions
            {
                DrainOnShutdown = false,
                PollRetryBackoff = TimeSpan.FromMilliseconds(500),
                MaxPollRetryBackoff = TimeSpan.FromSeconds(1)
            });

        try
        {
            await kafka.AddTimeoutAsync(ToxiproxyLane.Consumer, cancellationToken);
            await service.StartAsync(cancellationToken);

            // Before the fix, the initialization failure faulted ExecuteTask and stopped the host.
            await restartObserved.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
            await Assert.That(service.ExecuteTask!.IsCompleted).IsFalse();
        }
        finally
        {
            await kafka.HealNetworkFaultsAsync(CancellationToken.None);
        }

        var received = await service.Received.Task.WaitAsync(TimeSpan.FromSeconds(90), cancellationToken);
        await Assert.That(received).IsEqualTo("only-value");

        await service.StopAsync(cancellationToken);
        await Assert.That(service.ExecuteTask!.IsCompletedSuccessfully).IsTrue();
    }

    private async Task SeedAsync(string topic, string value, CancellationToken cancellationToken)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.ProducerBootstrapServers)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        _ = await producer.ProduceAsync(topic, "key", value, cancellationToken);
    }

    private sealed class RecordingConsumerService(
        IKafkaConsumer<string, string> consumer,
        ILogger logger,
        string topic,
        KafkaConsumerServiceOptions options)
        : KafkaConsumerService<string, string>(consumer, logger, serviceOptions: options)
    {
        public TaskCompletionSource<string?> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override IEnumerable<string> Topics => [topic];

        protected override ValueTask ProcessAsync(
            ConsumeResult<string, string> result,
            CancellationToken cancellationToken)
        {
            Received.TrySetResult(result.Value);
            return ValueTask.CompletedTask;
        }
    }
}
