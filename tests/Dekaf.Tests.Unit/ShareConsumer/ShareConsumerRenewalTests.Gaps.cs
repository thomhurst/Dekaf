using System.Buffers;
using System.Reflection;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    // Offsets 0-6 are acquired. Offset 1 was removed by compaction, offset 3 is a COMMIT
    // marker, and offsets 5-6 have no record bytes in the response.
    private static readonly long[] GapResponseDataOffsets = [0, 2, 4];
    private static readonly (long First, long Last)[] GapResponseGaps = [(1, 1), (3, 3), (5, 6)];

    [Test]
    [Arguments(false, ShareAcknowledgementMode.Implicit)]
    [Arguments(true, ShareAcknowledgementMode.Implicit)]
    [Arguments(false, ShareAcknowledgementMode.Explicit)]
    [Arguments(true, ShareAcknowledgementMode.Explicit)]
    public async Task Poll_ControlRecordsAndAcquiredGaps_AreNotDeliveredAndAcknowledgedAsGaps(
        bool prepared, ShareAcknowledgementMode mode)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponses = new([CreateGapFetchResponse()])
        };
        IDeserializer<string> deserializer = prepared
            ? new PreparedWindowCountingDeserializer(int.MaxValue)
            : Serializers.String;
        await using var fixture = CreateFixture(connection, mode, valueDeserializer: deserializer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        var delivered = new List<long>();
        for (var index = 0; index < GapResponseDataOffsets.Length; index++)
        {
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            await Assert.That(poll.Current.Value).IsEqualTo("new-value");
            delivered.Add(poll.Current.Offset);
        }

        await Assert.That(delivered).IsEquivalentTo(GapResponseDataOffsets);
        var types = ExpandFlushedTypes(FlushPendingAcknowledgements(fixture.Consumer)[new("topic", 0)]);
        await AssertGapTypes(types, mode == ShareAcknowledgementMode.Implicit ? AcknowledgeType.Accept : null);
    }

    [Test]
    [Arguments(ShareAcknowledgementMode.Implicit)]
    [Arguments(ShareAcknowledgementMode.Explicit)]
    public async Task PollBatches_ControlRecordsAndAcquiredGaps_AreNotDeliveredAndAcknowledgedAsGaps(
        ShareAcknowledgementMode mode)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponses = new([CreateGapFetchResponse(), CreateFetchResponse(0, 100)])
        };
        await using var fixture = CreateFixture(connection, mode, valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer);
        EstablishSessions(fixture.Consumer, 1);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var poll = fixture.Consumer.PollBatchesAsync(stop.Token).GetAsyncEnumerator();
        var delivered = new List<long>();
        while (delivered.Count < GapResponseDataOffsets.Length)
        {
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            foreach (var record in poll.Current)
            {
                await Assert.That(record.Value).IsEqualTo("new-value");
                delivered.Add(record.Offset);
            }
        }

        // The next fetch carries the dispositions inline, including the trailing gaps found
        // once the partition's last batch has been consumed.
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var next = poll.Current.GetEnumerator();
        await Assert.That(next.MoveNext()).IsTrue();
        await Assert.That(next.Current.Offset).IsEqualTo(100L);

        await Assert.That(delivered).IsEquivalentTo(GapResponseDataOffsets);
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(2);
        var sent = connection.ShareFetchRequests[1].Topics[0].Partitions[0].AcknowledgementBatches!;
        var types = ExpandFlushedTypes(sent.Select(static batch =>
            new AcknowledgementBatchData(batch.FirstOffset, batch.LastOffset, batch.AcknowledgeTypes.ToArray())).ToList());
        await AssertGapTypes(types, mode == ShareAcknowledgementMode.Implicit ? AcknowledgeType.Accept : null);
    }

    [Test]
    public async Task PollBatches_ExplicitAcknowledgements_DoNotSendUnacknowledgedRecordsAsGaps()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 100, 3)])
        };
        await using var fixture = CreateFixture(connection, valueDeserializer: Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var records = new List<ShareBatchRecord<string, string>>();
        foreach (var record in poll.Current)
            records.Add(record);
        poll.Current.Acknowledge(records[0]);
        poll.Current.Acknowledge(records[2], AcknowledgeType.Reject);

        var types = ExpandFlushedTypes(FlushBatchAcknowledgements(fixture.Consumer)[new("topic", 0)]);

        // Offset 101 has no disposition yet; a zero type would archive it as a gap.
        await Assert.That(types.Keys.Order()).IsEquivalentTo(new long[] { 100, 102 });
        await Assert.That(types[100]).IsEqualTo((byte)AcknowledgeType.Accept);
        await Assert.That(types[102]).IsEqualTo((byte)AcknowledgeType.Reject);
    }

    private static async Task AssertGapTypes(Dictionary<long, byte> types, AcknowledgeType? deliveredType)
    {
        var expected = new Dictionary<long, byte>();
        foreach (var (first, last) in GapResponseGaps)
            for (var offset = first; offset <= last; offset++)
                expected[offset] = (byte)AcknowledgeType.Gap;
        if (deliveredType is { } type)
            foreach (var offset in GapResponseDataOffsets)
                expected[offset] = (byte)type;
        await Assert.That(types).IsEquivalentTo(expected);
    }

    private static Dictionary<long, byte> ExpandFlushedTypes(List<AcknowledgementBatchData> batches)
    {
        var types = new Dictionary<long, byte>();
        var previousLast = long.MinValue;
        foreach (var batch in batches)
        {
            // The broker rejects unordered or overlapping acknowledgement batches.
            if (batch.FirstOffset <= previousLast)
                throw new InvalidOperationException($"Batch at {batch.FirstOffset} overlaps or is unordered.");
            previousLast = batch.LastOffset;
            var expanded = ExpandAcknowledgementTypes(batch.FirstOffset, batch.LastOffset, batch.AcknowledgeTypes).ToArray();
            for (var index = 0; index < expanded.Length; index++)
                types.Add(batch.FirstOffset + index, expanded[index]);
        }
        return types;
    }

    private static Dictionary<TopicPartition, List<AcknowledgementBatchData>> FlushBatchAcknowledgements(
        KafkaShareConsumer<string, string> consumer)
        => (Dictionary<TopicPartition, List<AcknowledgementBatchData>>)typeof(KafkaShareConsumer<string, string>)
            .GetMethod("FlushAcknowledgements", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(consumer, [false])!;

    private static ShareFetchResponse CreateGapFetchResponse()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var compacted = new RecordBatch
        {
            BaseOffset = 0,
            LastOffsetDelta = 2,
            Records =
            [
                new Record { OffsetDelta = 0, IsKeyNull = true, Value = "new-value"u8.ToArray() },
                new Record { OffsetDelta = 2, IsKeyNull = true, Value = "new-value"u8.ToArray() }
            ]
        })
        {
            compacted.Write(buffer);
        }
        using (var marker = new RecordBatch
        {
            BaseOffset = 3,
            LastOffsetDelta = 0,
            ProducerId = 7,
            ProducerEpoch = 0,
            Attributes = RecordBatchAttributes.IsTransactional | RecordBatchAttributes.IsControlBatch,
            Records =
            [
                // EndTransactionMarker: control key version 0, type 1 (COMMIT).
                new Record { OffsetDelta = 0, Key = new byte[] { 0, 0, 0, 1 }, Value = new byte[] { 0, 0, 0, 0, 0, 0 } }
            ]
        })
        {
            marker.Write(buffer);
        }
        using (var data = new RecordBatch
        {
            BaseOffset = 4,
            LastOffsetDelta = 0,
            Records = [new Record { OffsetDelta = 0, IsKeyNull = true, Value = "new-value"u8.ToArray() }]
        })
        {
            data.Write(buffer);
        }

        return new ShareFetchResponse
        {
            ErrorCode = ErrorCode.None,
            Responses =
            [
                new ShareFetchResponseTopic
                {
                    TopicId = TopicId,
                    Partitions =
                    [
                        new ShareFetchResponsePartition
                        {
                            PartitionIndex = 0,
                            CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                            RecordBytes = buffer.WrittenMemory,
                            AcquiredRecords = [new ShareFetchAcquiredRecords { FirstOffset = 0, LastOffset = 6, DeliveryCount = 1 }]
                        }
                    ]
                }
            ],
            NodeEndpoints = []
        };
    }
}
