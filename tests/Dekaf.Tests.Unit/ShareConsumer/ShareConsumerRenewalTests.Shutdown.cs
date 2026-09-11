using System.Reflection;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task HostedStop_UnsentBrokerDoesNotDiscardAnotherBrokersReply(bool commit)
    {
        using var caller = new CancellationTokenSource();
        using var shutdown = new CancellationTokenSource();
        var firstWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbacks = new List<(int Partition, long[] Offsets, Exception? Error)>();
        var first = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            BeforeWrite = async token =>
            {
                firstWaiting.TrySetResult();
                await release.Task.WaitAsync(token);
            }
        };
        var second = new CapturingConnection(ApiKey.ShareAcknowledge, 2, brokerId: 2, supportShareFetch: true)
        {
            ShareFetchHandler = async (_, token) =>
            {
                secondSent.TrySetResult();
                await release.Task.WaitAsync(token);
                return CreateFetchResponse(1, 100);
            },
            ShareAcknowledgeHandler = async (_, token) =>
            {
                secondSent.TrySetResult();
                await release.Task.WaitAsync(token);
                return CreateAcknowledgeResponse((1, ErrorCode.None));
            }
        };
        await using var fixture = CreateFixture(first, secondConnection: second);
        ((IHostedShareConsumer)fixture.Consumer).ObserveAcknowledgements(results =>
        {
            foreach (ref readonly var result in results)
                callbacks.Add((result.TopicPartition.Partition, CopyOffsets(result.Offsets), result.Exception));
        }, shutdown.Token);
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(CreateRecord(0, 42), AcknowledgeType.Release);
        fixture.Consumer.Acknowledge(CreateRecord(1, 43), AcknowledgeType.Accept);
        await using var poll = fixture.Consumer.PollAsync(caller.Token).GetAsyncEnumerator();
        var pendingPoll = commit ? null : poll.MoveNextAsync().AsTask();
        var request = commit ? fixture.Consumer.CommitAsync(caller.Token).AsTask() : pendingPoll!;
        try
        {
            await Task.WhenAll(firstWaiting.Task, secondSent.Task).WaitAsync(TimeSpan.FromSeconds(10));
            await caller.CancelAsync();
        }
        finally
        {
            release.TrySetResult();
        }
        if (commit)
            await Assert.That(async () => await request.WaitAsync(TimeSpan.FromSeconds(10)))
                .Throws<OperationCanceledException>();
        else
            await Assert.That(await pendingPoll!.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await Assert.That(callbacks.Count).IsEqualTo(1);
        await Assert.That(callbacks[0].Partition).IsEqualTo(1);
        await Assert.That(callbacks[0].Offsets).IsEquivalentTo(new long[] { 43 });
        await Assert.That(callbacks[0].Error).IsNull();
        await Assert.That(GetSessionEpoch(fixture.Consumer, 1)).IsEqualTo(0);
        await Assert.That(GetSessionEpoch(fixture.Consumer, 2)).IsEqualTo(1);
        await fixture.Consumer.CommitAsync(shutdown.Token);
        await Assert.That(callbacks.Count).IsEqualTo(2);
        await Assert.That(callbacks[1].Partition).IsEqualTo(0);
        await Assert.That(callbacks[1].Offsets).IsEquivalentTo(new long[] { 42 });
        await Assert.That(callbacks[1].Error).IsNull();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
    }

    [Test]
    [Arguments(AcknowledgeType.Accept, false, false)]
    [Arguments(AcknowledgeType.Accept, true, false)]
    [Arguments(AcknowledgeType.Release, false, false)]
    [Arguments(AcknowledgeType.Release, true, false)]
    [Arguments(AcknowledgeType.Reject, false, false)]
    [Arguments(AcknowledgeType.Reject, true, false)]
    [Arguments(AcknowledgeType.Renew, false, false)]
    [Arguments(AcknowledgeType.Renew, true, false)]
    [Arguments(AcknowledgeType.Accept, false, true)]
    [Arguments(AcknowledgeType.Accept, true, true)]
    [Arguments(AcknowledgeType.Release, false, true)]
    [Arguments(AcknowledgeType.Release, true, true)]
    [Arguments(AcknowledgeType.Reject, false, true)]
    [Arguments(AcknowledgeType.Reject, true, true)]
    [Arguments(AcknowledgeType.Renew, false, true)]
    [Arguments(AcknowledgeType.Renew, true, true)]
    public async Task HostedStop_BeforeWriteRetainsAcknowledgementsForFinalCommit(
        AcknowledgeType disposition, bool waitForLease, bool commit)
    {
        using var pollCancellation = new CancellationTokenSource();
        using var shutdownCancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbacks = new List<Exception?>();
        async ValueTask WaitBeforeWrite(CancellationToken token)
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        }
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            BeforeWrite = waitForLease ? null : WaitBeforeWrite
        };
        await using var fixture = CreateFixture(connection, leaseHandler: waitForLease
            ? async token => { await WaitBeforeWrite(token); return connection; }
            : null);
        var metrics = EnableShareTelemetry(fixture.Consumer);
        ((IHostedShareConsumer)fixture.Consumer).ObserveAcknowledgements(results =>
        {
            foreach (ref readonly var result in results)
                callbacks.Add(result.Exception);
        }, shutdownCancellation.Token);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(CreateRecord(), disposition);
        await using var poll = fixture.Consumer.PollAsync(pollCancellation.Token).GetAsyncEnumerator();
        var pendingPoll = commit ? null : poll.MoveNextAsync().AsTask();
        var request = commit ? fixture.Consumer.CommitAsync(pollCancellation.Token).AsTask() : pendingPoll!;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await pollCancellation.CancelAsync();
            if (commit)
                await Assert.That(async () => await request.WaitAsync(TimeSpan.FromSeconds(10)))
                    .Throws<OperationCanceledException>();
            else
                await Assert.That(await pendingPoll!.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
            await Assert.That(callbacks).IsEmpty();
            await Assert.That(connection.SendCount).IsEqualTo(0);
            await Assert.That(ShareMetricValue(metrics, "fetch.total")).IsEqualTo(0d);
            await Assert.That(ShareMetricValue(metrics, "acknowledgements.send.total")).IsEqualTo(0d);
            await Assert.That(ShareMetricValue(metrics, "acknowledgements.error.total")).IsEqualTo(0d);
            await Assert.That(GetSessionEpoch(fixture.Consumer, 1)).IsEqualTo(0);
            await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsTrue();
        }
        finally
        {
            release.TrySetResult();
        }
        await fixture.Consumer.CommitAsync(shutdownCancellation.Token);
        await Assert.That(callbacks.Count).IsEqualTo(1);
        await Assert.That(callbacks[0]).IsNull();
        await Assert.That(connection.ShareAcknowledgeRequest!.Topics[0].Partitions[0]
            .AcknowledgementBatches[0].AcknowledgeTypes[0]).IsEqualTo((byte)disposition);
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task LegacyHostedObserver_PreservesInflightRequestCancellation(bool fetch)
    {
        using var cancellation = new CancellationTokenSource();
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            ShareFetchHandler = async (_, token) =>
            {
                sent.TrySetResult();
                await release.Task.WaitAsync(token);
                return CreateFetchResponse(0, 100);
            },
            ShareAcknowledgeHandler = async (_, token) =>
            {
                sent.TrySetResult();
                await release.Task.WaitAsync(token);
                return CreateAcknowledgeResponse((0, ErrorCode.None));
            }
        };
        await using var fixture = CreateFixture(connection);
        ((IHostedShareConsumer)fixture.Consumer).ObserveAcknowledgements(static _ => { });
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(CreateRecord(), AcknowledgeType.Accept);
        await using var poll = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
        var request = fetch ? poll.MoveNextAsync().AsTask() : fixture.Consumer.CommitAsync(cancellation.Token).AsTask();
        try
        {
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            await Assert.That(async () => await request.WaitAsync(TimeSpan.FromSeconds(1)))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            release.TrySetResult();
            try { await request; } catch (OperationCanceledException) { }
            FlushPendingAcknowledgements(fixture.Consumer);
        }
    }

    [Test]
    [Arguments(AcknowledgeType.Accept, false)]
    [Arguments(AcknowledgeType.Accept, true)]
    [Arguments(AcknowledgeType.Renew, false)]
    [Arguments(AcknowledgeType.Renew, true)]
    public async Task HostedCommit_UsesShutdownBudgetForInflightAcknowledgements(
        AcknowledgeType disposition, bool shutdownDeadlineExpires)
    {
        using var processingCancellation = new CancellationTokenSource();
        using var shutdownCancellation = new CancellationTokenSource();
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completeResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbacks = new List<Exception?>();
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            ShareAcknowledgeHandler = async (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                sent.TrySetResult();
                await completeResponse.Task.WaitAsync(token);
                return CreateAcknowledgeResponse((0, ErrorCode.None));
            }
        };
        await using var fixture = CreateFixture(connection);
        ((IHostedShareConsumer)fixture.Consumer).ObserveAcknowledgements(results =>
        {
            foreach (ref readonly var result in results)
                callbacks.Add(result.Exception);
        }, shutdownCancellation.Token);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(), disposition);
        var commit = fixture.Consumer.CommitAsync(processingCancellation.Token).AsTask();
        try
        {
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await processingCancellation.CancelAsync();
            await Assert.That(commit.IsCompleted).IsFalse();
            if (shutdownDeadlineExpires)
                await shutdownCancellation.CancelAsync();
        }
        finally
        {
            completeResponse.TrySetResult();
        }

        if (shutdownDeadlineExpires)
            await Assert.That(async () => await commit.WaitAsync(TimeSpan.FromSeconds(10)))
                .Throws<OperationCanceledException>();
        else
            await commit.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(callbacks.Count).IsEqualTo(1);
        await Assert.That(callbacks[0] is OperationCanceledException).IsEqualTo(shutdownDeadlineExpires);
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsEqualTo(shutdownDeadlineExpires);
        await Assert.That(connection.ShareAcknowledgeRequest!.Topics[0].Partitions[0]
            .AcknowledgementBatches[0].AcknowledgeTypes[0]).IsEqualTo((byte)disposition);
        FlushPendingAcknowledgements(fixture.Consumer);
    }

    [Test]
    public async Task HostedStop_ShutdownDeadlineKeepsCancelledAcknowledgementsUnconfirmed()
    {
        using var pollCancellation = new CancellationTokenSource();
        using var shutdownCancellation = new CancellationTokenSource();
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbacks = new List<Exception?>();
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            ShareFetchHandler = async (request, token) =>
            {
                if (request.ShareSessionEpoch == ShareSessionManager.CloseEpoch)
                    return new ShareFetchResponse { ErrorCode = ErrorCode.None, Responses = [], NodeEndpoints = [] };
                sent.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("The request must end at the shutdown deadline.");
            }
        };
        await using var fixture = CreateFixture(connection);
        ((IHostedShareConsumer)fixture.Consumer).ObserveAcknowledgements(results =>
        {
            foreach (ref readonly var result in results)
                callbacks.Add(result.Exception);
        }, shutdownCancellation.Token);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(CreateRecord(), AcknowledgeType.Reject);
        await using var poll = fixture.Consumer.PollAsync(pollCancellation.Token).GetAsyncEnumerator();
        var pendingPoll = poll.MoveNextAsync().AsTask();
        try
        {
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await pollCancellation.CancelAsync();
            await Assert.That(pendingPoll.IsCompleted).IsFalse();
        }
        finally
        {
            await shutdownCancellation.CancelAsync();
        }

        await Assert.That(async () => await pendingPoll.WaitAsync(TimeSpan.FromSeconds(10)))
            .Throws<OperationCanceledException>();
        // Without a response, neither the session epoch nor acknowledgement success is known.
        await Assert.That(GetSessionEpoch(fixture.Consumer, 1)).IsEqualTo(0);
        await Assert.That(callbacks.Count).IsEqualTo(1);
        await Assert.That(callbacks[0]).IsTypeOf<TaskCanceledException>();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsTrue();
        var pending = FlushPendingAcknowledgements(fixture.Consumer);
        await Assert.That(pending[new TopicPartition("topic", 0)][0].AcknowledgeTypes[0])
            .IsEqualTo((byte)AcknowledgeType.Reject);
    }

    [Test]
    [Arguments(AcknowledgeType.Accept, false)]
    [Arguments(AcknowledgeType.Accept, true)]
    [Arguments(AcknowledgeType.Release, false)]
    [Arguments(AcknowledgeType.Release, true)]
    [Arguments(AcknowledgeType.Reject, false)]
    [Arguments(AcknowledgeType.Reject, true)]
    [Arguments(AcknowledgeType.Renew, false)]
    [Arguments(AcknowledgeType.Renew, true)]
    public async Task HostedStop_ObservesInflightAcknowledgementsBeforeFinalCommit(
        AcknowledgeType disposition, bool appliedBeforeStop)
    {
        using var pollCancellation = new CancellationTokenSource();
        using var shutdownCancellation = new CancellationTokenSource();
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completeResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var brokerEpoch = 1;
        var callbacks = new List<(long[] Offsets, Exception? Error)>();
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            ShareFetchHandler = async (request, token) =>
            {
                if (request.ShareSessionEpoch != brokerEpoch)
                    throw new InvalidOperationException("Unexpected broker session epoch.");
                if (appliedBeforeStop)
                    brokerEpoch++;
                sent.TrySetResult();
                await completeResponse.Task.WaitAsync(token);
                if (!appliedBeforeStop)
                    brokerEpoch++;
                return CreateFetchResponse(partition: 0, offset: 100);
            }
        };
        await using var fixture = CreateFixture(connection);
        ((IHostedShareConsumer)fixture.Consumer).ObserveAcknowledgements(results =>
        {
            foreach (ref readonly var result in results)
                callbacks.Add((CopyOffsets(result.Offsets), result.Exception));
        }, shutdownCancellation.Token);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        var sessions = (ShareSessionManager)typeof(KafkaShareConsumer<string, string>)
            .GetField("_sessionManager", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(fixture.Consumer)!;
        sessions.IncrementEpoch(1);
        fixture.Consumer.Acknowledge(CreateRecord(offset: 42), disposition);
        await using var poll = fixture.Consumer.PollAsync(pollCancellation.Token).GetAsyncEnumerator();
        var pendingPoll = poll.MoveNextAsync().AsTask();
        try
        {
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await pollCancellation.CancelAsync();
        }
        finally
        {
            completeResponse.TrySetResult();
        }

        // Stopping the next poll must not discard an already-sent acknowledgement response.
        await Assert.That(await pendingPoll.WaitAsync(TimeSpan.FromSeconds(10))).IsFalse();
        await Assert.That(callbacks.Count).IsEqualTo(1);
        await Assert.That(callbacks[0].Error).IsNull();
        await Assert.That(callbacks[0].Offsets).IsEquivalentTo(new long[] { 42 });
        await Assert.That(GetSessionEpoch(fixture.Consumer, 1)).IsEqualTo(brokerEpoch);
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
        await Assert.That(connection.ShareFetchRequest!.Topics[0].Partitions[0]
            .AcknowledgementBatches![0].AcknowledgeTypes[0]).IsEqualTo((byte)disposition);

        fixture.Consumer.Acknowledge(CreateRecord(offset: 43), AcknowledgeType.Accept);
        await fixture.Consumer.CommitAsync();
        await Assert.That(connection.ShareAcknowledgeRequest!.ShareSessionEpoch).IsEqualTo(brokerEpoch);
        await Assert.That(connection.ShareAcknowledgeRequest.Topics[0].Partitions[0]
            .AcknowledgementBatches[0].FirstOffset).IsEqualTo(43);
    }
}
