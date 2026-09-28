using System.Diagnostics.CodeAnalysis;
using Dekaf;
using Dekaf.Aspire;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.HealthChecks;
using Dekaf.Extensions.Hosting;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

/// <summary>Registers Dekaf share consumers (KIP-932) with Aspire configuration, health checks and telemetry.</summary>
/// <remarks>Share consumers require a broker with share groups enabled (Kafka 4.2 or later).</remarks>
public static class AspireDekafShareConsumerExtensions
{
    private const string Role = "ShareConsumer";

    /// <summary>Registers <see cref="IKafkaShareConsumer{TKey, TValue}"/> as a singleton.</summary>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">The connection string name, holding comma-separated bootstrap servers.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafShareConsumerSettings"/>.</param>
    /// <param name="configureBuilder">Optional customization of the native share consumer builder, with access to application services.</param>
    /// <remarks>
    /// Reads settings from <c>Aspire:Dekaf:ShareConsumer</c> and its <paramref name="connectionName"/> subsection.
    /// Native options under <c>Config</c> are applied first, then the connection string, then <paramref name="configureBuilder"/>.
    /// The host initializes the consumer at startup and disposes it on shutdown.
    /// </remarks>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddDekafShareConsumer<TKey, TValue>(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<DekafShareConsumerSettings>? configureSettings = null,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder = null)
        => AddShareConsumer(builder, connectionName, serviceKey: null, configureSettings, configureBuilder);

    /// <inheritdoc cref="AddDekafShareConsumer{TKey, TValue}(IHostApplicationBuilder, string, Action{DekafShareConsumerSettings}?, Action{IServiceProvider, ShareConsumerBuilder{TKey, TValue}}?)"/>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddDekafShareConsumer<TKey, TValue>(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<ShareConsumerBuilder<TKey, TValue>> configureBuilder)
    {
        ArgumentNullException.ThrowIfNull(configureBuilder);
        AddShareConsumer<TKey, TValue>(builder, connectionName, serviceKey: null, null, (_, consumer) => configureBuilder(consumer));
    }

