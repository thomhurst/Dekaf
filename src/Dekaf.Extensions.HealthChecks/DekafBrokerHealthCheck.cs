using Dekaf.Admin;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekaf.Extensions.HealthChecks;

/// <summary>
/// Health check that verifies broker connectivity by describing the cluster via the admin client.
/// Reports <see cref="HealthStatus.Healthy"/> when the cluster is reachable and has brokers,
/// and <see cref="HealthStatus.Unhealthy"/> when the cluster cannot be reached or has no brokers.
/// </summary>
public sealed class DekafBrokerHealthCheck : IHealthCheck
{
    private readonly IAdminClient _adminClient;
    private readonly DekafBrokerHealthCheckOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DekafBrokerHealthCheck"/> class.
    /// </summary>
    /// <param name="adminClient">The admin client to use for connectivity checks.</param>
    /// <param name="options">The health check options.</param>
    public DekafBrokerHealthCheck(IAdminClient adminClient, DekafBrokerHealthCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        ArgumentNullException.ThrowIfNull(options);
        _adminClient = adminClient;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.Timeout);

            var cluster = await _adminClient.DescribeClusterAsync(timeoutCts.Token).ConfigureAwait(false);

            var data = new Dictionary<string, object>
            {
                ["ClusterId"] = cluster.ClusterId ?? "unknown",
                ["ControllerId"] = cluster.ControllerId,
                ["BrokerCount"] = cluster.Nodes.Count
            };

            if (cluster.Nodes.Count == 0)
            {
                return HealthCheckResult.Unhealthy(
                    "Cluster is reachable but reports no brokers.",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"Cluster is reachable with {cluster.Nodes.Count} broker(s).",
                data: data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                $"Broker connectivity check timed out after {_options.Timeout.TotalSeconds}s.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Failed to connect to Kafka brokers.",
                exception: ex);
        }
    }
}
