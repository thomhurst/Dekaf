using System.Net.Sockets;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Retry;
using NSubstitute;

namespace Dekaf.Tests.Unit.Retry;

public sealed class RetryHelperTests
{
    [Test]
    [MethodDataSource(nameof(FailureClassifications))]
    public async Task FailureClassification_PreservesBrokerAndRequestPolicies(
        string scenario, Func<Exception> createFailure, bool retryBroker, bool retryRequest)
    {
        var failure = createFailure();
        await Assert.That(RetryHelper.IsRetriableBrokerFailure(failure)).IsEqualTo(retryBroker);
        await Assert.That(RetryHelper.IsRetriableRequestFailure(failure)).IsEqualTo(retryRequest);
    }

    public static IEnumerable<(string Scenario, Func<Exception> CreateFailure, bool RetryBroker, bool RetryRequest)> FailureClassifications()
    {
        yield return ("socket", static () => new SocketException((int)SocketError.ConnectionRefused), true, true);
        yield return ("io", static () => new IOException("connection reset"), true, true);
        yield return ("request timeout", static () => new TimeoutException("request timed out"), true, true);
        yield return ("dns", static () => new DnsResolutionException("broker", 9092), true, true);
        yield return ("Kafka timeout", static () => new KafkaTimeoutException("operation timed out"), true, false);
        yield return ("retriable Kafka error", static () => CreateRequestTimeout(), true, true);
        yield return ("fatal Kafka error", static () => new KafkaException(ErrorCode.UnsupportedVersion, "unsupported"), false, false);
        yield return ("unclassified Kafka error", static () => new KafkaException("unknown error"), false, false);
        yield return ("retry override", static () => new KafkaException(ErrorCode.UnknownServerError, "retry", isRetriable: true), true, true);
        yield return ("fatal override", static () => new KafkaException(ErrorCode.RequestTimedOut, "terminal", isRetriable: false), false, false);
        yield return ("authentication", static () => new AuthenticationException("invalid credentials", new IOException("transport")), false, false);
        yield return ("bootstrap deadline", static () => new BootstrapResolutionException(
            ["broker:9092"], TimeSpan.FromSeconds(1), new DnsResolutionException("broker", 9092)), false, false);
        yield return ("cancellation", static () => new OperationCanceledException(), false, false);
        yield return ("task cancellation", static () => new TaskCanceledException(), false, false);
        yield return ("disposed", static () => new ObjectDisposedException("connection"), false, false);
        yield return ("invalid state", static () => new InvalidOperationException("invalid state"), false, false);
        yield return ("wrapped transport", static () => new InvalidOperationException("wrapper", new IOException("transport")), false, true);
        yield return ("aggregate transport", static () => new AggregateException(new IOException("transport")), false, true);
        yield return ("aggregate fatal", static () => new AggregateException(new AuthenticationException("invalid credentials")), false, false);
    }

