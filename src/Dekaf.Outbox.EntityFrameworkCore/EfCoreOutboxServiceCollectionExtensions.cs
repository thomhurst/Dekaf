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
    /// Registers <see cref="EfCoreOutboxStore{TContext}"/> as the <see cref="IOutboxStore"/>.
    /// Requires an <c>AddDbContextFactory&lt;TContext&gt;</c> registration, and the context's
    /// model must call <see cref="OutboxModelBuilderExtensions.UseDekafOutbox"/>.
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
