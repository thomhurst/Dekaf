using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IDetailedClientQuotaMutationAdminClient
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<ClientQuotaEntity, AdminMutationResult>> AlterClientQuotasDetailedAsync(
        IEnumerable<ClientQuotaAlteration> alterations, AlterClientQuotasOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var opts = options ?? new AlterClientQuotasOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        var items = SnapshotDetailedClientQuotas(alterations);
        return ExecuteDetailedMutationAsync<ClientQuotaEntity, ClientQuotaAlteration, AlterClientQuotasRequest, AlterClientQuotasResponse>(
            items, static item => item.Entity,
            new(ApiKey.AlterClientQuotas, AlterClientQuotasRequest.LowestSupportedVersion,
                AlterClientQuotasRequest.HighestSupportedVersion, nameof(AlterClientQuotasAsync), BrokerOrController: true),
            opts.TimeoutMs,
            (pending, _) => new() { Entries = pending.Select(BuildAlterClientQuotaEntry).ToArray(), ValidateOnly = opts.ValidateOnly },
            static (_, response) => MapMutationResults(response.Entries, static entry => MapClientQuotaEntity(entry.Entity),
                static entry => entry.ErrorCode, static entry => entry.ErrorMessage), cancellationToken);
    }

    internal static List<ClientQuotaAlteration> SnapshotDetailedClientQuotas(IEnumerable<ClientQuotaAlteration> alterations)
    {
        ArgumentNullException.ThrowIfNull(alterations);
        var items = new List<ClientQuotaAlteration>();
        var entities = new HashSet<ClientQuotaEntity>();
        foreach (var alteration in alterations)
        {
            ArgumentNullException.ThrowIfNull(alteration);
            alteration.Validate();
            var components = new ClientQuotaEntityComponent[alteration.Entity.Components.Count];
            var types = new HashSet<ClientQuotaEntityType>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = alteration.Entity.Components[i];
                if (!types.Add(component.EntityType))
                    throw new ArgumentException("A quota entity cannot repeat a component type.", nameof(alterations));
                components[i] = new() { EntityType = component.EntityType, Name = component.Name };
            }
            var entity = new ClientQuotaEntity { Components = System.Array.AsReadOnly(components) };
            if (!entities.Add(entity))
                throw new ArgumentException("Quota alterations cannot repeat an entity.", nameof(alterations));
            var operations = new ClientQuotaOperation[alteration.Operations.Count];
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < operations.Length; i++)
            {
                var operation = alteration.Operations[i];
                if (!keys.Add(operation.Key))
                    throw new ArgumentException("A quota alteration cannot repeat an operation key.", nameof(alterations));
                operations[i] = new() { Key = operation.Key, Value = operation.Value, Remove = operation.Remove };
            }
            items.Add(new() { Entity = entity, Operations = System.Array.AsReadOnly(operations) });
        }
        return items;
    }
}
