namespace Dekaf.Protocol.Messages;

/// <summary>
/// AlterReplicaLogDirs request (API key 34).
/// Moves replicas to the requested log directories on a broker.
/// </summary>
public sealed class AlterReplicaLogDirsRequest : IKafkaRequest<AlterReplicaLogDirsResponse>
{
    public static ApiKey ApiKey => ApiKey.AlterReplicaLogDirs;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The directory assignments to apply.
    /// </summary>
    public required IReadOnlyList<AlterReplicaLogDirsRequestDir> Dirs { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 2;

    public static short GetRequestHeaderVersion(short version) => version >= 2 ? (short)2 : (short)1;

    public static short GetResponseHeaderVersion(short version) => version >= 2 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (version >= 2)
        {
            writer.WriteCompactArray(
                Dirs,
                static (ref KafkaProtocolWriter w, AlterReplicaLogDirsRequestDir d, short v) => d.Write(ref w, v),
                version);
            writer.WriteEmptyTaggedFields();
            return;
        }

        writer.WriteArray(
            Dirs,
            static (ref KafkaProtocolWriter w, AlterReplicaLogDirsRequestDir d, short v) => d.Write(ref w, v),
            version);
    }
}

/// <summary>
/// Target log directory and replicas for AlterReplicaLogDirs.
/// </summary>
public sealed class AlterReplicaLogDirsRequestDir
{
    /// <summary>
    /// Absolute log directory path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Topics to add to this directory.
    /// </summary>
    public required IReadOnlyList<AlterReplicaLogDirsRequestTopic> Topics { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (version >= 2)
        {
            writer.WriteCompactString(Path);
            writer.WriteCompactArray(
                Topics,
                static (ref KafkaProtocolWriter w, AlterReplicaLogDirsRequestTopic t, short v) => t.Write(ref w, v),
                version);
            writer.WriteEmptyTaggedFields();
            return;
        }

        writer.WriteString(Path);
        writer.WriteArray(
            Topics,
            static (ref KafkaProtocolWriter w, AlterReplicaLogDirsRequestTopic t, short v) => t.Write(ref w, v),
            version);
    }
}

/// <summary>
/// Topic in an AlterReplicaLogDirs request.
/// </summary>
public sealed class AlterReplicaLogDirsRequestTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (version >= 2)
        {
            writer.WriteCompactString(Name);
            writer.WriteCompactArray(
                Partitions,
                static (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
            writer.WriteEmptyTaggedFields();
            return;
        }

        writer.WriteString(Name);
        writer.WriteArray(
            Partitions,
            static (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
    }
}
