using System.Text;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public class ConsumeBatchTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task RawValueFastPath_PreservesWireNullKey(bool nullKey, bool ignoreKeys)
    {
        var actual = ignoreKeys
            ? ReadNullKey(Serializers.Ignore, nullKey)
            : ReadNullKey(Serializers.RawBytes, nullKey);
        await Assert.That(actual).IsEqualTo(nullKey);

        static bool ReadNullKey<TKey>(IDeserializer<TKey> keyDeserializer, bool nullKey)
        {
            var source = new RecordBatch
            {
                Records = [new Record { Key = ReadOnlyMemory<byte>.Empty, IsKeyNull = nullKey, Value = "value"u8.ToArray() }]
            };
            using var pending = PendingFetchData.Create("topic", 0, new[] { source });
            pending.EagerParseAll();
            var batch = new ConsumeBatch<TKey, ReadOnlyMemory<byte>>(pending, keyDeserializer, Serializers.RawBytes);
            using var records = batch.GetEnumerator();
            if (!records.MoveNext())
                throw new InvalidOperationException("The fixture must deliver its record.");
            return records.Current.IsKeyNull;
        }
    }

    [Test]
    [Arguments(false, true)]
    [Arguments(false, false)]
    [Arguments(true, true)]
    [Arguments(true, false)]
    public async Task Interceptor_DeliveryGuardHonorsUserCallbacks(bool duringDeserialization, bool paused)
    {
        var stoppedStatus = paused ? BatchIterationStatus.Paused : BatchIterationStatus.Stopped;
        using var pending = CreatePendingFetchData("test-topic", 0, 7, 1);
        var epoch = new BatchIterationEpoch();
        var status = BatchIterationStatus.Continue;
        var calls = 0;
        var stored = 0;
        void Stop()
        {
            status = stoppedStatus;
            epoch.Invalidate();
        }
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String,
            duringDeserialization ? new CallbackDeserializer(Stop) : Serializers.String,
            new BatchIterationGuard(epoch, epoch.Version, _ => status),
            storeOffsetOnDelivery: (_, _, _) => stored++,
            onConsume: result =>
            {
                calls++;
                Stop();
                return result;
            });
        using var records = batch.GetEnumerator();
        await Assert.That(records.MoveNext()).IsFalse();
        await Assert.That(calls).IsEqualTo(duringDeserialization ? 0 : 1);
        await Assert.That(stored).IsEqualTo(0);
        await Assert.That(batch.Count).IsEqualTo(0);
        await Assert.That(pending.MoveNext()).IsEqualTo(stoppedStatus == BatchIterationStatus.Paused);
    }

    [Test]
    public async Task Interceptor_OnlySeesUnfilteredRecordsAndPreservesDeliveredOffsets()
    {
        using var pending = CreatePendingFetchData("test-topic", 0, 0, 3);
        var calls = new List<long>();
        var stored = new List<long>();
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String,
            storeOffsetOnDelivery: (_, offset, _) => stored.Add(offset), recordFilter: new OddOffsetFilter(),
            onConsume: result =>
            {
                calls.Add(result.Offset);
                return new ConsumeResult<string, string>("replacement-topic", 99, 99, result.Key,
                    "replacement", result.Headers, 0, result.TimestampType, 99);
            });
        var results = batch.ToList();
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Value).IsEqualTo("replacement");
        await Assert.That(results[0].Topic).IsEqualTo("test-topic");
        await Assert.That(results[0].Partition).IsEqualTo(0);
        await Assert.That(results[0].Offset).IsEqualTo(1);
        await Assert.That(results[0].LeaderEpoch).IsNull();
        await Assert.That(results[0].IsPartitionEof).IsFalse();
        await Assert.That(calls.SequenceEqual([1L])).IsTrue();
        // Replacement metadata must not change the broker progress tracked for this fetch.
        await Assert.That(stored.SequenceEqual([1L, 2L, 3L])).IsTrue();
    }

    private sealed class OddOffsetFilter : IConsumerRecordFilter
    {
        public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context) => context.Offset % 2 != 0;
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task CompletionReservationBound_EmptyFetchReservesOneSlot(bool eagerParse)
    {
        using var pending = PendingFetchData.Create("empty", 0, Array.Empty<RecordBatch>());
        if (eagerParse)
            pending.EagerParseAll();
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String);
        await Assert.That(batch.MaximumRecordCount).IsEqualTo(1);
        await Assert.That(batch.GetEnumerator().MoveNext()).IsFalse();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task CompletionReservationBound_DoesNotScanAtPollConstruction(bool eagerParse)
    {
        var batches = new CountingBatchList(128);
        using var pending = PendingFetchData.Create("test-topic", 0, batches);
        if (eagerParse)
            pending.EagerParseAll();
        var accesses = batches.Accesses;

        for (var limit = 1; limit <= 128; limit++)
        {
            var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String,
                maxRecords: limit);
            if (batch.MaximumRecordCount != limit)
                throw new InvalidOperationException("Cached bound did not respect the poll limit.");
        }

        await Assert.That(batches.Accesses).IsEqualTo(accesses);
    }

    [Test]
    [Arguments(1, 1)]
    [Arguments(2, 2)]
    [Arguments(100, 3)]
    public async Task CompletionReservationBound_SurvivesFetchDisposal(int limit, int expected)
    {
        var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 0, messageCount: 3);
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String,
            maxRecords: limit);
        pending.Dispose();

        await Assert.That(batch.MaximumRecordCount).IsEqualTo(expected);
    }

    [Test]
    public async Task ConsumeBatch_EnumeratesAllRecords()
    {
        // Arrange
        using var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 0, messageCount: 5);
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String);

        // Act
        var results = new List<ConsumeResult<string, string>>();
        foreach (var result in batch)
        {
            results.Add(result);
        }

        // Assert
        await Assert.That(results.Count).IsEqualTo(5);
    }

    [Test]
    public async Task ConsumeBatch_TopicAndPartition_AreCorrect()
    {
        using var pending = CreatePendingFetchData("my-topic", partitionIndex: 7, baseOffset: 100, messageCount: 1);
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String);

        await Assert.That(batch.Topic).IsEqualTo("my-topic");
        await Assert.That(batch.Partition).IsEqualTo(7);
        await Assert.That(batch.TopicPartition).IsEqualTo(new TopicPartition("my-topic", 7));
    }

    [Test]
    public async Task ConsumeBatch_Count_MatchesRecordCount()
    {
        using var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 0, messageCount: 3);
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String);

        // Enumerate to populate the count
        foreach (var _ in batch) { }

        await Assert.That(batch.Count).IsEqualTo(3);
    }

    [Test]
    public async Task ConsumeBatch_Records_HaveCorrectOffsets()
    {
        using var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 42, messageCount: 3);
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String);

        var offsets = new List<long>();
        foreach (var result in batch)
        {
            offsets.Add(result.Offset);
        }

        await Assert.That(offsets.Count).IsEqualTo(3);
        await Assert.That(offsets[0]).IsEqualTo(42);
        await Assert.That(offsets[1]).IsEqualTo(43);
        await Assert.That(offsets[2]).IsEqualTo(44);
    }

    [Test]
    public async Task ConsumeBatch_EmptyBatch_YieldsNoRecords()
    {
        using var pending = PendingFetchData.Create("test-topic", 0, Array.Empty<RecordBatch>());
        pending.EagerParseAll();

        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String);

        var count = 0;
        foreach (var _ in batch)
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumeBatch_PartitionEofMarker_IsEmptyAndCarriesOffset()
    {
        using var pending = PendingFetchData.CreatePartitionEof("test-topic", 4, 73);
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String);

        await Assert.That(batch.TopicPartition).IsEqualTo(new TopicPartition("test-topic", 4));
        await Assert.That(batch.IsPartitionEof).IsTrue();
        await Assert.That(batch.PartitionEofOffset).IsEqualTo(73);
        using var records = batch.GetEnumerator();
        await Assert.That(records.MoveNext()).IsFalse();
    }

    [Test]
    public async Task ConsumeBatch_Records_HaveDeserializedKeyAndValue()
    {
        using var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 0, messageCount: 2);
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String);

        var results = new List<ConsumeResult<string, string>>();
        foreach (var result in batch)
        {
            results.Add(result);
        }

        await Assert.That(results[0].Key).IsEqualTo("key-0");
        await Assert.That(results[0].Value).IsEqualTo("value-0");
        await Assert.That(results[1].Key).IsEqualTo("key-1");
        await Assert.That(results[1].Value).IsEqualTo("value-1");
    }

    [Test]
    public async Task ConsumeBatch_Records_HaveCorrectTopic()
    {
        using var pending = CreatePendingFetchData("my-topic", partitionIndex: 3, baseOffset: 0, messageCount: 1);
        var batch = new ConsumeBatch<string, string>(pending, Serializers.String, Serializers.String);

        foreach (var result in batch)
        {
            await Assert.That(result.Topic).IsEqualTo("my-topic");
            await Assert.That(result.Partition).IsEqualTo(3);
        }
    }

    [Test]
    public async Task ConsumeBatch_RemainsCompletedAfterPartitionReturns()
    {
        using var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 0, messageCount: 3);
        var assignmentEpoch = new BatchIterationEpoch();
        var canContinue = true;
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            Serializers.String,
            new BatchIterationGuard(assignmentEpoch, assignmentEpoch.Version,
                _ => canContinue ? BatchIterationStatus.Continue : BatchIterationStatus.Stopped));

        using var enumerator = batch.GetEnumerator();

        await Assert.That(enumerator.MoveNext()).IsTrue();
        canContinue = false;
        assignmentEpoch.Invalidate();

        await Assert.That(enumerator.MoveNext()).IsFalse();
        canContinue = true;
        assignmentEpoch.Invalidate();
        await Assert.That(enumerator.MoveNext()).IsFalse();
        await Assert.That(batch.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeBatch_UnrelatedEpochChange_DoesNotDropRecord()
    {
        using var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 0, messageCount: 2);
        var assignmentEpoch = new BatchIterationEpoch();
        var membershipChecks = 0;
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            Serializers.String,
            new BatchIterationGuard(
                assignmentEpoch,
                assignmentEpoch.Version,
                _ =>
                {
                    membershipChecks++;
                    return BatchIterationStatus.Continue;
                }));
        using var enumerator = batch.GetEnumerator();

        await Assert.That(enumerator.MoveNext()).IsTrue();
        assignmentEpoch.Invalidate();

        await Assert.That(enumerator.MoveNext()).IsTrue();
        await Assert.That(enumerator.Current.Offset).IsEqualTo(1);
        await Assert.That(batch.Count).IsEqualTo(2);
        await Assert.That(membershipChecks).IsEqualTo(2);
    }

    [Test]
    public async Task ConsumeBatch_RevocationDuringDeserialization_DoesNotBufferCurrentRecord()
    {
        using var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 0, messageCount: 1);
        var assignmentEpoch = new BatchIterationEpoch();
        var canContinue = true;
        var deserializer = new CallbackDeserializer(() =>
        {
            canContinue = false;
            assignmentEpoch.Invalidate();
        });
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            deserializer,
            new BatchIterationGuard(assignmentEpoch, assignmentEpoch.Version,
                _ => canContinue ? BatchIterationStatus.Continue : BatchIterationStatus.Stopped));

        using var enumerator = batch.GetEnumerator();

        await Assert.That(enumerator.MoveNext()).IsFalse();
        await Assert.That(pending.MoveNext()).IsFalse();
    }

    [Test]
    public async Task ConsumeBatch_PauseDuringDeserialization_BuffersCurrentRecordForRedelivery()
    {
        using var pending = CreatePendingFetchData("test-topic", partitionIndex: 0, baseOffset: 7, messageCount: 1);
        var assignmentEpoch = new BatchIterationEpoch();
        var status = BatchIterationStatus.Continue;
        var deserializer = new CallbackDeserializer(() =>
        {
            status = BatchIterationStatus.Paused;
            assignmentEpoch.Invalidate();
        });
        var batch = new ConsumeBatch<string, string>(
            pending,
            Serializers.String,
            deserializer,
            new BatchIterationGuard(assignmentEpoch, assignmentEpoch.Version, _ => status));

        using var enumerator = batch.GetEnumerator();

        await Assert.That(enumerator.MoveNext()).IsFalse();
        await Assert.That(pending.MoveNext()).IsTrue();
        await Assert.That(pending.CurrentBaseOffset + pending.CurrentRecord.OffsetDelta).IsEqualTo(7L);
    }

    [Test]
    public async Task BatchIterationGuard_DoesNotAdoptInProgressPublication()
    {
        var assignmentEpoch = new BatchIterationEpoch();
        assignmentEpoch.BeginPublication();
        var observedVersion = assignmentEpoch.Version;
        var guard = new BatchIterationGuard(assignmentEpoch, observedVersion);

        try
        {
            await Assert.That(guard.CanStart(new TopicPartition("test-topic", 0), ref observedVersion))
                .IsFalse();
        }
        finally
        {
            assignmentEpoch.EndPublication();
        }
    }

    [Test]
    public async Task BatchIterationEpoch_DeliveryGateSerializesPublication()
    {
        var epoch = new BatchIterationEpoch();
        var deliveryVersion = epoch.Version;
        await Assert.That(epoch.TryBeginSnapshotDelivery(deliveryVersion)).IsTrue();
        using var publicationEntered = new ManualResetEventSlim();
        var publisher = new Thread(() =>
        {
            epoch.BeginPublication();
            publicationEntered.Set();
            epoch.EndPublication();
        })
        {
            IsBackground = true
        };

        try
        {
            publisher.Start();
            await Assert.That(SpinWait.SpinUntil(
                    () => Volatile.Read(ref epoch.ConsumeOneDeliveryChangesPending) != 0,
                    TimeSpan.FromSeconds(5)))
                .IsTrue();
            await Assert.That(publicationEntered.IsSet).IsFalse();
        }
        finally
        {
            epoch.EndSnapshotDelivery(deliveryVersion);
        }

        await Assert.That(publicationEntered.Wait(TimeSpan.FromSeconds(5))).IsTrue();
        await Assert.That(publisher.Join(TimeSpan.FromSeconds(5))).IsTrue();
        await Assert.That(epoch.Version & 1).IsEqualTo(0);
    }

    /// <summary>
    /// Creates a PendingFetchData with a single RecordBatch containing the specified number of records.
    /// </summary>
    private static PendingFetchData CreatePendingFetchData(string topic, int partitionIndex, long baseOffset, int messageCount)
    {
        var records = new Record[messageCount];
        for (var i = 0; i < messageCount; i++)
        {
            var key = Encoding.UTF8.GetBytes($"key-{i}");
            var value = Encoding.UTF8.GetBytes($"value-{i}");

            records[i] = new Record
            {
                OffsetDelta = i,
                TimestampDelta = i * 1000,
                Key = key,
                Value = value,
                IsKeyNull = false,
                IsValueNull = false,
                Headers = null,
                HeaderCount = 0,
            };
        }

        var recordBatch = new RecordBatch
        {
            BaseOffset = baseOffset,
            BaseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Records = records,
        };

        var pending = PendingFetchData.Create(topic, partitionIndex, new List<RecordBatch> { recordBatch });
        pending.EagerParseAll();
        return pending;
    }

    private sealed class CountingBatchList : IReadOnlyList<RecordBatch>
    {
        private readonly RecordBatch[] _batches;
        public int Accesses { get; private set; }
        public int Count => _batches.Length;
        public RecordBatch this[int index]
        {
            get
            {
                Accesses++;
                return _batches[index];
            }
        }

        public CountingBatchList(int count)
        {
            _batches = new RecordBatch[count];
            for (var index = 0; index < count; index++)
                _batches[index] = new RecordBatch { BaseOffset = index, Records = [new Record()] };
        }

        public IEnumerator<RecordBatch> GetEnumerator() => ((IEnumerable<RecordBatch>)_batches).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CallbackDeserializer(Action callback) : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            callback();
            return Serializers.String.Deserialize(data, context);
        }
    }
}
