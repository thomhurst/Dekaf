using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dekaf.Outbox.EntityFrameworkCore;

/// <summary>
/// Dependency injection registration for the Entity Framework Core outbox store.
/// </summary>
public static class EfCoreOutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the context factory and outbox store with immediate post-commit wake-ups.
    /// Register <c>AddDekafOutboxRelay</c> in the same container to provide the notifier.
    /// The context model must include <c>UseDekafOutbox</c>.
    /// </summary>
    public static IServiceCollection AddDekafEntityFrameworkCoreOutboxStore<TContext>(
        this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> configureContext)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureContext);
        services.AddDbContextFactory<TContext>((provider, builder) =>
        {
            configureContext(provider, builder);
            builder.UseDekafOutboxNotifications(provider.GetRequiredService<IOutboxNotifier>());
        });
        return services.AddDekafEntityFrameworkCoreOutboxStore<TContext>();
    }

    /// <summary>
    /// Registers <see cref="EfCoreOutboxStore{TContext}"/> as the <see cref="IOutboxStore"/>.
    /// Requires an <c>AddDbContextFactory&lt;TContext&gt;</c> registration, and the context's
    /// model must call <see cref="OutboxModelBuilderExtensions.UseDekafOutbox(ModelBuilder, string)"/>.
    /// </summary>
    /// <typeparam name="TContext">The application's context type containing the outbox model.</typeparam>
    public static IServiceCollection AddDekafEntityFrameworkCoreOutboxStore<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOutboxStore, EfCoreOutboxStore<TContext>>();

        return services;
    }
}
