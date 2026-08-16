using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class SnapshotConsumptionTests
{
    private static readonly TopicPartition Partition0 = new("orders", 0);
    private static readonly TopicPartition Partition1 = new("orders", 1);

    [Test]
    public async Task SnapshotState_CompletesOnlyAfterEveryCapturedPartition()
    {
        var state = new SnapshotConsumeState(new Dictionary<TopicPartition, long>
        {
            [Partition0] = 10,
            [Partition1] = 20
        });

        await Assert.That(state.IsComplete).IsFalse();
        await Assert.That(state.Complete(Partition0)).IsTrue();
        await Assert.That(state.Complete(Partition0)).IsFalse();
        await Assert.That(state.IsComplete).IsFalse();
        await Assert.That(state.Complete(Partition1)).IsTrue();
        await Assert.That(state.IsComplete).IsTrue();
    }

    [Test]
    public async Task SnapshotState_EndMarkerRequiresCapturedVisibleEndAndQueuesOnce()
    {
        var state = new SnapshotConsumeState(new Dictionary<TopicPartition, long>
        {
            [Partition0] = 42
        });

        var beforeBound = state.TryQueueEndMarker(Partition0, 41, out var beforeEnd);
        var atBound = state.TryQueueEndMarker(Partition0, 42, out var end);
        var duplicate = state.TryQueueEndMarker(Partition0, 100, out _);

        await Assert.That(beforeBound).IsFalse();
        await Assert.That(beforeEnd).IsEqualTo(42L);
        await Assert.That(atBound).IsTrue();
        await Assert.That(end).IsEqualTo(42L);
        await Assert.That(duplicate).IsFalse();
    }

    [Test]
    public async Task SnapshotState_AssignmentChangeThrows()
    {
        var assignment = new HashSet<TopicPartition> { Partition0 };
        var paused = new HashSet<TopicPartition>();
        var state = new SnapshotConsumeState(new Dictionary<TopicPartition, long>
        {
            [Partition0] = 10
        }, assignment, paused);

        await Assert.That(() => state.ThrowIfConsumerStateChanged(
                new HashSet<TopicPartition> { Partition1 },
                paused))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(RecordBatchAttributes.IsControlBatch, false)]
    [Arguments(RecordBatchAttributes.IsTransactional, true)]
    public async Task ExhaustedFilteredBatch_AdvancesConsumerPosition(
        RecordBatchAttributes attributes,
        bool includeAbortedTransaction)
    {
        const long baseOffset = 25;
        const long producerId = 7;
        var batch = new RecordBatch
        {
            BaseOffset = baseOffset,
            LastOffsetDelta = 2,
            ProducerId = producerId,
            Attributes = attributes,
            Records =
            [
                new Record { OffsetDelta = 0 },
                new Record { OffsetDelta = 1 },
                new Record { OffsetDelta = 2 }
            ]
        };
        var abortedTransactions = includeAbortedTransaction
            ? new[] { new AbortedTransaction { ProducerId = producerId, FirstOffset = baseOffset } }
            : null;
        using var pending = PendingFetchData.Create(
            Partition0.Topic,
            Partition0.Partition,
            [batch],
            abortedTransactions);
        pending.EagerParseAll();

        await Assert.That(pending.MoveNext()).IsFalse();
        await Assert.That(pending.IsExhausted).IsTrue();

        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1
            },
            Serializers.String,
            Serializers.String);
        var flushed = InvokeFlushConsumedPositions(consumer, pending);

        await Assert.That(flushed).IsTrue();
        await Assert.That(consumer.GetPosition(Partition0)).IsEqualTo(baseOffset + 3);
    }

    [Test]
    public async Task SnapshotEndMarker_CarriesExactCapturedBound()
    {
        using var marker = PendingFetchData.CreateSnapshotEnd("orders", 3, 123);

        await Assert.That(marker.IsSnapshotEnd).IsTrue();
        await Assert.That(marker.SnapshotEndOffset).IsEqualTo(123L);
        await Assert.That(marker.TopicPartition).IsEqualTo(new TopicPartition("orders", 3));
        await Assert.That(marker.MoveNext()).IsFalse();
    }

    [Test]
    public async Task SnapshotBound_SkipsPostBoundBatchWithoutRecordScan()
    {
        var records = new ThrowOnAccessRecordList(count: 1024);
        var batch = new RecordBatch
        {
            BaseOffset = 100,
            LastOffsetDelta = records.Count - 1,
            Records = records
        };
        using var pending = PendingFetchData.Create(
            Partition0.Topic,
            Partition0.Partition,
            [batch],
            stopAtOffsetExclusive: 50);
        pending.EagerParseAll();

        await Assert.That(pending.MoveNext()).IsFalse();
        await Assert.That(pending.IsExhausted).IsTrue();
    }

    [Test]
    public async Task DiscardedSnapshotEndMarker_CanBeQueuedAgain()
    {
        var state = new SnapshotConsumeState(new Dictionary<TopicPartition, long>
        {
            [Partition0] = 42
        });
        await Assert.That(state.TryQueueEndMarker(Partition0, 42, out var endOffset)).IsTrue();

        using (PendingFetchData.CreateSnapshotEnd(
                   Partition0.Topic,
                   Partition0.Partition,
                   endOffset,
                   state))
        {
        }

        await Assert.That(state.TryQueueEndMarker(Partition0, 42, out _)).IsTrue();
    }

    private static bool InvokeFlushConsumedPositions(
        KafkaConsumer<string, string> consumer,
        PendingFetchData pending)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "FlushConsumedPositions",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (bool)method.Invoke(consumer, [pending])!;
    }

    private sealed class ThrowOnAccessRecordList(int count) : IReadOnlyList<Record>
    {
        public int Count { get; } = count;

        public Record this[int index] =>
            throw new InvalidOperationException($"Record {index} should not be inspected.");

        public IEnumerator<Record> GetEnumerator() =>
            throw new InvalidOperationException("Records should not be enumerated.");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
