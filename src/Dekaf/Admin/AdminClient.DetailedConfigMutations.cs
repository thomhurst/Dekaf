using System.Globalization;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Admin;

public sealed partial class AdminClient : IDetailedConfigMutationAdminClient
{
    internal sealed record ConfigMutation(ConfigResource Resource, IReadOnlyList<ConfigAlter> Changes);

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> AlterConfigsDetailedAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>> configs, AlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var opts = options ?? new AlterConfigsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        var items = SnapshotDetailedConfigs(configs, static entry => ConfigAlter.Set(entry.Name, entry.Value));
        return ExecuteConfigMutationsAsync<AlterConfigsRequest, AlterConfigsResponse>(items,
            new(ApiKey.AlterConfigs, AlterConfigsRequest.LowestSupportedVersion, AlterConfigsRequest.HighestSupportedVersion, nameof(AlterConfigsAsync)),
            opts.TimeoutMs,
            (pending, _, _) => new()
            {
                ValidateOnly = opts.ValidateOnly,
                Resources = pending.Select(static item => new AlterConfigsResource
                {
                    ResourceType = (sbyte)item.Resource.Type, ResourceName = item.Resource.Name,
                    Configs = item.Changes.Select(static change => new AlterableConfig { Name = change.Name, Value = change.Value }).ToArray()
                }).ToArray()
            },
            static (_, response) => MapMutationResults(response.Responses,
                static resource => new ConfigResource { Type = (ConfigResourceType)resource.ResourceType, Name = resource.ResourceName },
                static resource => resource.ErrorCode, static resource => resource.ErrorMessage), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> IncrementalAlterConfigsDetailedAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigAlter>> configs, IncrementalAlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var opts = options ?? new IncrementalAlterConfigsOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(opts.TimeoutMs);
        var items = SnapshotDetailedConfigs(configs, static change => new ConfigAlter
            { Name = change.Name, Value = change.Value, Operation = change.Operation });
        return ExecuteConfigMutationsAsync<IncrementalAlterConfigsRequest, IncrementalAlterConfigsResponse>(items,
            new(ApiKey.IncrementalAlterConfigs, IncrementalAlterConfigsRequest.LowestSupportedVersion,
                IncrementalAlterConfigsRequest.HighestSupportedVersion, nameof(IncrementalAlterConfigsAsync)),
            opts.TimeoutMs,
            (pending, _, _) => new()
            {
                ValidateOnly = opts.ValidateOnly,
                Resources = pending.Select(static item => new IncrementalAlterConfigsResource
                {
                    ResourceType = (sbyte)item.Resource.Type, ResourceName = item.Resource.Name,
                    Configs = item.Changes.Select(static change => new IncrementalAlterableConfig
                        { Name = change.Name, Value = change.Value, ConfigOperation = (sbyte)change.Operation }).ToArray()
                }).ToArray()
            },
            static (_, response) => MapMutationResults(response.Responses,
                static resource => new ConfigResource { Type = (ConfigResourceType)resource.ResourceType, Name = resource.ResourceName },
                static resource => resource.ErrorCode, static resource => resource.ErrorMessage), cancellationToken);
    }

