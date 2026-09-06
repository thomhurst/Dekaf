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
