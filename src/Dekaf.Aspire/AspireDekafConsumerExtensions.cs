using System.Diagnostics.CodeAnalysis;
using Dekaf;
using Dekaf.Aspire;
using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>Registers Dekaf consumers with Aspire configuration, health checks and telemetry.</summary>
public static class AspireDekafConsumerExtensions
{
    private const string Role = "Consumer";

    /// <summary>Registers <see cref="IKafkaConsumer{TKey, TValue}"/> as a singleton.</summary>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">The connection string name, holding comma-separated bootstrap servers.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafConsumerSettings"/>.</param>
    /// <param name="configureBuilder">Optional customization of the native consumer builder, with access to application services.</param>
    /// <remarks>
    /// Reads settings from <c>Aspire:Dekaf:Consumer</c> and its <paramref name="connectionName"/> subsection.
    /// Native options under <c>Config</c> are applied first, then the connection string, then <paramref name="configureBuilder"/>.
    /// The host initializes the consumer at startup and disposes it on shutdown.
    /// </remarks>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddDekafConsumer<TKey, TValue>(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<DekafConsumerSettings>? configureSettings = null,
        Action<IServiceProvider, ConsumerBuilder<TKey, TValue>>? configureBuilder = null)
        => AddConsumer(builder, connectionName, serviceKey: null, configureSettings, configureBuilder);

    /// <inheritdoc cref="AddDekafConsumer{TKey, TValue}(IHostApplicationBuilder, string, Action{DekafConsumerSettings}?, Action{IServiceProvider, ConsumerBuilder{TKey, TValue}}?)"/>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddDekafConsumer<TKey, TValue>(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<ConsumerBuilder<TKey, TValue>> configureBuilder)
    {
        ArgumentNullException.ThrowIfNull(configureBuilder);
        AddConsumer<TKey, TValue>(builder, connectionName, serviceKey: null, null, (_, consumer) => configureBuilder(consumer));
    }

    /// <summary>Registers <see cref="IKafkaConsumer{TKey, TValue}"/> as a keyed singleton.</summary>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="name">The service key and connection string name.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafConsumerSettings"/>.</param>
    /// <param name="configureBuilder">Optional customization of the native consumer builder, with access to application services.</param>
    /// <remarks>Reads settings from <c>Aspire:Dekaf:Consumer</c> and its <paramref name="name"/> subsection.</remarks>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddKeyedDekafConsumer<TKey, TValue>(
        this IHostApplicationBuilder builder,
        string name,
        Action<DekafConsumerSettings>? configureSettings = null,
        Action<IServiceProvider, ConsumerBuilder<TKey, TValue>>? configureBuilder = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddConsumer(builder, name, serviceKey: name, configureSettings, configureBuilder);
    }

    /// <inheritdoc cref="AddKeyedDekafConsumer{TKey, TValue}(IHostApplicationBuilder, string, Action{DekafConsumerSettings}?, Action{IServiceProvider, ConsumerBuilder{TKey, TValue}}?)"/>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddKeyedDekafConsumer<TKey, TValue>(
        this IHostApplicationBuilder builder,
        string name,
        Action<ConsumerBuilder<TKey, TValue>> configureBuilder)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(configureBuilder);
        AddConsumer<TKey, TValue>(builder, name, serviceKey: name, null, (_, consumer) => configureBuilder(consumer));
    }

    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    private static void AddConsumer<TKey, TValue>(
        IHostApplicationBuilder builder,
        string connectionName,
        string? serviceKey,
        Action<DekafConsumerSettings>? configureSettings,
        Action<IServiceProvider, ConsumerBuilder<TKey, TValue>>? configureBuilder)
    {
        var (settings, native) = DekafAspireRegistration.ReadSettings(
            builder, Role, connectionName, configureSettings, DekafAspireRegistration.BrokerConnectionPaths);

        void Configure(IServiceProvider services, ConsumerBuilder<TKey, TValue> consumer)
        {
            if (settings.ConnectionString is not null)
                consumer.WithBootstrapServers(settings.ConnectionString);
            configureBuilder?.Invoke(services, consumer);
        }

        builder.Services.AddDekaf(dekaf =>
        {
            if (serviceKey is null)
                dekaf.AddConsumer<TKey, TValue>(native, Configure);
            else
                dekaf.AddConsumer<TKey, TValue>(serviceKey, native, Configure);
        });

        DekafAspireRegistration.AddTelemetry(builder, settings);

        if (!settings.DisableHealthChecks)
        {
            DekafAspireRegistration.TryAddHealthCheck(builder,
                DekafAspireRegistration.HealthCheckName<TKey, TValue>("consumer", serviceKey),
                services => new DekafConsumerHealthCheck<TKey, TValue>(
                    DekafAspireRegistration.Resolve<IKafkaConsumer<TKey, TValue>>(services, serviceKey),
                    settings.HealthCheck));
        }
    }
}
