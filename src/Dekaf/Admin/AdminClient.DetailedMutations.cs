using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Retry;

namespace Dekaf.Admin;

public sealed partial class AdminClient
{
    private readonly record struct MutationProtocol(ApiKey ApiKey, short MinimumVersion, short MaximumVersion, string Operation);

    private async ValueTask<IReadOnlyDictionary<TKey, AdminMutationResult>> ExecuteControllerMutationAsync<TKey, TItem, TRequest, TResponse>(
        List<TItem> items, Func<TItem, TKey> getKey, MutationProtocol protocol, int timeoutMs,
        Func<List<TItem>, short, TRequest> createRequest,
        Func<List<TItem>, TResponse, Dictionary<TKey, AdminMutationResult>> readResponse,
        CancellationToken cancellationToken)
        where TKey : notnull
        where TRequest : IKafkaRequest<TResponse>
        where TResponse : IKafkaResponse
    {
        var results = new Dictionary<TKey, AdminMutationResult>(items.Count);
        if (items.Count == 0) return results;

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutMs == 0) deadline.Cancel();
        else deadline.CancelAfter(timeoutMs);
        var token = deadline.Token;
        var pending = items;
        try
        {
            await WithRetryAsync(async () =>
            {
                token.ThrowIfCancellationRequested();
                await EnsureInitializedAsync(token, protocol.Operation).ConfigureAwait(false);
                KafkaConnectionLease acquiredLease;
                try
                {
                    acquiredLease = await LeaseDetailedControllerAsync(protocol.ApiKey, token).ConfigureAwait(false);
                }
                catch (InvalidOperationException exception)
                {
                    // Metadata can expose a controller before the pool registers its ID.
                    // Lease failures have not dispatched this mutation; retain prior outcomes.
                    var failure = MutationFailure(exception, deadline, timeoutMs, protocol.Operation, cancellationToken);
                    AddNotAttemptedMutations(pending, getKey, results, failure);
                    return;
                }
                using var lease = acquiredLease;
                var version = _metadataManager.GetNegotiatedApiVersion(
                    lease.Connection, protocol.ApiKey, protocol.MinimumVersion, protocol.MaximumVersion);
                var request = createRequest(pending, version);
                token.ThrowIfCancellationRequested();

                TResponse response;
                try
                {
                    response = await lease.Connection.SendAsync<TRequest, TResponse>(request, version, token).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsDetailedMutationFailure(exception) || exception is InvalidOperationException or MalformedProtocolDataException)
                {
                    // SendAsync does not expose an authoritative "not sent" boundary.
                    // Transport readiness guards also use InvalidOperationException.
                    // Do not replay an ambiguous mutation or reinterpret a later "exists" as success.
                    var failure = MutationFailure(exception, deadline, timeoutMs, protocol.Operation, cancellationToken);
                    foreach (var item in pending)
                        results[getKey(item)] = AdminMutationResult.Unconfirmed(
                            AdminMutationOutcome.Unknown, "The mutation may have applied; no definitive response was received.", failure);
                    return;
                }

                var responseResults = readResponse(pending, response);
                List<TItem>? retry = null;
                KafkaException? retryFailure = null;
                foreach (var item in pending)
                {
                    var key = getKey(item);
                    var result = responseResults.TryGetValue(key, out var received) ? received
                        : AdminMutationResult.Unconfirmed(AdminMutationOutcome.Unknown, "The response omitted this requested entity.");
                    results[key] = result;
                    if (AdminMutationResult.IsSafeControllerRetry(result))
                    {
                        (retry ??= new()).Add(item);
                        retryFailure ??= new KafkaException(result.ErrorCode!.Value, result.ErrorMessage ?? "Controller rejected the mutation.");
                    }
                }
                if (retryFailure is not null)
                {
                    pending = retry!;
                    throw retryFailure;
                }
            }, token).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsDetailedMutationFailure(exception))
        {
            var failure = MutationFailure(exception, deadline, timeoutMs, protocol.Operation, cancellationToken);
            AddNotAttemptedMutations(items, getKey, results, failure);
        }
        return results;
    }

    private static void AddNotAttemptedMutations<TKey, TItem>(List<TItem> items, Func<TItem, TKey> getKey,
        Dictionary<TKey, AdminMutationResult> results, Exception failure) where TKey : notnull
    {
        foreach (var item in items)
        {
            var key = getKey(item);
            // Retain confirmed rejection even if discovery or cancellation prevents its retry.
            if (!results.ContainsKey(key))
                results.Add(key, AdminMutationResult.Unconfirmed(
                    AdminMutationOutcome.NotAttempted, "The operation stopped before this mutation was sent.", failure));
        }
    }

    private ValueTask<KafkaConnectionLease> LeaseDetailedControllerAsync(ApiKey apiKey, CancellationToken cancellationToken)
    {
        if (_controllerMetadataManager is { } controllerMetadataManager)
            return controllerMetadataManager.LeaseActiveControllerAsync(apiKey, cancellationToken);

        // Capture one identity for validation and leasing. Keep the existing convenience
        // method's exception contract separate from detailed per-entity failure handling.
        var controllerId = _metadataManager.Metadata.ControllerId;
        if (controllerId < 0)
            throw new KafkaException(ErrorCode.BrokerNotAvailable, "No controller available.");
        return _connectionPool.LeaseConnectionAsync(controllerId, cancellationToken);
    }

    internal static bool IsDetailedMutationFailure(Exception exception) => exception is
        KafkaException or IOException or System.Net.Sockets.SocketException or TimeoutException or OperationCanceledException or ObjectDisposedException
        || RetryHelper.IsRetriableRequestFailure(exception);

    internal static Exception MutationFailure(Exception exception, CancellationTokenSource deadline,
        int timeoutMs, string operation, CancellationToken callerToken)
    {
        if (deadline.IsCancellationRequested && !callerToken.IsCancellationRequested)
        {
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);
            return new KafkaTimeoutException(TimeoutKind.Api, timeout, timeout, $"{operation} timed out after {timeoutMs} ms.", exception);
        }
        return exception;
    }

    private static Dictionary<TKey, AdminMutationResult> MapMutationResults<TResponse, TKey>(
        IReadOnlyList<TResponse> responses, Func<TResponse, TKey> key,
        Func<TResponse, ErrorCode> code, Func<TResponse, string?> message) where TKey : notnull
    {
        var results = new Dictionary<TKey, AdminMutationResult>(responses.Count);
        foreach (var response in responses)
            AddMutationResult(results, key(response), AdminMutationResult.FromResponse(code(response), message(response)));
        return results;
    }

    private static void AddMutationResult<TKey>(Dictionary<TKey, AdminMutationResult> results,
        TKey key, AdminMutationResult result) where TKey : notnull
    {
        if (!results.TryAdd(key, result))
            results[key] = AdminMutationResult.Unconfirmed(AdminMutationOutcome.Unknown, "The response contained duplicate entity results.");
    }
}
