using System.Net.Sockets;
using System.Reflection;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

[Timeout(15_000)]
public sealed class IdempotentInitializationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InitializeAsync_ConnectionFailure_TriesNextBroker(bool dnsFailure, CancellationToken cancellationToken)
    {
        await using var harness = new Harness();
        Exception failure = dnsFailure
            ? new DnsResolutionException("offline-broker", 9092, new SocketException((int)SocketError.HostNotFound))
            : new SocketException((int)SocketError.ConnectionRefused);
        harness.Connect = (id, _) => id == harness.BrokerIds[0]
            ? ValueTask.FromException<IKafkaConnection>(failure)
            : ValueTask.FromResult(harness.Connections[id]);

        await harness.Producer.InitializeAsync(cancellationToken);

        await Assert.That(harness.ConnectionAttempts.Take(2).ToArray()).IsEquivalentTo(harness.BrokerIds);
        await Assert.That(harness.Requests.Single().BrokerId).IsEqualTo(harness.BrokerIds[1]);
        await AssertInitializedAsync(harness);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    public async Task InitializeAsync_RetryableSendFailure_TriesNextBroker(int failureKind, CancellationToken cancellationToken)
    {
        Exception failure = failureKind switch
        {
            0 => new SocketException((int)SocketError.ConnectionReset),
            1 => new IOException("Connection closed unexpectedly"),
            2 => new KafkaException(ErrorCode.NetworkException, "Connection lost"),
            3 => new KafkaTimeoutException("Request timed out"),
            _ => new TimeoutException("Connection setup timed out")
        };
        await using var harness = new Harness();
        harness.Send = (id, _) => id == harness.BrokerIds[0]
            ? ValueTask.FromException<InitProducerIdResponse>(failure)
            : ValueTask.FromResult(Success());

        await harness.Producer.InitializeAsync(cancellationToken);

        await Assert.That(harness.Requests.Select(r => r.BrokerId).ToArray()).IsEquivalentTo(harness.BrokerIds);
        await AssertInitializedAsync(harness);
    }

    [Test]
    public async Task InitializeAsync_RetriableResponses_RotatesAndRetriesWholeRound(CancellationToken cancellationToken)
    {
        await using var harness = new Harness();
        harness.Send = (_, _) => ValueTask.FromResult(harness.Requests.Count <= 4
            ? new InitProducerIdResponse { ErrorCode = ErrorCode.CoordinatorLoadInProgress }
            : Success());

        await harness.Producer.InitializeAsync(cancellationToken);

        var expectedOrder = new[]
        {
            harness.BrokerIds[0], harness.BrokerIds[1],
            harness.BrokerIds[0], harness.BrokerIds[1], harness.BrokerIds[0]
        };
        await Assert.That(harness.Requests.Select(r => r.BrokerId).SequenceEqual(expectedOrder)).IsTrue();
        await AssertInitializedAsync(harness);
    }

    [Test]
    public async Task InitializeAsync_MetadataChangesBetweenRounds_UsesCurrentBrokers(CancellationToken cancellationToken)
    {
        await using var harness = new Harness();
        var remainingBroker = harness.Metadata.Metadata.GetBrokers()[1];
        harness.Send = (_, _) =>
        {
            if (harness.Requests.Count == 2)
            {
                harness.Metadata.Metadata.Update(new MetadataResponse
                {
                    Brokers = [new BrokerMetadata
                    {
                        NodeId = remainingBroker.NodeId, Host = remainingBroker.Host, Port = remainingBroker.Port
                    }],
                    Topics = []
                });
            }
            return ValueTask.FromResult(harness.Requests.Count <= 2
                ? new InitProducerIdResponse { ErrorCode = ErrorCode.CoordinatorLoadInProgress }
                : Success());
        };

        await harness.Producer.InitializeAsync(cancellationToken);

        var expectedOrder = new[] { harness.BrokerIds[0], remainingBroker.NodeId, remainingBroker.NodeId };
        await Assert.That(harness.Requests.Select(r => r.BrokerId).SequenceEqual(expectedOrder)).IsTrue();
        await AssertInitializedAsync(harness);
    }

    [Test]
    [Arguments(ErrorCode.ClusterAuthorizationFailed)]
    [Arguments(ErrorCode.TransactionalIdAuthorizationFailed)]
    [Arguments(ErrorCode.UnsupportedVersion)]
    public async Task InitializeAsync_FatalResponse_DoesNotRetry(ErrorCode errorCode, CancellationToken cancellationToken)
    {
        await using var harness = new Harness();
        harness.Send = (_, _) => ValueTask.FromResult(new InitProducerIdResponse { ErrorCode = errorCode });

        var exception = await Assert.That(async () => await harness.Producer.InitializeAsync(cancellationToken))
            .Throws<KafkaException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(errorCode);
        await Assert.That(harness.Requests.Count).IsEqualTo(1);
        await Assert.That(harness.Producer.RecordAccumulator.ProducerId).IsEqualTo(-1L);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InitializeAsync_FatalException_PreservesExceptionWithoutRetry(bool authenticationFailure, CancellationToken cancellationToken)
    {
        Exception failure = authenticationFailure
            ? new AuthenticationException("Invalid credentials")
            : new InvalidOperationException("Invalid client state");
        await using var harness = new Harness();
        harness.Connect = (_, _) => ValueTask.FromException<IKafkaConnection>(failure);

        var exception = await Assert.That(async () => await harness.Producer.InitializeAsync(cancellationToken))
            .Throws<Exception>();

        await Assert.That(ReferenceEquals(exception, failure)).IsTrue();
        await Assert.That(harness.ConnectionAttempts.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InitializeAsync_AllBrokersUnavailable_TimeoutPreservesLastTransportFailure(
        bool dnsFailure, CancellationToken cancellationToken)
    {
        await using var harness = new Harness(maxBlockMs: 1000, retryBackoffMs: 10_000);
        var firstFailure = new SocketException((int)SocketError.ConnectionRefused);
        Exception lastFailure = dnsFailure
            ? new DnsResolutionException("last-broker", 9093, firstFailure)
            : new IOException("Last broker disconnected", firstFailure);
        harness.Connect = (id, _) => ValueTask.FromException<IKafkaConnection>(
            id == harness.BrokerIds[0] ? firstFailure : lastFailure);

        var exception = await Assert.That(async () => await harness.Producer.InitializeAsync(cancellationToken))
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.Configured).IsEqualTo(TimeSpan.FromMilliseconds(1000));
        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception!.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
        await Assert.That(exception!.Message).Contains("InitProducerId");
        await Assert.That(ReferenceEquals(exception!.InnerException, lastFailure)).IsTrue();
        await Assert.That(ReferenceEquals(exception!.InnerException!.InnerException, firstFailure)).IsTrue();
        await Assert.That(harness.ConnectionAttempts.ToArray()).IsEquivalentTo(harness.BrokerIds);
    }

    [Test]
    public async Task InitializeAsync_RetriableResponsesUntilDeadline_PreservesErrorCode(CancellationToken cancellationToken)
    {
        await using var harness = new Harness(maxBlockMs: 1000, retryBackoffMs: 10_000);
        harness.Send = (_, _) => ValueTask.FromResult(new InitProducerIdResponse { ErrorCode = ErrorCode.CoordinatorLoadInProgress });

        var exception = await Assert.That(async () => await harness.Producer.InitializeAsync(cancellationToken))
            .Throws<KafkaTimeoutException>();

        await Assert.That(((KafkaException)exception!.InnerException!).ErrorCode).IsEqualTo(ErrorCode.CoordinatorLoadInProgress);
        await Assert.That(exception!.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
        await Assert.That(harness.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task InitializeAsync_TransportFailuresRecoverOnNextRound_Succeeds(CancellationToken cancellationToken)
    {
        await using var harness = new Harness();
        harness.Connect = (id, _) => harness.ConnectionAttempts.Count <= 2
            ? ValueTask.FromException<IKafkaConnection>(new SocketException((int)SocketError.ConnectionRefused))
            : ValueTask.FromResult(harness.Connections[id]);

        await harness.Producer.InitializeAsync(cancellationToken);

        var expectedOrder = new[] { harness.BrokerIds[0], harness.BrokerIds[1], harness.BrokerIds[0] };
        await Assert.That(harness.ConnectionAttempts.Take(3).SequenceEqual(expectedOrder)).IsTrue();
        await AssertInitializedAsync(harness);
    }

    [Test]
    public async Task InitializeAsync_CancelDuringBackoff_DoesNotAttemptAnotherRound(CancellationToken cancellationToken)
    {
        await using var harness = new Harness(retryBackoffMs: 10_000);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestsObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Send = (_, _) =>
        {
            if (harness.Requests.Count == 2)
                requestsObserved.TrySetResult();

            return ValueTask.FromResult(new InitProducerIdResponse { ErrorCode = ErrorCode.CoordinatorLoadInProgress });
        };

        var initialization = harness.Producer.InitializeAsync(caller.Token).AsTask();
        await requestsObserved.Task.WaitAsync(cancellationToken);
        await Assert.That(harness.Requests.Count).IsEqualTo(2);
        await Assert.That(initialization.IsCompleted).IsFalse();
        caller.Cancel();

        await Assert.That(() => initialization).Throws<OperationCanceledException>();
        await Assert.That(harness.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task InitializeAsync_TimeoutThenRecovery_CanInitializeAgain(CancellationToken cancellationToken)
    {
        await using var harness = new Harness(maxBlockMs: 1000, retryBackoffMs: 10_000);
        harness.Send = (_, _) => ValueTask.FromResult(new InitProducerIdResponse { ErrorCode = ErrorCode.CoordinatorLoadInProgress });
        await Assert.That(async () => await harness.Producer.InitializeAsync(cancellationToken)).Throws<KafkaTimeoutException>();

        harness.Send = (_, _) => ValueTask.FromResult(Success());
        await harness.Producer.InitializeAsync(cancellationToken);

        await AssertInitializedAsync(harness);
    }

    [Test]
    public async Task InitializeAsync_NoKnownBrokers_FailsWithoutConnecting(CancellationToken cancellationToken)
    {
        await using var harness = new Harness();
        harness.Metadata.Metadata.Update(new MetadataResponse { Brokers = [], Topics = [] });

        await Assert.That(async () => await harness.Producer.InitializeAsync(cancellationToken)).Throws<InvalidOperationException>();
        await Assert.That(harness.ConnectionAttempts.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InitializeAsync_StalledOperation_RespectsMaxBlock(bool duringConnect, CancellationToken cancellationToken)
    {
        await using var harness = new Harness(maxBlockMs: 1000);
        if (duringConnect)
            harness.Connect = async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return harness.Connections[1];
            };
        else
            harness.Send = async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return Success();
            };

        var exception = await Assert.That(async () => await harness.Producer.InitializeAsync(cancellationToken))
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
        await Assert.That(harness.ConnectionAttempts.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InitializeAsync_CallerCancellation_StopsInFlightOperation(bool duringConnect, CancellationToken cancellationToken)
    {
        await using var harness = new Harness();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (duringConnect)
            harness.Connect = async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return harness.Connections[1];
            };
        else
            harness.Send = async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                return Success();
            };

        var initialization = harness.Producer.InitializeAsync(caller.Token).AsTask();
        await started.Task.WaitAsync(cancellationToken);
        caller.Cancel();

        await Assert.That(() => initialization).Throws<OperationCanceledException>();
        await Assert.That(harness.ConnectionAttempts.Count).IsEqualTo(1);
        // Cancellation must release both initialization locks and leave initialization retryable.
        harness.Connect = (id, _) => ValueTask.FromResult(harness.Connections[id]);
        harness.Send = (_, _) => ValueTask.FromResult(Success());
        await harness.Producer.InitializeAsync(cancellationToken);
        await AssertInitializedAsync(harness);
    }

    [Test]
    public async Task InitializeAsync_PreCancelled_DoesNotConnect(CancellationToken cancellationToken)
    {
        await using var harness = new Harness();
        using var caller = new CancellationTokenSource();
        caller.Cancel();

        await Assert.That(async () => await harness.Producer.InitializeAsync(caller.Token)).Throws<OperationCanceledException>();
        await Assert.That(harness.ConnectionAttempts.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InitializeAsync_ConcurrentCalls_ObtainOnlyOneProducerId(CancellationToken cancellationToken)
    {
        await using var harness = new Harness();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Send = async (_, token) =>
        {
            started.SetResult();
            await release.Task.WaitAsync(token);
            return Success();
        };

        var first = harness.Producer.InitializeAsync(cancellationToken).AsTask();
        await started.Task.WaitAsync(cancellationToken);
        var second = harness.Producer.InitializeAsync(cancellationToken).AsTask();
        release.SetResult();
        await Task.WhenAll(first, second);
        await harness.Producer.InitializeAsync(cancellationToken);

        await Assert.That(harness.Requests.Count).IsEqualTo(1);
        await AssertInitializedAsync(harness);
    }

    [Test]
    [Arguments(false, null)]
    [Arguments(true, "transactional-producer")]
    public async Task InitializeAsync_NonIdempotentOrTransactional_DoesNotRequestProducerId(bool idempotent, string? transactionalId, CancellationToken cancellationToken)
    {
        await using var harness = new Harness(idempotent: idempotent, transactionalId: transactionalId);
        await harness.Producer.InitializeAsync(cancellationToken);
        await Assert.That(harness.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BumpEpochForRecovery_BelowMaxValue_BumpsLocallyWithoutRequest(CancellationToken cancellationToken)
    {
        await using var harness = new Harness();
        await harness.Producer.InitializeAsync(cancellationToken);
        var accumulator = harness.Producer.RecordAccumulator;
        accumulator.GetAndIncrementSequence(Tp0, 10);
        accumulator.GetAndIncrementSequence(Tp1, 20);

        var bump = harness.Producer.BumpEpochForRecoveryAsync(7, [Tp0], cancellationToken);

        await Assert.That(bump.IsCompletedSuccessfully).IsTrue();
        var state = await bump;
        await Assert.That(state.ProducerId).IsEqualTo(1234L);
        await Assert.That(state.Epoch).IsEqualTo((short)8);
        await Assert.That(accumulator.ProducerId).IsEqualTo(1234L);
        await Assert.That(accumulator.ProducerEpoch).IsEqualTo((short)8);
        // Only the affected partition restarts at 0; the other keeps its counter.
        await Assert.That(accumulator.GetAndIncrementSequence(Tp0, 1)).IsEqualTo(0);
        await Assert.That(accumulator.GetAndIncrementSequence(Tp1, 1)).IsEqualTo(20);
        await Assert.That(harness.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task BumpEpochForRecovery_AlreadyBumpedByAnotherSender_RestartsCallersPartitions(CancellationToken cancellationToken)
    {
        // Two send loops report epoch 7 for different partitions. The first bump restarts only
        // Tp0; the second call must still restart Tp1 under epoch 8, or its re-stamped batch
        // would carry sequence 20 and be rejected as an invalid sequence for the new epoch.
        await using var harness = new Harness();
        await harness.Producer.InitializeAsync(cancellationToken);
        var accumulator = harness.Producer.RecordAccumulator;
        accumulator.GetAndIncrementSequence(Tp0, 10);
        accumulator.GetAndIncrementSequence(Tp1, 20);
        await harness.Producer.BumpEpochForRecoveryAsync(7, [Tp0], cancellationToken);
        await Assert.That(accumulator.GetAndIncrementSequence(Tp0, 3)).IsEqualTo(0);

        var second = harness.Producer.BumpEpochForRecoveryAsync(7, [Tp1], cancellationToken);

        await Assert.That(second.IsCompletedSuccessfully).IsTrue();
        var state = await second;
        await Assert.That(state.ProducerId).IsEqualTo(1234L);
        await Assert.That(state.Epoch).IsEqualTo((short)8);
        await Assert.That(accumulator.ProducerEpoch).IsEqualTo((short)8);
        await Assert.That(accumulator.GetAndIncrementSequence(Tp1, 1)).IsEqualTo(0);
        // Tp0's sequences under epoch 8 are untouched by the second call.
        await Assert.That(accumulator.GetAndIncrementSequence(Tp0, 1)).IsEqualTo(3);
        await Assert.That(harness.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task BumpEpochForRecovery_LateRequestForResequencedPartition_KeepsNewEpochSequences(CancellationToken cancellationToken)
    {
        // With several batches in flight, the successors of a rejected batch fail after the bump
        // that their head already triggered. Their stale request must not zero Tp0 again: the
        // re-stamped head already holds sequence 0 under epoch 8.
        await using var harness = new Harness();
        await harness.Producer.InitializeAsync(cancellationToken);
        var accumulator = harness.Producer.RecordAccumulator;
        accumulator.GetAndIncrementSequence(Tp0, 10);
        await harness.Producer.BumpEpochForRecoveryAsync(7, [Tp0], cancellationToken);
        await Assert.That(accumulator.GetAndIncrementSequence(Tp0, 5)).IsEqualTo(0);

        var state = await harness.Producer.BumpEpochForRecoveryAsync(7, [Tp0], cancellationToken);

        await Assert.That(state.Epoch).IsEqualTo((short)8);
        await Assert.That(accumulator.GetAndIncrementSequence(Tp0, 1)).IsEqualTo(5);
        await Assert.That(harness.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task BumpEpochForRecovery_AtMaxValue_AfterReset_LateRequestKeepsNewProducerIdSequences(CancellationToken cancellationToken)
    {
        // The replacement ID restarted every partition; a late request for the exhausted epoch
        // naming a partition that already produced under the new ID must leave it alone.
        await using var harness = new Harness();
        await harness.Producer.InitializeAsync(cancellationToken);
        var accumulator = harness.Producer.RecordAccumulator;
        await ExhaustEpochSpaceAsync(harness, cancellationToken);
        accumulator.GetAndIncrementSequence(Tp0, 10);
        accumulator.GetAndIncrementSequence(Tp1, 20);
        harness.Send = (_, _) => ValueTask.FromResult(ReplacementProducerId());
        await harness.Producer.BumpEpochForRecoveryAsync(short.MaxValue, [Tp0], cancellationToken);
        await Assert.That(accumulator.GetAndIncrementSequence(Tp1, 3)).IsEqualTo(0);

        var state = await harness.Producer.BumpEpochForRecoveryAsync(short.MaxValue, [Tp1], cancellationToken);

        await Assert.That(state.ProducerId).IsEqualTo(5678L);
        await Assert.That(state.Epoch).IsEqualTo((short)0);
        await Assert.That(accumulator.GetAndIncrementSequence(Tp1, 1)).IsEqualTo(3);
        await Assert.That(harness.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task BumpEpochForRecovery_AtMaxValue_RequestsNewProducerIdAndRestartsAllSequences(CancellationToken cancellationToken)
    {
        // Regression: the local bump used to throw "Producer epoch overflow — requires producer
        // restart" at short.MaxValue. Java replaces the producer ID instead (resetIdempotentProducerId).
        await using var harness = new Harness();
        await harness.Producer.InitializeAsync(cancellationToken);
        var accumulator = harness.Producer.RecordAccumulator;
        await ExhaustEpochSpaceAsync(harness, cancellationToken);
        accumulator.GetAndIncrementSequence(Tp0, 10);
        accumulator.GetAndIncrementSequence(Tp1, 20);
        harness.Send = (_, _) => ValueTask.FromResult(ReplacementProducerId());

        var state = await harness.Producer.BumpEpochForRecoveryAsync(short.MaxValue, [Tp0], cancellationToken);

        await Assert.That(state.ProducerId).IsEqualTo(5678L);
        await Assert.That(state.Epoch).IsEqualTo((short)0);
        await Assert.That(accumulator.ProducerId).IsEqualTo(5678L);
        await Assert.That(accumulator.ProducerEpoch).IsEqualTo((short)0);
        // The broker holds no state for the new ID, so every partition restarts at 0, not only Tp0.
        await Assert.That(accumulator.GetAndIncrementSequence(Tp0, 1)).IsEqualTo(0);
        await Assert.That(accumulator.GetAndIncrementSequence(Tp1, 1)).IsEqualTo(0);
        await Assert.That(harness.Requests.Count).IsEqualTo(2);
        var (_, resetRequest) = harness.Requests[1];
        await Assert.That(resetRequest.TransactionalId).IsNull();
        await Assert.That(resetRequest.ProducerId).IsEqualTo(-1L);
        await Assert.That(resetRequest.ProducerEpoch).IsEqualTo((short)-1);
    }

    [Test]
    public async Task BumpEpochForRecovery_AtMaxValue_ConcurrentSenders_RequestOneProducerId(CancellationToken cancellationToken)
    {
        await using var harness = new Harness();
        await harness.Producer.InitializeAsync(cancellationToken);
        await ExhaustEpochSpaceAsync(harness, cancellationToken);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Send = async (_, token) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            return ReplacementProducerId();
        };

        var first = harness.Producer.BumpEpochForRecoveryAsync(short.MaxValue, [Tp0], cancellationToken).AsTask();
        await started.Task.WaitAsync(cancellationToken);
        var second = harness.Producer.BumpEpochForRecoveryAsync(short.MaxValue, [Tp1], cancellationToken).AsTask();
        release.SetResult();
        var states = await Task.WhenAll(first, second);

        await Assert.That(states[0].ProducerId).IsEqualTo(5678L);
        await Assert.That(states[1].ProducerId).IsEqualTo(5678L);
        await Assert.That(states[0].Epoch).IsEqualTo((short)0);
        await Assert.That(states[1].Epoch).IsEqualTo((short)0);
        await Assert.That(harness.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task BumpEpochForRecovery_AtMaxValue_AfterReset_StaleExpectedEpochDoesNotRequestAgain(CancellationToken cancellationToken)
    {
        // A response for a batch sent under the exhausted ID can still arrive after the reset and
        // signal a bump for short.MaxValue; it must not replace the producer ID a second time.
        await using var harness = new Harness();
        await harness.Producer.InitializeAsync(cancellationToken);
        await ExhaustEpochSpaceAsync(harness, cancellationToken);
        harness.Send = (_, _) => ValueTask.FromResult(ReplacementProducerId());
        await harness.Producer.BumpEpochForRecoveryAsync(short.MaxValue, [Tp0], cancellationToken);

        var state = await harness.Producer.BumpEpochForRecoveryAsync(short.MaxValue, [Tp0], cancellationToken);

        await Assert.That(state.ProducerId).IsEqualTo(5678L);
        await Assert.That(state.Epoch).IsEqualTo((short)0);
        await Assert.That(harness.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task BumpEpochForRecovery_AtMaxValue_RequestFailure_KeepsStateAndRetriesLater(CancellationToken cancellationToken)
    {
        await using var harness = new Harness();
        await harness.Producer.InitializeAsync(cancellationToken);
        var accumulator = harness.Producer.RecordAccumulator;
        await ExhaustEpochSpaceAsync(harness, cancellationToken);
        accumulator.GetAndIncrementSequence(Tp0, 10);
        harness.Send = (_, _) => ValueTask.FromResult(new InitProducerIdResponse { ErrorCode = ErrorCode.ClusterAuthorizationFailed });

        var exception = await Assert.That(async () =>
                await harness.Producer.BumpEpochForRecoveryAsync(short.MaxValue, [Tp0], cancellationToken))
            .Throws<KafkaException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
        await Assert.That(accumulator.ProducerId).IsEqualTo(1234L);
        await Assert.That(accumulator.ProducerEpoch).IsEqualTo(short.MaxValue);
        await Assert.That(accumulator.GetAndIncrementSequence(Tp0, 1)).IsEqualTo(10);

        harness.Send = (_, _) => ValueTask.FromResult(ReplacementProducerId());
        var state = await harness.Producer.BumpEpochForRecoveryAsync(short.MaxValue, [Tp0], cancellationToken);
        await Assert.That(state.ProducerId).IsEqualTo(5678L);
        await Assert.That(accumulator.ProducerEpoch).IsEqualTo((short)0);
        await Assert.That(harness.Requests.Count).IsEqualTo(3);
    }

    private static readonly TopicPartition Tp0 = new("test-topic", 0);
    private static readonly TopicPartition Tp1 = new("test-topic", 1);

    private static InitProducerIdResponse ReplacementProducerId() => new()
    {
        ErrorCode = ErrorCode.None,
        ProducerId = 5678,
        ProducerEpoch = 0
    };

    /// <summary>
    /// Drives the epoch to <see cref="short.MaxValue"/> through the same local bumps a real
    /// producer performs, without touching any partition's sequence counter or the broker.
    /// </summary>
    private static async Task ExhaustEpochSpaceAsync(Harness harness, CancellationToken cancellationToken)
    {
        var accumulator = harness.Producer.RecordAccumulator;
        while (accumulator.ProducerEpoch < short.MaxValue)
        {
            var bump = harness.Producer.BumpEpochForRecoveryAsync(
                accumulator.ProducerEpoch, Array.Empty<TopicPartition>(), cancellationToken);
            await Assert.That(bump.IsCompletedSuccessfully).IsTrue();
            await bump;
        }

        await Assert.That(harness.Requests.Count).IsEqualTo(1);
    }

    private static async Task AssertInitializedAsync(Harness harness)
    {
        await Assert.That(harness.Producer.RecordAccumulator.ProducerId).IsEqualTo(1234L);
        await Assert.That(harness.Producer.RecordAccumulator.ProducerEpoch).IsEqualTo((short)7);
        foreach (var (_, request) in harness.Requests)
        {
            await Assert.That(request.TransactionalId).IsNull();
            await Assert.That(request.TransactionTimeoutMs).IsEqualTo(-1);
            await Assert.That(request.ProducerId).IsEqualTo(-1L);
            await Assert.That(request.ProducerEpoch).IsEqualTo((short)-1);
        }
    }

    private static InitProducerIdResponse Success() => new()
    {
        ErrorCode = ErrorCode.None,
        ProducerId = 1234,
        ProducerEpoch = 7
    };

    private sealed class Harness : IAsyncDisposable
    {
        private readonly ConnectionPool _pool;
        private readonly MetadataManager _metadata;
        public MetadataManager Metadata => _metadata;
        public KafkaProducer<string, string> Producer { get; }
        public Dictionary<int, IKafkaConnection> Connections { get; } = [];
        public int[] BrokerIds { get; }
        public List<int> ConnectionAttempts { get; } = [];
        public List<(int BrokerId, InitProducerIdRequest Request)> Requests { get; } = [];
        public Func<int, CancellationToken, ValueTask<IKafkaConnection>> Connect { get; set; }
        public Func<int, CancellationToken, ValueTask<InitProducerIdResponse>> Send { get; set; } = (_, _) => ValueTask.FromResult(Success());

        public Harness(int maxBlockMs = 5000, int retryBackoffMs = 1, bool idempotent = true, string? transactionalId = null)
        {
            Connect = (id, _) => ValueTask.FromResult(Connections[id]);
            _pool = new ConnectionPool("idempotent-init-test", new ConnectionOptions { ReconnectBackoff = TimeSpan.Zero }, 1,
                (id, _, _, _, token) =>
                {
                    ConnectionAttempts.Add(id);
                    return Connect(id, token);
                });
            _metadata = new MetadataManager(_pool, ["localhost:9092"]);
            _metadata.Metadata.Update(new MetadataResponse
            {
                Brokers =
                [
                    new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 },
                    new BrokerMetadata { NodeId = 2, Host = "localhost", Port = 9093 }
                ],
                Topics = []
            });
            // Skip only metadata bootstrap; exercise the public producer initialization path.
            typeof(MetadataManager).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_metadata, true);
            _metadata.SetApiVersion(ApiKey.InitProducerId, 2, 5);
            BrokerIds = _metadata.Metadata.GetBrokers().Select(broker => broker.NodeId).ToArray();
            foreach (var broker in _metadata.Metadata.GetBrokers())
            {
                _pool.RegisterBroker(broker.NodeId, broker.Host, broker.Port);
                var connection = Substitute.For<IKafkaConnection>();
                connection.BrokerId.Returns(broker.NodeId);
                connection.Host.Returns(broker.Host);
                connection.Port.Returns(broker.Port);
                connection.IsConnected.Returns(true);
                connection.SendAsync<InitProducerIdRequest, InitProducerIdResponse>(
                        Arg.Any<InitProducerIdRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                    .Returns(call =>
                    {
                        Requests.Add((broker.NodeId, call.ArgAt<InitProducerIdRequest>(0)));
                        return Send(broker.NodeId, call.ArgAt<CancellationToken>(2));
                    });
                Connections.Add(broker.NodeId, connection);
            }
            Producer = new KafkaProducer<string, string>(new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                EnableIdempotence = idempotent,
                TransactionalId = transactionalId,
                MaxBlockMs = maxBlockMs,
                RetryBackoffMs = retryBackoffMs,
                RetryBackoffMaxMs = retryBackoffMs,
                CloseTimeoutMs = 100
            }, Serializers.String, Serializers.String, _pool, _metadata, DekafMemoryBudget.Global);
        }

        public async ValueTask DisposeAsync()
        {
            await Producer.DisposeAsync();
            await _metadata.DisposeAsync();
            await _pool.DisposeAsync();
        }
    }
}
