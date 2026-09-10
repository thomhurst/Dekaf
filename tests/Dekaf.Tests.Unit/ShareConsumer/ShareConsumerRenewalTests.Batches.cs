using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    [Arguments(ShareAcknowledgementMode.Implicit, false, false)]
    [Arguments(ShareAcknowledgementMode.Explicit, false, false)]
    [Arguments(ShareAcknowledgementMode.Explicit, true, false)]
    [Arguments(ShareAcknowledgementMode.Explicit, true, true)]
    public async Task PollBatches_UnsubscribeReleasesUnparsedBatchesAndPartitions(
        ShareAcknowledgementMode mode, bool settleFirst, bool reverseBatches)
    {
        var first = CreateFetchResponse(0, reverseBatches ? 200 : 100, 3).Responses[0].Partitions[0];
        var later = CreateFetchResponse(0, reverseBatches ? 100 : 200, 3).Responses[0].Partitions[0];
        var bytes = new ArrayBufferWriter<byte>();
        bytes.Write(first.RecordBytes.Span);
        bytes.Write(later.RecordBytes.Span);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None, NodeEndpoints = [],
                Responses = [new ShareFetchResponseTopic
                {
                    TopicId = TopicId,
                    Partitions = [new ShareFetchResponsePartition
                    {
                        PartitionIndex = 0, CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                        RecordBytes = bytes.WrittenMemory,
                        AcquiredRecords = reverseBatches
                            ? [..later.AcquiredRecords, ..first.AcquiredRecords]
                            : [..first.AcquiredRecords, ..later.AcquiredRecords]
                    }, CreateFetchResponse(1, 300, 2).Responses[0].Partitions[0]]
                }]
            },
            ShareAcknowledgeResponse = CreateAcknowledgeResponse((0, ErrorCode.None), (1, ErrorCode.None))
        };
        await using var fixture = CreateFixture(connection, acknowledgementMode: mode);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        if (settleFirst)
        {
            foreach (var record in poll.Current)
                poll.Current.Acknowledge(record);
            await fixture.Consumer.CommitAsync();
        }

        fixture.Consumer.Unsubscribe();
        await fixture.Consumer.DisposeAsync();
        await Assert.That(await poll.MoveNextAsync()).IsFalse();
        var request = connection.ShareAcknowledgeRequest!;
        // Kafka closes the whole broker session at the final epoch, including
        // acquisitions absent from the explicit acknowledgement payload.
        await Assert.That(request.ShareSessionEpoch).IsEqualTo(ShareSessionManager.CloseEpoch);
        var released = new List<long>();
        foreach (var topic in request.Topics)
        foreach (var partition in topic.Partitions)
        foreach (var batch in partition.AcknowledgementBatches!)
        {
            await Assert.That(batch.AcknowledgeTypes.All(static type => type == (byte)AcknowledgeType.Release)).IsTrue();
            for (var offset = batch.FirstOffset; offset <= batch.LastOffset; offset++)
                released.Add(offset);
        }
        long[] expected = settleFirst ? [] : [100, 101, 102];
        await Assert.That(released).IsEquivalentTo(expected);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Unsubscribe_ResubscribeWaitsForFinalAcknowledgement(bool batches)
    {
        var released = new TaskCompletionSource<ShareAcknowledgeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100),
            DelayedFinalAcknowledgement = released
        };
        await using var fixture = CreateFixture(connection);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        if (batches)
        {
            await using var initial = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
            await Assert.That(await initial.MoveNextAsync()).IsTrue();
            fixture.Consumer.Unsubscribe();
        }
        else
        {
            await using var initial = fixture.Consumer.PollAsync().GetAsyncEnumerator();
            await Assert.That(await initial.MoveNextAsync()).IsTrue();
            fixture.Consumer.Unsubscribe();
        }
        fixture.Consumer.Subscribe("topic");
        var sendsBefore = connection.SendCount;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var batchPoll = batches ? fixture.Consumer.PollBatchesAsync(timeout.Token).GetAsyncEnumerator() : null;
        await using var recordPoll = batches ? null : fixture.Consumer.PollAsync(timeout.Token).GetAsyncEnumerator();
        var next = batches ? batchPoll!.MoveNextAsync() : recordPoll!.MoveNextAsync();
        try
        {
            await Assert.That(next.IsCompleted).IsFalse();
            await Assert.That(connection.SendCount).IsEqualTo(sendsBefore);
        }
        finally
        {
            released.TrySetResult(CreateAcknowledgeResponse((0, ErrorCode.None)));
        }
        await Assert.That(await next).IsTrue();
        await Assert.That(connection.ShareFetchRequest!.ShareSessionEpoch).IsEqualTo(0);
    }

    [Test]
    public async Task PollBatches_UnsubscribeReleasesLaterBrokerResponse()
    {
        var first = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100, 3)
        };
        var second = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(1, 200, 3)
        };
        await using var fixture = CreateFixture(first, secondConnection: second);
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        fixture.Consumer.Unsubscribe();
        await fixture.Consumer.DisposeAsync();
        await Assert.That(first.ShareAcknowledgeRequest).IsNotNull();
        await Assert.That(second.ShareAcknowledgeRequest).IsNotNull();
        await Assert.That(first.ShareAcknowledgeRequest!.ShareSessionEpoch).IsEqualTo(ShareSessionManager.CloseEpoch);
        await Assert.That(second.ShareAcknowledgeRequest!.ShareSessionEpoch).IsEqualTo(ShareSessionManager.CloseEpoch);
        await Assert.That(second.ShareAcknowledgeRequest.Topics.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PollBatches_PendingReplayDispositionIsSentBeforeUnreadRenewal()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = new Queue<ShareFetchResponse>([CreateFetchResponse(0, 100, 2)]),
            ShareFetchResponse = new ShareFetchResponse { ErrorCode = ErrorCode.None, Responses = [], NodeEndpoints = [] }
        };
        await using var fixture = CreateFixture(connection, maxPollRecords: 1);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        foreach (var record in poll.Current)
            poll.Current.Acknowledge(record, AcknowledgeType.Renew);
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var firstReplay = poll.Current.GetEnumerator();
        await Assert.That(firstReplay.MoveNext()).IsTrue();
        await Assert.That(firstReplay.Current.Offset).IsEqualTo(100);
        poll.Current.Acknowledge(firstReplay.Current);
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        // Leave the next renewal unread: it must not starve the earlier Accept.
        await Assert.That(connection.ShareAcknowledgeRequest).IsNotNull();
        var sent = connection.ShareAcknowledgeRequest!.Topics[0].Partitions[0].AcknowledgementBatches!;
        await Assert.That(sent).HasSingleItem();
        await Assert.That(sent[0].FirstOffset).IsEqualTo(100);
        await Assert.That(sent[0].LastOffset).IsEqualTo(100);
        await Assert.That(sent[0].AcknowledgeTypes).IsEquivalentTo(new byte[] { (byte)AcknowledgeType.Accept });
    }

    [Test]
    [Arguments(ShareAcknowledgementMode.Implicit, false)]
    [Arguments(ShareAcknowledgementMode.Implicit, true)]
    [Arguments(ShareAcknowledgementMode.Explicit, false)]
    [Arguments(ShareAcknowledgementMode.Explicit, true)]
    public async Task PollBatches_UnsubscribeReleasesUnreadAcquisitions(ShareAcknowledgementMode mode, bool readFirst)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100, 3)
        };
        await using var fixture = CreateFixture(connection, acknowledgementMode: mode);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        if (readFirst)
        {
            var records = poll.Current.GetEnumerator();
            await Assert.That(records.MoveNext()).IsTrue();
        }
        fixture.Consumer.Unsubscribe();
        // Disposal awaits the tracked best-effort release task.
        await fixture.Consumer.DisposeAsync();
        await Assert.That(connection.ShareAcknowledgeRequest).IsNotNull();
        var sent = connection.ShareAcknowledgeRequest!.Topics[0].Partitions[0].AcknowledgementBatches!;
        await Assert.That(sent).HasSingleItem();
        await Assert.That(sent[0].FirstOffset).IsEqualTo(100);
        await Assert.That(sent[0].LastOffset).IsEqualTo(102);
        await Assert.That(sent[0].AcknowledgeTypes).IsEquivalentTo(new byte[] { 2, 2, 2 });
    }

    [Test]
    [Arguments(32)]
    [Arguments(131072)]
    public async Task PollBatches_RenewedRawPayloadSurvivesResponseDisposal(int payloadBytes)
    {
        var value = new byte[payloadBytes];
        Array.Fill(value, (byte)42);
        var encoded = new ArrayBufferWriter<byte>();
        using (var source = new RecordBatch
        {
            BaseOffset = 100,
            Records = [new Record
            {
                Key = "key"u8.ToArray(), Value = value,
                Headers = [new Header("identity", "header-value"u8.ToArray())], HeaderCount = 1
            }]
        })
            source.Write(encoded);
        var owner = new PoisonedBatchFrame(encoded.WrittenSpan.ToArray());
        var response = new ShareFetchResponse
        {
            ErrorCode = ErrorCode.None,
            Responses = [new ShareFetchResponseTopic
            {
                TopicId = TopicId,
                Partitions = [new ShareFetchResponsePartition
                {
                    PartitionIndex = 0, CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                    RecordBytes = owner.Memory,
                    AcquiredRecords = [new ShareFetchAcquiredRecords
                    {
                        FirstOffset = 100, LastOffset = 100, DeliveryCount = 1
                    }]
                }]
            }],
            NodeEndpoints = [], PooledMemoryOwner = owner
        };
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = new Queue<ShareFetchResponse>([response]),
            ShareFetchResponse = new ShareFetchResponse { ErrorCode = ErrorCode.None, Responses = [], NodeEndpoints = [] }
        };
        await using var fixture = CreateFixture(connection);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var first = poll.Current;
        var records = first.GetEnumerator();
        await Assert.That(records.MoveNext()).IsTrue();
        var original = records.Current;
        first.Acknowledge(original, AcknowledgeType.Renew);
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        await Assert.That(owner.Disposals).IsEqualTo(1);
        await Assert.That(() => original.ValueBytes).Throws<ObjectDisposedException>();
        var replay = poll.Current;
        var replayRecords = replay.GetEnumerator();
        await Assert.That(replayRecords.MoveNext()).IsTrue();
        var record = replayRecords.Current;
        await Assert.That(record.KeyBytes.Span.SequenceEqual("key"u8)).IsTrue();
        await Assert.That(record.ValueBytes.Span.SequenceEqual(value)).IsTrue();
        var headers = record.Headers.GetEnumerator();
        await Assert.That(headers.MoveNext()).IsTrue();
        await Assert.That(headers.Current.KeyUtf8.Span.SequenceEqual("identity"u8)).IsTrue();
        await Assert.That(headers.Current.Value.Span.SequenceEqual("header-value"u8)).IsTrue();
        await Assert.That(headers.MoveNext()).IsFalse();
        replay.Acknowledge(record);
        await fixture.Consumer.CommitAsync();
        await Assert.That(record.ValueBytes.Span.SequenceEqual(value)).IsTrue();
    }

    private sealed class PoisonedBatchFrame(byte[] bytes) : IPooledMemory
    {
        public ReadOnlyMemory<byte> Memory => bytes;
        public int Disposals { get; private set; }
        public void Dispose()
        {
            Disposals++;
            Array.Fill(bytes, (byte)0xDD);
        }
    }

    [Test]
    public async Task PollBatches_DeliversWholeAcquiredProducerBatchBeyondRequestBudget()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100, 3)
        };
        await using var fixture = CreateFixture(connection, maxPollRecords: 1,
            valueDeserializer: Dekaf.Serialization.Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();

        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var batch = poll.Current;
        await Assert.That(batch.Count).IsEqualTo(3);
        var offsets = new List<long>();
        foreach (var record in batch)
        {
            offsets.Add(record.Offset);
            batch.Acknowledge(record);
        }
        await Assert.That(offsets).IsEquivalentTo(new long[] { 100, 101, 102 });
        await fixture.Consumer.CommitAsync();
        var sent = connection.ShareAcknowledgeRequest!.Topics[0].Partitions[0].AcknowledgementBatches!;
        await Assert.That(sent).HasSingleItem();
        await Assert.That(sent[0].FirstOffset).IsEqualTo(100);
        await Assert.That(sent[0].LastOffset).IsEqualTo(102);
    }

    [Test]
    public async Task PollBatches_DeliversAcquiredRecordsFromEveryBrokerBeforeFetchingAgain()
    {
        var firstConnection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100)
        };
        var secondConnection = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2)
        {
            ShareFetchResponse = CreateFetchResponse(1, 200)
        };
        await using var fixture = CreateFixture(firstConnection, maxPollRecords: 1,
            secondConnection: secondConnection, valueDeserializer: Dekaf.Serialization.Serializers.String);
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        var offsets = new List<long>();
        for (var index = 0; index < 2; index++)
        {
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            foreach (var record in poll.Current)
                offsets.Add(record.Offset);
        }
        await Assert.That(offsets).IsEquivalentTo(new long[] { 100, 200 });
        await Assert.That(GetSessionEpoch(fixture.Consumer, 1)).IsEqualTo(1);
        await Assert.That(GetSessionEpoch(fixture.Consumer, 2)).IsEqualTo(1);
    }

    [Test]
    [Arguments(ShareAcknowledgementMode.Implicit, false)]
    [Arguments(ShareAcknowledgementMode.Explicit, false)]
    [Arguments(ShareAcknowledgementMode.Explicit, true)]
    public async Task PollBatches_AbandonedLeaseReleasesTrackingWithoutCommit(
        ShareAcknowledgementMode mode, bool deliverRecord)
    {
        var responses = new Queue<ShareFetchResponse>();
        for (var index = 0; index < 16; index++)
            responses.Enqueue(CreateFetchResponse(0, 100 + index));
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = responses
        };
        await using var fixture = CreateFixture(connection, acknowledgementMode: mode,
            valueDeserializer: Dekaf.Serialization.Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");

        for (var index = 0; index < 16; index++)
        {
            ShareBatchAcknowledgements<string, string>? tracker = null;
            await foreach (var batch in fixture.Consumer.PollBatchesAsync())
            {
                tracker = batch.Storage.Tracker;
                if (deliverRecord)
                {
                    var records = batch.GetEnumerator();
                    await Assert.That(records.MoveNext()).IsTrue();
                }
                break;
            }
            await Assert.That(tracker).IsNotNull();
            await Assert.That(tracker!.HasPending).IsFalse();
            await Assert.That(tracker.RetainedBatchCount).IsEqualTo(0);
        }
        await Assert.That(connection.ShareAcknowledgeRequest).IsNull();
    }

    [Test]
    [Arguments(ShareAcknowledgementMode.Implicit)]
    [Arguments(ShareAcknowledgementMode.Explicit)]
    public async Task PollBatches_PrunesOnlyClosedLeaseWhileResponseRemainsActive(ShareAcknowledgementMode mode)
    {
        var firstResponse = CreateFetchResponse(0, 100, 3);
        var firstBytes = firstResponse.Responses[0].Partitions[0].RecordBytes;
        var secondBytes = CreateFetchResponse(0, 103, 3).Responses[0].Partitions[0].RecordBytes;
        var bytes = new byte[firstBytes.Length + secondBytes.Length];
        firstBytes.CopyTo(bytes);
        secondBytes.CopyTo(bytes.AsMemory(firstBytes.Length));
        var response = new ShareFetchResponse
        {
            ErrorCode = ErrorCode.None,
            Responses = [new ShareFetchResponseTopic
            {
                TopicId = TopicId,
                Partitions = [new ShareFetchResponsePartition
                {
                    PartitionIndex = 0, CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                    RecordBytes = bytes,
                    AcquiredRecords = [new ShareFetchAcquiredRecords
                    {
                        FirstOffset = 100, LastOffset = 105, DeliveryCount = 1
                    }]
                }]
            }],
            NodeEndpoints = []
        };
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = response
        };
        await using var fixture = CreateFixture(connection, acknowledgementMode: mode,
            valueDeserializer: Dekaf.Serialization.Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var first = poll.Current;
        var records = first.GetEnumerator();
        await Assert.That(records.MoveNext()).IsTrue();
        if (mode == ShareAcknowledgementMode.Explicit)
            first.Acknowledge(records.Current);
        var tracker = first.Storage.Tracker!;
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        // Advancing prunes the first lease's unread records without scanning the response.
        await Assert.That(first.Storage.OpenLeases).IsEqualTo(0);
        await Assert.That(first.Storage.TrackedCount).IsEqualTo(1);
        await poll.DisposeAsync();
        // Iterator disposal also reclaims the unread second lease.
        await Assert.That(first.Storage.TrackedCount).IsEqualTo(1);
        await Assert.That(tracker.RetainedBatchCount).IsEqualTo(1);
        var pending = tracker.Flush()[new TopicPartition("topic", 0)];
        await Assert.That(pending).HasSingleItem();
        await Assert.That(pending[0].FirstOffset).IsEqualTo(100);
        await Assert.That(pending[0].LastOffset).IsEqualTo(100);
    }

    [Test]
    public async Task PollBatches_InlineRenewalReplaysThroughNewLease()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = new Queue<ShareFetchResponse>([CreateFetchResponse(0, 100)]),
            ShareFetchResponse = new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None,
                AcquisitionLockTimeoutMs = 30_000,
                Responses = [],
                NodeEndpoints = []
            }
        };
        await using var fixture = CreateFixture(connection, valueDeserializer: Dekaf.Serialization.Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var first = poll.Current;
        var records = first.GetEnumerator();
        await Assert.That(records.MoveNext()).IsTrue();
        var original = records.Current;
        first.Acknowledge(original, AcknowledgeType.Renew);
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        await Assert.That(connection.ShareFetchRequest!.IsRenewAck).IsTrue();
        await Assert.That(() => original.Value).Throws<ObjectDisposedException>();
        var replay = poll.Current;
        await Assert.That(ReferenceEquals(first, replay)).IsFalse();
        var replayRecords = replay.GetEnumerator();
        await Assert.That(replayRecords.MoveNext()).IsTrue();
        await Assert.That(replayRecords.Current.Offset).IsEqualTo(100);
        await Assert.That(replayRecords.Current.Value).IsEqualTo("new-value");
        await Assert.That(replayRecords.Current.DeliveryCount).IsEqualTo(1);
        await Assert.That(fixture.Consumer.RenewedRecordReplayCount).IsEqualTo(1);
        await Assert.That(fixture.Consumer.AcquisitionLockTimeoutMs).IsEqualTo(30_000);
        replay.Acknowledge(replayRecords.Current);
        await fixture.Consumer.CommitAsync();
    }

    [Test]
    public async Task PollBatches_DisposingIteratorInvalidatesLeaseAndCommitsOnlyEnumeratedRecords()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100, 3)
        };
        await using var fixture = CreateFixture(connection, acknowledgementMode: ShareAcknowledgementMode.Implicit, valueDeserializer: Dekaf.Serialization.Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");

        var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var batch = poll.Current;
        var records = batch.GetEnumerator();
        await Assert.That(records.MoveNext()).IsTrue();
        var record = records.Current;
        await Assert.That(record.Value).IsEqualTo("new-value");
        await Assert.That(record.DeliveryCount).IsEqualTo(1);
        await poll.DisposeAsync();
        await Assert.That(() => record.Value).Throws<ObjectDisposedException>();
        await fixture.Consumer.CommitAsync();

        var sent = connection.ShareAcknowledgeRequest!.Topics[0].Partitions[0].AcknowledgementBatches!;
        await Assert.That(sent).HasSingleItem();
        await Assert.That(sent[0].FirstOffset).IsEqualTo(100);
        await Assert.That(sent[0].LastOffset).IsEqualTo(100);
        await Assert.That(sent[0].AcknowledgeTypes).IsEquivalentTo(new byte[] { (byte)AcknowledgeType.Accept });
    }

    [Test]
    public async Task PollBatches_CloseReleasesImplicitDeliveryAndInvalidatesLease()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100, 3)
        };
        await using var fixture = CreateFixture(connection, acknowledgementMode: ShareAcknowledgementMode.Implicit, valueDeserializer: Dekaf.Serialization.Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var records = poll.Current.GetEnumerator();
        await Assert.That(records.MoveNext()).IsTrue();
        var record = records.Current;
        await fixture.Consumer.CloseAsync();
        await Assert.That(() => record.Value).Throws<ObjectDisposedException>();
        var sent = connection.ShareAcknowledgeRequest!.Topics[0].Partitions[0].AcknowledgementBatches!;
        await Assert.That(sent).HasSingleItem();
        await Assert.That(sent[0].FirstOffset).IsEqualTo(100);
        await Assert.That(sent[0].LastOffset).IsEqualTo(100);
        await Assert.That(sent[0].AcknowledgeTypes).IsEquivalentTo(new byte[] { (byte)AcknowledgeType.Release });
    }

    [Test]
    public async Task PollBatches_CannotSwitchToRetainedRecordPolling()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100)
        };
        await using var fixture = CreateFixture(connection);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var batchPoll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await batchPoll.MoveNextAsync()).IsTrue();
        await using var recordPoll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        await Assert.That(async () => await recordPoll.MoveNextAsync()).Throws<InvalidOperationException>();
        await Assert.That(() => fixture.Consumer.Acknowledge(CreateRecord())).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task PollBatches_ExplicitDispositionsReachStandaloneCommit()
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponse = CreateFetchResponse(0, 100, 3)
        };
        await using var fixture = CreateFixture(connection);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var batch = poll.Current;
        foreach (var record in batch)
            batch.Acknowledge(record, (AcknowledgeType)(record.Offset - 99));
        await fixture.Consumer.CommitAsync();
        var sent = connection.ShareAcknowledgeRequest!.Topics[0].Partitions[0].AcknowledgementBatches!;
        await Assert.That(sent).HasSingleItem();
        await Assert.That(sent[0].FirstOffset).IsEqualTo(100);
        await Assert.That(sent[0].LastOffset).IsEqualTo(102);
        await Assert.That(sent[0].AcknowledgeTypes).IsEquivalentTo(new byte[] { 1, 2, 3 });
    }
}
