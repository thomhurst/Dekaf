using Dekaf.Producer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Dekaf.Outbox;

/// <summary>
/// Dependency injection registration for the outbox relay.
/// </summary>
public static class OutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the outbox relay hosted service with a dedicated Dekaf producer.
    /// An <see cref="IOutboxStore"/> must be registered separately
    /// (e.g. <c>AddDekafEntityFrameworkCoreOutboxStore</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureProducer">Configures the relay's producer; at minimum set bootstrap
    /// servers. <see cref="Acks.All"/> and idempotence are enforced after this delegate runs -
    /// the at-least-once and ordering guarantees depend on them, so they cannot be downgraded
    /// here. Register a custom <see cref="IOutboxPublisher"/> to opt out deliberately.</param>
    /// <param name="options">Relay options; defaults are production-reasonable.</param>
    public static IServiceCollection AddDekafOutboxRelay(
        this IServiceCollection services,
        Action<ProducerBuilder<byte[]?, byte[]?>> configureProducer,
        OutboxRelayOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureProducer);

        services.TryAddSingleton<IOutboxPublisher>(provider => new DekafOutboxPublisher(
            CreateRelayProducerBuilder(configureProducer, provider.GetService<ILoggerFactory>()).Build()));

        return services.AddDekafOutboxRelayCore(options);
    }

    /// <summary>
    /// Builds a relay producer configuration with the outbox delivery guarantees enforced
    /// on top of the caller's configuration: <see cref="Acks.All"/>, idempotence, and a
    /// key-respecting partitioner. Public so manually wired relays (e.g. multiple logical
    /// outboxes in one host) get exactly the same enforcement as
    /// <see cref="AddDekafOutboxRelay(IServiceCollection, Action{ProducerBuilder{byte[], byte[]}}, OutboxRelayOptions)"/>.
    /// </summary>
    public static ProducerBuilder<byte[]?, byte[]?> CreateRelayProducerBuilder(
        Action<ProducerBuilder<byte[]?, byte[]?>> configureProducer,
        ILoggerFactory? loggerFactory)
    {
        var builder = Kafka.CreateProducer<byte[]?, byte[]?>()
            .WithClientId("dekaf-outbox-relay");
        if (loggerFactory is not null)
            builder.WithLoggerFactory(loggerFactory);
        configureProducer(builder);
        // Applied after the caller's delegate so they cannot be downgraded: prefix
        // accounting is only truthful when every counted ack is durable (Acks.All) and
        // per-partition sequencing holds (idempotence), and per-key ordering survives only
        // when equal keys map to one partition. Murmur2RandomPartitioner rather than the
        // stock Default: Default sticky-rotates zero-length keys across partitions, but the
        // outbox treats an empty serialized key as a real key with an ordering requirement.
        // Placement for non-empty keys is identical (both are Kafka's Murmur2); null keys
        // spread randomly and carry no ordering requirement. A custom partitioner set here
        // takes precedence over anything the delegate configured.
        return builder
            .WithAcks(Acks.All)
            .WithIdempotence(true)
            .WithCustomPartitioner(new Murmur2RandomPartitioner());
    }

    /// <summary>
    /// Registers the outbox relay hosted service using an already-registered
    /// <see cref="IOutboxPublisher"/> (advanced: custom publisher or shared producer).
    /// </summary>
    public static IServiceCollection AddDekafOutboxRelay(
        this IServiceCollection services,
        OutboxRelayOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddDekafOutboxRelayCore(options);
    }

    private static IServiceCollection AddDekafOutboxRelayCore(
        this IServiceCollection services,
        OutboxRelayOptions? options)
    {
        // The OutboxRelayService constructor is the single validation gate for the options.
        services.TryAddSingleton(options ?? new OutboxRelayOptions());
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OutboxRelayService>();

        return services;
    }
}
