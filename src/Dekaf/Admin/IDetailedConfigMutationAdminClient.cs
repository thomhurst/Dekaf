namespace Dekaf.Admin;

/// <summary>Optional capability for per-resource configuration mutation outcomes.</summary>
/// <remarks>
/// Results retain every requested resource, its original broker error, and confirmed successes across retries.
/// Requests are grouped by their required broker/controller endpoint. Only explicit controller or quota
/// rejections are retried; ambiguous sends and missing responses return Unknown. TimeoutMs bounds all endpoint
/// batches and retries together. Cancellation before invocation throws; cancellation during execution retains
/// known outcomes and marks unsent resources NotAttempted. Validate-only success does not change configuration.
/// </remarks>
public interface IDetailedConfigMutationAdminClient
{
    /// <summary>Replaces configuration and reports each resource's outcome.</summary>
    ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> AlterConfigsDetailedAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>> configs, AlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Applies incremental configuration operations and reports each resource's outcome.</summary>
    ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> IncrementalAlterConfigsDetailedAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigAlter>> configs, IncrementalAlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Detailed configuration mutations for clients supporting the optional capability.</summary>
public static class AdminClientDetailedConfigMutationExtensions
{
    /// <inheritdoc cref="IDetailedConfigMutationAdminClient.AlterConfigsDetailedAsync" />
    public static ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> AlterConfigsDetailedAsync(
        this IAdminClient adminClient, IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>> configs,
        AlterConfigsOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).AlterConfigsDetailedAsync(configs, options, cancellationToken);

    /// <inheritdoc cref="IDetailedConfigMutationAdminClient.IncrementalAlterConfigsDetailedAsync" />
    public static ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> IncrementalAlterConfigsDetailedAsync(
        this IAdminClient adminClient, IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigAlter>> configs,
        IncrementalAlterConfigsOptions? options = null, CancellationToken cancellationToken = default) =>
        GetCapability(adminClient).IncrementalAlterConfigsDetailedAsync(configs, options, cancellationToken);

    private static IDetailedConfigMutationAdminClient GetCapability(IAdminClient adminClient)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient as IDetailedConfigMutationAdminClient
            ?? throw new NotSupportedException($"Admin client type '{adminClient.GetType().FullName}' does not support detailed configuration mutations.");
    }
}