    /// <summary>Registers <see cref="IKafkaShareConsumer{TKey, TValue}"/> as a keyed singleton.</summary>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="name">The service key and connection string name.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafShareConsumerSettings"/>.</param>
    /// <param name="configureBuilder">Optional customization of the native share consumer builder, with access to application services.</param>
    /// <remarks>Reads settings from <c>Aspire:Dekaf:ShareConsumer</c> and its <paramref name="name"/> subsection.</remarks>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddKeyedDekafShareConsumer<TKey, TValue>(
        this IHostApplicationBuilder builder,
        string name,
        Action<DekafShareConsumerSettings>? configureSettings = null,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddShareConsumer(builder, name, serviceKey: name, configureSettings, configureBuilder);
    }

    /// <inheritdoc cref="AddKeyedDekafShareConsumer{TKey, TValue}(IHostApplicationBuilder, string, Action{DekafShareConsumerSettings}?, Action{IServiceProvider, ShareConsumerBuilder{TKey, TValue}}?)"/>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddKeyedDekafShareConsumer<TKey, TValue>(
        this IHostApplicationBuilder builder,
        string name,
        Action<ShareConsumerBuilder<TKey, TValue>> configureBuilder)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(configureBuilder);
        AddShareConsumer<TKey, TValue>(builder, name, serviceKey: name, null, (_, consumer) => configureBuilder(consumer));
    }

    /// <summary>Registers a share consumer and a hosted <typeparamref name="TService"/> that processes its records.</summary>
    /// <typeparam name="TService">The hosted share consumer service.</typeparam>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">The connection string name, holding comma-separated bootstrap servers.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafShareConsumerSettings"/>, including dead-letter routing.</param>
    /// <param name="configureBuilder">Optional customization of the native share consumer builder, with access to application services.</param>
    /// <remarks>
    /// The service receives its own consumer, which defaults to explicit acknowledgement. Each service type and
    /// key pair gets an independent consumer and health check.
    /// </remarks>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddDekafShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<DekafShareConsumerSettings>? configureSettings = null,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder = null)
        where TService : KafkaShareConsumerService<TKey, TValue>
        => AddShareConsumerService<TService, TKey, TValue>(builder, connectionName, serviceKey: null, configureSettings, configureBuilder);

    /// <summary>Registers a keyed share consumer and a hosted <typeparamref name="TService"/> that processes its records.</summary>
    /// <typeparam name="TService">The hosted share consumer service.</typeparam>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="name">The service key and connection string name.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafShareConsumerSettings"/>, including dead-letter routing.</param>
    /// <param name="configureBuilder">Optional customization of the native share consumer builder, with access to application services.</param>
    /// <remarks>
    /// The service receives its own consumer, which defaults to explicit acknowledgement. Each service type and
    /// key pair gets an independent consumer and health check.
    /// </remarks>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddKeyedDekafShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        this IHostApplicationBuilder builder,
        string name,
        Action<DekafShareConsumerSettings>? configureSettings = null,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder = null)
        where TService : KafkaShareConsumerService<TKey, TValue>
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddShareConsumerService<TService, TKey, TValue>(builder, name, serviceKey: name, configureSettings, configureBuilder);
    }

    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    private static void AddShareConsumer<TKey, TValue>(
        IHostApplicationBuilder builder,
        string connectionName,
        string? serviceKey,
        Action<DekafShareConsumerSettings>? configureSettings,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder)
    {
        var settings = Register(builder, connectionName, configureSettings, configureBuilder,
            (dekaf, native, configure, deadLetter) =>
            {
                if (serviceKey is null)
                    dekaf.AddShareConsumerFromConfiguration(native, configure, deadLetter);
                else
                    dekaf.AddShareConsumerFromConfiguration(serviceKey, native, configure, deadLetter);
            });

        if (!settings.DisableHealthChecks)
        {
            DekafAspireRegistration.TryAddHealthCheck(builder,
                DekafAspireRegistration.HealthCheckName<TKey, TValue>("shareconsumer", serviceKey),
                services => new DekafShareConsumerHealthCheck<TKey, TValue>(
                    DekafAspireRegistration.Resolve<IKafkaShareConsumer<TKey, TValue>>(services, serviceKey)));
        }
    }

    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    private static void AddShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        IHostApplicationBuilder builder,
        string connectionName,
        string? serviceKey,
        Action<DekafShareConsumerSettings>? configureSettings,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder)
        where TService : KafkaShareConsumerService<TKey, TValue>
    {
        var settings = Register(builder, connectionName, configureSettings, configureBuilder,
            (dekaf, native, configure, deadLetter) =>
            {
                if (serviceKey is null)
                    dekaf.AddShareConsumerServiceFromConfiguration<TService, TKey, TValue>(native, configure, deadLetter);
                else
                    dekaf.AddShareConsumerServiceFromConfiguration<TService, TKey, TValue>(serviceKey, native, configure, deadLetter);
            });

        if (!settings.DisableHealthChecks)
        {
            // Resolve the worker's own consumer; the public alias can belong to another registration.
            var workerKey = KafkaShareConsumerServiceKey.For<TService>(serviceKey);
            DekafAspireRegistration.TryAddHealthCheck(builder,
                DekafAspireRegistration.HealthCheckName($"shareconsumer_service<{typeof(TService)},{typeof(TKey)},{typeof(TValue)}>", serviceKey),
                services => new DekafShareConsumerHealthCheck<TKey, TValue>(
                    DekafAspireRegistration.Resolve<IKafkaShareConsumer<TKey, TValue>>(services, workerKey)));
        }
    }

    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    private static DekafShareConsumerSettings Register<TKey, TValue>(
        IHostApplicationBuilder builder,
        string connectionName,
        Action<DekafShareConsumerSettings>? configureSettings,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder,
        Action<DekafBuilder, IConfiguration, Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>, Action<DeadLetterQueueBuilder>?> register)
    {
        var (settings, native) = DekafAspireRegistration.ReadSettings(
            builder, Role, connectionName, configureSettings, DekafAspireRegistration.BrokerConnectionPaths);

        void Configure(IServiceProvider services, ShareConsumerBuilder<TKey, TValue> consumer)
        {
            if (settings.ConnectionString is not null)
                consumer.WithBootstrapServers(settings.ConnectionString);
            configureBuilder?.Invoke(services, consumer);
        }

        builder.Services.AddDekaf(dekaf => register(dekaf, native, Configure, settings.ConfigureDeadLetterQueue));
        DekafAspireRegistration.AddTelemetry(builder, settings);
        return settings;
    }
}
