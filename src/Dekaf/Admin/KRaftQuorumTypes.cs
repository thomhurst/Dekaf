namespace Dekaf.Admin;

/// <summary>
/// KRaft metadata quorum description.
/// </summary>
public sealed class MetadataQuorumDescription
{
    public int LeaderId { get; init; }
    public int LeaderEpoch { get; init; }
    public long HighWatermark { get; init; }
    public required IReadOnlyList<QuorumReplicaState> CurrentVoters { get; init; }
    public required IReadOnlyList<QuorumReplicaState> Observers { get; init; }
    public IReadOnlyList<QuorumNode> Nodes { get; init; } = [];
}

/// <summary>
/// State of a KRaft quorum voter or observer.
/// </summary>
public sealed class QuorumReplicaState
{
    public int ReplicaId { get; init; }
    public Guid? ReplicaDirectoryId { get; init; }
    public long LogEndOffset { get; init; }
    public long LastFetchTimestamp { get; init; } = -1;
    public long LastCaughtUpTimestamp { get; init; } = -1;
}

/// <summary>
/// KRaft quorum node and its controller listeners.
/// </summary>
public sealed class QuorumNode
{
    public int NodeId { get; init; }
    public required IReadOnlyList<RaftVoterEndpoint> Listeners { get; init; }
}

/// <summary>
/// Endpoint used to communicate with a KRaft voter.
/// </summary>
public sealed class RaftVoterEndpoint
{
    public required string Name { get; init; }
    public required string Host { get; init; }
    public int Port { get; init; }
}

/// <summary>
/// Options for adding a KRaft voter.
/// </summary>
public sealed class AddRaftVoterOptions
{
    public string? ClusterId { get; init; }
    public int TimeoutMs { get; init; } = 30000;
    public bool AckWhenCommitted { get; init; } = true;
}

/// <summary>
/// Options for removing a KRaft voter.
/// </summary>
public sealed class RemoveRaftVoterOptions
{
    public string? ClusterId { get; init; }
}
