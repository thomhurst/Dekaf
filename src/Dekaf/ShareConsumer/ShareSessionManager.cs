using System.Collections.Concurrent;
using Dekaf.Protocol.Messages;

namespace Dekaf.ShareConsumer;

/// <summary>
/// Tracks share fetch session state per broker.
/// Share sessions allow incremental fetch requests (only sending changes)
/// after the initial full request, reducing wire overhead.
/// <para>
/// Thread-safety: a poll or commit sends to every broker concurrently, and each per-broker
/// task advances or resets its own broker's epoch after its response arrives. Callers keep at
/// most one request in flight per broker, so per-broker order is theirs to keep; this class
/// only has to stay consistent while different brokers are updated at the same time.
/// </para>
/// </summary>
internal sealed class ShareSessionManager
{
    // One slot per broker, kept across resets: reads and updates of a known broker are lock-free
    // and allocation-free. A slot is only allocated the first time a broker is seen.
    private readonly ConcurrentDictionary<int, EpochSlot> _sessionEpochs = new();

    /// <summary>
    /// Gets the current session epoch for a broker.
    /// Returns 0 (new session) if no session exists for this broker.
    /// </summary>
    internal int GetSessionEpoch(int brokerId)
    {
        return _sessionEpochs.TryGetValue(brokerId, out var slot) ? Volatile.Read(ref slot.Epoch) : 0;
    }

    /// <summary>
    /// Updates the session after a successful fetch by incrementing the epoch.
    /// The broker wraps from int.MaxValue to 1, never through 0 (a new session) or -1 (close).
    /// </summary>
    internal void IncrementEpoch(int brokerId)
    {
        var slot = _sessionEpochs.GetOrAdd(brokerId, static _ => new EpochSlot());
        var epoch = Volatile.Read(ref slot.Epoch);
        Volatile.Write(ref slot.Epoch, epoch == int.MaxValue ? 1 : epoch + 1);
        Volatile.Write(ref slot.Opened, true);
    }

    /// <summary>
    /// Resets a broker's session to epoch 0 (new session).
    /// Called on ShareSessionNotFound or InvalidShareSessionEpoch errors, and after a transport
    /// failure that may have reached the broker, because the broker's epoch may then have moved.
    /// </summary>
    internal void ResetSession(int brokerId)
    {
        if (_sessionEpochs.TryGetValue(brokerId, out var slot))
            Volatile.Write(ref slot.Epoch, 0);
    }

    /// <summary>
    /// Whether a session was ever opened on this broker. With epoch 0, that session has been reset.
    /// </summary>
    internal bool HadSession(int brokerId)
        => _sessionEpochs.TryGetValue(brokerId, out var slot) && Volatile.Read(ref slot.Opened);

    /// <summary>
    /// Gets the partitions of a broker's current share session, each keyed to the topic ID it was
    /// added with. A request captures the set before it reads the epoch and updates that set after
    /// its reply: <see cref="ResetAll"/> replaces every session, so a reply to a request sent before
    /// the reset updates the old, unreachable set and never the new session's. Only that broker's
    /// requests read or change the set, and callers keep at most one request in flight per broker.
    /// </summary>
    internal Dictionary<TopicPartition, Guid> GetSessionPartitions(int brokerId)
        => _sessionEpochs.GetOrAdd(brokerId, static _ => new EpochSlot()).Partitions;

    /// <summary>
    /// Records the partitions a successful ShareFetch left in the broker's session. A request that
    /// opens a session holds exactly the partitions it sent; an incremental request removes the
    /// partitions it forgot, then adds the partitions it sent.
    /// </summary>
    internal static void UpdateSessionPartitions(
        Dictionary<TopicPartition, Guid> sessionPartitions,
        bool newSession,
        Dictionary<string, (Guid TopicId, List<ShareFetchRequestPartition> Partitions)> sentTopics,
        List<TopicPartition>? forgottenPartitions)
    {
        if (newSession)
        {
            sessionPartitions.Clear();
        }
        else if (forgottenPartitions is not null)
        {
            foreach (var partition in forgottenPartitions)
                sessionPartitions.Remove(partition);
        }

        foreach (var (topic, (topicId, sentPartitions)) in sentTopics)
        {
            foreach (var partition in sentPartitions)
                sessionPartitions[new TopicPartition(topic, partition.PartitionIndex)] = topicId;
        }
    }

    /// <summary>
    /// Returns the close epoch (-1) for session teardown.
    /// </summary>
    internal const int CloseEpoch = -1;

    /// <summary>
    /// Resets all sessions. Called during coordinator transitions.
    /// </summary>
    internal void ResetAll()
    {
        _sessionEpochs.Clear();
    }

    private sealed class EpochSlot
    {
        public int Epoch;
        // Set by the first epoch advance. A slot can exist earlier, when a request reads its partitions.
        public bool Opened;
        public readonly Dictionary<TopicPartition, Guid> Partitions = [];
    }
}
