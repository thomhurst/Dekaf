using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, true, false)]
    [Arguments(true, false, false)]
    [Arguments(true, true, false)]
    [Arguments(false, false, true)]
    [Arguments(false, true, true)]
    [Arguments(true, false, true)]
    [Arguments(true, true, true)]
    public async Task Poll_BufferedOwnerRejectsPriorWindowRenewal(bool prepared, bool renewedSibling, bool exhausted)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 42, renewedSibling ? 3 : 4, withHeaders: true)]),
            ShareAcknowledgeResponse = CreateAcknowledgeResponse((0, ErrorCode.None))
        };
        IDeserializer<string> deserializer = prepared
            ? new PreparedWindowCountingDeserializer(int.MaxValue)
            : Serializers.String;
        await using var fixture = CreateFixture(connection, maxPollRecords: 2, valueDeserializer: deserializer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var first = poll.Current;
        if (renewedSibling)
            fixture.Consumer.Acknowledge(first, AcknowledgeType.Renew);
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        var previous = poll.Current;
        await Assert.That(previous.Offset).IsEqualTo(43L);
        if (exhausted)
        {
            var owner = previous.BatchOwner!;
            typeof(ShareRecordBatchOwner).GetProperty("Generation", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(owner, ShareRecordBatchOwner.MaximumGeneration);
            first.AttachBatchOwner(owner);
            previous.AttachBatchOwner(owner);
        }
        await Assert.That(await poll.MoveNextAsync()).IsTrue();
        if (renewedSibling)
        {
            await Assert.That(poll.Current).IsSameReferenceAs(first);
            await Assert.That(Encoding.UTF8.GetString(first.Headers[0].Value.Span)).IsEqualTo("header");
            fixture.Consumer.Acknowledge(first, AcknowledgeType.Renew);
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
        }
        await Assert.That(poll.Current.Offset).IsEqualTo(44L);
        await Assert.That(Encoding.UTF8.GetString(poll.Current.Headers[0].Value.Span)).IsEqualTo("header");
        await Assert.That(() => fixture.Consumer.Acknowledge(previous, AcknowledgeType.Renew))
            .Throws<InvalidOperationException>();
        await Assert.That(previous.AcknowledgeType).IsEqualTo(AcknowledgeType.Accept);
        fixture.Consumer.Acknowledge(poll.Current, AcknowledgeType.Renew);
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(false, false, 2)]
    [Arguments(true, false, 2)]
    [Arguments(false, true, 2)]
    [Arguments(true, true, 2)]
    [Arguments(false, false, 4)]
    [Arguments(true, false, 4)]
    [Arguments(false, true, 4)]
    [Arguments(true, true, 4)]
    public async Task Poll_PartialIteratorRefreshesOnlyUndisclosedRecords(bool prepared, bool exhausted, int recordCount)
    {
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 42, recordCount, withHeaders: true)])
        };
        IDeserializer<string> deserializer = prepared
            ? new PreparedWindowCountingDeserializer(int.MaxValue)
            : Serializers.String;
        await using var fixture = CreateFixture(connection, maxPollRecords: 2, valueDeserializer: deserializer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        ShareConsumeResult<string, string> previous;
        await using (var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator())
        {
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            previous = poll.Current;
            if (exhausted)
            {
                var owner = previous.BatchOwner!;
                typeof(ShareRecordBatchOwner).GetProperty("Generation", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(owner, ShareRecordBatchOwner.MaximumGeneration);
                previous.AttachBatchOwner(owner);
            }
        }
        await using var next = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        for (var offset = 43L; offset < 42 + recordCount; offset++)
        {
            await Assert.That(await next.MoveNextAsync()).IsTrue();
            await Assert.That(next.Current.Offset).IsEqualTo(offset);
            await Assert.That(Encoding.UTF8.GetString(next.Current.Headers[0].Value.Span)).IsEqualTo("header");
            await Assert.That(next.Current.BatchOwner).IsNotNull();
            await Assert.That(() => fixture.Consumer.Acknowledge(previous, AcknowledgeType.Renew))
                .Throws<InvalidOperationException>();
        }
        fixture.Consumer.Acknowledge(next.Current, AcknowledgeType.Renew);
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Poll_DeserializesOnlyTheCurrentWindow(bool prepared, bool failOutsideWindow)
    {
        var failOnCall = failOutsideWindow ? 3 : int.MaxValue;
        WindowCountingDeserializer deserializer = prepared
            ? new PreparedWindowCountingDeserializer(failOnCall)
            : new WindowCountingDeserializer(failOnCall);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 42, 5)])
        };
        await using var fixture = CreateFixture(connection, maxPollRecords: 2, valueDeserializer: deserializer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        for (var index = 0; index < 2; index++)
        {
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            await Assert.That(poll.Current.Offset).IsEqualTo(42L + index);
            await Assert.That(deserializer.Calls).IsEqualTo(2);
        }
        if (failOutsideWindow)
        {
            await Assert.That(async () => await poll.MoveNextAsync()).Throws<FormatException>();
            await Assert.That(deserializer.Calls).IsEqualTo(3);
            var released = FlushPendingAcknowledgements(fixture.Consumer)[new("topic", 0)];
            await Assert.That(released).HasSingleItem();
            await Assert.That(released[0].FirstOffset).IsEqualTo(44);
            await Assert.That(released[0].LastOffset).IsEqualTo(46);
            await Assert.That(ExpandAcknowledgementTypes(released[0].FirstOffset, released[0].LastOffset, released[0].AcknowledgeTypes)).IsEquivalentTo([(byte)AcknowledgeType.Release, (byte)AcknowledgeType.Release, (byte)AcknowledgeType.Release]);
        }
        else
        {
            for (var index = 2; index < 5; index++)
            {
                await Assert.That(await poll.MoveNextAsync()).IsTrue();
                await Assert.That(poll.Current.Offset).IsEqualTo(42L + index);
                await Assert.That(poll.Current.Value).IsEqualTo("new-value");
                await Assert.That(deserializer.Calls).IsEqualTo(index < 4 ? 4 : 5);
            }
        }
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Poll_DisposeDuringFetch_DoesNotRetainLateAcquisition()
    {
        var response = CreateFetchResponse(0, 42, 5);
        var frame = OwnResponse(response);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponses = new([response]),
            FirstShareFetchPause = release.Task,
            OnSend = () => entered.TrySetResult()
        };
        await using var fixture = CreateFixture(connection, maxPollRecords: 2);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
        var pending = poll.MoveNextAsync().AsTask();
        await entered.Task.WaitAsync(stop.Token);
        await fixture.Consumer.DisposeAsync();
        await Assert.That(frame.Disposed).IsFalse();
        release.SetResult();
        await Assert.That(await pending).IsFalse();
        await Assert.That(frame.Disposed).IsTrue();
        var remaining = typeof(KafkaShareConsumer<string, string>)
            .GetField("_bufferedRecordCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Consumer);
        await Assert.That((int)remaining!).IsEqualTo(0);
    }

    [Test]
    public async Task Poll_RepeatedPreparationCancellation_ReclaimsUndisclosedRawPrefixes()
    {
        var response = CreateFetchResponse(0, 42, 5);
        var frame = OwnResponse(response);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2) { ShareFetchResponses = new([response]) };
        var preparer = new FirstRecordThenCancelPreparer();
        await using var fixture = CreateFixture(connection, maxPollRecords: 2, valueDeserializer: preparer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        var raw = (IRawShareRecordAccessor)fixture.Consumer;
        raw.EnableRawRecordTracking();
        var buffer = typeof(KafkaShareConsumer<string, string>)
            .GetField("_rawBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Consumer)!;
        // The netstandard build embeds its own ArrayBufferWriter polyfill.
        var writtenCount = buffer.GetType().GetProperty("WrittenCount")!;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            preparer.Reset();
            using var stop = new CancellationTokenSource();
            await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
            var pending = poll.MoveNextAsync().AsTask();
            await preparer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That((int)writtenCount.GetValue(buffer)!).IsGreaterThan(0);
            stop.Cancel();
            await Assert.That(async () => await pending).Throws<OperationCanceledException>();
            await Assert.That((int)writtenCount.GetValue(buffer)!).IsEqualTo(0);
            await Assert.That(raw.TryGetRawRecord(new("topic", 0, 42), out _, out _)).IsFalse();
            await Assert.That(frame.Disposed).IsFalse();
        }
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Poll_MultiBatchOverflow_PreservesHeadersRawBytesAndReceiptTime(bool prepared)
    {
        var buffer = new ArrayBufferWriter<byte>();
        for (var batchIndex = 0; batchIndex < 3; batchIndex++)
        {
            using var batch = new RecordBatch
            {
                BaseOffset = 100 + batchIndex * 2,
                Records = Enumerable.Range(0, 2).Select(index => new Record
                {
                    OffsetDelta = index,
                    Key = Encoding.UTF8.GetBytes($"key-{batchIndex}-{index}"),
                    Value = Encoding.UTF8.GetBytes($"value-{batchIndex}-{index}"),
                    Headers = [new Header("identity", Encoding.UTF8.GetBytes($"header-{batchIndex}-{index}"))],
                    HeaderCount = 1
                }).ToList()
            };
            batch.Write(buffer);
        }
        var response = new ShareFetchResponse
        {
            ErrorCode = ErrorCode.None, NodeEndpoints = [],
            Responses = [new ShareFetchResponseTopic
            {
                TopicId = TopicId,
                Partitions = [new ShareFetchResponsePartition
                {
                    PartitionIndex = 0, CurrentLeader = new(), RecordBytes = buffer.WrittenMemory,
                    AcquiredRecords = [new ShareFetchAcquiredRecords { FirstOffset = 100, LastOffset = 105, DeliveryCount = 1 }]
                }]
            }]
        };
        var frame = OwnResponse(response);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2) { ShareFetchResponses = new([response]) };
        var preparer = prepared ? new PausedDeserializerPreparer() : null;
        preparer?.Release.SetResult();
        await using var fixture = CreateFixture(connection, maxPollRecords: 2,
            valueDeserializer: (IDeserializer<string>?)preparer ?? Serializers.String);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        var raw = (IRawShareRecordAccessor)fixture.Consumer;
        raw.EnableRawRecordTracking();
        var hosted = (IHostedShareConsumer)fixture.Consumer;
        hosted.ObserveAcknowledgements(static _ => { });
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        long? receipt = null;
        var count = 0;
        await foreach (var record in fixture.Consumer.PollAsync(stop.Token))
        {
            var suffix = $"{count / 2}-{count % 2}";
            await Assert.That(record.Value).IsEqualTo($"value-{suffix}");
            await Assert.That(Encoding.UTF8.GetString(record.Headers[0].Value.Span)).IsEqualTo($"header-{suffix}");
            await Assert.That(raw.TryGetRawRecord(new("topic", 0, record.Offset), out var key, out var value)).IsTrue();
            await Assert.That(Encoding.UTF8.GetString(key!)).IsEqualTo($"key-{suffix}");
            await Assert.That(Encoding.UTF8.GetString(value!)).IsEqualTo($"value-{suffix}");
            receipt ??= hosted.AcquisitionStartedTimestamp;
            await Assert.That(hosted.AcquisitionStartedTimestamp).IsEqualTo(receipt.Value);
            if (++count == 6) break;
        }
        await Assert.That(count).IsEqualTo(6);
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(1);
        await Assert.That(frame.Disposed).IsTrue();
    }

    [Test]
    public async Task Poll_AssignmentChange_DiscardsOnlyRevokedBufferedPartitions()
    {
        var first = new CapturingConnection(ApiKey.ShareFetch, 2, includeShareAcknowledge: true)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 100, 3)]),
            ShareAcknowledgeResponse = CreateAcknowledgeResponse((0, ErrorCode.None))
        };
        var second = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2) { ShareFetchResponses = new([CreateFetchResponse(1, 200, 3)]) };
        await using var fixture = CreateFixture(first, maxPollRecords: 2, secondConnection: second);
        PrepareForPoll(fixture.Consumer, new("topic", 0), new("topic", 1));
        fixture.Consumer.Subscribe("topic");
        await using (var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator())
        {
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            await Assert.That(poll.Current.Offset).IsEqualTo(100);
        }
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 1));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var offsets = new List<long>();
        await foreach (var record in fixture.Consumer.PollAsync(stop.Token))
        {
            offsets.Add(record.Offset);
            if (offsets.Count == 3) break;
        }
        await Assert.That(offsets).IsEquivalentTo([200L, 201L, 202L]);
        await Assert.That(first.ShareFetchRequests.Count).IsEqualTo(1);
        await Assert.That(second.ShareFetchRequests.Count).IsEqualTo(1);
        await Assert.That(first.ShareAcknowledgeRequests).HasSingleItem();
        var released = first.ShareAcknowledgeRequests[0].Topics[0].Partitions[0].AcknowledgementBatches[0];
        await Assert.That(released.FirstOffset).IsEqualTo(101);
        await Assert.That(released.LastOffset).IsEqualTo(102);
        await Assert.That(ExpandAcknowledgementTypes(released.FirstOffset, released.LastOffset, released.AcknowledgeTypes))
            .IsEquivalentTo([(byte)AcknowledgeType.Release, (byte)AcknowledgeType.Release]);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Poll_PartialRounds_PreserveLaterBrokerRecordsAndFrames(bool prepared, bool restartEnumeration)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var firstResponse = CreateFetchResponse(0, 100, 3);
        var secondResponse = CreateFetchResponse(1, 200, 3);
        var firstFrame = OwnResponse(firstResponse);
        var secondFrame = OwnResponse(secondResponse);
        var first = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchResponses = new([firstResponse]),
            OnSend = () => { if (firstFrame.Disposed) stop.Cancel(); }
        };
        var second = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2)
        {
            ShareFetchResponses = new([secondResponse])
        };
        var preparer = prepared ? new PausedDeserializerPreparer() : null;
        preparer?.Release.SetResult();
        await using var fixture = CreateFixture(first, maxPollRecords: 2, secondConnection: second,
            valueDeserializer: (IDeserializer<string>?)preparer ?? Serializers.String);
        PrepareForPoll(fixture.Consumer, new("topic", 0), new("topic", 1));
        fixture.Consumer.Subscribe("topic");
        var offsets = new List<long>();
        if (restartEnumeration)
        {
            for (var index = 0; index < 6; index++)
            {
                await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
                await Assert.That(await poll.MoveNextAsync()).IsTrue();
                offsets.Add(poll.Current.Offset);
                await Assert.That(poll.Current.Value).IsEqualTo("new-value");
                if (index == 0)
                {
                    // The first partition still has an unread record after this window.
                    await Assert.That(firstFrame.Disposed).IsFalse();
                    await Assert.That(secondFrame.Disposed).IsFalse();
                }
            }
        }
        else
        {
            await foreach (var record in fixture.Consumer.PollAsync(stop.Token))
            {
                offsets.Add(record.Offset);
                await Assert.That(record.Value).IsEqualTo("new-value");
                if (offsets.Count == 6) break;
            }
        }
        await Assert.That(offsets).IsEquivalentTo([100L, 101L, 102L, 200L, 201L, 202L]);
        await Assert.That(first.ShareFetchRequests.Count).IsEqualTo(1);
        await Assert.That(second.ShareFetchRequests.Count).IsEqualTo(1);
        await Assert.That(secondFrame.Disposed).IsTrue();
    }

    [Test]
    public async Task Poll_ImplicitOverflow_CommitsOnlyDisclosedOffsets()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 42, 5)])
        };
        await using var fixture = CreateFixture(connection, ShareAcknowledgementMode.Implicit, maxPollRecords: 2);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        for (var index = 0; index < 5; index++)
        {
            await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            await Assert.That(poll.Current.Offset).IsEqualTo(42 + index);
            if (index != 0)
            {
                var batch = connection.ShareAcknowledgeRequests[^1].Topics[0].Partitions[0].AcknowledgementBatches[0];
                await Assert.That(batch.FirstOffset).IsEqualTo(41 + index);
                await Assert.That(batch.LastOffset).IsEqualTo(41 + index);
                await Assert.That(batch.AcknowledgeTypes).IsEquivalentTo([(byte)AcknowledgeType.Accept]);
            }
        }
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(1);
        await Assert.That(connection.ShareAcknowledgeRequests.Count).IsEqualTo(4);
        var pending = FlushPendingAcknowledgements(fixture.Consumer)[new("topic", 0)];
        await Assert.That(pending).HasSingleItem();
        await Assert.That(pending[0].FirstOffset).IsEqualTo(46);
        await Assert.That(pending[0].LastOffset).IsEqualTo(46);
    }

    [Test]
    public async Task Poll_CancelledPreparation_RetriesSameAcquisition()
    {
        var response = CreateFetchResponse(0, 42, 5);
        var frame = OwnResponse(response);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2) { ShareFetchResponses = new([response]) };
        var preparer = new PausedDeserializerPreparer();
        await using var fixture = CreateFixture(connection, maxPollRecords: 2, valueDeserializer: preparer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        using (var stop = new CancellationTokenSource())
        {
            await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
            var pending = poll.MoveNextAsync().AsTask();
            await preparer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            stop.Cancel();
            await Assert.That(async () => await pending).Throws<OperationCanceledException>();
        }
        await Assert.That(frame.Disposed).IsFalse();
        preparer.Release.SetResult();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var offsets = new List<long>();
        await foreach (var record in fixture.Consumer.PollAsync(timeout.Token))
        {
            offsets.Add(record.Offset);
            if (offsets.Count == 5) break;
        }
        await Assert.That(offsets).IsEquivalentTo([42L, 43L, 44L, 45L, 46L]);
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(1);
        await Assert.That(frame.Disposed).IsTrue();
    }

    [Test]
    public async Task Poll_Resubscribe_ReleasesParsedAndUnparsedRemainders()
    {
        var firstResponse = CreateFetchResponse(0, 42, 3);
        var secondResponse = CreateFetchResponse(1, 200, 2);
        var secondFrame = OwnResponse(secondResponse);
        var first = new CapturingConnection(ApiKey.ShareFetch, 2) { ShareFetchResponses = new([firstResponse]) };
        var second = new CapturingConnection(ApiKey.ShareFetch, 2, brokerId: 2) { ShareFetchResponses = new([secondResponse]) };
        await using var fixture = CreateFixture(first, maxPollRecords: 2, secondConnection: second);
        PrepareForPoll(fixture.Consumer, new("topic", 0), new("topic", 1));
        fixture.Consumer.Subscribe("topic");
        await using (var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator())
        {
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            fixture.Consumer.Acknowledge(poll.Current, AcknowledgeType.Accept);
        }
        fixture.Consumer.Subscribe("topic", "additional-topic");
        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        var firstPartition = pending[new("topic", 0)];
        await Assert.That(firstPartition[0].FirstOffset).IsEqualTo(42);
        await Assert.That(firstPartition[0].AcknowledgeTypes[0]).IsEqualTo((byte)AcknowledgeType.Accept);
        var outcomes = firstPartition.SelectMany(static batch => batch.AcknowledgeTypes).ToArray();
        await Assert.That(outcomes).IsEquivalentTo([(byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Release, (byte)AcknowledgeType.Release]);
        var secondPartition = pending[new("topic", 1)];
        await Assert.That(secondPartition[0].FirstOffset).IsEqualTo(200);
        await Assert.That(secondPartition[^1].LastOffset).IsEqualTo(201);
        await Assert.That(secondPartition.SelectMany(static batch => ExpandAcknowledgementTypes(batch.FirstOffset, batch.LastOffset, batch.AcknowledgeTypes)))
            .IsEquivalentTo([(byte)AcknowledgeType.Release, (byte)AcknowledgeType.Release]);
        await Assert.That(secondFrame.Disposed).IsTrue();
    }

    [Test]
    public async Task Poll_PartialRenewalReplay_PreservesFreshRemainder()
    {
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            ShareFetchResponses = new([CreateFetchResponse(0, 100, 5)])
        };
        await using var fixture = CreateFixture(connection, maxPollRecords: 2);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        var renewed = CreateRecord();
        fixture.Consumer.Acknowledge(renewed, AcknowledgeType.Renew);
        ApplySuccessfulAcknowledgements(fixture.Consumer, RenewalAcknowledgements());
        FlushPendingAcknowledgements(fixture.Consumer);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        for (var index = 0; index < 6; index++)
        {
            await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
            await Assert.That(await poll.MoveNextAsync()).IsTrue();
            await Assert.That(poll.Current.Offset).IsEqualTo(index < 4 ? 100 + index : index == 4 ? 42 : 104);
            if (index == 4)
            {
                await Assert.That(poll.Current).IsSameReferenceAs(renewed);
                fixture.Consumer.Acknowledge(renewed, AcknowledgeType.Accept);
                await fixture.Consumer.CommitAsync(stop.Token);
            }
        }
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Poll_CancelledLaterWindow_RetriesOnlyUndisclosedRecords(bool multipleBatches)
    {
        var response = CreateFetchResponse(0, 42, 5, recordsPerBatch: multipleBatches ? 3 : 5);
        var frame = OwnResponse(response);
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2) { ShareFetchResponses = new([response]) };
        var preparer = new LaterWindowPreparer();
        await using var fixture = CreateFixture(connection, maxPollRecords: 2, valueDeserializer: preparer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        var raw = (IRawShareRecordAccessor)fixture.Consumer;
        raw.EnableRawRecordTracking();
        using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            await using var poll = fixture.Consumer.PollAsync(stop.Token).GetAsyncEnumerator();
            for (var offset = 42L; offset < 44; offset++)
            {
                await Assert.That(await poll.MoveNextAsync()).IsTrue();
                await Assert.That(poll.Current.Offset).IsEqualTo(offset);
            }
            var pending = poll.MoveNextAsync().AsTask();
            await preparer.Entered.Task.WaitAsync(stop.Token);
            stop.Cancel();
            await Assert.That(async () => await pending).Throws<OperationCanceledException>();
            await Assert.That(raw.TryGetRawRecord(new("topic", 0, 42), out _, out var value)).IsTrue();
            await Assert.That(Encoding.UTF8.GetString(value!)).IsEqualTo("new-value");
            await Assert.That(raw.TryGetRawRecord(new("topic", 0, 44), out _, out _)).IsFalse();
            await Assert.That(frame.Disposed).IsFalse();
        }
        preparer.Ready = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var retry = fixture.Consumer.PollAsync(timeout.Token).GetAsyncEnumerator();
        for (var offset = 44L; offset < 47; offset++)
        {
            await Assert.That(await retry.MoveNextAsync()).IsTrue();
            await Assert.That(retry.Current.Offset).IsEqualTo(offset);
            await Assert.That(retry.Current.Value).IsEqualTo("new-value");
        }
        await Assert.That(preparer.DeserializedCount).IsEqualTo(6);
        await Assert.That(connection.ShareFetchRequests.Count).IsEqualTo(1);
        await Assert.That(frame.Disposed).IsTrue();
    }
    private class WindowCountingDeserializer(int failOnCall) : IDeserializer<string>
    {
        internal int Calls { get; private set; }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            if (++Calls == failOnCall)
                throw new FormatException("Record outside the first poll window failed deserialization.");
            return Serializers.String.Deserialize(data, context);
        }
    }

    private sealed class PreparedWindowCountingDeserializer(int failOnCall)
        : WindowCountingDeserializer(failOnCall), IAsyncDeserializerPreparer<string>
    {
        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out string value)
        {
            value = Deserialize(data, context);
            return true;
        }

        public ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class LaterWindowPreparer : IDeserializer<string>, IAsyncDeserializerPreparer<string>
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Ready { get; set; }
        internal int DeserializedCount { get; private set; }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            DeserializedCount++;
            return Serializers.String.Deserialize(data, context);
        }

        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out string value)
        {
            if (!Ready && DeserializedCount == 3)
            {
                value = default!;
                return false;
            }
            value = Deserialize(data, context);
            return true;
        }

        public ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            return new ValueTask(Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }
    private sealed class FirstRecordThenCancelPreparer : IDeserializer<string>, IAsyncDeserializerPreparer<string>
    {
        private int _calls;
        internal TaskCompletionSource Entered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Reset()
        {
            _calls = 0;
            Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => Serializers.String.Deserialize(data, context);
        public bool TryDeserialize(ReadOnlyMemory<byte> data, SerializationContext context, out string value)
        {
            value = Deserialize(data, context);
            return _calls++ == 0;
        }
        public ValueTask PrepareAsync(ReadOnlyMemory<byte> data, SerializationContext context, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            return new ValueTask(Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }
}
