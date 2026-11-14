using System.Text;
using Dekaf.Admin;
using Dekaf.Streams.Internal;

namespace Dekaf.Streams;

/// <summary>A compiled, immutable topology.</summary>
public sealed class Topology
{
    // Kept beyond the window so a restoring task still finds records of windows that have not closed.
    private const long WindowChangelogAdditionalRetentionMs = 24 * 60 * 60 * 1000;

    private readonly List<GraphNode> _nodes;
    private readonly List<List<string>> _copartitionGroups = [];
    private readonly Dictionary<string, int> _subtopologyBySourceTopic = new(StringComparer.Ordinal);
    private readonly HashSet<string> _repartitionTopics = new(StringComparer.Ordinal);

    internal Topology(string applicationId, List<GraphNode> nodes, Func<ProcessingError, ErrorResponse>? errorHandler)
    {
        ApplicationId = applicationId;
        ErrorHandler = errorHandler;
        _nodes = nodes;

        var nodesBySubtopology = AssignSubtopologies(nodes);
        NodesBySubtopology = nodesBySubtopology;

        var descriptions = new SubtopologyDescription[nodesBySubtopology.Count];
        for (var i = 0; i < descriptions.Length; i++)
        {
            descriptions[i] = DescribeSubtopology(i, nodesBySubtopology[i]);
            foreach (var node in nodesBySubtopology[i])
            {
                if (node.SourceTopic is { } topic)
                {
                    _subtopologyBySourceTopic.Add(topic, i);
                    if (node.IsRepartition)
                    {
                        _repartitionTopics.Add(topic);
                    }
                }
            }
        }

        Subtopologies = descriptions;
        CollectCopartitionGroups();
    }

    /// <summary>Gets the application id.</summary>
    public string ApplicationId { get; }

    /// <summary>Gets the subtopologies in id order. Each one runs as one task per input partition.</summary>
    public IReadOnlyList<SubtopologyDescription> Subtopologies { get; }

    internal Func<ProcessingError, ErrorResponse>? ErrorHandler { get; }

    internal IReadOnlyList<IReadOnlyList<GraphNode>> NodesBySubtopology { get; }

    /// <summary>Whether <paramref name="topic"/> is an internal repartition topic of this topology.</summary>
    internal bool IsRepartitionTopic(string topic) => _repartitionTopics.Contains(topic);

    /// <summary>Returns the index of the subtopology that reads <paramref name="topic"/>, or -1.</summary>
    internal int SubtopologyOfSourceTopic(string topic) => _subtopologyBySourceTopic.GetValueOrDefault(topic, -1);

    /// <summary>
    /// Returns the topology in the form a streams group member sends to the coordinator when it joins,
    /// including the processing-graph description.
    /// </summary>
    public StreamsGroupTopology ToStreamsGroupTopology()
    {
        var subtopologies = new StreamsGroupSubtopology[Subtopologies.Count];
        var described = new StreamsGroupTopologyDescriptionSubtopology[Subtopologies.Count];
        for (var i = 0; i < subtopologies.Length; i++)
        {
            var description = Subtopologies[i];
            var nodes = NodesBySubtopology[i];

            var changelogs = new List<StreamsGroupTopicInfo>();
            var graph = new List<StreamsGroupTopologyNode>(nodes.Count);
            foreach (var node in nodes)
            {
                foreach (var store in node.Stores)
                {
                    if (store.ChangelogTopic is { } changelog)
                    {
                        changelogs.Add(ChangelogTopic(changelog, store.RetentionMs));
                    }
                }

                graph.Add(new StreamsGroupTopologyNode
                {
                    Name = node.Name,
                    NodeType = node.Kind switch
                    {
                        NodeKind.Source => StreamsGroupTopologyNodeType.Source,
                        NodeKind.Sink => StreamsGroupTopologyNodeType.Sink,
                        _ => StreamsGroupTopologyNodeType.Processor
                    },
                    SourceTopics = node.SourceTopic is { } source ? [source] : [],
                    SinkTopic = node.SinkTopic,
                    Stores = node.Stores.ConvertAll(static store => store.Name),
                    Successors = node.Children.ConvertAll(static child => child.Name)
                });
            }

            subtopologies[i] = new StreamsGroupSubtopology
            {
                SubtopologyId = description.Id,
                SourceTopics = description.SourceTopics,
                SourceTopicRegex = [],
                StateChangelogTopics = changelogs,
                RepartitionSinkTopics = description.RepartitionSinkTopics,
                RepartitionSourceTopics = description.RepartitionSourceTopics.Select(RepartitionTopic).ToArray(),
                CopartitionGroups = CopartitionGroupsOf(description)
            };
            described[i] = new StreamsGroupTopologyDescriptionSubtopology { SubtopologyId = description.Id, Nodes = graph };
        }

        return new StreamsGroupTopology
        {
            Epoch = 0,
            Subtopologies = subtopologies,
            Description = new StreamsGroupTopologyDescription { Subtopologies = described, GlobalStores = [] }
        };
    }

