using System.Net.Sockets;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Retry;

namespace Dekaf.Tests.Unit.Retry;

/// <summary>
/// One table for the one transport rule. Every caller that retries a transport failure
/// (consumer and share-consumer group join, the streams member, RetryHelper's deadline mode,
/// the producer's transaction control plane) classifies through
/// <see cref="TransportFailureClassifier"/>; the columns are their policies. A new exception
/// kind or a new caller is a new row or column here, not another private classifier.
/// </summary>
public sealed class TransportFailureClassifierTests
{
    [Test]
    [MethodDataSource(nameof(Matrix))]
    public async Task Classification_MatchesTheSharedTable(
        string kind,
        Func<Exception> createFailure,
        bool request,
        bool groupJoin,
        bool producerTransaction)
    {
        await Assert.That(Classify(createFailure(), TransportRetryPolicy.Request)).IsEqualTo(request)
            .Because($"request policy, {kind}");
        await Assert.That(Classify(createFailure(), TransportRetryPolicy.GroupJoin)).IsEqualTo(groupJoin)
            .Because($"group join policy, {kind}");
        await Assert.That(Classify(createFailure(), TransportRetryPolicy.ProducerTransaction))
            .IsEqualTo(producerTransaction)
            .Because($"producer transaction policy, {kind}");
    }

    public static IEnumerable<(string Kind, Func<Exception> CreateFailure, bool Request, bool GroupJoin, bool ProducerTransaction)> Matrix()
    {
        // Socket-level and connection-setup failures: retriable for everyone.
        yield return ("connection reset",
            static () => new IOException("reset", new SocketException((int)SocketError.ConnectionReset)), true, true, true);
        yield return ("connection refused",
            static () => new SocketException((int)SocketError.ConnectionRefused), true, true, true);
        yield return ("io", static () => new IOException("socket closed mid-request"), true, true, true);
        yield return ("dns", static () => new DnsResolutionException("broker", 9092), true, true, true);
        yield return ("setup timeout", static () => new TimeoutException("connection setup timed out"), true, true, true);

        // A connection retired by pool churn while the owner is alive.
        yield return ("retired connection", static () => new ObjectDisposedException("KafkaConnection"), true, true, true);

        // Client-side routing failures that clear once metadata catches up.
        yield return ("unknown broker", static () => new UnknownBrokerException(7), true, true, true);
        yield return ("connection setup exhausted", static () => new ConnectionSetupExhaustedException(3), true, true, true);
        yield return ("metadata refresh failed",
            static () => new MetadataRefreshFailedException(new SocketException((int)SocketError.ConnectionRefused)),
            true, true, true);
        yield return ("metadata refresh failed without cause",
            static () => new MetadataRefreshFailedException(null), true, true, true);

        // A genuine operation invariant is never retried.
        yield return ("invalid state", static () => new InvalidOperationException("invariant violated"), false, false, false);

        // Wrapped transport failures are unwrapped.
        yield return ("wrapped transport",
            static () => new InvalidOperationException("wrapper", new IOException("transport")), true, true, true);
        yield return ("aggregate transport",
            static () => new AggregateException(new SocketException((int)SocketError.ConnectionRefused)), true, true, true);

        // Fatal for everyone, even when they wrap a transient transport failure.
        yield return ("authentication",
            static () => new AuthenticationException("invalid credentials", new IOException("transport")), false, false, false);
        yield return ("tls handshake",
            static () => AuthenticationException.FromTlsHandshake(
                "TLS handshake failed",
                new System.Security.Authentication.AuthenticationException("The remote certificate is invalid.")),
            false, false, false);
        yield return ("authorization", static () => new AuthorizationException("denied"), false, false, false);
        yield return ("broker version", static () => new BrokerVersionException("unsupported"), false, false, false);
        yield return ("aggregate fatal",
            static () => new AggregateException(new AuthenticationException("invalid credentials")), false, false, false);

        // Cancellation is never a transport failure, even when it carries one as its cause.
        yield return ("cancellation", static () => new OperationCanceledException(), false, false, false);
        yield return ("cancellation with transport cause",
            static () => new OperationCanceledException("cancelled", new IOException("transport")), false, false, false);

        // Kafka failures: request and transaction paths follow IsRetriable; a join loop retries
        // everything it did not exclude, because a closed connection and a request timeout reach
        // it as code-less Kafka exceptions.
        yield return ("retriable kafka error",
            static () => new KafkaException(ErrorCode.RequestTimedOut, "request timed out"), true, true, true);
        yield return ("fatal kafka error",
            static () => new KafkaException(ErrorCode.UnsupportedVersion, "unsupported"), false, true, false);
        // The connection reports a peer that closed mid-request as a retriable NETWORK_EXCEPTION.
        yield return ("connection closed by peer",
            static () => new KafkaException(
                ErrorCode.NetworkException, "Connection closed by remote peer (EOF)", isRetriable: true),
            true, true, true);
        // A code-less Kafka exception carries no retry signal outside a join loop.
        yield return ("code-less kafka error",
            static () => new KafkaException("unclassified"), false, true, false);
        yield return ("kafka timeout", static () => new KafkaTimeoutException("operation timed out"), false, true, false);

        // Typed exclusions.
        yield return ("retriable group error",
            static () => new GroupException(ErrorCode.CoordinatorNotAvailable, "coordinator not available"),
            true, false, true);
        yield return ("transaction error", static () => new TransactionException("invalid transition"), false, true, false);
    }

