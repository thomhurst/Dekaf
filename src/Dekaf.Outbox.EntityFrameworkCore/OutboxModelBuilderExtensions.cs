using Microsoft.EntityFrameworkCore;

namespace Dekaf.Outbox.EntityFrameworkCore;

/// <summary>
/// Model configuration for the outbox tables. The resulting schema is documented in the
/// outbox docs page ("Database Schema"); table names and schema are controlled via
/// <see cref="OutboxModelOptions"/>, and any further customization can be layered on by
/// configuring the entities after this call (later fluent configuration wins).
/// </summary>
public static class OutboxModelBuilderExtensions
{
    /// <summary>
    /// Maps the Dekaf outbox entities (<see cref="OutboxMessage"/>, <see cref="OutboxLease"/>,
    /// <see cref="OutboxRelayInstance"/>) with default table names. Call from your context's
    /// <c>OnModelCreating</c>.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="schema">Optional database schema for the outbox tables.</param>
    public static ModelBuilder UseDekafOutbox(this ModelBuilder modelBuilder, string? schema = null)
        => modelBuilder.UseDekafOutbox(new OutboxModelOptions { Schema = schema });

    /// <summary>
    /// Maps the Dekaf outbox entities with full control over schema and table names.
    /// Call from your context's <c>OnModelCreating</c>.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="options">Schema and table naming options.</param>
    public static ModelBuilder UseDekafOutbox(this ModelBuilder modelBuilder, OutboxModelOptions options)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable(options.MessagesTableName, options.Schema);
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).ValueGeneratedOnAdd();
            entity.Property(m => m.MessageId).IsRequired();
            // Kafka topic names are limited to 249 characters.
            entity.Property(m => m.Topic).IsRequired().HasMaxLength(249);
            // The relay reads each bucket's rows oldest-first; this index makes that the
            // primary access path.
            entity.HasIndex(m => new { m.Bucket, m.Id });
            // CreatedAtUtc keeps the provider's native timestamp mapping (readable during
            // incidents); only the lease/heartbeat columns below need server-side
            // comparisons and therefore the ticks conversion.
        });

        modelBuilder.Entity<OutboxLease>(entity =>
        {
            entity.ToTable(options.LeasesTableName, options.Schema);
            entity.HasKey(l => l.Bucket);
            entity.Property(l => l.Bucket).ValueGeneratedNever();
            // Sized to the contract bound that OutboxRelayOptions.Validate enforces, so an
            // oversized relay id fails at startup, never on a store write.
            entity.Property(l => l.Owner).HasMaxLength(OutboxRelayOptions.MaxRelayIdLength);
            // Stored as UTC ticks so expiry comparisons translate to plain integer
            // comparisons on every provider (SQLite has no native DateTimeOffset).
            entity.Property(l => l.ExpiresAtUtc).HasConversion(
                v => v.UtcTicks,
                v => new DateTimeOffset(v, TimeSpan.Zero));
        });

        modelBuilder.Entity<OutboxRelayInstance>(entity =>
        {
            entity.ToTable(options.RelaysTableName, options.Schema);
            entity.HasKey(r => r.RelayId);
            entity.Property(r => r.RelayId).HasMaxLength(OutboxRelayOptions.MaxRelayIdLength);
            entity.Property(r => r.LastSeenUtc).HasConversion(
                v => v.UtcTicks,
                v => new DateTimeOffset(v, TimeSpan.Zero));
        });

        return modelBuilder;
    }
}
