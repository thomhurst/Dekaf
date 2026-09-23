using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

public sealed partial class AdminClient
{
    /// <inheritdoc cref="INodeFeatureAdminClient.DescribeFeaturesAsync"/>
    async ValueTask<FeatureMetadata> INodeFeatureAdminClient.DescribeFeaturesAsync(
        DescribeFeaturesOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.NodeId is { } requestedNodeId)
            ArgumentOutOfRangeException.ThrowIfNegative(requestedNodeId, nameof(options));
        var timeoutMs = options.TimeoutMs ?? _options.RequestTimeoutMs;
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs, nameof(options));
        cancellationToken.ThrowIfCancellationRequested();
        if (timeoutMs == 0)
            throw new TimeoutException("DescribeFeatures timed out before node discovery.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMs);
        var operationToken = timeout.Token;
        try
        {
            if (options.NodeId is not { } nodeId)
                return await DescribeFeaturesCoreAsync(Timeout.Infinite, operationToken).ConfigureAwait(false);

            return await WithRetryAsync(async attemptToken =>
            {
                attemptToken.ThrowIfCancellationRequested();
                // Inside the retried operation: controller discovery on a fresh client can be
                // refused transiently, and that is retried like the request itself.
                await EnsureInitializedAsync(attemptToken, nameof(DescribeFeaturesAsync)).ConfigureAwait(false);
                using var lease = await LeaseFeatureNodeAsync(nodeId, attemptToken).ConfigureAwait(false);
                var request = CreateFeatureRequest(lease.Connection, nodeId, out var apiVersion);
                var response = await lease.Connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                    request, apiVersion, attemptToken).ConfigureAwait(false);
                return MapFeatureResponse(response);
            }, operationToken, Timeout.Infinite, nameof(DescribeFeaturesAsync)).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            var message = options.NodeId is { } nodeId
                ? $"DescribeFeatures timed out for node {nodeId} after {timeoutMs} ms."
                : $"DescribeFeatures timed out (default) after {timeoutMs} ms.";
            throw new TimeoutException(message, ex);
        }
        catch (OperationCanceledException ex) when (
            cancellationToken.IsCancellationRequested && ex.CancellationToken != cancellationToken)
        {
            throw CallerCancellation(ex, cancellationToken);
        }
    }

    private ValueTask<KafkaConnectionLease> LeaseFeatureNodeAsync(int nodeId, CancellationToken cancellationToken)
    {
        if (_controllerMetadataManager is { } controllers)
            return controllers.LeaseControllerAsync(nodeId, ApiKey.ApiVersions, cancellationToken);

        if (_metadataManager.Metadata.GetBroker(nodeId) is null)
            throw new KafkaException(ErrorCode.BrokerNotAvailable,
                $"Node {nodeId} is not a known broker in the configured broker bootstrap endpoints.");

        return _connectionPool.LeaseConnectionAsync(nodeId, cancellationToken);
    }
}
