#if NET10_0_OR_GREATER
using StringSet = System.Collections.Generic.IReadOnlySet<string>;
using PartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#else
using StringSet = System.Collections.Generic.IReadOnlyCollection<string>;
using PartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#endif
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Producer;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Hosting;

public sealed partial class KafkaConsumerServiceTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RetryPostponement_RebalanceInvalidatesOldOwnershipAndPreservesOtherPartitions(bool lost)
    {
        var consumer = new RebalanceTestConsumer();
        var registered = new TaskCompletionSource<IRebalanceListener>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = Substitute.For<IDisposable>();
        consumer.Register = listener => { registered.TrySetResult(listener); return registration; };
        consumer.Inner.ConsumeAsync(Arg.Any<CancellationToken>()).Returns(call => WaitForCancellation(call.Arg<CancellationToken>()));
        await using var service = new TestConsumerService(consumer, ["orders"],
            options: new Dekaf.Extensions.Hosting.KafkaConsumerServiceOptions { DrainOnShutdown = false },
            deadLetterOptions: new DeadLetterOptions
            {
                RetryTopics = new RetryTopicOptions { Delays = [TimeSpan.FromSeconds(1)] }
            });
        using var stopping = new CancellationTokenSource();
        await service.StartAsync(stopping.Token);
        try
        {
            var listener = await registered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var headers = RetryTopicHeaders.Build(CreateResult("orders"), 1, TimeSpan.FromSeconds(1),
                DateTimeOffset.UtcNow.AddMinutes(5)).ToList();
            var revoked = new TopicPartition("orders-retry-1s", 0);
            var retained = new TopicPartition("orders-retry-1s", 1);
            var result = CreateResult(revoked.Topic, revoked.Partition, 42, headers);
            await ProcessWithRetriesAsync(service, result, stopping.Token);
            await ProcessWithRetriesAsync(service, CreateResult(retained.Topic, retained.Partition, 42, headers), stopping.Token);

            var serviceType = typeof(Dekaf.Extensions.Hosting.KafkaConsumerService<string, string>);
            var pending = (System.Collections.IDictionary)serviceType.GetField("_retryTopicPostponements",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(service)!;
            var oldPostponement = pending[revoked]!;
            var retainedPostponement = pending[retained]!;
            if (lost)
                await listener.OnPartitionsLostAsync([revoked], CancellationToken.None);
            else
                await listener.OnPartitionsRevokedAsync([revoked], CancellationToken.None);
            await listener.OnPartitionsAssignedAsync([revoked], CancellationToken.None);
            await ProcessWithRetriesAsync(service, result, stopping.Token);

            var complete = serviceType.GetMethod("TryCompleteRetryTopicPostponement",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            await Assert.That((bool)complete.Invoke(service, [revoked, oldPostponement])!).IsFalse();
            consumer.Partitions.DidNotReceive().Resume(Arg.Any<TopicPartition[]>());
            await Assert.That(pending.Contains(revoked)).IsTrue();
            await Assert.That((bool)complete.Invoke(service, [retained, retainedPostponement])!).IsTrue();
            await Assert.That((bool)complete.Invoke(service, [revoked, pending[revoked]])!).IsTrue();
            consumer.Partitions.Received(1).Resume(Arg.Is<TopicPartition[]>(partitions => partitions.Length == 1 && partitions[0] == revoked));
            consumer.Partitions.Received(1).Resume(Arg.Is<TopicPartition[]>(partitions => partitions.Length == 1 && partitions[0] == retained));
            consumer.Partitions.Received(2).Pause(Arg.Is<TopicPartition[]>(partitions => partitions.Length == 1 && partitions[0] == revoked));
        }
        finally
        {
            await stopping.CancelAsync();
            await service.StopAsync(CancellationToken.None);
        }
        registration.Received(1).Dispose();
    }

    [Test]
    [Arguments(false, 0)]
    [Arguments(false, 1)]
    [Arguments(false, 2)]
    [Arguments(true, 0)]
    [Arguments(true, 1)]
    [Arguments(true, 2)]
    public async Task FailureRouting_PreservesNullEmptyAndNonemptyBytes(bool retry, int payloadKind)
    {
        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions { BootstrapServers = ["localhost:9092"], OffsetCommitMode = OffsetCommitMode.Manual },
            Serializers.String, Serializers.String);
        ReadOnlyMemory<byte> bytes = payloadKind switch
        {
            0 => default,
            1 => Array.Empty<byte>(),
            _ => new byte[] { 0, 128, 255 }
        };
        ((IRawRecordAccessor)consumer).EnableRawRecordTracking();
        var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        consumer.GetType().GetField("_currentRawKey", fields)!.SetValue(consumer, bytes);
        consumer.GetType().GetField("_currentRawValue", fields)!.SetValue(consumer, bytes);
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        ProducerMessage<byte[]?, byte[]?>? sent = null;
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(call => { sent = call.ArgAt<ProducerMessage<byte[]?, byte[]?>>(0); return new ValueTask<RecordMetadata>(default(RecordMetadata)); });
        await using var service = new FailingConsumerService(consumer, ["orders"], new DeadLetterOptions
        {
            RetryTopics = retry ? new RetryTopicOptions { Delays = [TimeSpan.FromSeconds(1)] } : null
        });
        SetDlqProducer(service, producer);

        await ProcessWithRetriesAsync(service, CreateResult("orders"), CancellationToken.None);

        await Assert.That(sent).IsNotNull();
        await Assert.That(sent!.Topic).IsEqualTo(retry ? "orders-retry-1s" : "orders.DLQ");
        if (payloadKind == 0)
        {
            await Assert.That(sent.Key).IsNull();
            await Assert.That(sent.Value).IsNull();
        }
        else
        {
            await Assert.That(sent.Key).IsNotNull();
            await Assert.That(sent.Value).IsNotNull();
            await Assert.That(sent.Key!.AsSpan().SequenceEqual(bytes.Span)).IsTrue();
            await Assert.That(sent.Value!.AsSpan().SequenceEqual(bytes.Span)).IsTrue();
        }
    }

    [Test]
    [Arguments("9223372036854775807")]
    [Arguments("-9223372036854775808")]
    [Arguments("253402300800000")]
    [Arguments("-62135596800001")]
    [Arguments("invalid")]
    public async Task InvalidRetryDueTimestamp_DoesNotBypassProcessing(string timestamp)
    {
        await using var service = new TestConsumerService(CreateConsumerSubstitute(), ["orders"],
            deadLetterOptions: new DeadLetterOptions
            {
                RetryTopics = new RetryTopicOptions { Delays = [TimeSpan.FromSeconds(1)] }
            });
        var result = CreateResult("orders-retry-1s", headers: [new Header(RetryTopicHeaders.DueTimestampMsKey, System.Text.Encoding.UTF8.GetBytes(timestamp))]);

        await ProcessWithRetriesAsync(service, result, CancellationToken.None);

        await Assert.That(service.ProcessedMessages).Count().IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RetryFailureCount_AtMaximum_SaturatesWhenRoutedToDeadLetter(bool retry)
    {
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        ProducerMessage<byte[]?, byte[]?>? sent = null;
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(call => { sent = call.ArgAt<ProducerMessage<byte[]?, byte[]?>>(0); return new ValueTask<RecordMetadata>(default(RecordMetadata)); });
        await using var service = new FailingConsumerService(CreateConsumerSubstitute(), ["orders"], new DeadLetterOptions
        {
            RetryTopics = retry ? new RetryTopicOptions { Delays = [TimeSpan.FromSeconds(1)] } : null
        });
        SetDlqProducer(service, producer);
        var result = CreateResult("orders", headers: [new Header(RetryTopicHeaders.FailureCountKey, "2147483647"u8.ToArray())]);

        await ProcessWithRetriesAsync(service, result, CancellationToken.None);

        await Assert.That(sent).IsNotNull();
        await Assert.That(sent!.Topic).IsEqualTo("orders.DLQ");
        await Assert.That(sent.Headers!.GetFirstAsString(DeadLetterHeaders.FailureCountKey)).IsEqualTo("2147483647");
    }
    private sealed class RebalanceTestConsumer : IKafkaConsumer<string, string>, IConsumerRebalanceEventSource,
        IConsumerOffsetStoreTimingConfiguration
    {
        internal IKafkaConsumer<string, string> Inner { get; } = Substitute.For<IKafkaConsumer<string, string>>();
        internal Func<IRebalanceListener, IDisposable> Register { get; set; } = null!;
        public IDisposable RegisterRuntimeRebalanceListener(IRebalanceListener listener) => Register(listener);
        public OffsetCommitMode OffsetCommitMode => OffsetCommitMode.Manual;
        public bool EnableAutoOffsetStore => false;
        public bool HasConsumerGroup => false;
        public bool StoresOffsetsOnDelivery => false;
        public StringSet Subscription => Inner.Subscription;
        public string? SubscriptionPattern => Inner.SubscriptionPattern;
        public PartitionSet Assignment => Inner.Assignment;
        public PartitionSet Paused => Inner.Paused;
        public string? MemberId => Inner.MemberId;
        public ConsumerGroupMetadata? ConsumerGroupMetadata => Inner.ConsumerGroupMetadata;
        public IConsumerPositions Positions => Inner.Positions;
        public IConsumerPartitions Partitions => Inner.Partitions;
        public IConsumerOffsets Offsets => Inner.Offsets;
        public void Subscribe(params string[] topics) => Inner.Subscribe(topics);
        public void Subscribe(Func<string, bool> filter) => Inner.Subscribe(filter);
        public void SubscribePattern(string pattern) => Inner.SubscribePattern(pattern);
        public void Unsubscribe() => Inner.Unsubscribe();
        public IAsyncEnumerable<ConsumeResult<string, string>> ConsumeAsync(CancellationToken cancellationToken = default) => Inner.ConsumeAsync(cancellationToken);
        public ValueTask<ConsumeResult<string, string>?> ConsumeOneAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => Inner.ConsumeOneAsync(timeout, cancellationToken);
        public IAsyncEnumerable<ConsumeBatch<string, string>> ConsumeBatchAsync(CancellationToken cancellationToken = default) => Inner.ConsumeBatchAsync(cancellationToken);
        public IAsyncEnumerable<ConsumeRawBatch> ConsumeRawBatchAsync(CancellationToken cancellationToken = default) => Inner.ConsumeRawBatchAsync(cancellationToken);
        public void RegisterMetricForSubscription(Dekaf.Telemetry.ApplicationTelemetryMetric metric) => Inner.RegisterMetricForSubscription(metric);
        public void UnregisterMetricFromSubscription(string name) => Inner.UnregisterMetricFromSubscription(name);
        public ValueTask CommitAsync(CancellationToken cancellationToken = default) => Inner.CommitAsync(cancellationToken);
        public ValueTask CommitAsync(IEnumerable<TopicPartitionOffset> offsets, CancellationToken cancellationToken = default) => Inner.CommitAsync(offsets, cancellationToken);
        public void StoreOffset(ConsumeResult<string, string> result) => Inner.StoreOffset(result);
        public void StoreOffset(TopicPartitionOffset offset) => Inner.StoreOffset(offset);
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => Inner.InitializeAsync(cancellationToken);
        public ValueTask CloseAsync(CancellationToken cancellationToken = default) => Inner.CloseAsync(cancellationToken);
        public ValueTask CloseAsync(ConsumerCloseOptions options, CancellationToken cancellationToken = default) => Inner.CloseAsync(options, cancellationToken);
        public ValueTask DisposeAsync() => Inner.DisposeAsync();
    }

}
