using Microsoft.EntityFrameworkCore;

namespace Dekaf.Outbox.EntityFrameworkCore;

/// <summary>
/// Configures transaction-aware local outbox notifications.
/// </summary>
public static class OutboxNotificationOptionsExtensions
{
    /// <summary>
    /// Wakes the local relay after successful outbox commits. Explicit transactions must
    /// commit through EF Core; ambient transactions require a provider that supports enlistment.
    /// External transaction owners and other processes still rely on fallback polling.
    /// </summary>
    public static DbContextOptionsBuilder UseDekafOutboxNotifications(
        this DbContextOptionsBuilder optionsBuilder, IOutboxNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(notifier);
        var observer = new OutboxCommitObserver(notifier);
        return optionsBuilder.AddInterceptors(observer.SaveChanges, observer.Transactions);
    }

    /// <summary>
    /// Configures transaction-aware notifications while retaining the typed options builder.
    /// </summary>
    public static DbContextOptionsBuilder<TContext> UseDekafOutboxNotifications<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder, IOutboxNotifier notifier)
        where TContext : DbContext
    {
        UseDekafOutboxNotifications((DbContextOptionsBuilder)optionsBuilder, notifier);
        return optionsBuilder;
    }
}
