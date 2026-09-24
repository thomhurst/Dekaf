using System.Runtime.CompilerServices;
using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Extensions.Hosting;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

#if NET10_0_OR_GREATER
using StringSet = System.Collections.Generic.IReadOnlySet<string>;
using PartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#else
using StringSet = System.Collections.Generic.IReadOnlyCollection<string>;
using PartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#endif

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public class HostedShareConsumerPollRetryTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task JoinTimeout_DoesNotStopHost_AndNextPollConsumesFromKafka()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"hosted-share-retry-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        }, cancellationToken: timeout.Token);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(timeout.Token);
        await producer.ProduceAsync(topic, "key", "queued-before-join", timeout.Token);
        var consumer = new JoinTimeoutConsumer(Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(group)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit).Build());
        await using var worker = new Worker(consumer, topic);
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddSingleton<IHostedService>(worker)).Build();
        await host.StartAsync(timeout.Token);
        await worker.Processed.Task.WaitAsync(timeout.Token);
        await Assert.That(host.Services.GetRequiredService<IHostApplicationLifetime>()
            .ApplicationStopping.IsCancellationRequested).IsFalse();
        await Assert.That(worker.Failure).IsTypeOf<KafkaTimeoutException>();
        await Assert.That(consumer.PollAttempts).IsEqualTo(2);
        await host.StopAsync(timeout.Token);
        await worker.ExecuteTask!.WaitAsync(timeout.Token);
    }

    private sealed class Worker(IKafkaShareConsumer<string, string> consumer, string topic)
        : KafkaShareConsumerService<string, string>(consumer, NullLogger.Instance,
            serviceOptions: new KafkaShareConsumerServiceOptions { PollRetryBackoff = TimeSpan.FromMilliseconds(1) })
    {
        public TaskCompletionSource Processed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? Failure { get; private set; }
        protected override IEnumerable<string> Topics => [topic];
        protected override ValueTask ProcessAsync(ShareConsumeResult<string, string> record, CancellationToken token)
        {
            if (record.Value != "queued-before-join") throw new InvalidOperationException("Unexpected record.");
            Processed.TrySetResult();
            return ValueTask.CompletedTask;
        }
        protected override ValueTask OnErrorAsync(Exception exception, ShareConsumeResult<string, string>? record, CancellationToken token)
        { Failure = exception; return ValueTask.CompletedTask; }
    }

    // Inject exactly the coordinator's timeout at the poll boundary; subsequent operations
    // use the real client and broker. This avoids a timing-dependent broker outage.
    private sealed class JoinTimeoutConsumer(IKafkaShareConsumer<string, string> inner)
        : IKafkaShareConsumer<string, string>, IShareConsumerConfiguration, IHostedShareConsumer
    {
        public int PollAttempts { get; private set; }
        public long AcquisitionStartedTimestamp => ((IHostedShareConsumer)inner).AcquisitionStartedTimestamp;
        public void ObserveAcknowledgements(ShareAcknowledgementCommitCallback observer)
            => ((IHostedShareConsumer)inner).ObserveAcknowledgements(observer);
        public void ObserveAcknowledgements(ShareAcknowledgementCommitCallback observer, CancellationToken requestCancellationToken)
            => ((IHostedShareConsumer)inner).ObserveAcknowledgements(observer, requestCancellationToken);
        public ShareAcknowledgementMode AcknowledgementMode => ShareAcknowledgementMode.Explicit;
        public StringSet Subscription => inner.Subscription;
        public PartitionSet Assignment => inner.Assignment;
        public string? MemberId => inner.MemberId;
        public int? AcquisitionLockTimeoutMs => inner.AcquisitionLockTimeoutMs;
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => inner.InitializeAsync(cancellationToken);
        public IKafkaShareConsumer<string, string> Subscribe(params string[] topics) { inner.Subscribe(topics); return this; }
        public IKafkaShareConsumer<string, string> Unsubscribe() { inner.Unsubscribe(); return this; }
        public async IAsyncEnumerable<ShareConsumeResult<string, string>> PollAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (++PollAttempts == 1)
                throw new KafkaTimeoutException(TimeoutKind.Rebalance, TimeSpan.FromSeconds(45),
                    TimeSpan.FromSeconds(45), "Failed to join share group within timeout");
            await foreach (var record in inner.PollAsync(cancellationToken)) yield return record;
        }
        public void Acknowledge(ShareConsumeResult<string, string> record, AcknowledgeType type = AcknowledgeType.Accept)
            => inner.Acknowledge(record, type);
        public ValueTask CommitAsync(CancellationToken cancellationToken = default) => inner.CommitAsync(cancellationToken);
        public ValueTask CloseAsync(CancellationToken cancellationToken = default) => inner.CloseAsync(cancellationToken);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
