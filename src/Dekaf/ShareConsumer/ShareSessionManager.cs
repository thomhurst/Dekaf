using System.Collections.Concurrent;

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
    }
}
