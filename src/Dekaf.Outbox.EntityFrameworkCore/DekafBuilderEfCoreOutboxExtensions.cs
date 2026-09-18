using Dekaf.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Dekaf.Outbox.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core outbox store registration for <see cref="DekafBuilder"/>. Each method
/// registers exactly what its <see cref="EfCoreOutboxServiceCollectionExtensions"/> counterpart
/// registers.
/// </summary>
public static class DekafBuilderEfCoreOutboxExtensions
{
    /// <summary>
    /// Registers the context factory and outbox store with immediate post-commit wake-ups.
    /// Add <c>AddOutboxRelay</c> to the same builder to provide the notifier. The context
    /// model must include <c>UseDekafOutbox</c>.
    /// </summary>
    /// <typeparam name="TContext">The application's context type containing the outbox model.</typeparam>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="configureContext">Configures the EF Core provider of the context.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static DekafBuilder AddEntityFrameworkCoreOutboxStore<TContext>(
        this DekafBuilder builder, Action<IServiceProvider, DbContextOptionsBuilder> configureContext)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddDekafEntityFrameworkCoreOutboxStore<TContext>(configureContext);
        return builder;
    }

    /// <summary>
    /// Registers <see cref="EfCoreOutboxStore{TContext}"/> as the <see cref="IOutboxStore"/>.
    /// Requires an <c>AddDbContextFactory&lt;TContext&gt;</c> registration, and the context's
    /// model must call <see cref="OutboxModelBuilderExtensions.UseDekafOutbox(ModelBuilder, string)"/>.
    /// </summary>
    /// <typeparam name="TContext">The application's context type containing the outbox model.</typeparam>
    /// <param name="builder">The Dekaf builder.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static DekafBuilder AddEntityFrameworkCoreOutboxStore<TContext>(this DekafBuilder builder)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddDekafEntityFrameworkCoreOutboxStore<TContext>();
        return builder;
    }
}