    /// <summary>Returns a readable listing of subtopologies, nodes, topics and stores.</summary>
    public string Describe()
    {
        var text = new StringBuilder();
        for (var i = 0; i < Subtopologies.Count; i++)
        {
            text.Append("Subtopology ").AppendLine(Subtopologies[i].Id);
            foreach (var node in NodesBySubtopology[i])
            {
                text.Append("  ").Append(node.Kind).Append(' ').Append(node.Name);
                if ((node.SourceTopic ?? node.SinkTopic) is { } topic)
                {
                    text.Append(" (topic: ").Append(topic).Append(')');
                }

                if (node.Stores.Count > 0)
                {
                    text.Append(" (stores: ").AppendJoin(", ", node.Stores.Select(static store => store.Name)).Append(')');
                }

                if (node.Children.Count > 0)
                {
                    text.Append(" --> ").AppendJoin(", ", node.Children.Select(static child => child.Name));
                }

                text.AppendLine();
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Groups nodes into subtopologies: nodes connected by an edge or a store dependency run together.
    /// A repartition topic is not an edge, so it separates two subtopologies.
    /// </summary>
    private static List<List<GraphNode>> AssignSubtopologies(List<GraphNode> nodes)
    {
        var root = new int[nodes.Count];
        for (var i = 0; i < root.Length; i++)
        {
            root[i] = i;
        }

        int Find(int id)
        {
            while (root[id] != id)
            {
                id = root[id] = root[root[id]];
            }

            return id;
        }

        // The smaller id wins, so a subtopology is identified by its first declared node.
        void Union(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a != b)
            {
                root[Math.Max(a, b)] = Math.Min(a, b);
            }
        }

        foreach (var node in nodes)
        {
            foreach (var parent in node.Parents)
            {
                Union(node.Id, parent.Id);
            }

            foreach (var dependency in node.Dependencies)
            {
                Union(node.Id, dependency.Id);
            }
        }

        var result = new List<List<GraphNode>>();
        var indexByRoot = new Dictionary<int, int>();
        foreach (var node in nodes)
        {
            var nodeRoot = Find(node.Id);
            if (!indexByRoot.TryGetValue(nodeRoot, out var index))
            {
                index = result.Count;
                indexByRoot.Add(nodeRoot, index);
                result.Add([]);
            }

            node.Subtopology = index;
            result[index].Add(node);
        }

        return result;
    }

    private static SubtopologyDescription DescribeSubtopology(int index, List<GraphNode> nodes)
    {
        var sourceTopics = new List<string>();
        var repartitionSources = new List<string>();
        var repartitionSinks = new List<string>();
        var sinkTopics = new List<string>();
        var changelogs = new List<string>();
        var stores = new List<string>();
        foreach (var node in nodes)
        {
            if (node.SourceTopic is { } source)
            {
                (node.IsRepartition ? repartitionSources : sourceTopics).Add(source);
            }

            if (node.SinkTopic is { } sink)
            {
                (node.IsRepartition ? repartitionSinks : sinkTopics).Add(sink);
            }

            foreach (var store in node.Stores)
            {
                stores.Add(store.Name);
                if (store.ChangelogTopic is { } changelog)
                {
                    changelogs.Add(changelog);
                }
            }
        }

        return new SubtopologyDescription
        {
            Id = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SourceTopics = sourceTopics,
            RepartitionSourceTopics = repartitionSources,
            RepartitionSinkTopics = repartitionSinks,
            SinkTopics = sinkTopics,
            ChangelogTopics = changelogs,
            StateStores = stores
        };
    }

    /// <summary>A join needs its stream and its table to have the same partition count.</summary>
    private void CollectCopartitionGroups()
    {
        foreach (var node in _nodes)
        {
            if (node.Dependencies.Count == 0)
            {
                continue;
            }

            var topics = new List<string>();
            AddSourceTopics(node, topics);
            foreach (var dependency in node.Dependencies)
            {
                AddSourceTopics(dependency, topics);
            }

            _copartitionGroups.Add(topics);
        }
    }

    private static void AddSourceTopics(GraphNode node, List<string> topics)
    {
        if (node.SourceTopic is { } topic)
        {
            if (!topics.Contains(topic))
            {
                topics.Add(topic);
            }

            return;
        }

        foreach (var parent in node.Parents)
        {
            AddSourceTopics(parent, topics);
        }
    }

    private StreamsGroupCopartitionGroup[] CopartitionGroupsOf(SubtopologyDescription description)
    {
        var groups = new List<StreamsGroupCopartitionGroup>();
        foreach (var topics in _copartitionGroups)
        {
            var sources = new List<short>();
            var repartitionSources = new List<short>();
            foreach (var topic in topics)
            {
                var index = IndexOf(description.SourceTopics, topic);
                if (index >= 0)
                {
                    sources.Add((short)index);
                }
                else if ((index = IndexOf(description.RepartitionSourceTopics, topic)) >= 0)
                {
                    repartitionSources.Add((short)index);
                }
            }

            if (sources.Count + repartitionSources.Count > 1)
            {
                groups.Add(new StreamsGroupCopartitionGroup
                {
                    SourceTopics = sources,
                    SourceTopicRegex = [],
                    RepartitionSourceTopics = repartitionSources
                });
            }
        }

        return [.. groups];
    }

    private static int IndexOf(IReadOnlyList<string> topics, string topic)
    {
        for (var i = 0; i < topics.Count; i++)
        {
            if (topics[i] == topic)
            {
                return i;
            }
        }

        return -1;
    }

    // Partition count and replication factor stay unset: the coordinator derives the count from the
    // source topics and uses the broker's default replication factor.
    private static StreamsGroupTopicInfo ChangelogTopic(string name, long windowRetentionMs) => new()
    {
        Name = name,
        TopicConfigs = windowRetentionMs == 0
            ? [Config("cleanup.policy", "compact")]
            :
            [
                Config("cleanup.policy", "compact,delete"),
                Config("retention.ms", (windowRetentionMs + WindowChangelogAdditionalRetentionMs)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture))
            ]
    };

    // Repartition records are deleted once consumed, never by age.
    private static StreamsGroupTopicInfo RepartitionTopic(string name) => new()
    {
        Name = name,
        TopicConfigs = [Config("cleanup.policy", "delete"), Config("retention.ms", "-1")]
    };

    private static StreamsGroupKeyValue Config(string key, string value) => new() { Key = key, Value = value };
}

/// <summary>The topics and stores of one subtopology.</summary>
public sealed class SubtopologyDescription
{
    /// <summary>Gets the subtopology id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the topics the subtopology reads, excluding internal repartition topics.</summary>
    public required IReadOnlyList<string> SourceTopics { get; init; }

    /// <summary>Gets the internal repartition topics the subtopology reads.</summary>
    public required IReadOnlyList<string> RepartitionSourceTopics { get; init; }

    /// <summary>Gets the internal repartition topics the subtopology writes.</summary>
    public required IReadOnlyList<string> RepartitionSinkTopics { get; init; }

    /// <summary>Gets the topics the subtopology writes, excluding internal topics.</summary>
    public required IReadOnlyList<string> SinkTopics { get; init; }

    /// <summary>Gets the internal changelog topics of the subtopology's stores.</summary>
    public required IReadOnlyList<string> ChangelogTopics { get; init; }

    /// <summary>Gets the names of the subtopology's state stores.</summary>
    public required IReadOnlyList<string> StateStores { get; init; }
}
