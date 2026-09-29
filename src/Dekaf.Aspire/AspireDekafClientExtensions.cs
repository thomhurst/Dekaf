using System.Diagnostics.CodeAnalysis;
using Dekaf;
using Dekaf.Admin;
using Dekaf.Aspire;
using Dekaf.Extensions.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Registers shared Dekaf root clients. Producers, consumers and admin clients created from one root share its
/// connections, metadata and memory budget.
/// </summary>
public static class AspireDekafClientExtensions
{
    private const string Role = "Client";

    /// <summary>Registers a <see cref="KafkaClient"/> as a singleton with a broker connectivity health check.</summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">The connection string name, holding comma-separated bootstrap servers.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafClientSettings"/>.</param>
    /// <param name="configureBuilder">Optional customization of the root client builder, with access to application services.</param>
    /// <remarks>
    /// Reads settings from <c>Aspire:Dekaf:Client</c> and its <paramref name="connectionName"/> subsection.
    /// Configure native options, such as security, through <paramref name="configureBuilder"/>.
    /// The host owns the root client. Applications initialize and dispose the child clients they create from it.
    /// </remarks>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddDekafClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<DekafClientSettings>? configureSettings = null,
        Action<IServiceProvider, KafkaClientBuilder>? configureBuilder = null)
        => AddClient(builder, connectionName, serviceKey: null, configureSettings, configureBuilder);

    /// <summary>Registers a <see cref="KafkaClient"/> as a keyed singleton with a broker connectivity health check.</summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="name">The service key and connection string name.</param>
    /// <param name="configureSettings">Optional customization of the <see cref="DekafClientSettings"/>.</param>
    /// <param name="configureBuilder">Optional customization of the root client builder, with access to application services.</param>
    /// <remarks>Reads settings from <c>Aspire:Dekaf:Client</c> and its <paramref name="name"/> subsection.</remarks>
    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    public static void AddKeyedDekafClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<DekafClientSettings>? configureSettings = null,
        Action<IServiceProvider, KafkaClientBuilder>? configureBuilder = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddClient(builder, name, serviceKey: name, configureSettings, configureBuilder);
    }

    [RequiresDynamicCode(DekafAspireRegistration.RequiresDynamicCodeMessage)]
    [RequiresUnreferencedCode(DekafAspireRegistration.RequiresUnreferencedCodeMessage)]
    private static void AddClient(
        IHostApplicationBuilder builder,
        string connectionName,
        string? serviceKey,
        Action<DekafClientSettings>? configureSettings,
        Action<IServiceProvider, KafkaClientBuilder>? configureBuilder)
    {
        var (settings, _) = DekafAspireRegistration.ReadSettings(
            builder, Role, connectionName, configureSettings, ["ConnectionString"]);

        KafkaClient Create(IServiceProvider services)
        {
            var client = new KafkaClientBuilder();
            if (services.GetService<ILoggerFactory>() is { } loggerFactory)
                client.WithLoggerFactory(loggerFactory);
            if (settings.ConnectionString is not null)
                client.WithBootstrapServers(settings.ConnectionString);
            configureBuilder?.Invoke(services, client);
            return client.Build();
        }

        if (serviceKey is null)
            builder.Services.AddSingleton(Create);
        else
            builder.Services.AddKeyedSingleton(serviceKey, (services, _) => Create(services));

        DekafAspireRegistration.AddTelemetry(builder, settings);

        if (!settings.DisableHealthChecks)
        {
            var healthClientKey = new object();
            var added = DekafAspireRegistration.TryAddHealthCheck(builder,
                DekafAspireRegistration.HealthCheckName("client", serviceKey),
                services => new DekafBrokerHealthCheck(
                    services.GetRequiredKeyedService<IAdminClient>(healthClientKey),
                    settings.HealthCheck));

            // The container owns this admin client, so it is disposed before the root it borrows connections from.
            if (added)
            {
                builder.Services.AddKeyedSingleton<IAdminClient>(healthClientKey, (services, _) =>
                    DekafAspireRegistration.Resolve<KafkaClient>(services, serviceKey).CreateAdminClient().Build());
            }
        }
    }
}
