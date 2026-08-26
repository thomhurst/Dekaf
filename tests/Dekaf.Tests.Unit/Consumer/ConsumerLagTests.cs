using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

[NotInParallel]
public sealed class ConsumerLagTests
{
    private static readonly TopicPartition Partition = new("lag-topic", 0);

    [Test]
    public async Task GetCurrentLag_ReturnsNullUntilPositionAndWatermarkAreAvailable()
    {
        await using var consumer = CreateConsumer();

        await Assert.That(consumer.GetCurrentLag(Partition)).IsNull();

        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);

        await Assert.That(consumer.GetCurrentLag(Partition)).IsNull();

        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));

        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(15);
    }

    [Test]
    public async Task GetCurrentLag_UsesCachedWatermarkAcrossSeekAndClampsAtZero()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));

        consumer.Seek(new TopicPartitionOffset(Partition.Topic, Partition.Partition, 20));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(5);

        consumer.Seek(new TopicPartitionOffset(Partition.Topic, Partition.Partition, 30));
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(0);

        consumer.SeekToEnd(Partition);
        await Assert.That(consumer.GetCurrentLag(Partition)).IsNull();
    }

    [Test]
    public async Task GetCurrentLag_ReturnsNullAfterPartitionIsUnassigned()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        SetCachedWatermarks(consumer, new WatermarkOffsets(0, 25));

        consumer.IncrementalUnassign([Partition]);

        await Assert.That(consumer.GetCurrentLag(Partition)).IsNull();
    }

    [Test]
    public async Task GetCurrentLag_ReadCommittedUsesLastStableOffset()
    {
        await using var consumer = CreateConsumer(IsolationLevel.ReadCommitted);
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        var response = new FetchResponsePartition
        {
            PartitionIndex = Partition.Partition,
            HighWatermark = 25,
            LastStableOffset = 17,
            LogStartOffset = 0
        };

        UpdateWatermarksFromFetchResponse(consumer, response);

        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(7);
        await Assert.That(consumer.GetWatermarkOffsets(Partition)!.Value.High).IsEqualTo(25);
    }

    [Test]
    public async Task QueryCurrentLagAsync_UnassignedPartitionCompletesSynchronouslyWithoutNetwork()
    {
        await using var consumer = CreateConsumer();
        SetInitialized(consumer);

        var result = consumer.QueryCurrentLagAsync(Partition);

        await Assert.That(result.IsCompletedSuccessfully).IsTrue();
        await Assert.That(await result).IsNull();
    }

    [Test]
    public async Task QueryCurrentLagAsync_BeforeInitializationThrowsInvalidOperationException()
    {
        await using var consumer = CreateConsumer();

        await Assert.That(() => { _ = consumer.QueryCurrentLagAsync(Partition); })
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task QueryCurrentLagAsync_CanceledTokenThrowsBeforeNetwork()
    {
        await using var consumer = CreateConsumer();
        SetInitialized(consumer);
        consumer.IncrementalAssign([new TopicPartitionOffset(Partition.Topic, Partition.Partition, 10)]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(() => { _ = consumer.QueryCurrentLagAsync(Partition, cancellation.Token); })
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task LagQueries_AfterDisposal_ThrowObjectDisposedException()
    {
        var consumer = CreateConsumer();
        await consumer.DisposeAsync();

        await Assert.That(() => consumer.GetCurrentLag(Partition))
            .Throws<ObjectDisposedException>();
        await Assert.That(async () => await consumer.QueryCurrentLagAsync(Partition))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task LagExtensions_CustomConsumerWithoutCapability_ThrowNotSupportedException()
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>>();

        await Assert.That(() => consumer.GetCurrentLag(Partition))
            .Throws<NotSupportedException>();
        await Assert.That(() => { _ = consumer.QueryCurrentLagAsync(Partition); })
            .Throws<NotSupportedException>();
    }

    private static KafkaConsumer<string, string> CreateConsumer(
        IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted) => new(
        new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "lag-tests",
            IsolationLevel = isolationLevel
        },
        Serializers.String,
        Serializers.String);

    private static void SetCachedWatermarks(
        KafkaConsumer<string, string> consumer,
        WatermarkOffsets watermarks)
    {
        UpdateWatermarksFromFetchResponse(consumer, new FetchResponsePartition
        {
            PartitionIndex = Partition.Partition,
            HighWatermark = watermarks.High,
            LastStableOffset = watermarks.High,
            LogStartOffset = watermarks.Low
        });
    }

    private static void UpdateWatermarksFromFetchResponse(
        KafkaConsumer<string, string> consumer,
        FetchResponsePartition response)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "UpdateWatermarksFromFetchResponse",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("UpdateWatermarksFromFetchResponse method not found");
        method.Invoke(consumer, [Partition.Topic, response]);
    }

    private static void SetInitialized(KafkaConsumer<string, string> consumer)
    {
        var initialized = typeof(KafkaConsumer<string, string>)
            .GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_initialized field not found");
        initialized.SetValue(consumer, true);
    }
}
