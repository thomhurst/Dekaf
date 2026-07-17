using System.Diagnostics.CodeAnalysis;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dekaf.Extensions.Hosting;

/// <summary>
/// Hosting registration extensions for <see cref="DekafBuilder"/>.
/// </summary>
public static class DekafBuilderHostingExtensions
{
    /// <summary>
    /// Adds a consumer and a <see cref="KafkaConsumerService{TKey, TValue}"/> hosted service that
    /// processes it, in one call. Equivalent to <c>AddConsumer</c> followed by
    /// <c>services.AddHostedService&lt;TService&gt;()</c>.
    /// </summary>
    /// <typeparam name="TService">The hosted consumer service type.</typeparam>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="configure">Configures the full consumer builder surface.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration for the service.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static DekafBuilder AddConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        TKey, TValue>(
        this DekafBuilder builder,
        Action<ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddConsumer(configure, configureDeadLetterQueue);
        return RegisterHostedService<TService, TKey, TValue>(
            builder, serviceKey: null, configureDeadLetterQueue is not null);
    }

    /// <summary>
    /// Adds a consumer configured from typed options and a <see cref="KafkaConsumerService{TKey, TValue}"/>
    /// hosted service that processes it, in one call.
    /// </summary>
    /// <typeparam name="TService">The hosted consumer service type.</typeparam>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="options">Consumer options to apply.</param>
    /// <param name="configure">Optional additional consumer configuration.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration for the service.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static DekafBuilder AddConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        TKey, TValue>(
        this DekafBuilder builder,
        ConsumerOptions options,
        Action<ConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddConsumer(options, configure, configureDeadLetterQueue);
        return RegisterHostedService<TService, TKey, TValue>(
            builder, serviceKey: null, configureDeadLetterQueue is not null);
    }

    /// <summary>
    /// Adds a consumer configured from an <see cref="IConfiguration"/> section and a
    /// <see cref="KafkaConsumerService{TKey, TValue}"/> hosted service that processes it, in one call.
    /// </summary>
    /// <typeparam name="TService">The hosted consumer service type.</typeparam>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="configuration">Configuration section using <see cref="ConsumerOptions"/> property names.</param>
    /// <param name="configure">Optional additional consumer configuration.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration for the service.</param>
    /// <returns>The builder instance for method chaining.</returns>
    [RequiresDynamicCode(DekafConfigurationBinding.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafConfigurationBinding.RequiresUnreferencedCodeMessage)]
    public static DekafBuilder AddConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        TKey, TValue>(
        this DekafBuilder builder,
        IConfiguration configuration,
        Action<ConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddConsumer(configuration, configure, configureDeadLetterQueue);
        return RegisterHostedService<TService, TKey, TValue>(
            builder, serviceKey: null, configureDeadLetterQueue is not null);
    }

    /// <summary>
    /// Adds a keyed consumer and a <see cref="KafkaConsumerService{TKey, TValue}"/> hosted service
    /// that processes it, in one call. The service receives the keyed consumer (and keyed dead
    /// letter options, when configured) without needing <c>[FromKeyedServices]</c>, so multiple
    /// hosted services can share the same <typeparamref name="TKey"/>/<typeparamref name="TValue"/> pair.
    /// </summary>
    /// <typeparam name="TService">The hosted consumer service type.</typeparam>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="serviceKey">Key used to resolve the consumer through keyed DI.</param>
    /// <param name="configure">Configures the full consumer builder surface.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration for the service.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static DekafBuilder AddConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        Action<ConsumerBuilder<TKey, TValue>> configure,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        builder.AddConsumer(serviceKey, configure, configureDeadLetterQueue);
        return RegisterHostedService<TService, TKey, TValue>(
            builder, serviceKey, configureDeadLetterQueue is not null);
    }

    /// <summary>
    /// Adds a keyed consumer configured from typed options and a
    /// <see cref="KafkaConsumerService{TKey, TValue}"/> hosted service that processes it, in one call.
    /// </summary>
    /// <typeparam name="TService">The hosted consumer service type.</typeparam>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="serviceKey">Key used to resolve the consumer through keyed DI.</param>
    /// <param name="options">Consumer options to apply.</param>
    /// <param name="configure">Optional additional consumer configuration.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration for the service.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public static DekafBuilder AddConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        ConsumerOptions options,
        Action<ConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        builder.AddConsumer(serviceKey, options, configure, configureDeadLetterQueue);
        return RegisterHostedService<TService, TKey, TValue>(
            builder, serviceKey, configureDeadLetterQueue is not null);
    }

    /// <summary>
    /// Adds a keyed consumer configured from an <see cref="IConfiguration"/> section and a
    /// <see cref="KafkaConsumerService{TKey, TValue}"/> hosted service that processes it, in one call.
    /// </summary>
    /// <typeparam name="TService">The hosted consumer service type.</typeparam>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The Dekaf builder.</param>
    /// <param name="serviceKey">Key used to resolve the consumer through keyed DI.</param>
    /// <param name="configuration">Configuration section using <see cref="ConsumerOptions"/> property names.</param>
    /// <param name="configure">Optional additional consumer configuration.</param>
    /// <param name="configureDeadLetterQueue">Optional dead letter queue configuration for the service.</param>
    /// <returns>The builder instance for method chaining.</returns>
    [RequiresDynamicCode(DekafConfigurationBinding.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafConfigurationBinding.RequiresUnreferencedCodeMessage)]
    public static DekafBuilder AddConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        TKey, TValue>(
        this DekafBuilder builder,
        object serviceKey,
        IConfiguration configuration,
        Action<ConsumerBuilder<TKey, TValue>>? configure = null,
        Action<DeadLetterQueueBuilder>? configureDeadLetterQueue = null)
        where TService : KafkaConsumerService<TKey, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        builder.AddConsumer(serviceKey, configuration, configure, configureDeadLetterQueue);
        return RegisterHostedService<TService, TKey, TValue>(
            builder, serviceKey, configureDeadLetterQueue is not null);
    }

    private static DekafBuilder RegisterHostedService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        TKey, TValue>(
        DekafBuilder builder, object? serviceKey, bool deadLetterConfigured)
        where TService : KafkaConsumerService<TKey, TValue>
    {
        // Registering the same service type under the same key twice would either be silently
        // dropped (AddHostedService de-duplicates) or start two competing instances (the
        // factory path does not) depending on the registration shapes involved — fail loudly
        // instead. Distinct service keys intentionally remain allowed: that is how one service
        // class runs multiple instances.
        var registrationKey = new ConsumerServiceRegistrationKey(typeof(TService), serviceKey);
        if (builder.Services.Any(descriptor =>
                descriptor.IsKeyedService &&
                descriptor.ServiceType == typeof(ConsumerServiceRegistrationMarker) &&
                Equals(descriptor.ServiceKey, registrationKey)))
        {
            throw new InvalidOperationException(
                $"{typeof(TService).Name} is already registered as a hosted consumer service" +
                (serviceKey is null ? "" : $" for service key '{serviceKey}'") +
                ". Each AddConsumerService call starts an independent service instance; " +
                "use distinct service keys to run the same service class multiple times.");
        }

        builder.Services.AddKeyedSingleton(registrationKey, ConsumerServiceRegistrationMarker.Instance);

        if (serviceKey is null && !deadLetterConfigured)
        {
            builder.Services.AddHostedService<TService>();
        }
        else
        {
            // A factory is needed to hand the service its keyed consumer and/or verify the
            // registered DeadLetterOptions actually reached the base constructor. Registered
            // with AddSingleton rather than AddHostedService because the latter de-duplicates
            // by implementation type, which would drop the second registration when one service
            // class is registered under multiple service keys.
            builder.Services.AddSingleton<IHostedService>(sp =>
                CreateService<TService, TKey, TValue>(sp, serviceKey, deadLetterConfigured));
        }

        return builder;
    }

    private sealed record ConsumerServiceRegistrationKey(Type ServiceType, object? ServiceKey);

    private sealed class ConsumerServiceRegistrationMarker
    {
        public static readonly ConsumerServiceRegistrationMarker Instance = new();
    }

    private static TService CreateService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService,
        TKey, TValue>(
        IServiceProvider serviceProvider, object? serviceKey, bool deadLetterConfigured)
        where TService : KafkaConsumerService<TKey, TValue>
    {
        var explicitArguments = new List<object>(2);
        if (serviceKey is not null)
        {
            explicitArguments.Add(
                serviceProvider.GetRequiredKeyedService<IKafkaConsumer<TKey, TValue>>(serviceKey));
        }

        if (deadLetterConfigured)
        {
            // Verify up front that the service can receive the options at all, so a forgotten
            // constructor parameter produces this message instead of an activation failure —
            // and unrelated activation failures (missing DI registrations) surface unchanged.
            if (!typeof(TService).GetConstructors().Any(static ctor =>
                    ctor.GetParameters().Any(static p => p.ParameterType == typeof(DeadLetterOptions))))
            {
                throw new InvalidOperationException(DeadLetterOptionsNotForwardedMessage<TService>());
            }

            explicitArguments.Add(serviceProvider.GetRequiredKeyedService<DeadLetterOptions>(
                serviceKey ?? typeof(IKafkaConsumer<TKey, TValue>)));
        }

        var service = ActivatorUtilities.CreateInstance<TService>(serviceProvider, [.. explicitArguments]);

        if (deadLetterConfigured && service.ConfiguredDeadLetterOptions is null)
        {
            throw new InvalidOperationException(DeadLetterOptionsNotForwardedMessage<TService>());
        }

        return service;
    }

    private static string DeadLetterOptionsNotForwardedMessage<TService>() =>
        $"{typeof(TService).Name} was registered with a dead letter queue, but its constructor " +
        "did not pass DeadLetterOptions through to the KafkaConsumerService base constructor. " +
        "Add a DeadLetterOptions parameter to the constructor and forward it to base(...).";
}
