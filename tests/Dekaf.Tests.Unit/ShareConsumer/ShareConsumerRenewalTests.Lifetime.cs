using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Poll_DisposeDuringPreparation_DefersFrameReturnUntilParserUnwinds(bool multipleBatches)
    {
        var response = CreateFetchResponse(0, 42, 1);
        if (multipleBatches)
        {
            var bytes = new ArrayBufferWriter<byte>();
            foreach (var offset in new[] { 42L, 43L })
                bytes.Write(CreateFetchResponse(0, offset, 1).Responses[0].Partitions[0].RecordBytes.Span);
            response = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None, NodeEndpoints = [],
                Responses = [new ShareFetchResponseTopic
                {
                    TopicId = TopicId,
                    Partitions = [new ShareFetchResponsePartition
                    {
                        PartitionIndex = 0, CurrentLeader = new(), RecordBytes = bytes.WrittenMemory,
                        AcquiredRecords = [new ShareFetchAcquiredRecords
                        {
                            FirstOffset = 42, LastOffset = 43, DeliveryCount = 1
                        }]
                    }]
                }]
            };
        }
        var frame = OwnResponse(response);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2) { ShareFetchResponses = new([response]) };
        var preparer = new PausedDeserializerPreparer();
        await using var fixture = CreateFixture(connection, maxPollRecords: 2, valueDeserializer: preparer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        var pending = poll.MoveNextAsync().AsTask();
        try
        {
            await preparer.Entered.Task.WaitAsync(stop.Token);
            await fixture.Consumer.DisposeAsync();
            await Assert.That(frame.Disposed).IsFalse();
        }
        finally
        {
            preparer.Release.TrySetResult();
        }
        await Assert.That(await pending).IsFalse();
        await Assert.That(frame.Disposed).IsTrue();
        var owners = (List<ShareRecordBatchOwner>)typeof(KafkaShareConsumer<string, string>)
            .GetField("_polledBatchOwners", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Consumer)!;
        await Assert.That(owners.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Poll_RenewedBatch_PinsOncePerRound()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 42, 4)])
        };
        await using var fixture = CreateFixture(connection, maxPollRecords: 4);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        var records = new List<ShareConsumeResult<string, string>>();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using (var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
        {
            for (var index = 0; index < 4; index++)
            {
                await Assert.That(await poll.MoveNextAsync()).IsTrue();
                records.Add(poll.Current);
            }
        }
        foreach (var record in records)
            fixture.Consumer.Acknowledge(record, AcknowledgeType.Renew);
        ApplySuccessfulAcknowledgements(fixture.Consumer, FlushPendingAcknowledgements(fixture.Consumer));
        var owners = (List<ShareRecordBatchOwner>)fixture.Consumer.GetType()
            .GetField("_polledBatchOwners", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Consumer)!;
        var assignment = new HashSet<TopicPartition> { new("topic", 0) };
        for (var round = 0; round < 2; round++)
        {
            using var scope = fixture.Consumer.BeginRecordBatchScope();
            await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment).Count).IsEqualTo(4);
            await Assert.That(owners.Count).IsEqualTo(1);
            // Rebuilding the replay snapshot in the same round must not add pins.
            await Assert.That(GetActiveRenewedRecords(fixture.Consumer, assignment).Count).IsEqualTo(4);
            await Assert.That(owners.Count).IsEqualTo(1);
        }
        foreach (var record in records)
            fixture.Consumer.Acknowledge(record, AcknowledgeType.Accept);
        await fixture.Consumer.CommitAsync(stop.Token);
        await Assert.That(records[0].BatchOwner).IsNotNull();
        using (fixture.Consumer.BeginRecordBatchScope())
            await Assert.That(owners.Count).IsEqualTo(0);
    }

    private static PoisonedFrame OwnResponse(ShareFetchResponse response)
    {
        var frame = new PoisonedFrame(response.Responses[0].Partitions[0].RecordBytes);
        response.PooledMemoryOwner = frame;
        return frame;
    }

    private sealed class PoisonedFrame(ReadOnlyMemory<byte> bytes) : IPooledMemory
    {
        public ReadOnlyMemory<byte> Memory => bytes;
        internal bool Disposed { get; private set; }
        public void Dispose()
        {
            if (Disposed) throw new InvalidOperationException("Response frame returned twice.");
            Disposed = true;
            if (MemoryMarshal.TryGetArray(bytes, out var array)) array.AsSpan().Fill(0xcc);
        }
    }
}
