namespace Dekaf.Admin;

/// <summary>Optional capability for per-entity client quota mutation outcomes.</summary>
public interface IDetailedClientQuotaMutationAdminClient
{
    /// <summary>Alters quotas and returns an outcome for every requested entity.</summary>
    /// <remarks>
    /// Keys contain every entity component; component order does not affect equality and null names identify defaults.
    /// Validate-only success confirms validation without changing quotas. Confirmed successes are never replayed.
    /// Missing responses and failures after dispatch produce Unknown outcomes. Only explicit controller or quota
    /// rejections are retried automatically. Cancellation before invocation throws; cancellation or timeout after
    /// invocation preserves known outcomes and marks unsent work NotAttempted. Duplicate entities, component types,
    /// or operation keys are rejected before dispatch. Empty input returns an empty result without network activity.
    /// </remarks>
    ValueTask<IReadOnlyDictionary<ClientQuotaEntity, AdminMutationResult>> AlterClientQuotasDetailedAsync(
        IEnumerable<ClientQuotaAlteration> alterations, AlterClientQuotasOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Detailed client quota mutations for clients supporting the optional capability.</summary>
public static class AdminClientDetailedClientQuotaMutationExtensions
{
    /// <inheritdoc cref="IDetailedClientQuotaMutationAdminClient.AlterClientQuotasDetailedAsync" />
    public static ValueTask<IReadOnlyDictionary<ClientQuotaEntity, AdminMutationResult>> AlterClientQuotasDetailedAsync(
        this IAdminClient adminClient, IEnumerable<ClientQuotaAlteration> alterations,
        AlterClientQuotasOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient is IDetailedClientQuotaMutationAdminClient capability
            ? capability.AlterClientQuotasDetailedAsync(alterations, options, cancellationToken)
            : throw new NotSupportedException($"Admin client type '{adminClient.GetType().FullName}' does not support detailed client quota mutations.");
    }
}
