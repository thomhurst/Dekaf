namespace Dekaf.Admin;

/// <summary>
/// Optional admin capability for removing stale KRaft controller registrations (KIP-1312).
/// </summary>
public interface IControllerRegistrationAdminClient
{
    /// <summary>
    /// Unregisters a controller from KRaft cluster metadata. This removes only the controller
    /// registration; it does not remove the controller from the Raft voter set. Use
    /// <see cref="IAdminClient.RemoveRaftVoterAsync"/> to change voters.
    /// </summary>
    /// <param name="controllerId">The ID of the controller registration to remove.</param>
    /// <param name="options">Optional settings, including the total operation timeout.</param>
    /// <param name="cancellationToken">Cancels waiting. A request already sent may still apply.</param>
    /// <exception cref="Errors.BrokerVersionException">The destination does not support UnregisterController (Kafka 4.4+).</exception>
    /// <exception cref="Errors.KafkaException">
    /// The controller rejected the request, for example with
    /// <see cref="Protocol.ErrorCode.ControllerIdNotRegistered"/> for an unknown ID or
    /// <see cref="Protocol.ErrorCode.UnsupportedVersion"/> when the finalized metadata version
    /// is below 4.4-IV2.
    /// </exception>
    ValueTask UnregisterControllerAsync(
        int controllerId,
        UnregisterControllerOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Options for unregistering a KRaft controller.</summary>
public sealed class UnregisterControllerOptions
{
    internal const int DefaultTimeoutMs = 30_000;

    /// <summary>
    /// Total operation timeout in milliseconds, including controller discovery and retries.
    /// Zero is an already-expired deadline.
    /// </summary>
    public int TimeoutMs { get; init; } = DefaultTimeoutMs;
}

/// <summary>Controller registration operations for <see cref="IAdminClient"/>.</summary>
public static class AdminClientControllerRegistrationExtensions
{
    /// <summary>
    /// Unregisters a controller from KRaft cluster metadata without changing Raft voters.
    /// </summary>
    /// <exception cref="NotSupportedException">The client does not implement <see cref="IControllerRegistrationAdminClient"/>.</exception>
    public static ValueTask UnregisterControllerAsync(
        this IAdminClient adminClient,
        int controllerId,
        UnregisterControllerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        return adminClient is IControllerRegistrationAdminClient registrationClient
            ? registrationClient.UnregisterControllerAsync(controllerId, options, cancellationToken)
            : throw new NotSupportedException(
                $"Admin client type '{adminClient.GetType().FullName}' does not support controller unregistration.");
    }
}
