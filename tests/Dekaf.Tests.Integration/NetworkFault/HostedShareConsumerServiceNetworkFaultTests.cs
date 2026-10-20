using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Logging;

namespace Dekaf.Tests.Integration.NetworkFault;

/// <summary>
/// Hosted share consumer service behaviour when the broker is unreachable while the service starts.
/// Faults go through the consumer proxy only; records are seeded through the producer proxy.
/// </summary>
[ClassDataSource<ShareFaultKafkaContainer>(Shared = SharedType.PerTestSession)]
[Category("NetworkPartition")]
[NotInParallel("ShareFaultKafkaContainer")]
public sealed class HostedShareConsumerServiceNetworkFaultTests(ShareFaultKafkaContainer kafka)
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(4);

    [Test]
    public async Task BrokerUnreachableAtStartup_RestartsInsteadOfFaultingAndConsumesAfterHeal()
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync(partitions: 1);
        await SeedAsync(topic, "only-value", cancellationToken);

        // The consumer is not initialized yet: the service initializes it while the lane is
        // black-holed, so initialization fails once its 60 s budget expires.
        var consumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId($"hosted-share-startup-fault-{Guid.NewGuid():N}")
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRequestTimeoutMs(5_000)
            .WithConnectionTimeout(TimeSpan.FromSeconds(2))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
        await using var service = new RecordingShareConsumerService(consumer, topic);

        try
        {
            await kafka.AddTimeoutAsync(ToxiproxyLane.Consumer, cancellationToken);
            await service.StartAsync(cancellationToken);

            // Before the fix, the initialization failure faulted ExecuteTask and stopped the host.
            await service.FailureObserved.Task.WaitAsync(TimeSpan.FromSeconds(120), cancellationToken);
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

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task MemberExpiredWhileProcessing_KeepsRunningAndConsumesAfterRejoin(bool hold)
    {
        using var testTimeout = new CancellationTokenSource(TestTimeout);
        var cancellationToken = testTimeout.Token;
        var topic = await kafka.CreateTestTopicAsync(partitions: 1);
        var groupId = $"hosted-share-member-expiry-{Guid.NewGuid():N}";
        await kafka.UseShortGroupSessionAsync(groupId, cancellationToken, shareGroup: true);

        var consumer = Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(kafka.ConsumerBootstrapServers)
            .WithGroupId(groupId)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRequestTimeoutMs(5_000)
            .WithConnectionTimeout(TimeSpan.FromSeconds(2))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
        await using var service = new RecordingShareConsumerService(consumer, topic)
        {
            HoldValue = hold ? "first" : null
        };

        await service.StartAsync(cancellationToken);
        await SeedAsync(topic, "first", cancellationToken);
        if (hold)
            await service.HoldStarted.Task.WaitAsync(TimeSpan.FromSeconds(90), cancellationToken);
        else
            await service.WaitForAsync("first").WaitAsync(TimeSpan.FromSeconds(90), cancellationToken);

        // The handler is still running when the member expires, so the record's acknowledgement
        // (and any lock renewal) is sent for an acquisition the expired membership no longer owns.
        await kafka.ExpireConsumerMemberAsync(cancellationToken);
        service.ReleaseHold.TrySetResult();

        await SeedAsync(topic, "second", cancellationToken);
        // A faulted service never receives the record, so report the fault instead of a timeout.
        var second = service.WaitForAsync("second");
        await Task.WhenAny(second, service.ExecuteTask!).WaitAsync(TimeSpan.FromSeconds(90), cancellationToken);
        // Awaiting a faulted service rethrows the failure that stopped it.
        if (service.ExecuteTask!.IsCompleted)
            await service.ExecuteTask;
        await Assert.That(service.ExecuteTask.IsCompleted).IsFalse();
        await second;

        // Awaiting surfaces the fault itself if shutdown failed.
        await service.StopAsync(cancellationToken);
        await service.ExecuteTask!;
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

    private sealed class RecordingShareConsumerService(IKafkaShareConsumer<string, string> consumer, string topic)
        : KafkaShareConsumerService<string, string>(consumer,
            GlobalTestSetup.GetLoggerFactory().CreateLogger<RecordingShareConsumerService>(),
            serviceOptions: new KafkaShareConsumerServiceOptions
            {
                PollRetryBackoff = TimeSpan.FromMilliseconds(500),
                MaxPollRetryBackoff = TimeSpan.FromSeconds(1)
            })
    {
        public TaskCompletionSource FailureObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<string?> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override IEnumerable<string> Topics => [topic];

        private readonly ConcurrentDictionary<string, TaskCompletionSource> _values = new(StringComparer.Ordinal);

        public Task WaitForAsync(string value) => GetSignal(value).Task;

        private TaskCompletionSource GetSignal(string value) => _values.GetOrAdd(
            value,
            static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        /// <summary>A record with this value blocks its handler until <see cref="ReleaseHold"/>.</summary>
        public string? HoldValue { get; init; }

        public TaskCompletionSource HoldStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseHold { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async ValueTask ProcessAsync(
            ShareConsumeResult<string, string> result,
            CancellationToken cancellationToken)
        {
            Received.TrySetResult(result.Value);
            if (result.Value is not null && result.Value == HoldValue && HoldStarted.TrySetResult())
                await ReleaseHold.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (result.Value is not null)
                GetSignal(result.Value).TrySetResult();
        }

        protected override ValueTask OnErrorAsync(
            Exception exception,
            ShareConsumeResult<string, string>? result,
            CancellationToken cancellationToken)
        {
            FailureObserved.TrySetResult();
            return base.OnErrorAsync(exception, result, cancellationToken);
        }
    }
}
