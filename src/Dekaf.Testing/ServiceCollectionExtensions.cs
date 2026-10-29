using System.Diagnostics.CodeAnalysis;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Internal;
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
    /// Replaces Dekaf client registrations with in-memory test doubles backed by one shared cluster,
    /// preserving their service keys, lifetimes and hosted aliases. Singleton open-generic fallbacks
    /// are added only for client families with no unkeyed registrations, to avoid duplicate clients
    /// when enumerating services. Register all required unkeyed types before calling this method.
    /// </summary>
    [RequiresDynamicCode("Replacing closed generic clients requires constructing in-memory client types at runtime.")]
    public static IServiceCollection AddDekafInMemory(
        this IServiceCollection services,
        Action<InMemoryKafkaClusterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new InMemoryKafkaClusterOptions();
        configure?.Invoke(options);

        ReplaceDekafClientRegistrations(services);

        services.AddSingleton(options);
        services.TryAddSingleton(new InMemoryConsumerOptions());
        services.TryAddSingleton(new InMemoryShareConsumerOptions());
        services.AddSingleton<InMemoryKafkaCluster>();
        AddFallback(services, typeof(IKafkaProducer<,>), typeof(InMemoryProducer<,>));
        AddFallback(services, typeof(IKafkaConsumer<,>), typeof(InMemoryConsumer<,>));
        AddFallback(services, typeof(IKafkaShareConsumer<,>), typeof(InMemoryShareConsumer<,>));
        services.TryAddSingleton<IAdminClient, InMemoryAdminClient>();

        return services;
    }

    private static void AddFallback(IServiceCollection services, Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
    {
        foreach (var descriptor in services)
        {
            if (!descriptor.IsKeyedService && descriptor.ServiceType.IsGenericType &&
                descriptor.ServiceType.GetGenericTypeDefinition() == serviceType)
                return;
        }

        services.AddSingleton(serviceType, implementationType);
    }

    [RequiresDynamicCode("Replacing closed generic clients requires constructing in-memory client types at runtime.")]
    private static void ReplaceDekafClientRegistrations(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            // In-memory clients need no eager broker initialization.
            if (descriptor.ServiceType == typeof(IInitializableKafkaClient))
            {
                services.RemoveAt(i);
                continue;
            }

            // Hosted aliases must keep forwarding to the replaced private keyed client.
            if (descriptor is IKafkaClientAliasRegistration)
                continue;

            var implementationType = GetInMemoryImplementationType(descriptor.ServiceType);
            if (implementationType is null)
                continue;

            services[i] = descriptor.IsKeyedService
                ? ServiceDescriptor.DescribeKeyed(descriptor.ServiceType, descriptor.ServiceKey, implementationType, descriptor.Lifetime)
                : ServiceDescriptor.Describe(descriptor.ServiceType, implementationType, descriptor.Lifetime);
        }
    }

    [RequiresDynamicCode("Replacing closed generic clients requires constructing in-memory client types at runtime.")]
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private static Type? GetInMemoryImplementationType(Type serviceType)
    {
        if (serviceType == typeof(IAdminClient))
            return typeof(InMemoryAdminClient);

        if (!serviceType.IsGenericType)
            return null;

        var definition = serviceType.GetGenericTypeDefinition();
        Type implementationType;
        if (definition == typeof(IKafkaProducer<,>))
            implementationType = typeof(InMemoryProducer<,>);
        else if (definition == typeof(IKafkaConsumer<,>))
            implementationType = typeof(InMemoryConsumer<,>);
        else if (definition == typeof(IKafkaShareConsumer<,>))
            implementationType = typeof(InMemoryShareConsumer<,>);
        else
            return null;

        return serviceType.IsGenericTypeDefinition
            ? implementationType
            : implementationType.MakeGenericType(serviceType.GetGenericArguments());
    }
}
