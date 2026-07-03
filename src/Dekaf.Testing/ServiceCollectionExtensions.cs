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

        RemoveDekafClientRegistrations(services);

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

    private static void RemoveDekafClientRegistrations(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (IsDekafClientServiceType(services[i].ServiceType))
                services.RemoveAt(i);
        }
    }

    private static bool IsDekafClientServiceType(Type serviceType)
    {
        if (serviceType == typeof(IAdminClient) ||
            serviceType == typeof(IInitializableKafkaClient))
        {
            return true;
        }

        if (!serviceType.IsGenericType)
            return false;

        var definition = serviceType.GetGenericTypeDefinition();
        return definition == typeof(IKafkaProducer<,>) ||
               definition == typeof(IKafkaConsumer<,>) ||
               definition == typeof(IKafkaShareConsumer<,>);
    }
}
