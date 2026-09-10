using System.Diagnostics.CodeAnalysis;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dekaf.Extensions.Hosting;

/// <summary>Registers independent hosted share consumers.</summary>
public static class DekafBuilderShareConsumerHostingExtensions
{
    /// <summary>Starts an independent hosted share consumer with unkeyed configuration.</summary>
    public static DekafBuilder AddShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        this DekafBuilder builder,
        Action<ShareConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaShareConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return Register<TService, TKey, TValue>(builder, null, configureDeadLetterQueue is not null,
            registrationKey => builder.AddShareConsumer<TKey, TValue>(registrationKey, consumer =>
            {
                consumer.WithAcknowledgementMode(ShareAcknowledgementMode.Explicit);
                configure(consumer);
            }, configureDeadLetterQueue));
    }

    /// <summary>Starts an independent hosted share consumer with unkeyed configuration.</summary>
    public static DekafBuilder AddShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        this DekafBuilder builder,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaShareConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return Register<TService, TKey, TValue>(builder, null, configureDeadLetterQueue is not null,
            registrationKey => builder.AddShareConsumer<TKey, TValue>(registrationKey, (provider, consumer) =>
            {
                consumer.WithAcknowledgementMode(ShareAcknowledgementMode.Explicit);
                configure(provider, consumer);
            }, configureDeadLetterQueue));
    }

    /// <summary>Starts an independent hosted share consumer with unkeyed configuration.</summary>
    public static DekafBuilder AddShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        this DekafBuilder builder,
        ShareConsumerOptions options,
        Action<ShareConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaShareConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return Register<TService, TKey, TValue>(builder, null, configureDeadLetterQueue is not null,
            registrationKey => builder.AddShareConsumer<TKey, TValue>(registrationKey, options, configure, configureDeadLetterQueue));
    }

    /// <summary>Starts an independent hosted share consumer with unkeyed configuration.</summary>
    [RequiresDynamicCode(DekafConfigurationBinding.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafConfigurationBinding.RequiresUnreferencedCodeMessage)]
    public static DekafBuilder AddShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        this DekafBuilder builder,
        IConfiguration configuration,
        Action<ShareConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaShareConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        return Register<TService, TKey, TValue>(builder, null, configureDeadLetterQueue is not null,
            registrationKey => builder.AddShareConsumer<TKey, TValue>(registrationKey, configuration, configure, configureDeadLetterQueue));
    }

    /// <summary>Starts an independent hosted share consumer with keyed configuration.</summary>
    public static DekafBuilder AddShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        Action<ShareConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaShareConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);
        return Register<TService, TKey, TValue>(builder, serviceKey, configureDeadLetterQueue is not null,
            registrationKey => builder.AddShareConsumer<TKey, TValue>(registrationKey, consumer =>
            {
                consumer.WithAcknowledgementMode(ShareAcknowledgementMode.Explicit);
                configure(consumer);
            }, configureDeadLetterQueue));
    }

    /// <summary>Starts an independent hosted share consumer with keyed configuration.</summary>
    public static DekafBuilder AddShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaShareConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);
        return Register<TService, TKey, TValue>(builder, serviceKey, configureDeadLetterQueue is not null,
            registrationKey => builder.AddShareConsumer<TKey, TValue>(registrationKey, (provider, consumer) =>
            {
                consumer.WithAcknowledgementMode(ShareAcknowledgementMode.Explicit);
                configure(provider, consumer);
            }, configureDeadLetterQueue));
    }

    /// <summary>Starts an independent hosted share consumer with keyed configuration.</summary>
    public static DekafBuilder AddShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        ShareConsumerOptions options,
        Action<ShareConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaShareConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(options);
        return Register<TService, TKey, TValue>(builder, serviceKey, configureDeadLetterQueue is not null,
            registrationKey => builder.AddShareConsumer<TKey, TValue>(registrationKey, options, configure, configureDeadLetterQueue));
    }

    /// <summary>Starts an independent hosted share consumer with keyed configuration.</summary>
    [RequiresDynamicCode(DekafConfigurationBinding.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafConfigurationBinding.RequiresUnreferencedCodeMessage)]
    public static DekafBuilder AddShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        IConfiguration configuration,
        Action<ShareConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaShareConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configuration);
        return Register<TService, TKey, TValue>(builder, serviceKey, configureDeadLetterQueue is not null,
            registrationKey => builder.AddShareConsumer<TKey, TValue>(registrationKey, configuration, configure, configureDeadLetterQueue));
    }

    private static DekafBuilder Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        DekafBuilder builder, object? serviceKey, bool deadLetterConfigured, Action<object> registerConsumer)
        where TService : KafkaShareConsumerService<TKey, TValue>
    {
        var registrationKey = new RegistrationKey(typeof(TService), serviceKey);
        if (builder.Services.Any(descriptor => descriptor.IsKeyedService &&
                descriptor.ServiceType == typeof(RegistrationMarker) && Equals(descriptor.ServiceKey, registrationKey)))
            throw new InvalidOperationException($"{typeof(TService).Name} is already registered for this service key. Use distinct service keys.");

        registerConsumer(registrationKey);
        builder.Services.AddKeyedSingleton(registrationKey, RegistrationMarker.Instance);
        // Public aliases support application resolution. Hosted factories always use the private
        // registration key so another service with the same message types cannot replace its wiring.
        if (serviceKey is null)
            builder.Services.AddSingleton<IKafkaShareConsumer<TKey, TValue>>(provider =>
                provider.GetRequiredKeyedService<IKafkaShareConsumer<TKey, TValue>>(registrationKey));
        else
            builder.Services.AddKeyedSingleton<IKafkaShareConsumer<TKey, TValue>>(serviceKey, (provider, _) =>
                provider.GetRequiredKeyedService<IKafkaShareConsumer<TKey, TValue>>(registrationKey));

        builder.Services.AddSingleton<IHostedService>(provider =>
        {
            var consumer = provider.GetRequiredKeyedService<IKafkaShareConsumer<TKey, TValue>>(registrationKey);
            var options = deadLetterConfigured
                ? provider.GetRequiredKeyedService<DeadLetterOptions>(
                    DekafBuilderShareConsumerExtensions.DeadLetterOptionsKey<TKey, TValue>(registrationKey))
                : null;
            if (options is not null && !typeof(TService).GetConstructors().Any(static constructor =>
                    constructor.GetParameters().Any(static parameter => parameter.ParameterType == typeof(DeadLetterOptions))))
                throw new InvalidOperationException("The hosted service constructor must accept and forward DeadLetterOptions to base(...).");
            object[] arguments = options is null ? [consumer] : [consumer, options];
            var service = ActivatorUtilities.CreateInstance<TService>(provider, arguments);
            if (!ReferenceEquals(service.ConfiguredDeadLetterOptions, options))
                throw new InvalidOperationException("The hosted service must forward the matching DeadLetterOptions to base(...).");
            return service;
        });
        return builder;
    }

    private sealed record RegistrationKey(Type ServiceType, object? ServiceKey);
    private sealed class RegistrationMarker
    {
        internal static readonly RegistrationMarker Instance = new();
    }
}
