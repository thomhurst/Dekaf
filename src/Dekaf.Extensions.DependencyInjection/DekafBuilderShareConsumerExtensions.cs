using System.Diagnostics.CodeAnalysis;
using Dekaf.Consumer.DeadLetter;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dekaf.Extensions.DependencyInjection;

/// <summary>Registration extensions for Kafka share consumers.</summary>
public static class DekafBuilderShareConsumerExtensions
{
    /// <summary>Adds an unkeyed singleton share consumer.</summary>
    public static DekafBuilder AddShareConsumer<TKey, TValue>(
        this DekafBuilder builder,
        Action<ShareConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return Register<TKey, TValue>(builder, null, (_, consumer) => configure(consumer), configureDeadLetterQueue);
    }

    /// <summary>Adds an unkeyed singleton share consumer.</summary>
    public static DekafBuilder AddShareConsumer<TKey, TValue>(
        this DekafBuilder builder,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return Register<TKey, TValue>(builder, null, configure, configureDeadLetterQueue);
    }

    /// <summary>Adds an unkeyed singleton share consumer.</summary>
    public static DekafBuilder AddShareConsumer<TKey, TValue>(
        this DekafBuilder builder,
        ShareConsumerOptions options,
        Action<ShareConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return Register<TKey, TValue>(builder, null, (_, consumer) =>
        {
            DekafOptionsBinding.ApplyShareConsumer(options, consumer);
            configure?.Invoke(consumer);
        }, configureDeadLetterQueue);
    }

    /// <summary>Adds an unkeyed singleton share consumer from configuration.</summary>
    [RequiresDynamicCode(DekafConfigurationBinding.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafConfigurationBinding.RequiresUnreferencedCodeMessage)]
    public static DekafBuilder AddShareConsumer<TKey, TValue>(
        this DekafBuilder builder,
        IConfiguration configuration,
        Action<ShareConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        return Register<TKey, TValue>(builder, null, (_, consumer) =>
        {
            DekafConfigurationBinding.ApplyShareConsumer(configuration, consumer);
            configure?.Invoke(consumer);
        }, configureDeadLetterQueue);
    }

