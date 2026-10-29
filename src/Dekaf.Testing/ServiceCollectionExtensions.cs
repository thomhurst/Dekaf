using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
    /// <remarks>
    /// Native AOT supports fallbacks, open-generic registrations and admin clients. Replacing an
    /// existing closed-generic client registration requires dynamic code and is not supported.
    /// </remarks>
    /// <exception cref="PlatformNotSupportedException">
    /// A closed-generic client registration requires replacement, but dynamic code is unavailable.
    /// </exception>
    public static IServiceCollection AddDekafInMemory(
        this IServiceCollection services,
        Action<InMemoryKafkaClusterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new InMemoryKafkaClusterOptions();
        configure?.Invoke(options);

        ReplaceDekafClientRegistrations(services);

        services.AddSingleton(options);
        services.TryAddSingleton(new InMemoryProducerOptions());
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

            if (descriptor is IShareConsumerConfigurationRegistration shareRegistration)
            {
                services[i] = descriptor.IsKeyedService
                    ? ServiceDescriptor.DescribeKeyed(descriptor.ServiceType, descriptor.ServiceKey,
                        (provider, _) => CreateShareConsumer(provider, implementationType, shareRegistration), descriptor.Lifetime)
                    : ServiceDescriptor.Describe(descriptor.ServiceType,
                        provider => CreateShareConsumer(provider, implementationType, shareRegistration), descriptor.Lifetime);
                continue;
            }

            services[i] = descriptor.IsKeyedService
                ? ServiceDescriptor.DescribeKeyed(descriptor.ServiceType, descriptor.ServiceKey, implementationType, descriptor.Lifetime)
                : ServiceDescriptor.Describe(descriptor.ServiceType, implementationType, descriptor.Lifetime);
        }
    }

    private static object CreateShareConsumer(IServiceProvider provider,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType,
        IShareConsumerConfigurationRegistration registration)
    {
        var options = registration.GetOptions(provider);
        var inMemoryOptions = new InMemoryShareConsumerOptions
        {
            GroupId = options.GroupId,
            AutoOffsetReset = options.AutoOffsetReset,
            AutoOffsetResetDuration = options.AutoOffsetResetDuration,
            MaxPollRecords = options.MaxPollRecords,
            AcknowledgementMode = options.AcknowledgementMode,
            DeadLetterQueueTopic = options.DeadLetterQueueTopic,
            DeadLetterQueueCopyRecord = options.DeadLetterQueueCopyRecord ?? false
        };
        return ActivatorUtilities.CreateInstance(provider, implementationType, inMemoryOptions);
    }

#if NET8_0
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "MakeGenericType is guarded by RuntimeFeature.IsDynamicCodeSupported; the .NET 8 analyzer does not recognize this guard.")]
#endif
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

        if (serviceType.IsGenericTypeDefinition)
            return implementationType;

        if (RuntimeFeature.IsDynamicCodeSupported)
            return implementationType.MakeGenericType(serviceType.GetGenericArguments());

        throw new PlatformNotSupportedException(
            "Replacing closed generic Dekaf client registrations requires dynamic code. " +
            "For Native AOT, use open-generic in-memory registrations or AddDekafInMemory without existing closed generic clients.");
    }
}
