using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    internal ErrorCode IncrementallyAlterConfigs(ConfigResource resource, IReadOnlyList<ConfigAlter> changes, bool validateOnly)
    {
        if (resource.Type != ConfigResourceType.Topic)
            return AlterShareIsolation(resource, changes, replace: false, validateOnly);

        lock (_gate)
        {
            if (!_topics.TryGetValue(resource.Name, out var topic))
                return ErrorCode.UnknownTopicOrPartition;

            // No list types or broker defaults are modeled. Reject unsupported operations before
            // applying any changes to this resource, including during validation-only requests.
            foreach (var change in changes)
            {
                if (change.Operation != ConfigAlterOperation.Delete
                    && (change.Operation != ConfigAlterOperation.Set || change.Value is null))
                    return ErrorCode.InvalidConfig;
            }

            if (validateOnly)
                return ErrorCode.None;

            foreach (var change in changes)
            {
                if (change.Operation == ConfigAlterOperation.Delete)
                    topic.Configs.Remove(change.Name);
                else
                    topic.Configs[change.Name] = change.Value!;
            }
            return ErrorCode.None;
        }
    }
}
