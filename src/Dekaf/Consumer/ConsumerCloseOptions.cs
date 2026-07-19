namespace Dekaf.Consumer;

/// <summary>
/// Specifies how closing a consumer affects its consumer-group membership.
/// </summary>
public enum ConsumerGroupMembershipOperation
{
    /// <summary>
    /// Uses Kafka's default close behavior: dynamic members leave the group, while static
    /// members retain their membership so they can rejoin without a rebalance.
    /// </summary>
    Default,

    /// <summary>
    /// Permanently leaves the consumer group. For a static member, this releases its static
    /// membership and triggers a rebalance instead of keeping its assignment warm.
    /// </summary>
    LeaveGroup,

    /// <summary>
    /// Closes without sending a terminal group heartbeat. The broker retains the member until
    /// its session expires.
    /// </summary>
    RemainInGroup
}

/// <summary>
/// Options controlling consumer shutdown behavior.
/// </summary>
public sealed class ConsumerCloseOptions
{
    /// <summary>
    /// Gets how closing affects consumer-group membership.
    /// </summary>
    public ConsumerGroupMembershipOperation GroupMembershipOperation { get; init; } =
        ConsumerGroupMembershipOperation.Default;
}
