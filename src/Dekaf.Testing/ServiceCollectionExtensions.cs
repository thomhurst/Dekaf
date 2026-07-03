using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dekaf.Testing;

/// <summary>
/// Dependency injection helpers for in-memory Dekaf test doubles.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces Dekaf client registrations with in-memory test doubles backed by one shared cluster.
    /// </summary>
    public static IServiceCollection AddDekafInMemory(
        this IServiceCollection services,
        Action<InMemoryKafkaClusterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new InMemoryKafkaClusterOptions();
        configure?.Invoke(options);

        services.RemoveAll(typeof(IKafkaProducer<,>));
        services.RemoveAll(typeof(IKafkaConsumer<,>));
        services.RemoveAll(typeof(IKafkaShareConsumer<,>));
        services.RemoveAll<IAdminClient>();

        services.AddSingleton(options);
        services.TryAddSingleton(new InMemoryConsumerOptions());
        services.TryAddSingleton(new InMemoryShareConsumerOptions());
        services.AddSingleton<InMemoryKafkaCluster>();
        services.AddTransient(typeof(IKafkaProducer<,>), typeof(InMemoryProducer<,>));
        services.AddTransient(typeof(IKafkaConsumer<,>), typeof(InMemoryConsumer<,>));
        services.AddTransient(typeof(IKafkaShareConsumer<,>), typeof(InMemoryShareConsumer<,>));
        services.AddSingleton<IAdminClient, InMemoryAdminClient>();

        return services;
    }
}
