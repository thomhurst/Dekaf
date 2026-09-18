using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Dekaf.Outbox.DynamoDB;

/// <summary>
/// Dependency injection registration for the DynamoDB outbox store.
/// </summary>
public static class DynamoDbOutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DynamoDbOutboxStore"/> as the <see cref="IOutboxStore"/> and
    /// <see cref="DynamoDbOutboxWriter"/> as the <see cref="IDynamoDbOutboxWriter"/>. Register
    /// <c>AddDekafOutboxRelay</c> in the same container to publish; a process that only
    /// enqueues needs the writer alone.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Table and layout options, identical for every writer and relay.</param>
    /// <param name="clientFactory">Supplies the DynamoDB client; called once per container.
    /// Defaults to the container's <see cref="IAmazonDynamoDB"/> registration. The container
    /// does not dispose a client returned by this factory.</param>
    public static IServiceCollection AddDekafDynamoDbOutboxStore(
        this IServiceCollection services,
        DynamoDbOutboxOptions options,
        Func<IServiceProvider, IAmazonDynamoDB>? clientFactory = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        clientFactory ??= static provider => provider.GetRequiredService<IAmazonDynamoDB>();

        services.TryAddSingleton(TimeProvider.System);
        // One client for the store and the writer, however the factory makes it.
        services.TryAddSingleton(provider => new OutboxClient(clientFactory(provider)));
        services.TryAddSingleton<IOutboxStore>(provider => new DynamoDbOutboxStore(
            provider.GetRequiredService<OutboxClient>().Client, options, provider.GetRequiredService<TimeProvider>(),
            provider.GetService<ILogger<DynamoDbOutboxStore>>()));
        // The notifier exists only where a relay is registered; a writer without one still
        // enqueues, and the owning relay finds the messages by polling.
        services.TryAddSingleton<IDynamoDbOutboxWriter>(provider => new DynamoDbOutboxWriter(
            provider.GetRequiredService<OutboxClient>().Client, options, provider.GetService<IOutboxNotifier>()));

        return services;
    }

    // Not disposable, so the container leaves the caller's client alone.
    private sealed class OutboxClient(IAmazonDynamoDB client)
    {
        public IAmazonDynamoDB Client { get; } = client;
    }
}