    [Test]
    public async Task RetiredConnection_IsTerminalOnceTheOwnerIsDisposed()
    {
        var retired = new ObjectDisposedException("ConnectionPool");

        await Assert.That(TransportFailureClassifier.IsRetriable(
            retired, TransportRetryPolicy.GroupJoin, ownerDisposed: false)).IsTrue();
        await Assert.That(TransportFailureClassifier.IsRetriable(
            retired, TransportRetryPolicy.GroupJoin, ownerDisposed: true)).IsFalse();
        await Assert.That(TransportFailureClassifier.IsRetriable(
            new InvalidOperationException("wrapper", retired),
            TransportRetryPolicy.Request,
            ownerDisposed: true)).IsFalse();
    }

    [Test]
    public async Task RoutingFailures_KeepTheHistoricalTypeAndMessage()
    {
        // Existing catch sites and callers match these as InvalidOperationException.
        InvalidOperationException unknownBroker = new UnknownBrokerException(7);
        InvalidOperationException exhausted = new ConnectionSetupExhaustedException(3);
        var cause = new SocketException((int)SocketError.ConnectionRefused);
        InvalidOperationException refreshFailed = new MetadataRefreshFailedException(cause);

        await Assert.That(unknownBroker.Message).IsEqualTo("Unknown broker ID: 7");
        await Assert.That(exhausted.Message).IsEqualTo("Failed to create connection after 3 retries");
        await Assert.That(refreshFailed.Message).IsEqualTo("Failed to refresh metadata from any broker");
        await Assert.That(refreshFailed.InnerException).IsSameReferenceAs(cause);
        await Assert.That(TransportFailureClassifier.RequiresMetadataRefresh(unknownBroker)).IsTrue();
        await Assert.That(TransportFailureClassifier.RequiresMetadataRefresh(exhausted)).IsFalse();
    }

    [Test]
    [MethodDataSource(nameof(LegacyRequestRows))]
    public async Task LegacyRequestClassification_IgnoresRoutingFailures(
        string kind, Func<Exception> createFailure, bool retryRequest)
    {
        // AdminClient retries are not idempotent, so the count-bounded RetryHelper path keeps
        // its classification: the new routing types and retired connections stay non-retriable
        // there unless they wrap a transport failure, exactly as the untyped exceptions did.
        await Assert.That(RetryHelper.IsRetriableRequestFailure(createFailure())).IsEqualTo(retryRequest)
            .Because(kind);
    }

    public static IEnumerable<(string Kind, Func<Exception> CreateFailure, bool RetryRequest)> LegacyRequestRows()
    {
        yield return ("unknown broker", static () => new UnknownBrokerException(7), false);
        yield return ("connection setup exhausted", static () => new ConnectionSetupExhaustedException(3), false);
        yield return ("metadata refresh failed without cause", static () => new MetadataRefreshFailedException(null), false);
        yield return ("metadata refresh failed with transport cause",
            static () => new MetadataRefreshFailedException(new IOException("transport")), true);
        yield return ("retired connection", static () => new ObjectDisposedException("KafkaConnection"), false);
    }

    private static bool Classify(Exception failure, TransportRetryPolicy policy) =>
        TransportFailureClassifier.IsRetriable(failure, policy, ownerDisposed: false);
}