    /// <summary>Adds an unkeyed singleton share consumer from configuration and the service provider.</summary>
    /// <remarks>Configuration is applied first, so <paramref name="configure"/> can override bound values.</remarks>
    [RequiresDynamicCode(DekafConfigurationBinding.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafConfigurationBinding.RequiresUnreferencedCodeMessage)]
    public static DekafBuilder AddShareConsumer<TKey, TValue>(
        this DekafBuilder builder,
        IConfiguration configuration,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);
        return Register(builder, null, ConfigureFrom(configuration, configure), configureDeadLetterQueue);
    }

    /// <summary>Adds a keyed singleton share consumer.</summary>
    public static DekafBuilder AddShareConsumer<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        Action<ShareConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);
        return Register<TKey, TValue>(builder, serviceKey, (_, consumer) => configure(consumer), configureDeadLetterQueue);
    }

    /// <summary>Adds a keyed singleton share consumer.</summary>
    public static DekafBuilder AddShareConsumer<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);
        return Register<TKey, TValue>(builder, serviceKey, configure, configureDeadLetterQueue);
    }

    /// <summary>Adds a keyed singleton share consumer.</summary>
    public static DekafBuilder AddShareConsumer<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        ShareConsumerOptions options,
        Action<ShareConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(options);
        return Register<TKey, TValue>(builder, serviceKey, (_, consumer) =>
        {
            DekafOptionsBinding.ApplyShareConsumer(options, consumer);
            configure?.Invoke(consumer);
        }, configureDeadLetterQueue);
    }

    /// <summary>Adds a keyed singleton share consumer from configuration.</summary>
    [RequiresDynamicCode(DekafConfigurationBinding.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafConfigurationBinding.RequiresUnreferencedCodeMessage)]
    public static DekafBuilder AddShareConsumer<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        IConfiguration configuration,
        Action<ShareConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configuration);
        return Register<TKey, TValue>(builder, serviceKey, (_, consumer) =>
        {
            DekafConfigurationBinding.ApplyShareConsumer(configuration, consumer);
            configure?.Invoke(consumer);
        }, configureDeadLetterQueue);
    }

    /// <summary>Adds a keyed singleton share consumer from configuration and the service provider.</summary>
    /// <remarks>Configuration is applied first, so <paramref name="configure"/> can override bound values.</remarks>
    [RequiresDynamicCode(DekafConfigurationBinding.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafConfigurationBinding.RequiresUnreferencedCodeMessage)]
    public static DekafBuilder AddShareConsumer<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        IConfiguration configuration,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);
        return Register(builder, serviceKey, ConfigureFrom(configuration, configure), configureDeadLetterQueue);
    }

    [RequiresDynamicCode(DekafConfigurationBinding.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafConfigurationBinding.RequiresUnreferencedCodeMessage)]
    internal static Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>> ConfigureFrom<TKey, TValue>(
        IConfiguration configuration,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>> configure) =>
        (provider, consumer) =>
        {
            DekafConfigurationBinding.ApplyShareConsumer(configuration, consumer);
            configure(provider, consumer);
        };

    internal static object DeadLetterOptionsKey<TKey, TValue>(object? serviceKey) =>
        new ShareConsumerOptionsKey(typeof(IKafkaShareConsumer<TKey, TValue>), serviceKey);

    private sealed record ShareConsumerOptionsKey(Type ConsumerType, object? ServiceKey);

    private static DekafBuilder Register<TKey, TValue>(
        DekafBuilder builder, object? serviceKey,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue)
    {
        var stateKey = new object();
        builder.Services.AddKeyedSingleton<Registration<TKey, TValue>>(stateKey, (provider, _) =>
        {
            var consumerBuilder = new ShareConsumerBuilder<TKey, TValue>();
            var loggerFactory = provider.GetService<ILoggerFactory>();
            if (loggerFactory is not null)
                consumerBuilder.WithLoggerFactory(loggerFactory);
            configure(provider, consumerBuilder);
            DeadLetterOptions? options = null;
            if (configureDeadLetterQueue is not null)
            {
                var deadLetterBuilder = new DeadLetterQueueBuilder();
                configureDeadLetterQueue(deadLetterBuilder);
                if (consumerBuilder.BootstrapServersString is { } servers)
                    deadLetterBuilder.WithDefaultBootstrapServers(servers);
                options = deadLetterBuilder.Build();
            }
            return new Registration<TKey, TValue>(consumerBuilder, options);
        });
        if (serviceKey is null)
        {
            builder.Services.AddSingleton<IKafkaShareConsumer<TKey, TValue>>(provider => provider
                .GetRequiredKeyedService<Registration<TKey, TValue>>(stateKey).Builder.Build());
            builder.Services.AddSingleton<IInitializableKafkaClient>(provider => provider
                .GetRequiredService<IKafkaShareConsumer<TKey, TValue>>());
        }
        else
        {
            builder.Services.AddKeyedSingleton<IKafkaShareConsumer<TKey, TValue>>(serviceKey, (provider, _) => provider
                .GetRequiredKeyedService<Registration<TKey, TValue>>(stateKey).Builder.Build());
            builder.Services.AddSingleton<IInitializableKafkaClient>(provider => provider
                .GetRequiredKeyedService<IKafkaShareConsumer<TKey, TValue>>(serviceKey));
        }
        if (configureDeadLetterQueue is not null)
            builder.Services.AddKeyedSingleton<DeadLetterOptions>(DeadLetterOptionsKey<TKey, TValue>(serviceKey),
                (provider, _) => provider.GetRequiredKeyedService<Registration<TKey, TValue>>(stateKey).DeadLetterOptions!);
        return builder;
    }

    private sealed record Registration<TKey, TValue>(
        ShareConsumerBuilder<TKey, TValue> Builder, DeadLetterOptions? DeadLetterOptions);
}
