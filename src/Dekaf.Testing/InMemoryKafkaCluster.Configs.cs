using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Testing;

public sealed partial class InMemoryKafkaCluster
{
    internal IReadOnlyList<ConfigEntry> DescribeConfigs(ConfigResource resource)
    {
        if (resource.Type != ConfigResourceType.Topic)
            return DescribeShareIsolation(resource);

        lock (_gate)
        {
            if (!_topics.TryGetValue(resource.Name, out var topic))
                throw new KafkaException(ErrorCode.UnknownTopicOrPartition, $"Topic '{resource.Name}' does not exist.");

            var entries = new ConfigEntry[topic.Configs.Count];
            var index = 0;
            foreach (var config in topic.Configs)
            {
                entries[index++] = new ConfigEntry
                {
                    Name = config.Key,
                    Value = config.Value,
                    Source = ConfigSource.DynamicTopicConfig
                };
            }
            return entries;
        }
    }

    internal ErrorCode ReplaceConfigs(ConfigResource resource, IReadOnlyList<ConfigAlter> changes, bool validateOnly)
    {
        if (resource.Type != ConfigResourceType.Topic)
            return AlterShareIsolation(resource, changes, replace: true, validateOnly);

        lock (_gate)
        {
            if (!_topics.TryGetValue(resource.Name, out var topic))
                return ErrorCode.UnknownTopicOrPartition;

            if (validateOnly)
                return ErrorCode.None;

            // Replacement removes omitted overrides; null values also reset an override.
            topic.Configs.Clear();
            foreach (var change in changes)
            {
                if (change.Value is { } value)
                    topic.Configs[change.Name] = value;
            }
            return ErrorCode.None;
        }
    }
}
