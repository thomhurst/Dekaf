using Dekaf.Serialization;
using Dekaf.Streams.Internal;

namespace Dekaf.Streams;

/// <summary>Declares a stream-processing topology.</summary>
public sealed class StreamsBuilder
{
    private readonly List<GraphNode> _nodes = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly HashSet<string> _sourceTopics = new(StringComparer.Ordinal);
    private Func<ProcessingError, ErrorResponse>? _errorHandler;
    private bool _built;

    /// <summary>Creates a builder.</summary>
    /// <param name="applicationId">
    /// Identifies the application. It is the streams group id and the prefix of every internal topic.
    /// </param>
    public StreamsBuilder(string applicationId)
    {
        ValidateName(applicationId, "application id");
        ApplicationId = applicationId;
    }

    /// <summary>Gets the application id.</summary>
    public string ApplicationId { get; }

    /// <summary>Reads a topic as a stream of independent records.</summary>
    public IStream<TKey, TValue> Stream<TKey, TValue>(string topic, ISerde<TKey> keySerde, ISerde<TValue> valueSerde)
    {
        ArgumentNullException.ThrowIfNull(keySerde);
        ArgumentNullException.ThrowIfNull(valueSerde);
        RegisterSourceTopic(topic);
        var node = Add(new SourceNode<TKey, TValue>(AutoName("source"), topic, keySerde, valueSerde, isRepartition: false));
        return new StreamImpl<TKey, TValue>(this, node, keySerde, valueSerde);
    }

    /// <summary>
    /// Reads a topic as a table: the latest value per key, kept in the store <paramref name="storeName"/>.
    /// A record with a null value deletes its key.
    /// </summary>
    public ITable<TKey, TValue> Table<TKey, TValue>(
        string topic,
        string storeName,
        ISerde<TKey> keySerde,
        ISerde<TValue> valueSerde)
    {
        ArgumentNullException.ThrowIfNull(keySerde);
        ArgumentNullException.ThrowIfNull(valueSerde);
        RegisterSourceTopic(topic);
        RegisterName(storeName);
        var node = Add(new TableSourceNode<TKey, TValue>(AutoName("table-source"), topic, storeName, keySerde, valueSerde));
        return new TableImpl<TKey, TValue>(this, node, storeName, keySerde, valueSerde);
    }

    /// <summary>
    /// Sets the handler for records that fail to deserialize or process. Without one, a failure stops the task.
    /// </summary>
    /// <remarks>
    /// When an operator fails after earlier operators already forwarded output for the same record, that
    /// output is not withdrawn.
    /// </remarks>
    public StreamsBuilder OnError(Func<ProcessingError, ErrorResponse> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _errorHandler = handler;
        return this;
    }

    /// <summary>Validates the declared graph and compiles it. The builder cannot be changed afterwards.</summary>
    public Topology Build()
    {
        if (_nodes.Count == 0)
        {
            throw new TopologyException("The topology has no sources.");
        }

        _built = true;
        return new Topology(ApplicationId, _nodes, _errorHandler);
    }

    internal TNode Add<TNode>(TNode node) where TNode : GraphNode
    {
        if (_built)
        {
            throw new TopologyException("The topology is already built.");
        }

        node.Id = _nodes.Count;
        _nodes.Add(node);
        return node;
    }

    internal string AutoName(string kind) => $"{kind}-{_nodes.Count}";

    /// <summary>Reserves a user-supplied operator or store name.</summary>
    internal string RegisterName(string name)
    {
        ValidateName(name, "name");
        if (!_names.Add(name))
        {
            throw new TopologyException($"The name '{name}' is used more than once. Operator and store names must be unique.");
        }

        return name;
    }

    private void RegisterSourceTopic(string topic)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        if (!_sourceTopics.Add(topic))
        {
            throw new TopologyException($"Topic '{topic}' is already read by this topology. Read it once and reuse the stream or table.");
        }
    }

    // Names become part of topic names, so they follow Kafka's topic character set.
    private static void ValidateName(string name, string what)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        foreach (var c in name)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-'))
            {
                throw new TopologyException($"The {what} '{name}' may only contain ASCII letters, digits, '.', '_' and '-'.");
            }
        }
    }
}
