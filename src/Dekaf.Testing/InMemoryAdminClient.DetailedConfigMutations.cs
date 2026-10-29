using Dekaf.Admin;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IDetailedConfigMutationAdminClient
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> AlterConfigsDetailedAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>> configs, AlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = AdminClient.SnapshotDetailedConfigs(configs, static entry => ConfigAlter.Set(entry.Name, entry.Value));
        return ExecuteInMemoryConfigMutationsAsync(items, options?.TimeoutMs ?? 30000, replace: true, options?.ValidateOnly ?? false, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> IncrementalAlterConfigsDetailedAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigAlter>> configs, IncrementalAlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = AdminClient.SnapshotDetailedConfigs(configs, static change => new ConfigAlter
            { Name = change.Name, Value = change.Value, Operation = change.Operation });
        return ExecuteInMemoryConfigMutationsAsync(items, options?.TimeoutMs ?? 30000, replace: false, options?.ValidateOnly ?? false, cancellationToken);
    }

    private ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> ExecuteInMemoryConfigMutationsAsync(
        List<AdminClient.ConfigMutation> items, int timeoutMs, bool replace, bool validateOnly, CancellationToken token) =>
        ExecuteInMemoryMutationAsync(items, static item => item.Resource, static _ => (null, null),
            item => replace
                ? _cluster.AlterShareIsolation(item.Resource, item.Changes, replace: true, validateOnly)
                : _cluster.IncrementallyAlterConfigs(item.Resource, item.Changes, validateOnly),
            timeoutMs, token, applyFault: (item, cancellation) => ApplyAdminResourceFaultAsync(item.Resource, cancellation));
}
