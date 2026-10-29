using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class OffsetStoreTimingTests
{
    [Test]
    public async Task BufferedBatch_HandlerThrows_LeavesEveryPartitionUnstaged()
    {
        await using var consumer = CreateBufferedBatchConsumer(
            BufferedFetch(0, 0, 2), BufferedFetch(1, 0, 2), BufferedFetch(0, 2, 2));

        await Assert.That(async () =>
        {
            await foreach (var batch in consumer.ConsumeAsync().Batch(6))
            {
                await Assert.That(batch.Count).IsEqualTo(6);
                // Failing after processing part of the application batch proves none of it.
                foreach (var record in batch)
                    if (record.Partition == 1)
                        throw new InvalidOperationException("batch processing failed");
            }
        }).Throws<InvalidOperationException>();

        await Assert.That(GetDirtyStoredOffsets(consumer)).IsEmpty();
        await Assert.That(GetPositions(consumer)[new TopicPartition(Topic, 0)]).IsEqualTo(4L);
        await Assert.That(GetPositions(consumer)[new TopicPartition(Topic, 1)]).IsEqualTo(2L);
    }

    [Test]
    public async Task BufferedBatch_NextBatch_StagesAllPreviousPartitionsWithoutStagingCurrentBatch()
    {
        await using var consumer = CreateBufferedBatchConsumer(BufferedFetch(0, 0, 2), BufferedFetch(1, 0, 6));
        await using var batches = consumer.ConsumeAsync().Batch(3).GetAsyncEnumerator();
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(GetDirtyStoredOffsets(consumer)).IsEmpty();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        var offsets = GetDirtyStoredOffsets(consumer);
        await Assert.That(offsets[new TopicPartition(Topic, 0)]).IsEqualTo(2L);
        await Assert.That(offsets[new TopicPartition(Topic, 1)]).IsEqualTo(1L);
        await Assert.That(batches.Current[0].Offset).IsEqualTo(1L);
        await Assert.That(batches.Current[2].Offset).IsEqualTo(3L);
    }

    [Test]
    public async Task BufferedBatch_SecondHandlerThrows_KeepsOnlySuccessfulBatchProven()
    {
        await using var consumer = CreateBufferedBatchConsumer(BufferedFetch(0, 0, 2), BufferedFetch(0, 2, 4));
        var batchCount = 0;
        await Assert.That(async () =>
        {
            await foreach (var batch in consumer.ConsumeAsync().Batch(3))
                if (++batchCount == 2)
                    throw new InvalidOperationException("second batch failed");
        }).Throws<InvalidOperationException>();
        await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 0)]).IsEqualTo(3L);
    }

    [Test]
    public async Task BufferedBatch_SeekDuringHandler_DiscardsCompletedFetchCheckpoint()
    {
        await using var consumer = CreateBufferedBatchConsumer(BufferedFetch(0, 0, 2), BufferedFetch(1, 0, 6));
        await using var batches = consumer.ConsumeAsync().Batch(3).GetAsyncEnumerator();
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        consumer.Seek(new TopicPartitionOffset(Topic, 0, 0));

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 0)]).IsEqualTo(0L);
        await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 1)]).IsEqualTo(1L);
    }

    [Test]
    public async Task BufferedBatch_RevocationDuringHandler_DiscardsOnlyRevokedPartitionProof()
    {
        await using var consumer = CreateBufferedBatchConsumer(BufferedFetch(0, 0, 2), BufferedFetch(1, 0, 6));
        await using var batches = consumer.ConsumeAsync().Batch(3).GetAsyncEnumerator();
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        // Publish the same invalidation marker as a coordinator revocation, before the
        // foreground poll has cleared the released fetch's partition.
        consumer.GetType().GetMethod("StagePendingFetchClear",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(consumer, [new TopicPartition(Topic, 0)]);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(GetDirtyStoredOffsets(consumer).ContainsKey(new TopicPartition(Topic, 0))).IsFalse();
        await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 1)]).IsEqualTo(1L);
    }

    [Test]
    public async Task BufferedBatch_ExplicitCommitAfterBreak_VouchesForEveryPartition()
    {
        await using var consumer = CreateBufferedBatchConsumer(BufferedFetch(0, 0, 2), BufferedFetch(1, 0, 2));
        await foreach (var batch in consumer.ConsumeAsync().Batch(4))
            break;

        consumer.StageExplicitCommitOffsetsForTesting();
        await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 0)]).IsEqualTo(2L);
        await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 1)]).IsEqualTo(2L);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BufferedBatch_DisabledAutoStore_NeverStagesAutomatically(bool eager)
    {
        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto,
            eager ? OffsetStoreTiming.OnDelivery : OffsetStoreTiming.AfterProcessing,
            enableAutoOffsetStore: false, BufferedFetch(0, 0, 6));
        await using var batches = consumer.ConsumeAsync().Batch(2).GetAsyncEnumerator();
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(GetDirtyStoredOffsets(consumer)).IsEmpty();
    }

    [Test]
    public async Task BufferedBatch_OnDelivery_PreservesEagerStaging()
    {
        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto, OffsetStoreTiming.OnDelivery,
            BufferedFetch(0, 0, 3));
        await foreach (var batch in consumer.ConsumeAsync().Batch(3))
        {
            await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 0)]).IsEqualTo(3L);
            break;
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BufferedBatch_CancellationDuringFill_DoesNotProvePartialBatch(bool sourceCancellation)
    {
        using var cancellation = new CancellationTokenSource();
        var valueDeserializer = new CancelOnSecondValue(cancellation);
        await using var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto,
            valueDeserializer, BufferedFetch(0, 0, 6));
        var stream = consumer.ConsumeAsync(sourceCancellation ? cancellation.Token : default)
            .Batch(4, sourceCancellation ? default : cancellation.Token);
        await Assert.That(async () =>
        {
            await foreach (var _ in stream) { }
        }).Throws<OperationCanceledException>();
        await Assert.That(GetDirtyStoredOffsets(consumer)).IsEmpty();
    }

    [Test]
    public async Task BufferedBatch_FilteredRecords_DoNotProveBufferedPrefix()
    {
        await using var consumer = new KafkaConsumer<string, string>(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], OffsetCommitMode = OffsetCommitMode.Auto,
            QueuedMinMessages = 1, RecordFilter = new RejectOddOffsets()
        }, Dekaf.Serialization.Serializers.String, Dekaf.Serialization.Serializers.String);
        SetInitialized(consumer);
        InjectPendingFetch(consumer, BufferedFetch(0, 0, 6));
        await foreach (var batch in consumer.ConsumeAsync().Batch(2))
        {
            await Assert.That(batch[0].Offset).IsEqualTo(0L);
            await Assert.That(batch[1].Offset).IsEqualTo(2L);
            break;
        }
        await Assert.That(GetDirtyStoredOffsets(consumer)).IsEmpty();
    }

    [Test]
    public async Task BufferedBatch_SuccessfulBatch_IncludesReleasedFetchTrailingProgress()
    {
        var fetch = BufferedFetch(0, 0, 1, lastOffsetDelta: 3);
        await using var consumer = CreateBufferedBatchConsumer(fetch, BufferedFetch(1, 0, 5));
        await using var batches = consumer.ConsumeAsync().Batch(2).GetAsyncEnumerator();
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(GetDirtyStoredOffsets(consumer)).IsEmpty();
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 0)]).IsEqualTo(4L);
        await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 1)]).IsEqualTo(1L);
    }

    [Test]
    public async Task BufferedBatch_FinalPull_ProvesLastBatchEvenWhenStreamEnds()
    {
        await using var consumer = CreateBufferedBatchConsumer(BufferedFetch(0, 0, 4));
        using var cancellation = new CancellationTokenSource();
        await using var batches = consumer.ConsumeAsync(cancellation.Token).Batch(4).GetAsyncEnumerator();
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(GetDirtyStoredOffsets(consumer)).IsEmpty();
        cancellation.Cancel();
        await Assert.That(await batches.MoveNextAsync()).IsFalse();
        await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 0)]).IsEqualTo(4L);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BufferedBatch_StreamChange_PreservesExplicitCommitCheckpoints(bool nextBatchStream)
    {
        await using var consumer = CreateBufferedBatchConsumer(BufferedFetch(0, 0, 2), BufferedFetch(1, 0, 8));
        await foreach (var _ in consumer.ConsumeAsync().Batch(3))
            break;
        await Assert.That(GetDirtyStoredOffsets(consumer)).IsEmpty();

        if (nextBatchStream)
        {
            await using var batches = consumer.ConsumeAsync().Batch(2).GetAsyncEnumerator();
            await Assert.That(await batches.MoveNextAsync()).IsTrue();
            await Assert.That(batches.Current[0].Partition).IsEqualTo(1);
            await Assert.That(batches.Current[0].Offset).IsEqualTo(1L);
            await Assert.That(GetDirtyStoredOffsets(consumer)).IsEmpty();
            await Assert.That(await batches.MoveNextAsync()).IsTrue();
            // Proving the new stream must not automatically prove an abandoned partition.
            await Assert.That(GetDirtyStoredOffsets(consumer).ContainsKey(new TopicPartition(Topic, 0))).IsFalse();
        }
        else
        {
            await using var records = consumer.ConsumeAsync().GetAsyncEnumerator();
            await Assert.That(await records.MoveNextAsync()).IsTrue();
        }

        consumer.StageExplicitCommitOffsetsForTesting();
        await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 0)]).IsEqualTo(2L);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BufferedBatch_StreamChange_ExplicitCommitPreservesNewerProvenOffset(bool nextBatchStream)
    {
        await using var consumer = CreateBufferedBatchConsumer(BufferedFetch(0, 0, 2), BufferedFetch(0, 2, 6));
        await foreach (var _ in consumer.ConsumeAsync().Batch(3))
            break;

        using var cancellation = new CancellationTokenSource();
        if (nextBatchStream)
        {
            await foreach (var _ in consumer.ConsumeAsync(cancellation.Token).Batch(5))
                cancellation.Cancel();
        }
        else
        {
            await foreach (var record in consumer.ConsumeAsync(cancellation.Token))
                if (record.Offset == 7)
                    cancellation.Cancel();
        }

        await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 0)]).IsEqualTo(8L);
        consumer.StageExplicitCommitOffsetsForTesting();
        await Assert.That(GetDirtyStoredOffsets(consumer)[new TopicPartition(Topic, 0)]).IsEqualTo(8L);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task BufferedBatch_RejectedFetch_StagesOnlyWithoutUnfinishedPartitionBatch(bool unfinishedBatch)
    {
        using var cancellation = new CancellationTokenSource();
        var filter = new RejectAfterOffset(unfinishedBatch ? 0 : -1) { Cancellation = cancellation };
        await using var consumer = new KafkaConsumer<string, string>(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], OffsetCommitMode = OffsetCommitMode.Auto,
            QueuedMinMessages = 1, RecordFilter = filter
        }, Dekaf.Serialization.Serializers.String, Dekaf.Serialization.Serializers.String);
        SetInitialized(consumer);
        consumer.Assign(new TopicPartition(Topic, 0));
        GetFetchPositions(consumer)[new TopicPartition(Topic, 0)] = 0;
        GetPendingFetches(consumer).Enqueue(BufferedFetch(0, 0, 2));
        GetPendingFetches(consumer).Enqueue(BufferedFetch(0, 2, 2));
        // A later fetch cancels deterministically after both earlier fetches are flushed.
        GetPendingFetches(consumer).Enqueue(BufferedFetch(0, 4, 1));
        MarkManualAssignmentCurrent(consumer);

        await Assert.That(async () =>
        {
            await foreach (var _ in consumer.ConsumeAsync(cancellation.Token).Batch(8)) { }
        }).Throws<OperationCanceledException>();

        var offsets = GetDirtyStoredOffsets(consumer);
        if (unfinishedBatch)
            await Assert.That(offsets).IsEmpty();
        else
            await Assert.That(offsets[new TopicPartition(Topic, 0)]).IsEqualTo(4L);
    }

    private sealed class RejectAfterOffset(long acceptedOffset) : IConsumerRecordFilter
    {
        public CancellationTokenSource? Cancellation { get; set; }
        public bool ShouldDeserialize(in ConsumerRecordFilterContext record)
        {
            if (record.Offset == 4)
                Cancellation?.Cancel();
            return record.Offset == acceptedOffset;
        }
    }

    private sealed class RejectOddOffsets : IConsumerRecordFilter
    {
        public bool ShouldDeserialize(in ConsumerRecordFilterContext record) => record.Offset % 2 == 0;
    }

    private sealed class CancelOnSecondValue(CancellationTokenSource cancellation) : Dekaf.Serialization.IDeserializer<string>
    {
        private int _calls;
        public string Deserialize(ReadOnlyMemory<byte> data, Dekaf.Serialization.SerializationContext context)
        {
            if (++_calls == 2)
            {
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            }
            return "value";
        }
    }

    private static PendingFetchData BufferedFetch(int partition, long offset, int count, int? lastOffsetDelta = null)
    {
        var records = new Dekaf.Protocol.Records.Record[count];
        for (var i = 0; i < count; i++)
            records[i] = CreateRecord(i, "key", "value");
        var batch = CreateBatch(offset, records);
        batch.LastOffsetDelta = lastOffsetDelta ?? count - 1;
        return PendingFetchData.Create(Topic, partition, [batch]);
    }

    private static KafkaConsumer<string, string> CreateBufferedBatchConsumer(params PendingFetchData[] fetches)
    {
        var consumer = CreateInitializedConsumer(OffsetCommitMode.Auto);
        consumer.Assign(fetches.Select(fetch => fetch.TopicPartition).Distinct().ToArray());
        foreach (var fetch in fetches)
        {
            GetFetchPositions(consumer)[fetch.TopicPartition] = 0;
            GetPendingFetches(consumer).Enqueue(fetch);
        }
        MarkManualAssignmentCurrent(consumer);
        return consumer;
    }
}