    private async ValueTask<IReadOnlyDictionary<ConfigResource, AdminMutationResult>> ExecuteConfigMutationsAsync<TRequest, TResponse>(
        List<ConfigMutation> items, MutationProtocol protocol, int timeoutMs,
        Func<List<ConfigMutation>, short, Dictionary<ConfigResource, AdminMutationResult>, TRequest> createRequest,
        Func<List<ConfigMutation>, TResponse, Dictionary<ConfigResource, AdminMutationResult>> readResponse,
        CancellationToken cancellationToken)
        where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse
    {
        var results = new Dictionary<ConfigResource, AdminMutationResult>(items.Count);
        if (items.Count == 0) return results;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutMs == 0) deadline.Cancel();
        else deadline.CancelAfter(timeoutMs);
        var groups = new Dictionary<int, List<ConfigMutation>>();
        foreach (var item in items)
        {
            var resource = item.Resource;
            var targetsPhysicalNode = resource.Type == ConfigResourceType.BrokerLogger ||
                (_controllerMetadataManager is null && resource.Type == ConfigResourceType.Broker && resource.Name.Length != 0);
            var endpoint = targetsPhysicalNode ? int.Parse(resource.Name, CultureInfo.InvariantCulture) : -1;
            if (!groups.TryGetValue(endpoint, out var group)) groups.Add(endpoint, group = new());
            group.Add(item);
        }
        foreach (var group in groups)
        {
            // One total budget includes earlier endpoint batches and their retries.
            var outcomes = await ExecuteDetailedMutationAsync<ConfigResource, ConfigMutation, TRequest, TResponse>(
                group.Value, static item => item.Resource, protocol, timeoutMs, createRequest, readResponse, cancellationToken,
                token => LeaseDetailedConfigEndpointAsync(group.Key, protocol.ApiKey, token), deadline).ConfigureAwait(false);
            foreach (var outcome in outcomes) results.Add(outcome.Key, outcome.Value);
        }
        return results;
    }

    private ValueTask<KafkaConnectionLease> LeaseDetailedConfigEndpointAsync(int endpoint, ApiKey apiKey, CancellationToken token)
    {
        if (_controllerMetadataManager is { } controller)
            return endpoint < 0 ? controller.LeaseActiveControllerAsync(apiKey, token) : controller.LeaseControllerAsync(endpoint, apiKey, token);
        return endpoint < 0 ? LeaseAnyBrokerConnectionAsync(token) : _connectionPool.LeaseConnectionAsync(endpoint, token);
    }

    internal static List<ConfigMutation> SnapshotDetailedConfigs<TEntry>(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<TEntry>> configs, Func<TEntry, ConfigAlter> copy) where TEntry : class
    {
        ArgumentNullException.ThrowIfNull(configs);
        var items = new List<ConfigMutation>(configs.Count);
        var resources = new HashSet<ConfigResource>();
        foreach (var pair in configs)
        {
            var resource = pair.Key;
            ArgumentNullException.ThrowIfNull(resource);
            ArgumentNullException.ThrowIfNull(resource.Name);
            if (resource.Type is not (ConfigResourceType.Topic or ConfigResourceType.Broker or ConfigResourceType.BrokerLogger
                or ConfigResourceType.ClientMetrics or ConfigResourceType.Group))
                throw new ArgumentOutOfRangeException(nameof(configs), "Unknown configuration resource type.");
            if (resource.Type != ConfigResourceType.Broker) ArgumentException.ThrowIfNullOrWhiteSpace(resource.Name, nameof(configs));
            if (resource.Type == ConfigResourceType.BrokerLogger || (resource.Type == ConfigResourceType.Broker && resource.Name.Length != 0))
            {
                if (!int.TryParse(resource.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id < 0)
                    throw new ArgumentException("Broker and logger resource names must be nonnegative node IDs.", nameof(configs));
            }
            if (!resources.Add(resource)) throw new ArgumentException("Configuration mutations cannot repeat a resource.", nameof(configs));
            ArgumentNullException.ThrowIfNull(pair.Value);
            var names = new HashSet<string>(StringComparer.Ordinal);
            var changes = new ConfigAlter[pair.Value.Count];
            for (var i = 0; i < changes.Length; i++)
            {
                var entry = pair.Value[i];
                ArgumentNullException.ThrowIfNull(entry);
                var change = copy(entry);
                ArgumentException.ThrowIfNullOrWhiteSpace(change.Name, nameof(configs));
                if ((byte)change.Operation > (byte)ConfigAlterOperation.Subtract)
                    throw new ArgumentOutOfRangeException(nameof(configs), "Unknown incremental configuration operation.");
                if (!names.Add(change.Name)) throw new ArgumentException("A resource cannot repeat a configuration key.", nameof(configs));
                changes[i] = change;
            }
            items.Add(new(new() { Type = resource.Type, Name = resource.Name }, System.Array.AsReadOnly(changes)));
        }
        return items;
    }
}
