using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Testing;

public sealed partial class InMemoryAdminClient : IDetailedClientQuotaMutationAdminClient
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<ClientQuotaEntity, AdminMutationResult>> AlterClientQuotasDetailedAsync(
        IEnumerable<ClientQuotaAlteration> alterations, AlterClientQuotasOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = AdminClient.SnapshotDetailedClientQuotas(alterations);
        return ExecuteInMemoryMutationAsync(items, static item => item.Entity, static _ => (null, null),
            item =>
            {
                if (options?.ValidateOnly != true)
                {
                    lock (_clientQuotas)
                        ApplyClientQuotaAlteration(item);
                }
                return ErrorCode.None;
            }, options?.TimeoutMs ?? 30000, cancellationToken);
    }
}
