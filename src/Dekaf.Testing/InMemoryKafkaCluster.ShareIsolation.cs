using Dekaf.Admin;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    private const string ShareIsolationConfig = "share.isolation.level";
    private readonly Dictionary<string, IsolationLevel> _shareIsolationLevels = new(StringComparer.Ordinal);

    // Call only while holding _gate, including acquisition and configuration reads.
    private IsolationLevel GetShareIsolationUnderLock(string groupId) =>
        _shareIsolationLevels.GetValueOrDefault(groupId, IsolationLevel.ReadUncommitted);

    internal IReadOnlyList<ConfigEntry> DescribeShareIsolation(ConfigResource resource)
    {
        if (resource.Type != ConfigResourceType.Group)
            return Array.Empty<ConfigEntry>();

        lock (_gate)
        {
            var isDefault = !_shareIsolationLevels.ContainsKey(resource.Name);
            return [new ConfigEntry
            {
                Name = ShareIsolationConfig,
                Value = GetShareIsolationUnderLock(resource.Name) == IsolationLevel.ReadCommitted
                    ? "read_committed" : "read_uncommitted",
                IsDefault = isDefault,
                Source = isDefault ? ConfigSource.DefaultConfig : ConfigSource.Unknown
            }];
        }
    }

    internal ErrorCode AlterShareIsolation(
        ConfigResource resource, IReadOnlyList<ConfigAlter> changes, bool replace, bool validateOnly)
    {
        if (resource.Type != ConfigResourceType.Group)
            return ErrorCode.None;

        lock (_gate)
        {
            IsolationLevel? isolation = null;
            var changed = replace;
            foreach (var change in changes)
            {
                if (change.Name != ShareIsolationConfig)
                    continue;

                changed = true;
                if (change.Operation == ConfigAlterOperation.Delete)
                {
                    isolation = null;
                }
                else if (change.Operation == ConfigAlterOperation.Set)
                {
                    isolation = change.Value switch
                    {
                        "read_uncommitted" => IsolationLevel.ReadUncommitted,
                        "read_committed" => IsolationLevel.ReadCommitted,
                        _ => null
                    };
                    if (isolation is null)
                        return ErrorCode.InvalidConfig;
                }
                else
                {
                    return ErrorCode.InvalidConfig;
                }
            }

            if (!validateOnly && changed)
            {
                if (isolation is { } value)
                    _shareIsolationLevels[resource.Name] = value;
                else
                    _shareIsolationLevels.Remove(resource.Name);
            }
            return ErrorCode.None;
        }
    }
}
