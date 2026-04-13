using System.Collections.Concurrent;

namespace Dekaf.ShareConsumer;

/// <summary>
/// Tracks share fetch session state per broker.
/// Share sessions allow incremental fetch requests (only sending changes)
/// after the initial full request, reducing wire overhead.
/// </summary>
internal sealed class ShareSessionManager
{
    private readonly ConcurrentDictionary<int, int> _sessionEpochs = new();

    /// <summary>
    /// Gets the current session epoch for a broker.
    /// Returns 0 (new session) if no session exists for this broker.
    /// </summary>
    internal int GetSessionEpoch(int brokerId)
    {
        return _sessionEpochs.GetValueOrDefault(brokerId, 0);
    }

    /// <summary>
    /// Updates the session after a successful fetch by incrementing the epoch.
    /// </summary>
    internal void IncrementEpoch(int brokerId)
    {
        _sessionEpochs.AddOrUpdate(brokerId, 1, static (_, current) => current + 1);
    }

    /// <summary>
    /// Resets a broker's session to epoch 0 (new session).
    /// Called on ShareSessionNotFound or InvalidShareSessionEpoch errors.
    /// </summary>
    internal void ResetSession(int brokerId)
    {
        _sessionEpochs.TryRemove(brokerId, out _);
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
}
