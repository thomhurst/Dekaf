namespace Dekaf.Protocol.Messages;

/// <summary>
/// Broker endpoint information carried by KIP-951 response tagged fields.
/// </summary>
public sealed class NodeEndpoint
{
    public int NodeId { get; init; }

    public required string Host { get; init; }

    public int Port { get; init; }

    public string? Rack { get; init; }

    public static NodeEndpoint Read(ref KafkaProtocolReader reader)
    {
        var nodeId = reader.ReadInt32();
        var host = reader.ReadCompactNonNullableString();
        var port = reader.ReadInt32();
        var rack = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new NodeEndpoint
        {
            NodeId = nodeId,
            Host = host,
            Port = port,
            Rack = rack
        };
    }
}

internal static class LeaderDiscoveryFields
{
    public static LeaderIdAndEpoch ReadLeaderIdAndEpoch(ref KafkaProtocolReader reader, int size)
    {
        EnsureMinSize(size, 8);

        var start = reader.Consumed;
        var leader = new LeaderIdAndEpoch
        {
            LeaderId = reader.ReadInt32(),
            LeaderEpoch = reader.ReadInt32()
        };

        SkipRemaining(ref reader, start, size);
        return leader;
    }

    public static EpochEndOffset ReadEpochEndOffset(ref KafkaProtocolReader reader, int size)
    {
        EnsureMinSize(size, 12);

        var start = reader.Consumed;
        var epochEndOffset = new EpochEndOffset
        {
            Epoch = reader.ReadInt32(),
            EndOffset = reader.ReadInt64()
        };

        SkipRemaining(ref reader, start, size);
        return epochEndOffset;
    }

    public static SnapshotId ReadSnapshotId(ref KafkaProtocolReader reader, int size)
    {
        EnsureMinSize(size, 12);

        var start = reader.Consumed;
        var snapshotId = new SnapshotId
        {
            EndOffset = reader.ReadInt64(),
            Epoch = reader.ReadInt32()
        };

        SkipRemaining(ref reader, start, size);
        return snapshotId;
    }

    public static NodeEndpoint? FindNodeEndpoint(IReadOnlyList<NodeEndpoint> nodeEndpoints, int nodeId)
    {
        for (var i = 0; i < nodeEndpoints.Count; i++)
        {
            if (nodeEndpoints[i].NodeId == nodeId)
                return nodeEndpoints[i];
        }

        return null;
    }

    public static void SkipRemaining(ref KafkaProtocolReader reader, long start, int size)
    {
        var consumed = reader.Consumed - start;
        if (consumed < size)
        {
            reader.Skip((int)(size - consumed));
            return;
        }

        if (consumed > size)
        {
            throw new MalformedProtocolDataException(
                $"Tagged field parser consumed {consumed} bytes but field size is {size}");
        }
    }

    private static void EnsureMinSize(int size, int minSize)
    {
        if (size < minSize)
        {
            throw new MalformedProtocolDataException(
                $"Tagged field size {size} is smaller than required {minSize} bytes");
        }
    }
}
