using System.Diagnostics.CodeAnalysis;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Producer;
using Microsoft.Extensions.Configuration;

namespace Dekaf.Extensions.DependencyInjection;

/// <summary>
/// Service-provider-aware registration extensions for <see cref="DekafBuilder"/>.
/// </summary>
public static class DekafBuilderServiceProviderExtensions
{
    /// <summary>Adds a producer configured using the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="configure">Configures the producer using the service provider.</param>
    public static DekafBuilder AddProducer<TKey, TValue>(
        this DekafBuilder builder,
        Action<IServiceProvider, ProducerBuilder<TKey, TValue>> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.AddProviderConfiguredProducer(serviceKey: null, isKeyed: false, configure);
    }

    /// <summary>Adds a producer configured from typed options and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="options">Producer options to apply.</param>
    /// <param name="configure">Additional producer configuration using the service provider.</param>
    public static DekafBuilder AddProducer<TKey, TValue>(
        this DekafBuilder builder,
        ProducerOptions options,
        Action<IServiceProvider, ProducerBuilder<TKey, TValue>> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        return AddProducer<TKey, TValue>(builder, (serviceProvider, producer) =>
        {
            DekafOptionsBinding.ApplyProducer(options, producer);
            configure(serviceProvider, producer);
        });
    }

    /// <summary>Adds a keyed producer configured using the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="serviceKey">Key used to resolve the producer through keyed DI.</param>
    /// <param name="configure">Configures the producer using the service provider.</param>
    public static DekafBuilder AddProducer<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        Action<IServiceProvider, ProducerBuilder<TKey, TValue>> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.AddProviderConfiguredProducer(serviceKey, isKeyed: true, configure);
    }

    /// <summary>Adds a keyed producer configured from typed options and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="serviceKey">Key used to resolve the producer through keyed DI.</param>
    /// <param name="options">Producer options to apply.</param>
    /// <param name="configure">Additional producer configuration using the service provider.</param>
    public static DekafBuilder AddProducer<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        ProducerOptions options,
        Action<IServiceProvider, ProducerBuilder<TKey, TValue>> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        return AddProducer<TKey, TValue>(builder, serviceKey, (serviceProvider, producer) =>
        {
            DekafOptionsBinding.ApplyProducer(options, producer);
            configure(serviceProvider, producer);
        });
    }

    /// <summary>Adds a producer configured from a configuration section and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="configuration">Configuration section using <see cref="ProducerOptions"/> property names.</param>
    /// <param name="configure">Additional producer configuration using the service provider.</param>
    [RequiresDynamicCode(DekafBuilder.ConfigurationBindingRequiresDynamicCode)]
    [RequiresUnreferencedCode(DekafBuilder.ConfigurationBindingRequiresUnreferencedCode)]
    public static DekafBuilder AddProducer<TKey, TValue>(
        this DekafBuilder builder,
        IConfiguration configuration,
        Action<IServiceProvider, ProducerBuilder<TKey, TValue>> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        return AddProducer<TKey, TValue>(builder, (serviceProvider, producer) =>
        {
            DekafConfigurationBinding.ApplyProducer(configuration, producer);
            configure(serviceProvider, producer);
        });
    }

    /// <summary>Adds a keyed producer configured from a configuration section and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="serviceKey">Key used to resolve the producer through keyed DI.</param>
    /// <param name="configuration">Configuration section using <see cref="ProducerOptions"/> property names.</param>
    /// <param name="configure">Additional producer configuration using the service provider.</param>
    [RequiresDynamicCode(DekafBuilder.ConfigurationBindingRequiresDynamicCode)]
    [RequiresUnreferencedCode(DekafBuilder.ConfigurationBindingRequiresUnreferencedCode)]
    public static DekafBuilder AddProducer<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        IConfiguration configuration,
        Action<IServiceProvider, ProducerBuilder<TKey, TValue>> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        return AddProducer<TKey, TValue>(builder, serviceKey, (serviceProvider, producer) =>
        {
            DekafConfigurationBinding.ApplyProducer(configuration, producer);
            configure(serviceProvider, producer);
        });
    }

    /// <summary>Adds a producer configured from Confluent configuration and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="configuration">The Confluent <c>ProducerConfig</c> section.</param>
    /// <param name="configure">Additional producer configuration using the service provider.</param>
    [RequiresDynamicCode(DekafBuilder.ConfigurationBindingRequiresDynamicCode)]
    [RequiresUnreferencedCode(DekafBuilder.ConfigurationBindingRequiresUnreferencedCode)]
    public static DekafBuilder AddProducerFromConfluentConfig<TKey, TValue>(
        this DekafBuilder builder,
        IConfiguration configuration,
        Action<IServiceProvider, ProducerBuilder<TKey, TValue>> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        return AddProducer<TKey, TValue>(builder, (serviceProvider, producer) =>
        {
            ConfluentConfigurationBinding.ApplyProducer(configuration, producer);
            configure(serviceProvider, producer);
        });
    }

    /// <summary>Adds a keyed producer configured from Confluent configuration and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="serviceKey">Key used to resolve the producer through keyed DI.</param>
    /// <param name="configuration">The Confluent <c>ProducerConfig</c> section.</param>
    /// <param name="configure">Additional producer configuration using the service provider.</param>
    [RequiresDynamicCode(DekafBuilder.ConfigurationBindingRequiresDynamicCode)]
    [RequiresUnreferencedCode(DekafBuilder.ConfigurationBindingRequiresUnreferencedCode)]
    public static DekafBuilder AddProducerFromConfluentConfig<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        IConfiguration configuration,
        Action<IServiceProvider, ProducerBuilder<TKey, TValue>> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        return AddProducer<TKey, TValue>(builder, serviceKey, (serviceProvider, producer) =>
        {
            ConfluentConfigurationBinding.ApplyProducer(configuration, producer);
            configure(serviceProvider, producer);
        });
    }

    /// <summary>Adds a consumer configured using the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="configure">Configures the consumer using the service provider.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration.</param>
    public static DekafBuilder AddConsumer<TKey, TValue>(
        this DekafBuilder builder,
        Action<IServiceProvider, ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.AddProviderConfiguredConsumer(
            serviceKey: null,
            isKeyed: false,
            configure,
            configureDeadLetterQueue);
    }

    /// <summary>Adds a consumer configured from typed options and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="options">Consumer options to apply.</param>
    /// <param name="configure">Additional consumer configuration using the service provider.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration.</param>
    public static DekafBuilder AddConsumer<TKey, TValue>(
        this DekafBuilder builder,
        ConsumerOptions options,
        Action<IServiceProvider, ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        return AddConsumer<TKey, TValue>(
            builder,
            (serviceProvider, consumer) =>
            {
                DekafOptionsBinding.ApplyConsumer(options, consumer);
                configure(serviceProvider, consumer);
            },
            configureDeadLetterQueue);
    }

    /// <summary>Adds a keyed consumer configured using the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="serviceKey">Key used to resolve the consumer through keyed DI.</param>
    /// <param name="configure">Configures the consumer using the service provider.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration.</param>
    public static DekafBuilder AddConsumer<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        Action<IServiceProvider, ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.AddProviderConfiguredConsumer(serviceKey, isKeyed: true, configure, configureDeadLetterQueue);
    }

    /// <summary>Adds a keyed consumer configured from typed options and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="serviceKey">Key used to resolve the consumer through keyed DI.</param>
    /// <param name="options">Consumer options to apply.</param>
    /// <param name="configure">Additional consumer configuration using the service provider.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration.</param>
    public static DekafBuilder AddConsumer<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        ConsumerOptions options,
        Action<IServiceProvider, ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        return AddConsumer<TKey, TValue>(
            builder,
            serviceKey,
            (serviceProvider, consumer) =>
            {
                DekafOptionsBinding.ApplyConsumer(options, consumer);
                configure(serviceProvider, consumer);
            },
            configureDeadLetterQueue);
    }

    /// <summary>Adds a consumer configured from Confluent configuration and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="configuration">The Confluent <c>ConsumerConfig</c> section.</param>
    /// <param name="configure">Additional consumer configuration using the service provider.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration.</param>
    [RequiresDynamicCode(DekafBuilder.ConfigurationBindingRequiresDynamicCode)]
    [RequiresUnreferencedCode(DekafBuilder.ConfigurationBindingRequiresUnreferencedCode)]
    public static DekafBuilder AddConsumerFromConfluentConfig<TKey, TValue>(
        this DekafBuilder builder,
        IConfiguration configuration,
        Action<IServiceProvider, ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        return AddConsumer<TKey, TValue>(
            builder,
            (serviceProvider, consumer) =>
            {
                ConfluentConfigurationBinding.ApplyConsumer(configuration, consumer);
                configure(serviceProvider, consumer);
            },
            configureDeadLetterQueue);
    }

    /// <summary>Adds a keyed consumer configured from Confluent configuration and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="serviceKey">Key used to resolve the consumer through keyed DI.</param>
    /// <param name="configuration">The Confluent <c>ConsumerConfig</c> section.</param>
    /// <param name="configure">Additional consumer configuration using the service provider.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration.</param>
    [RequiresDynamicCode(DekafBuilder.ConfigurationBindingRequiresDynamicCode)]
    [RequiresUnreferencedCode(DekafBuilder.ConfigurationBindingRequiresUnreferencedCode)]
    public static DekafBuilder AddConsumerFromConfluentConfig<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        IConfiguration configuration,
        Action<IServiceProvider, ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        return AddConsumer<TKey, TValue>(
            builder,
            serviceKey,
            (serviceProvider, consumer) =>
            {
                ConfluentConfigurationBinding.ApplyConsumer(configuration, consumer);
                configure(serviceProvider, consumer);
            },
            configureDeadLetterQueue);
    }

    /// <summary>Adds a consumer configured from a configuration section and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="configuration">Configuration section using <see cref="ConsumerOptions"/> property names.</param>
    /// <param name="configure">Additional consumer configuration using the service provider.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration.</param>
    [RequiresDynamicCode(DekafBuilder.ConfigurationBindingRequiresDynamicCode)]
    [RequiresUnreferencedCode(DekafBuilder.ConfigurationBindingRequiresUnreferencedCode)]
    public static DekafBuilder AddConsumer<TKey, TValue>(
        this DekafBuilder builder,
        IConfiguration configuration,
        Action<IServiceProvider, ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        return AddConsumer<TKey, TValue>(
            builder,
            (serviceProvider, consumer) =>
            {
                DekafConfigurationBinding.ApplyConsumer(configuration, consumer);
                configure(serviceProvider, consumer);
            },
            configureDeadLetterQueue);
    }

    /// <summary>Adds a keyed consumer configured from a configuration section and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="serviceKey">Key used to resolve the consumer through keyed DI.</param>
    /// <param name="configuration">Configuration section using <see cref="ConsumerOptions"/> property names.</param>
    /// <param name="configure">Additional consumer configuration using the service provider.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration.</param>
    [RequiresDynamicCode(DekafBuilder.ConfigurationBindingRequiresDynamicCode)]
    [RequiresUnreferencedCode(DekafBuilder.ConfigurationBindingRequiresUnreferencedCode)]
    public static DekafBuilder AddConsumer<TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        IConfiguration configuration,
        Action<IServiceProvider, ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        return AddConsumer<TKey, TValue>(
            builder,
            serviceKey,
            (serviceProvider, consumer) =>
            {
                DekafConfigurationBinding.ApplyConsumer(configuration, consumer);
                configure(serviceProvider, consumer);
            },
            configureDeadLetterQueue);
    }

    /// <summary>Adds an admin client configured using the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="configure">Configures the admin client using the service provider.</param>
    public static DekafBuilder AddAdminClient(
        this DekafBuilder builder,
        Action<IServiceProvider, AdminClientServiceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.AddProviderConfiguredAdminClient(configure);
    }

    /// <summary>Adds an admin client configured from typed options and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="options">Admin client options to apply.</param>
    /// <param name="configure">Additional admin client configuration using the service provider.</param>
    public static DekafBuilder AddAdminClient(
        this DekafBuilder builder,
        AdminClientOptions options,
        Action<IServiceProvider, AdminClientServiceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        return AddAdminClient(builder, (serviceProvider, admin) =>
        {
            admin.ApplyOptions(options);
            configure(serviceProvider, admin);
        });
    }

    /// <summary>Adds an admin client configured from a configuration section and the service provider.</summary>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="configuration">Configuration section using <see cref="AdminClientOptions"/> property names.</param>
    /// <param name="configure">Additional admin client configuration using the service provider.</param>
    [RequiresDynamicCode(DekafBuilder.ConfigurationBindingRequiresDynamicCode)]
    [RequiresUnreferencedCode(DekafBuilder.ConfigurationBindingRequiresUnreferencedCode)]
    public static DekafBuilder AddAdminClient(
        this DekafBuilder builder,
        IConfiguration configuration,
        Action<IServiceProvider, AdminClientServiceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configure);

        return AddAdminClient(builder, (serviceProvider, admin) =>
        {
            admin.ApplyConfiguration(configuration);
            configure(serviceProvider, admin);
        });
    }
}
