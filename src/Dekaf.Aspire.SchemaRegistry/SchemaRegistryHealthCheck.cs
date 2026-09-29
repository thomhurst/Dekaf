using Dekaf.SchemaRegistry;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dekaf.Aspire;

/// <summary>Checks Schema Registry connectivity and authentication by listing subjects.</summary>
/// <remarks>
/// Listing subjects succeeds for an empty registry and never registers or modifies schemas:
/// https://docs.confluent.io/platform/current/schema-registry/develop/api.html#get--subjects
/// </remarks>
internal sealed class SchemaRegistryHealthCheck(ISchemaRegistryClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await client.GetAllSubjectsAsync(cancellationToken).ConfigureAwait(false);
        return HealthCheckResult.Healthy();
    }
}
