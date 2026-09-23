using Dekaf.Errors;

namespace Dekaf.Consumer;

/// <summary>
/// Thrown by an offset fetch that must not rejoin the group itself: the member has left the
/// group, and the caller rejoins once it has released the lock it holds. Never surfaces to the
/// application.
/// </summary>
internal sealed class GroupRejoinRequiredException(string? groupId)
    : KafkaException($"The consumer must rejoin group '{groupId}' before fetching committed offsets.");
