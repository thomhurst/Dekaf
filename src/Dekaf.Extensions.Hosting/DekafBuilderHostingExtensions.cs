using System.Diagnostics.CodeAnalysis;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        builder.Services.AddHostedService<TService>();
        return builder;
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
        builder.Services.AddHostedService<TService>();
        return builder;
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
        builder.Services.AddHostedService<TService>();
        return builder;
    }
}
