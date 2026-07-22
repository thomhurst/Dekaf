namespace Dekaf.Outbox.EntityFrameworkCore;

/// <summary>
/// Controls where the outbox entities land in the database: schema and table names.
/// For anything beyond naming (column names, extra indexes, provider-specific column
/// types), configure the entities after calling
/// <see cref="OutboxModelBuilderExtensions.UseDekafOutbox(Microsoft.EntityFrameworkCore.ModelBuilder, OutboxModelOptions)"/> -
/// later fluent configuration wins over earlier configuration in EF Core.
/// </summary>
public sealed class OutboxModelOptions
{
    /// <summary>Default name of the pending-messages table.</summary>
    public const string DefaultMessagesTableName = "dekaf_outbox_messages";

    /// <summary>Default name of the bucket-leases table.</summary>
    public const string DefaultLeasesTableName = "dekaf_outbox_leases";

    /// <summary>Default name of the relay-heartbeats table.</summary>
    public const string DefaultRelaysTableName = "dekaf_outbox_relays";

    /// <summary>
    /// Database schema for all three outbox tables, or null for the provider default.
    /// </summary>
    public string? Schema { get; init; }

    /// <summary>
    /// Name of the pending-messages table.
    /// </summary>
    public string MessagesTableName { get; init; } = DefaultMessagesTableName;

    /// <summary>
    /// Name of the bucket-leases table.
    /// </summary>
    public string LeasesTableName { get; init; } = DefaultLeasesTableName;

    /// <summary>
    /// Name of the relay-heartbeats table.
    /// </summary>
    public string RelaysTableName { get; init; } = DefaultRelaysTableName;

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrEmpty(MessagesTableName);
        ArgumentException.ThrowIfNullOrEmpty(LeasesTableName);
        ArgumentException.ThrowIfNullOrEmpty(RelaysTableName);
    }
}
