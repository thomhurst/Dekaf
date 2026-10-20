using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

// A cancelled poll stops waiting for its ShareFetch while the broker may still acquire records
// for it. The next poll must deliver those records instead of stranding them until their
// acquisition locks expire (#3592).
public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    public async Task Poll_CancelledAfterWrite_DeliversTheLateResponseOnTheNextPoll()
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = async (request, _) =>
            {
                if (request.ShareSessionEpoch != 0)
                    return CreateFetchResponse(0, 200);
                sent.TrySetResult();
                await respond.Task;
                return CreateFetchResponse(0, 100);
            }
        };
        var connection = new AfterWriteCancellationConnection(inner);
        await using var fixture = CreateFixture(inner, leaseHandler: connection.LeaseAsync);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");

        using (var cancellation = new CancellationTokenSource())
        {
            await using var cancelled = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
            var pending = cancelled.MoveNextAsync().AsTask();
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            await Assert.That(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)))
                .Throws<OperationCanceledException>();
        }

        respond.TrySetResult();
        await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
        await Assert.That(poll.Current.Offset).IsEqualTo(100);
        await Assert.That(inner.ShareFetchRequests).HasSingleItem();
        // The resumed response advanced the session instead of forcing a new one.
        await Assert.That(GetSessionEpoch(fixture.Consumer, 1)).IsEqualTo(1);
    }

    [Test]
    public async Task Poll_CancelledAfterWrite_ReportsInlineAcknowledgementsOnceWithTheirOutcome()
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outcomes = new List<(long[] Offsets, Exception? Error)>();
        var inner = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = async (request, _) =>
            {
                if (request.ShareSessionEpoch != 1)
                    return CreateFetchResponse(0, 200);
                sent.TrySetResult();
                await respond.Task;
                return CreateFetchResponse(0, 100);
            }
        };
        var connection = new AfterWriteCancellationConnection(inner);
        await using var fixture = CreateFixture(inner, leaseHandler: connection.LeaseAsync,
            acknowledgementCommitCallback: results => CaptureOutcomes(results, outcomes));
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        EstablishSessions(fixture.Consumer, 1);
        fixture.Consumer.Acknowledge(CreateRecord(0, 42));

        using (var cancellation = new CancellationTokenSource())
        {
            await using var cancelled = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
            var pending = cancelled.MoveNextAsync().AsTask();
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            await Assert.That(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)))
                .Throws<OperationCanceledException>();
        }
        // The request carried the acknowledgement. Its outcome is unknown until the response.
        await Assert.That(outcomes).IsEmpty();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();

        respond.TrySetResult();
        await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
        await Assert.That(poll.Current.Offset).IsEqualTo(100);
        await Assert.That(outcomes).HasSingleItem();
        await Assert.That(outcomes[0].Offsets).IsEquivalentTo([42L]);
        await Assert.That(outcomes[0].Error).IsNull();
        await Assert.That(inner.ShareFetchRequests).HasSingleItem();
        await Assert.That(GetSessionEpoch(fixture.Consumer, 1)).IsEqualTo(2);
    }

    [Test]
    public async Task Commit_AfterCancelledPoll_WaitsForTheDetachedFetchBeforeUsingItsSession()
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            ShareFetchHandler = async (_, _) =>
            {
                sent.TrySetResult();
                await respond.Task;
                return CreateFetchResponse(0, 100);
            },
            ShareAcknowledgeResponse = CreateAcknowledgeResponse((0, ErrorCode.None))
        };
        var connection = new AfterWriteCancellationConnection(inner);
        await using var fixture = CreateFixture(inner, leaseHandler: connection.LeaseAsync);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        EstablishSessions(fixture.Consumer, 1);

        using (var cancellation = new CancellationTokenSource())
        {
            await using var cancelled = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
            var pending = cancelled.MoveNextAsync().AsTask();
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            await Assert.That(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)))
                .Throws<OperationCanceledException>();
        }

        fixture.Consumer.Acknowledge(CreateRecord(0, 42));
        var commit = fixture.Consumer.CommitAsync().AsTask();
        await Assert.That(commit.IsCompleted).IsFalse();
        await Assert.That(inner.ShareAcknowledgeRequests).IsEmpty();

        respond.TrySetResult();
        await commit.WaitAsync(TimeSpan.FromSeconds(10));
        // The acknowledgement follows the fetch that advanced the session from 1 to 2.
        await Assert.That(inner.ShareAcknowledgeRequest!.ShareSessionEpoch).IsEqualTo(2);

        await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
        await Assert.That(poll.Current.Offset).IsEqualTo(100);
        await Assert.That(inner.ShareFetchRequests).HasSingleItem();
    }

    [Test]
    public async Task Commit_AfterCancelledPollCarriedEveryAcknowledgement_WaitsForTheirOutcome()
    {
        // The cancelled poll took the only acknowledgement from the tracker. A commit must still
        // wait for the detached request that carries it and report its outcome.
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outcomes = new List<(long[] Offsets, Exception? Error)>();
        var inner = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = async (_, _) =>
            {
                sent.TrySetResult();
                await respond.Task;
                return CreateFetchResponse(0, 100);
            }
        };
        var connection = new AfterWriteCancellationConnection(inner);
        await using var fixture = CreateFixture(inner, leaseHandler: connection.LeaseAsync,
            acknowledgementCommitCallback: results => CaptureOutcomes(results, outcomes));
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        EstablishSessions(fixture.Consumer, 1);
        fixture.Consumer.Acknowledge(CreateRecord(0, 42));

        using (var cancellation = new CancellationTokenSource())
        {
            await using var cancelled = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
            var pending = cancelled.MoveNextAsync().AsTask();
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            await Assert.That(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)))
                .Throws<OperationCanceledException>();
        }
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();

        bool waited;
        try
        {
            var commit = fixture.Consumer.CommitAsync().AsTask();
            waited = !commit.IsCompleted;
            respond.TrySetResult();
            await commit.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            // Disposal waits for the detached request, so never leave it parked.
            respond.TrySetResult();
        }
        await Assert.That(waited).IsTrue();
        await Assert.That(outcomes).HasSingleItem();
        await Assert.That(outcomes[0].Offsets).IsEquivalentTo([42L]);
        await Assert.That(outcomes[0].Error).IsNull();
    }

    [Test]
    public async Task PollBatches_ResumedAfterAssignmentChange_SkipsRevokedPartitions()
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = async (request, _) =>
            {
                if (request.ShareSessionEpoch != 0)
                    return CreateFetchResponse(0, 200);
                sent.TrySetResult();
                await respond.Task;
                return CreateFetchResponse(1, 100);
            }
        };
        var connection = new AfterWriteCancellationConnection(inner);
        await using var fixture = CreateFixture(inner, leaseHandler: connection.LeaseAsync);
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        fixture.Consumer.Subscribe("topic");

        using (var cancellation = new CancellationTokenSource())
        {
            await using var cancelled = fixture.Consumer.PollBatchesAsync(cancellation.Token).GetAsyncEnumerator();
            var pending = cancelled.MoveNextAsync().AsTask();
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            await Assert.That(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)))
                .Throws<OperationCanceledException>();
        }

        // Partition 1 is revoked before the detached response that acquired its records is resumed.
        PrepareForPoll(fixture.Consumer, new TopicPartition("topic", 0));
        respond.TrySetResult();
        await using var poll = fixture.Consumer.PollBatchesAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
        await Assert.That(poll.Current.TopicPartition).IsEqualTo(new TopicPartition("topic", 0));
        var offsets = new List<long>();
        foreach (var record in poll.Current)
            offsets.Add(record.Offset);
        await Assert.That(offsets).IsEquivalentTo([200L]);
    }

    [Test]
    public async Task Poll_CancelledBeforeWrite_KeepsTheSessionAndAcknowledgements()
    {
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outcomes = new List<(long[] Offsets, Exception? Error)>();
        var blockWrites = true;
        var inner = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            BeforeWrite = async token =>
            {
                if (!blockWrites)
                    return;
                waiting.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
            },
            ShareFetchResponse = CreateFetchResponse(0, 100)
        };
        var connection = new AfterWriteCancellationConnection(inner);
        await using var fixture = CreateFixture(inner, leaseHandler: connection.LeaseAsync,
            acknowledgementCommitCallback: results => CaptureOutcomes(results, outcomes));
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        EstablishSessions(fixture.Consumer, 1);
        fixture.Consumer.Acknowledge(CreateRecord(0, 42));

        using (var cancellation = new CancellationTokenSource())
        {
            await using var cancelled = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
            var pending = cancelled.MoveNextAsync().AsTask();
            await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            await Assert.That(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)))
                .Throws<OperationCanceledException>();
        }

        blockWrites = false;
        await using var poll = fixture.Consumer.PollAsync().GetAsyncEnumerator();
        await Assert.That(await poll.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).IsTrue();
        await Assert.That(poll.Current.Offset).IsEqualTo(100);
        // Nothing reached the broker, so the next request reuses the session and carries the
        // acknowledgement that the cancelled request never submitted.
        await Assert.That(inner.ShareFetchRequests).HasSingleItem();
        await Assert.That(inner.ShareFetchRequests[0].ShareSessionEpoch).IsEqualTo(1);
        await Assert.That(AcknowledgedOffsets(inner.ShareFetchRequests[0])).IsEquivalentTo([42L]);
        await Assert.That(outcomes).HasSingleItem();
        await Assert.That(outcomes[0].Error).IsNull();
    }

    [Test]
    public async Task Close_AfterCancelledPoll_SettlesTheDetachedFetch()
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outcomes = new List<(long[] Offsets, Exception? Error)>();
        var inner = new CapturingConnection(ApiKey.ShareAcknowledge, 2, supportShareFetch: true)
        {
            ShareFetchHandler = async (request, _) =>
            {
                if (request.ShareSessionEpoch != 1)
                    return CreateFetchResponse(0, 200);
                sent.TrySetResult();
                await respond.Task;
                return CreateFetchResponse(0, 100);
            }
        };
        var connection = new AfterWriteCancellationConnection(inner);
        await using var fixture = CreateFixture(inner, leaseHandler: connection.LeaseAsync,
            acknowledgementCommitCallback: results => CaptureOutcomes(results, outcomes));
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        EstablishSessions(fixture.Consumer, 1);
        fixture.Consumer.Acknowledge(CreateRecord(0, 42));

        using (var cancellation = new CancellationTokenSource())
        {
            await using var cancelled = fixture.Consumer.PollAsync(cancellation.Token).GetAsyncEnumerator();
            var pending = cancelled.MoveNextAsync().AsTask();
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cancellation.CancelAsync();
            await Assert.That(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)))
                .Throws<OperationCanceledException>();
        }

        var close = fixture.Consumer.CloseAsync().AsTask();
        respond.TrySetResult();
        await close.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(outcomes).HasSingleItem();
        await Assert.That(outcomes[0].Offsets).IsEquivalentTo([42L]);
        await Assert.That(outcomes[0].Error).IsNull();
        // Closing the session releases the records the late response acquired.
        await Assert.That(inner.ShareAcknowledgeRequests).IsEmpty();
        await Assert.That(inner.ShareFetchRequests[^1].ShareSessionEpoch).IsEqualTo(ShareSessionManager.CloseEpoch);
    }

    // Splits the write from the response wait like KafkaConnection: the caller's token bounds the
    // write, and only the request context's response token bounds the wait for the answer.
    private sealed class AfterWriteCancellationConnection(CapturingConnection inner) :
        IKafkaConnection,
        IKafkaCapabilityProvider,
        IKafkaRequestAfterWriteCancellationConnection
    {
        public int BrokerId => inner.BrokerId;
        public string Host => inner.Host;
        public int Port => inner.Port;
        public bool IsConnected => inner.IsConnected;
        public KafkaConnectionCapabilities Capabilities => inner.Capabilities;

        internal ValueTask<IKafkaConnection> LeaseAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult<IKafkaConnection>(this);

        public ValueTask<TResponse> SendWithResponseCancellationAfterWriteAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, KafkaRequestWriteContext context,
            CancellationToken cancellationToken)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendWithResponseCancellationAsync<TRequest, TResponse>(
                request, apiVersion, context, cancellationToken);

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendFireAndForgetAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendPipelinedAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
            => inner.ConnectAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
