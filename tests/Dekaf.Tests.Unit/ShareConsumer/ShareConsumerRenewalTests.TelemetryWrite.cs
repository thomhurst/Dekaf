using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerRenewalTests
{
    [Test]
    [Arguments(TelemetryRequestKind.Fetch, false, false)]
    [Arguments(TelemetryRequestKind.Fetch, true, false)]
    [Arguments(TelemetryRequestKind.InlineAcknowledgements, false, false)]
    [Arguments(TelemetryRequestKind.InlineAcknowledgements, true, false)]
    [Arguments(TelemetryRequestKind.StandaloneAcknowledgements, false, false)]
    [Arguments(TelemetryRequestKind.StandaloneAcknowledgements, true, false)]
    [Arguments(TelemetryRequestKind.SessionClose, false, false)]
    [Arguments(TelemetryRequestKind.SessionClose, true, false)]
    [Arguments(TelemetryRequestKind.Fetch, false, true)]
    [Arguments(TelemetryRequestKind.InlineAcknowledgements, false, true)]
    [Arguments(TelemetryRequestKind.StandaloneAcknowledgements, false, true)]
    [Arguments(TelemetryRequestKind.SessionClose, false, true)]
    [Timeout(30_000)]
    public async Task TelemetryWrite_CancellationCountsOnlySubmittedRequests(
        TelemetryRequestKind kind, bool afterWrite, bool subscribeDuringLease, CancellationToken cancellationToken)
    {
        var gate = new TelemetryWriteGate(afterWrite);
        var connection = new CapturingConnection(
            kind == TelemetryRequestKind.StandaloneAcknowledgements ? ApiKey.ShareAcknowledge : ApiKey.ShareFetch, 2)
        {
            BeforeWrite = gate.BeforeWriteAsync,
            ShareFetchHandler = async (_, token) =>
            {
                await gate.BeforeResponseAsync(token);
                return new ShareFetchResponse { ErrorCode = ErrorCode.None, Responses = [], NodeEndpoints = [] };
            },
            ShareAcknowledgeHandler = async (_, token) =>
            {
                await gate.BeforeResponseAsync(token);
                return new ShareAcknowledgeResponse { Responses = [], NodeEndpoints = [] };
            }
        };
        var leaseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = CreateFixture(connection, leaseHandler: subscribeDuringLease
            ? async token =>
            {
                leaseEntered.TrySetResult();
                await releaseLease.Task.WaitAsync(token);
                return connection;
            }
            : null);
        var metrics = EnableShareTelemetry(fixture.Consumer);
        if (subscribeDuringLease) metrics.Disable();
        using var requestCancellation = new CancellationTokenSource();
        var request = InvokeTelemetryRequest(fixture.Consumer, kind, requestCancellation.Token);
        var canceled = false;
        try
        {
            if (subscribeDuringLease)
            {
                await leaseEntered.Task.WaitAsync(cancellationToken);
                EnableShareTelemetry(fixture.Consumer);
                releaseLease.TrySetResult();
            }
            await gate.Entered.Task.WaitAsync(cancellationToken);
            await Assert.That(gate.ObservedToken).IsEqualTo(requestCancellation.Token);
            await requestCancellation.CancelAsync();
            try { await request.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested) { canceled = true; }
        }
        finally
        {
            releaseLease.TrySetResult();
            gate.Release();
            await request.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        if (kind is TelemetryRequestKind.Fetch or TelemetryRequestKind.InlineAcknowledgements)
            await Assert.That(canceled).IsTrue();
        var fetches = kind == TelemetryRequestKind.StandaloneAcknowledgements ? 0d : 1d;
        var acknowledgements = kind is TelemetryRequestKind.InlineAcknowledgements
            or TelemetryRequestKind.StandaloneAcknowledgements ? 2d : 0d;
        await Assert.That(connection.SendCount).IsEqualTo(afterWrite ? 1 : 0);
        await Assert.That(ShareMetricValue(metrics, "fetch.total")).IsEqualTo(afterWrite ? fetches : 0d);
        await Assert.That(ShareMetricValue(metrics, "acknowledgements.send.total")).IsEqualTo(afterWrite ? acknowledgements : 0d);
        await Assert.That(ShareMetricValue(metrics, "acknowledgements.error.total")).IsEqualTo(afterWrite ? acknowledgements : 0d);

        // The same broker state must reset after either canceled attempt.
        await InvokeTelemetryRequest(fixture.Consumer, kind, cancellationToken);
        await Assert.That(ShareMetricValue(metrics, "fetch.total")).IsEqualTo(fetches * (afterWrite ? 2 : 1));
        await Assert.That(ShareMetricValue(metrics, "acknowledgements.send.total")).IsEqualTo(acknowledgements * (afterWrite ? 2 : 1));
        await Assert.That(ShareMetricValue(metrics, "acknowledgements.error.total")).IsEqualTo(afterWrite ? acknowledgements : 0d);
    }

    public enum TelemetryRequestKind { Fetch, InlineAcknowledgements, StandaloneAcknowledgements, SessionClose }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Timeout(30_000)]
    public async Task TelemetryWrite_OrdinaryCommitKeepsCanceledOutcome(bool afterWrite, CancellationToken cancellationToken)
    {
        var gate = new TelemetryWriteGate(afterWrite);
        var connection = new CapturingConnection(ApiKey.ShareAcknowledge, 2)
        {
            BeforeWrite = gate.BeforeWriteAsync,
            ShareAcknowledgeHandler = async (_, token) =>
            {
                await gate.BeforeResponseAsync(token);
                return CreateAcknowledgeResponse((0, ErrorCode.None));
            }
        };
        var failures = new List<Exception?>();
        await using var fixture = CreateFixture(connection, acknowledgementCommitCallback: results =>
        {
            foreach (ref readonly var result in results) failures.Add(result.Exception);
        });
        var metrics = EnableShareTelemetry(fixture.Consumer);
        PrepareForPoll(fixture.Consumer);
        fixture.Consumer.Acknowledge(CreateRecord(), AcknowledgeType.Accept);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var commit = fixture.Consumer.CommitAsync(caller.Token).AsTask();
        try
        {
            await gate.Entered.Task.WaitAsync(cancellationToken);
            await caller.CancelAsync();
            await Assert.That(async () => await commit.WaitAsync(cancellationToken)).Throws<OperationCanceledException>();
        }
        finally
        {
            gate.Release();
            await commit.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        await Assert.That(failures.Count).IsEqualTo(1);
        await Assert.That(failures[0] is OperationCanceledException).IsTrue();
        await Assert.That(ShareMetricValue(metrics, "acknowledgements.send.total")).IsEqualTo(afterWrite ? 1d : 0d);
        await Assert.That(ShareMetricValue(metrics, "acknowledgements.error.total")).IsEqualTo(afterWrite ? 1d : 0d);
    }

    private static Task InvokeTelemetryRequest(
        KafkaShareConsumer<string, string> consumer, TelemetryRequestKind kind, CancellationToken cancellationToken)
    {
        var partition = new TopicPartition("topic", 0);
        var acknowledgements = new Dictionary<TopicPartition, List<AcknowledgementBatchData>>();
        if (kind is TelemetryRequestKind.InlineAcknowledgements or TelemetryRequestKind.StandaloneAcknowledgements)
            acknowledgements.Add(partition,
                [new AcknowledgementBatchData(42, 44, [(byte)AcknowledgeType.Accept, (byte)AcknowledgeType.Gap, (byte)AcknowledgeType.Release])]);
        return kind switch
        {
            TelemetryRequestKind.StandaloneAcknowledgements => (Task)InvokePrivate(
                consumer, "SendAcknowledgeAsync", 1, acknowledgements, false, cancellationToken, false)!,
            TelemetryRequestKind.SessionClose => (Task)InvokePrivate(
                consumer, "CloseSessionForBrokerAsync", 1, new List<TopicPartition> { partition }, cancellationToken)!,
            _ => (Task)InvokePrivate(consumer, "SendShareFetchForPartitionsAsync", 1,
                new List<TopicPartition> { partition }, acknowledgements, cancellationToken)!
        };
    }

    private sealed class TelemetryWriteGate(bool afterWrite)
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _active = true;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken ObservedToken { get; private set; }
        internal ValueTask BeforeWriteAsync(CancellationToken token) => afterWrite ? default : WaitAsync(token);
        internal ValueTask BeforeResponseAsync(CancellationToken token) => afterWrite ? WaitAsync(token) : default;
        private async ValueTask WaitAsync(CancellationToken token)
        {
            if (!_active) return;
            ObservedToken = token;
            Entered.TrySetResult();
            await _release.Task.WaitAsync(token);
        }
        internal void Release()
        {
            _active = false;
            _release.TrySetResult();
        }
    }
}
