using System.Diagnostics.CodeAnalysis;
using Dekaf;
using Dekaf.Aspire;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.HealthChecks;
using Dekaf.Producer;

namespace Microsoft.Extensions.Hosting;

/// <summary>Registers Dekaf producers with Aspire configuration, health checks and telemetry.</summary>
public static class AspireDekafProducerExtensions
{
    private const string Role = "Producer";

    /// <summary>Registers <see cref="IKafkaProducer{TKey, TValue}"/> as a singleton.</summary>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">The connection string name, holding comma-separated bootstrap servers.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafProducerSettings"/>.</param>
    /// <param name="configureBuilder">Optional customization of the native producer builder, with access to application services.</param>
    /// <remarks>
    /// Reads settings from <c>Aspire:Dekaf:Producer</c> and its <paramref name="connectionName"/> subsection.
    /// Native options under <c>Config</c> are applied first, then the connection string, then <paramref name="configureBuilder"/>.
    /// The host initializes the producer at startup and disposes it on shutdown.
    /// </remarks>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddDekafProducer<TKey, TValue>(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<DekafProducerSettings>? configureSettings = null,
        Action<IServiceProvider, ProducerBuilder<TKey, TValue>>? configureBuilder = null)
        => AddProducer(builder, connectionName, serviceKey: null, configureSettings, configureBuilder);

    /// <inheritdoc cref="AddDekafProducer{TKey, TValue}(IHostApplicationBuilder, string, Action{DekafProducerSettings}?, Action{IServiceProvider, ProducerBuilder{TKey, TValue}}?)"/>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddDekafProducer<TKey, TValue>(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<ProducerBuilder<TKey, TValue>> configureBuilder)
    {
        ArgumentNullException.ThrowIfNull(configureBuilder);
        AddProducer<TKey, TValue>(builder, connectionName, serviceKey: null, null, (_, producer) => configureBuilder(producer));
    }

    /// <summary>Registers <see cref="IKafkaProducer{TKey, TValue}"/> as a keyed singleton.</summary>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="name">The service key and connection string name.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafProducerSettings"/>.</param>
    /// <param name="configureBuilder">Optional customization of the native producer builder, with access to application services.</param>
    /// <remarks>Reads settings from <c>Aspire:Dekaf:Producer</c> and its <paramref name="name"/> subsection.</remarks>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddKeyedDekafProducer<TKey, TValue>(
        this IHostApplicationBuilder builder,
        string name,
        Action<DekafProducerSettings>? configureSettings = null,
        Action<IServiceProvider, ProducerBuilder<TKey, TValue>>? configureBuilder = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddProducer(builder, name, serviceKey: name, configureSettings, configureBuilder);
    }

    /// <inheritdoc cref="AddKeyedDekafProducer{TKey, TValue}(IHostApplicationBuilder, string, Action{DekafProducerSettings}?, Action{IServiceProvider, ProducerBuilder{TKey, TValue}}?)"/>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddKeyedDekafProducer<TKey, TValue>(
        this IHostApplicationBuilder builder,
        string name,
        Action<ProducerBuilder<TKey, TValue>> configureBuilder)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(configureBuilder);
        AddProducer<TKey, TValue>(builder, name, serviceKey: name, null, (_, producer) => configureBuilder(producer));
    }

    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    private static void AddProducer<TKey, TValue>(
        IHostApplicationBuilder builder,
        string connectionName,
        string? serviceKey,
        Action<DekafProducerSettings>? configureSettings,
        Action<IServiceProvider, ProducerBuilder<TKey, TValue>>? configureBuilder)
    {
        var (settings, native) = DekafAspireRegistration.ReadSettings(
            builder, Role, connectionName, configureSettings, DekafAspireRegistration.BrokerConnectionPaths);

        void Configure(IServiceProvider services, ProducerBuilder<TKey, TValue> producer)
        {
            if (settings.ConnectionString is not null)
                producer.WithBootstrapServers(settings.ConnectionString);
            configureBuilder?.Invoke(services, producer);
        }

        builder.Services.AddDekaf(dekaf =>
        {
            if (serviceKey is null)
                dekaf.AddProducer<TKey, TValue>(native, Configure);
            else
                dekaf.AddProducer<TKey, TValue>(serviceKey, native, Configure);
        });

        DekafAspireRegistration.AddTelemetry(builder, settings);

        if (!settings.DisableHealthChecks)
        {
            DekafAspireRegistration.TryAddHealthCheck(builder,
                DekafAspireRegistration.HealthCheckName<TKey, TValue>("producer", serviceKey),
                services => new DekafProducerHealthCheck<TKey, TValue>(
                    DekafAspireRegistration.Resolve<IKafkaProducer<TKey, TValue>>(services, serviceKey),
                    settings.HealthCheck));
        }
    }
}
