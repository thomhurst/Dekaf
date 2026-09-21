using System.Net.Sockets;
using Dekaf.Errors;
using Dekaf.Networking;

namespace Dekaf.Retry;

/// <summary>
/// How a caller treats a <see cref="KafkaException"/> that none of its typed exclusions matched.
/// </summary>
internal enum KafkaExceptionRetryRule
{
    /// <summary>
    /// Retry only when <see cref="KafkaException.IsRetriable"/> says so. Request and transaction
    /// paths use this: a typed Kafka failure, including a terminal operation deadline, stays final.
    /// </summary>
    ByIsRetriable,

    /// <summary>
    /// Retry every Kafka exception the caller did not exclude. Group join loops use this: a
    /// closed connection and a request timeout reach them as code-less Kafka exceptions, and the
    /// loop is bounded by its own join deadline.
    /// </summary>
    AnyNotExcluded
}

/// <summary>
/// A caller's typed exclusions for <see cref="TransportFailureClassifier"/>. The transport rule
/// itself is shared; callers differ only in which typed failures stay terminal for them.
/// </summary>
internal sealed class TransportRetryPolicy
{
    private readonly Func<Exception, bool> _isTerminal;

    private TransportRetryPolicy(Func<Exception, bool> isTerminal, KafkaExceptionRetryRule kafkaExceptionRule)
    {
        _isTerminal = isTerminal;
        KafkaExceptionRule = kafkaExceptionRule;
    }

    internal KafkaExceptionRetryRule KafkaExceptionRule { get; }

    internal bool IsTerminal(Exception exception) => _isTerminal(exception);

    /// <summary>
    /// A single request retried by <see cref="RetryHelper"/> or a background loop: the outer
    /// Kafka failure decides, nothing else is excluded.
    /// </summary>
    internal static readonly TransportRetryPolicy Request = new(
        static _ => false,
        KafkaExceptionRetryRule.ByIsRetriable);

    /// <summary>
    /// Consumer and share-consumer group join loops. Typed group errors have dedicated handlers
    /// in those loops; broker-version, authentication (including a TLS handshake failure) and
    /// authorization failures are fatal.
    /// </summary>
    internal static readonly TransportRetryPolicy GroupJoin = new(
        static exception => exception is GroupException or BrokerVersionException
            or AuthorizationException or AuthenticationException,
        KafkaExceptionRetryRule.AnyNotExcluded);

    /// <summary>
    /// Producer transaction control plane (coordinator lookup, InitProducerId, AddPartitionsToTxn,
    /// AddOffsetsToTxn, EndTxn). Typed transaction, timeout, broker-version, authentication and
    /// authorization failures propagate.
    /// </summary>
    internal static readonly TransportRetryPolicy ProducerTransaction = new(
        static exception => exception is TransactionException or KafkaTimeoutException
            or BrokerVersionException or AuthenticationException or AuthorizationException,
        KafkaExceptionRetryRule.ByIsRetriable);
}

/// <summary>
/// The one rule for "is this a transport or connection-setup failure worth retrying": a broker
/// that resets or refuses connections, a socket that died mid-request, a DNS miss, a setup
/// timeout, a connection retired by pool churn, or a route the client has not learned yet.
/// Callers supply their typed exclusions and whether they themselves are disposed; they still
/// own their cancellation token and retry budget.
/// </summary>
internal static class TransportFailureClassifier
{
    /// <summary>
    /// Socket-level and connection-setup failures raised by the networking layer.
    /// </summary>
    internal static bool IsSocketLevelFailure(Exception exception) =>
        exception is IOException
            or SocketException
            or TimeoutException
            or DnsResolutionException;

    /// <summary>
    /// Client-side routing failures that clear once metadata catches up or a broker finishes
    /// restarting: an unknown broker ID, connection setup that kept producing closed connections,
    /// and a metadata refresh that failed against every endpoint.
    /// </summary>
    internal static bool IsClientRoutingFailure(Exception exception) =>
        exception is UnknownBrokerException
            or ConnectionSetupExhaustedException
            or MetadataRefreshFailedException;

    /// <summary>
    /// True when a retry should first refresh cluster metadata: the client has no route for the
    /// broker it was told to use, so only newer metadata can fix the next attempt.
    /// </summary>
    internal static bool RequiresMetadataRefresh(Exception exception) =>
        exception is UnknownBrokerException;

    /// <summary>
    /// Classifies <paramref name="exception"/> for <paramref name="policy"/>.
    /// </summary>
    /// <param name="exception">The failure of one attempt.</param>
    /// <param name="policy">The caller's typed exclusions.</param>
    /// <param name="ownerDisposed">
    /// Whether the calling component is itself disposed. An <see cref="ObjectDisposedException"/>
    /// is a connection retired by pool churn between lease and send, and retriable, only while
    /// the owner is alive; after the owner's disposal it is terminal, so a retry loop cannot spin
    /// against a disposed pool.
    /// </param>
    internal static bool IsRetriable(Exception exception, TransportRetryPolicy policy, bool ownerDisposed)
    {
        // Cancellation is never a transport failure, even when it wraps one.
        if (exception is OperationCanceledException)
            return false;

        // Checked before the routing failures below: it also derives from InvalidOperationException.
        if (exception is ObjectDisposedException)
            return !ownerDisposed;

        // The outer typed failure decides; a fatal exception that wraps a transient transport
        // failure stays fatal.
        if (policy.IsTerminal(exception))
            return false;

        if (exception is KafkaException kafkaException)
        {
            return policy.KafkaExceptionRule == KafkaExceptionRetryRule.AnyNotExcluded
                || kafkaException.IsRetriable;
        }

        if (IsSocketLevelFailure(exception) || IsClientRoutingFailure(exception))
            return true;

        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                if (IsRetriable(innerException, policy, ownerDisposed))
                    return true;
            }
        }

        return exception.InnerException is not null
            && IsRetriable(exception.InnerException, policy, ownerDisposed);
    }
}
