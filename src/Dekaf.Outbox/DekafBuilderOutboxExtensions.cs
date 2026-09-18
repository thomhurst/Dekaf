using System.Diagnostics.CodeAnalysis;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Producer;

namespace Dekaf.Outbox;

/// <summary>
/// Outbox relay registration for <see cref="DekafBuilder"/>, so the outbox is configured
/// inside <c>AddDekaf</c> next to the application's producers and consumers.
/// </summary>
/// <remarks>
/// Each method registers exactly what its <see cref="OutboxServiceCollectionExtensions"/>
/// counterpart registers. The store comes from a store package, for example
/// <c>AddEntityFrameworkCoreOutboxStore</c> or <c>AddDynamoDbOutboxStore</c>.
/// </remarks>
public static class DekafBuilderOutboxExtensions
{
    /// <summary>
    /// Registers the outbox relay hosted service with a dedicated Dekaf producer. See
    /// <see cref="OutboxServiceCollectionExtensions.AddDekafOutboxRelay(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{ProducerBuilder{byte[], byte[]}}, OutboxRelayOptions)"/>.
    /// </summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="configureProducer">Configures the relay's producer; at minimum set bootstrap
    /// servers. <see cref="Acks.All"/>, idempotence and a key-respecting partitioner are enforced
    /// after this delegate runs.</param>
    /// <param name="options">Relay options; defaults are production-reasonable.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static DekafBuilder AddOutboxRelay(
        this DekafBuilder builder,
        Action<ProducerBuilder<byte[]?, byte[]?>> configureProducer,
        OutboxRelayOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddDekafOutboxRelay(configureProducer, options);
        return builder;
    }

    /// <summary>
    /// Registers the outbox relay hosted service using an already-registered
    /// <see cref="IOutboxPublisher"/> (advanced: custom publisher or shared producer).
    /// </summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="options">Relay options; defaults are production-reasonable.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static DekafBuilder AddOutboxRelay(this DekafBuilder builder, OutboxRelayOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddDekafOutboxRelay(options);
        return builder;
    }

    /// <summary>
    /// Enables optional cross-process post-commit hints using an application-supplied
    /// broadcast transport. See
    /// <see cref="OutboxServiceCollectionExtensions.AddDekafOutboxNotificationTransport{TTransport}"/>.
    /// </summary>
    /// <typeparam name="TTransport">The application's transport implementation.</typeparam>
    /// <param name="builder">The Dekaf builder.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static DekafBuilder AddOutboxNotificationTransport<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTransport>(this DekafBuilder builder)
        where TTransport : class, IOutboxNotificationTransport
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddDekafOutboxNotificationTransport<TTransport>();
        return builder;
    }
}
