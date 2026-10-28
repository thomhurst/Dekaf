namespace Dekaf.Protocol.Messages;

/// <summary>
/// Full Streams processing-graph description (KIP-1331), as carried by
/// StreamsGroupDescribe v1+ responses.
/// </summary>
public sealed class StreamsTopologyDescriptionData
{
    internal const int MaxSubtopologyCount = 1_000_000;
    internal const int MaxNodeCount = 1_000_000;
    internal const int MaxGlobalStoreCount = 100_000;
    internal const int MaxNameCount = 1_000_000;

    public required IReadOnlyList<StreamsTopologyDescriptionSubtopologyData> Subtopologies { get; init; }
    public required IReadOnlyList<StreamsTopologyDescriptionGlobalStoreData> GlobalStores { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactArray(
            Subtopologies,
            static (ref KafkaProtocolWriter w, StreamsTopologyDescriptionSubtopologyData subtopology) => subtopology.Write(ref w));
        writer.WriteCompactArray(
            GlobalStores,
            static (ref KafkaProtocolWriter w, StreamsTopologyDescriptionGlobalStoreData store) => store.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }

    public static void WriteNullable(ref KafkaProtocolWriter writer, StreamsTopologyDescriptionData? description)
    {
        if (description is null)
        {
            writer.WriteInt8(-1);
            return;
        }

        writer.WriteInt8(1);
        description.Write(ref writer);
    }

    public static StreamsTopologyDescriptionData Read(ref KafkaProtocolReader reader)
    {
        var subtopologies = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsTopologyDescriptionSubtopologyData.Read(ref r),
            minElementSize: 3,
            maxCount: MaxSubtopologyCount);
        var globalStores = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsTopologyDescriptionGlobalStoreData.Read(ref r),
            minElementSize: 15,
            maxCount: MaxGlobalStoreCount);

        reader.SkipTaggedFields();

        return new StreamsTopologyDescriptionData
        {
            Subtopologies = subtopologies,
            GlobalStores = globalStores
        };
    }

    public static StreamsTopologyDescriptionData? ReadNullable(ref KafkaProtocolReader reader)
    {
        return reader.ReadInt8() < 0 ? null : Read(ref reader);
    }
}

/// <summary>
/// Subtopology in a Streams topology description.
/// </summary>
public sealed class StreamsTopologyDescriptionSubtopologyData
{
    public required string SubtopologyId { get; init; }
    public required IReadOnlyList<StreamsTopologyDescriptionNodeData> Nodes { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(SubtopologyId);
        writer.WriteCompactArray(
            Nodes,
            static (ref KafkaProtocolWriter w, StreamsTopologyDescriptionNodeData node) => node.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsTopologyDescriptionSubtopologyData Read(ref KafkaProtocolReader reader)
    {
        var subtopologyId = reader.ReadCompactString() ?? string.Empty;
        var nodes = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => StreamsTopologyDescriptionNodeData.Read(ref r),
            minElementSize: 7,
            maxCount: StreamsTopologyDescriptionData.MaxNodeCount);

        reader.SkipTaggedFields();

        return new StreamsTopologyDescriptionSubtopologyData
        {
            SubtopologyId = subtopologyId,
            Nodes = nodes
        };
    }
}

/// <summary>
/// Processing node in a Streams topology description.
/// </summary>
public sealed class StreamsTopologyDescriptionNodeData
{
    public required string Name { get; init; }

    /// <summary>
    /// The node type: 1 = source, 2 = processor, 3 = sink.
    /// </summary>
    public sbyte NodeType { get; init; }

    public required IReadOnlyList<string> SourceTopics { get; init; }
    public string? SinkTopic { get; init; }
    public required IReadOnlyList<string> Stores { get; init; }
    public required IReadOnlyList<string> Successors { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(Name);
        writer.WriteInt8(NodeType);
        WriteNames(ref writer, SourceTopics);
        writer.WriteCompactNullableString(SinkTopic);
        WriteNames(ref writer, Stores);
        WriteNames(ref writer, Successors);
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsTopologyDescriptionNodeData Read(ref KafkaProtocolReader reader)
    {
        var name = reader.ReadCompactString() ?? string.Empty;
        var nodeType = reader.ReadInt8();
        var sourceTopics = ReadNames(ref reader);
        var sinkTopic = reader.ReadCompactString();
        var stores = ReadNames(ref reader);
        var successors = ReadNames(ref reader);

        reader.SkipTaggedFields();

        return new StreamsTopologyDescriptionNodeData
        {
            Name = name,
            NodeType = nodeType,
            SourceTopics = sourceTopics,
            SinkTopic = sinkTopic,
            Stores = stores,
            Successors = successors
        };
    }

    private static void WriteNames(ref KafkaProtocolWriter writer, IReadOnlyList<string> names) =>
        writer.WriteCompactArray(
            names,
            static (ref KafkaProtocolWriter w, string name) => w.WriteCompactString(name));

    private static IReadOnlyList<string> ReadNames(ref KafkaProtocolReader reader) =>
        reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadCompactString() ?? string.Empty,
            minElementSize: 1,
            maxCount: StreamsTopologyDescriptionData.MaxNameCount);
}

/// <summary>
/// Global state store in a Streams topology description.
/// </summary>
public sealed class StreamsTopologyDescriptionGlobalStoreData
{
    public required StreamsTopologyDescriptionNodeData Source { get; init; }
    public required StreamsTopologyDescriptionNodeData Processor { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        Source.Write(ref writer);
        Processor.Write(ref writer);
        writer.WriteEmptyTaggedFields();
    }

    public static StreamsTopologyDescriptionGlobalStoreData Read(ref KafkaProtocolReader reader)
    {
        var source = StreamsTopologyDescriptionNodeData.Read(ref reader);
        var processor = StreamsTopologyDescriptionNodeData.Read(ref reader);

        reader.SkipTaggedFields();

        return new StreamsTopologyDescriptionGlobalStoreData
        {
            Source = source,
            Processor = processor
        };
    }
}
