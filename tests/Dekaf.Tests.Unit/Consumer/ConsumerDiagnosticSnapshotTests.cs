using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerDiagnosticSnapshotTests
{
    [Test]
    public async Task CaptureDiagnosticSnapshot_IncludesConsumerStallState()
    {
        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                EnableAdaptiveFetchSizing = true,
                AdaptiveFetchSizingOptions = new AdaptiveFetchSizingOptions
                {
                    InitialPartitionFetchBytes = 2_000_000,
                    InitialFetchMaxBytes = 20_000_000,
                    MaxPartitionFetchBytes = 4_000_000,
                    MaxFetchMaxBytes = 40_000_000
                }
            },
            Serializers.String,
            Serializers.String);
        var partition = new TopicPartition("diagnostics-topic", 2);
        consumer.IncrementalAssign(
            [new TopicPartitionOffset(partition.Topic, partition.Partition, 42)]);

        GetField<ConcurrentDictionary<TopicPartition, byte>>(
            consumer,
            "_coordinatorRevokedPartitionsPendingFetchClear")[partition] = 0;
        GetField<ConcurrentDictionary<TopicPartition, int>>(
            consumer,
            "_minimumFetchBufferEpochsByPartition")[partition] = 6;
        GetField<ConcurrentDictionary<TopicPartition, (long EndOffset, int Epoch)>>(
            consumer,
            "_pendingDivergingEpochResets")[partition] = (EndOffset: 123, Epoch: 8);
        SetField(consumer, "_prefetchedBytes", 1_024L);
        SetField(consumer, "_fetchBufferEpoch", 7);
        SetField(consumer, "_minimumFetchBufferEpoch", 5);
        SetField(consumer, "_coordinatorRevokedPartitionsPendingFetchClearMarkerPresent", 1);
        SetField(consumer, "_coordinatorRevokedPartitionsPendingFetchClearPending", 1);
        EnqueuePendingFetch(consumer, PendingFetchData.Create(
            partition.Topic,
            partition.Partition,
            Array.Empty<RecordBatch>()));
        GetField<MpscFetchBuffer>(consumer, "_prefetchBuffer").TryWrite(PendingFetchData.Create(
            partition.Topic,
            partition.Partition,
            Array.Empty<RecordBatch>()));

        var snapshot = consumer.CaptureDiagnosticSnapshot();

        await Assert.That(snapshot.Assignment).HasSingleItem();
        await Assert.That(snapshot.FetchPositions).Contains(item =>
            item.Topic == partition.Topic && item.Partition == partition.Partition && item.Offset == 42);
        await Assert.That(snapshot.PrefetchedBytes).IsEqualTo(1_024);
        await Assert.That(snapshot.PendingFetchDepth).IsEqualTo(1);
        await Assert.That(snapshot.PrefetchBufferDepth).IsEqualTo(1);
        await Assert.That(snapshot.PrefetchDepth).IsEqualTo(2);
        await Assert.That(snapshot.PendingRevocations).HasSingleItem();
        await Assert.That(snapshot.PendingRevocationMarkerPresent).IsTrue();
        await Assert.That(snapshot.PendingRevocationClearPending).IsTrue();
        await Assert.That(snapshot.FetchBufferEpoch).IsEqualTo(7);
        await Assert.That(snapshot.MinimumFetchBufferEpoch).IsEqualTo(5);
        await Assert.That(snapshot.MinimumFetchBufferEpochsByPartition).HasSingleItem();
        await Assert.That(snapshot.PendingDivergingEpochResets).Contains(item =>
            item.Topic == partition.Topic && item.Partition == partition.Partition &&
            item.EndOffset == 123 && item.Epoch == 8);
        await Assert.That(snapshot.AdaptivePartitionFetchBytes).IsEqualTo(2_000_000);
        await Assert.That(snapshot.AdaptiveFetchMaxBytes).IsEqualTo(20_000_000);
    }

    private static TField GetField<TField>(KafkaConsumer<string, string> consumer, string name) =>
        (TField)(typeof(KafkaConsumer<string, string>)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(consumer)
            ?? throw new InvalidOperationException($"Could not read {name}."));

    private static void SetField<TField>(KafkaConsumer<string, string> consumer, string name, TField value) =>
        (typeof(KafkaConsumer<string, string>)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find {name}."))
        .SetValue(consumer, value);

    private static void EnqueuePendingFetch(
        KafkaConsumer<string, string> consumer,
        PendingFetchData pending) =>
        (typeof(KafkaConsumer<string, string>)
            .GetMethod("EnqueuePendingFetch", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find EnqueuePendingFetch."))
        .Invoke(consumer, [pending]);
}
