using System.Reflection;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    public async Task Poll_NewSession_CarriesAcknowledgementsOnTheSecondRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var broker = new ShareSessionBroker();
        var outcomes = new List<(long[] Offsets, Exception? Error)>();
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = (request, _) => broker.Fetch(request, cancelAfter: 3, cancellation)
        };
        await using var fixture = CreateFixture(
            connection,
            acknowledgementCommitCallback: results => CaptureOutcomes(results, outcomes));
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        fixture.Consumer.Acknowledge(CreateRecord(0, 42));

        await DrainPollAsync(fixture.Consumer, cancellation.Token);

        await Assert.That(broker.Rejections).IsEmpty();
        await Assert.That(connection.ShareFetchRequests[0].ShareSessionEpoch).IsEqualTo(0);
        await Assert.That(AcknowledgedOffsets(connection.ShareFetchRequests[0])).IsEmpty();
        await Assert.That(connection.ShareFetchRequests[1].ShareSessionEpoch).IsEqualTo(1);
        await Assert.That(AcknowledgedOffsets(connection.ShareFetchRequests[1])).IsEquivalentTo([42L]);
        await Assert.That(outcomes).HasSingleItem();
        await Assert.That(outcomes[0].Offsets).IsEquivalentTo([42L]);
        await Assert.That(outcomes[0].Error).IsNull();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
    }

    [Test]
    public async Task Poll_ResponseLostAfterTheBrokerAdvancedTheSession_OpensANewSession()
    {
        using var cancellation = new CancellationTokenSource();
        var broker = new ShareSessionBroker(epoch: 3) { LoseNextResponse = true };
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = (request, _) => broker.Fetch(request, cancelAfter: 3, cancellation)
        };
        await using var fixture = CreateFixture(connection, retryBackoffMs: 1);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        SetSessionEpoch(fixture.Consumer, brokerId: 1, epoch: 3);

        await DrainPollAsync(fixture.Consumer, cancellation.Token);

        await Assert.That(broker.Rejections).IsEmpty();
        await Assert.That(connection.ShareFetchRequests[0].ShareSessionEpoch).IsEqualTo(3);
        await Assert.That(connection.ShareFetchRequests[1].ShareSessionEpoch).IsEqualTo(0);
        await Assert.That(connection.ShareFetchRequests[2].ShareSessionEpoch).IsEqualTo(1);
    }

    [Test]
    public async Task Poll_AcknowledgementsLostInFlight_AreResentOnTheNewSession()
    {
        using var cancellation = new CancellationTokenSource();
        var broker = new ShareSessionBroker(epoch: 3) { LoseNextResponse = true };
        var outcomes = new List<(long[] Offsets, Exception? Error)>();
        var connection = new CapturingConnection(ApiKey.ShareFetch, 2)
        {
            ShareFetchHandler = (request, _) => broker.Fetch(request, cancelAfter: 4, cancellation)
        };
        await using var fixture = CreateFixture(
            connection,
            acknowledgementCommitCallback: results => CaptureOutcomes(results, outcomes),
            retryBackoffMs: 1);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        SetSessionEpoch(fixture.Consumer, brokerId: 1, epoch: 3);
        fixture.Consumer.Acknowledge(CreateRecord(0, 42));

        await DrainPollAsync(fixture.Consumer, cancellation.Token);

        // The lost request's outcome is unknown, so it is reported as failed and resent once a
        // new session exists. The request that opens that session carries no acknowledgements.
        await Assert.That(broker.Rejections).IsEmpty();
        await Assert.That(AcknowledgedOffsets(connection.ShareFetchRequests[0])).IsEquivalentTo([42L]);
        await Assert.That(connection.ShareFetchRequests[1].ShareSessionEpoch).IsEqualTo(0);
        await Assert.That(AcknowledgedOffsets(connection.ShareFetchRequests[1])).IsEmpty();
        await Assert.That(connection.ShareFetchRequests[2].ShareSessionEpoch).IsEqualTo(1);
        await Assert.That(AcknowledgedOffsets(connection.ShareFetchRequests[2])).IsEquivalentTo([42L]);
        await Assert.That(outcomes.Count).IsEqualTo(2);
        await Assert.That(outcomes[0].Error).IsTypeOf<IOException>();
        await Assert.That(outcomes[1].Offsets).IsEquivalentTo([42L]);
        await Assert.That(outcomes[1].Error).IsNull();
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsFalse();
    }

    [Test]
    public async Task Commit_ResponseLost_DoesNotRetryWithAStaleEpoch_AndThePollResendsIt()
    {
        using var cancellation = new CancellationTokenSource();
        var broker = new ShareSessionBroker(epoch: 3) { LoseNextResponse = true };
        var connection = new CapturingConnection(
            ApiKey.ShareAcknowledge,
            2,
            supportShareFetch: true)
        {
            ShareFetchHandler = (request, _) => broker.Fetch(request, cancelAfter: 2, cancellation),
            ShareAcknowledgeHandler = (request, _) => broker.Acknowledge(request)
        };
        await using var fixture = CreateFixture(connection, retryBackoffMs: 1);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Subscribe("topic");
        SetSessionEpoch(fixture.Consumer, brokerId: 1, epoch: 3);
        fixture.Consumer.Acknowledge(CreateRecord(0, 42));

        await Assert.That(async () => await fixture.Consumer.CommitAsync(CancellationToken.None))
            .Throws<Dekaf.Errors.KafkaException>();

        await Assert.That(connection.ShareAcknowledgeRequests).HasSingleItem();
        await Assert.That(GetSessionEpoch(fixture.Consumer, 1)).IsEqualTo(0);
        await Assert.That(HasPendingAcknowledgements(fixture.Consumer)).IsTrue();

        await DrainPollAsync(fixture.Consumer, cancellation.Token);

        await Assert.That(broker.Rejections).IsEmpty();
        await Assert.That(connection.ShareFetchRequests[0].ShareSessionEpoch).IsEqualTo(0);
        await Assert.That(AcknowledgedOffsets(connection.ShareFetchRequests[0])).IsEmpty();
        await Assert.That(connection.ShareFetchRequests[1].ShareSessionEpoch).IsEqualTo(1);
        await Assert.That(AcknowledgedOffsets(connection.ShareFetchRequests[1])).IsEquivalentTo([42L]);
    }

    private static async Task DrainPollAsync(
        Dekaf.ShareConsumer.KafkaShareConsumer<string, string> consumer,
        CancellationToken cancellationToken)
    {
        await using var poll = consumer.PollAsync(cancellationToken).GetAsyncEnumerator(CancellationToken.None);
        try
        {
            while (await poll.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None))
            {
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static void CaptureOutcomes(
        ReadOnlySpan<ShareAcknowledgementCommitResult> results,
        List<(long[] Offsets, Exception? Error)> outcomes)
    {
        foreach (ref readonly var result in results)
            outcomes.Add((CopyOffsets(result.Offsets), result.Exception));
    }

    private static List<long> AcknowledgedOffsets(ShareFetchRequest request)
    {
        var offsets = new List<long>();
        foreach (var topic in request.Topics)
        {
            foreach (var partition in topic.Partitions)
            {
                if (partition.AcknowledgementBatches is not { } batches)
                    continue;
                foreach (var batch in batches)
                {
                    for (var offset = batch.FirstOffset; offset <= batch.LastOffset; offset++)
                        offsets.Add(offset);
                }
            }
        }

        return offsets;
    }

    private static void SetSessionEpoch(
        Dekaf.ShareConsumer.KafkaShareConsumer<string, string> consumer,
        int brokerId,
        int epoch)
    {
        var sessionManager = (ShareSessionManager)typeof(Dekaf.ShareConsumer.KafkaShareConsumer<string, string>)
            .GetField("_sessionManager", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(consumer)!;
        sessionManager.ResetSession(brokerId);
        for (var i = 0; i < epoch; i++)
            sessionManager.IncrementEpoch(brokerId);
    }

    /// <summary>
    /// Enforces the broker's share-session rules (Kafka 4.x SharePartitionManager): epoch 0 opens a
    /// new session and must not carry acknowledgements, ShareAcknowledge cannot open a session,
    /// and every other request must carry the session's current epoch. A lost response is applied
    /// on the broker before the client sees the transport failure.
    /// </summary>
    private sealed class ShareSessionBroker(int epoch = -1)
    {
        private int _epoch = epoch;
        private int _fetches;

        public bool LoseNextResponse { get; set; }

        public List<ErrorCode> Rejections { get; } = [];

        public ValueTask<ShareFetchResponse> Fetch(
            ShareFetchRequest request,
            int cancelAfter,
            CancellationTokenSource cancellation)
        {
            if (++_fetches >= cancelAfter)
                cancellation.Cancel();

            ErrorCode error;
            if (request.ShareSessionEpoch == 0)
            {
                error = HasAcknowledgements(request) ? ErrorCode.InvalidRequest : ErrorCode.None;
                if (error == ErrorCode.None)
                    _epoch = 1;
            }
            else
            {
                error = Advance(request.ShareSessionEpoch);
            }

            if (error != ErrorCode.None)
            {
                Rejections.Add(error);
                return ValueTask.FromResult(new ShareFetchResponse
                {
                    ErrorCode = error,
                    Responses = [],
                    NodeEndpoints = []
                });
            }

            if (LoseNextResponse)
            {
                LoseNextResponse = false;
                return ValueTask.FromException<ShareFetchResponse>(
                    new IOException("Connection reset before the response arrived."));
            }

            return ValueTask.FromResult(new ShareFetchResponse
            {
                ErrorCode = ErrorCode.None,
                Responses = [],
                NodeEndpoints = []
            });
        }

        public ValueTask<ShareAcknowledgeResponse> Acknowledge(ShareAcknowledgeRequest request)
        {
            var error = request.ShareSessionEpoch == 0
                ? ErrorCode.InvalidShareSessionEpoch
                : Advance(request.ShareSessionEpoch);
            if (error != ErrorCode.None)
            {
                Rejections.Add(error);
                return ValueTask.FromResult(new ShareAcknowledgeResponse
                {
                    ErrorCode = error,
                    Responses = [],
                    NodeEndpoints = []
                });
            }

            if (LoseNextResponse)
            {
                LoseNextResponse = false;
                return ValueTask.FromException<ShareAcknowledgeResponse>(
                    new IOException("Connection reset before the response arrived."));
            }

            return ValueTask.FromResult(CreateAcknowledgeResponse((0, ErrorCode.None)));
        }

        private ErrorCode Advance(int requestEpoch)
        {
            if (_epoch < 0)
                return ErrorCode.ShareSessionNotFound;
            if (requestEpoch != _epoch)
                return ErrorCode.InvalidShareSessionEpoch;
            _epoch++;
            return ErrorCode.None;
        }

        private static bool HasAcknowledgements(ShareFetchRequest request)
        {
            foreach (var topic in request.Topics)
            {
                foreach (var partition in topic.Partitions)
                {
                    if (partition.AcknowledgementBatches is { Count: > 0 })
                        return true;
                }
            }

            return false;
        }
    }
}
