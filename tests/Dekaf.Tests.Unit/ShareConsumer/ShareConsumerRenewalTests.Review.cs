using System.Collections;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    public async Task PollBatches_CommittedRenewalReplaysBeforeFreshTraffic()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = new Queue<ShareFetchResponse>([CreateFetchResponse(0, 100), CreateFetchResponse(0, 200)])
        };
        await using var fixture = CreateFixture(connection, maxPollRecords: 1,
            valueDeserializer: Dekaf.Serialization.Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var records = poll.Current.GetEnumerator();
        await Assert.That(records.MoveNext()).IsTrue();
        poll.Current.Acknowledge(records.Current, AcknowledgeType.Renew);
        await fixture.Consumer.CommitAsync();

        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var replay = poll.Current.GetEnumerator();
        await Assert.That(replay.MoveNext()).IsTrue();
        await Assert.That(replay.Current.Offset).IsEqualTo(100);
        await Assert.That(connection.ShareFetchResponses.Count).IsEqualTo(1);
        poll.Current.Acknowledge(replay.Current);

        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var fresh = poll.Current.GetEnumerator();
        await Assert.That(fresh.MoveNext()).IsTrue();
        await Assert.That(fresh.Current.Offset).IsEqualTo(200);
        await Assert.That(fixture.Consumer.RenewedRecordReplayCount).IsEqualTo(1);
    }

    [Test]
    public async Task PollBatches_SparseCommitCallbackExcludesUnacknowledgedOffsets()
    {
        long[]? offsets = null;
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100, 3)
        };
        await using var fixture = CreateFixture(connection,
            acknowledgementCommitCallback: results => offsets = CopyOffsets(results[0].Offsets),
            valueDeserializer: Dekaf.Serialization.Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        foreach (var record in poll.Current)
            if (record.Offset != 101)
                poll.Current.Acknowledge(record);
        await fixture.Consumer.CommitAsync();
        await Assert.That(offsets).IsEquivalentTo(new long[] { 100, 102 });
    }

    [Test]
    public async Task PollBatches_FragmentedAcquisitionRangesAreVisitedLinearly()
    {
        const int count = 64;
        var bytes = new List<byte>();
        var ranges = new ShareFetchAcquiredRecords[count];
        for (var index = 0; index < count; index++)
        {
            var offset = 100 + index * 2;
            bytes.AddRange(CreateFetchResponse(0, offset).Responses[0].Partitions[0].RecordBytes.ToArray());
            ranges[index] = new ShareFetchAcquiredRecords { FirstOffset = offset, LastOffset = offset, DeliveryCount = 1 };
        }
        var counted = new CountedAcquisitionRanges(ranges);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None,
                Responses = [new ShareFetchResponseTopic
                {
                    TopicId = TopicId,
                    Partitions = [new ShareFetchResponsePartition
                    {
                        PartitionIndex = 0, CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                        RecordBytes = bytes.ToArray(), AcquiredRecords = counted
                    }]
                }], NodeEndpoints = []
            }
        };
        await using var fixture = CreateFixture(connection, valueDeserializer: Dekaf.Serialization.Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        for (var index = 0; index < count; index++)
        {
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            var records = poll.Current.GetEnumerator();
            await Assert.That(records.MoveNext()).IsTrue();
            await Assert.That(records.Current.Offset).IsEqualTo(100 + index * 2);
            await Assert.That(records.Current.DeliveryCount).IsEqualTo((short)1);
        }
        await Assert.That(counted.Reads).IsLessThanOrEqualTo(count * 6);
    }

    private sealed class CountedAcquisitionRanges(ShareFetchAcquiredRecords[] ranges) : IReadOnlyList<ShareFetchAcquiredRecords>
    {
        internal int Reads { get; private set; }
        public int Count => ranges.Length;
        public ShareFetchAcquiredRecords this[int index] { get { Reads++; return ranges[index]; } }
        public IEnumerator<ShareFetchAcquiredRecords> GetEnumerator() => ((IEnumerable<ShareFetchAcquiredRecords>)ranges).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