    [Test]
    public async Task MetadataRefreshUnavailable_RetriesOriginalOperation()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        var result = await RetryHelper.WithRetryAsync(
            () => Interlocked.Increment(ref attempts) == 1
                ? ValueTask.FromException<int>(CreateRequestTimeout())
                : ValueTask.FromResult(42),
            metadataManager,
            CancellationToken.None,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            maxRetries: 1);

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task MetadataRefreshUnavailable_PreservesFinalKafkaFailure()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await RetryHelper.WithRetryAsync<int>(
                () =>
                {
                    Interlocked.Increment(ref attempts);
                    return ValueTask.FromException<int>(CreateRequestTimeout());
                },
                metadataManager,
                CancellationToken.None,
                retryBackoffMs: 0,
                retryBackoffMaxMs: 0,
                maxRetries: 1));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task TransportFailure_RetriesOriginalOperation()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        var result = await RetryHelper.WithRetryAsync(
            () => Interlocked.Increment(ref attempts) == 1
                ? ValueTask.FromException<int>(new IOException("connection reset"))
                : ValueTask.FromResult(42),
            metadataManager,
            CancellationToken.None,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            maxRetries: 1);

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task DnsResolutionFailure_RetriesOriginalOperation()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        var result = await RetryHelper.WithRetryAsync(
            () => Interlocked.Increment(ref attempts) == 1
                ? ValueTask.FromException<int>(new DnsResolutionException(
                    "broker",
                    9092,
                    new SocketException((int)SocketError.HostNotFound)))
                : ValueTask.FromResult(42),
            metadataManager,
            CancellationToken.None,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            maxRetries: 1);

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task BootstrapResolutionDeadline_RemainsNonRetriable()
    {
        var exception = new BootstrapResolutionException(
            ["broker:9092"],
            TimeSpan.FromSeconds(1),
            new DnsResolutionException(
                "broker",
                9092,
                new SocketException((int)SocketError.HostNotFound)));

        await Assert.That(RetryHelper.IsRetriableRequestFailure(exception)).IsFalse();
    }

    [Test]
    public async Task CancellationDuringBackoff_StopsRetrying()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await RetryHelper.WithRetryAsync(
                () =>
                {
                    Interlocked.Increment(ref attempts);
                    return ValueTask.FromException(CreateRequestTimeout());
                },
                metadataManager,
                cancellation.Token,
                retryBackoffMs: 1000,
                retryBackoffMaxMs: 1000,
                onRetry: _ =>
                {
                    cancellation.Cancel();
                    return ValueTask.CompletedTask;
                },
                maxRetries: 3));

        await Assert.That(attempts).IsEqualTo(1);
    }

    [Test]
    public async Task DeadlineMode_TransportFailuresBeyondMaxRetries_KeepRetryingUntilTheBrokerReturns()
    {
        // A killed broker stays in cluster metadata for seconds; the count-bounded mode burns its
        // three retries inside that window and surfaces the raw SocketException.
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        var result = await RetryHelper.WithRetryAsync(
            () => Interlocked.Increment(ref attempts) <= 10
                ? ValueTask.FromException<int>(new SocketException((int)SocketError.ConnectionRefused))
                : ValueTask.FromResult(42),
            metadataManager,
            CancellationToken.None,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            deadline: new RetryDeadline("TestOperation", TimeSpan.FromSeconds(30)));

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(attempts).IsEqualTo(11);
    }

    [Test]
    public async Task CountMode_TransportFailuresBeyondMaxRetries_StillSurfaceTheRawFailure()
    {
        // The count-bounded mode is unchanged: AdminClient retries are not idempotent.
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        await Assert.ThrowsAsync<SocketException>(async () =>
            await RetryHelper.WithRetryAsync<int>(
                () =>
                {
                    Interlocked.Increment(ref attempts);
                    return ValueTask.FromException<int>(new SocketException((int)SocketError.ConnectionRefused));
                },
                metadataManager,
                CancellationToken.None,
                retryBackoffMs: 0,
                retryBackoffMaxMs: 0));

        await Assert.That(attempts).IsEqualTo(RetryHelper.MaxRetries + 1);
    }

    [Test]
    public async Task DeadlineMode_BudgetExhausted_ThrowsTypedTimeoutWithTheTransportCause()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        var failure = new SocketException((int)SocketError.ConnectionRefused);

        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await RetryHelper.WithRetryAsync<int>(
                () => ValueTask.FromException<int>(failure),
                metadataManager,
                CancellationToken.None,
                retryBackoffMs: 5,
                retryBackoffMaxMs: 5,
                deadline: new RetryDeadline("TestOperation", TimeSpan.FromMilliseconds(100))));

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception.InnerException).IsSameReferenceAs(failure);
        await Assert.That(exception.Message).Contains("TestOperation");
    }

    [Test]
    public async Task DeadlineMode_TokenIsTheDeadline_CancellationCarriesTheTransportCause()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        using var operationDeadline = new CancellationTokenSource();
        var failure = new IOException("connection reset");
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await RetryHelper.WithRetryAsync<int>(
                () =>
                {
                    if (Interlocked.Increment(ref attempts) == 3)
                        operationDeadline.Cancel();
                    return ValueTask.FromException<int>(failure);
                },
                metadataManager,
                operationDeadline.Token,
                retryBackoffMs: 1,
                retryBackoffMaxMs: 1,
                deadline: new RetryDeadline("TestOperation", Timeout.InfiniteTimeSpan)));

        await Assert.That(exception!.InnerException).IsSameReferenceAs(failure);
        await Assert.That(attempts).IsEqualTo(3);
    }

    [Test]
    public async Task DeadlineMode_BrokerAnsweredRetriableError_KeepsTheCountBound()
    {
        // A retriable error a live broker answered with says nothing about an outage; its typed
        // failure must reach the caller after the usual retries, not after the whole deadline.
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await RetryHelper.WithRetryAsync<int>(
                () =>
                {
                    Interlocked.Increment(ref attempts);
                    return ValueTask.FromException<int>(CreateRequestTimeout());
                },
                metadataManager,
                CancellationToken.None,
                retryBackoffMs: 0,
                retryBackoffMaxMs: 0,
                maxRetries: 2,
                deadline: new RetryDeadline("TestOperation", TimeSpan.FromSeconds(30))));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
        await Assert.That(attempts).IsEqualTo(3);
    }

    [Test]
    public async Task DeadlineMode_PeerClosedConnection_IsTransportLevelNotBrokerAnswered()
    {
        // NETWORK_EXCEPTION is the connection's own report that the peer went away before a
        // response arrived. It carries an error code, but no broker answered: it must ride the
        // deadline like a socket failure, not stop at the count bound.
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        var result = await RetryHelper.WithRetryAsync(
            () => Interlocked.Increment(ref attempts) <= 10
                ? ValueTask.FromException<int>(new KafkaException(
                    ErrorCode.NetworkException, "Connection closed by remote peer (EOF)", isRetriable: true))
                : ValueTask.FromResult(42),
            metadataManager,
            CancellationToken.None,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            maxRetries: 2,
            deadline: new RetryDeadline("TestOperation", TimeSpan.FromSeconds(30)));

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(attempts).IsEqualTo(11);
    }

    [Test]
    public async Task CountMode_PeerClosedConnection_IsRetried()
    {
        // The count-bounded mode follows IsRetriable, so the typed failure is retried there too.
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        var result = await RetryHelper.WithRetryAsync(
            () => Interlocked.Increment(ref attempts) == 1
                ? ValueTask.FromException<int>(new KafkaException(
                    ErrorCode.NetworkException, "Connection closed by remote peer (EOF)", isRetriable: true))
                : ValueTask.FromResult(42),
            metadataManager,
            CancellationToken.None,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0);

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task DeadlineMode_RecoveryStepFailsWithTransport_IsRetriedInsteadOfEscaping()
    {
        // Re-discovering a coordinator through stale metadata fails the same way the request
        // did. In count mode that failure escaped the retry loop on the spot.
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;
        var recoveries = 0;

        var result = await RetryHelper.WithRetryAsync(
            () => Interlocked.Increment(ref attempts) == 1
                ? ValueTask.FromException<int>(new IOException("connection reset"))
                : ValueTask.FromResult(42),
            metadataManager,
            CancellationToken.None,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            onRetry: _ => Interlocked.Increment(ref recoveries) <= 2
                ? ValueTask.FromException(new GroupException(
                    ErrorCode.CoordinatorNotAvailable,
                    "FindCoordinator failed after 5 retries",
                    new SocketException((int)SocketError.ConnectionRefused)))
                : ValueTask.CompletedTask,
            deadline: new RetryDeadline("TestOperation", TimeSpan.FromSeconds(30)));

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(recoveries).IsEqualTo(3);
        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task DeadlineMode_RecoverySpendsTheBudget_TheRequestIsNotRepeatedPastTheDeadline()
    {
        // No token stands behind the budget here, as for an offset lookup: a recovery that
        // outlasts it must end the call, not hand over to a backoff and one more request.
        await using var metadataManager = CreateUnavailableMetadataManager();
        var failure = new SocketException((int)SocketError.ConnectionRefused);
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            await RetryHelper.WithRetryAsync(
                () => Interlocked.Increment(ref attempts) == 1
                    ? ValueTask.FromException<int>(failure)
                    : ValueTask.FromResult(42),
                metadataManager,
                CancellationToken.None,
                retryBackoffMs: 0,
                retryBackoffMaxMs: 0,
                onRetry: static async token => await Task.Delay(TimeSpan.FromMilliseconds(400), token),
                deadline: new RetryDeadline("TestOperation", TimeSpan.FromMilliseconds(200))));

        await Assert.That(exception!.InnerException).IsSameReferenceAs(failure);
        await Assert.That(attempts).IsEqualTo(1);
    }

    [Test]
    public async Task DeadlineMode_RetiredConnection_RetriedOnlyWhileTheOwnerIsAlive()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;
        var ownerDisposed = false;

        var result = await RetryHelper.WithRetryAsync(
            () => Interlocked.Increment(ref attempts) == 1
                ? ValueTask.FromException<int>(new ObjectDisposedException("KafkaConnection"))
                : ValueTask.FromResult(42),
            metadataManager,
            CancellationToken.None,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0,
            deadline: new RetryDeadline("TestOperation", TimeSpan.FromSeconds(30), () => ownerDisposed));

        await Assert.That(result).IsEqualTo(42);

        ownerDisposed = true;
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await RetryHelper.WithRetryAsync<int>(
                () => ValueTask.FromException<int>(new ObjectDisposedException("ConnectionPool")),
                metadataManager,
                CancellationToken.None,
                retryBackoffMs: 0,
                retryBackoffMaxMs: 0,
                deadline: new RetryDeadline("TestOperation", TimeSpan.FromSeconds(30), () => ownerDisposed)));
    }

    [Test]
    public async Task DeadlineMode_FatalFailure_PropagatesWithoutRetry()
    {
        await using var metadataManager = CreateUnavailableMetadataManager();
        var attempts = 0;

        await Assert.ThrowsAsync<AuthenticationException>(async () =>
            await RetryHelper.WithRetryAsync<int>(
                () =>
                {
                    Interlocked.Increment(ref attempts);
                    return ValueTask.FromException<int>(
                        new AuthenticationException("invalid credentials", new IOException("transport")));
                },
                metadataManager,
                CancellationToken.None,
                retryBackoffMs: 0,
                retryBackoffMaxMs: 0,
                deadline: new RetryDeadline("TestOperation", TimeSpan.FromSeconds(30))));

        await Assert.That(attempts).IsEqualTo(1);
    }

    private static MetadataManager CreateUnavailableMetadataManager()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        connectionPool.GetConnectionAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<IKafkaConnection>(new TimeoutException("broker unavailable")));

        return new MetadataManager(
            connectionPool,
            ["localhost:9092"],
            new MetadataOptions
            {
                EnableBackgroundRefresh = false,
                MetadataRecoveryStrategy = MetadataRecoveryStrategy.None
            });
    }

    private static KafkaException CreateRequestTimeout() =>
        new(ErrorCode.RequestTimedOut, "request timed out");
}
